using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Cad2Gdml.Geometry;
using Cad2Gdml.Materials;
using Cad2Gdml.Meshes;
using Cad2Gdml.Pipeline;
using Cad2Gdml.Scene;

namespace Cad2Gdml.Tests
{
    // Проверки 2j: density overrides, принудительная ось фронта
    public static class Checks2j
    {
        public static List<KeyValuePair<string, Action>> All(string tempDir)
        {
            var list = new List<KeyValuePair<string, Action>>();
            list.Add(new KeyValuePair<string, Action>("density_override_gdml", () => DensityOverrideGdml(tempDir)));
            list.Add(new KeyValuePair<string, Action>("density_override_unknown_material", () => DensityOverrideUnknown(tempDir)));
            list.Add(new KeyValuePair<string, Action>("front_axis_forced", () => FrontAxisForced(tempDir)));
            return list;
        }

        // Запуск конвейера с фиксированными параметрами
        private static PipelineResult RunPipe(string dir, string[] inputs, string frontSign,
            List<KeyValuePair<string, double>> densityOverrides)
        {
            Directory.CreateDirectory(dir);
            var opt = new PipelineOptions();
            opt.Inputs = new List<string>();
            foreach (string inp in inputs)
                opt.Inputs.Add(Path.IsPathRooted(inp) ? inp : Path.Combine(dir, inp));
            opt.UnitToMm = 1.0;
            opt.FrontSign = frontSign;
            opt.OutDir = dir;
            opt.BaseName = "o";
            opt.AllowDefaultMaterial = false;
            opt.NoRepair = true;
            opt.DensityOverrides = densityOverrides;
            return Cad2GdmlPipeline.Run(opt);
        }

        // 1. Density override → GDML содержит производное вещество
        private static void DensityOverrideGdml(string tempDir)
        {
            string dir = Path.Combine(tempDir, "density_override");
            Directory.CreateDirectory(dir);

            Fixtures.WriteBinaryStl(Path.Combine(dir, "Crystal_NaI.stl"), Fixtures.Box(10, 10, 30, 0, 0, 0));
            Fixtures.WriteBinaryStl(Path.Combine(dir, "Housing.stl"), Fixtures.Box(20, 20, 50, 0, 0, 0));

            var dens = new List<KeyValuePair<string, double>>();
            dens.Add(new KeyValuePair<string, double>("G4_Al", 2.5));

            var r = RunPipe(dir, new[] { "Crystal_NaI.stl", "Housing.stl" }, "+z", dens);

            if (r.ExitCode != 0)
                throw new Exception("density_override_gdml: ExitCode=" + r.ExitCode + " " + r.MainErrorId);

            string txt = File.ReadAllText(r.GdmlPath);
            if (txt.IndexOf("<material name=\"Al_rho2.5\"", StringComparison.Ordinal) < 0)
                throw new Exception("density_override_gdml: нет <material name=\"Al_rho2.5\"");
            if (txt.IndexOf("<D value=\"2.5\"", StringComparison.Ordinal) < 0)
                throw new Exception("density_override_gdml: нет <D value=\"2.5\"");
            if (txt.IndexOf("<materialref ref=\"Al_rho2.5\"/>", StringComparison.Ordinal) < 0)
                throw new Exception("density_override_gdml: нет <materialref ref=\"Al_rho2.5\"/>");
            if (txt.IndexOf("ref=\"G4_Al\"", StringComparison.Ordinal) >= 0)
                throw new Exception("density_override_gdml: найдена ref=\"G4_Al\" (не должна быть)");
        }

        // 2. Density override с неизвестным веществом → E_MAT_UNKNOWN
        private static void DensityOverrideUnknown(string tempDir)
        {
            string dir = Path.Combine(tempDir, "density_unknown");
            Directory.CreateDirectory(dir);

            Fixtures.WriteBinaryStl(Path.Combine(dir, "Crystal_NaI.stl"), Fixtures.Box(10, 10, 30, 0, 0, 0));
            Fixtures.WriteBinaryStl(Path.Combine(dir, "Housing.stl"), Fixtures.Box(20, 20, 50, 0, 0, 0));

            var dens = new List<KeyValuePair<string, double>>();
            dens.Add(new KeyValuePair<string, double>("Unobtainium", 1.0));

            var r = RunPipe(dir, new[] { "Crystal_NaI.stl", "Housing.stl" }, "+z", dens);

            if (r.ExitCode == 0)
                throw new Exception("density_override_unknown_material: ExitCode=0, ожидаем ненулевой");
            if (r.MainErrorId != "E_MAT_UNKNOWN")
                throw new Exception("density_override_unknown_material: MainErrorId=" + r.MainErrorId + ", ожидаем E_MAT_UNKNOWN");
        }

        // 3. Принудительная ось фронта: +x и +y
        private static void FrontAxisForced(string tempDir)
        {
            // +x: длинная сторона 30 (x) → z, max z ≈ 0, min z ≈ −30
            {
                string dir = Path.Combine(tempDir, "front_x");
                Directory.CreateDirectory(dir);

                Fixtures.WriteBinaryStl(Path.Combine(dir, "Crystal_NaI.stl"), Fixtures.Box(30, 10, 10, 0, 0, 0));
                Fixtures.WriteBinaryStl(Path.Combine(dir, "Housing.stl"), Fixtures.Box(40, 20, 20, 0, 0, 0));

                var r = RunPipe(dir, new[] { "Crystal_NaI.stl", "Housing.stl" }, "+x", null);

                if (r.ExitCode != 0)
                    throw new Exception("front_axis_forced(+x): ExitCode=" + r.ExitCode + " " + r.MainErrorId);

                Part crystal = FindCrystal(r.Scene);
                Vec3d lo, hi;
                crystal.Mesh.GetBounds(out lo, out hi);

                if (Math.Abs(hi.Z) > 1e-6)
                    throw new Exception("front_axis_forced(+x): max z=" + hi.Z.ToString(CultureInfo.InvariantCulture) + ", ожидаем ~0");
                if (Math.Abs(lo.Z - (-30.0)) > 1e-6)
                    throw new Exception("front_axis_forced(+x): min z=" + lo.Z.ToString(CultureInfo.InvariantCulture) + ", ожидаем ~-30");
            }

            // +y: сторона y (10) → z, min z ≈ −10
            {
                string dir = Path.Combine(tempDir, "front_y");
                Directory.CreateDirectory(dir);

                Fixtures.WriteBinaryStl(Path.Combine(dir, "Crystal_NaI.stl"), Fixtures.Box(30, 10, 10, 0, 0, 0));
                Fixtures.WriteBinaryStl(Path.Combine(dir, "Housing.stl"), Fixtures.Box(40, 20, 20, 0, 0, 0));

                var r = RunPipe(dir, new[] { "Crystal_NaI.stl", "Housing.stl" }, "+y", null);

                if (r.ExitCode != 0)
                    throw new Exception("front_axis_forced(+y): ExitCode=" + r.ExitCode + " " + r.MainErrorId);

                Part crystal = FindCrystal(r.Scene);
                Vec3d lo, hi;
                crystal.Mesh.GetBounds(out lo, out hi);

                if (Math.Abs(lo.Z - (-10.0)) > 1e-6)
                    throw new Exception("front_axis_forced(+y): min z=" + lo.Z.ToString(CultureInfo.InvariantCulture) + ", ожидаем ~-10");
            }
        }

        // Поиск кристалла по имени
        private static Part FindCrystal(SceneModel scene)
        {
            for (int i = 0; i < scene.Parts.Count; i++)
            {
                if (scene.Parts[i].Name.IndexOf("Crystal", StringComparison.OrdinalIgnoreCase) >= 0)
                    return scene.Parts[i];
            }
            throw new Exception("front_axis_forced: деталь Crystal не найдена в сцене");
        }
    }
}
