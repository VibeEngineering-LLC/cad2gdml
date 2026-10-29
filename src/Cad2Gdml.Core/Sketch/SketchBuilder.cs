using System;
using System.Collections.Generic;
using System.Globalization;
using Cad2Gdml.Geometry;
using Cad2Gdml.Meshes;
using Cad2Gdml.Scene;

namespace Cad2Gdml.Sketch
{
    // Палитра цветов по роли
    public static class Palette
    {
        // Цвет (hex) по роли и имени детали
        public static string ColorHex(PartRole role, string partName)
        {
            switch (role)
            {
                case PartRole.Crystal:
                    return "5B8DEF";
                case PartRole.Source:
                    return "2BB7A3";
                case PartRole.Container:
                    return "9AA5B1";
                case PartRole.Shield:
                    return "5A6370";
                case PartRole.Housing:
                    return "F2A154";
                default:
                    return HashToHex(partName);
            }
        }

        // Детерминированный хеш имени → HSL → hex
        private static string HashToHex(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "888888";
            }
            int h = 0;
            foreach (char ch in name)
            {
                h = h * 31 + ch;
            }
            int hue = (h & 0x7FFFFFFF) % 360;
            double s = 0.30;
            double l = 0.55;
            double c = (1.0 - Math.Abs(2.0 * l - 1.0)) * s;
            double hp = hue / 60.0;
            double x = c * (1.0 - Math.Abs(hp % 2.0 - 1.0));
            double r1 = 0, g1 = 0, b1 = 0;
            if (hp < 1.0) { r1 = c; g1 = x; }
            else if (hp < 2.0) { r1 = x; g1 = c; }
            else if (hp < 3.0) { g1 = c; b1 = x; }
            else if (hp < 4.0) { g1 = x; b1 = c; }
            else if (hp < 5.0) { r1 = x; b1 = c; }
            else { r1 = c; b1 = x; }
            double m = l - c / 2.0;
            int r = Clamp8((int)Math.Round((r1 + m) * 255.0));
            int g = Clamp8((int)Math.Round((g1 + m) * 255.0));
            int b = Clamp8((int)Math.Round((b1 + m) * 255.0));
            return r.ToString("X2", CultureInfo.InvariantCulture)
                 + g.ToString("X2", CultureInfo.InvariantCulture)
                 + b.ToString("X2", CultureInfo.InvariantCulture);
        }

        private static int Clamp8(int v)
        {
            if (v < 0) return 0;
            if (v > 255) return 255;
            return v;
        }
    }

    // Форматирование подписей
    public static class CaptionFormatter
    {
        // Строки подписи для сцены
        public static List<string> Lines(SceneModel scene)
        {
            List<string> lines = new List<string>();

            // Строка 1 — детектор
            Part crystal = FindFirstRole(scene, PartRole.Crystal);
            if (crystal != null && crystal.Shape != null)
            {
                if (crystal.Shape.Kind == SolidKind.Tube)
                {
                    double d = 2.0 * crystal.Shape.RMax;
                    double h = crystal.Shape.Z1 - crystal.Shape.Z0;
                    lines.Add("\u0414\u0435\u0442\u0435\u043A\u0442\u043E\u0440: \u2300" + Fmt(d) + " \u00D7 " + Fmt(h) + " \u043C\u043C");
                }
                else if (crystal.Shape.Kind == SolidKind.Box)
                {
                    lines.Add("\u0414\u0435\u0442\u0435\u043A\u0442\u043E\u0440: " + Fmt(crystal.Shape.BoxX) + " \u00D7 " + Fmt(crystal.Shape.BoxY) + " \u00D7 " + Fmt(crystal.Shape.BoxZ) + " \u043C\u043C");
                }
            }

            // Строка 2 — стакан Маринелли (Container, tube)
            Part container = FindFirstRole(scene, PartRole.Container);
            if (container != null && container.Shape != null && container.Shape.Kind == SolidKind.Tube)
            {
                double d = 2.0 * container.Shape.RMax;
                double h = container.Shape.Z1 - container.Shape.Z0;
                lines.Add("\u0421\u0442\u0430\u043A\u0430\u043D \u041C\u0430\u0440\u0438\u043D\u0435\u043B\u043B\u0438: \u2300" + Fmt(d) + " \u00D7 " + Fmt(h) + " \u043C\u043C");
            }

            // Образец (Source)
            Part source = FindFirstRole(scene, PartRole.Source);
            if (source != null && source.Shape != null)
            {
                if (source.Shape.Kind == SolidKind.Tube)
                {
                    double d = 2.0 * source.Shape.RMax;
                    double h = source.Shape.Z1 - source.Shape.Z0;
                    lines.Add("\u041E\u0431\u0440\u0430\u0437\u0435\u0446: \u2300" + Fmt(d) + " \u00D7 " + Fmt(h) + " \u043C\u043C");
                }
                else if (source.Shape.Kind == SolidKind.Box)
                {
                    lines.Add("\u041E\u0431\u0440\u0430\u0437\u0435\u0446: " + Fmt(source.Shape.BoxX) + " \u00D7 " + Fmt(source.Shape.BoxY) + " \u00D7 " + Fmt(source.Shape.BoxZ) + " \u043C\u043C");
                }
            }

            // Не в расчёте (Shield / Housing)
            List<string> excluded = new List<string>();
            foreach (Part p in scene.Parts)
            {
                if (p.Role == PartRole.Shield || p.Role == PartRole.Housing)
                {
                    excluded.Add(p.Name);
                }
            }
            if (excluded.Count > 0)
            {
                lines.Add("\u041D\u0435 \u0432 \u0440\u0430\u0441\u0447\u0451\u0442\u0435: " + string.Join(", ", excluded));
            }

            return lines;
        }

        private static string Fmt(double v)
        {
            return v.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static Part FindFirstRole(SceneModel scene, PartRole role)
        {
            foreach (Part p in scene.Parts)
            {
                if (p.Role == role)
                {
                    return p;
                }
            }
            return null;
        }
    }

    // Сборка модели сечения y = 0
    public static class SketchBuilder
    {
        private const double JoinTol = 1e-4;

        // Построить модель сечения
        public static SketchModel Build(SceneModel scene)
        {
            SketchModel model = new SketchModel();
            model.Regions = new List<SketchRegion>();
            model.Captions = CaptionFormatter.Lines(scene);

            foreach (Part part in scene.Parts)
            {
                if (part.Shape == null)
                {
                    continue;
                }

                switch (part.Shape.Kind)
                {
                    case SolidKind.Tube:
                        AddTube(model, part);
                        break;
                    case SolidKind.Polycone:
                        AddPolycone(model, part);
                        break;
                    case SolidKind.Box:
                        AddBox(model, part);
                        break;
                    case SolidKind.Tessellated:
                        AddTessellated(model, part);
                        break;
                }
            }

            return model;
        }

        // Трубка — сечение хордой с учётом смещения оси (X0, Y0)
        private static void AddTube(SketchModel model, Part part)
        {
            double rMax = part.Shape.RMax;
            double rMin = part.Shape.RMin;
            double z0 = part.Shape.Z0;
            double z1 = part.Shape.Z1;
            double cx = part.Shape.X0;
            double cy = part.Shape.Y0;

            // Плоскость y=0 не пересекает трубку
            if (Math.Abs(cy) >= rMax)
            {
                return;
            }

            double halfOuter = Math.Sqrt(Math.Max(0.0, rMax * rMax - cy * cy));

            if (rMin <= 0.0)
            {
                AddToRegions(model, MakeRect(part, cx - halfOuter, cx + halfOuter, z0, z1));
            }
            else
            {
                double halfInner = Math.Sqrt(Math.Max(0.0, rMin * rMin - cy * cy));
                if (halfInner < 1e-9)
                {
                    AddToRegions(model, MakeRect(part, cx - halfOuter, cx + halfOuter, z0, z1));
                }
                else
                {
                    AddToRegions(model, MakeRect(part, cx - halfOuter, cx - halfInner, z0, z1));
                    AddToRegions(model, MakeRect(part, cx + halfInner, cx + halfOuter, z0, z1));
                }
            }
        }

        // Поликонус — сечение с учётом смещения оси (X0, Y0)
        private static void AddPolycone(SketchModel model, Part part)
        {
            List<double> zp = part.Shape.ZPlane;
            List<double> ro = part.Shape.ROuter;
            List<double> ri = part.Shape.RInner;
            int n = zp.Count;
            if (n < 2)
            {
                return;
            }

            double cx = part.Shape.X0;
            double cy = part.Shape.Y0;

            // Проверка: есть ли хоть один уровень, который пересекает плоскость
            bool anyVisible = false;
            for (int i = 0; i < n; i++)
            {
                if (Math.Abs(cy) < ro[i])
                {
                    anyVisible = true;
                    break;
                }
            }
            if (!anyVisible)
            {
                return;
            }

            // Внешний контур
            List<Vec3d> outer = new List<Vec3d>(n * 2);
            for (int i = 0; i < n; i++)
            {
                double ho = Math.Sqrt(Math.Max(0.0, ro[i] * ro[i] - cy * cy));
                outer.Add(new Vec3d(cx + ho, 0.0, zp[i]));
            }
            for (int i = n - 1; i >= 0; i--)
            {
                double ho = Math.Sqrt(Math.Max(0.0, ro[i] * ro[i] - cy * cy));
                outer.Add(new Vec3d(cx - ho, 0.0, zp[i]));
            }

            SketchRegion op = new SketchRegion();
            op.Points = outer;
            op.IsClosed = true;
            op.Role = part.Role;
            op.Label = part.Name;
            AddToRegions(model, op);

            // Внутренний контур (полость)
            bool hasHole = false;
            for (int i = 0; i < n; i++)
            {
                if (ri[i] > 0.0 && Math.Abs(cy) < ri[i]) { hasHole = true; break; }
            }
            if (hasHole)
            {
                List<Vec3d> inner = new List<Vec3d>(n * 2);
                for (int i = 0; i < n; i++)
                {
                    double hi = Math.Sqrt(Math.Max(0.0, ri[i] * ri[i] - cy * cy));
                    inner.Add(new Vec3d(cx + hi, 0.0, zp[i]));
                }
                for (int i = n - 1; i >= 0; i--)
                {
                    double hi = Math.Sqrt(Math.Max(0.0, ri[i] * ri[i] - cy * cy));
                    inner.Add(new Vec3d(cx - hi, 0.0, zp[i]));
                }

                SketchRegion ip = new SketchRegion();
                ip.Points = inner;
                ip.IsClosed = true;
                ip.Role = part.Role;
                ip.Label = part.Name;
                AddToRegions(model, ip);
            }
        }

        // Брусок — центр в (X0, Y0, Z0), размер BoxX×BoxY×BoxZ
        private static void AddBox(SketchModel model, Part part)
        {
            double bx = part.Shape.BoxX;
            double by = part.Shape.BoxY;
            double bz = part.Shape.BoxZ;
            double cx = part.Shape.X0;
            double cy = part.Shape.Y0;
            double cz = part.Shape.Z0;

            // Плоскость y=0 не пересекает бокс
            if (Math.Abs(cy) > by / 2.0)
            {
                return;
            }

            double halfW = bx / 2.0;
            double halfH = bz / 2.0;

            AddToRegions(model, MakeRect(part, cx - halfW, cx + halfW, cz - halfH, cz + halfH));
        }

        // Tessellated — сечение треугольников плоскостью y=0
        private static void AddTessellated(SketchModel model, Part part)
        {
            Mesh mesh = part.Mesh;
            if (mesh == null || mesh.TriangleCount == 0)
            {
                return;
            }

            // Отрезки: [x1, z1, x2, z2]
            List<double[]> segs = new List<double[]>();

            for (int i = 0; i < mesh.TriangleCount; i++)
            {
                Vec3d a, b, c;
                mesh.GetTriangle(i, out a, out b, out c);

                double[] pa = Cross(a, b);
                double[] pb = Cross(b, c);
                double[] pc = Cross(c, a);

                if (pa != null && pb != null)
                {
                    segs.Add(new double[] { pa[0], pa[1], pb[0], pb[1] });
                }
                else if (pb != null && pc != null)
                {
                    segs.Add(new double[] { pb[0], pb[1], pc[0], pc[1] });
                }
                else if (pa != null && pc != null)
                {
                    segs.Add(new double[] { pa[0], pa[1], pc[0], pc[1] });
                }
            }

            if (segs.Count == 0)
            {
                return;
            }

            // Сшивка отрезков в полилинии по концам
            List<SketchRegion> chains = ChainSegs(segs, part);
            foreach (SketchRegion cr in chains)
            {
                AddToRegions(model, cr);
            }
        }

        // Пересечение отрезка с y=0; null если нет
        private static double[] Cross(Vec3d a, Vec3d b)
        {
            // Оба на плоскости — не пересечение
            if (Math.Abs(a.Y) < 1e-12 && Math.Abs(b.Y) < 1e-12)
            {
                return null;
            }
            // Оба с одной стороны
            if (a.Y * b.Y > 0.0)
            {
                return null;
            }

            double t = a.Y / (a.Y - b.Y);
            if (t < -1e-9 || t > 1.0 + 1e-9)
            {
                return null;
            }
            if (t < 0.0) t = 0.0;
            if (t > 1.0) t = 1.0;

            double x = a.X + t * (b.X - a.X);
            double z = a.Z + t * (b.Z - a.Z);
            return new double[] { x, z };
        }

        // Сшивка отрезков в полилинии
        private static List<SketchRegion> ChainSegs(List<double[]> segs, Part part)
        {
            int n = segs.Count;
            bool[] used = new bool[n];
            List<SketchRegion> result = new List<SketchRegion>();

            for (int i = 0; i < n; i++)
            {
                if (used[i])
                {
                    continue;
                }
                used[i] = true;

                double[] s = segs[i];
                List<Vec3d> pts = new List<Vec3d>();
                pts.Add(new Vec3d(s[0], 0.0, s[1]));
                pts.Add(new Vec3d(s[2], 0.0, s[3]));

                // Расширяем вперёд и назад
                bool grew = true;
                while (grew)
                {
                    grew = false;
                    double lx = pts[pts.Count - 1].X;
                    double lz = pts[pts.Count - 1].Z;

                    for (int j = 0; j < n; j++)
                    {
                        if (used[j])
                        {
                            continue;
                        }
                        double[] sj = segs[j];
                        if (Math.Abs(sj[0] - lx) < JoinTol && Math.Abs(sj[1] - lz) < JoinTol)
                        {
                            pts.Add(new Vec3d(sj[2], 0.0, sj[3]));
                            used[j] = true;
                            grew = true;
                            break;
                        }
                        if (Math.Abs(sj[2] - lx) < JoinTol && Math.Abs(sj[3] - lz) < JoinTol)
                        {
                            pts.Add(new Vec3d(sj[0], 0.0, sj[1]));
                            used[j] = true;
                            grew = true;
                            break;
                        }
                    }

                    // Расширение назад
                    double fx = pts[0].X;
                    double fz = pts[0].Z;
                    for (int j = 0; j < n; j++)
                    {
                        if (used[j])
                        {
                            continue;
                        }
                        double[] sj = segs[j];
                        if (Math.Abs(sj[2] - fx) < JoinTol && Math.Abs(sj[3] - fz) < JoinTol)
                        {
                            pts.Insert(0, new Vec3d(sj[0], 0.0, sj[1]));
                            used[j] = true;
                            grew = true;
                            break;
                        }
                        if (Math.Abs(sj[0] - fx) < JoinTol && Math.Abs(sj[1] - fz) < JoinTol)
                        {
                            pts.Insert(0, new Vec3d(sj[2], 0.0, sj[3]));
                            used[j] = true;
                            grew = true;
                            break;
                        }
                    }
                }

                if (pts.Count >= 2)
                {
                    // Проверка замкнутости: первая и последняя точки совпадают
                    Vec3d first = pts[0];
                    Vec3d last = pts[pts.Count - 1];
                    double dx = first.X - last.X;
                    double dz = first.Z - last.Z;
                    bool closed = (dx * dx + dz * dz) < (JoinTol * JoinTol);

                    SketchRegion region = new SketchRegion();
                    region.Points = pts;
                    region.IsClosed = closed;
                    region.Role = part.Role;
                    region.Label = part.Name;
                    result.Add(region);
                }
            }

            return result;
        }

        // Прямоугольник (замкнутый, 4 точки)
        private static SketchRegion MakeRect(Part part, double x0, double x1, double z0, double z1)
        {
            SketchRegion r = new SketchRegion();
            r.IsClosed = true;
            r.Role = part.Role;
            r.Label = part.Name;
            r.Points = new List<Vec3d>
            {
                new Vec3d(x0, 0.0, z0),
                new Vec3d(x1, 0.0, z0),
                new Vec3d(x1, 0.0, z1),
                new Vec3d(x0, 0.0, z1)
            };
            return r;
        }

        // Добавить регион в модель
        private static void AddToRegions(SketchModel model, SketchRegion region)
        {
            if (model.Regions == null)
            {
                model.Regions = new List<SketchRegion>();
            }
            model.Regions.Add(region);
        }
    }
}
