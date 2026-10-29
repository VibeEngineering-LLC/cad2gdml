using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace Cad2Gdml.Materials
{
    // Определение вещества
    public sealed class MaterialDef
    {
        public string Name;
        public double DensityGcm3;
        public List<KeyValuePair<string, double>> MassFractions;
        public bool IsNist;
    }

    // Правило: regex на имя детали -> вещество
    public sealed class MaterialRule
    {
        public string Pattern;
        public string Material;
    }

    // Библиотека веществ и правил назначения
    public sealed class MaterialLibrary
    {
        public Dictionary<string, MaterialDef> Materials;
        public List<MaterialRule> Rules;
        public string DefaultMaterial;

        // Встроенная библиотека с NIST-веществами
        public static MaterialLibrary Default()
        {
            var lib = new MaterialLibrary();
            lib.Materials = new Dictionary<string, MaterialDef>(StringComparer.OrdinalIgnoreCase);
            lib.Rules = new List<MaterialRule>();
            lib.DefaultMaterial = "G4_AIR";

            // NIST
            lib.Materials["G4_AIR"] = Nist("G4_AIR", 0.001205,
                new KeyValuePair<string, double>("N", 0.75527),
                new KeyValuePair<string, double>("O", 0.231917),
                new KeyValuePair<string, double>("Ar", 0.012827));
            lib.Materials["G4_WATER"] = Nist("G4_WATER", 1.0,
                new KeyValuePair<string, double>("H", 0.111896),
                new KeyValuePair<string, double>("O", 0.888104));
            lib.Materials["G4_Al"] = Nist("G4_Al", 2.699,
                new KeyValuePair<string, double>("Al", 1.0));
            lib.Materials["G4_Fe"] = Nist("G4_Fe", 7.874,
                new KeyValuePair<string, double>("Fe", 1.0));
            lib.Materials["G4_Cu"] = Nist("G4_Cu", 8.96,
                new KeyValuePair<string, double>("Cu", 1.0));
            lib.Materials["G4_Pb"] = Nist("G4_Pb", 11.35,
                new KeyValuePair<string, double>("Pb", 1.0));
            lib.Materials["G4_Sn"] = Nist("G4_Sn", 7.31,
                new KeyValuePair<string, double>("Sn", 1.0));
            lib.Materials["G4_Si"] = Nist("G4_Si", 2.33,
                new KeyValuePair<string, double>("Si", 1.0));
            lib.Materials["G4_POLYETHYLENE"] = Nist("G4_POLYETHYLENE", 0.94,
                new KeyValuePair<string, double>("H", 0.143711),
                new KeyValuePair<string, double>("C", 0.856289));

            // Свои
            lib.Materials["NaI"] = Custom("NaI", 3.67,
                new KeyValuePair<string, double>("Na", 0.1534),
                new KeyValuePair<string, double>("I", 0.8466));
            lib.Materials["NaI_Tl"] = Custom("NaI_Tl", 3.67,
                new KeyValuePair<string, double>("Na", 0.1534),
                new KeyValuePair<string, double>("I", 0.8466));
            lib.Materials["CsI"] = Custom("CsI", 4.51,
                new KeyValuePair<string, double>("Cs", 0.5113),
                new KeyValuePair<string, double>("I", 0.4887));

            // Правила
            lib.Rules.Add(new MaterialRule { Pattern = "crystal|nai|scint|кристалл", Material = "NaI" });
            lib.Rules.Add(new MaterialRule { Pattern = "shell|housing|case|корпус|оболоч", Material = "G4_Al" });
            lib.Rules.Add(new MaterialRule { Pattern = "water|вода|sample|проб", Material = "G4_WATER" });
            lib.Rules.Add(new MaterialRule { Pattern = "lead|pb|свинец", Material = "G4_Pb" });
            lib.Rules.Add(new MaterialRule { Pattern = "cu|copper|медь", Material = "G4_Cu" });
            lib.Rules.Add(new MaterialRule { Pattern = "air|воздух", Material = "G4_AIR" });

            return lib;
        }

        // Поиск вещества по имени (регистронезависимо)
        public MaterialDef Find(string name)
        {
            if (name == null) return null;
            MaterialDef def;
            if (Materials.TryGetValue(name, out def))
                return def;
            return null;
        }

        // Назначение: первое совпавшее правило -> имя вещества, иначе null
        public string Resolve(string partName)
        {
            if (string.IsNullOrEmpty(partName)) return null;
            foreach (MaterialRule rule in Rules)
            {
                if (string.IsNullOrEmpty(rule.Pattern)) continue;
                try
                {
                    if (Regex.IsMatch(partName, rule.Pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                        return rule.Material;
                }
                catch (ArgumentException)
                {
                    // некорректный regex — пропускаем
                }
            }
            return null;
        }

        // Загрузка из JSON (custom, stl_rules, stl_default)
        public static MaterialLibrary LoadJson(string path)
        {
            string text = File.ReadAllText(path);
            object root = Cad2Gdml.Json.MiniJson.Parse(text);
            var dict = root as Dictionary<string, object>;
            if (dict == null) return Default();

            var lib = new MaterialLibrary();
            lib.Materials = new Dictionary<string, MaterialDef>(StringComparer.OrdinalIgnoreCase);
            lib.Rules = new List<MaterialRule>();
            lib.DefaultMaterial = "G4_AIR";

            // Ключ "custom"
            object customObj;
            if (dict.TryGetValue("custom", out customObj))
            {
                var custom = customObj as Dictionary<string, object>;
                if (custom != null)
                {
                    foreach (KeyValuePair<string, object> kv in custom)
                    {
                        var matDict = kv.Value as Dictionary<string, object>;
                        if (matDict == null) continue;
                        var def = new MaterialDef
                        {
                            Name = kv.Key,
                            IsNist = false,
                            MassFractions = new List<KeyValuePair<string, double>>()
                        };
                        object densObj;
                        if (matDict.TryGetValue("density", out densObj))
                            def.DensityGcm3 = ToDouble(densObj);
                        object elemObj;
                        if (matDict.TryGetValue("elements", out elemObj))
                        {
                            var elems = elemObj as Dictionary<string, object>;
                            if (elems != null)
                            {
                                foreach (KeyValuePair<string, object> e in elems)
                                {
                                    def.MassFractions.Add(new KeyValuePair<string, double>(e.Key, ToDouble(e.Value)));
                                }
                            }
                        }
                        lib.Materials[kv.Key] = def;
                    }
                }
            }

            // Ключ "stl_rules"
            object rulesObj;
            if (dict.TryGetValue("stl_rules", out rulesObj))
            {
                var rulesList = rulesObj as List<object>;
                if (rulesList != null)
                {
                    foreach (object item in rulesList)
                    {
                        var pair = item as List<object>;
                        if (pair == null || pair.Count < 2) continue;
                        string pattern = pair[0] as string;
                        string material = pair[1] as string;
                        if (pattern != null && material != null)
                            lib.Rules.Add(new MaterialRule { Pattern = pattern, Material = material });
                    }
                }
            }

            // Ключ "stl_default"
            object defObj;
            if (dict.TryGetValue("stl_default", out defObj))
            {
                string defStr = defObj as string;
                if (defStr != null)
                    lib.DefaultMaterial = defStr;
            }

            return lib;
        }

        // Создание NIST-записи
        private static MaterialDef Nist(string name, double density, params KeyValuePair<string, double>[] fr)
        {
            return new MaterialDef
            {
                Name = name,
                DensityGcm3 = density,
                MassFractions = new List<KeyValuePair<string, double>>(fr),
                IsNist = true
            };
        }

        // Создание custom-записи
        private static MaterialDef Custom(string name, double density, params KeyValuePair<string, double>[] fr)
        {
            return new MaterialDef
            {
                Name = name,
                DensityGcm3 = density,
                MassFractions = new List<KeyValuePair<string, double>>(fr),
                IsNist = false
            };
        }

        // Преобразование объекта JSON в double
        private static double ToDouble(object o)
        {
            if (o == null) return 0.0;
            if (o is double) return (double)o;
            if (o is int) return (int)o;
            if (o is float) return (float)o;
            if (o is long) return (long)o;
            if (o is decimal) return (double)(decimal)o;
            if (o is string)
            {
                double r;
                if (double.TryParse((string)o, NumberStyles.Float, CultureInfo.InvariantCulture, out r))
                    return r;
            }
            return 0.0;
        }
    }
}
