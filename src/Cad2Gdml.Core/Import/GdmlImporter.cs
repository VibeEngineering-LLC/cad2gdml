using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using Cad2Gdml.Geometry;
using Cad2Gdml.Meshes;
using Cad2Gdml.Scene;

namespace Cad2Gdml.Import
{
    public static class GdmlImporter
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const int MaxDepth = 64;

        public static IList<Part> Load(string path, double unitToMm)
        {
            return Load(path, out _);
        }

        public static IList<Part> Load(string path, out IList<string> skipped)
        {
            XDocument doc;
            try { doc = XDocument.Load(path); }
            catch (Exception ex) { throw new InvalidDataException("Не удалось загрузить XML: " + ex.Message); }
            if (doc.Root == null) throw new InvalidDataException("Пустой XML-документ.");

            var setup = FindChild(doc.Root, "setup");
            if (setup == null) throw new InvalidDataException("Не найден элемент <setup>.");

            double lunit = ParseLunit(GetAttr(setup, "lunit"));
            var skippedList = new List<string>();
            var solids = ParseSolids(doc.Root, lunit, skippedList);
            var volumes = ParseStructure(doc.Root);

            var defPos = new Dictionary<string, double[]>();
            var defRot = new Dictionary<string, double[]>();
            ParseDefines(setup, defPos, defRot);

            var worldEl = FindChild(setup, "world");
            if (worldEl == null) throw new InvalidDataException("Не найден элемент <world> в <setup>.");
            string worldRef = GetAttr(worldEl, "ref");
            if (string.IsNullOrEmpty(worldRef)) throw new InvalidDataException("Не указан ref для <world>.");

            var parts = new List<Part>();
            var nameCount = new Dictionary<string, int>();
            Walk(worldRef, Identity(), 0, 0, 0, 0, volumes, solids, defPos, defRot, parts, nameCount, path, true);
            skipped = skippedList;
            return parts;
        }

        private static double ParseLunit(string s)
        {
            if (string.IsNullOrEmpty(s)) return 1.0;
            switch (s)
            {
                case "mm": return 1.0;
                case "cm": return 10.0;
                case "m": return 1000.0;
                case "um": return 0.001;
                default: return 1.0;
            }
        }

        private static Dictionary<string, Func<Mesh>> ParseSolids(XElement root, double lunit, List<string> skipped)
        {
            var result = new Dictionary<string, Func<Mesh>>();
            var solidsEl = FindChild(root, "solids");
            if (solidsEl == null) throw new InvalidDataException("Не найден элемент <solids>.");
            foreach (var s in solidsEl.Elements())
            {
                string name = GetAttr(s, "name");
                if (string.IsNullOrEmpty(name)) continue;
                string type = s.Name.LocalName;
                double lu = ParseLunit(GetAttr(s, "lunit"));
                if (type == "box")
                {
                    double x = PDouble(s, "x") * lu, y = PDouble(s, "y") * lu, z = PDouble(s, "z") * lu;
                    result[name] = () => GdmlMeshing.BuildBox(x, y, z);
                }
                else if (type == "tube")
                {
                    double rmin = PDouble(s, "rmin") * lu, rmax = PDouble(s, "rmax") * lu, z = PDouble(s, "z") * lu;
                    double sp = ToRad(s, "startphi"), dp = ToRad(s, "deltaphi", true);
                    result[name] = () => GdmlMeshing.BuildTube(rmin, rmax, z, sp, dp);
                }
                else if (type == "polycone")
                {
                    double sp = ToRad(s, "startphi"), dp = ToRad(s, "deltaphi", true);
                    var planes = new List<double[]>();
                    foreach (var zp in s.Elements())
                        if (zp.Name.LocalName == "zplane")
                            planes.Add(new double[] { PDouble(zp, "z") * lu, PDouble(zp, "rmin") * lu, PDouble(zp, "rmax") * lu });
                    result[name] = () => GdmlMeshing.BuildPolycone(sp, dp, planes);
                }
                else { skipped.Add(name); }
            }
            return result;
        }

        private struct VolumeInfo
        {
            public string MaterialRef;
            public string SolidRef;
            public List<PhysvolInfo> Physvols;
        }
        private struct PhysvolInfo
        {
            public string VolumeRef;
            public double Px, Py, Pz;
            public double Rx, Ry, Rz;
        }

        private static Dictionary<string, VolumeInfo> ParseStructure(XElement root)
        {
            var result = new Dictionary<string, VolumeInfo>();
            var st = FindChild(root, "structure");
            if (st == null) throw new InvalidDataException("Не найден элемент <structure>.");
            foreach (var vol in st.Elements())
            {
                if (vol.Name.LocalName != "volume") continue;
                string name = GetAttr(vol, "name");
                if (string.IsNullOrEmpty(name)) continue;
                var info = new VolumeInfo { Physvols = new List<PhysvolInfo>() };
                var mr = FindChild(vol, "materialref");
                if (mr != null) info.MaterialRef = GetAttr(mr, "ref");
                var sr = FindChild(vol, "solidref");
                if (sr != null) info.SolidRef = GetAttr(sr, "ref");
                foreach (var pv in vol.Elements())
                {
                    if (pv.Name.LocalName != "physvol") continue;
                    var pi = new PhysvolInfo { VolumeRef = GetAttr(FindChild(pv, "volumeref"), "ref") };
                    var pos = FindChild(pv, "position");
                    if (pos != null) { double pu = UnitMm(GetAttr(pos, "unit")); pi.Px = PDouble(pos, "x") * pu; pi.Py = PDouble(pos, "y") * pu; pi.Pz = PDouble(pos, "z") * pu; }
                    var rot = FindChild(pv, "rotation");
                    if (rot != null) { bool ru = GetAttr(rot, "unit") != "deg"; pi.Rx = ToAngle(rot, "x", ru); pi.Ry = ToAngle(rot, "y", ru); pi.Rz = ToAngle(rot, "z", ru); }
                    info.Physvols.Add(pi);
                }
                result[name] = info;
            }
            return result;
        }

        private static void ParseDefines(XElement setup, Dictionary<string, double[]> defPos, Dictionary<string, double[]> defRot)
        {
            foreach (var d in setup.Elements())
            {
                if (d.Name.LocalName != "define") continue;
                string name = GetAttr(d, "name");
                if (string.IsNullOrEmpty(name)) continue;
                var pos = FindChild(d, "position");
                if (pos != null) { double pu = UnitMm(GetAttr(pos, "unit")); defPos[name] = new double[] { PDouble(pos, "x") * pu, PDouble(pos, "y") * pu, PDouble(pos, "z") * pu }; }
                var rot = FindChild(d, "rotation");
                if (rot != null) { bool ru = GetAttr(rot, "unit") != "deg"; defRot[name] = new double[] { ToAngle(rot, "x", ru), ToAngle(rot, "y", ru), ToAngle(rot, "z", ru) }; }
            }
        }

        private static void Walk(string volName, double[] mAcc, double pAx, double pAy, double pAz, int depth,
            Dictionary<string, VolumeInfo> volumes, Dictionary<string, Func<Mesh>> solids,
            Dictionary<string, double[]> defPos, Dictionary<string, double[]> defRot,
            List<Part> parts, Dictionary<string, int> nameCount, string path, bool isWorld)
        {
            if (depth > MaxDepth) throw new InvalidDataException("Превышена максимальная глубина вложенности (64).");
            if (!volumes.ContainsKey(volName)) return;
            var info = volumes[volName];

            // Если volume имеет solid — создаём Part (кроме world)
            if (!isWorld && !string.IsNullOrEmpty(info.SolidRef) && solids.ContainsKey(info.SolidRef))
            {
                var baseMesh = solids[info.SolidRef]();
                if (baseMesh.TriangleCount > 0)
                {
                    var tmesh = TransformMesh(baseMesh, mAcc, pAx, pAy, pAz);
                    if (tmesh.TriangleCount > 0)
                    {
                        string pName;
                        int cnt;
                        if (!nameCount.TryGetValue(volName, out cnt)) { nameCount[volName] = 1; pName = volName; }
                        else { nameCount[volName] = cnt + 1; pName = volName + "_" + (cnt + 1); }
                        var part = new Part
                        {
                            Name = pName,
                            OrigName = volName,
                            Mesh = tmesh,
                            Material = info.MaterialRef,
                            SourceMaterial = info.MaterialRef,
                            SourceFile = path
                        };
                        parts.Add(part);
                    }
                }
            }

            // Обходим physvols
            foreach (var pv in info.Physvols)
            {
                double px = pv.Px, py = pv.Py, pz = pv.Pz, rx = pv.Rx, ry = pv.Ry, rz = pv.Rz;

                // Подсчёт матрицы и смещения
                double[] mLoc = RotInv(rx, ry, rz);
                double[] mNew = MatMul(mAcc, mLoc);
                double npX = mAcc[0]*px + mAcc[1]*py + mAcc[2]*pz + pAx;
                double npY = mAcc[3]*px + mAcc[4]*py + mAcc[5]*pz + pAy;
                double npZ = mAcc[6]*px + mAcc[7]*py + mAcc[8]*pz + pAz;

                Walk(pv.VolumeRef, mNew, npX, npY, npZ, depth + 1, volumes, solids, defPos, defRot, parts, nameCount, path, false);
            }
        }

        private static Vec3d Apply(double[] m, Vec3d v, double px, double py, double pz)
        {
            double x = m[0]*v.X + m[1]*v.Y + m[2]*v.Z + px;
            double y = m[3]*v.X + m[4]*v.Y + m[5]*v.Z + py;
            double z = m[6]*v.X + m[7]*v.Y + m[8]*v.Z + pz;
            return new Vec3d(x, y, z);
        }

        private static Mesh TransformMesh(Mesh src, double[] m, double px, double py, double pz)
        {
            var result = new Mesh();
            for (int i = 0; i < src.TriangleCount; i++)
            {
                Vec3d a, b, c;
                src.GetTriangle(i, out a, out b, out c);
                result.AddTriangle(Apply(m, a, px, py, pz), Apply(m, b, px, py, pz), Apply(m, c, px, py, pz));
            }
            return result;
        }

        // R^{-1} = Rx(-ax)*Ry(-ay)*Rz(-az), row-major
        private static double[] RotInv(double ax, double ay, double az)
        {
            double ca = Math.Cos(ax), sa = Math.Sin(ax);
            double cb = Math.Cos(ay), sb = Math.Sin(ay);
            double cc = Math.Cos(az), sc = Math.Sin(az);
            return new double[]
            {
                cb*cc, cb*sc, -sb,
                sa*sb*cc - ca*sc, ca*cc + sa*sb*sc, sa*cb,
                sa*sc + ca*sb*cc, ca*sb*sc - sa*cc, ca*cb
            };
        }

        private static double[] MatMul(double[] a, double[] b)
        {
            var r = new double[9];
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    r[i*3+j] = a[i*3]*b[j] + a[i*3+1]*b[3+j] + a[i*3+2]*b[6+j];
            return r;
        }

        private static double[] Identity() { return new double[] { 1,0,0, 0,1,0, 0,0,1 }; }

        // --- вспомогательные ---
        private static string GetAttr(XElement el, string name)
        {
            var a = el?.Attribute(name);
            return a?.Value ?? "";
        }

        private static XElement FindChild(XElement parent, string localName)
        {
            foreach (var c in parent.Elements())
                if (c.Name.LocalName == localName) return c;
            return null;
        }

        private static double PDouble(XElement el, string attr)
        {
            var a = el.Attribute(attr);
            if (a == null) return 0.0;
            double v;
            if (!double.TryParse(a.Value, NumberStyles.Float, Inv, out v))
                throw new InvalidDataException("Неверное числовое значение: " + a.Value);
            if (double.IsNaN(v) || double.IsInfinity(v))
                throw new InvalidDataException("Значение NaN или бесконечность: " + a.Value);
            return v;
        }

        private static double UnitMm(string unit)
        {
            if (string.IsNullOrEmpty(unit) || unit == "mm") return 1.0;
            if (unit == "cm") return 10.0;
            if (unit == "m") return 1000.0;
            if (unit == "um") return 0.001;
            return 1.0;
        }

        private static double ToRad(XElement el, string attr) { return ToRad(el, attr, false); }
        private static double ToRad(XElement el, string attr, bool isDelta)
        {
            string u = GetAttr(el, "aunit");
            bool rad = (u == "rad");
            double v = PDouble(el, attr);
            if (isDelta && string.IsNullOrEmpty(u) && attr == "deltaphi") { /* default 360 deg */ if (v == 0) v = 360.0; }
            if (rad) return v;
            return v * Math.PI / 180.0;
        }

        private static double ToAngle(XElement el, string attr, bool isRadUnit)
        {
            double v = PDouble(el, attr);
            if (isRadUnit) return v;
            return v * Math.PI / 180.0;
        }
    }
}
