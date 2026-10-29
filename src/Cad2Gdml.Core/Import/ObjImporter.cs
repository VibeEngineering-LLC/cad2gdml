using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Cad2Gdml.Geometry;
using Cad2Gdml.Meshes;
using Cad2Gdml.Scene;

namespace Cad2Gdml.Import
{
    // Импорт OBJ-файла в список деталей (масштаб в мм)
    public static class ObjImporter
    {
        public static IList<Part> Load(string path, double unitToMm)
        {
            string fileName = Path.GetFileNameWithoutExtension(path);
            List<Part> result = new List<Part>();
            List<Vec3d> globalVerts = new List<Vec3d>();

            // Состояние текущей группы
            string currentName = fileName;
            string currentMat = null;
            List<int[]> currentFaces = new List<int[]>();
            int totalFaceCount = 0;
            int lineNo = 0;

            // Завершить текущую группу (пропуск пустых)
            void FinalizeCurrent()
            {
                if (currentFaces.Count == 0)
                {
                    currentMat = null;
                    return;
                }

                Part part = new Part();
                part.Name = currentName;
                part.OrigName = currentName;
                part.SourceFile = path;
                part.SourceMaterial = currentMat;
                currentMat = null;

                Mesh mesh = new Mesh();
                Dictionary<int, int> vertMap = new Dictionary<int, int>();

                for (int fi = 0; fi < currentFaces.Count; fi++)
                {
                    int[] tri = currentFaces[fi];
                    int l0, l1, l2;

                    if (!vertMap.TryGetValue(tri[0], out l0))
                    {
                        l0 = mesh.Vertices.Count;
                        mesh.Vertices.Add(globalVerts[tri[0]]);
                        vertMap[tri[0]] = l0;
                    }
                    if (!vertMap.TryGetValue(tri[1], out l1))
                    {
                        l1 = mesh.Vertices.Count;
                        mesh.Vertices.Add(globalVerts[tri[1]]);
                        vertMap[tri[1]] = l1;
                    }
                    if (!vertMap.TryGetValue(tri[2], out l2))
                    {
                        l2 = mesh.Vertices.Count;
                        mesh.Vertices.Add(globalVerts[tri[2]]);
                        vertMap[tri[2]] = l2;
                    }

                    mesh.Indices.Add(l0);
                    mesh.Indices.Add(l1);
                    mesh.Indices.Add(l2);
                }

                part.Mesh = mesh;
                result.Add(part);
                currentFaces.Clear();
            }

            using (StreamReader reader = new StreamReader(path))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    lineNo++;
                    string trimmed = line.TrimStart();
                    if (trimmed.Length == 0)
                    {
                        continue;
                    }
                    if (trimmed[0] == '#')
                    {
                        continue; // комментарий
                    }

                    // Извлечь тег и аргументы
                    int spIdx = trimmed.IndexOf(' ');
                    if (spIdx < 0)
                    {
                        spIdx = trimmed.IndexOf('\t');
                    }
                    if (spIdx < 0)
                    {
                        continue; // тег без аргументов — не наш случай
                    }

                    string tag = trimmed.Substring(0, spIdx);
                    string args = trimmed.Substring(spIdx + 1).Trim();

                    if (tag == "v")
                    {
                        // Вершина: v x y z [w]
                        string[] tok = args.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (tok.Length < 3)
                        {
                            throw new InvalidDataException("Строка " + lineNo + ": вершина требует минимум 3 координаты.");
                        }

                        double x = ParseCoord(tok[0], lineNo) * unitToMm;
                        double y = ParseCoord(tok[1], lineNo) * unitToMm;
                        double z = ParseCoord(tok[2], lineNo) * unitToMm;
                        globalVerts.Add(new Vec3d(x, y, z));
                    }
                    else if (tag == "o" || tag == "g")
                    {
                        // Новая часть
                        FinalizeCurrent();
                        currentName = args.Length > 0 ? args : fileName;
                    }
                    else if (tag == "usemtl")
                    {
                        // Первый usemtl в группе задаёт материал
                        if (currentMat == null && args.Length > 0)
                        {
                            currentMat = args;
                        }
                    }
                    else if (tag == "f")
                    {
                        // Грани: f v | v/vt | v//vn | v/vt/vn
                        string[] tok = args.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (tok.Length < 3)
                        {
                            throw new InvalidDataException("Строка " + lineNo + ": грань требует минимум 3 вершины.");
                        }

                        int vertCount = globalVerts.Count;
                        int[] vIdx = new int[tok.Length];
                        for (int pi = 0; pi < tok.Length; pi++)
                        {
                            // Номер вершины — часть до первого '/'
                            int slashPos = tok[pi].IndexOf('/');
                            string numStr = slashPos >= 0 ? tok[pi].Substring(0, slashPos) : tok[pi];
                            int raw = int.Parse(numStr, NumberStyles.Integer, CultureInfo.InvariantCulture);
                            if (raw > 0)
                            {
                                if (raw > vertCount)
                                {
                                    throw new InvalidDataException(
                                        "Строка " + lineNo + ": индекс " + raw + " вне диапазона 1.." + vertCount + ".");
                                }
                                vIdx[pi] = raw - 1;
                            }
                            else
                            {
                                if (raw < -vertCount)
                                {
                                    throw new InvalidDataException(
                                        "Строка " + lineNo + ": относительный индекс " + raw + " вне диапазона -" + vertCount + "..-1.");
                                }
                                vIdx[pi] = vertCount + raw;
                            }
                        }

                        // Fan-триангуляция полигона
                        for (int ti = 1; ti < tok.Length - 1; ti++)
                        {
                            currentFaces.Add(new int[] { vIdx[0], vIdx[ti], vIdx[ti + 1] });
                            totalFaceCount++;
                        }
                    }
                    // vt, vn, mtllib — игнорируем
                }
            }

            // Последняя группа
            FinalizeCurrent();

            if (totalFaceCount == 0)
            {
                throw new InvalidDataException("Файл не содержит ни одной грани.");
            }

            return result;
        }

        // Разбор числа координаты; бросает InvalidDataException при ошибке
        private static double ParseCoord(string s, int lineNo)
        {
            double val;
            if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out val))
            {
                throw new InvalidDataException("Строка " + lineNo + ": некорректное число '" + s + "'.");
            }
            if (double.IsNaN(val) || double.IsInfinity(val))
            {
                throw new InvalidDataException("Строка " + lineNo + ": недопустимое значение '" + s + "'.");
            }
            return val;
        }
    }
}
