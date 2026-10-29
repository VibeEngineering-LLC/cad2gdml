using System;
using System.Collections.Generic;
using System.Globalization;
using Cad2Gdml.Scene;

namespace Cad2Gdml.Report
{
    public static class DiagnosticGrouper
    {
        // Группирует диагностики и форматирует в список строк
        public static List<string> Format(IEnumerable<Diagnostic> items, bool includeInfo)
        {
            var result = new List<string>();
            if (items == null) return result;

            // Группируем по (Severity, Code), сохраняем порядок первого появления
            var groups = new List<Group>();
            var index = new Dictionary<string, int>();

            foreach (var d in items)
            {
                if (d == null) continue;

                // Пропускаем Info, кроме I_SPLIT, если includeInfo = false
                if (d.Severity == DiagSeverity.Info && !includeInfo && d.Code != "I_SPLIT")
                    continue;

                string code = d.Code ?? "";
                string key = d.Severity.ToString() + "|" + code;

                int idx;
                if (!index.TryGetValue(key, out idx))
                {
                    idx = groups.Count;
                    index[key] = idx;
                    groups.Add(new Group(d.Severity, code, new List<Diagnostic> { d }));
                }
                else
                {
                    groups[idx].Items.Add(d);
                }
            }

            // Сортировка: Error → Warning → Info; внутри — стабильно по порядку
            groups.Sort((a, b) => SeverityRank(a.Severity).CompareTo(SeverityRank(b.Severity)));

            foreach (var g in groups)
            {
                int n = g.Items.Count;
                string partName = g.Items[0].PartName ?? "";
                string message = g.Items[0].Message ?? "";

                if (n == 1)
                {
                    string prefix = partName.Length > 0 ? partName + ": " : "";
                    result.Add("  [" + g.Code + "] " + prefix + message);
                }
                else if (g.Severity == DiagSeverity.Info)
                {
                    // Info с N > 1: только код и количество
                    result.Add("  [" + g.Code + "] \u00d7" + n.ToString(CultureInfo.InvariantCulture));
                }
                else
                {
                    // Убираем префикс имени детали из сообщения
                    string cleaned = message;
                    string partPrefix = partName.Length > 0 ? partName + ": " : "";
                    if (partPrefix.Length > 0 && cleaned.StartsWith(partPrefix, StringComparison.Ordinal))
                        cleaned = cleaned.Substring(partPrefix.Length);

                    string line = "  [" + g.Code + "] \u00d7" + n.ToString(CultureInfo.InvariantCulture) + ": " + cleaned;

                    // Собираем имена первых 5 деталей (непустые)
                    var names = new List<string>();
                    foreach (var item in g.Items)
                    {
                        if (names.Count >= 5) break;
                        string pn = item.PartName ?? "";
                        if (pn.Length > 0) names.Add(pn);
                    }

                    if (names.Count > 0)
                    {
                        line += " (" + string.Join(", ", names);
                        if (n > 5)
                            line += " \u0438 \u0435\u0449\u0451 " + (n - 5).ToString(CultureInfo.InvariantCulture);
                        line += ")";
                    }

                    result.Add(line);
                }
            }

            return result;
        }

        private static int SeverityRank(DiagSeverity s)
        {
            switch (s)
            {
                case DiagSeverity.Error: return 0;
                case DiagSeverity.Warning: return 1;
                default: return 2;
            }
        }

        // Внутренняя группа диагностик
        private sealed class Group
        {
            public DiagSeverity Severity;
            public string Code;
            public List<Diagnostic> Items;

            public Group(DiagSeverity sev, string code, List<Diagnostic> items)
            {
                Severity = sev;
                Code = code;
                Items = items;
            }
        }
    }
}
