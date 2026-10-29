using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Cad2Gdml;
using Cad2Gdml.Meshes;
using Cad2Gdml.Scene;

namespace Cad2Gdml.Report
{
    // Генерация текстового описания сцены
    public static class SceneDescriber
    {
        public static string Describe(SceneModel scene, string sourceFile, string gdmlFile)
        {
            var sb = new StringBuilder();
            var inv = CultureInfo.InvariantCulture;

            // Заголовок
            Line(sb, scene.Title ?? "Сцена");
            Line(sb, "");
            Line(sb, "Вход: " + sourceFile);
            Line(sb, "GDML: " + gdmlFile);
            Line(sb, "Деталей: " + scene.Parts.Count.ToString(inv));
            Line(sb, "");

            // Таблица деталей
            Line(sb, "Имя | Роль | Материал | Тело | V, см³ | Габариты, мм");
            Line(sb, "----|------|----------|------|---------|--------------");
            foreach (var p in scene.Parts)
            {
                string role = RoleName(p.Role);
                string kind = KindName(p.Shape);
                string vol = (p.VolumeMm3 / 1000.0).ToString("F2", inv);
                string bbox = BboxStr(p.Mesh);
                Line(sb, p.Name + " | " + role + " | " + (p.Material ?? "-") + " | " + kind + " | " + vol + " | " + bbox);
            }
            Line(sb, "");

            // Детектор: кристалл-труба
            bool foundDet = false;
            foreach (var p in scene.Parts)
            {
                if (p.Role == PartRole.Crystal && p.Shape != null && p.Shape.Kind == SolidKind.Tube)
                {
                    double d = 2.0 * p.Shape.RMax;
                    double h = p.Shape.Z1 - p.Shape.Z0;
                    Line(sb, "Детектор: ⌀" + d.ToString("0.##", inv) + " × " + h.ToString("0.##", inv) + " мм (" + (p.Material ?? "?") + ")");
                    foundDet = true;
                    break;
                }
            }

            // Образец
            foreach (var p in scene.Parts)
            {
                if (p.Role == PartRole.Source)
                {
                    double ml = p.VolumeMm3 / 1000.0;
                    Line(sb, "Образец: " + ml.ToString("0.##", inv) + " мл");
                    break;
                }
            }

            if (foundDet)
                Line(sb, "");

            // Предупреждения
            var warns = new List<Diagnostic>();
            foreach (var d in scene.Diagnostics)
            {
                if (d.Severity == DiagSeverity.Warning || d.Severity == DiagSeverity.Error)
                    warns.Add(d);
            }
            if (warns.Count > 0)
            {
                Line(sb, "Предупреждения:");
                foreach (var d in warns)
                {
                    Line(sb, "  [" + d.Severity + "] " + d.Message);
                }
                Line(sb, "");
            }

            // Что не проверено
            Line(sb, "Что не проверено:");
            Line(sb, "  — физика материалов не проверяется");
            Line(sb, "  — перекрытия не проверяются в v0.1");

            return sb.ToString();
        }

        // Добавление строки с LF
        private static void Line(StringBuilder sb, string text)
        {
            sb.Append(text).Append('\n');
        }

        // Название роли по-русски
        private static string RoleName(PartRole r)
        {
            switch (r)
            {
                case PartRole.Crystal: return "кристалл";
                case PartRole.Source: return "образец";
                case PartRole.Container: return "контейнер";
                case PartRole.Shield: return "защита";
                case PartRole.Housing: return "корпус";
                default: return "прочее";
            }
        }

        // Название типа тела
        private static string KindName(SolidShape s)
        {
            if (s == null) return "тетра";
            switch (s.Kind)
            {
                case SolidKind.Tube: return "труба";
                case SolidKind.Polycone: return "поликонус";
                case SolidKind.Box: return "бокс";
                default: return "тетра";
            }
        }

        // Габариты бокса в мм
        private static string BboxStr(Mesh mesh)
        {
            if (mesh == null) return "-";
            // Инференс типа, чтобы не ссылаться на Vec3d напрямую
            mesh.GetBounds(out var mn, out var mx);
            var inv = CultureInfo.InvariantCulture;
            double w = mx.X - mn.X;
            double h = mx.Y - mn.Y;
            double d = mx.Z - mn.Z;
            return w.ToString("0.##", inv) + "×" + h.ToString("0.##", inv) + "×" + d.ToString("0.##", inv);
        }
    }
}
