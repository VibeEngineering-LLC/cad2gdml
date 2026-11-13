using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using Cad2Gdml.Geometry;
using Cad2Gdml.Meshes;

namespace Cad2Gdml.Import
{
    // Тело нельзя построить из-за плохой позиции/единицы — вызывающий пропускает тело
    internal sealed class GdmlSkipBodyException : Exception
    {
        internal GdmlSkipBodyException(string message) : base(message) { }
    }

    // Ленивый доступ к позициям из <define>; парсинг при первом обращении Get
    internal sealed class TessPositions
    {
        private readonly Dictionary<string, XElement> _els;
        private readonly HashSet<string> _dups;
        private readonly Dictionary<string, double[]> _cache;

        private TessPositions(Dictionary<string, XElement> els, HashSet<string> dups)
        {
            _els = els;
            _dups = dups;
            _cache = new Dictionary<string, double[]>(els.Count);
        }

        internal static TessPositions Read(XElement root)
        {
            var list = new List<KeyValuePair<string, XElement>>();
            var seen = new HashSet<string>();
            var dups = new HashSet<string>();

            // Один проход по всем потомкам root
            foreach (var el in root.Descendants())
            {
                if (el.Name.LocalName != "position") continue;
                if (!IsInsideDefine(el)) continue;
                var nm = el.Attribute("name")?.Value;
                if (string.IsNullOrEmpty(nm)) continue;
                if (seen.Contains(nm)) dups.Add(nm);
                else { seen.Add(nm); list.Add(new KeyValuePair<string, XElement>(nm, el)); }
            }

            var dict = new Dictionary<string, XElement>(list.Count);
            for (int i = 0; i < list.Count; i++)
                dict[list[i].Key] = list[i].Value;

            return new TessPositions(dict, dups);
        }

        internal double[] Get(string name)
        {
            if (_dups.Contains(name))
                throw new GdmlSkipBodyException("позиция «" + name + "» определена в <define> более одного раза");
            if (!_els.TryGetValue(name, out var el))
                return null;
            if (_cache.TryGetValue(name, out var cached))
                return cached;

            double unit = UnitFactor(el, name);
            double x = Coord(el, "x", name, unit);
            double y = Coord(el, "y", name, unit);
            double z = Coord(el, "z", name, unit);
            var r = new double[] { x, y, z };
            _cache[name] = r;
            return r;
        }

        private static bool IsInsideDefine(XElement el)
        {
            for (var a = el.Parent; a != null; a = a.Parent)
                if (a.Name.LocalName == "define") return true;
            return false;
        }

        private static double UnitFactor(XElement pos, string name)
        {
            switch (pos.Attribute("unit")?.Value ?? "")
            {
                case "": case "mm": return 1.0;
                case "cm": return 10.0;
                case "m": return 1000.0;
                case "um": return 0.001;
                case "nm": return 1e-6;
                case "km": return 1e6;
                default:
                    throw new GdmlSkipBodyException("позиция «" + name + "»: неизвестная единица длины «" + (pos.Attribute("unit")?.Value ?? "") + "»");
            }
        }

        private static double Coord(XElement pos, string attr, string name, double unit)
        {
            var s = pos.Attribute(attr)?.Value;
            if (s == null) return 0.0;
            if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || double.IsNaN(v) || double.IsInfinity(v))
                throw new GdmlSkipBodyException("позиция «" + name + "»: значение «" + s + "» не число (выражения и константы GDML не поддержаны)");
            return v * unit;
        }
    }

    // Построение Mesh из <tessellated> по правилам Geant4
    internal static class GdmlTessellated
    {
        internal static Mesh Build(XElement solid, TessPositions positions)
        {
            var mesh = new Mesh();
            var sName = solid.Attribute("name")?.Value ?? "";

            foreach (var face in solid.Elements())
            {
                var ln = face.Name.LocalName;
                if (ln != "triangular" && ln != "quadrangular") continue;

                double lu = FaceUnit(face, sName);
                bool rel = string.Equals(face.Attribute("type")?.Value, "RELATIVE", StringComparison.Ordinal);
                int n = (ln == "triangular") ? 3 : 4;

                var v = new Vec3d[n];
                for (int i = 1; i <= n; i++)
                    v[i - 1] = Vertex(face, i, sName, positions, lu);

                // RELATIVE: вершины 2..n — смещения от вершины 1
                if (rel)
                {
                    for (int i = 1; i < n; i++)
                        v[i] = new Vec3d(v[0].X + v[i].X, v[0].Y + v[i].Y, v[0].Z + v[i].Z);
                }

                if (n == 3)
                    mesh.AddTriangle(v[0], v[1], v[2]);
                else
                {
                    // G4QuadrangularFacet: (v1,v2,v3) + (v1,v3,v4)
                    mesh.AddTriangle(v[0], v[1], v[2]);
                    mesh.AddTriangle(v[0], v[2], v[3]);
                }
            }
            return mesh;
        }

        private static Vec3d Vertex(XElement face, int idx, string sName, TessPositions positions, double lu)
        {
            var an = "vertex" + idx;
            var s = face.Attribute(an)?.Value;
            if (s == null)
                throw new InvalidDataException("Тело tessellated «" + sName + "»: не задан атрибут " + an + ".");
            var c = positions.Get(s);
            if (c == null)
                throw new InvalidDataException("Тело tessellated «" + sName + "»: вершина «" + s + "» не найдена среди позиций <define>.");
            return new Vec3d(c[0] * lu, c[1] * lu, c[2] * lu);
        }

        private static double FaceUnit(XElement face, string sName)
        {
            switch (face.Attribute("lunit")?.Value ?? "")
            {
                case "": case "mm": return 1.0;
                case "cm": return 10.0;
                case "m": return 1000.0;
                case "um": return 0.001;
                case "nm": return 1e-6;
                case "km": return 1e6;
                default:
                    throw new GdmlSkipBodyException("тело tessellated «" + sName + "»: неизвестная единица грани «" + (face.Attribute("lunit")?.Value ?? "") + "»");
            }
        }
    }
}
