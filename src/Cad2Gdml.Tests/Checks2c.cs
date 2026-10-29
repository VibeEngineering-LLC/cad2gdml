using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;

namespace Cad2Gdml.Tests
{
    public static class Checks2c
    {
        public static List<KeyValuePair<string, Action>> All(string tempDir)
        {
            var checks = new List<KeyValuePair<string, Action>>();

            // 9. open_default_error
            checks.Add(new KeyValuePair<string, Action>("open_default_error", () =>
            {
                string dir = Path.Combine(tempDir, "c09");
                string sceneDir = Path.Combine(dir, "scene");
                CheckUtil.TwoPartScene(sceneDir);

                // lead_shield: коробка с одним удалённым треугольником → открытая сетка
                var boxTris = Fixtures.Box(5, 5, 5, 0, 0, 100);
                var openTris = Fixtures.RemoveOne(boxTris, 0);
                Fixtures.WriteBinaryStl(Path.Combine(sceneDir, "lead_shield.stl"), openTris);

                string[] inputs =
                {
                    Path.Combine(sceneDir, "crystal.stl"),
                    Path.Combine(sceneDir, "water_sample.stl"),
                    Path.Combine(sceneDir, "lead_shield.stl")
                };

                // dropOpen=false → ошибка
                var r1 = CheckUtil.Run(Path.Combine(dir, "o1"), inputs, 1.0, "auto", true, false);
                CheckUtil.Assert(r1.ExitCode == 3, "Ожидался exit 3, получен " + r1.ExitCode);
                CheckUtil.Assert(r1.MainErrorId == "E_MESH_OPEN", "Ожидался E_MESH_OPEN, получен " + r1.MainErrorId);

                // dropOpen=true → успех + W_MESH_OPEN_DROPPED
                var r2 = CheckUtil.Run(Path.Combine(dir, "o2"), inputs, 1.0, "auto", true, true);
                CheckUtil.Assert(r2.ExitCode == 0, "Ожидался exit 0, получен " + r2.ExitCode);
                bool found = false;
                for (int i = 0; i < r2.Diagnostics.Count; i++)
                {
                    if (r2.Diagnostics[i].Code == "W_MESH_OPEN_DROPPED") { found = true; break; }
                }
                CheckUtil.Assert(found, "Не найдена диагностика W_MESH_OPEN_DROPPED.");
            }));

            // 10. ru_culture
            checks.Add(new KeyValuePair<string, Action>("ru_culture", () =>
            {
                string dir = Path.Combine(tempDir, "c10");
                string sceneDir = Path.Combine(dir, "scene");
                CheckUtil.TwoPartScene(sceneDir);
                string[] inputs =
                {
                    Path.Combine(sceneDir, "crystal.stl"),
                    Path.Combine(sceneDir, "water_sample.stl")
                };

                var saved = Thread.CurrentThread.CurrentCulture;
                try
                {
                    // Базовый запуск: InvariantCulture
                    Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
                    string dirInv = Path.Combine(dir, "inv");
                    var rInv = CheckUtil.Run(dirInv, inputs, 1.0, "auto", true, false);
                    CheckUtil.Assert(rInv.ExitCode == 0, "Invariant exit: " + rInv.ExitCode);
                    string shaInv = Sha256(File.ReadAllBytes(Path.Combine(dirInv, "o.gdml")));

                    // Два запуска в ru-RU
                    Thread.CurrentThread.CurrentCulture = new CultureInfo("ru-RU");
                    string d1 = Path.Combine(dir, "ru1");
                    var r1 = CheckUtil.Run(d1, inputs, 1.0, "auto", true, false);
                    CheckUtil.Assert(r1.ExitCode == 0, "ru1 exit: " + r1.ExitCode);
                    string sha1 = Sha256(File.ReadAllBytes(Path.Combine(d1, "o.gdml")));

                    string d2 = Path.Combine(dir, "ru2");
                    var r2 = CheckUtil.Run(d2, inputs, 1.0, "auto", true, false);
                    CheckUtil.Assert(r2.ExitCode == 0, "ru2 exit: " + r2.ExitCode);
                    string sha2 = Sha256(File.ReadAllBytes(Path.Combine(d2, "o.gdml")));

                    CheckUtil.Assert(sha1 == shaInv, "SHA256 ru1 ≠ invariant: " + sha1 + " vs " + shaInv);
                    CheckUtil.Assert(sha2 == shaInv, "SHA256 ru2 ≠ invariant: " + sha2 + " vs " + shaInv);

                    // Запятая не должна встречаться в числовых атрибутах
                    string text = File.ReadAllText(Path.Combine(d1, "o.gdml"), new UTF8Encoding(false));
                    var re = new Regex("(?:x|y|z|rmax|rmin)=\"[0-9.-]+,");
                    CheckUtil.Assert(!re.IsMatch(text), "Найдена запятая в числовом атрибуте.");
                }
                finally
                {
                    Thread.CurrentThread.CurrentCulture = saved;
                }
            }));

            // 11. aux_roles
            checks.Add(new KeyValuePair<string, Action>("aux_roles", () =>
            {
                string dir = Path.Combine(tempDir, "c11");
                string sceneDir = CheckUtil.TwoPartScene(Path.Combine(dir, "scene"));
                string[] inputs =
                {
                    Path.Combine(sceneDir, "crystal.stl"),
                    Path.Combine(sceneDir, "water_sample.stl")
                };
                string outDir = Path.Combine(dir, "out");
                var r = CheckUtil.Run(outDir, inputs, 1.0, "auto", true, false);
                CheckUtil.Assert(r.ExitCode == 0, "Exit: " + r.ExitCode);

                var doc = CheckUtil.Gdml(r);
                FindAux(doc, "BecqMoni:Role", "crystal");
                FindAux(doc, "BecqMoni:Role", "source");
            }));

            // 12. png_reuse
            checks.Add(new KeyValuePair<string, Action>("png_reuse", () =>
            {
                string dir = Path.Combine(tempDir, "c12");
                string sceneDir = Path.Combine(dir, "scene");
                CheckUtil.TwoPartScene(sceneDir);
                string[] inputs =
                {
                    Path.Combine(sceneDir, "crystal.stl"),
                    Path.Combine(sceneDir, "water_sample.stl")
                };
                string outDir = Path.Combine(dir, "out");

                var r1 = CheckUtil.Run(outDir, inputs, 1.0, "auto", true, false);
                CheckUtil.Assert(r1.ExitCode == 0, "run1 exit: " + r1.ExitCode);
                CheckUtil.Assert(File.Exists(r1.PngPath), "PNG отсутствует после run1.");

                var r2 = CheckUtil.Run(outDir, inputs, 1.0, "auto", true, false);
                CheckUtil.Assert(r2.ExitCode == 0, "run2 exit: " + r2.ExitCode);
                CheckUtil.Assert(File.Exists(r2.PngPath), "PNG отсутствует после run2.");
            }));

            return checks;
        }

        // SHA-256 → hex-строка
        private static string Sha256(byte[] data)
        {
            using (var sha = SHA256.Create())
            {
                byte[] h = sha.ComputeHash(data);
                var sb = new StringBuilder(h.Length * 2);
                for (int i = 0; i < h.Length; i++) sb.Append(h[i].ToString("x2"));
                return sb.ToString();
            }
        }

        // Поиск <auxiliary auxtype="…" auxvalue="…"/> в дереве
        private static void FindAux(XDocument doc, string auxType, string auxValue)
        {
            foreach (XElement el in doc.Descendants())
            {
                if (el.Name.LocalName != "auxiliary") continue;
                XAttribute t = el.Attribute("auxtype");
                XAttribute v = el.Attribute("auxvalue");
                if (t != null && t.Value == auxType && v != null && v.Value == auxValue)
                    return;
            }
            throw new InvalidOperationException("Не найден auxiliary " + auxType + "=" + auxValue);
        }
    }
}
