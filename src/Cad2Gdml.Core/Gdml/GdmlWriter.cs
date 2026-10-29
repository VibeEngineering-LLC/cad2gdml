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
            if (char.IsDigit(name[0])) name = "p" + name;

            if (!used.Contains(name))
            {
                used.Add(name);
                return name;
            }

            int i = 2;
            string candidate;
            do
            {
                candidate = name + "_" + i.ToString(CultureInfo.InvariantCulture);
                i++;
            }
            while (used.Contains(candidate));

            used.Add(candidate);
            return candidate;
        }

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
                else if (char.IsLetterOrDigit(c))
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

            // Заглавные А-Я
            if (c >= '\u0410' && c <= '\u042F')
            {
                int idx = c - '\u0410';
                string[] arr = {
                    "A","B","V","G","D","E","Zh","Z","I","Y","K","L","M","N","O","P","R","S","T","U",
                    "F","H","Ts","Ch","Sh","Shch","","Y","","E","Yu","Ya"
                };
                return idx < arr.Length ? arr[idx] : "";
            }

            // Строчные а-я
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
        // Таблица элементов H..U: Z и A
        private static readonly Dictionary<string, int> ElZ = BuildZ();
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

        // Форматирование числа: InvariantCulture, до 9 значащих цифр, -0 -> 0
        private static string Fmt(double v)
        {
            if (v == 0.0) return "0";
            return v.ToString("G9", CultureInfo.InvariantCulture);
        }

        // XML-экранирование
        private static string XmlEsc(string s)
        {
            if (s == null) return "";
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                    .Replace("\"", "&quot;").Replace("'", "&apos;");
        }

        public static GdmlWriteResult Write(SceneModel scene, MaterialLibrary lib, string path)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            var result = new GdmlWriteResult();

            // Сборка списков
            int partCount = scene.Parts.Count;
            string[] volNames = new string[partCount];
            string[] solidNames = new string[partCount];
            var neededMaterials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var neededElements = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Расчёт bbox сцены
            Vec3d sceneMin, sceneMax;
            bool hasBBox = false;
            {
                double mnX = double.MaxValue, mnY = double.MaxValue, mnZ = double.MaxValue;
                double mxX = double.MinValue, mxY = double.MinValue, mxZ = double.MinValue;
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
                sceneMin = hasBBox ? new Vec3d(mnX, mnY, mnZ) : Vec3d.Zero;
                sceneMax = hasBBox ? new Vec3d(mxX, mxY, mxZ) : Vec3d.Zero;
            }

            // World-полуребро
            double wHx = HalfExtent(sceneMin.X, sceneMax.X);
            double wHy = HalfExtent(sceneMin.Y, sceneMax.Y);
            double wHz = HalfExtent(sceneMin.Z, sceneMax.Z);
            double worldBoxX = 2.0 * wHx;
            double worldBoxY = 2.0 * wHy;
            double worldBoxZ = 2.0 * wHz;

            // Названия
            string worldVolName = GdmlName.Make("World", names);
            string worldSolidName = GdmlName.Make("WorldBox", names);

            for (int pi = 0; pi < partCount; pi++)
            {
                Part p = scene.Parts[pi];
                string baseName = p.Name ?? ("part" + pi);
                volNames[pi] = GdmlName.Make(baseName, names);
                solidNames[pi] = GdmlName.Make(baseName, names);

                // Вещество
                string matName = p.Material;
                if (string.IsNullOrEmpty(matName))
                {
                    matName = lib.DefaultMaterial;
                }
                if (!string.IsNullOrEmpty(matName))
                {
                    neededMaterials.Add(matName);
                    MaterialDef def = lib.Find(matName);
                    if (def != null && !def.IsNist)
                    {
                        for (int fi = 0; fi < def.MassFractions.Count; fi++)
                        {
                            neededElements.Add(def.MassFractions[fi].Key);
                        }
                    }
                }
            }

            // Подсчёт видов
            int tessCount = 0, triCount = 0, tubeCount = 0, polyCount = 0, boxCount = 0;
            for (int pi = 0; pi < partCount; pi++)
            {
                Part p = scene.Parts[pi];
                if (p.Shape != null)
                {
                    switch (p.Shape.Kind)
                    {
                        case SolidKind.Tube: tubeCount++; break;
                        case SolidKind.Polycone: polyCount++; break;
                        case SolidKind.Box: boxCount++; break;
                        default: tessCount++; break;
                    }
                }
                else
                {
                    tessCount++;
                }
                if (p.Mesh != null)
                {
                    triCount += p.Mesh.TriangleCount;
                }
            }

            result.Volumes = partCount + 1; // + World
            result.Tessellated = tessCount;
            result.Triangles = triCount;
            result.Tubes = tubeCount;
            result.Polycones = polyCount;
            result.Boxes = boxCount;

            // Построение XML
            var xml = new StringBuilder();
            xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            xml.AppendLine("<gdml xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:noNamespaceSchemaLocation=\"http://cern.ch/service/geo/gdml/gdml.xsd\">");

            // <define>
            xml.AppendLine("  <define>");
            for (int pi = 0; pi < partCount; pi++)
            {
                Part p = scene.Parts[pi];
                bool isTess = (p.Shape == null || p.Shape.Kind == SolidKind.Tessellated);
                if (!isTess) continue;
                if (p.Mesh == null || p.Mesh.Vertices.Count == 0) continue;

                for (int vi = 0; vi < p.Mesh.Vertices.Count; vi++)
                {
                    Vec3d v = p.Mesh.Vertices[vi];
                    string vname = "v" + pi + "_" + vi;
                    xml.Append("    <position name=\"").Append(XmlEsc(vname)).Append("\" x=\"")
                       .Append(Fmt(v.X)).Append("\" y=\"")
                       .Append(Fmt(v.Y)).Append("\" z=\"")
                       .Append(Fmt(v.Z)).Append("\" unit=\"mm\"/>");
                    xml.AppendLine();
                }
            }
            xml.AppendLine("  </define>");

            // <materials>
            xml.AppendLine("  <materials>");

            // Элементы
            foreach (string elem in SortStrings(neededElements))
            {
                int z; double a;
                if (ElZ.TryGetValue(elem, out z) && ElA.TryGetValue(elem, out a))
                {
                    xml.Append("    <element name=\"").Append(XmlEsc(elem)).Append("\" Z=\"")
                       .Append(z.ToString(CultureInfo.InvariantCulture)).Append("\" A=\"")
                       .Append(Fmt(a)).Append("\"/>");
                    xml.AppendLine();
                }
                else
                {
                    // Неизвестный элемент — пропускаем
                }
            }

            // Вещества (только non-NIST)
            foreach (string matName in SortStrings(neededMaterials))
            {
                MaterialDef def = lib.Find(matName);
                if (def == null) continue;
                if (def.IsNist) continue; // NIST — только по ссылке

                xml.Append("    <material name=\"").Append(XmlEsc(def.Name)).Append("\" state=\"solid\">");
                xml.AppendLine();
                xml.Append("      <D value=\"").Append(Fmt(def.DensityGcm3)).Append("\" unit=\"g/cm3\"/>");
                xml.AppendLine();
                if (def.MassFractions != null)
                {
                    for (int fi = 0; fi < def.MassFractions.Count; fi++)
                    {
                        string elemSym = def.MassFractions[fi].Key;
                        double frac = def.MassFractions[fi].Value;
                        xml.Append("      <fraction n=\"").Append(Fmt(frac)).Append("\" ref=\"")
                           .Append(XmlEsc(elemSym)).Append("\"/>");
                        xml.AppendLine();
                    }
                }
                xml.AppendLine("    </material>");
            }

            xml.AppendLine("  </materials>");

            // <solids>
            xml.AppendLine("  <solids>");

            // World box
            xml.Append("    <box name=\"").Append(XmlEsc(worldSolidName)).Append("\" x=\"")
               .Append(Fmt(worldBoxX)).Append("\" y=\"")
               .Append(Fmt(worldBoxY)).Append("\" z=\"")
               .Append(Fmt(worldBoxZ)).Append("\"/>");
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
                            xml.Append("    <box name=\"").Append(XmlEsc(sname)).Append("\" x=\"")
                               .Append(Fmt(p.Shape.BoxX)).Append("\" y=\"")
                               .Append(Fmt(p.Shape.BoxY)).Append("\" z=\"")
                               .Append(Fmt(p.Shape.BoxZ)).Append("\"/>");
                            xml.AppendLine();
                            break;

                        case SolidKind.Tube:
                            double tubeZ = p.Shape.Z1 - p.Shape.Z0;
                            xml.Append("    <tube name=\"").Append(XmlEsc(sname)).Append("\" aunit=\"rad\" lunit=\"mm\" rmin=\"")
                               .Append(Fmt(p.Shape.RMin)).Append("\" rmax=\"")
                               .Append(Fmt(p.Shape.RMax)).Append("\" z=\"")
                               .Append(Fmt(tubeZ)).Append("\" startphi=\"0\" deltaphi=\"")
                               .Append(Fmt(2.0 * Math.PI)).Append("\"/>");
                            xml.AppendLine();
                            break;

                        case SolidKind.Polycone:
                            xml.Append("    <polycone name=\"").Append(XmlEsc(sname)).Append("\" aunit=\"rad\" lunit=\"mm\" startphi=\"0\" deltaphi=\"")
                               .Append(Fmt(2.0 * Math.PI)).Append("\">");
                            xml.AppendLine();
                            if (p.Shape.ZPlane != null)
                            {
                                int pc = p.Shape.ZPlane.Count;
                                for (int zi = 0; zi < pc; zi++)
                                {
                                    double zr = (zi < p.Shape.RInner.Count) ? p.Shape.RInner[zi] : 0.0;
                                    double zo = (zi < p.Shape.ROuter.Count) ? p.Shape.ROuter[zi] : 0.0;
                                    xml.Append("      <zplane rmin=\"").Append(Fmt(zr)).Append("\" rmax=\"")
                                       .Append(Fmt(zo)).Append("\" z=\"")
                                       .Append(Fmt(p.Shape.ZPlane[zi])).Append("\"/>");
                                    xml.AppendLine();
                                }
                            }
                            xml.AppendLine("    </polycone>");
                            break;

                        case SolidKind.Tessellated:
                        default:
                            // Tessellated
                            xml.Append("    <tessellated name=\"").Append(XmlEsc(sname)).Append("\">");
                            xml.AppendLine();
                            if (p.Mesh != null)
                            {
                                int triN = p.Mesh.TriangleCount;
                                for (int ti = 0; ti < triN; ti++)
                                {
                                    Vec3d a, b, c;
                                    p.Mesh.GetTriangle(ti, out a, out b, out c);
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
                            }
                            xml.AppendLine("    </tessellated>");
                            break;
                    }
                }
                else
                {
                    // Нет Shape — tessellated по сетке
                    xml.Append("    <tessellated name=\"").Append(XmlEsc(sname)).Append("\">");
                    xml.AppendLine();
                    if (p.Mesh != null)
                    {
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
                    }
                    xml.AppendLine("    </tessellated>");
                }
            }

            xml.AppendLine("  </solids>");

            // <structure>
            xml.AppendLine("  <structure>");

            for (int pi = 0; pi < partCount; pi++)
            {
                Part p = scene.Parts[pi];
                string matName = p.Material;
                if (string.IsNullOrEmpty(matName)) matName = lib.DefaultMaterial;

                xml.Append("    <volume name=\"").Append(XmlEsc(volNames[pi]))
                   .Append("\" materialref=\"").Append(XmlEsc(matName))
                   .Append("\" solidref=\"").Append(XmlEsc(solidNames[pi])).Append("\">");
                xml.AppendLine();
                xml.Append("      <auxiliary auxtype=\"Cad2Gdml:Role\" auxvalue=\"")
                   .Append(XmlEsc(p.Role.ToString())).Append("\"/>");
                xml.AppendLine();
                xml.AppendLine("    </volume>");
            }

            // World volume
            xml.Append("    <volume name=\"").Append(XmlEsc(worldVolName))
               .Append("\" materialref=\"G4_AIR\" solidref=\"").Append(XmlEsc(worldSolidName)).Append("\">");
            xml.AppendLine();
            for (int pi = 0; pi < partCount; pi++)
            {
                xml.Append("      <physvol ref=\"").Append(XmlEsc(volNames[pi])).Append("\"/>");
                xml.AppendLine();
            }
            xml.AppendLine("    </volume>");
            xml.AppendLine("  </structure>");

            // <setup>
            xml.AppendLine("  <setup name=\"Default\" version=\"1.0\">");
            xml.Append("    <world ref=\"").Append(XmlEsc(worldVolName)).Append("\"/>");
            xml.AppendLine();
            xml.AppendLine("  </setup>");

            xml.AppendLine("</gdml>");

            // Запись файла: UTF-8 без BOM, LF
            byte[] bytes = Encoding.UTF8.GetBytes(xml.ToString().Replace("\r\n", "\n").Replace("\r", "\n"));
            File.WriteAllBytes(path, bytes);

            // SHA256
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                var hex = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    hex.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                }
                result.Sha256 = hex.ToString();
            }

            return result;
        }

        // Полуребро с маржином
        private static double HalfExtent(double lo, double hi)
        {
            double m = Math.Max(Math.Abs(lo), Math.Abs(hi));
            double margin = Math.Max(10.0, m * 0.1);
            return m + margin;
        }

        // Сортировка строк для детерминизма
        private static List<string> SortStrings(HashSet<string> set)
        {
            var list = new List<string>(set);
            list.Sort(StringComparer.Ordinal);
            return list;
        }
    }
}
