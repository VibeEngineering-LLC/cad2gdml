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
        public bool IsGuess;
        public bool IsFileRule; // true — правило из файла, case-sensitive
    }

    // Библиотека веществ и правил назначения
    public sealed class MaterialLibrary
    {
        public Dictionary<string, MaterialDef> Materials;
        public List<MaterialRule> Rules;
        public string DefaultMaterial;
        public bool DefaultFromFile; // stl_default задан в materials.json
        public bool LastResolveWasGuess;

        // Проверка: имя является имплицитной NIST-ссылкой (G4_*)
        public bool IsNistRef(string name)
        {
            return name != null && name.StartsWith("G4_", StringComparison.Ordinal);
        }

        // Встроенная библиотека с NIST-веществами
        public static MaterialLibrary Default()
        {
            var lib = new MaterialLibrary();
            lib.Materials = new Dictionary<string, MaterialDef>(StringComparer.OrdinalIgnoreCase);
            lib.Rules = new List<MaterialRule>();
            lib.DefaultMaterial = "G4_AIR";
            lib.LastResolveWasGuess = false;

            // NIST-вещества
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

            // Свои вещества
            lib.Materials["NaI"] = Custom("NaI", 3.67,
                new KeyValuePair<string, double>("Na", 0.1534),
                new KeyValuePair<string, double>("I", 0.8466));
            lib.Materials["NaI_Tl"] = Custom("NaI_Tl", 3.67,
                new KeyValuePair<string, double>("Na", 0.1534),
                new KeyValuePair<string, double>("I", 0.8466));
            lib.Materials["CsI"] = Custom("CsI", 4.51,
                new KeyValuePair<string, double>("Cs", 0.5113),
                new KeyValuePair<string, double>("I", 0.4887));

            // Встроенные правила: границы и слева, и справа
            lib.Rules.Add(Builtin("(^|[^\\p{L}])(nai)(?=$|[^\\p{L}])", "NaI", false));
            lib.Rules.Add(Builtin("(^|[^\\p{L}])(crystal|кристалл|scint)(?=$|[^\\p{L}])", "NaI", true));
            lib.Rules.Add(Builtin("(^|[^\\p{L}])(shell|housing|case|корпус|оболоч)(?=$|[^\\p{L}])", "G4_Al", false));
            lib.Rules.Add(Builtin("(^|[^\\p{L}])(water|вода|sample|проб)(?=$|[^\\p{L}])", "G4_WATER", false));
            lib.Rules.Add(Builtin("(^|[^\\p{L}])(lead|pb|свинец)(?=$|[^\\p{L}])", "G4_Pb", false));
            lib.Rules.Add(Builtin("(^|[^\\p{L}])(cu|copper|медь)(?=$|[^\\p{L}])", "G4_Cu", false));
            lib.Rules.Add(Builtin("(^|[^\\p{L}])(air|воздух)(?=$|[^\\p{L}])", "G4_AIR", false));

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

        // Назначение: первое совпавшее правило -> имя вещества
        public string Resolve(string partName)
        {
            LastResolveWasGuess = false;
            if (string.IsNullOrEmpty(partName)) return null;
            foreach (MaterialRule rule in Rules)
            {
                if (string.IsNullOrEmpty(rule.Pattern)) continue;
                try
                {
                    bool matched;
                    if (rule.IsFileRule)
                        matched = Regex.IsMatch(partName, rule.Pattern, RegexOptions.CultureInvariant);
                    else
                        matched = Regex.IsMatch(partName, rule.Pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                    if (matched)
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

        // Загрузка из JSON: custom (list/dict), stl_rules, stl_default
        public static MaterialLibrary LoadJson(string path)
        {
            string text = File.ReadAllText(path); // FileNotFoundException при отсутствии
            object root = Cad2Gdml.Json.MiniJson.Parse(text);

            var dict = root as Dictionary<string, object>;
            if (dict == null)
                throw new InvalidDataException("E_MAT_ROOT: корень JSON не является объектом");

            // База — встроенная библиотека
            var lib = Default();

            // Ключ "custom": словарь {name:{...}} или список [{name,...}]
            object customObj;
            if (dict.TryGetValue("custom", out customObj))
            {
                var cd = customObj as Dictionary<string, object>;
                if (cd != null)
                {
                    foreach (KeyValuePair<string, object> kv in cd)
                        ParseCustomEntry(lib, kv.Key, kv.Value);
                }
                else
                {
                    var cl = customObj as List<object>;
                    if (cl != null)
                    {
                        foreach (object item in cl)
                        {
                            var entry = item as Dictionary<string, object>;
                            if (entry == null) continue;
                            string nm = null;
                            object nmObj;
                            if (entry.TryGetValue("name", out nmObj))
                                nm = nmObj as string;
                            if (string.IsNullOrEmpty(nm)) continue;
                            ParseCustomEntry(lib, nm, entry);
                        }
                    }
                }
            }

            // Ключ "stl_rules": правила из файла, case-sensitive, без границ
            object rulesObj;
            if (dict.TryGetValue("stl_rules", out rulesObj))
            {
                var rl = rulesObj as List<object>;
                if (rl != null)
                {
                    var fileRules = new List<MaterialRule>();
                    foreach (object item in rl)
                    {
                        var pair = item as List<object>;
                        if (pair == null || pair.Count < 2) continue;
                        string pat = pair[0] as string;
                        string mat = pair[1] as string;
                        if (pat != null && mat != null)
                            fileRules.Add(new MaterialRule { Pattern = pat, Material = mat, IsGuess = false, IsFileRule = true });
                    }
                    // Файловые правила — ПЕРВЫМИ
                    lib.Rules.InsertRange(0, fileRules);
                }
            }

            // Ключ "stl_default"
            object defObj;
            if (dict.TryGetValue("stl_default", out defObj))
            {
                string ds = defObj as string;
                if (ds != null)
                    lib.DefaultMaterial = ds;
                    lib.DefaultFromFile = true;
            }

            // Валидация: материал правила/default должен быть определён или G4_*
            foreach (MaterialRule rule in lib.Rules)
            {
                if (rule.Material == null) continue;
                if (!lib.Materials.ContainsKey(rule.Material) && !lib.IsNistRef(rule.Material))
                    throw new InvalidDataException("E_MAT_UNKNOWN: " + rule.Material);
            }
            if (lib.DefaultMaterial != null && !lib.Materials.ContainsKey(lib.DefaultMaterial) && !lib.IsNistRef(lib.DefaultMaterial))
                throw new InvalidDataException("E_MAT_UNKNOWN: " + lib.DefaultMaterial);

            return lib;
        }

        // Разбор одного custom-вещества из JSON
        private static void ParseCustomEntry(MaterialLibrary lib, string name, object raw)
        {
            var md = raw as Dictionary<string, object>;
            if (md == null) return;

            // Плотность
            double density = 0.0;
            bool hasD = false;
            object dObj;
            if (md.TryGetValue("density", out dObj))
            {
                density = ToDouble(dObj);
                hasD = true;
            }
            if (!hasD)
            {
                object dGObj;
                if (md.TryGetValue("density_g_cm3", out dGObj))
                {
                    density = ToDouble(dGObj);
                    hasD = true;
                }
            }
            if (!hasD || density <= 0.0)
                throw new InvalidDataException("E_MAT_DENSITY: некорректная плотность для '" + name + "'");

            // Состав: "elements" (dict) или "fractions" (list of [ref, frac])
            var fractions = new List<KeyValuePair<string, double>>();
            object elemObj;
            if (md.TryGetValue("elements", out elemObj))
            {
                var elems = elemObj as Dictionary<string, object>;
                if (elems != null)
                {
                    foreach (KeyValuePair<string, object> e in elems)
                        fractions.Add(new KeyValuePair<string, double>(e.Key, ToDouble(e.Value)));
                }
            }
            if (fractions.Count == 0)
            {
                object fracObj;
                if (md.TryGetValue("fractions", out fracObj))
                {
                    var fl = fracObj as List<object>;
                    if (fl != null)
                    {
                        foreach (object item in fl)
                        {
                            var pair = item as List<object>;
                            if (pair == null || pair.Count < 2) continue;
                            string refName = pair[0] as string;
                            double fv = ToDouble(pair[1]);
                            if (refName != null)
                                fractions.Add(new KeyValuePair<string, double>(refName, fv));
                        }
                    }
                }
            }
            if (fractions.Count == 0)
                throw new InvalidDataException("E_MAT_COMPO: пустой состав для '" + name + "'");

            // Нормализация долей
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
                    "E_MAT_FRAC: сумма долей = " + sum.ToString("R", CultureInfo.InvariantCulture) + " для '" + name + "'");
            }

            lib.Materials[name] = new MaterialDef
            {
                Name = name,
                DensityGcm3 = density,
                MassFractions = fractions,
                IsNist = false
            };
        }

        // Встроенное правило
        private static MaterialRule Builtin(string pattern, string material, bool isGuess)
        {
            return new MaterialRule { Pattern = pattern, Material = material, IsGuess = isGuess, IsFileRule = false };
        }

        // NIST-запись
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

        // Custom-запись
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

        // JSON-объект -> double
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
