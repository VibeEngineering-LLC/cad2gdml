using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Cad2Gdml.Scene;
using Cad2Gdml.Recognize;
using Cad2Gdml.Gdml;
using Cad2Gdml.Pipeline;

namespace Cad2Gdml.Tests
{
    // Мини-раннер тестов без внешних пакетов
    public static class Program
    {
        private static string _tempDir;

        public static int Main(string[] args)
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "cad2gdml_tests_" + Guid.NewGuid().ToString("N"));
            int passed = 0;
            int failed = 0;

            var checks = new List<KeyValuePair<string, Action>>
            {
                new KeyValuePair<string, Action>("scene_ok", CheckSceneOk),
                new KeyValuePair<string, Action>("determinism", CheckDeterminism),
                new KeyValuePair<string, Action>("prism_not_cylinder", CheckPrismNotCylinder),
                new KeyValuePair<string, Action>("cylinder_24_is_tube", CheckCylinder24IsTube),
                new KeyValuePair<string, Action>("unit_missing", CheckUnitMissing),
                new KeyValuePair<string, Action>("open_mesh", CheckOpenMesh),
                new KeyValuePair<string, Action>("flipped_mesh", CheckFlippedMesh),
                new KeyValuePair<string, Action>("inch_unit", CheckInchUnit),
                new KeyValuePair<string, Action>("no_crystal", CheckNoCrystal),
                new KeyValuePair<string, Action>("png_written", CheckPngWritten)
            };

            checks.AddRange(Checks2a.All(_tempDir));
            checks.AddRange(Checks2b.All(_tempDir));
            checks.AddRange(Checks2c.All(_tempDir));
            checks.AddRange(Checks2d.All(_tempDir));
            checks.AddRange(Checks2e.All(_tempDir));
            checks.AddRange(Checks2f.All(_tempDir));
            checks.AddRange(Checks2g.All(_tempDir));
            checks.AddRange(Checks2h.All(_tempDir));
            checks.AddRange(ChecksGui.All(_tempDir));

            foreach (var kv in checks)
            {
                try
                {
                    kv.Value();
                    Console.WriteLine("PASS " + kv.Key);
                    passed++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("FAIL " + kv.Key + ": " + ex.Message);
                    failed++;
                }
            }

            Console.WriteLine("TESTS passed=" + passed.ToString(CultureInfo.InvariantCulture)
                + " failed=" + failed.ToString(CultureInfo.InvariantCulture));

            // Очистка временного каталога
            try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); }
            catch { /* ignore */ }

            return (failed == 0) ? 0 : 1;
        }

        // 1. Базовая сцена: Tube, размеры кристалла
        private static void CheckSceneOk()
        {
            string dir = Path.Combine(_tempDir, "scene_ok");
            Fixtures.WriteScene(dir);

            var opt = MakeOpt(dir, "out");
            var result = Cad2GdmlPipeline.Run(opt);

            Assert(result.ExitCode == 0, "ExitCode=" + result.ExitCode.ToString(CultureInfo.InvariantCulture));
            Assert(File.Exists(result.GdmlPath), "GDML file missing");
            Assert(result.Gdml.Tubes >= 1, "Tubes < 1");

            Part crystal = FindCrystal(result.Scene);
            Assert(crystal != null, "crystal not found");
            Assert(crystal.Shape != null, "shape is null");
            Assert(crystal.Shape.Kind == SolidKind.Tube, "kind=" + crystal.Shape.Kind.ToString());
            AssertNear(25.4, crystal.Shape.RMax, 0.01, "RMax");
            double len = crystal.Shape.Z1 - crystal.Shape.Z0;
            AssertNear(50.8, len, 0.01, "length");
        }

        // 2. Детерминированность SHA256
        private static void CheckDeterminism()
        {
            string d1 = Path.Combine(_tempDir, "det1");
            string d2 = Path.Combine(_tempDir, "det2");
            Fixtures.WriteScene(d1);
            Fixtures.WriteScene(d2);

            var r1 = Cad2GdmlPipeline.Run(MakeOpt(d1, "out"));
            var r2 = Cad2GdmlPipeline.Run(MakeOpt(d2, "out"));

            Assert(r1.ExitCode == 0, "r1 exit=" + r1.ExitCode.ToString(CultureInfo.InvariantCulture));
            Assert(r2.ExitCode == 0, "r2 exit=" + r2.ExitCode.ToString(CultureInfo.InvariantCulture));
            Assert(r1.Gdml.Sha256 == r2.Gdml.Sha256, "sha256 mismatch");
        }

        // 3. Призма (6 граней) не распознаётся как Tube
        private static void CheckPrismNotCylinder()
        {
            string dir = Path.Combine(_tempDir, "prism");
            Directory.CreateDirectory(dir);
            Fixtures.WriteBinaryStl(Path.Combine(dir, "crystal.stl"), Fixtures.Prism(25.4, 50.8, 6));

            var opt = new PipelineOptions();
            opt.Inputs = new List<string> { Path.Combine(dir, "crystal.stl") };
            opt.FrontSign = "+z";
            opt.UnitToMm = 1.0;
            opt.OutDir = dir;
            opt.BaseName = "out";
            opt.AllowDefaultMaterial = true;

            var result = Cad2GdmlPipeline.Run(opt);
            Assert(result.ExitCode == 0, "exit=" + result.ExitCode.ToString(CultureInfo.InvariantCulture));

            Part crystal = FindCrystal(result.Scene);
            Assert(crystal != null, "no crystal");
            Assert(crystal.Shape != null, "shape null");
            Assert(crystal.Shape.Kind == SolidKind.Tessellated, "expected Tessellated, got " + crystal.Shape.Kind.ToString());
        }

        // 4. Цилиндр с 24 сегментами — Tube
        private static void CheckCylinder24IsTube()
        {
            string dir = Path.Combine(_tempDir, "cyl24");
            Directory.CreateDirectory(dir);
            Fixtures.WriteBinaryStl(Path.Combine(dir, "crystal.stl"), Fixtures.Cylinder(25.4, 50.8, 24, 0.0));

            var opt = new PipelineOptions();
            opt.Inputs = new List<string> { Path.Combine(dir, "crystal.stl") };
            opt.FrontSign = "+z";
            opt.UnitToMm = 1.0;
            opt.OutDir = dir;
            opt.BaseName = "out";
            opt.AllowDefaultMaterial = true;

            var result = Cad2GdmlPipeline.Run(opt);
            Assert(result.ExitCode == 0, "exit=" + result.ExitCode.ToString(CultureInfo.InvariantCulture));

            Part crystal = FindCrystal(result.Scene);
            Assert(crystal != null, "no crystal");
            Assert(crystal.Shape != null, "shape null");
            Assert(crystal.Shape.Kind == SolidKind.Tube, "kind=" + crystal.Shape.Kind.ToString());
        }

        // 5. Единицы не заданы — ExitCode 2
        private static void CheckUnitMissing()
        {
            string dir = Path.Combine(_tempDir, "unit_missing");
            Directory.CreateDirectory(dir);
            Fixtures.WriteBinaryStl(Path.Combine(dir, "crystal.stl"), Fixtures.Cylinder(10, 20, 32, 0));

            var opt = new PipelineOptions();
            opt.Inputs = new List<string> { Path.Combine(dir, "crystal.stl") };
            opt.UnitToMm = 0.0;
            opt.OutDir = dir;
            opt.BaseName = "out";
            opt.AllowDefaultMaterial = true;

            var result = Cad2GdmlPipeline.Run(opt);
            Assert(result.ExitCode == 2, "exit=" + result.ExitCode.ToString(CultureInfo.InvariantCulture));

            bool found = HasDiag(result.Scene, "E_UNIT_MISSING");
            Assert(found, "E_UNIT_MISSING not found");
        }

        // 6. Открытая сетка — ExitCode 3
        private static void CheckOpenMesh()
        {
            string dir = Path.Combine(_tempDir, "open_mesh");
            Directory.CreateDirectory(dir);
            var tris = Fixtures.Cylinder(25.4, 50.8, 64, 10.0);
            var open = Fixtures.RemoveOne(tris, 0);
            Fixtures.WriteBinaryStl(Path.Combine(dir, "crystal.stl"), open);

            var opt = new PipelineOptions();
            opt.Inputs = new List<string> { Path.Combine(dir, "crystal.stl") };
            opt.UnitToMm = 1.0;
            opt.OutDir = dir;
            opt.BaseName = "out";
            opt.AllowDefaultMaterial = true;
            opt.NoRepair = true;

            var result = Cad2GdmlPipeline.Run(opt);
            Assert(result.ExitCode == 3, "exit=" + result.ExitCode.ToString(CultureInfo.InvariantCulture));
        }

        // 7. Инвертированная обвязка образца — W_MESH_FLIPPED, объёмы > 0
        private static void CheckFlippedMesh()
        {
            string dir = Path.Combine(_tempDir, "flipped");
            Directory.CreateDirectory(dir);
            Fixtures.WriteBinaryStl(Path.Combine(dir, "crystal.stl"), Fixtures.Cylinder(25.4, 50.8, 64, 10.0));
            Fixtures.WriteBinaryStl(Path.Combine(dir, "shell.stl"), Fixtures.HollowCylinder(28.0, 25.9, 60.0, 64, 5.0));
            var flipped = Fixtures.FlipAll(Fixtures.Cylinder(30.0, 40.0, 64, 70.0));
            Fixtures.WriteBinaryStl(Path.Combine(dir, "water_sample.stl"), flipped);

            var opt = MakeOpt(dir, "out");
            var result = Cad2GdmlPipeline.Run(opt);

            Assert(result.ExitCode == 0, "exit=" + result.ExitCode.ToString(CultureInfo.InvariantCulture));
            Assert(HasDiag(result.Scene, "W_MESH_FLIPPED"), "W_MESH_FLIPPED not found");

            // Все объёмы положительны
            for (int i = 0; i < result.Scene.Parts.Count; i++)
            {
                Part p = result.Scene.Parts[i];
                Assert(p.VolumeMm3 > 0.0, "volume<=0 for " + p.Name);
            }
        }

        // 8. Дюймы: масштаб 1/25.4, UnitToMm=25.4
        private static void CheckInchUnit()
        {
            string dir = Path.Combine(_tempDir, "inch");
            Directory.CreateDirectory(dir);
            double s = 1.0 / 25.4;
            Fixtures.WriteBinaryStl(Path.Combine(dir, "crystal.stl"), ScaleTris(Fixtures.Cylinder(25.4, 50.8, 64, 10.0), s));
            Fixtures.WriteBinaryStl(Path.Combine(dir, "shell.stl"), ScaleTris(Fixtures.HollowCylinder(28.0, 25.9, 60.0, 64, 5.0), s));
            Fixtures.WriteBinaryStl(Path.Combine(dir, "water_sample.stl"), ScaleTris(Fixtures.Cylinder(30.0, 40.0, 64, 70.0), s));

            var opt = new PipelineOptions();
            opt.Inputs = new List<string>
            {
                Path.Combine(dir, "crystal.stl"),
                Path.Combine(dir, "shell.stl"),
                Path.Combine(dir, "water_sample.stl")
            };
            opt.UnitToMm = 25.4;
            opt.OutDir = dir;
            opt.BaseName = "out";
            opt.AllowDefaultMaterial = true;

            var result = Cad2GdmlPipeline.Run(opt);
            Assert(result.ExitCode == 0, "exit=" + result.ExitCode.ToString(CultureInfo.InvariantCulture));

            Part crystal = FindCrystal(result.Scene);
            Assert(crystal != null, "no crystal");
            Assert(crystal.Shape != null, "shape null");
            AssertNear(25.4, crystal.Shape.RMax, 0.01, "RMax(inch)");
        }

        // 9. Нет кристалла — ExitCode 4
        private static void CheckNoCrystal()
        {
            string dir = Path.Combine(_tempDir, "no_crystal");
            Directory.CreateDirectory(dir);
            Fixtures.WriteBinaryStl(Path.Combine(dir, "a.stl"), Fixtures.Cylinder(10, 20, 32, 0));
            Fixtures.WriteBinaryStl(Path.Combine(dir, "b.stl"), Fixtures.Cylinder(15, 25, 32, 0));

            var opt = new PipelineOptions();
            opt.Inputs = new List<string>
            {
                Path.Combine(dir, "a.stl"),
                Path.Combine(dir, "b.stl")
            };
            opt.UnitToMm = 1.0;
            opt.OutDir = dir;
            opt.BaseName = "out";
            opt.AllowDefaultMaterial = true;

            var result = Cad2GdmlPipeline.Run(opt);
            Assert(result.ExitCode == 0, "exit=" + result.ExitCode.ToString(CultureInfo.InvariantCulture));
            Assert(result.Diagnostics.Exists(d => d.Code == "W_NO_CRYSTAL"), "нет W_NO_CRYSTAL");
        }

        // 10. PNG существует и > 1000 байт
        private static void CheckPngWritten()
        {
            string dir = Path.Combine(_tempDir, "png");
            Fixtures.WriteScene(dir);

            var opt = MakeOpt(dir, "out");
            var result = Cad2GdmlPipeline.Run(opt);

            Assert(result.ExitCode == 0, "exit=" + result.ExitCode.ToString(CultureInfo.InvariantCulture));
            Assert(!string.IsNullOrEmpty(result.PngPath), "PngPath is empty");
            Assert(File.Exists(result.PngPath), "PNG file missing");
            long len = new FileInfo(result.PngPath).Length;
            Assert(len > 1000, "PNG too small: " + len.ToString(CultureInfo.InvariantCulture));
        }

        // --- Вспомогательные ---

        // Стандартные параметры для полной сцены
        private static PipelineOptions MakeOpt(string dir, string baseName)
        {
            var opt = new PipelineOptions();
            opt.Inputs = new List<string>
            {
                Path.Combine(dir, "crystal.stl"),
                Path.Combine(dir, "shell.stl"),
                Path.Combine(dir, "water_sample.stl")
            };
            opt.UnitToMm = 1.0;
            opt.OutDir = dir;
            opt.BaseName = baseName;
            opt.AllowDefaultMaterial = true;
            return opt;
        }

        // Поиск детали с ролью Crystal
        private static Part FindCrystal(SceneModel scene)
        {
            for (int i = 0; i < scene.Parts.Count; i++)
            {
                if (scene.Parts[i].Role == PartRole.Crystal)
                    return scene.Parts[i];
            }
            return null;
        }

        // Есть ли диагностика с кодом
        private static bool HasDiag(SceneModel scene, string code)
        {
            for (int i = 0; i < scene.Diagnostics.Count; i++)
            {
                if (scene.Diagnostics[i].Code == code)
                    return true;
            }
            return false;
        }

        // Масштабирование всех координат
        private static IList<double[]> ScaleTris(IList<double[]> tris, double s)
        {
            var result = new List<double[]>(tris.Count);
            for (int i = 0; i < tris.Count; i++)
            {
                double[] t = tris[i];
                double[] f = new double[9];
                for (int j = 0; j < 9; j++) f[j] = t[j] * s;
                result.Add(f);
            }
            return result;
        }

        // Утверждение: бросает исключение при false
        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
        }

        // Утверждение близости чисел
        private static void AssertNear(double expected, double actual, double tol, string what)
        {
            if (Math.Abs(expected - actual) > tol)
                throw new Exception(what + ": expected " + expected.ToString(CultureInfo.InvariantCulture)
                    + " got " + actual.ToString(CultureInfo.InvariantCulture));
        }
    }
}
