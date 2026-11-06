using System;
using System.Collections.Generic;
using Cad2Gdml.Geometry;
using Cad2Gdml.Meshes;

namespace Cad2Gdml.Import
{
    internal static class GdmlMeshing
    {
        internal static Mesh BuildBox(double x, double y, double z)
        {
            var m = new Mesh();
            double hx = x / 2, hy = y / 2, hz = z / 2;
            var v = new[]
            {
                new Vec3d(-hx,-hy,-hz), new Vec3d(hx,-hy,-hz),
                new Vec3d(hx,hy,-hz),  new Vec3d(-hx,hy,-hz),
                new Vec3d(-hx,-hy,hz), new Vec3d(hx,-hy,hz),
                new Vec3d(hx,hy,hz),   new Vec3d(-hx,hy,hz)
            };
            m.AddTriangle(v[0],v[3],v[2]); m.AddTriangle(v[0],v[2],v[1]); // дно z-
            m.AddTriangle(v[4],v[5],v[6]); m.AddTriangle(v[4],v[6],v[7]); // верх z+
            m.AddTriangle(v[0],v[1],v[5]); m.AddTriangle(v[0],v[5],v[4]); // перед y-
            m.AddTriangle(v[2],v[3],v[7]); m.AddTriangle(v[2],v[7],v[6]); // зад y+
            m.AddTriangle(v[3],v[0],v[4]); m.AddTriangle(v[3],v[4],v[7]); // лево x-
            m.AddTriangle(v[1],v[2],v[6]); m.AddTriangle(v[1],v[6],v[5]); // право x+
            return m;
        }

        internal static Mesh BuildTube(double rmin, double rmax, double z, double startPhi, double deltaPhi)
        {
            double hz = z / 2;
            var profile = new List<double[]>
            {
                new[] { rmin, -hz }, new[] { rmax, -hz },
                new[] { rmax,  hz }, new[] { rmin,  hz }
            };
            return BuildRevolved(profile, startPhi, deltaPhi);
        }

        internal static Mesh BuildPolycone(double startPhi, double deltaPhi, IList<double[]> planes)
        {
            int n = planes.Count;
            var profile = new List<double[]>();
            for (int i = 0; i < n; i++) profile.Add(new[] { planes[i][2], planes[i][0] });
            for (int i = n - 1; i >= 0; i--) profile.Add(new[] { planes[i][1], planes[i][0] });
            return BuildRevolved(profile, startPhi, deltaPhi);
        }

        internal static Mesh BuildRevolved(List<double[]> profile, double startPhi, double deltaPhi)
        {
            // a) Убираем подряд идущие дубликаты и последнюю точку, если = первой
            var pts = new List<double[]>();
            foreach (var p in profile)
            {
                if (pts.Count > 0 && Math.Abs(pts[pts.Count - 1][0] - p[0]) < 1e-9
                    && Math.Abs(pts[pts.Count - 1][1] - p[1]) < 1e-9) continue;
                pts.Add(p);
            }
            if (pts.Count > 1 && Math.Abs(pts[pts.Count - 1][0] - pts[0][0]) < 1e-9
                && Math.Abs(pts[pts.Count - 1][1] - pts[0][1]) < 1e-9)
                pts.RemoveAt(pts.Count - 1);
            if (pts.Count < 3) return new Mesh();

            // b) Площадь со знаком (шнурок); при отрицательной — разворот
            double area = 0;
            for (int i = 0; i < pts.Count; i++)
            {
                int j = (i + 1) % pts.Count;
                area += pts[i][0] * pts[j][1] - pts[j][0] * pts[i][1];
            }
            if (area < 0) pts.Reverse();

            // c) Число угловых сегментов и массивы вершин
            int n = Math.Max(3, (int)Math.Ceiling(48.0 * deltaPhi / (2.0 * Math.PI)));
            bool full = deltaPhi >= 2.0 * Math.PI - 1e-9;
            var verts = new Vec3d[pts.Count][];
            for (int i = 0; i < pts.Count; i++)
            {
                verts[i] = new Vec3d[n + 1];
                for (int k = 0; k < n; k++)
                {
                    double a = startPhi + k * deltaPhi / n;
                    verts[i][k] = new Vec3d(pts[i][0] * Math.Cos(a), pts[i][0] * Math.Sin(a), pts[i][1]);
                }
                if (full) verts[i][n] = verts[i][0]; // тот же объект
                else
                {
                    double a = startPhi + deltaPhi;
                    verts[i][n] = new Vec3d(pts[i][0] * Math.Cos(a), pts[i][0] * Math.Sin(a), pts[i][1]);
                }
            }

            // d) Боковая поверхность: треугольники по рёбрам профиля
            var tris = new List<Vec3d[]>();
            for (int e = 0; e < pts.Count; e++)
            {
                int e0 = e, e1 = (e + 1) % pts.Count;
                for (int k = 0; k < n; k++)
                {
                    var A0 = verts[e0][k]; var A1 = verts[e0][k + 1];
                    var B0 = verts[e1][k]; var B1 = verts[e1][k + 1];
                    if (TriArea(A0, A1, B1) >= 1e-9) tris.Add(new[] { A0, A1, B1 });
                    if (TriArea(A0, B1, B0) >= 1e-9) tris.Add(new[] { A0, B1, B0 });
                }
            }

            // Проверка: нормаль на внешней стенке наружу
            if (tris.Count > 0)
            {
                var t = tris[0];
                var nrm = Cross(Sub(t[1], t[0]), Sub(t[2], t[0]));
                double cx = (t[0].X + t[1].X + t[2].X) / 3.0;
                double cy = (t[0].Y + t[1].Y + t[2].Y) / 3.0;
                double rr = Math.Sqrt(cx * cx + cy * cy);
                if (rr > 1e-9 && nrm.X * cx + nrm.Y * cy < 0)
                    for (int i = 0; i < tris.Count; i++)
                        tris[i] = new[] { tris[i][0], tris[i][2], tris[i][1] };
            }

            var mesh = new Mesh();
            foreach (var t in tris) mesh.AddTriangle(t[0], t[1], t[2]);

            // e) Боковые плоскости при неполном обороте
            if (!full)
            {
                var ears = EarClip(pts);
                foreach (var tri in ears)
                    mesh.AddTriangle(verts[tri[0]][0], verts[tri[1]][0], verts[tri[2]][0]);
                foreach (var tri in ears)
                    mesh.AddTriangle(verts[tri[0]][n], verts[tri[2]][n], verts[tri[1]][n]);
            }
            return mesh;
        }

        private static double TriArea(Vec3d a, Vec3d b, Vec3d c)
        {
            var n = Cross(Sub(b, a), Sub(c, a));
            return Math.Sqrt(n.X * n.X + n.Y * n.Y + n.Z * n.Z);
        }

        private static Vec3d Sub(Vec3d a, Vec3d b) => new Vec3d(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        private static Vec3d Cross(Vec3d a, Vec3d b) => new Vec3d(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

        // Отсечение ушей для CCW-полигона в плоскости (r,z)
        private static List<int[]> EarClip(List<double[]> poly)
        {
            int cnt = poly.Count;
            var idx = new List<int>();
            for (int i = 0; i < cnt; i++) idx.Add(i);
            var res = new List<int[]>();
            while (idx.Count > 3)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count && !clipped; i++)
                {
                    int ip = (i - 1 + idx.Count) % idx.Count;
                    int inxt = (i + 1) % idx.Count;
                    var P = poly[idx[ip]]; var C = poly[idx[i]]; var N = poly[idx[inxt]];
                    double cr = (C[0] - P[0]) * (N[1] - P[1]) - (C[1] - P[1]) * (N[0] - P[0]);
                    if (cr <= 1e-12) continue;
                    bool bad = false;
                    for (int j = 0; j < idx.Count && !bad; j++)
                    {
                        if (j == ip || j == i || j == inxt) continue;
                        if (PtInTri(poly[idx[j]], P, C, N)) bad = true;
                    }
                    if (bad) continue;
                    res.Add(new[] { idx[ip], idx[i], idx[inxt] });
                    idx.RemoveAt(i);
                    clipped = true;
                }
                if (!clipped) break;
            }
            if (idx.Count == 3) res.Add(new[] { idx[0], idx[1], idx[2] });
            return res;
        }

        private static bool PtInTri(double[] p, double[] a, double[] b, double[] c)
        {
            double d1 = (p[0] - b[0]) * (a[1] - b[1]) - (a[0] - b[0]) * (p[1] - b[1]);
            double d2 = (p[0] - c[0]) * (b[1] - c[1]) - (b[0] - c[0]) * (p[1] - c[1]);
            double d3 = (p[0] - a[0]) * (c[1] - a[1]) - (c[0] - a[0]) * (p[1] - a[1]);
            bool neg = d1 < -1e-12 || d2 < -1e-12 || d3 < -1e-12;
            bool pos = d1 > 1e-12 || d2 > 1e-12 || d3 > 1e-12;
            return !(neg && pos);
        }
    }
}
