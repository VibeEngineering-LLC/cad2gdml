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
        public bool IsGuess; // true если совпадение по общим словам (crystal/scint)
    }

    // Библиотека веществ и правил назначения
    public sealed class MaterialLibrary
    {
        public Dictionary<string, MaterialDef> Materials;
        public List<MaterialRule> Rules;
        public string DefaultMaterial;

        // Установлено при совпадении по общим словам (crystal/кристалл/scint)
        public bool LastResolveWasGuess;

        // Встроенная библиотека с NIST-веществами
        public static MaterialLibrary Default()
        {
            var lib = new MaterialLibrary();
            lib.Materials = new Dictionary<string, MaterialDef>(StringComparer.OrdinalIgnoreCase);
            lib.Rules = new List<MaterialRule>();
            lib.DefaultMaterial = "G4_AIR";
            lib.LastResolveWasGuess = false;

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

            // Правила — границы: альтернатива в начале или после небуквы
            lib.Rules.Add(new MaterialRule { Pattern = "(^|[^\\p{L}])(nai)", Material = "NaI", IsGuess = false });
            lib.Rules.Add(new MaterialRule { Pattern = "(^|[^\\p{L}])(crystal|кристалл|scint)", Material = "NaI", IsGuess = true });
            lib.Rules.Add(new MaterialRule { Pattern = "(^|[^\\p{L}])(shell|housing|case|корпус|оболоч)", Material = "G4_Al", IsGuess = false });
            lib.Rules.Add(new MaterialRule { Pattern = "(^|[^\\p{L}])(water|вода|sample|проб)", Material = "G4_WATER", IsGuess = false });
            lib.Rules.Add(new MaterialRule { Pattern = "(^|[^\\p{L}])(lead|pb|свинец)", Material = "G4_Pb", IsGuess = false });
            lib.Rules.Add(new MaterialRule { Pattern = "(^|[^\\p{L}])(cu|copper|медь)", Material = "G4_Cu", IsGuess = false });
            lib.Rules.Add(new MaterialRule { Pattern = "(^|[^\\p{L}])(air|воздух)", Material = "G4_AIR", IsGuess = false });

            return lib;
        }

        // Поиск вещества по имени (регистронезависимо), возвращает def с каноническим Name
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
            LastResolveWasGuess = false;
            if (string.IsNullOrEmpty(partName)) return null;
            foreach (MaterialRule rule in Rules)
            {
                if (string.IsNullOrEmpty(rule.Pattern)) continue;
                try
                {
                    if (Regex.IsMatch(partName, rule.Pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                    {
                        LastResolveWasGuess = rule.IsGuess;
                        return rule.Material;
                    }
                }
                catch (ArgumentException)
                {
                    // некорректный regex — пропускаем
                }
            }
            return null;
        }

        // Загрузка из JSON (две схемы состава, валидация)
        public static MaterialLibrary LoadJson(string path)
        {
            string text = File.ReadAllText(path);
            object root = Cad2Gdml.Json.MiniJson.Parse(text);

            var dict = root as Dictionary<string, object>;
            if (dict == null)
                throw new InvalidDataException("E_MAT_ROOT: корень JSON не является объектом");

            var lib = new MaterialLibrary();
            lib.Materials = new Dictionary<string, MaterialDef>(StringComparer.OrdinalIgnoreCase);
            lib.Rules = new List<MaterialRule>();
            lib.DefaultMaterial = "G4_AIR";
            lib.LastResolveWasGuess = false;

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

                        // Плотность: "density" или "density_g_cm3"
                        double density = 0.0;
                        bool hasDensity = false;

                        object densObj;
                        if (matDict.TryGetValue("density", out densObj))
                        {
                            density = ToDouble(densObj);
                            hasDensity = true;
                        }
                        if (!hasDensity)
                        {
                            object densGObj;
                            if (matDict.TryGetValue("density_g_cm3", out densGObj))
                            {
                                density = ToDouble(densGObj);
                                hasDensity = true;
                            }
                        }

                        if (!hasDensity || density <= 0.0)
                            throw new InvalidDataException("E_MAT_DENSITY: некорректная плотность для '" + kv.Key + "'");

                        // Состав: "elements" (dict) или "fractions" (list of [ref, frac])
                        var fractions = new List<KeyValuePair<string, double>>();

                        object elemObj;
                        if (matDict.TryGetValue("elements", out elemObj))
                        {
                            var elems = elemObj as Dictionary<string, object>;
                            if (elems != null)
                            {
                                foreach (KeyValuePair<string, object> e in elems)
                                {
                                    fractions.Add(new KeyValuePair<string, double>(e.Key, ToDouble(e.Value)));
                                }
                            }
                        }

                        if (fractions.Count == 0)
                        {
                            object fracObj;
                            if (matDict.TryGetValue("fractions", out fracObj))
                            {
                                var fracList = fracObj as List<object>;
                                if (fracList != null)
                                {
                                    foreach (object item in fracList)
                                    {
                                        var pair = item as List<object>;
                                        if (pair == null || pair.Count < 2) continue;
                                        string refName = pair[0] as string;
                                        double fracVal = ToDouble(pair[1]);
                                        if (refName != null)
                                            fractions.Add(new KeyValuePair<string, double>(refName, fracVal));
                                    }
                                }
                            }
                        }

                        if (fractions.Count == 0)
                            throw new InvalidDataException("E_MAT_COMPO: пустой состав для '" + kv.Key + "'");

                        // Нормализация долей: сумма ≈ 1 -> нормализуем, иначе ошибка
                        double sum = 0.0;
                        for (int i = 0; i < fractions.Count; i++)
                            sum += fractions[i].Value;

                        if (Math.Abs(sum - 1.0) <= 1e-3)
                        {
                            for (int i = 0; i < fractions.Count; i++)
                            {
                                double norm = fractions[i].Value / sum;
                                fractions[i] = new KeyValuePair<string, double>(fractions[i].Key, norm);
                            }
                        }
                        else
                        {
                            throw new InvalidDataException(
                                "E_MAT_FRAC: сумма долей = " + sum.ToString("R", CultureInfo.InvariantCulture) + " для '" + kv.Key + "'");
                        }

                        lib.Materials[kv.Key] = new MaterialDef
                        {
                            Name = kv.Key,
                            DensityGcm3 = density,
                            MassFractions = fractions,
                            IsNist = false
                        };
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
                            lib.Rules.Add(new MaterialRule { Pattern = pattern, Material = material, IsGuess = false });
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

            // Проверка: каждое правило ссылается на существующее вещество
            foreach (MaterialRule rule in lib.Rules)
            {
                if (rule.Material != null && !lib.Materials.ContainsKey(rule.Material))
                    throw new InvalidDataException("E_MAT_UNKNOWN: " + rule.Material);
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
