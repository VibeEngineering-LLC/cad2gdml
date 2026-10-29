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
    // Проверки 2k: полость не разделяется на отдельные тела
    public static class Checks2k
    {
        public static List<KeyValuePair<string, Action>> All(string tempDir)
        {
            var list = new List<KeyValuePair<string, Action>>();
            list.Add(new KeyValuePair<string, Action>("hollow_body_not_split", () => HollowBodyNotSplit(tempDir)));
            return list;
        }

        // Полость (инвертированная внутренняя коробка) не создаёт _c1/_c2
        private static void HollowBodyNotSplit(string tempDir)
        {
            string dir = Path.Combine(tempDir, "hollow_body");
            Directory.CreateDirectory(dir);

            // Внешняя коробка 20×20×30, внутренняя 10×10×20 (инвертированная — полость)
            IList<double[]> outer = Fixtures.Box(20, 20, 30, 0, 0, 0);
            IList<double[]> inner = Fixtures.FlipAll(Fixtures.Box(10, 10, 20, 0, 0, 0));

            var combined = new List<double[]>();
            combined.AddRange(outer);
            combined.AddRange(inner);
            Fixtures.WriteBinaryStl(Path.Combine(dir, "Reflector_PTFE.stl"), combined);

            // Кристалл и корпус
            Fixtures.WriteBinaryStl(Path.Combine(dir, "Crystal_NaI.stl"), Fixtures.Box(10, 10, 20, 0, 0, 0));
            Fixtures.WriteBinaryStl(Path.Combine(dir, "Housing.stl"), Fixtures.Box(40, 40, 60, 0, 0, 0));

            // Запуск конвейера
            var opt = new PipelineOptions();
            opt.Inputs = new List<string>();
            foreach (string inp in new[] { "Reflector_PTFE.stl", "Crystal_NaI.stl", "Housing.stl" })
                opt.Inputs.Add(Path.Combine(dir, inp));
            opt.UnitToMm = 1.0;
            opt.FrontSign = "+z";
            opt.OutDir = dir;
            opt.BaseName = "o";
            opt.AllowDefaultMaterial = true;
            opt.NoRepair = true;

            var r = Cad2GdmlPipeline.Run(opt);

            if (r.ExitCode != 0)
                throw new Exception("hollow_body_not_split: ExitCode=" + r.ExitCode + " " + r.MainErrorId);

            // Ищем детали Reflector_PTFE
            int count = 0;
            Part reflector = null;
            for (int i = 0; i < r.Scene.Parts.Count; i++)
            {
                if (r.Scene.Parts[i].Name.StartsWith("Reflector_PTFE", StringComparison.Ordinal))
                {
                    count++;
                    reflector = r.Scene.Parts[i];
                }
            }

            if (count != 1)
                throw new Exception("hollow_body_not_split: деталей Reflector_PTFE = "
                    + count.ToString(CultureInfo.InvariantCulture) + ", ожидаем 1");

            // Объём: 20·20·30 − 10·10·20 = 12000 − 2000 = 10000
            double expectedVol = 10000.0;
            double actualVol = reflector.VolumeMm3;
            if (Math.Abs(actualVol - expectedVol) > 1.0)
                throw new Exception("hollow_body_not_split: VolumeMm3="
                    + actualVol.ToString(CultureInfo.InvariantCulture)
                    + ", ожидаем " + expectedVol.ToString(CultureInfo.InvariantCulture) + " ± 1");

            // Диагностика: I_CAVITY_MERGED есть, W_MESH_FLIPPED для Reflector_PTFE — нет
            bool hasCavity = false;
            bool hasFlipped = false;
            for (int i = 0; i < r.Scene.Diagnostics.Count; i++)
            {
                Diagnostic d = r.Scene.Diagnostics[i];
                if (d.Code == "I_CAVITY_MERGED")
                    hasCavity = true;
                if (d.Code == "W_MESH_FLIPPED"
                    && d.PartName != null
                    && d.PartName.StartsWith("Reflector_PTFE", StringComparison.Ordinal))
                    hasFlipped = true;
            }

            if (!hasCavity)
                throw new Exception("hollow_body_not_split: нет I_CAVITY_MERGED в диагностике");

            if (hasFlipped)
                throw new Exception("hollow_body_not_split: найдена W_MESH_FLIPPED для Reflector_PTFE");
        }
    }
}
