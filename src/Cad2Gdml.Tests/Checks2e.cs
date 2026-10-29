using System;
using System.Collections.Generic;
using System.IO;
using Cad2Gdml.Meshes;
using Cad2Gdml.Pipeline;
using Cad2Gdml.Scene;

namespace Cad2Gdml.Tests
{
    // Проверки 2e: разбиение, инверсия, неманифольд, открытые сетки
    public static class Checks2e
    {
        public static List<KeyValuePair<string, Action>> All(string tempDir)
        {
            var list = new List<KeyValuePair<string, Action>>();
            list.Add(new KeyValuePair<string, Action>("split_role_inherit", () => SplitRoleInherit(tempDir)));
            list.Add(new KeyValuePair<string, Action>("touching_inverted_body", () => TouchingInvertedBody(tempDir)));
            list.Add(new KeyValuePair<string, Action>("nosplit_nonmanifold", () => NoSplitNonManifold(tempDir)));
            list.Add(new KeyValuePair<string, Action>("open_no_crystal_code", () => OpenNoCrystalCode(tempDir)));
            return list;
        }

        // Запуск конвейера с расширенными параметрами
        private static PipelineResult RunOpt(string dir, string[] inputs, bool allowDefault, bool dropOpen, bool keepOpen, bool noSplit)
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

        // 1. Разбиение + наследование ролей
        private static void SplitRoleInherit(string tempDir)
        {
            string dir = Path.Combine(tempDir, "split_role_inherit");
            Directory.CreateDirectory(dir);

            // Кристалл и маленький бокс в одном файле
            var tris = new List<double[]>(Fixtures.Cylinder(25.4, 50.8, 64, 10.0));
            tris.AddRange(Fixtures.Box(3, 3, 3, 100, 0, 5));
            Fixtures.WriteBinaryStl(Path.Combine(dir, "crystal.stl"), tris);
            Fixtures.WriteBinaryStl(Path.Combine(dir, "water_sample.stl"), Fixtures.Cylinder(30, 40, 64, 70));

            var r = RunOpt(dir, new[] { "crystal.stl", "water_sample.stl" }, true, false, false, false);
            CheckUtil.Assert(r.ExitCode == 0, "split_role_inherit: ExitCode != 0, код=" + r.MainErrorId);
            CheckUtil.Assert(Has(r, "I_SPLIT"), "split_role_inherit: нет I_SPLIT");
            CheckUtil.Assert(Has(r, "W_ROLE_GUESS"), "split_role_inherit: нет W_ROLE_GUESS");
            CheckUtil.Assert(!Has(r, "E_ROLE_AMBIG"), "split_role_inherit: есть E_ROLE_AMBIG");

            int cnt = 0;
            for (int i = 0; i < r.Scene.Parts.Count; i++)
            {
                if (r.Scene.Parts[i].Role == PartRole.Crystal) cnt++;
            }
            CheckUtil.Assert(cnt == 1, "split_role_inherit: кристаллов=" + cnt + ", ожидаем 1");
        }

        // 2. Касающееся инвертированное тело
        private static void TouchingInvertedBody(string tempDir)
        {
            string dir = Path.Combine(tempDir, "touching_inverted_body");
            Directory.CreateDirectory(dir);

            // Два бокса, делящие ребро; второй инвертирован
            var tris = new List<double[]>(Fixtures.Box(20, 20, 80, 0, 0, 40));
            tris.AddRange(Fixtures.FlipAll(Fixtures.Box(10, 10, 80, 15, 15, 40)));
            Fixtures.WriteBinaryStl(Path.Combine(dir, "crystal.stl"), tris);
            Fixtures.WriteBinaryStl(Path.Combine(dir, "water_sample.stl"), Fixtures.Cylinder(30, 40, 64, 70));

            var r = RunOpt(dir, new[] { "crystal.stl", "water_sample.stl" }, true, false, false, false);
            CheckUtil.Assert(r.ExitCode == 0, "touching_inverted_body: ExitCode != 0, код=" + r.MainErrorId);
            CheckUtil.Assert(Has(r, "W_MESH_FLIPPED"), "touching_inverted_body: нет W_MESH_FLIPPED");
            CheckUtil.Assert(!Has(r, "E_MESH_NONMANIFOLD"), "touching_inverted_body: есть E_MESH_NONMANIFOLD");

            // Сумма объёмов деталей, начинающихся с "crystal"
            double sum = 0.0;
            for (int i = 0; i < r.Scene.Parts.Count; i++)
            {
                Part p = r.Scene.Parts[i];
                if (p.Name.StartsWith("crystal") && p.Mesh != null)
                    sum += MeshProperties.SignedVolume(p.Mesh);
            }
            CheckUtil.Near(40000.0, sum, 1.0, "touching_inverted_body: сумма объёмов");
        }

        // 3. Без разбивки — неманифольдные рёбра
        private static void NoSplitNonManifold(string tempDir)
        {
            string dir = Path.Combine(tempDir, "nosplit_nonmanifold");
            Directory.CreateDirectory(dir);

            var tris = new List<double[]>(Fixtures.Box(20, 20, 80, 0, 0, 40));
            tris.AddRange(Fixtures.FlipAll(Fixtures.Box(10, 10, 80, 15, 15, 40)));
            Fixtures.WriteBinaryStl(Path.Combine(dir, "crystal.stl"), tris);
            Fixtures.WriteBinaryStl(Path.Combine(dir, "water_sample.stl"), Fixtures.Cylinder(30, 40, 64, 70));

            var r = RunOpt(dir, new[] { "crystal.stl", "water_sample.stl" }, true, false, false, true);
            CheckUtil.Assert(r.ExitCode == 3, "nosplit_nonmanifold: ExitCode=" + r.ExitCode + ", ожидаем 3");
            CheckUtil.Assert(r.MainErrorId == "E_MESH_NONMANIFOLD", "nosplit_nonmanifold: MainErrorId=" + r.MainErrorId);
        }

        // 4. Открытые сетки без кристалла
        private static void OpenNoCrystalCode(string tempDir)
        {
            string dir = Path.Combine(tempDir, "open_no_crystal_code");
            Directory.CreateDirectory(dir);

            // Открытые цилиндры (без слова crystal в имени)
            Fixtures.WriteBinaryStl(Path.Combine(dir, "a.stl"), Fixtures.RemoveOne(Fixtures.Cylinder(10, 20, 32, 0), 3));
            Fixtures.WriteBinaryStl(Path.Combine(dir, "b.stl"), Fixtures.RemoveOne(Fixtures.Cylinder(10, 20, 32, 50), 3));

            // Без флагов → E_MESH_OPEN
            var r1 = RunOpt(dir, new[] { "a.stl", "b.stl" }, true, false, false, false);
            CheckUtil.Assert(r1.ExitCode == 3, "open_no_crystal (plain): ExitCode=" + r1.ExitCode);
            CheckUtil.Assert(r1.MainErrorId == "E_MESH_OPEN", "open_no_crystal (plain): " + r1.MainErrorId);

            // dropOpen → E_SCENE_EMPTY
            var r2 = RunOpt(dir, new[] { "a.stl", "b.stl" }, true, true, false, false);
            CheckUtil.Assert(r2.ExitCode == 3, "open_no_crystal (drop): ExitCode=" + r2.ExitCode);
            CheckUtil.Assert(r2.MainErrorId == "E_SCENE_EMPTY", "open_no_crystal (drop): " + r2.MainErrorId);

            // keepOpen → успех + W_MESH_OPEN_KEPT
            var r3 = RunOpt(dir, new[] { "a.stl", "b.stl" }, true, false, true, false);
            CheckUtil.Assert(r3.ExitCode == 0, "open_no_crystal (keep): ExitCode=" + r3.ExitCode + " " + r3.MainErrorId);
            CheckUtil.Assert(Has(r3, "W_MESH_OPEN_KEPT"), "open_no_crystal (keep): нет W_MESH_OPEN_KEPT");
        }
    }
}
