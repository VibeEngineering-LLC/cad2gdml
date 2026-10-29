using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Cad2Gdml.Geometry;
using Cad2Gdml.Meshes;
using Cad2Gdml.Scene;

namespace Cad2Gdml.Import
{
    // Импортер STL (бинарный и ASCII)
    public static class StlImporter
    {
        // Загрузка одного STL-файла
        public static Part Load(string path, double unitToMm)
        {
            if (!File.Exists(path))
            {
                throw new InvalidDataException("Файл не найден: " + path);
            }

            long len = new FileInfo(path).Length;
            if (len == 0)
            {
                throw new InvalidDataException("Файл пуст: " + path);
            }

            Mesh mesh = null;

            if (len >= 84)
            {
                // Попытка бинарного формата
                byte[] head = new byte[84];
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    int n = fs.Read(head, 0, 84);
                    if (n == 84)
                    {
                        uint count = (uint)head[80] | ((uint)head[81] << 8) | ((uint)head[82] << 16) | ((uint)head[83] << 24);
                        if (84L + 50L * (long)count == len)
                        {
                            mesh = ReadBinary(path, count, unitToMm);
                        }
                    }
                }

                // Размер не совпал — проверяем ASCII-признаки
                if (mesh == null)
                {
                    if (LooksLikeAscii(path))
                    {
                        mesh = ReadAscii(path, unitToMm);
                    }
                    else
                    {
                        throw new InvalidDataException("STL: size mismatch");
                    }
                }
            }
            else
            {
                // Коротко для бинарного — только ASCII
                mesh = ReadAscii(path, unitToMm);
            }

            Part part = new Part();
            part.Name = Path.GetFileNameWithoutExtension(path);
            part.Mesh = mesh;
            return part;
        }

        // Загрузка всех *.stl из папки (сортировка по ordinal)
        public static IList<Part> LoadFolder(string folder, double unitToMm)
        {
            List<Part> result = new List<Part>();
            if (!Directory.Exists(folder))
            {
                return result;
            }

            string[] files = Directory.GetFiles(folder, "*.stl");
            Array.Sort(files, StringComparer.Ordinal);

            for (int i = 0; i < files.Length; i++)
            {
                result.Add(Load(files[i], unitToMm));
            }

            return result;
        }

        // Проверка: файл начинается с "solid" и содержит "facet"
        private static bool LooksLikeAscii(string path)
        {
            try
            {
                string text;
                using (StreamReader sr = new StreamReader(path, Encoding.UTF8, true))
                {
                    text = sr.ReadToEnd();
                }

                if (text.Length > 0 && text[0] == '\uFEFF')
                {
                    text = text.Substring(1);
                }

                string trimmed = text.TrimStart();
                if (trimmed.Length < 5)
                {
                    return false;
                }

                string head = trimmed.Substring(0, 5).ToLowerInvariant();
                if (head != "solid")
                {
                    return false;
                }

                return trimmed.IndexOf("facet", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return false;
            }
        }

        // Бинарный STL: 80 байт хедер + uint32 + 50 байт на треугольник
        private static Mesh ReadBinary(string path, uint count, double unitToMm)
        {
            Mesh mesh = new Mesh();
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (BinaryReader br = new BinaryReader(fs, Encoding.UTF8, false))
            {
                br.ReadBytes(80);
                br.ReadUInt32();

                for (uint i = 0; i < count; i++)
                {
                    br.ReadSingle();
                    br.ReadSingle();
                    br.ReadSingle();

                    float ax = br.ReadSingle();
                    float ay = br.ReadSingle();
                    float az = br.ReadSingle();
                    float bx = br.ReadSingle();
                    float by = br.ReadSingle();
                    float bz = br.ReadSingle();
                    float cx = br.ReadSingle();
                    float cy = br.ReadSingle();
                    float cz = br.ReadSingle();

                    br.ReadUInt16();

                    Vec3d v0 = new Vec3d((double)ax * unitToMm, (double)ay * unitToMm, (double)az * unitToMm);
                    Vec3d v1 = new Vec3d((double)bx * unitToMm, (double)by * unitToMm, (double)bz * unitToMm);
                    Vec3d v2 = new Vec3d((double)cx * unitToMm, (double)cy * unitToMm, (double)cz * unitToMm);

                    mesh.AddTriangle(v0, v1, v2);
                }
            }

            if (mesh.TriangleCount == 0)
            {
                throw new InvalidDataException("Бинарный STL: нулевое число треугольников.");
            }

            return mesh;
        }

        // ASCII STL: строки "vertex x y z"
        private static Mesh ReadAscii(string path, double unitToMm)
        {
            string text;
            using (StreamReader sr = new StreamReader(path, Encoding.UTF8, true))
            {
                text = sr.ReadToEnd();
            }

            // Убираем BOM
            if (text.Length > 0 && text[0] == '\uFEFF')
            {
                text = text.Substring(1);
            }

            string trimmed = text.TrimStart();
            if (trimmed.Length < 5)
            {
                throw new InvalidDataException("Не распознан формат STL: файл слишком короткий.");
            }

            string head = trimmed.Substring(0, 5).ToLowerInvariant();
            if (head != "solid")
            {
                throw new InvalidDataException("Не распознан формат STL: ожидается заголовок 'solid'.");
            }

            Mesh mesh = new Mesh();
            Vec3d a = new Vec3d(0, 0, 0);
            Vec3d b = new Vec3d(0, 0, 0);
            Vec3d c = new Vec3d(0, 0, 0);
            int slot = 0;

            string[] lines = text.Split('\n');
            for (int li = 0; li < lines.Length; li++)
            {
                string line = lines[li].TrimEnd('\r').TrimStart();

                if (line.Length < 6)
                {
                    continue;
                }

                // Проверяем начало "vertex"
                string kw = line.Substring(0, 6).ToLowerInvariant();
                if (kw != "vertex")
                {
                    continue;
                }

                string rest = line.Substring(6).TrimStart();
                if (rest.Length == 0)
                {
                    throw new InvalidDataException("STL line " + (li + 1) + ": пустая строка vertex");
                }

                string[] tokens = rest.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 3)
                {
                    throw new InvalidDataException("STL line " + (li + 1) + ": недостаточно чисел в vertex");
                }

                double x, y, z;
                try
                {
                    x = double.Parse(tokens[0], NumberStyles.Float, CultureInfo.InvariantCulture);
                    y = double.Parse(tokens[1], NumberStyles.Float, CultureInfo.InvariantCulture);
                    z = double.Parse(tokens[2], NumberStyles.Float, CultureInfo.InvariantCulture);
                }
                catch (FormatException)
                {
                    throw new InvalidDataException("STL line " + (li + 1) + ": некорректное число");
                }
                catch (OverflowException)
                {
                    throw new InvalidDataException("STL line " + (li + 1) + ": число вне диапазона");
                }

                // Защита от NaN / Infinity
                if (double.IsNaN(x) || double.IsNaN(y) || double.IsNaN(z) ||
                    double.IsInfinity(x) || double.IsInfinity(y) || double.IsInfinity(z))
                {
                    throw new InvalidDataException("STL line " + (li + 1) + ": NaN или Infinity");
                }

                Vec3d v = new Vec3d(x * unitToMm, y * unitToMm, z * unitToMm);

                if (slot == 0)
                {
                    a = v;
                }
                else if (slot == 1)
                {
                    b = v;
                }
                else
                {
                    c = v;
                    mesh.AddTriangle(a, b, c);
                }

                slot++;
                if (slot > 2)
                {
                    slot = 0;
                }
            }

            // Нечётный хвост — неполный треугольник
            if (slot > 0)
            {
                throw new InvalidDataException("STL: неполный треугольник на конце файла (" + slot + " вершин(ы) без третьей)");
            }

            if (mesh.TriangleCount == 0)
            {
                throw new InvalidDataException("ASCII STL: не найдено ни одного треугольника.");
            }

            return mesh;
        }
    }
}
