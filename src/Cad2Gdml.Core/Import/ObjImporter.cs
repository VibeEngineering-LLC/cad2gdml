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
            List<int[]> currentFaces = new List<int[]>();

            // Завершить текущую группу (пропуск пустых)
            void FinalizeCurrent()
            {
                if (currentFaces.Count == 0)
                {
                    return;
                }

                Part part = new Part();
                part.Name = currentName;
                part.OrigName = currentName;
                part.SourceFile = path;

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
                    line = line.TrimStart();
                    if (line.Length == 0)
                    {
                        continue;
                    }
                    if (line[0] == '#')
                    {
                        continue; // комментарий
                    }

                    // Извлечь тег и аргументы
                    int spIdx = line.IndexOf(' ');
                    if (spIdx < 0)
                    {
                        spIdx = line.IndexOf('\t');
                    }
                    if (spIdx < 0)
                    {
                        continue; // тег без аргументов — не наш случай
                    }

                    string tag = line.Substring(0, spIdx);
                    string args = line.Substring(spIdx + 1).Trim();

                    if (tag == "v")
                    {
                        // Вершина: v x y z [w]
                        string[] tok = args.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (tok.Length >= 3)
                        {
                            double x = double.Parse(tok[0], CultureInfo.InvariantCulture) * unitToMm;
                            double y = double.Parse(tok[1], CultureInfo.InvariantCulture) * unitToMm;
                            double z = double.Parse(tok[2], CultureInfo.InvariantCulture) * unitToMm;
                            globalVerts.Add(new Vec3d(x, y, z));
                        }
                    }
                    else if (tag == "o" || tag == "g")
                    {
                        // Новая часть
                        FinalizeCurrent();
                        currentName = args.Length > 0 ? args : fileName;
                    }
                    else if (tag == "f")
                    {
                        // Грани: f v | v/vt | v//vn | v/vt/vn
                        string[] tok = args.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (tok.Length < 3)
                        {
                            continue; // минимум три вершины
                        }

                        int vertCount = globalVerts.Count;
                        int[] vIdx = new int[tok.Length];
                        for (int pi = 0; pi < tok.Length; pi++)
                        {
                            // Номер вершины — часть до первого '/'
                            int slashPos = tok[pi].IndexOf('/');
                            string numStr = slashPos >= 0 ? tok[pi].Substring(0, slashPos) : tok[pi];
                            int raw = int.Parse(numStr, CultureInfo.InvariantCulture);
                            if (raw > 0)
                            {
                                vIdx[pi] = raw - 1;
                            }
                            else
                            {
                                vIdx[pi] = vertCount + raw; // отрицательный — от конца
                            }
                        }

                        // Fan-триангуляция полигона
                        for (int ti = 1; ti < tok.Length - 1; ti++)
                        {
                            currentFaces.Add(new int[] { vIdx[0], vIdx[ti], vIdx[ti + 1] });
                        }
                    }
                    // vt, vn, usemtl, mtllib — игнорируем
                }
            }

            // Последняя группа
            FinalizeCurrent();

            return result;
        }
    }
}
