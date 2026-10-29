using System;
using System.Collections.Generic;
using System.IO;
using Cad2Gdml.Meshes;
using Cad2Gdml.Pipeline;
using Cad2Gdml.Scene;

namespace Cad2Gdml.Tests
{
    // Проверки 2f: граничные касания, выбор кристалла по объёму, неоднозначность, override веществ
    public static class Checks2f
    {
        public static List<KeyValuePair<string, Action>> All(string tempDir)
        {
            var list = new List<KeyValuePair<string, Action>>();
            list.Add(new KeyValuePair<string, Action>("face_touch_kept", () => FaceTouchKept(tempDir)));
            list.Add(new KeyValuePair<string, Action>("crystal_by_volume", () => CrystalByVolume(tempDir)));
            list.Add(new KeyValuePair<string, Action>("separate_files_ambig", () => SeparateFilesAmbig(tempDir)));
            list.Add(new KeyValuePair<string, Action>("material_override", () => MaterialOverride(tempDir)));
            return list;
        }

        // Запуск конвейера с расширенными параметрами
        private static PipelineResult RunOpt(string dir, string[] inputs, bool allowDefault, bool dropOpen, bool keepOpen, bool noSplit, List<KeyValuePair<string, string>> overrides)
        {
            Directory.CreateDirectory(dir);
            var opt = new PipelineOptions();
            opt.Inputs = new List<string>();
            foreach (string inp in inputs)
                opt.Inputs.Add(Path.IsPathRooted(inp) ? inp : Path.Combine(dir, inp));
            opt.UnitToMm = 1.0;
            opt.FrontSign = "auto";
            opt.OutDir = dir;
            opt.BaseName = "o";
            opt.AllowDefaultMaterial = allowDefault;
            opt.DropOpen = dropOpen;
            opt.KeepOpen = keepOpen;
            opt.NoSplit = noSplit;
            opt.MaterialOverrides = overrides;
            return Cad2GdmlPipeline.Run(opt);
        }

        // Есть ли диагностика с кодом
        private static bool Has(PipelineResult r, string code)
        {
            for (int i = 0; i < r.Diagnostics.Count; i++)
            {
                if (r.Diagnostics[i].Code == code) return true;
            }
            return false;
        }

        // 1. Касание по полной грани — 24 треугольника, без E_MESH_OPEN
        private static void FaceTouchKept(string tempDir)
        {
            string dir = Path.Combine(tempDir, "face_touch_kept");
            Directory.CreateDirectory(dir);

            // Два бокса, делящие общую грань 20×20
            var tris = new List<double[]>(Fixtures.Box(20, 20, 40, 0, 0, 20));
            tris.AddRange(Fixtures.Box(20, 20, 40, 0, 0, 60));
            Fixtures.WriteBinaryStl(Path.Combine(dir, "crystal.stl"), tris);
            Fixtures.WriteBinaryStl(Path.Combine(dir, "water_sample.stl"), Fixtures.Cylinder(30, 40, 64, 70));

            var r = RunOpt(dir, new[] { "crystal.stl", "water_sample.stl" }, true, false, false, false, null);
            CheckUtil.Assert(r.ExitCode == 0, "face_touch_kept: ExitCode=" + r.ExitCode + " " + r.MainErrorId);
            CheckUtil.Assert(!Has(r, "E_MESH_OPEN"), "face_touch_kept: есть E_MESH_OPEN");

            // Сумма треугольников деталей, начинающихся с "crystal"
            int sum = 0;
            for (int i = 0; i < r.Scene.Parts.Count; i++)
            {
                if (r.Scene.Parts[i].Name.StartsWith("crystal"))
                    sum += r.Scene.Parts[i].Mesh.TriangleCount;
            }
            CheckUtil.Assert(sum == 24, "face_touch_kept: треугольников=" + sum + ", ожидаем 24");
        }

        // 2. Кристалл выбирается по объёму (большой бокс, а не мелкий цилиндр)
        private static void CrystalByVolume(string tempDir)
        {
            string dir = Path.Combine(tempDir, "crystal_by_volume");
            Directory.CreateDirectory(dir);

            // Большой куб + мелкий цилиндр в одном файле
            var tris = new List<double[]>(Fixtures.Box(100, 100, 100, 0, 0, 50));
            tris.AddRange(Fixtures.Cylinder(2, 4, 64, 300));
            Fixtures.WriteBinaryStl(Path.Combine(dir, "crystal.stl"), tris);
            Fixtures.WriteBinaryStl(Path.Combine(dir, "water_sample.stl"), Fixtures.Cylinder(30, 40, 64, 70));

            var r = RunOpt(dir, new[] { "crystal.stl", "water_sample.stl" }, true, false, false, false, null);
            CheckUtil.Assert(r.ExitCode == 0, "crystal_by_volume: ExitCode=" + r.ExitCode + " " + r.MainErrorId);

            // Ищем деталь с ролью Crystal
            Part crystal = null;
            for (int i = 0; i < r.Scene.Parts.Count; i++)
            {
                if (r.Scene.Parts[i].Role == PartRole.Crystal)
                {
                    crystal = r.Scene.Parts[i];
                    break;
                }
            }
            CheckUtil.Assert(crystal != null, "crystal_by_volume: кристалл не найден");
            double vol = Math.Abs(MeshProperties.SignedVolume(crystal.Mesh));
            CheckUtil.Assert(vol > 900000.0, "crystal_by_volume: объём=" + vol + ", ожидаем >900000");
        }

        // 3. Два независимых файла-кристалла — неоднозначность
        private static void SeparateFilesAmbig(string tempDir)
        {
            string dir = Path.Combine(tempDir, "separate_files_ambig");
            Directory.CreateDirectory(dir);

            // Два бокса с именем crystal в отдельных файлах, без воды
            Fixtures.WriteBinaryStl(Path.Combine(dir, "crystal_c1.stl"), Fixtures.Box(10, 10, 10, 0, 0, 0));
            Fixtures.WriteBinaryStl(Path.Combine(dir, "crystal_c2.stl"), Fixtures.Box(12, 12, 12, 50, 0, 0));

            var r = RunOpt(dir, new[] { "crystal_c1.stl", "crystal_c2.stl" }, true, false, false, false, null);
            CheckUtil.Assert(r.ExitCode == 4, "separate_files_ambig: ExitCode=" + r.ExitCode);
            CheckUtil.Assert(r.MainErrorId == "E_ROLE_AMBIG", "separate_files_ambig: " + r.MainErrorId);
        }

        // 4. Вручное назначение вещества через override
        private static void MaterialOverride(string tempDir)
        {
            string dir = Path.Combine(tempDir, "material_override");
            Directory.CreateDirectory(dir);

            Fixtures.WriteBinaryStl(Path.Combine(dir, "crystal.stl"), Fixtures.Cylinder(25.4, 50.8, 64, 10));
            Fixtures.WriteBinaryStl(Path.Combine(dir, "water_sample.stl"), Fixtures.Cylinder(30, 40, 64, 70));

            // Удачный override: вода → G4_Al
            var ov1 = new List<KeyValuePair<string, string>>();
            ov1.Add(new KeyValuePair<string, string>("=water_sample", "G4_Al"));
            var r1 = RunOpt(dir, new[] { "crystal.stl", "water_sample.stl" }, true, false, false, false, ov1);
            CheckUtil.Assert(r1.ExitCode == 0, "material_override: ExitCode=" + r1.ExitCode + " " + r1.MainErrorId);
            CheckUtil.Assert(Has(r1, "I_MAT_MANUAL"), "material_override: нет I_MAT_MANUAL");

            // Проверяем материал детали water_sample
            Part water = null;
            for (int i = 0; i < r1.Scene.Parts.Count; i++)
            {
                if (r1.Scene.Parts[i].Name == "water_sample") water = r1.Scene.Parts[i];
            }
            CheckUtil.Assert(water != null, "material_override: деталь water_sample не найдена");
            CheckUtil.Assert(water.Material == "G4_Al", "material_override: Material=" + water.Material + ", ожидаем G4_Al");

            // Неправильное вещество → E_MAT_UNKNOWN
            var ov2 = new List<KeyValuePair<string, string>>();
            ov2.Add(new KeyValuePair<string, string>("=water_sample", "NoSuchMaterialXYZ"));
            var r2 = RunOpt(dir, new[] { "crystal.stl", "water_sample.stl" }, true, false, false, false, ov2);
            CheckUtil.Assert(r2.ExitCode == 5, "material_override (bad): ExitCode=" + r2.ExitCode);
            CheckUtil.Assert(r2.MainErrorId == "E_MAT_UNKNOWN", "material_override (bad): " + r2.MainErrorId);
        }
    }
}
