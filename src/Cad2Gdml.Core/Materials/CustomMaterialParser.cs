using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Cad2Gdml.Gdml;

namespace Cad2Gdml.Materials
{
    // Валидация «Своё вещество…» (#CG-32), без WinForms
    public static class CustomMaterialParser
    {
        public static bool TryParse(string name, string densityText, string compositionText, out MaterialDef def, out string error)
        {
            def = null;
            error = null;

            // 1. Имя
            if (string.IsNullOrWhiteSpace(name))
            {
                error = "Имя: латиница, цифры, _ и -, первая буква; не G4_…; до 41 символа.";
                return false;
            }
            string n = name.Trim();
            if (!Regex.IsMatch(n, "^[A-Za-z][A-Za-z0-9_\\-]{0,40}$", RegexOptions.CultureInvariant)
                || n.StartsWith("G4_", StringComparison.OrdinalIgnoreCase))
            {
                error = "Имя: латиница, цифры, _ и -, первая буква; не G4_…; до 41 символа.";
                return false;
            }

            // 2. Плотность
            if (string.IsNullOrWhiteSpace(densityText))
            {
                error = "Плотность: число г/см³, больше 0 и не более 30.";
                return false;
            }
            double density;
            string dTxt = densityText.Trim().Replace(',', '.');
            if (!double.TryParse(dTxt, NumberStyles.Float, CultureInfo.InvariantCulture, out density)
                || double.IsNaN(density) || double.IsInfinity(density)
                || density <= 0.0 || density > 30.0)
            {
                error = "Плотность: число г/см³, больше 0 и не более 30.";
                return false;
            }

            // 3. Состав
            if (string.IsNullOrWhiteSpace(compositionText))
            {
                error = "Состав: пусто. Пример: La=0.36688; Br=0.63312";
                return false;
            }

            string[] parts = Regex.Split(compositionText, "[;\r\n]+");
            var fractions = new List<KeyValuePair<string, double>>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int p = 0; p < parts.Length; p++)
            {
                string pair = parts[p].Trim();
                if (pair.Length == 0) continue;

                // Разделитель — первый '=' или ':'
                int sep = -1;
                int eq = pair.IndexOf('=');
                int co = pair.IndexOf(':');
                if (eq >= 0 && co >= 0) sep = Math.Min(eq, co);
                else if (eq >= 0) sep = eq;
                else if (co >= 0) sep = co;

                if (sep <= 0)
                {
                    error = "Состав: не разобрано «" + pair + "».";
                    return false;
                }

                string sym = pair.Substring(0, sep).Trim();
                string fracTxt = pair.Substring(sep + 1).Trim().Replace(',', '.');

                if (sym.Length == 0)
                {
                    error = "Состав: не разобрано «" + pair + "».";
                    return false;
                }

                double frac;
                if (!double.TryParse(fracTxt, NumberStyles.Float, CultureInfo.InvariantCulture, out frac)
                    || double.IsNaN(frac) || double.IsInfinity(frac)
                    || frac <= 0.0 || frac > 1.0)
                {
                    error = "Состав: не разобрано «" + pair + "».";
                    return false;
                }

                // Канонический символ: первая заглавная, остальное строчные
                string canonical = char.ToUpper(sym[0]) + sym.Substring(1).ToLower();

                if (!GdmlWriter.IsKnownElement(canonical))
                {
                    error = "Состав: неизвестный элемент «" + canonical + "».";
                    return false;
                }
                if (!seen.Add(canonical))
                {
                    error = "Состав: элемент " + canonical + " повторяется.";
                    return false;
                }

                fractions.Add(new KeyValuePair<string, double>(canonical, frac));
            }

            if (fractions.Count == 0)
            {
                error = "Состав: пусто. Пример: La=0.36688; Br=0.63312";
                return false;
            }

            // 4. Сумма долей ≈ 1
            double sum = 0.0;
            for (int i = 0; i < fractions.Count; i++)
                sum += fractions[i].Value;

            if (Math.Abs(sum - 1.0) > 1e-4)
            {
                error = "Состав: сумма долей = " + sum.ToString("0.#####", CultureInfo.InvariantCulture) + ", нужно 1 ± 0,0001.";
                return false;
            }

            def = new MaterialDef
            {
                Name = n,
                DensityGcm3 = density,
                MassFractions = fractions,
                IsNist = false
            };
            return true;
        }
    }
}
