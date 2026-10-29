using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using Cad2Gdml.Pipeline;
using Cad2Gdml.Scene;
using Cad2Gdml.Geometry;

namespace Cad2Gdml.Tests
{
    public static class Checks2b
    {
        public static List<KeyValuePair<string, Action>> All(string tempDir)
        {
            var checks = new List<KeyValuePair<string, Action>>();
            checks.Add(new KeyValuePair<string, Action>("5_extra_ring", () => CheckExtraRing(tempDir)));
            checks.Add(new KeyValuePair<string, Action>("6_units_3mf", () => CheckUnits3mf(tempDir)));
            checks.Add(new KeyValuePair<string, Action>("7_obj_comma", () => CheckObjComma(tempDir)));
            checks.Add(new KeyValuePair<string, Action>("8_bad_inputs", () => CheckBadInputs(tempDir)));
            return checks;
        }

        // 5. Лишнее кольцо на z=15: после кадра max Z кристалла = 0
        private static void CheckExtraRing(string tempDir)
        {
            string dir = Path.Combine(tempDir, "c5");
            Directory.CreateDirectory(dir);

            // Два соосных цилиндра равного радиуса: z 10..15 и 15..60.8
            var t1 = Fixtures.Cylinder(25.4, 5.0, 64, 10.0);
            var t2 = Fixtures.Cylinder(25.4, 45.8, 64, 15.0);
            var all = new List<double[]>();
            // внутренние торцы (все z = 15) отбрасываем, иначе сетка неманифольдна
            foreach (var tri in t1) { if (!(tri[2] == 15.0 && tri[5] == 15.0 && tri[8] == 15.0)) all.Add(tri); }
            foreach (var tri in t2) { if (!(tri[2] == 15.0 && tri[5] == 15.0 && tri[8] == 15.0)) all.Add(tri); }
            Fixtures.WriteBinaryStl(Path.Combine(dir, "crystal.stl"), all);
            Fixtures.WriteBinaryStl(Path.Combine(dir, "water_sample.stl"), Fixtures.Cylinder(30.0, 40.0, 64, 70.0));

            var result = CheckUtil.Run(dir, new[] { "crystal.stl", "water_sample.stl" }, 1.0, "+z", true, false);
            CheckUtil.Assert(result.ExitCode == 0, "Ожидался успех, код=" + result.ExitCode);

            Part crystal = null;
            foreach (var p in result.Scene.Parts)
            {
                if (p.Role == PartRole.Crystal) { crystal = p; break; }
            }
            CheckUtil.Assert(crystal != null, "Кристалл не найден в сцене");

            Vec3d mn, mx;
            crystal.Mesh.GetBounds(out mn, out mx);
            CheckUtil.Near(0.0, mx.Z, 0.01, "max Z кристалла");
        }

        // 6. 3MF с unit=centimeter: r=2.54 см → rmax=25.4 мм
        private static void CheckUnits3mf(string tempDir)
        {
            string dir = Path.Combine(tempDir, "c6");
            Directory.CreateDirectory(dir);

            var tris = Fixtures.Cylinder(2.54, 5.08, 64, 0.0);
            Fixtures3mf.Write3Mf(Path.Combine(dir, "crystal.3mf"), tris, "centimeter", "crystal");

            var result = CheckUtil.Run(dir, new[] { "crystal.3mf" }, 0.0, "+z", true, false);
            CheckUtil.Assert(result.ExitCode == 0, "Ожидался успех, код=" + result.ExitCode + " err=" + result.MainErrorId);

            var doc = CheckUtil.Gdml(result);
            XElement vol = CheckUtil.Vol(doc, "crystal");
            XElement tube = null;
            foreach (XElement c in doc.Descendants())
            {
                if (c.Name.LocalName == "tube" && c.Attribute("name") != null && c.Attribute("name").Value.Contains("crystal")) { tube = c; break; }
            }
            CheckUtil.Assert(tube != null, "Tube не найден в volume");
            CheckUtil.Near(25.4, CheckUtil.Num(tube, "rmax"), 0.01, "rmax");
            CheckUtil.Near(50.8, CheckUtil.Num(tube, "z"), 0.01, "dz");
        }

        // 7. OBJ с запятой в числе → exit 2
        private static void CheckObjComma(string tempDir)
        {
            string dir = Path.Combine(tempDir, "c7");
            Directory.CreateDirectory(dir);

            string obj = "o crystal\nv 0,5 0 0\nv 1 0 0\nv 0 1 0\nv 0 0 1\nf 1 2 3\nf 1 3 4\nf 1 2 4\nf 2 3 4\n";
            File.WriteAllText(Path.Combine(dir, "crystal.obj"), obj);
            Fixtures.WriteBinaryStl(Path.Combine(dir, "water_sample.stl"), Fixtures.Cylinder(30.0, 40.0, 64, 70.0));

            var result = CheckUtil.Run(dir, new[] { "crystal.obj", "water_sample.stl" }, 1.0, "+z", true, false);
            CheckUtil.Assert(result.ExitCode == 2, "Ожидался код 2, получено: " + result.ExitCode);
            CheckUtil.Assert(result.MainErrorId != null, "MainErrorId не должен быть null");
        }

        // 8. Неверные входы: код 2 или 3, MainErrorId не null
        private static void CheckBadInputs(string tempDir)
        {
            string dir = Path.Combine(tempDir, "c8");
            Directory.CreateDirectory(dir);

            // Пустой STL
            File.WriteAllText(Path.Combine(dir, "empty.stl"), "");
            BadRun(dir, "empty.stl");

            // Несуществующий файл
            BadRun(dir, "ghost.stl");

            // Случайные байты
            byte[] rb = new byte[80 + 4 + 100];
            for (int i = 0; i < rb.Length; i++) rb[i] = (byte)(i * 13 + 7);
            File.WriteAllBytes(Path.Combine(dir, "rand.stl"), rb);
            BadRun(dir, "rand.stl");

            // STL с NaN в вершинах
            var nanTris = new List<double[]>
            {
                new double[] { 0, 0, 1, double.NaN, 0, 0, 1, 0, 0 },
                new double[] { 0, 0, 1, 1, 0, 0, 0, double.NaN, 0 },
                new double[] { 0, 0, 1, 1, double.NaN, 0, 0, 0, double.NaN },
                new double[] { 0, 0, 1, 1, 1, 0, 0, 1, 1 }
            };
            Fixtures.WriteBinaryStl(Path.Combine(dir, "nan.stl"), nanTris);
            BadRun(dir, "nan.stl");
        }

        private static void BadRun(string dir, string input)
        {
            var result = CheckUtil.Run(dir, new[] { input }, 1.0, "+z", true, false);
            CheckUtil.Assert(result.ExitCode == 2 || result.ExitCode == 3,
                input + ": код должен быть 2 или 3, получено " + result.ExitCode);
            CheckUtil.Assert(result.MainErrorId != null, input + ": MainErrorId не null");
        }
    }
}
