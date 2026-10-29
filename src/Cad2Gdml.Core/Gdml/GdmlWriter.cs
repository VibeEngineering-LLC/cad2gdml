using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Cad2Gdml.Scene;
using Cad2Gdml.Materials;
using Cad2Gdml.Meshes;
using Cad2Gdml.Geometry;

namespace Cad2Gdml.Gdml
{
    // Результат записи GDML
    public sealed class GdmlWriteResult
    {
        public int Volumes;
        public int Tessellated;
        public int Triangles;
        public int Tubes;
        public int Polycones;
        public int Boxes;
        public string Sha256;
    }

    // Генерация ASCII-имён GDML
    public static class GdmlName
    {
        public static string Make(string raw, HashSet<string> used)
        {
            string name = Transliterate(raw);
            if (string.IsNullOrEmpty(name)) name = "unnamed";

            // Схлопываем повторяющиеся подчёркивания
            while (name.IndexOf("__", StringComparison.Ordinal) >= 0)
                name = name.Replace("__", "_");
            name = name.Trim('_');
            if (string.IsNullOrEmpty(name)) name = "unnamed";

            // Префикс при цифре
            if (char.IsDigit(name[0])) name = "p" + name;

            // Запрещаем "0x"
            while (name.IndexOf("0x", StringComparison.Ordinal) >= 0)
                name = name.Replace("0x", "0_x");

            // Обрезка до 64
            if (name.Length > 64) name = name.Substring(0, 64);
            while (name.IndexOf("__", StringComparison.Ordinal) >= 0)
                name = name.Replace("__", "_");
            name = name.TrimEnd('_');
            if (string.IsNullOrEmpty(name)) name = "unnamed";

            // Уникальность
            if (!used.Contains(name))
            {
                used.Add(name);
                return name;
            }

            int i = 2;
            string candidate;
            do
            {
                string suffix = "_" + i.ToString(CultureInfo.InvariantCulture);
                candidate = (name.Length + suffix.Length > 64 ? name.Substring(0, 64 - suffix.Length) : name) + suffix;
                i++;
            }
            while (used.Contains(candidate));

            used.Add(candidate);
            return candidate;
        }

        // Транслитерация и очистка: только [A-Za-z0-9_]
        private static string Transliterate(string s)
        {
            if (s == null) return string.Empty;
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c >= '\u0400' && c <= '\u04FF')
                {
                    sb.Append(CyrToLatin(c));
                }
                else if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_')
                {
                    sb.Append(c);
                }
                else
                {
                    sb.Append('_');
                }
            }
            return sb.ToString();
        }

        private static string CyrToLatin(char c)
        {
            if (c == '\u0401') return "Yo";
            if (c == '\u0451') return "yo";

            if (c >= '\u0410' && c <= '\u042F')
            {
                int idx = c - '\u0410';
                string[] arr = {
                    "A","B","V","G","D","E","Zh","Z","I","Y","K","L","M","N","O","P","R","S","T","U",
                    "F","H","Ts","Ch","Sh","Shch","","Y","","E","Yu","Ya"
                };
                return idx < arr.Length ? arr[idx] : "";
            }

            if (c >= '\u0430' && c <= '\u044F')
            {
                int idx = c - '\u0430';
                string[] arr = {
                    "a","b","v","g","d","e","zh","z","i","y","k","l","m","n","o","p","r","s","t","u",
                    "f","h","ts","ch","sh","shch","","y","","e","yu","ya"
                };
                return idx < arr.Length ? arr[idx] : "";
            }

            return "_";
        }
    }

    // Запись GDML-файла
    public static class GdmlWriter
    {
        // Таблица Z по символам
        private static readonly Dictionary<string, int> ElZ = BuildZ();
        // Таблица A по символам
        private static readonly Dictionary<string, double> ElA = BuildA();

        private static Dictionary<string, int> BuildZ()
        {
            var d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            Add(d, "H", 1); Add(d, "He", 2); Add(d, "Li", 3); Add(d, "Be", 4);
            Add(d, "B", 5); Add(d, "C", 6); Add(d, "N", 7); Add(d, "O", 8);
            Add(d, "F", 9); Add(d, "Ne", 10); Add(d, "Na", 11); Add(d, "Mg", 12);
            Add(d, "Al", 13); Add(d, "Si", 14); Add(d, "P", 15); Add(d, "S", 16);
            Add(d, "Cl", 17); Add(d, "Ar", 18); Add(d, "K", 19); Add(d, "Ca", 20);
            Add(d, "Sc", 21); Add(d, "Ti", 22); Add(d, "V", 23); Add(d, "Cr", 24);
            Add(d, "Mn", 25); Add(d, "Fe", 26); Add(d, "Co", 27); Add(d, "Ni", 28);
            Add(d, "Cu", 29); Add(d, "Zn", 30); Add(d, "Ga", 31); Add(d, "Ge", 32);
            Add(d, "As", 33); Add(d, "Se", 34); Add(d, "Br", 35); Add(d, "Kr", 36);
            Add(d, "Rb", 37); Add(d, "Sr", 38); Add(d, "Y", 39); Add(d, "Zr", 40);
            Add(d, "Nb", 41); Add(d, "Mo", 42); Add(d, "Tc", 43); Add(d, "Ru", 44);
            Add(d, "Rh", 45); Add(d, "Pd", 46); Add(d, "Ag", 47); Add(d, "Cd", 48);
            Add(d, "In", 49); Add(d, "Sn", 50); Add(d, "Sb", 51); Add(d, "Te", 52);
            Add(d, "I", 53); Add(d, "Xe", 54); Add(d, "Cs", 55); Add(d, "Ba", 56);
            Add(d, "La", 57); Add(d, "Ce", 58); Add(d, "Pr", 59); Add(d, "Nd", 60);
            Add(d, "Pm", 61); Add(d, "Sm", 62); Add(d, "Eu", 63); Add(d, "Gd", 64);
            Add(d, "Tb", 65); Add(d, "Dy", 66); Add(d, "Ho", 67); Add(d, "Er", 68);
            Add(d, "Tm", 69); Add(d, "Yb", 70); Add(d, "Lu", 71); Add(d, "Hf", 72);
            Add(d, "Ta", 73); Add(d, "W", 74); Add(d, "Re", 75); Add(d, "Os", 76);
            Add(d, "Ir", 77); Add(d, "Pt", 78); Add(d, "Au", 79); Add(d, "Hg", 80);
            Add(d, "Tl", 81); Add(d, "Pb", 82); Add(d, "Bi", 83); Add(d, "Po", 84);
            Add(d, "At", 85); Add(d, "Rn", 86); Add(d, "Fr", 87); Add(d, "Ra", 88);
            Add(d, "Ac", 89); Add(d, "Th", 90); Add(d, "Pa", 91); Add(d, "U", 92);
            return d;
        }

        private static Dictionary<string, double> BuildA()
        {
            var d = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            AddA(d, "H", 1.008); AddA(d, "He", 4.00260); AddA(d, "Li", 6.94);
            AddA(d, "Be", 9.0122); AddA(d, "B", 10.81); AddA(d, "C", 12.011);
            AddA(d, "N", 14.007); AddA(d, "O", 15.999); AddA(d, "F", 18.998);
            AddA(d, "Ne", 20.180); AddA(d, "Na", 22.990); AddA(d, "Mg", 24.305);
            AddA(d, "Al", 26.982); AddA(d, "Si", 28.085); AddA(d, "P", 30.974);
            AddA(d, "S", 32.065); AddA(d, "Cl", 35.453); AddA(d, "Ar", 39.948);
            AddA(d, "K", 39.098); AddA(d, "Ca", 40.078); AddA(d, "Sc", 44.956);
            AddA(d, "Ti", 47.867); AddA(d, "V", 50.942); AddA(d, "Cr", 51.996);
            AddA(d, "Mn", 54.938); AddA(d, "Fe", 55.845); AddA(d, "Co", 58.933);
            AddA(d, "Ni", 58.693); AddA(d, "Cu", 63.546); AddA(d, "Zn", 65.38);
            AddA(d, "Ga", 69.723); AddA(d, "Ge", 72.630); AddA(d, "As", 74.922);
            AddA(d, "Se", 78.971); AddA(d, "Br", 79.904); AddA(d, "Kr", 83.798);
            AddA(d, "Rb", 85.468); AddA(d, "Sr", 87.62); AddA(d, "Y", 88.906);
            AddA(d, "Zr", 91.224); AddA(d, "Nb", 92.906); AddA(d, "Mo", 95.95);
            AddA(d, "Tc", 98.0); AddA(d, "Ru", 101.07); AddA(d, "Rh", 102.91);
            AddA(d, "Pd", 106.42); AddA(d, "Ag", 107.87); AddA(d, "Cd", 112.41);
            AddA(d, "In", 114.82); AddA(d, "Sn", 118.71); AddA(d, "Sb", 121.76);
            AddA(d, "Te", 127.60); AddA(d, "I", 126.90); AddA(d, "Xe", 131.29);
            AddA(d, "Cs", 132.91); AddA(d, "Ba", 137.33); AddA(d, "La", 138.91);
            AddA(d, "Ce", 140.12); AddA(d, "Pr", 140.91); AddA(d, "Nd", 144.24);
            AddA(d, "Pm", 145.0); AddA(d, "Sm", 150.36); AddA(d, "Eu", 151.96);
            AddA(d, "Gd", 157.25); AddA(d, "Tb", 158.93); AddA(d, "Dy", 162.50);
            AddA(d, "Ho", 164.93); AddA(d, "Er", 167.26); AddA(d, "Tm", 168.93);
            AddA(d, "Yb", 173.05); AddA(d, "Lu", 174.97); AddA(d, "Hf", 178.49);
            AddA(d, "Ta", 180.95); AddA(d, "W", 183.84); AddA(d, "Re", 186.21);
            AddA(d, "Os", 190.23); AddA(d, "Ir", 192.22); AddA(d, "Pt", 195.08);
            AddA(d, "Au", 196.97); AddA(d, "Hg", 200.59); AddA(d, "Tl", 204.38);
            AddA(d, "Pb", 207.2); AddA(d, "Bi", 208.98); AddA(d, "Po", 209.0);
            AddA(d, "At", 210.0); AddA(d, "Rn", 222.0); AddA(d, "Fr", 223.0);
            AddA(d, "Ra", 226.0); AddA(d, "Ac", 227.0); AddA(d, "Th", 232.04);
            AddA(d, "Pa", 231.04); AddA(d, "U", 238.03);
            return d;
        }

        private static void Add(Dictionary<string, int> d, string k, int v) { d[k] = v; }
        private static void AddA(Dictionary<string, double> d, string k, double v) { d[k] = v; }

        // Числовой формат для координат и размеров (мм)
        private static string Fmt(double v)
        {
            v = Math.Round(v, 4);
            if (v == 0.0) return "0";
            return v.ToString("0.######", CultureInfo.InvariantCulture);
        }

        // Точный формат для плотностей, долей, атомных масс — 9 значащих цифр
        private static string FmtPrecise(double v)
        {
            string result;
            if (Math.Abs(v) >= 1e-4)
            {
                result = v.ToString("G9", CultureInfo.InvariantCulture);
                // Защита от экспоненты при аномально крупных значениях
                if (result.IndexOf('E') >= 0 || result.IndexOf('e') >= 0)
                {
                    result = v.ToString("0.#########", CultureInfo.InvariantCulture);
                }
            }
            else
            {
                result = v.ToString("0.#########", CultureInfo.InvariantCulture);
            }

            if (result == "0" || result == "-0")
            {
                throw new InvalidDataException("E_MAT_BAD_VALUE: значение " + v.ToString("R", CultureInfo.InvariantCulture) + " округляется до нуля");
            }
            return result;
        }

        // XML-экранирование
        private static string XmlEsc(string s)
        {
            if (s == null) return "";
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                    .Replace("\"", "&quot;").Replace("'", "&apos;");
        }

        // Роль -> BecqMoni-строка
        private static string RoleToBecq(PartRole r)
        {
            switch (r)
            {
                case PartRole.Crystal: return "crystal";
                case PartRole.Source: return "source";
                case PartRole.Container: return "vessel";
                case PartRole.Shield: return "shield";
                case PartRole.Housing: return "housing";
                default: return "other";
            }
        }

        // Каноническое имя элемента из таблицы
        private static string CanonicalElementName(string key)
        {
            foreach (KeyValuePair<string, int> kv in ElZ)
            {
                if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
                    return kv.Key;
            }
            return key;
        }

        // Разрешение ссылки в fraction/materialref на каноническое имя
        private static string ResolveRef(string key, MaterialLibrary lib)
        {
            if (key == null) return null;
            if (key.StartsWith("G4_", StringComparison.Ordinal)) return key;

            // Сначала — таблица элементов
            string canonElem = CanonicalElementName(key);
            if (canonElem != key) return canonElem;

            // Затем — библиотека материалов
            MaterialDef def = lib.Find(key);
            if (def != null) return def.Name;

            return key;
        }

        public static GdmlWriteResult Write(SceneModel scene, MaterialLibrary lib, string path)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            var result = new GdmlWriteResult();

            int partCount = scene.Parts.Count;
            string[] volNames = new string[partCount];
            string[] solidNames = new string[partCount];
            string[] matNames = new string[partCount];

            // Названия и разрешение веществ
            for (int pi = 0; pi < partCount; pi++)
            {
                Part p = scene.Parts[pi];
                string baseName = (p.Name != null && p.Name.Length > 0) ? p.Name : ("part" + pi.ToString(CultureInfo.InvariantCulture));

                volNames[pi] = GdmlName.Make(baseName, names);
                solidNames[pi] = volNames[pi] + "_s";
                names.Add(solidNames[pi]);

                string mat = p.Material;
                if (string.IsNullOrEmpty(mat)) mat = lib.DefaultMaterial;
                if (string.IsNullOrEmpty(mat))
                    throw new InvalidDataException("E_MAT_UNKNOWN: " + baseName);

                // G4_* — NIST-ссылка, допустимо без определения в библиотеке
                if (mat.StartsWith("G4_", StringComparison.Ordinal))
                {
                    MaterialDef nistDef = lib.Find(mat);
                    if (nistDef == null)
                    {
                        matNames[pi] = mat;
                        continue;
                    }
                    matNames[pi] = nistDef.Name;
                    continue;
                }

                MaterialDef def = lib.Find(mat);
                if (def == null)
                    throw new InvalidDataException("E_MAT_UNKNOWN: " + mat);
                matNames[pi] = def.Name;
            }

            // World
            string worldVolName = GdmlName.Make("World", names);
            string worldSolidName = worldVolName + "_s";
            names.Add(worldSolidName);

            // Расчёт bbox сцены
            double mnX = double.MaxValue, mnY = double.MaxValue, mnZ = double.MaxValue;
            double mxX = double.MinValue, mxY = double.MinValue, mxZ = double.MinValue;
            bool hasBBox = false;
            for (int pi = 0; pi < partCount; pi++)
            {
                Part p = scene.Parts[pi];
                if (p.Mesh == null || p.Mesh.Vertices.Count == 0) continue;
                Vec3d lo, hi;
                p.Mesh.GetBounds(out lo, out hi);
                if (lo.X < mnX) mnX = lo.X;
                if (lo.Y < mnY) mnY = lo.Y;
                if (lo.Z < mnZ) mnZ = lo.Z;
                if (hi.X > mxX) mxX = hi.X;
                if (hi.Y > mxY) mxY = hi.Y;
                if (hi.Z > mxZ) mxZ = hi.Z;
                hasBBox = true;
            }
            if (!hasBBox) { mnX = 0; mnY = 0; mnZ = 0; mxX = 0; mxY = 0; mxZ = 0; }

            double wHx = HalfExtent(mnX, mxX);
            double wHy = HalfExtent(mnY, mxY);
            double wHz = HalfExtent(mnZ, mxZ);
            double worldBoxX = 2.0 * wHx;
            double worldBoxY = 2.0 * wHy;
            double worldBoxZ = 2.0 * wHz;

            // Подсчёт видов
            int tessCount = 0, triCount = 0, tubeCount = 0, polyCount = 0, boxCount = 0;
            for (int pi = 0; pi < partCount; pi++)
            {
                Part p = scene.Parts[pi];
                bool isTess = (p.Shape == null || p.Shape.Kind == SolidKind.Tessellated);
                if (isTess)
                {
                    if (p.Mesh == null || p.Mesh.TriangleCount == 0)
                        throw new InvalidDataException("E_MESH_EMPTY: " + (p.Name ?? ("part" + pi.ToString(CultureInfo.InvariantCulture))));
                    tessCount++;
                }
                else
                {
                    switch (p.Shape.Kind)
                    {
                        case SolidKind.Tube: tubeCount++; break;
                        case SolidKind.Polycone: polyCount++; break;
                        case SolidKind.Box: boxCount++; break;
                        default: tessCount++; break;
                    }
                }
                if (p.Mesh != null) triCount += p.Mesh.TriangleCount;
            }

            result.Volumes = partCount + 1;
            result.Tessellated = tessCount;
            result.Triangles = triCount;
            result.Tubes = tubeCount;
            result.Polycones = polyCount;
            result.Boxes = boxCount;

            // Сборка XML
            var xml = new StringBuilder();
            xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            xml.AppendLine("<gdml xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:noNamespaceSchemaLocation=\"http://cern.ch/service/geo/gdml/gdml.xsd\">");

            // --- <define>: вершины только tessellated ---
            xml.AppendLine("  <define>");
            for (int pi = 0; pi < partCount; pi++)
            {
                Part p = scene.Parts[pi];
                bool isTess = (p.Shape == null || p.Shape.Kind == SolidKind.Tessellated);
                if (!isTess) continue;
                if (p.Mesh == null) continue;

                var usedVerts = new HashSet<int>();
                for (int ii = 0; ii < p.Mesh.Indices.Count; ii++)
                    usedVerts.Add(p.Mesh.Indices[ii]);

                foreach (int vi in SortInts(usedVerts))
                {
                    Vec3d v = p.Mesh.Vertices[vi];
                    string vname = "v" + pi.ToString(CultureInfo.InvariantCulture) + "_" + vi.ToString(CultureInfo.InvariantCulture);
                    xml.Append("    <position name=\"").Append(XmlEsc(vname)).Append("\" x=\"")
                       .Append(Fmt(v.X)).Append("\" y=\"")
                       .Append(Fmt(v.Y)).Append("\" z=\"")
                       .Append(Fmt(v.Z)).Append("\" unit=\"mm\"/>");
                    xml.AppendLine();
                }
            }
            xml.AppendLine("  </define>");

            // --- <materials> ---
            xml.AppendLine("  <materials>");

            // Множество non-NIST материалов, подлежащих записи (включая транзитивные зависимости)
            var matsToWrite = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string matName in matNames)
            {
                MaterialDef d = lib.Find(matName);
                if (d == null) continue;
                if (d.IsNist) continue;
                matsToWrite.Add(d.Name);
            }

            // Транзитивное расширение: fraction-ссылки на другие non-NIST материалы
            bool expanded = true;
            while (expanded)
            {
                expanded = false;
                var toAdd = new List<string>();
                foreach (string matName in matsToWrite)
                {
                    MaterialDef def = lib.Find(matName);
                    if (def == null || def.MassFractions == null) continue;
                    for (int fi = 0; fi < def.MassFractions.Count; fi++)
                    {
                        string key = def.MassFractions[fi].Key;
                        if (key.StartsWith("G4_", StringComparison.Ordinal)) continue;
                        string canonKey = ResolveRef(key, lib);
                        MaterialDef refDef = lib.Find(canonKey);
                        if (refDef != null && !refDef.IsNist && !matsToWrite.Contains(refDef.Name))
                        {
                            toAdd.Add(refDef.Name);
                            expanded = true;
                        }
                    }
                }
                foreach (string s in toAdd) matsToWrite.Add(s);
            }

            // Элементы: только plain-символы (не G4_*, не имена материалов)
            var neededElements = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string matName in matsToWrite)
            {
                MaterialDef def = lib.Find(matName);
                if (def == null || def.MassFractions == null) continue;
                for (int fi = 0; fi < def.MassFractions.Count; fi++)
                {
                    string key = def.MassFractions[fi].Key;
                    if (key.StartsWith("G4_", StringComparison.Ordinal)) continue;
                    string canonKey = ResolveRef(key, lib);
                    // Если это имя материала в библиотеке — не элемент
                    MaterialDef refDef = lib.Find(canonKey);
                    if (refDef != null) continue;
                    neededElements.Add(canonKey);
                }
            }

            foreach (string elem in SortStrings(neededElements))
            {
                int z; double a;
                if (!ElZ.TryGetValue(elem, out z))
                    throw new InvalidDataException("E_ELEMENT_UNKNOWN: " + elem);
                if (!ElA.TryGetValue(elem, out a))
                    throw new InvalidDataException("E_ELEMENT_UNKNOWN: " + elem);

                // Каноническое имя элемента из таблицы
                string canonElem = CanonicalElementName(elem);

                xml.Append("    <element name=\"").Append(XmlEsc(canonElem))
                   .Append("\" formula=\"").Append(XmlEsc(canonElem))
                   .Append("\" Z=\"").Append(z.ToString(CultureInfo.InvariantCulture))
                   .Append("\"><atom value=\"").Append(FmtPrecise(a)).Append("\"/></element>");
                xml.AppendLine();
            }

            // Топологическая сортировка материалов (зависимые — после зависимых)
            List<string> matOrder = TopoSortMaterials(matsToWrite, lib);

            foreach (string matName in matOrder)
            {
                MaterialDef def = lib.Find(matName);
                if (def == null) continue;

                xml.Append("    <material name=\"").Append(XmlEsc(def.Name)).Append("\" state=\"solid\">");
                xml.AppendLine();
                xml.Append("      <D value=\"").Append(FmtPrecise(def.DensityGcm3)).Append("\" unit=\"g/cm3\"/>");
                xml.AppendLine();
                if (def.MassFractions != null)
                {
                    for (int fi = 0; fi < def.MassFractions.Count; fi++)
                    {
                        string sym = def.MassFractions[fi].Key;
                        double frac = def.MassFractions[fi].Value;
                        string canonRef = ResolveRef(sym, lib);
                        xml.Append("      <fraction n=\"").Append(FmtPrecise(frac))
                           .Append("\" ref=\"").Append(XmlEsc(canonRef)).Append("\"/>");
                        xml.AppendLine();
                    }
                }
                xml.AppendLine("    </material>");
            }
            xml.AppendLine("  </materials>");

            // --- <solids> ---
            xml.AppendLine("  <solids>");

            xml.Append("    <box name=\"").Append(XmlEsc(worldSolidName))
               .Append("\" x=\"").Append(Fmt(worldBoxX))
               .Append("\" y=\"").Append(Fmt(worldBoxY))
               .Append("\" z=\"").Append(Fmt(worldBoxZ)).Append("\"/>");
            xml.AppendLine();

            for (int pi = 0; pi < partCount; pi++)
            {
                Part p = scene.Parts[pi];
                string sname = solidNames[pi];

                if (p.Shape != null)
                {
                    switch (p.Shape.Kind)
                    {
                        case SolidKind.Box:
                            xml.Append("    <box name=\"").Append(XmlEsc(sname))
                               .Append("\" x=\"").Append(Fmt(p.Shape.BoxX))
                               .Append("\" y=\"").Append(Fmt(p.Shape.BoxY))
                               .Append("\" z=\"").Append(Fmt(p.Shape.BoxZ)).Append("\"/>");
                            xml.AppendLine();
                            break;

                        case SolidKind.Tube:
                            {
                                double tubeZ = p.Shape.Z1 - p.Shape.Z0;
                                xml.Append("    <tube name=\"").Append(XmlEsc(sname))
                                   .Append("\" aunit=\"deg\" lunit=\"mm\" rmin=\"")
                                   .Append(Fmt(p.Shape.RMin)).Append("\" rmax=\"")
                                   .Append(Fmt(p.Shape.RMax)).Append("\" z=\"")
                                   .Append(Fmt(tubeZ))
                                   .Append("\" startphi=\"0\" deltaphi=\"360\"/>");
                                xml.AppendLine();
                            }
                            break;

                        case SolidKind.Polycone:
                            xml.Append("    <polycone name=\"").Append(XmlEsc(sname))
                               .Append("\" aunit=\"deg\" lunit=\"mm\" startphi=\"0\" deltaphi=\"360\">");
                            xml.AppendLine();
                            if (p.Shape.ZPlane != null)
                            {
                                int pc = p.Shape.ZPlane.Count;
                                for (int zi = 0; zi < pc; zi++)
                                {
                                    double zr = (zi < p.Shape.RInner.Count) ? p.Shape.RInner[zi] : 0.0;
                                    double zo = (zi < p.Shape.ROuter.Count) ? p.Shape.ROuter[zi] : 0.0;
                                    xml.Append("      <zplane rmin=\"").Append(Fmt(zr))
                                       .Append("\" rmax=\"").Append(Fmt(zo))
                                       .Append("\" z=\"").Append(Fmt(p.Shape.ZPlane[zi])).Append("\"/>");
                                    xml.AppendLine();
                                }
                            }
                            xml.AppendLine("    </polycone>");
                            break;

                        case SolidKind.Tessellated:
                        default:
                            WriteTessellated(xml, sname, p, pi);
                            break;
                    }
                }
                else
                {
                    WriteTessellated(xml, sname, p, pi);
                }
            }
            xml.AppendLine("  </solids>");

            // --- <structure> ---
            xml.AppendLine("  <structure>");

            for (int pi = 0; pi < partCount; pi++)
            {
                Part p = scene.Parts[pi];

                xml.Append("    <volume name=\"").Append(XmlEsc(volNames[pi])).Append("\">");
                xml.AppendLine();
                xml.Append("      <materialref ref=\"").Append(XmlEsc(matNames[pi])).Append("\"/>");
                xml.AppendLine();
                xml.Append("      <solidref ref=\"").Append(XmlEsc(solidNames[pi])).Append("\"/>");
                xml.AppendLine();
                xml.Append("      <auxiliary auxtype=\"BecqMoni:Role\" auxvalue=\"")
                   .Append(RoleToBecq(p.Role)).Append("\"/>");
                xml.AppendLine();
                xml.Append("      <auxiliary auxtype=\"Cad2Gdml:Role\" auxvalue=\"")
                   .Append(p.Role.ToString()).Append("\"/>");
                xml.AppendLine();
                xml.AppendLine("    </volume>");
            }

            xml.Append("    <volume name=\"").Append(XmlEsc(worldVolName)).Append("\">");
            xml.AppendLine();
            xml.Append("      <materialref ref=\"G4_AIR\"/>");
            xml.AppendLine();
            xml.Append("      <solidref ref=\"").Append(XmlEsc(worldSolidName)).Append("\"/>");
            xml.AppendLine();

            for (int pi = 0; pi < partCount; pi++)
            {
                Part p = scene.Parts[pi];
                string pvName = "PV_" + volNames[pi];
                string posName = "pos_" + volNames[pi];

                xml.Append("      <physvol name=\"").Append(XmlEsc(pvName)).Append("\">");
                xml.AppendLine();
                xml.Append("        <volumeref ref=\"").Append(XmlEsc(volNames[pi])).Append("\"/>");
                xml.AppendLine();

                double px = 0.0, py = 0.0, pz = 0.0;
                if (p.Shape != null)
                {
                    switch (p.Shape.Kind)
                    {
                        case SolidKind.Tube:
                            px = p.Shape.X0;
                            py = p.Shape.Y0;
                            pz = (p.Shape.Z0 + p.Shape.Z1) / 2.0;
                            break;
                        case SolidKind.Box:
                            px = p.Shape.X0;
                            py = p.Shape.Y0;
                            pz = p.Shape.Z0;
                            break;
                        case SolidKind.Polycone:
                            px = p.Shape.X0;
                            py = p.Shape.Y0;
                            pz = 0.0;
                            break;
                        default:
                            px = 0.0; py = 0.0; pz = 0.0;
                            break;
                    }
                }

                xml.Append("        <position name=\"").Append(XmlEsc(posName))
                   .Append("\" unit=\"mm\" x=\"").Append(Fmt(px))
                   .Append("\" y=\"").Append(Fmt(py))
                   .Append("\" z=\"").Append(Fmt(pz)).Append("\"/>");
                xml.AppendLine();
                xml.AppendLine("      </physvol>");
            }
            xml.AppendLine("    </volume>");
            xml.AppendLine("  </structure>");

            // --- <setup> ---
            xml.AppendLine("  <setup name=\"Default\" version=\"1.0\">");
            xml.Append("    <world ref=\"").Append(XmlEsc(worldVolName)).Append("\"/>");
            xml.AppendLine();
            xml.AppendLine("  </setup>");

            xml.AppendLine("</gdml>");

            // Запись: UTF-8 без BOM, LF
            string text = xml.ToString().Replace("\r\n", "\n").Replace("\r", "\n");
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            File.WriteAllBytes(path, bytes);

            // SHA256
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                var hex = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    hex.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                result.Sha256 = hex.ToString();
            }

            return result;
        }

        // Топологическая сортировка: зависимые материалы — после зависимых
        private static List<string> TopoSortMaterials(HashSet<string> mats, MaterialLibrary lib)
        {
            var order = new List<string>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var inStack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var sorted = new List<string>(mats);
            sorted.Sort(StringComparer.Ordinal);

            for (int i = 0; i < sorted.Count; i++)
                VisitMat(sorted[i], mats, lib, visited, inStack, order);

            return order;
        }

        // Рекурсивный обход для топосортировки
        private static void VisitMat(string name, HashSet<string> mats, MaterialLibrary lib,
            HashSet<string> visited, HashSet<string> inStack, List<string> order)
        {
            if (inStack.Contains(name))
                throw new InvalidDataException("E_MAT_CYCLE: " + name);
            if (visited.Contains(name)) return;
            inStack.Add(name);

            MaterialDef def = lib.Find(name);
            if (def != null && def.MassFractions != null)
            {
                for (int fi = 0; fi < def.MassFractions.Count; fi++)
                {
                    string key = def.MassFractions[fi].Key;
                    if (key.StartsWith("G4_", StringComparison.Ordinal)) continue;
                    string canonKey = ResolveRef(key, lib);
                    MaterialDef refDef = lib.Find(canonKey);
                    if (refDef != null && !refDef.IsNist && mats.Contains(refDef.Name))
                    {
                        VisitMat(refDef.Name, mats, lib, visited, inStack, order);
                    }
                }
            }

            inStack.Remove(name);
            visited.Add(name);
            order.Add(name);
        }

        // Запись tessellated-твёрдого
        private static void WriteTessellated(StringBuilder xml, string sname, Part p, int pi)
        {
            if (p.Mesh == null || p.Mesh.TriangleCount == 0)
            {
                string nm = p.Name ?? ("part" + pi.ToString(CultureInfo.InvariantCulture));
                throw new InvalidDataException("E_MESH_EMPTY: " + nm);
            }

            xml.Append("    <tessellated name=\"").Append(XmlEsc(sname)).Append("\">");
            xml.AppendLine();
            int triN = p.Mesh.TriangleCount;
            for (int ti = 0; ti < triN; ti++)
            {
                int i0 = p.Mesh.Indices[ti * 3];
                int i1 = p.Mesh.Indices[ti * 3 + 1];
                int i2 = p.Mesh.Indices[ti * 3 + 2];
                xml.Append("      <triangular vertex1=\"v")
                   .Append(pi.ToString(CultureInfo.InvariantCulture)).Append("_")
                   .Append(i0.ToString(CultureInfo.InvariantCulture))
                   .Append("\" vertex2=\"v")
                   .Append(pi.ToString(CultureInfo.InvariantCulture)).Append("_")
                   .Append(i1.ToString(CultureInfo.InvariantCulture))
                   .Append("\" vertex3=\"v")
                   .Append(pi.ToString(CultureInfo.InvariantCulture)).Append("_")
                   .Append(i2.ToString(CultureInfo.InvariantCulture))
                   .Append("\" type=\"ABSOLUTE\"/>");
                xml.AppendLine();
            }
            xml.AppendLine("    </tessellated>");
        }

        // Полуребро World
        private static double HalfExtent(double lo, double hi)
        {
            double m = Math.Max(Math.Abs(lo), Math.Abs(hi));
            double margin = Math.Max(10.0, m * 0.1);
            return m + margin;
        }

        // Сортировка строк
        private static List<string> SortStrings(HashSet<string> set)
        {
            var list = new List<string>(set);
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        // Сортировка int-ов
        private static List<int> SortInts(HashSet<int> set)
        {
            var list = new List<int>(set);
            list.Sort();
            return list;
        }
    }
}
