using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Cad2Gdml.Scene
{
    // Опции регулярных выражений для назначения ролей
    public sealed class RoleOptions
    {
        public string CrystalRegex = "crystal|кристалл|nai|csi|labr|ceBr|scint";
        public string SourceRegex = "water|sample|source|проб|вода|marinelli|маринелли";
        public string ContainerRegex = "vessel|beaker|container|стакан|сосуд|bottle|кювет";
        public string ShieldRegex = "lead|pb|shield|защит|свинец";
        public string HousingRegex = "shell|housing|case|корпус|оболоч";
    }

    // Назначение ролей деталям сцены
    public static class RoleAssigner
    {
        // Оборачивает шаблон: совпадение только в начале или после не-буквы
        private static string WrapPattern(string pattern)
        {
            return "(^|[^\\p{L}])(" + pattern + ")";
        }

        public static void Assign(SceneModel scene, RoleOptions opt, string crystalNameOverride)
        {
            // Сброс всех ролей
            foreach (var p in scene.Parts)
                p.Role = PartRole.Other;

            bool hasOverride = !string.IsNullOrEmpty(crystalNameOverride);

            // Переопределение: ищем кристалл по имени
            if (hasOverride)
            {
                Part overridePart = null;

                // 1) Точное совпадение имени (без учёта регистра)
                foreach (var p in scene.Parts)
                {
                    if (string.Equals(p.Name, crystalNameOverride, StringComparison.OrdinalIgnoreCase))
                    {
                        overridePart = p;
                        break;
                    }
                }

                // 2) Иначе — часть содержит текст; допустимо ровно одна
                if (overridePart == null)
                {
                    int containsCount = 0;
                    foreach (var p in scene.Parts)
                    {
                        if (p.Name != null &&
                            p.Name.IndexOf(crystalNameOverride, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            containsCount++;
                            overridePart = p;
                        }
                    }

                    // Ноль или несколько — неоднозначность
                    if (containsCount != 1)
                    {
                        scene.Add("E_ROLE_AMBIG", DiagSeverity.Error, "",
                            "override: совпадений содержит = " +
                            containsCount.ToString(CultureInfo.InvariantCulture));
                    }
                }

                if (overridePart != null)
                    overridePart.Role = PartRole.Crystal;
            }

            // Шаблоны с word-boundary
            string reCrystal   = WrapPattern(opt.CrystalRegex);
            string reContainer = WrapPattern(opt.ContainerRegex);
            string reSource    = WrapPattern(opt.SourceRegex);
            string reShield    = WrapPattern(opt.ShieldRegex);
            string reHousing   = WrapPattern(opt.HousingRegex);

            // Назначаем роли по regex; при override Crystal по regex не выдаём
            foreach (var p in scene.Parts)
            {
                if (p.Role == PartRole.Crystal)
                    continue;

                if (!hasOverride && Regex.IsMatch(p.Name, reCrystal, RegexOptions.IgnoreCase))
                {
                    p.Role = PartRole.Crystal;
                    continue;
                }

                if (Regex.IsMatch(p.Name, reContainer, RegexOptions.IgnoreCase))
                    p.Role = PartRole.Container;
                else if (Regex.IsMatch(p.Name, reSource, RegexOptions.IgnoreCase))
                    p.Role = PartRole.Source;
                else if (Regex.IsMatch(p.Name, reShield, RegexOptions.IgnoreCase))
                    p.Role = PartRole.Shield;
                else if (Regex.IsMatch(p.Name, reHousing, RegexOptions.IgnoreCase))
                    p.Role = PartRole.Housing;
            }

            // Подсчёт кристаллов
            int crystalCount = 0;
            foreach (var p in scene.Parts)
            {
                if (p.Role == PartRole.Crystal)
                    crystalCount++;
            }

            // Диагностика: отсутствие или неоднозначность
            if (crystalCount == 0)
                scene.Add("E_ROLE_NONE", DiagSeverity.Error, "", "кристалл не найден");
            else if (crystalCount > 1)
                scene.Add("E_ROLE_AMBIG", DiagSeverity.Error, "",
                    "несколько кристаллов: " + crystalCount.ToString(CultureInfo.InvariantCulture));

            // Инфо о назначении каждой детали
            foreach (var p in scene.Parts)
                scene.Add("I_ROLE", DiagSeverity.Info, p.Name, p.Role.ToString());
        }
    }
}
