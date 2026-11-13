using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace Cad2Gdml.Import
{
    internal static class GdmlComposite
    {
        internal static bool TryMassFractions(
            XElement material,
            IDictionary<string, string> elementSymbol,
            IDictionary<string, double> elementMass,
            out List<KeyValuePair<string, double>> fractions,
            out string note)
        {
            fractions = null;
            note = null;
            var name = (string)material.Attribute("name") ?? "";
            try
            {
                var composites = material.Elements().Where(e => e.Name.LocalName == "composite").ToList();
                if (composites.Count == 0)
                {
                    note = "Вещество " + name + " пропущено: нет состава";
                    return false;
                }
                var inv = CultureInfo.InvariantCulture;
                // Порядок символов по первому появлению
                var order = new List<string>();
                // Массы по символу (суммируются при повторе)
                var mass = new Dictionary<string, double>();
                // Атомная масса, с которой символ встретился впервые (изотопы с одной formula не сливаем)
                var atomA = new Dictionary<string, double>();
                bool fractional = false;

                foreach (var c in composites)
                {
                    var refName = (string)c.Attribute("ref");
                    var nRaw = (string)c.Attribute("n");
                    if (string.IsNullOrEmpty(refName) || string.IsNullOrEmpty(nRaw) ||
                        !double.TryParse(nRaw, NumberStyles.Float, inv, out var n) || n <= 0)
                    {
                        note = "Вещество " + name + " пропущено: неверная запись composite";
                        return false;
                    }
                    if (!elementSymbol.TryGetValue(refName, out var symbol))
                    {
                        note = "Вещество " + name + " пропущено: элемент " + refName + " не описан";
                        return false;
                    }
                    if (!elementMass.TryGetValue(refName, out var aMass))
                    {
                        note = "Вещество " + name + " пропущено: нет атомной массы элемента " + refName;
                        return false;
                    }
                    double a0;
                    if (!atomA.TryGetValue(symbol, out a0)) atomA[symbol] = aMass;
                    else if (Math.Abs(a0 - aMass) > 1e-9)
                    {
                        note = "Вещество " + name + " пропущено: элементы с одной formula " + symbol + " и разной атомной массой (изотопы) не поддержаны";
                        return false;
                    }
                    if (n != Math.Floor(n)) fractional = true;
                    if (!mass.ContainsKey(symbol)) { order.Add(symbol); mass[symbol] = 0.0; }
                    mass[symbol] += n * aMass; // m_i = n_i × A_i
                }

                var total = mass.Values.Sum();
                if (total <= 0)
                {
                    note = "Вещество " + name + " пропущено: нет состава";
                    return false;
                }
                // Массовые доли: доля_i = m_i / Σm
                fractions = order.Select(s => new KeyValuePair<string, double>(s, mass[s] / total)).ToList();
                if (fractional) note = "Вещество " + name + ": число атомов n нецелое, принято как есть";
                return true;
            }
            catch
            {
                note = "Вещество " + name + " пропущено: нет состава";
                fractions = null;
                return false;
            }
        }
    }
}
