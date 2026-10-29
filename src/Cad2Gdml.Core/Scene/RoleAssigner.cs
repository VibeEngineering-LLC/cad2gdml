using System;
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
        public static void Assign(SceneModel scene, RoleOptions opt, string crystalNameOverride)
        {
            // Сброс всех ролей
            foreach (var p in scene.Parts)
                p.Role = PartRole.Other;

            // Переопределение: часть с данным именем — кристалл
            if (!string.IsNullOrEmpty(crystalNameOverride))
            {
                foreach (var p in scene.Parts)
                {
                    if (string.Equals(p.Name, crystalNameOverride, StringComparison.OrdinalIgnoreCase))
                    {
                        p.Role = PartRole.Crystal;
                        break;
                    }
                }
            }

            // Совпадение по регулярным выражениям (порядок: Crystal, Container, Source, Shield, Housing)
            foreach (var p in scene.Parts)
            {
                if (p.Role == PartRole.Crystal)
                    continue;

                if (Regex.IsMatch(p.Name, opt.CrystalRegex, RegexOptions.IgnoreCase))
                    p.Role = PartRole.Crystal;
                else if (Regex.IsMatch(p.Name, opt.ContainerRegex, RegexOptions.IgnoreCase))
                    p.Role = PartRole.Container;
                else if (Regex.IsMatch(p.Name, opt.SourceRegex, RegexOptions.IgnoreCase))
                    p.Role = PartRole.Source;
                else if (Regex.IsMatch(p.Name, opt.ShieldRegex, RegexOptions.IgnoreCase))
                    p.Role = PartRole.Shield;
                else if (Regex.IsMatch(p.Name, opt.HousingRegex, RegexOptions.IgnoreCase))
                    p.Role = PartRole.Housing;
            }

            // Подсчёт кристаллов
            int crystalCount = 0;
            foreach (var p in scene.Parts)
            {
                if (p.Role == PartRole.Crystal)
                    crystalCount++;
            }

            // Диагностика: отсутствие или неоднозначность кристалла
            if (crystalCount == 0)
                scene.Add("E_ROLE_NONE", DiagSeverity.Error, "", "кристалл не найден");
            else if (crystalCount > 1)
                scene.Add("E_ROLE_AMBIG", DiagSeverity.Error, "", "несколько кристаллов: " + crystalCount.ToString(System.Globalization.CultureInfo.InvariantCulture));

            // Инфо о назначении каждой детали
            foreach (var p in scene.Parts)
                scene.Add("I_ROLE", DiagSeverity.Info, p.Name, p.Role.ToString());
        }
    }
}
