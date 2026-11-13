using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Cad2Gdml.Import;
using Cad2Gdml.Materials;
using Cad2Gdml.Scene;

namespace Cad2Gdml.Tests
{
    public static class ChecksTess2
    {
        private const double Tol = 1e-9;
        private static readonly double[] Ref = { 0.625019513972004, 0.04196284539730447, 0.3330176406306916 };
        private static readonly string[] RefKeys = { "C", "H", "O" };

        private static void Fail(string test, string msg)
        {
            throw new Exception(test + ": " + msg);
        }

        private static IList<Part> Load(string td, string name, string text, out IList<string[]> warnings)
        {
            string path = Path.Combine(td, name);
            File.WriteAllText(path, text, new UTF8Encoding(false));
            IList<string> skipped;
            return GdmlImporter.Load(path, out skipped, out warnings);
        }

        // элементы C, H, O с атомными массами для материальных файлов-обрезков
        private const string Els = "<element name=\"C\" formula=\"C\" Z=\"6\"><atom value=\"12.011\"/></element>"
            + "<element name=\"H\" formula=\"H\" Z=\"1\"><atom value=\"1.008\"/></element>"
            + "<element name=\"O\" formula=\"O\" Z=\"8\"><atom value=\"15.999\"/></element>";

        private static void CheckPetg(string test, MaterialDef m)
        {
            for (int i = 0; i < 3; i++)
            {
                var kv = m.MassFractions.FirstOrDefault(k => k.Key == RefKeys[i]); double v = kv.Value;
                if (kv.Key == null)
                    Fail(test, "отсутствует элемент " + RefKeys[i]);
                if (Math.Abs(v - Ref[i]) > Tol)
                    Fail(test, RefKeys[i] + " = " + v.ToString("R") + ", ожидалось " + Ref[i].ToString("R"));
            }
        }

        private static void EnsureReplace(string test, string text, string old, string neu, out string result)
        {
            if (text.IndexOf(old, StringComparison.Ordinal) < 0)
                Fail(test, "подстрока не найдена: \"" + old + "\"");
            result = text.Replace(old, neu);
        }

        public static List<KeyValuePair<string, Action>> All(string tempDir)
        {
            string td = Path.Combine(tempDir, "tess2");
            Directory.CreateDirectory(td);
            var list = new List<KeyValuePair<string, Action>>();

            list.Add(new KeyValuePair<string, Action>("tess_body_lunit_ignored", () =>
            {
                const string test = "tess_body_lunit_ignored";
                string txt = ChecksTess.CubeGdml("quadAbs", "mm", 1, "mm", 1, "cm", null);
                IList<string[]> w;
                var parts = Load(td, "t1_cm.gdml", txt, out w);
                if (parts.Count != 1) Fail(test, "ожидается 1 деталь, получено " + parts.Count);
                double vol = ChecksTess.Volume(parts[0].Mesh);
                if (Math.Abs(vol - 1000.0) > 1e-6) Fail(test, "объём = " + vol + ", ожидалось 1000");
                if (w == null || !w.Any(x => x[0] == "W_GDML_LUNIT_IGNORED" && x[1] == "Cube_solid"))
                    Fail(test, "нет W_GDML_LUNIT_IGNORED для Cube_solid");
                // lunit="mm" — предупреждение не ожидается
                string txt2 = ChecksTess.CubeGdml("quadAbs", "mm", 1, "mm", 1, "mm", null);
                IList<string[]> w2;
                Load(td, "t1_mm.gdml", txt2, out w2);
                if (w2 != null && w2.Any(x => x[0] == "W_GDML_LUNIT_IGNORED"))
                    Fail(test, "неожиданное W_GDML_LUNIT_IGNORED при lunit=mm");
            }));

            list.Add(new KeyValuePair<string, Action>("tess_facet_lunit", () =>
            {
                const string test = "tess_facet_lunit";
                string txt = ChecksTess.CubeGdml("quadAbs", "mm", 1, "mm", 1, null, null);
                txt = txt.Replace("<quadrangular ", "<quadrangular lunit=\"cm\" ");
                IList<string[]> w;
                var parts = Load(td, "t2.gdml", txt, out w);
                if (parts.Count != 1) Fail(test, "ожидается 1 деталь, получено " + parts.Count);
                double vol = ChecksTess.Volume(parts[0].Mesh);
                if (Math.Abs(vol - 1e6) > 1e-3) Fail(test, "объём = " + vol + ", ожидалось 1e6");
                if (w != null && w.Any(x => x[0] == "W_GDML_LUNIT_IGNORED"))
                    Fail(test, "неожиданное W_GDML_LUNIT_IGNORED");
            }));

            list.Add(new KeyValuePair<string, Action>("tess_bad_position_skips_body", () =>
            {
                const string test = "tess_bad_position_skips_body";
                string b = ChecksTess.CubeGdml("quadAbs", "mm", 1, "mm", 1, null, null);
                string badSolid = "<tessellated name=\"Bad_solid\"><triangular vertex1=\"bad\" vertex2=\"c000\" vertex3=\"c100\" type=\"ABSOLUTE\"/></tessellated>";
                string badVol = "<volume name=\"Bad\"><materialref ref=\"PETG\"/><solidref ref=\"Bad_solid\"/></volume>";
                string badPhys = "<physvol name=\"Bad_pv\"><volumeref ref=\"Bad\"/></physvol>";
                // Вариант 1: x="2*cm"
                string t1 = b.Replace("</define>", "<position name=\"bad\" x=\"2*cm\" y=\"0\" z=\"0\"/></define>")
                    .Replace("</solids>", badSolid + "</solids>")
                    .Replace("<volume name=\"World\">", badVol + "<volume name=\"World\">")
                    .Replace("<physvol name=\"Cube_pv\"><volumeref ref=\"Cube\"/></physvol>",
                             "<physvol name=\"Cube_pv\"><volumeref ref=\"Cube\"/></physvol>" + badPhys);
                IList<string[]> w1;
                var p1 = Load(td, "t3a.gdml", t1, out w1);
                if (p1.Count != 1) Fail(test, "ожидается 1 деталь, получено " + p1.Count);
                if (Math.Abs(ChecksTess.Volume(p1[0].Mesh) - 1000.0) > 1e-6) Fail(test, "объём куба != 1000");
                if (w1 == null || !w1.Any(x => x[0] == "W_GDML_SKIPPED" && x[1] == "Bad_solid" && x[2].Contains("bad") && x[2].Contains("2*cm")))
                    Fail(test, "нет W_GDML_SKIPPED Bad_solid с 2*cm");
                // Вариант 2: unit="furlong"
                string t2 = b.Replace("</define>", "<position name=\"bad\" unit=\"furlong\" x=\"1\" y=\"0\" z=\"0\"/></define>")
                    .Replace("</solids>", badSolid + "</solids>")
                    .Replace("<volume name=\"World\">", badVol + "<volume name=\"World\">")
                    .Replace("<physvol name=\"Cube_pv\"><volumeref ref=\"Cube\"/></physvol>",
                             "<physvol name=\"Cube_pv\"><volumeref ref=\"Cube\"/></physvol>" + badPhys);
                IList<string[]> w2;
                var p2 = Load(td, "t3b.gdml", t2, out w2);
                if (p2.Count != 1) Fail(test, "в2: ожидается 1 деталь, получено " + p2.Count);
                if (w2 == null || !w2.Any(x => x[0] == "W_GDML_SKIPPED" && x[1] == "Bad_solid" && x[2].Contains("furlong")))
                    Fail(test, "в2: нет W_GDML_SKIPPED с furlong");
            }));

            list.Add(new KeyValuePair<string, Action>("tess_duplicate_position", () =>
            {
                const string test = "tess_duplicate_position";
                string txt = ChecksTess.CubeGdml("quadAbs", "mm", 1, "mm", 1, null, null);
                txt = txt.Replace("</define>", "<position name=\"c000\" x=\"5\" y=\"5\" z=\"5\"/></define>");
                IList<string[]> w;
                var parts = Load(td, "t4.gdml", txt, out w);
                if (parts.Count != 0) Fail(test, "ожидается 0 деталей, получено " + parts.Count);
                if (w == null || !w.Any(x => x[0] == "W_GDML_SKIPPED" && x[1] == "Cube_solid" && x[2].Contains("c000") && x[2].Contains("более одного раза")))
                    Fail(test, "нет W_GDML_SKIPPED Cube_solid (дубль c000)");
            }));

            list.Add(new KeyValuePair<string, Action>("tess_composite_duplicate_ref", () =>
            {
                const string test = "tess_composite_duplicate_ref";
                string mat = "<gdml><materials>" + Els
                    + "<material name=\"DUP\" state=\"solid\"><D value=\"1.27\" unit=\"g/cm3\"/>"
                    + "<composite n=\"5\" ref=\"C\"/><composite n=\"5\" ref=\"C\"/><composite n=\"8\" ref=\"H\"/><composite n=\"4\" ref=\"O\"/></material>"
                    + "<material name=\"NEG\" state=\"solid\"><D value=\"1.0\" unit=\"g/cm3\"/><composite n=\"0\" ref=\"C\"/><composite n=\"8\" ref=\"H\"/></material>"
                    + "<material name=\"NEG2\" state=\"solid\"><D value=\"1.0\" unit=\"g/cm3\"/><composite n=\"-3\" ref=\"C\"/></material>"
                    + "</materials></gdml>";
                string path = Path.Combine(td, "t5.gdml");
                File.WriteAllText(path, mat, new UTF8Encoding(false));
                List<string> notes;
                var mats = GdmlMaterials.Load(path, out notes);
                var dup = mats.FirstOrDefault(m => m.Name == "DUP");
                if (dup == null) Fail(test, "DUP отсутствует в результате");
                CheckPetg(test, dup);
                if (mats.Any(m => m.Name == "NEG")) Fail(test, "NEG не должен быть в результате");
                if (mats.Any(m => m.Name == "NEG2")) Fail(test, "NEG2 не должен быть в результате");
                if (notes == null || !notes.Any(n => n.Contains("NEG") && n.Contains("composite")))
                    Fail(test, "нет notes для NEG (composite)");
                if (notes == null || !notes.Any(n => n.Contains("NEG2") && n.Contains("composite")))
                    Fail(test, "нет notes для NEG2 (composite)");
            }));

            list.Add(new KeyValuePair<string, Action>("tess_composite_isotopes", () =>
            {
                const string test = "tess_composite_isotopes";
                string mat = "<gdml><materials>" + Els
                    + "<element name=\"U235\" formula=\"U\" Z=\"92\"><atom value=\"235.04\"/></element>"
                    + "<element name=\"U238\" formula=\"U\" Z=\"92\"><atom value=\"238.05\"/></element>"
                    + "<element name=\"Cx\" formula=\"C\" Z=\"6\"><atom value=\"12.011\"/></element>"
                    + "<material name=\"UO\" state=\"solid\"><D value=\"1.0\" unit=\"g/cm3\"/><composite n=\"1\" ref=\"U235\"/><composite n=\"1\" ref=\"U238\"/></material>"
                    + "<material name=\"CX\" state=\"solid\"><D value=\"1.27\" unit=\"g/cm3\"/><composite n=\"5\" ref=\"C\"/><composite n=\"5\" ref=\"Cx\"/><composite n=\"8\" ref=\"H\"/><composite n=\"4\" ref=\"O\"/></material>"
                    + "</materials></gdml>";
                string path = Path.Combine(td, "t6.gdml");
                File.WriteAllText(path, mat, new UTF8Encoding(false));
                List<string> notes;
                var mats = GdmlMaterials.Load(path, out notes);
                if (mats.Any(m => m.Name == "UO")) Fail(test, "UO не должен быть в результате");
                if (notes == null || !notes.Any(n => n.Contains("UO") && n.Contains("изотоп")))
                    Fail(test, "нет notes для UO (изотоп)");
                var cx = mats.FirstOrDefault(m => m.Name == "CX");
                if (cx == null) Fail(test, "CX отсутствует в результате");
                CheckPetg(test, cx);
            }));

            list.Add(new KeyValuePair<string, Action>("tess_composite_warnings", () =>
            {
                const string test = "tess_composite_warnings";
                string mat = "<gdml><materials>" + Els
                    + "<material name=\"FRAC\" state=\"solid\"><D value=\"1.0\" unit=\"g/cm3\"/><composite n=\"10.5\" ref=\"C\"/><composite n=\"8\" ref=\"H\"/><composite n=\"4\" ref=\"O\"/></material>"
                    + "<material name=\"BOTH\" state=\"solid\"><D value=\"1.27\" unit=\"g/cm3\"/><composite n=\"10\" ref=\"C\"/><composite n=\"8\" ref=\"H\"/><composite n=\"4\" ref=\"O\"/><fraction n=\"1\" ref=\"C\"/></material>"
                    + "</materials></gdml>";
                string path = Path.Combine(td, "t7.gdml");
                File.WriteAllText(path, mat, new UTF8Encoding(false));
                List<string> notes;
                var mats = GdmlMaterials.Load(path, out notes);
                var frac = mats.FirstOrDefault(m => m.Name == "FRAC");
                if (frac == null) Fail(test, "FRAC отсутствует в результате");
                if (notes == null || !notes.Any(n => n.Contains("FRAC") && n.Contains("нецелое")))
                    Fail(test, "нет notes для FRAC (нецелое)");
                var both = mats.FirstOrDefault(m => m.Name == "BOTH");
                if (both == null) Fail(test, "BOTH отсутствует в результате");
                CheckPetg(test, both);
                if (notes == null || !notes.Any(n => n.Contains("BOTH") && n.Contains("fraction игнорируются")))
                    Fail(test, "нет notes для BOTH (fraction игнорируются)");
            }));

            return list;
        }
    }
}
