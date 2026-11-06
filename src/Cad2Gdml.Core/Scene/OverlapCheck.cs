using System;
using System.Collections.Generic;
using System.Globalization;
using Cad2Gdml.Geometry;
using Cad2Gdml.Meshes;

namespace Cad2Gdml.Scene
{
    public static class OverlapCheck
    {
        private const double Tol = 1e-6;
        private const double DenomEps = 1e-12;
        private const int MaxPen = 1000;
        private const int MaxPairs = 5000;
        private const int SampleN = 5;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private struct PartData
        {
            public Part P;
            public Vec3d Min, Max;
            public float[] Boxes; // 6 float на треугольник
        }

        public static void Run(SceneModel scene)
        {
            if (scene.Parts == null) return;
            var data = new List<PartData>();
            for (int i = 0; i < scene.Parts.Count; i++)
            {
                var p = scene.Parts[i];
                if (p.Mesh == null || p.Mesh.TriangleCount <= 0 || p.MeshOpen) continue;
                data.Add(BuildData(p));
            }
            int n = data.Count;
            if (n < 2) return;

            // Размах сцены: максимальный размер объединённого бокса
            double ext = 1e-9;
            for (int i = 0; i < n; i++)
            {
                ext = Math.Max(ext, data[i].Max.X - data[i].Min.X);
                ext = Math.Max(ext, data[i].Max.Y - data[i].Min.Y);
                ext = Math.Max(ext, data[i].Max.Z - data[i].Min.Z);
            }
            double eps = 1e-6 * ext;

            // Счётчик пар с пересекающимися боксами
            int pairCount = 0;
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                    if (BoxOv(data[i].Min, data[i].Max, data[j].Min, data[j].Max, eps))
                        pairCount++;

            // Подробная проверка не более MaxPairs пар
            int checkedN = 0;
            for (int i = 0; i < n && checkedN < MaxPairs; i++)
                for (int j = i + 1; j < n && checkedN < MaxPairs; j++)
                {
                    if (!BoxOv(data[i].Min, data[i].Max, data[j].Min, data[j].Max, eps)) continue;
                    checkedN++;
                    CheckPair(scene, data[i], data[j], eps);
                }

            if (pairCount > MaxPairs)
                scene.Add("W_OVERLAP_LIMIT", DiagSeverity.Warning, "",
                    "Проверено " + MaxPairs.ToString(Inv) + " пар из " + pairCount.ToString(Inv) + ": остальные пропущены.");
        }

        private static PartData BuildData(Part p)
        {
            var d = new PartData { P = p };
            p.Mesh.GetBounds(out d.Min, out d.Max);
            int tc = p.Mesh.TriangleCount;
            d.Boxes = new float[tc * 6];
            for (int i = 0; i < tc; i++)
            {
                p.Mesh.GetTriangle(i, out var a, out var b, out var c);
                int o = i * 6;
                d.Boxes[o] = (float)Math.Min(a.X, Math.Min(b.X, c.X));
                d.Boxes[o + 1] = (float)Math.Min(a.Y, Math.Min(b.Y, c.Y));
                d.Boxes[o + 2] = (float)Math.Min(a.Z, Math.Min(b.Z, c.Z));
                d.Boxes[o + 3] = (float)Math.Max(a.X, Math.Max(b.X, c.X));
                d.Boxes[o + 4] = (float)Math.Max(a.Y, Math.Max(b.Y, c.Y));
                d.Boxes[o + 5] = (float)Math.Max(a.Z, Math.Max(b.Z, c.Z));
            }
            return d;
        }

        private static bool BoxOv(Vec3d m1, Vec3d M1, Vec3d m2, Vec3d M2, double eps)
        {
            return m1.X <= M2.X + eps && m2.X <= M1.X + eps &&
                   m1.Y <= M2.Y + eps && m2.Y <= M1.Y + eps &&
                   m1.Z <= M2.Z + eps && m2.Z <= M1.Z + eps;
        }

        private static bool TriBoxOv(float[] b1, int i, float[] b2, int j, double eps)
        {
            int o1 = i * 6, o2 = j * 6;
            return b1[o1] <= b2[o2 + 3] + eps && b2[o2] <= b1[o1 + 3] + eps &&
                   b1[o1 + 1] <= b2[o2 + 4] + eps && b2[o2 + 1] <= b1[o1 + 4] + eps &&
                   b1[o1 + 2] <= b2[o2 + 5] + eps && b2[o2 + 2] <= b1[o1 + 5] + eps;
        }

        private static void CheckPair(SceneModel scene, PartData A, PartData B, double eps)
        {
            int pen = 0;
            int tcA = A.P.Mesh.TriangleCount, tcB = B.P.Mesh.TriangleCount;

            // Рёбра A против треугольников B
            for (int ia = 0; ia < tcA && pen < MaxPen; ia++)
            {
                A.P.Mesh.GetTriangle(ia, out var a0, out var a1, out var a2);
                for (int e = 0; e < 3 && pen < MaxPen; e++)
                {
                    Vec3d eP, eQ;
                    if (e == 0) { eP = a0; eQ = a1; }
                    else if (e == 1) { eP = a1; eQ = a2; }
                    else { eP = a2; eQ = a0; }
                    for (int ib = 0; ib < tcB && pen < MaxPen; ib++)
                    {
                        if (!TriBoxOv(A.Boxes, ia, B.Boxes, ib, eps)) continue;
                        B.P.Mesh.GetTriangle(ib, out var b0, out var b1, out var b2);
                        if (SegTri(eP, eQ, b0, b1, b2)) pen++;
                    }
                }
            }

            // Рёбра B против треугольников A
            for (int ib = 0; ib < tcB && pen < MaxPen; ib++)
            {
                B.P.Mesh.GetTriangle(ib, out var b0, out var b1, out var b2);
                for (int e = 0; e < 3 && pen < MaxPen; e++)
                {
                    Vec3d eP, eQ;
                    if (e == 0) { eP = b0; eQ = b1; }
                    else if (e == 1) { eP = b1; eQ = b2; }
                    else { eP = b2; eQ = b0; }
                    for (int ia = 0; ia < tcA && pen < MaxPen; ia++)
                    {
                        if (!TriBoxOv(B.Boxes, ib, A.Boxes, ia, eps)) continue;
                        A.P.Mesh.GetTriangle(ia, out var a0, out var a1, out var a2);
                        if (SegTri(eP, eQ, a0, a1, a2)) pen++;
                    }
                }
            }

            if (pen > 0)
            {
                bool limit = pen >= MaxPen;
                string msg = "Поверхности пересекаются: найдено протыканий " + pen.ToString(Inv)
                    + (limit ? "+" : "") +
                    ". Проверьте положение деталей (в GDML плоской структуры это перекрытие объёмов).";
                scene.Add("W_OVERLAP", DiagSeverity.Warning, A.P.Name + " × " + B.P.Name, msg);
            }
            else
            {
                CheckNested(scene, A, B);
            }
        }

        private static void CheckNested(SceneModel scene, PartData A, PartData B)
        {
            string msgTpl = "Деталь целиком внутри тела «{0}»: в GDML плоской структуры это перекрытие объёмов (полость должна быть вырезана).";
            if (IsInside(B, A))
            {
                scene.Add("W_INSIDE", DiagSeverity.Warning, B.P.Name,
                    string.Format(Inv, msgTpl, A.P.Name));
                return;
            }
            if (IsInside(A, B))
            {
                scene.Add("W_INSIDE", DiagSeverity.Warning, A.P.Name,
                    string.Format(Inv, msgTpl, B.P.Name));
            }
        }

        // Точки на поверхности target, чётность пересечений с container
        private static bool IsInside(PartData target, PartData container)
        {
            int tc = target.P.Mesh.TriangleCount;
            var dir = new Vec3d(1.0, 0.1234567, 0.0456789);
            int tcC = container.P.Mesh.TriangleCount;
            int defined = 0;
            bool allIn = true;

            for (int s = 0; s < SampleN; s++)
            {
                int idx = (int)((double)s / SampleN * tc);
                if (idx >= tc) idx = tc - 1;
                target.P.Mesh.GetTriangle(idx, out var a, out var b, out var c);
                var pt = new Vec3d((a.X + b.X + c.X) / 3.0, (a.Y + b.Y + c.Y) / 3.0, (a.Z + b.Z + c.Z) / 3.0);
                int hits = 0;
                bool undefined = false;
                for (int i = 0; i < tcC && !undefined; i++)
                {
                    container.P.Mesh.GetTriangle(i, out var ca, out var cb, out var cc);
                    if (RayTri(pt, dir, ca, cb, cc, out double t, out double u, out double v))
                    {
                        if (u <= 1e-9 || v <= 1e-9 || (1.0 - u - v) <= 1e-9)
                        { undefined = true; break; }
                        hits++;
                    }
                }
                if (undefined) continue;
                defined++;
                if (hits % 2 == 0) { allIn = false; break; }
            }
            return defined >= 1 && allIn;
        }

        // Мёллер—Трумбор: отрезок P→Q против треугольника a,b,c
        private static bool SegTri(Vec3d P, Vec3d Q, Vec3d a, Vec3d b, Vec3d c)
        {
            double Dx = Q.X - P.X, Dy = Q.Y - P.Y, Dz = Q.Z - P.Z;
            double e1x = b.X - a.X, e1y = b.Y - a.Y, e1z = b.Z - a.Z;
            double e2x = c.X - a.X, e2y = c.Y - a.Y, e2z = c.Z - a.Z;
            double hx = Dy * e2z - Dz * e2y, hy = Dz * e2x - Dx * e2z, hz = Dx * e2y - Dy * e2x;
            double det = e1x * hx + e1y * hy + e1z * hz;
            if (Math.Abs(det) < DenomEps) return false;
            double inv = 1.0 / det;
            double sx = P.X - a.X, sy = P.Y - a.Y, sz = P.Z - a.Z;
            double u = inv * (sx * hx + sy * hy + sz * hz);
            if (u < Tol || u > 1.0 - Tol) return false;
            double qx = sy * e1z - sz * e1y, qy = sz * e1x - sx * e1z, qz = sx * e1y - sy * e1x;
            double v = inv * (Dx * qx + Dy * qy + Dz * qz);
            if (v < Tol || u + v > 1.0 - Tol) return false;
            double t = inv * (e2x * qx + e2y * qy + e2z * qz);
            return t > Tol && t < 1.0 - Tol;
        }

        // Мёллер—Трумбор: луч O+D против треугольника a,b,c
        private static bool RayTri(Vec3d O, Vec3d D, Vec3d a, Vec3d b, Vec3d c,
            out double t, out double u, out double v)
        {
            t = 0; u = 0; v = 0;
            double e1x = b.X - a.X, e1y = b.Y - a.Y, e1z = b.Z - a.Z;
            double e2x = c.X - a.X, e2y = c.Y - a.Y, e2z = c.Z - a.Z;
            double hx = D.Y * e2z - D.Z * e2y, hy = D.Z * e2x - D.X * e2z, hz = D.X * e2y - D.Y * e2x;
            double det = e1x * hx + e1y * hy + e1z * hz;
            if (Math.Abs(det) < DenomEps) return false;
            double inv = 1.0 / det;
            double sx = O.X - a.X, sy = O.Y - a.Y, sz = O.Z - a.Z;
            u = inv * (sx * hx + sy * hy + sz * hz);
            if (u < 0 || u > 1) return false;
            double qx = sy * e1z - sz * e1y, qy = sz * e1x - sx * e1z, qz = sx * e1y - sy * e1x;
            v = inv * (D.X * qx + D.Y * qy + D.Z * qz);
            if (v < 0 || u + v > 1) return false;
            t = inv * (e2x * qx + e2y * qy + e2z * qz);
            return t > 1e-9;
        }
    }
}
