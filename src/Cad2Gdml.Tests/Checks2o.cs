using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Cad2Gdml.Geometry;
using Cad2Gdml.Materials;
using Cad2Gdml.Meshes;
using Cad2Gdml.Pipeline;
using Cad2Gdml.Scene;

namespace Cad2Gdml.Tests
{
    // Проверки 2o: кастомные вещества (CustomMaterialParser + GDML)
    public static class Checks2o
    {
        public static List<KeyValuePair<string, Action>> All(string tempDir)
        {
            var list = new List<KeyValuePair<string, Action>>();
            list.Add(new KeyValuePair<string, Action>("custom_mat_parse_ok", () => ParseOk(tempDir)));
            list.Add(new KeyValuePair<string, Action>("custom_mat_rejects_sum", () => RejectsSum(tempDir)));
            list.Add(new KeyValuePair<string, Action>("custom_mat_rejects_element", () => RejectsElement(tempDir)));
            list.Add(new KeyValuePair<string, Action>("custom_mat_rejects_density", () => RejectsDensity(tempDir)));
            list.Add(new KeyValuePair<string, Action>("custom_mat_rejects_name", () => RejectsName(tempDir)));
            list.Add(new KeyValuePair<string, Action>("custom_mat_in_gdml", () => InGdml(tempDir)));
            return list;
        }

        // 1. Успешный парсинг + канонические символы
        private static void ParseOk(string tempDir)
        {
            MaterialDef def;
            string error;

            bool ok = CustomMaterialParser.TryParse("MyScint", "4,2", "La=0.36688; Br:0,63312", out def, out error);
            if (!ok)
                throw new Exception("custom_mat_parse_ok: TryParse вернул false, error=" + error);
            if (error != null)
                throw new Exception("custom_mat_parse_ok: error не null: " + error);
            if (def.Name != "MyScint")
                throw new Exception("custom_mat_parse_ok: Name=" + def.Name);
            if (Math.Abs(def.DensityGcm3 - 4.2) > 1e-12)
                throw new Exception("custom_mat_parse_ok: Density=" + def.DensityGcm3.ToString(CultureInfo.InvariantCulture));
            if (def.MassFractions.Count != 2)
                throw new Exception("custom_mat_parse_ok: MassFractions.Count=" + def.MassFractions.Count);
            if (def.MassFractions[0].Key != "La")
                throw new Exception("custom_mat_parse_ok: frac[0].Key=" + def.MassFractions[0].Key);
            if (Math.Abs(def.MassFractions[0].Value - 0.36688) > 1e-12)
                throw new Exception("custom_mat_parse_ok: frac[0].Val=" + def.MassFractions[0].Value.ToString(CultureInfo.InvariantCulture));
            if (def.MassFractions[1].Key != "Br")
                throw new Exception("custom_mat_parse_ok: frac[1].Key=" + def.MassFractions[1].Key);
            if (Math.Abs(def.MassFractions[1].Value - 0.63312) > 1e-12)
                throw new Exception("custom_mat_parse_ok: frac[1].Val=" + def.MassFractions[1].Value.ToString(CultureInfo.InvariantCulture));
            if (def.IsNist)
                throw new Exception("custom_mat_parse_ok: IsNist=true, ожидаем false");

            // строчные символы → канонические
            MaterialDef def2;
            string err2;
            bool ok2 = CustomMaterialParser.TryParse("MyScint", "4.2", "la=0.36688;BR=0.63312", out def2, out err2);
            if (!ok2)
                throw new Exception("custom_mat_parse_ok(lower): TryParse=false, error=" + err2);
            if (def2.MassFractions[0].Key != "La")
                throw new Exception("custom_mat_parse_ok(lower): frac[0].Key=" + def2.MassFractions[0].Key);
            if (def2.MassFractions[1].Key != "Br")
                throw new Exception("custom_mat_parse_ok(lower): frac[1].Key=" + def2.MassFractions[1].Key);
        }

        // 2. Отклонение по сумме долей
        private static void RejectsSum(string tempDir)
        {
            MaterialDef def;
            string error;

            // сумма 0.9 → отказ
            bool ok = CustomMaterialParser.TryParse("M1", "2", "La=0.5; Br=0.4", out def, out error);
            if (ok) throw new Exception("custom_mat_rejects_sum: сумма 0.9 принята");
            if (string.IsNullOrEmpty(error)) throw new Exception("custom_mat_rejects_sum: error пуст");

            // сумма 1.0 → ок
            ok = CustomMaterialParser.TryParse("M1", "2", "La=0.36688; Br=0.63312", out def, out error);
            if (!ok) throw new Exception("custom_mat_rejects_sum: сумма 1.0 отклонена: " + error);

            // сумма 1.00009 → ок (в пределах 1e-4)
            ok = CustomMaterialParser.TryParse("M1", "2", "La=0.36688; Br=0.63321", out def, out error);
            if (!ok) throw new Exception("custom_mat_rejects_sum: сумма 1.00009 отклонена: " + error);

            // сумма 1.0002 → отказ
            ok = CustomMaterialParser.TryParse("M1", "2", "La=0.36688; Br=0.63332", out def, out error);
            if (ok) throw new Exception("custom_mat_rejects_sum: сумма 1.0002 принята");
            if (string.IsNullOrEmpty(error)) throw new Exception("custom_mat_rejects_sum: error пуст");
        }

        // 3. Отклонение по элементам
        private static void RejectsElement(string tempDir)
        {
            MaterialDef def;
            string error;

            // неизвестный элемент
            bool ok = CustomMaterialParser.TryParse("M1", "2", "Xx=1", out def, out error);
            if (ok) throw new Exception("custom_mat_rejects_element: Xx=1 принят");
            if (string.IsNullOrEmpty(error)) throw new Exception("custom_mat_rejects_element: error пуст (Xx)");

            // дубликат
            ok = CustomMaterialParser.TryParse("M1", "2", "La=0.5;La=0.5", out def, out error);
            if (ok) throw new Exception("custom_mat_rejects_element: дубликат La принят");

            // доля 0
            ok = CustomMaterialParser.TryParse("M1", "2", "La=0", out def, out error);
            if (ok) throw new Exception("custom_mat_rejects_element: La=0 принят");

            // пустой состав
            ok = CustomMaterialParser.TryParse("M1", "2", "", out def, out error);
            if (ok) throw new Exception("custom_mat_rejects_element: пустой состав принят");

            // без разделителя
            ok = CustomMaterialParser.TryParse("M1", "2", "La", out def, out error);
            if (ok) throw new Exception("custom_mat_rejects_element: 'La' без = принят");

            // валидный Al
            ok = CustomMaterialParser.TryParse("M1", "2", "Al=1", out def, out error);
            if (!ok) throw new Exception("custom_mat_rejects_element: Al=1 отклонён: " + error);
        }

        // 4. Отклонение по плотности
        private static void RejectsDensity(string tempDir)
        {
            MaterialDef def;
            string error;

            string[] badDens = { "0", "-1", "abc", "31", "" };
            for (int i = 0; i < badDens.Length; i++)
            {
                bool bad = CustomMaterialParser.TryParse("M1", badDens[i], "Al=1", out def, out error);
                if (bad) throw new Exception("custom_mat_rejects_density: dens='" + badDens[i] + "' принят");
                if (string.IsNullOrEmpty(error)) throw new Exception("custom_mat_rejects_density: error пуст для '" + badDens[i] + "'");
            }

            // 30 — граница, ок
            bool okBoundary = CustomMaterialParser.TryParse("M1", "30", "Al=1", out def, out error);
            if (!okBoundary) throw new Exception("custom_mat_rejects_density: dens=30 отклонён: " + error);
        }

        // 5. Отклонение по имени
        private static void RejectsName(string tempDir)
        {
            MaterialDef def;
            string error;

            string[] badNames = { "", "1abc", "G4_Foo", "a b", "кир" };
            for (int i = 0; i < badNames.Length; i++)
            {
                bool bad = CustomMaterialParser.TryParse(badNames[i], "2", "Al=1", out def, out error);
                if (bad) throw new Exception("custom_mat_rejects_name: имя='" + badNames[i] + "' принято");
                if (string.IsNullOrEmpty(error)) throw new Exception("custom_mat_rejects_name: error пуст для '" + badNames[i] + "'");
            }

            // допустимое имя
            bool okGood = CustomMaterialParser.TryParse("a-b_1", "2", "Al=1", out def, out error);
            if (!okGood) throw new Exception("custom_mat_rejects_name: 'a-b_1' отклонён: " + error);
        }

        // 6. Кастомное вещество в GDML
        private static void InGdml(string tempDir)
        {
            string dir = Path.Combine(tempDir, "custom_mat_gdml");
            Directory.CreateDirectory(dir);

            Fixtures.WriteBinaryStl(Path.Combine(dir, "Crystal_NaI.stl"), Fixtures.Box(10, 10, 30, 0, 0, 0));
            Fixtures.WriteBinaryStl(Path.Combine(dir, "Housing.stl"), Fixtures.Box(20, 20, 50, 0, 0, 0));

            // парсим кастомное вещество
            MaterialDef customDef;
            string parseErr;
            if (!CustomMaterialParser.TryParse("MyScint", "4.2", "La=0.36688;Br=0.63312", out customDef, out parseErr))
                throw new Exception("custom_mat_in_gdml: TryParse не удался: " + parseErr);

            // первый прогон — с CustomMaterials
            {
                var opt = BuildOptions(dir, new[] { "Crystal_NaI.stl", "Housing.stl" });
                opt.CustomMaterials = new List<MaterialDef> { customDef };
                opt.MaterialOverrides = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("=Crystal_NaI", "MyScint")
                };

                var r = Cad2GdmlPipeline.Run(opt);
                if (r.ExitCode != 0)
                    throw new Exception("custom_mat_in_gdml: ExitCode=" + r.ExitCode + " " + r.MainErrorId);

                XDocument doc = XDocument.Load(r.GdmlPath);
                XNamespace ns = doc.Root.Name.Namespace;

                // <material name="MyScint">
                XElement mat = doc.Descendants(ns + "material")
                    .FirstOrDefault(e => (string)e.Attribute("name") == "MyScint");
                if (mat == null)
                    throw new Exception("custom_mat_in_gdml: <material name=\"MyScint\"> не найден");

                XElement dEl = mat.Element(ns + "D");
                if (dEl == null)
                    throw new Exception("custom_mat_in_gdml: <D> не найден в material");
                double dVal = Convert.ToDouble((string)dEl.Attribute("value"), CultureInfo.InvariantCulture);
                if (Math.Abs(dVal - 4.2) > 1e-9)
                    throw new Exception("custom_mat_in_gdml: D value=" + dVal.ToString(CultureInfo.InvariantCulture));
                string dUnit = (string)dEl.Attribute("unit");
                if (dUnit != "g/cm3")
                    throw new Exception("custom_mat_in_gdml: D unit=" + dUnit);

                // volume с "Crystal" → materialref ref="MyScint"
                XElement crystalVol = null;
                foreach (XElement vol in doc.Descendants(ns + "volume"))
                {
                    string vname = (string)vol.Attribute("name");
                    if (vname != null && vname.IndexOf("Crystal", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        crystalVol = vol;
                        break;
                    }
                }
                if (crystalVol == null)
                    throw new Exception("custom_mat_in_gdml: volume с Crystal не найден");

                XElement refEl = crystalVol.Descendants(ns + "materialref").FirstOrDefault();
                if (refEl == null)
                    throw new Exception("custom_mat_in_gdml: <materialref> не найден в кристалле");
                string refName = (string)refEl.Attribute("ref");
                if (refName != "MyScint")
                    throw new Exception("custom_mat_in_gdml: materialref ref=" + refName);
            }

            // второй прогон — без CustomMaterials, тот же override → E_MAT_UNKNOWN
            {
                var opt = BuildOptions(dir, new[] { "Crystal_NaI.stl", "Housing.stl" });
                opt.CustomMaterials = null;
                opt.MaterialOverrides = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("=Crystal_NaI", "MyScint")
                };

                var r = Cad2GdmlPipeline.Run(opt);
                if (r.ExitCode == 0)
                    throw new Exception("custom_mat_in_gdml(no def): ExitCode=0, ожидаем ненулевой");
                if (r.MainErrorId != "E_MAT_UNKNOWN")
                    throw new Exception("custom_mat_in_gdml(no def): MainErrorId=" + r.MainErrorId);
            }
        }

        // Сборка PipelineOptions (аналог RunPipe из Checks2j)
        private static PipelineOptions BuildOptions(string dir, string[] inputs)
        {
            var opt = new PipelineOptions();
            opt.Inputs = new List<string>();
            foreach (string inp in inputs)
                opt.Inputs.Add(Path.IsPathRooted(inp) ? inp : Path.Combine(dir, inp));
            opt.UnitToMm = 1.0;
            opt.FrontSign = "+z";
            opt.OutDir = dir;
            opt.BaseName = "o";
            opt.AllowDefaultMaterial = false;
            opt.NoRepair = true;
            return opt;
        }
    }
}
