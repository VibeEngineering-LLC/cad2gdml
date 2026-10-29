using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Cad2Gdml.Materials;
using Cad2Gdml.Meshes;
using Cad2Gdml.Pipeline;
using Cad2Gdml.Scene;

namespace Cad2Gdml.Tests
{
    // Проверки 2i: materials.json, OBJ usemtl, встроенные правила
    public static class Checks2i
    {
        public static List<KeyValuePair<string, Action>> All(string tempDir)
        {
            var list = new List<KeyValuePair<string, Action>>();
            list.Add(new KeyValuePair<string, Action>("materials_json_next_to_model", () => MatJsonNextToModel(tempDir)));
            list.Add(new KeyValuePair<string, Action>("materials_json_absent_unassigned", () => MatJsonAbsent(tempDir)));
            list.Add(new KeyValuePair<string, Action>("obj_usemtl_fallback", () => ObjUsemtl(tempDir)));
            list.Add(new KeyValuePair<string, Action>("builtin_rules_new", BuiltinRulesNew));
            return list;
        }

        // Запуск конвейера с фиксированными параметрами
        private static PipelineResult RunPipe(string dir, string[] inputs, bool allowDefault)
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
            opt.NoRepair = true;
            return Cad2GdmlPipeline.Run(opt);
        }

        // Есть ли диагностика с данным кодом
        private static bool HasDiag(PipelineResult r, string code)
        {
            for (int i = 0; i < r.Diagnostics.Count; i++)
                if (r.Diagnostics[i].Code == code) return true;
            return false;
        }

        // 1. materials.json рядом с моделью
        private static void MatJsonNextToModel(string tempDir)
        {
            string dir = Path.Combine(tempDir, "mat_json_next");
            Directory.CreateDirectory(dir);

            Fixtures.WriteBinaryStl(Path.Combine(dir, "Housing_x.stl"), Fixtures.Box(20, 20, 20, 0, 0, 0));

            // Правило ^Housing → G4_Cu, default G4_AIR
            string json = "{\"stl_rules\":[[\"^Housing\",\"G4_Cu\"]],\"stl_default\":\"G4_AIR\"}";
            File.WriteAllText(Path.Combine(dir, "materials.json"), json, new UTF8Encoding(false));

            var r = RunPipe(dir, new[] { "Housing_x.stl" }, false);

            if (r.ExitCode != 0 && (r.Scene == null || r.Scene.Parts.Count == 0))
                throw new Exception("materials_json_next_to_model: ExitCode=" + r.ExitCode + " " + r.MainErrorId);

            Part part = null;
            for (int i = 0; i < r.Scene.Parts.Count; i++)
                if (r.Scene.Parts[i].Name == "Housing_x") { part = r.Scene.Parts[i]; break; }
            if (part == null) throw new Exception("materials_json_next_to_model: деталь Housing_x не найдена");
            if (part.Material != "G4_Cu")
                throw new Exception("materials_json_next_to_model: Material=" + part.Material + ", ожидаем G4_Cu");
            if (!HasDiag(r, "I_MAT_FILE"))
                throw new Exception("materials_json_next_to_model: нет I_MAT_FILE");
        }

        // 2. Нет materials.json, имя не совпадает — E_MAT_UNASSIGNED
        private static void MatJsonAbsent(string tempDir)
        {
            string dir = Path.Combine(tempDir, "mat_json_absent");
            Directory.CreateDirectory(dir);

            Fixtures.WriteBinaryStl(Path.Combine(dir, "Zzz_x.stl"), Fixtures.Box(20, 20, 20, 0, 0, 0));

            var r = RunPipe(dir, new[] { "Zzz_x.stl" }, false);

            if (r.ExitCode != 5)
                throw new Exception("materials_json_absent_unassigned: ExitCode=" + r.ExitCode + ", ожидаем 5");
            if (!HasDiag(r, "E_MAT_UNASSIGNED"))
                throw new Exception("materials_json_absent_unassigned: нет E_MAT_UNASSIGNED");
            bool mentions = false;
            for (int i = 0; i < r.Diagnostics.Count; i++)
                if (r.Diagnostics[i].Code == "E_MAT_UNASSIGNED" && r.Diagnostics[i].Message.Contains("Zzz_x"))
                    mentions = true;
            if (!mentions)
                throw new Exception("materials_json_absent_unassigned: сообщение не упоминает Zzz_x");
        }

        // 3. OBJ usemtl → вещество из исходного файла
        private static void ObjUsemtl(string tempDir)
        {
            string dir = Path.Combine(tempDir, "obj_usemtl");
            Directory.CreateDirectory(dir);

            File.WriteAllText(Path.Combine(dir, "p.obj"), BuildObj(), new UTF8Encoding(false));

            var r = RunPipe(dir, new[] { "p.obj" }, false);

            if (r.ExitCode != 0)
                throw new Exception("obj_usemtl_fallback: ExitCode=" + r.ExitCode + " " + r.MainErrorId);

            Part za = null, zb = null;
            for (int i = 0; i < r.Scene.Parts.Count; i++)
            {
                string nm = r.Scene.Parts[i].Name;
                if (nm == "Zzz_a") za = r.Scene.Parts[i];
                else if (nm == "Zzz_b") zb = r.Scene.Parts[i];
            }
            if (za == null) throw new Exception("obj_usemtl_fallback: Zzz_a не найдена");
            if (zb == null) throw new Exception("obj_usemtl_fallback: Zzz_b не найдена");
            if (za.Material == null || !za.Material.Contains("LaBr3"))
                throw new Exception("obj_usemtl_fallback: Zzz_a.Material=" + za.Material + ", ожидаем *LaBr3*");
            if (zb.Material != "G4_Al")
                throw new Exception("obj_usemtl_fallback: Zzz_b.Material=" + zb.Material + ", ожидаем G4_Al");
            if (!HasDiag(r, "I_MAT_FROM_SOURCE"))
                throw new Exception("obj_usemtl_fallback: нет I_MAT_FROM_SOURCE");
        }

        // 4. Встроенные правила MaterialLibrary
        private static void BuiltinRulesNew()
        {
            MaterialLibrary lib = MaterialLibrary.Default();
            CheckRes(lib, "Bezel_PC_A", "G4_POLYCARBONATE");
            CheckRes(lib, "Reflector_c02", "G4_TEFLON");
            CheckRes(lib, "PCB_main", "FR4");
            CheckRes(lib, "Endplate_Al5052_A", "G4_Al");
            CheckRes(lib, "Pin_3", "G4_Cu");
        }

        private static void CheckRes(MaterialLibrary lib, string name, string expected)
        {
            string got = lib.Resolve(name);
            if (got != expected)
                throw new Exception("builtin_rules_new: Resolve(\"" + name + "\")=" + got + ", ожидаем " + expected);
        }

        // Форматирование double
        private static string F(double v)
        {
            return v.ToString(CultureInfo.InvariantCulture);
        }

        // Сборка OBJ: два замкнутых куба
        private static string BuildObj()
        {
            var sb = new StringBuilder();

            // --- Zzz_a: куб 10x10x10, центр (0,0,0), вершины 1-8 ---
            sb.AppendLine("o Zzz_a");
            sb.AppendLine("usemtl Crystal_LaBr3_mat.001");
            double h = 5.0;
            sb.AppendLine("v " + F(-h) + " " + F(-h) + " " + F(-h)); // 1
            sb.AppendLine("v " + F(h) + " " + F(-h) + " " + F(-h));  // 2
            sb.AppendLine("v " + F(h) + " " + F(h) + " " + F(-h));   // 3
            sb.AppendLine("v " + F(-h) + " " + F(h) + " " + F(-h));  // 4
            sb.AppendLine("v " + F(-h) + " " + F(-h) + " " + F(h));  // 5
            sb.AppendLine("v " + F(h) + " " + F(-h) + " " + F(h));   // 6
            sb.AppendLine("v " + F(h) + " " + F(h) + " " + F(h));    // 7
            sb.AppendLine("v " + F(-h) + " " + F(h) + " " + F(h));   // 8
            // 12 граней, внешняя закрутка
            sb.AppendLine("f 5 8 4");
            sb.AppendLine("f 5 4 1");
            sb.AppendLine("f 2 3 7");
            sb.AppendLine("f 2 7 6");
            sb.AppendLine("f 1 2 6");
            sb.AppendLine("f 1 6 5");
            sb.AppendLine("f 3 4 8");
            sb.AppendLine("f 3 8 7");
            sb.AppendLine("f 4 3 2");
            sb.AppendLine("f 4 2 1");
            sb.AppendLine("f 5 6 7");
            sb.AppendLine("f 5 7 8");
            sb.AppendLine();

            // --- Zzz_b: куб 30x30x30, центр (50,0,0), вершины 9-16 ---
            sb.AppendLine("o Zzz_b");
            sb.AppendLine("usemtl Housing_Al6063_mat.001");
            double h2 = 15.0;
            double cx = 50.0;
            sb.AppendLine("v " + F(cx - h2) + " " + F(-h2) + " " + F(-h2)); // 9
            sb.AppendLine("v " + F(cx + h2) + " " + F(-h2) + " " + F(-h2)); // 10
            sb.AppendLine("v " + F(cx + h2) + " " + F(h2) + " " + F(-h2));  // 11
            sb.AppendLine("v " + F(cx - h2) + " " + F(h2) + " " + F(-h2));  // 12
            sb.AppendLine("v " + F(cx - h2) + " " + F(-h2) + " " + F(h2));  // 13
            sb.AppendLine("v " + F(cx + h2) + " " + F(-h2) + " " + F(h2));  // 14
            sb.AppendLine("v " + F(cx + h2) + " " + F(h2) + " " + F(h2));   // 15
            sb.AppendLine("v " + F(cx - h2) + " " + F(h2) + " " + F(h2));   // 16
            sb.AppendLine("f 13 16 12");
            sb.AppendLine("f 13 12 9");
            sb.AppendLine("f 10 11 15");
            sb.AppendLine("f 10 15 14");
            sb.AppendLine("f 9 10 14");
            sb.AppendLine("f 9 14 13");
            sb.AppendLine("f 11 12 16");
            sb.AppendLine("f 11 16 15");
            sb.AppendLine("f 12 11 10");
            sb.AppendLine("f 12 10 9");
            sb.AppendLine("f 13 14 15");
            sb.AppendLine("f 13 15 16");

            return sb.ToString();
        }
    }
}
