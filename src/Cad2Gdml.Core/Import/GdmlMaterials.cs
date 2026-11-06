using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Cad2Gdml.Materials;

namespace Cad2Gdml.Import
{
    public static class GdmlMaterials
    {
        public static List<MaterialDef> Load(string path, out List<string> notes)
        {
            notes = new List<string>();
            XDocument doc;
            try { doc = XDocument.Load(path); }
            catch (System.Xml.XmlException ex)
            { throw new InvalidDataException("Не удалось разобрать XML: " + ex.Message, ex); }

            // Собираем имена элементов и материалов из всего файла
            var elementSym = new Dictionary<string, string>(StringComparer.Ordinal);
            var materialNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var el in doc.Descendants("element"))
            {
                var n = (string)el.Attribute("name");
                if (n == null || elementSym.ContainsKey(n)) continue;
                var f = (string)el.Attribute("formula");
                elementSym[n] = f ?? n;
            }
            foreach (var m in doc.Descendants("material"))
            {
                var n = (string)m.Attribute("name");
                if (n != null) materialNames.Add(n);
            }

            var materialsNode = doc.Descendants("materials").FirstOrDefault();
            var result = new List<MaterialDef>();
            if (materialsNode == null) return result;

            foreach (var mat in materialsNode.Elements("material"))
            {
                string name = (string)mat.Attribute("name");
                if (name == null || name.StartsWith("G4_")) continue; // NIST-справочные

                // Плотность из <D unit=... value=.../>
                double density = 0; bool ok = false;
                var d = mat.Element("D");
                if (d != null)
                {
                    var unit = (string)d.Attribute("unit");
                    var vs = (string)d.Attribute("value");
                    if (vs != null && double.TryParse(vs, NumberStyles.Float, CultureInfo.InvariantCulture, out double raw))
                    {
                        if (unit == "g/cm3") { density = raw; ok = true; }
                        else if (unit == "mg/cm3" || unit == "kg/m3") { density = raw / 1000.0; ok = true; }
                    }
                }
                if (!ok) { notes.Add("Вещество " + name + " пропущено: нет плотности"); continue; }

                // Composite — не поддерживаем
                if (mat.Element("composite") != null)
                { notes.Add("Вещество " + name + " пропущено: состав по числу атомов не поддержан"); continue; }

                // Состав из <fraction>
                var frs = mat.Elements("fraction").ToList();
                if (frs.Count == 0) { notes.Add("Вещество " + name + " пропущено: нет состава"); continue; }

                var bySym = new Dictionary<string, double>(StringComparer.Ordinal);
                double total = 0; bool skip = false;
                foreach (var fr in frs)
                {
                    string r = (string)fr.Attribute("ref");
                    if (r == null) continue;
                    if (!double.TryParse((string)fr.Attribute("n"), NumberStyles.Float, CultureInfo.InvariantCulture, out double n)) continue;
                    // Если ref — это другой материал, а не элемент
                    if (materialNames.Contains(r) && !elementSym.ContainsKey(r))
                    { notes.Add("Вещество " + name + " пропущено: состав по числу атомов не поддержан"); skip = true; break; }
                    if (!elementSym.ContainsKey(r))
                    { notes.Add("Вещество " + name + " пропущено: состав по числу атомов не поддержан"); skip = true; break; }
                    string sym = elementSym[r];
                    bySym[sym] = bySym.ContainsKey(sym) ? bySym[sym] + n : n;
                    total += n;
                }
                if (skip) continue;
                if (total <= 0) { notes.Add("Вещество " + name + " пропущено: нет состава"); continue; }

                // Нормировка
                if (Math.Abs(total - 1.0) > 1e-3)
                { notes.Add("Вещество " + name + ": сумма долей нормирована на 1"); total = 1.0; }

                var def = new MaterialDef { Name = name, DensityGcm3 = density, IsNist = false,
                    MassFractions = new List<KeyValuePair<string, double>>() };
                foreach (var kv in bySym)
                    def.MassFractions.Add(new KeyValuePair<string, double>(kv.Key, kv.Value / total));
                result.Add(def);
            }
            return result;
        }
    }
}
