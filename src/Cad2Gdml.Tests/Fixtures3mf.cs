using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Cad2Gdml.Tests
{
    public static class Fixtures3mf
    {
        // Минимальный 3MF: ZIP с content-types, rels и моделью
        public static void Write3Mf(string path, IList<double[]> triangles, string unit, string objectName)
        {
            // Де-дупликация вершин по точным координатам
            var vmap = BuildVertexMap(triangles);
            int vcount = vmap.Value.Count / 3;

            // XML модели
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
            sb.Append("<model unit=\"").Append(X(unit)).Append("\" xmlns=\"http://schemas.microsoft.com/3dmanufacturing/core/2015/02\">\n");
            sb.Append("  <resources>\n");
            sb.Append("    <object id=\"1\" type=\"model\" name=\"").Append(X(objectName)).Append("\">\n");
            sb.Append("      <mesh>\n");
            sb.Append("        <vertices>\n");
            var flat = vmap.Value;
            for (int i = 0; i < flat.Count; i += 3)
            {
                sb.Append("          <vertex x=\"").Append(R(flat[i]))
                  .Append("\" y=\"").Append(R(flat[i + 1]))
                  .Append("\" z=\"").Append(R(flat[i + 2])).Append("\"/>\n");
            }
            sb.Append("        </vertices>\n");
            sb.Append("        <triangles>\n");
            foreach (var t in triangles)
            {
                int i0 = Lookup(vmap, t[0], t[1], t[2]);
                int i1 = Lookup(vmap, t[3], t[4], t[5]);
                int i2 = Lookup(vmap, t[6], t[7], t[8]);
                sb.Append("          <triangle v1=\"").Append(i0.ToString(CultureInfo.InvariantCulture))
                  .Append("\" v2=\"").Append(i1.ToString(CultureInfo.InvariantCulture))
                  .Append("\" v3=\"").Append(i2.ToString(CultureInfo.InvariantCulture)).Append("\"/>\n");
            }
            sb.Append("        </triangles>\n");
            sb.Append("      </mesh>\n");
            sb.Append("    </object>\n");
            sb.Append("  </resources>\n");
            sb.Append("  <build>\n");
            sb.Append("    <item objectid=\"1\"/>\n");
            sb.Append("  </build>\n");
            sb.Append("</model>\n");

            string contentTypes =
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
                "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">\n" +
                "  <Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>\n" +
                "  <Default Extension=\"model\" ContentType=\"application/vnd.ms-package.3dmanufacturing-3dmodel+xml\"/>\n" +
                "</Types>\n";

            string rels =
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">\n" +
                "  <Relationship Id=\"rel0\" Type=\"http://schemas.microsoft.com/3dmanufacturing/2013/01/3dmodel\" Target=\"/3D/3dmodel.model\"/>\n" +
                "</Relationships>\n";

            // Пакет ZIP (перезапись), метод «store»
            byte[][] parts = {
                Encoding.UTF8.GetBytes(contentTypes),
                Encoding.UTF8.GetBytes(rels),
                Encoding.UTF8.GetBytes(sb.ToString())
            };
            string[] names = { "[Content_Types].xml", "_rels/.rels", "3D/3dmodel.model" };
            WriteZipStored(path, names, parts);
        }

        // Запись OBJ с де-дупликацией вершин
        public static void WriteObj(string path, IList<double[]> triangles, string groupName)
        {
            var vmap = BuildVertexMap(triangles);
            var flat = vmap.Value;

            var sb = new StringBuilder();
            sb.Append("o ").Append(groupName).Append('\n');
            for (int i = 0; i < flat.Count; i += 3)
            {
                sb.Append("v ").Append(R(flat[i])).Append(' ')
                  .Append(R(flat[i + 1])).Append(' ')
                  .Append(R(flat[i + 2])).Append('\n');
            }
            foreach (var t in triangles)
            {
                // 1-based индексы
                int i0 = Lookup(vmap, t[0], t[1], t[2]) + 1;
                int i1 = Lookup(vmap, t[3], t[4], t[5]) + 1;
                int i2 = Lookup(vmap, t[6], t[7], t[8]) + 1;
                sb.Append("f ").Append(i0.ToString(CultureInfo.InvariantCulture)).Append(' ')
                  .Append(i1.ToString(CultureInfo.InvariantCulture)).Append(' ')
                  .Append(i2.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        // Карта: тройка координат -> индекс
        private struct VKey
        {
            public double X;
            public double Y;
            public double Z;
            public VKey(double x, double y, double z) { X = x; Y = y; Z = z; }
            public override bool Equals(object obj)
            {
                if (!(obj is VKey)) return false;
                VKey o = (VKey)obj;
                return X == o.X && Y == o.Y && Z == o.Z;
            }
            public override int GetHashCode()
            {
                int h = X.GetHashCode();
                h = h * 397 ^ Y.GetHashCode();
                h = h * 397 ^ Z.GetHashCode();
                return h;
            }
        }

        private struct VertexMap
        {
            public Dictionary<VKey, int> Map;
            public List<double> Value; // x,y,z,x,y,z,...
        }

        private static VertexMap BuildVertexMap(IList<double[]> triangles)
        {
            var map = new Dictionary<VKey, int>();
            var flat = new List<double>(triangles.Count * 9);
            foreach (var t in triangles)
            {
                for (int vi = 0; vi < 3; vi++)
                {
                    double x = t[vi * 3];
                    double y = t[vi * 3 + 1];
                    double z = t[vi * 3 + 2];
                    var key = new VKey(x, y, z);
                    int idx;
                    if (!map.TryGetValue(key, out idx))
                    {
                        idx = flat.Count / 3;
                        map[key] = idx;
                        flat.Add(x);
                        flat.Add(y);
                        flat.Add(z);
                    }
                }
            }
            VertexMap vm;
            vm.Map = map;
            vm.Value = flat;
            return vm;
        }

        private static int Lookup(VertexMap vm, double x, double y, double z)
        {
            int idx;
            vm.Map.TryGetValue(new VKey(x, y, z), out idx);
            return idx;
        }

        // Число в текст "R"
        private static string R(double v)
        {
            return v.ToString("R", CultureInfo.InvariantCulture);
        }

        // Экранирование XML-атрибута
        private static string X(string s)
        {
            if (s == null) return string.Empty;
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        // Таблица CRC-32
        private static readonly uint[] CrcTable = BuildCrcTable();

        private static uint[] BuildCrcTable()
        {
            var table = new uint[256];
            for (int i = 0; i < 256; i++)
            {
                uint c = (uint)i;
                for (int k = 0; k < 8; k++)
                {
                    if ((c & 1u) != 0u) c = 0xEDB88320u ^ (c >> 1);
                    else c >>= 1;
                }
                table[i] = c;
            }
            return table;
        }

        private static uint Crc32(byte[] data)
        {
            uint crc = 0xFFFFFFFFu;
            for (int i = 0; i < data.Length; i++)
            {
                crc = CrcTable[(crc ^ data[i]) & 0xFFu] ^ (crc >> 8);
            }
            return crc ^ 0xFFFFFFFFu;
        }

        // Ручная запись ZIP (метод 0 — stored)
        private static void WriteZipStored(string path, string[] names, byte[][] contents)
        {
            int n = names.Length;
            int[] offsets = new int[n];
            uint[] crcs = new uint[n];
            int[] sizes = new int[n];

            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs))
            {
                // Локальные заголовки + данные
                for (int i = 0; i < n; i++)
                {
                    byte[] nameB = Encoding.UTF8.GetBytes(names[i]);
                    byte[] data = contents[i];
                    offsets[i] = (int)fs.Position;
                    crcs[i] = Crc32(data);
                    sizes[i] = data.Length;

                    bw.Write(0x04034b50);       // подпись
                    bw.Write((ushort)20);       // версия
                    bw.Write((ushort)0);        // флаги
                    bw.Write((ushort)0);        // метод: stored
                    bw.Write((ushort)0);        // время
                    bw.Write((ushort)0);        // дата
                    bw.Write((uint)crcs[i]);
                    bw.Write((uint)sizes[i]);   // сжатый размер
                    bw.Write((uint)sizes[i]);   // несжатый размер
                    bw.Write((ushort)nameB.Length);
                    bw.Write((ushort)0);        // extra
                    bw.Write(nameB);
                    bw.Write(data);
                }

                // Центральный каталог
                int cdOffset = (int)fs.Position;
                int cdSize = 0;
                for (int i = 0; i < n; i++)
                {
                    byte[] nameB = Encoding.UTF8.GetBytes(names[i]);
                    bw.Write(0x02014b50);
                    bw.Write((ushort)20);
                    bw.Write((ushort)20);
                    bw.Write((ushort)0);
                    bw.Write((ushort)0);
                    bw.Write((ushort)0);
                    bw.Write((ushort)0);
                    bw.Write((uint)crcs[i]);
                    bw.Write((uint)sizes[i]);
                    bw.Write((uint)sizes[i]);
                    bw.Write((ushort)nameB.Length);
                    bw.Write((ushort)0);
                    bw.Write((ushort)0);
                    bw.Write((ushort)0);
                    bw.Write((ushort)0);
                    bw.Write((uint)0);
                    bw.Write((uint)offsets[i]);
                    bw.Write(nameB);
                    cdSize += 46 + nameB.Length;
                }

                // Конец центрального каталога
                bw.Write(0x06054b50);
                bw.Write((ushort)0);
                bw.Write((ushort)0);
                bw.Write((ushort)n);
                bw.Write((ushort)n);
                bw.Write((uint)cdSize);
                bw.Write((uint)cdOffset);
                bw.Write((ushort)0);
            }
        }
    }
}
