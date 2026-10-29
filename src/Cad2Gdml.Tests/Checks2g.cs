using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Xml.Linq;
using Cad2Gdml.Gdml;
using Cad2Gdml.Pipeline;

namespace Cad2Gdml.Tests
{
    // Проверки 2g: длинные имена GDML, прямой вызов GdmlName.Make
    public static class Checks2g
    {
        public static List<KeyValuePair<string, Action>> All(string tempDir)
        {
            var list = new List<KeyValuePair<string, Action>>();
            list.Add(new KeyValuePair<string, Action>("long_names_unique", () => LongNamesUnique(tempDir)));
            list.Add(new KeyValuePair<string, Action>("gdmlname_direct", () => GdmlNameDirect(tempDir)));
            return list;
        }

        // Четыре STL с идентичными 64 первыми символами имени
        private static void LongNamesUnique(string tempDir)
        {
            string dir = Path.Combine(tempDir, "long_names");
            Directory.CreateDirectory(dir);

            // 70-символьный базовый строковый префикс
            string base70 = "Passivated 18-8 Stainless Steel Phillips Flat Head Screw ABCDEFGHIJKLM";
            int[] cx = { 0, 20, 40, 60 };

            for (int i = 0; i < 4; i++)
            {
                string fname = base70 + "_" + (i + 1).ToString() + ".stl";
                Fixtures.WriteBinaryStl(Path.Combine(dir, fname), Fixtures.Box(5, 5, 5, cx[i], 0, 0));
            }

            string outDir = Path.Combine(tempDir, "long_names_out");
            Directory.CreateDirectory(outDir);

            var opt = new PipelineOptions();
            opt.Inputs = new List<string> { dir };
            opt.UnitToMm = 1.0;
            opt.AllowDefaultMaterial = true;
            opt.OutDir = outDir;
            opt.BaseName = "o";
            opt.NoRepair = true;

            // Запуск в потоке с таймаутом
            PipelineResult r = null;
            var t = new Thread(() => { r = Cad2GdmlPipeline.Run(opt); });
            t.IsBackground = true;
            t.Start();
            if (!t.Join(30000))
                throw new InvalidOperationException("зависание GdmlName.Make");

            CheckUtil.Assert(r != null, "long_names_unique: результат null");
            CheckUtil.Assert(r.ExitCode == 0, "long_names_unique: ExitCode=" + r.ExitCode + " " + r.MainErrorId);

            // Читаем GDML
            XDocument doc = XDocument.Load(r.GdmlPath);

            // Имена volume
            var volNames = new List<string>();
            foreach (XElement e in doc.Descendants())
            {
                if (e.Name.LocalName == "volume")
                {
                    XAttribute a = e.Attribute("name");
                    if (a != null) volNames.Add(a.Value);
                }
            }

            // Имена твёрдых тел под <solids>
            var solidNames = new List<string>();
            foreach (XElement e in doc.Descendants())
            {
                string ln = e.Name.LocalName;
                if (ln == "box" || ln == "tube" || ln == "polycone" || ln == "tessellated")
                {
                    XAttribute a = e.Attribute("name");
                    if (a != null) solidNames.Add(a.Value);
                }
            }

            // Имена physvol
            var physNames = new List<string>();
            foreach (XElement e in doc.Descendants())
            {
                if (e.Name.LocalName == "physvol")
                {
                    XAttribute a = e.Attribute("name");
                    if (a != null) physNames.Add(a.Value);
                }
            }

            // В каждой группе: уникальность + длина <= 64
            CheckDistinctAndLength(volNames, "volume");
            CheckDistinctAndLength(solidNames, "solid");
            CheckDistinctAndLength(physNames, "physvol");
        }

        // Парная различимость и ограничение длины
        private static void CheckDistinctAndLength(List<string> names, string group)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < names.Count; i++)
            {
                string n = names[i];
                if (n.Length > 70) // база до 64 + служебные префикс/суффикс (PV_, _s)
                    throw new InvalidOperationException(group + ": имя длиннее 70: " + n + " (" + n.Length + ")");
                if (!set.Add(n))
                    throw new InvalidOperationException(group + ": дублирующее имя: " + n);
            }
        }

        // 200 вызовов GdmlName.Make с одинаковым raw
        private static void GdmlNameDirect(string tempDir)
        {
            string raw = new string('a', 120);
            List<string> results = null;

            var t = new Thread(() =>
            {
                var used = new HashSet<string>();
                var res = new List<string>(200);
                for (int i = 0; i < 200; i++)
                    res.Add(GdmlName.Make(raw, used));
                results = res;
            });
            t.IsBackground = true;
            t.Start();
            if (!t.Join(10000))
                throw new InvalidOperationException("зависание GdmlName.Make");

            CheckUtil.Assert(results != null, "gdmlname_direct: результаты null");
            CheckUtil.Assert(results.Count == 200, "gdmlname_direct: count=" + results.Count);

            var set = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < results.Count; i++)
            {
                string n = results[i];
                if (n.Length > 64)
                    throw new InvalidOperationException("gdmlname_direct: длина=" + n.Length + ", ожидаем <=64");
                if (!set.Add(n))
                    throw new InvalidOperationException("gdmlname_direct: дубликат: " + n);
            }
            CheckUtil.Assert(set.Count == 200, "gdmlname_direct: уникальных=" + set.Count);
        }
    }
}
