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

            Part container = FindFirstRole(scene, PartRole.Container);
            if (container != null && container.Shape != null && container.Shape.Kind == SolidKind.Tube)
            {
                double d = 2.0 * container.Shape.RMax;
                double h = container.Shape.Z1 - container.Shape.Z0;
                lines.Add("\u0421\u0442\u0430\u043A\u0430\u043D \u041C\u0430\u0440\u0438\u043D\u0435\u043B\u043B\u0438: \u2300" + Fmt(d) + " \u00D7 " + Fmt(h) + " \u043C\u043C");
            }

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
        private const double LabelMinFrac = 0.03;   // 3 % сцены
        private const double NoiseMinFrac = 0.002;  // 0.2 % сцены
        private const int MaxLabels = 15;
        private const int MaxLabelLen = 24;

        // Построить модель сечения
        public static SketchModel Build(SceneModel scene)
        {
            // Первый проход: генерация регионов по частям
            List<Part> partOrder = new List<Part>();
            Dictionary<Part, List<SketchRegion>> partRegions = new Dictionary<Part, List<SketchRegion>>();

            // Без кристалла кадр не строится: сечение — на середине габарита сцены по y (иначе плоскость y=0 файла может мимо)
            double sectionY = 0.0;
            bool hasCrystal = false;
            double yMin = double.MaxValue, yMax = double.MinValue;
            foreach (Part part in scene.Parts)
            {
                if (part.Role == PartRole.Crystal) hasCrystal = true;
                if (part.Mesh == null || part.Mesh.TriangleCount == 0) continue;
                Vec3d lo, hi;
                part.Mesh.GetBounds(out lo, out hi);
                if (lo.Y < yMin) yMin = lo.Y;
                if (hi.Y > yMax) yMax = hi.Y;
            }
            if (!hasCrystal && yMax >= yMin) sectionY = 0.5 * (yMin + yMax);

            foreach (Part part in scene.Parts)
            {
                if (part.Shape == null)
                {
                    continue;
                }

                List<SketchRegion> regs = new List<SketchRegion>();
                switch (part.Shape.Kind)
                {
                    case SolidKind.Tube:
                        AddTube(regs, part);
                        break;
                    case SolidKind.Polycone:
                        AddPolycone(regs, part);
                        break;
                    case SolidKind.Box:
                        AddBox(regs, part);
                        break;
                    case SolidKind.Tessellated:
                        AddTessellated(regs, part, sectionY);
                        break;
                }

                if (regs.Count > 0)
                {
                    partOrder.Add(part);
                    partRegions[part] = regs;
                }
            }

            if (partOrder.Count == 0)
            {
                SketchModel empty = new SketchModel();
                empty.Regions = new List<SketchRegion>();
                empty.Captions = CaptionFormatter.Lines(scene);
                return empty;
            }

            // Габариты всей сцены (по x и z)
            double sceneMinX = double.MaxValue;
            double sceneMaxX = double.MinValue;
            double sceneMinZ = double.MaxValue;
            double sceneMaxZ = double.MinValue;

            foreach (Part p in partOrder)
            {
                foreach (SketchRegion r in partRegions[p])
                {
                    foreach (Vec3d pt in r.Points)
                    {
                        if (pt.X < sceneMinX) { sceneMinX = pt.X; }
                        if (pt.X > sceneMaxX) { sceneMaxX = pt.X; }
                        if (pt.Z < sceneMinZ) { sceneMinZ = pt.Z; }
                        if (pt.Z > sceneMaxZ) { sceneMaxZ = pt.Z; }
                    }
                }
            }

            double sceneWx = sceneMaxX - sceneMinX;
            double sceneWz = sceneMaxZ - sceneMinZ;

            // Фильтрация tessellated: убираем замкнутые полилинии-шум (< 0.2 % сцены),
            // но сохраняем наибольшую замкнутую для части
            foreach (Part p in partOrder)
            {
                if (p.Shape == null || p.Shape.Kind != SolidKind.Tessellated)
                {
                    continue;
                }

                List<SketchRegion> regs = partRegions[p];
                if (regs.Count <= 1)
                {
                    continue;
                }

                // Находим наибольшую замкнутую полилинию по площади bbox
                SketchRegion largestClosed = null;
                double largestArea = -1.0;

                foreach (SketchRegion r in regs)
                {
                    if (!r.IsClosed)
                    {
                        continue;
                    }
                    double[] bb = RegionBBox(r.Points);
                    double area = (bb[1] - bb[0]) * (bb[3] - bb[2]);
                    if (area > largestArea)
                    {
                        largestArea = area;
                        largestClosed = r;
                    }
                }

                double noiseW = sceneWx * NoiseMinFrac;
                double noiseH = sceneWz * NoiseMinFrac;

                if (largestClosed != null)
                {
                    List<SketchRegion> kept = new List<SketchRegion>();
                    foreach (SketchRegion r in regs)
                    {
                        if (r.IsClosed && !ReferenceEquals(r, largestClosed))
                        {
                            double[] bb = RegionBBox(r.Points);
                            double w = bb[1] - bb[0];
                            double h = bb[3] - bb[2];
                            if (w < noiseW && h < noiseH)
                            {
                                continue; // шум — пропускаем
                            }
                        }
                        kept.Add(r);
                    }
                    partRegions[p] = kept;
                }
            }

            // Bounding box каждой части (после фильтрации)
            Dictionary<Part, double[]> partBBox = new Dictionary<Part, double[]>();

            foreach (Part p in partOrder)
            {
                List<SketchRegion> regs = partRegions[p];
                if (regs.Count == 0)
                {
                    continue;
                }

                double minX = double.MaxValue;
                double maxX = double.MinValue;
                double minZ = double.MaxValue;
                double maxZ = double.MinValue;

                foreach (SketchRegion r in regs)
                {
                    foreach (Vec3d pt in r.Points)
                    {
                        if (pt.X < minX) { minX = pt.X; }
                        if (pt.X > maxX) { maxX = pt.X; }
                        if (pt.Z < minZ) { minZ = pt.Z; }
                        if (pt.Z > maxZ) { maxZ = pt.Z; }
                    }
                }

                partBBox[p] = new double[] { minX, maxX, minZ, maxZ };
            }

            // Определение частей, которым присваивается подпись:
            // bbox НЕ меньше 3 % сцены И по x И по z
            List<KeyValuePair<Part, double>> eligible = new List<KeyValuePair<Part, double>>();
            double labelMinW = sceneWx * LabelMinFrac;
            double labelMinH = sceneWz * LabelMinFrac;

            foreach (Part p in partOrder)
            {
                if (!partBBox.ContainsKey(p))
                {
                    continue;
                }

                double[] bb = partBBox[p];
                double w = bb[1] - bb[0];
                double h = bb[3] - bb[2];

                // Нет подписи, если и по x, и по z меньше порога
                if (w < labelMinW && h < labelMinH)
                {
                    continue;
                }

                eligible.Add(new KeyValuePair<Part, double>(p, w * h));
            }

            // Ограничение: максимум 15 подписей (наибольшие по площади)
            if (eligible.Count > MaxLabels)
            {
                eligible.Sort(delegate(KeyValuePair<Part, double> a, KeyValuePair<Part, double> b)
                {
                    return b.Value.CompareTo(a.Value);
                });
                eligible = eligible.GetRange(0, MaxLabels);
            }

            HashSet<Part> labelSet = new HashSet<Part>();
            foreach (KeyValuePair<Part, double> kv in eligible)
            {
                labelSet.Add(kv.Key);
            }

            // Сборка финальной модели
            SketchModel model = new SketchModel();
            model.Regions = new List<SketchRegion>();
            model.Captions = CaptionFormatter.Lines(scene);

            foreach (Part p in partOrder)
            {
                List<SketchRegion> regs = partRegions[p];

                // Контур без подписи
                foreach (SketchRegion r in regs)
                {
                    r.Label = null;
                    model.Regions.Add(r);
                }

                // Одна подпись на часть — в центре bbox сечения
                if (labelSet.Contains(p) && partBBox.ContainsKey(p))
                {
                    double[] bb = partBBox[p];
                    double cx = (bb[0] + bb[1]) / 2.0;
                    double cz = (bb[2] + bb[3]) / 2.0;

                    SketchRegion lbl = new SketchRegion();
                    lbl.Points = new List<Vec3d>
                    {
                        new Vec3d(cx, 0.0, cz),
                        new Vec3d(cx, 0.0, cz)
                    };
                    lbl.IsClosed = false;
                    lbl.Role = p.Role;
                    lbl.Label = ShortenName(p.Name);
                    model.Regions.Add(lbl);
                }
            }

            return model;
        }

        // Сокращение имени до 24 символов с многоточием
        private static string ShortenName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return name;
            }
            if (name.Length <= MaxLabelLen)
            {
                return name;
            }
            return name.Substring(0, MaxLabelLen - 1) + "\u2026";
        }

        // Bounding box набора точек: [minX, maxX, minZ, maxZ]
        private static double[] RegionBBox(List<Vec3d> pts)
        {
            double minX = double.MaxValue;
            double maxX = double.MinValue;
            double minZ = double.MaxValue;
            double maxZ = double.MinValue;

            foreach (Vec3d pt in pts)
            {
                if (pt.X < minX) { minX = pt.X; }
                if (pt.X > maxX) { maxX = pt.X; }
                if (pt.Z < minZ) { minZ = pt.Z; }
                if (pt.Z > maxZ) { maxZ = pt.Z; }
            }

            return new double[] { minX, maxX, minZ, maxZ };
        }

        // Трубка — сечение хордой с учётом смещения оси (X0, Y0)
        private static void AddTube(List<SketchRegion> outList, Part part)
        {
            double rMax = part.Shape.RMax;
            double rMin = part.Shape.RMin;
            double z0 = part.Shape.Z0;
            double z1 = part.Shape.Z1;
            double cx = part.Shape.X0;
            double cy = part.Shape.Y0;

            if (Math.Abs(cy) >= rMax)
            {
                return;
            }

            double halfOuter = Math.Sqrt(Math.Max(0.0, rMax * rMax - cy * cy));

            if (rMin <= 0.0)
            {
                outList.Add(MakeRect(part, cx - halfOuter, cx + halfOuter, z0, z1));
            }
            else
            {
                double halfInner = Math.Sqrt(Math.Max(0.0, rMin * rMin - cy * cy));
                if (halfInner < 1e-9)
                {
                    outList.Add(MakeRect(part, cx - halfOuter, cx + halfOuter, z0, z1));
                }
                else
                {
                    outList.Add(MakeRect(part, cx - halfOuter, cx - halfInner, z0, z1));
                    outList.Add(MakeRect(part, cx + halfInner, cx + halfOuter, z0, z1));
                }
            }
        }

        // Поликонус — сечение с учётом смещения оси (X0, Y0)
        private static void AddPolycone(List<SketchRegion> outList, Part part)
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
            outList.Add(op);

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
                outList.Add(ip);
            }
        }

        // Брусок — центр в (X0, Y0, Z0), размер BoxX×BoxY×BoxZ
        private static void AddBox(List<SketchRegion> outList, Part part)
        {
            double bx = part.Shape.BoxX;
            double by = part.Shape.BoxY;
            double bz = part.Shape.BoxZ;
            double cx = part.Shape.X0;
            double cy = part.Shape.Y0;
            double cz = part.Shape.Z0;

            if (Math.Abs(cy) > by / 2.0)
            {
                return;
            }

            double halfW = bx / 2.0;
            double halfH = bz / 2.0;

            outList.Add(MakeRect(part, cx - halfW, cx + halfW, cz - halfH, cz + halfH));
        }

        // Tessellated — сечение треугольников плоскостью y = sectionY (0 при наличии кристалла)
        private static void AddTessellated(List<SketchRegion> outList, Part part, double sectionY)
        {
            Mesh mesh = part.Mesh;
            if (mesh == null || mesh.TriangleCount == 0)
            {
                return;
            }

            List<double[]> segs = new List<double[]>();

            for (int i = 0; i < mesh.TriangleCount; i++)
            {
                Vec3d a, b, c;
                mesh.GetTriangle(i, out a, out b, out c);

                // Сдвиг: плоскость реза y = sectionY приводится к y = 0
                a = new Vec3d(a.X, a.Y - sectionY, a.Z);
                b = new Vec3d(b.X, b.Y - sectionY, b.Z);
                c = new Vec3d(c.X, c.Y - sectionY, c.Z);

                double[] pa = CrossY(a, b);
                double[] pb = CrossY(b, c);
                double[] pc = CrossY(c, a);

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

            List<SketchRegion> chains = ChainSegs(segs, part);
            outList.AddRange(chains);
        }

        // Пересечение отрезка с y = 0 (сдвинутые точки); null если нет
        private static double[] CrossY(Vec3d a, Vec3d b)
        {
            if (Math.Abs(a.Y) < 1e-12 && Math.Abs(b.Y) < 1e-12)
            {
                return null;
            }
            if (a.Y * b.Y > 0.0)
            {
                return null;
            }

            double t = a.Y / (a.Y - b.Y);
            if (t < -1e-9 || t > 1.0 + 1e-9)
            {
                return null;
            }
            if (t < 0.0) { t = 0.0; }
            if (t > 1.0) { t = 1.0; }

            double x = a.X + t * (b.X - a.X);
            double z = a.Z + t * (b.Z - a.Z);
            return new double[] { x, z };
        }

        // Хеш ячейки пространственной сетки
        private static long CellKeyHash(long cx, long cz)
        {
            return (cx * 73856093L) ^ (cz * 19349663L);
        }

        // Ключ ячейки из координат
        private static long CellKey(double x, double z)
        {
            long cx = (long)Math.Floor(x / JoinTol);
            long cz = (long)Math.Floor(z / JoinTol);
            return CellKeyHash(cx, cz);
        }

        // Сшивка отрезков в полилинии (O(n) через пространственный хэш)
        private static List<SketchRegion> ChainSegs(List<double[]> segs, Part part)
        {
            int n = segs.Count;
            bool[] used = new bool[n];
            List<SketchRegion> result = new List<SketchRegion>();

            // Пространственный хэш: cell → список индексов сегментов
            Dictionary<long, List<int>> grid = new Dictionary<long, List<int>>();
            for (int i = 0; i < n; i++)
            {
                double[] s = segs[i];
                long k1 = CellKey(s[0], s[1]);
                long k2 = CellKey(s[2], s[3]);
                List<int> l1;
                if (!grid.TryGetValue(k1, out l1))
                {
                    l1 = new List<int>();
                    grid[k1] = l1;
                }
                l1.Add(i);
                List<int> l2;
                if (k1 != k2)
                {
                    if (!grid.TryGetValue(k2, out l2))
                    {
                        l2 = new List<int>();
                        grid[k2] = l2;
                    }
                    l2.Add(i);
                }
            }

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

                // Рост вперёд от последней точки
                double lx = s[2], lz = s[3];
                bool grew = true;
                while (grew)
                {
                    grew = false;
                    long cx = (long)Math.Floor(lx / JoinTol);
                    long cz = (long)Math.Floor(lz / JoinTol);
                    for (long ox = -1; ox <= 1 && !grew; ox++)
                    {
                        for (long oz = -1; oz <= 1 && !grew; oz++)
                        {
                            long key = CellKeyHash(cx + ox, cz + oz);
                            List<int> cellList;
                            if (!grid.TryGetValue(key, out cellList))
                            {
                                continue;
                            }
                            for (int j = 0; j < cellList.Count; j++)
                            {
                                int sj = cellList[j];
                                if (used[sj])
                                {
                                    continue;
                                }
                                double[] seg = segs[sj];
                                if (Math.Abs(seg[0] - lx) < JoinTol && Math.Abs(seg[1] - lz) < JoinTol)
                                {
                                    pts.Add(new Vec3d(seg[2], 0.0, seg[3]));
                                    used[sj] = true;
                                    lx = seg[2]; lz = seg[3];
                                    grew = true;
                                    break;
                                }
                                if (Math.Abs(seg[2] - lx) < JoinTol && Math.Abs(seg[3] - lz) < JoinTol)
                                {
                                    pts.Add(new Vec3d(seg[0], 0.0, seg[1]));
                                    used[sj] = true;
                                    lx = seg[0]; lz = seg[1];
                                    grew = true;
                                    break;
                                }
                            }
                        }
                    }
                }

                // Рост назад от первой точки (сбор в отдельный список)
                List<Vec3d> back = new List<Vec3d>();
                double fx = s[0], fz = s[1];
                grew = true;
                while (grew)
                {
                    grew = false;
                    long cx = (long)Math.Floor(fx / JoinTol);
                    long cz = (long)Math.Floor(fz / JoinTol);
                    for (long ox = -1; ox <= 1 && !grew; ox++)
                    {
                        for (long oz = -1; oz <= 1 && !grew; oz++)
                        {
                            long key = CellKeyHash(cx + ox, cz + oz);
                            List<int> cellList;
                            if (!grid.TryGetValue(key, out cellList))
                            {
                                continue;
                            }
                            for (int j = 0; j < cellList.Count; j++)
                            {
                                int sj = cellList[j];
                                if (used[sj])
                                {
                                    continue;
                                }
                                double[] seg = segs[sj];
                                if (Math.Abs(seg[0] - fx) < JoinTol && Math.Abs(seg[1] - fz) < JoinTol)
                                {
                                    back.Add(new Vec3d(seg[2], 0.0, seg[3]));
                                    used[sj] = true;
                                    fx = seg[2]; fz = seg[3];
                                    grew = true;
                                    break;
                                }
                                if (Math.Abs(seg[2] - fx) < JoinTol && Math.Abs(seg[3] - fz) < JoinTol)
                                {
                                    back.Add(new Vec3d(seg[0], 0.0, seg[1]));
                                    used[sj] = true;
                                    fx = seg[0]; fz = seg[1];
                                    grew = true;
                                    break;
                                }
                            }
                        }
                    }
                }

                // Объединение: обратный back + pts
                if (back.Count > 0)
                {
                    List<Vec3d> merged = new List<Vec3d>(back.Count + pts.Count);
                    for (int k = back.Count - 1; k >= 0; k--)
                    {
                        merged.Add(back[k]);
                    }
                    merged.AddRange(pts);
                    pts = merged;
                }

                if (pts.Count >= 2)
                {
                    Vec3d first = pts[0];
                    Vec3d last = pts[pts.Count - 1];
                    double dx = first.X - last.X;
                    double dz = first.Z - last.Z;
                    bool closed = (dx * dx + dz * dz) < (JoinTol * JoinTol);

                    SketchRegion region = new SketchRegion();
                    region.Points = pts;
                    region.IsClosed = closed;
                    region.Role = part.Role;
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
            r.Points = new List<Vec3d>
            {
                new Vec3d(x0, 0.0, z0),
                new Vec3d(x1, 0.0, z0),
                new Vec3d(x1, 0.0, z1),
                new Vec3d(x0, 0.0, z1)
            };
            return r;
        }
    }
}
