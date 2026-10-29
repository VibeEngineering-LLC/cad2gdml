using System;
using System.Collections.Generic;
using System.Globalization;
using Cad2Gdml.Geometry;
using Cad2Gdml.Meshes;
using Cad2Gdml.Scene;

namespace Cad2Gdml.Recognize
{
    // Параметры распознавания тел вращения
    public sealed class RecognizeOptions
    {
        public double RevTolMm = 0.01;
        public int RevMinSegments = 24;
    }

    // Распознавание тел вращения по сетке (v0.1)
    // Ось ∥ z, позиция = центр bbox по X,Y;
    // вершины → (r,z); уровни по z с шагом RevTolMm;
    // кольца: разрыв радиусов > 10*RevTolMm;
    // проверки: n_min, прилежание к профилю, объём (Папп).
    public static class RevolutionRecognizer
    {
        public static SolidShape TryRecognize(Part part, RecognizeOptions opt, out string whyNot)
        {
            whyNot = null;

            if (part == null || part.Mesh == null || part.Mesh.Vertices.Count < 3)
            {
                whyNot = "I_REV_EMPTY";
                return null;
            }

            double tol = opt.RevTolMm;

            // Позиция оси: центр bounding box
            Vec3d bMin, bMax;
            part.Mesh.GetBounds(out bMin, out bMax);
            double x0 = (bMin.X + bMax.X) / 2.0;
            double y0 = (bMin.Y + bMax.Y) / 2.0;

            int nv = part.Mesh.Vertices.Count;
            double[] rAll = new double[nv];
            double[] zAll = new double[nv];
            for (int i = 0; i < nv; i++)
            {
                Vec3d v = part.Mesh.Vertices[i];
                double dx = v.X - x0;
                double dy = v.Y - y0;
                rAll[i] = Math.Sqrt(dx * dx + dy * dy);
                zAll[i] = v.Z;
            }

            // Группировка по z-уровням (округление к кратному tol)
            Dictionary<long, List<int>> groups = new Dictionary<long, List<int>>();
            for (int i = 0; i < nv; i++)
            {
                long key = (long)Math.Round(zAll[i] / tol, MidpointRounding.AwayFromZero);
                if (!groups.ContainsKey(key))
                {
                    groups[key] = new List<int>();
                }
                groups[key].Add(i);
            }

            List<long> sortedKeys = new List<long>(groups.Keys);
            sortedKeys.Sort();
            int nLevels = sortedKeys.Count;

            if (nLevels < 2)
            {
                whyNot = "I_REV_FLAT";
                return null;
            }

            // n_min — наименьшее число вершин на уровне
            int nMin = int.MaxValue;
            for (int li = 0; li < nLevels; li++)
            {
                int cnt = groups[sortedKeys[li]].Count;
                if (cnt < nMin)
                {
                    nMin = cnt;
                }
            }

            if (nMin < opt.RevMinSegments)
            {
                whyNot = "I_REV_COARSE";
                return null;
            }

            // Радиусы колец и z каждого уровня
            double[] lRIn = new double[nLevels];
            double[] lROut = new double[nLevels];
            double[] lZ = new double[nLevels];
            double gapTh = 10.0 * tol;

            for (int li = 0; li < nLevels; li++)
            {
                List<int> verts = groups[sortedKeys[li]];

                // Средний z уровня
                double zSum = 0.0;
                for (int j = 0; j < verts.Count; j++)
                {
                    zSum += zAll[verts[j]];
                }
                lZ[li] = zSum / verts.Count;

                // Сортировка радиусов вершин уровня
                double[] lv = new double[verts.Count];
                for (int j = 0; j < verts.Count; j++)
                {
                    lv[j] = rAll[verts[j]];
                }
                Array.Sort(lv);

                // Разделение на максимум два кластера по разрыву > gapTh
                int splitAt = -1;
                for (int j = 1; j < lv.Length; j++)
                {
                    if (lv[j] - lv[j - 1] > gapTh)
                    {
                        splitAt = j;
                        break;
                    }
                }

                if (splitAt > 0)
                {
                    // Проверяем: нет второго разрыва (не более двух колец)
                    for (int j = splitAt + 1; j < lv.Length; j++)
                    {
                        if (lv[j] - lv[j - 1] > gapTh)
                        {
                            whyNot = "I_REV_MULTI_RING";
                            return null;
                        }
                    }
                    lRIn[li] = lv[splitAt - 1];
                    lROut[li] = lv[lv.Length - 1];
                }
                else
                {
                    lRIn[li] = 0.0;
                    lROut[li] = lv[lv.Length - 1];
                }

                // Профиль должен быть корректным: rIn < rOut
                if (lROut[li] - lRIn[li] < tol)
                {
                    whyNot = "I_REV_DEGENERATE";
                    return null;
                }
            }

            // Проверка (a): каждая вершина на своём кольце
            Dictionary<long, int> keyToLevel = new Dictionary<long, int>();
            for (int li = 0; li < nLevels; li++)
            {
                keyToLevel[sortedKeys[li]] = li;
            }

            for (int i = 0; i < nv; i++)
            {
                long key = (long)Math.Round(zAll[i] / tol, MidpointRounding.AwayFromZero);
                int li;
                if (!keyToLevel.TryGetValue(key, out li))
                {
                    whyNot = "I_REV_OFFPROFILE";
                    return null;
                }

                double r = rAll[i];
                bool onRing = (r <= tol); // вершины на оси (центры веерных торцов)
                if (lRIn[li] > 0.0)
                {
                    if (Math.Abs(r - lRIn[li]) <= tol)
                    {
                        onRing = true;
                    }
                }
                if (!onRing)
                {
                    if (Math.Abs(r - lROut[li]) <= tol)
                    {
                        onRing = true;
                    }
                }

                if (!onRing)
                {
                    whyNot = "I_REV_OFFPROFILE";
                    return null;
                }
            }

            // Объём сетки
            double meshVol = Math.Abs(MeshProperties.SignedVolume(part.Mesh));

            // Объём профиля по Паппу
            double profVol = ComputeProfileVolume(lZ, lRIn, lROut, nLevels);

            if (profVol <= 1e-9)
            {
                whyNot = "I_REV_NO_VOLUME";
                return null;
            }

            // Проверка (b): V_сетки ≤ V_проф и относительное расхождение
            if (meshVol > profVol * (1.0 + 1e-6))
            {
                whyNot = "I_REV_VOL_EXCEEDS";
                return null;
            }

            double twoPiN = 2.0 * Math.PI / nMin;
            double volTol = 1.5 * twoPiN * twoPiN / 6.0 + 1e-6;
            double relDiff = (profVol - meshVol) / profVol;
            if (relDiff > volTol)
            {
                whyNot = "I_REV_VOL_MISMATCH";
                return null;
            }

            // Формирование результата
            SolidShape shape = new SolidShape();
            shape.X0 = x0;
            shape.Y0 = y0;

            // Два уровня с постоянными радиусами → tube
            if (nLevels == 2)
            {
                bool inConst = (lRIn[0] <= tol && lRIn[1] <= tol) ||
                               Math.Abs(lRIn[0] - lRIn[1]) <= tol;
                bool outConst = Math.Abs(lROut[0] - lROut[1]) <= tol;

                if (inConst && outConst)
                {
                    shape.Kind = SolidKind.Tube;
                    shape.RMin = (lRIn[0] <= tol) ? 0.0 : lRIn[0];
                    shape.RMax = lROut[0];
                    shape.Z0 = lZ[0];
                    shape.Z1 = lZ[1];
                    return shape;
                }
            }

            // Несколько уровней → polycone (каждый интервал — фрустус)
            shape.Kind = SolidKind.Polycone;
            shape.ZPlane = new List<double>();
            shape.RInner = new List<double>();
            shape.ROuter = new List<double>();
            for (int li = 0; li < nLevels - 1; li++)
            {
                shape.ZPlane.Add(lZ[li]);
                shape.ZPlane.Add(lZ[li + 1]);
                shape.RInner.Add(lRIn[li]);
                shape.RInner.Add(lRIn[li + 1]);
                shape.ROuter.Add(lROut[li]);
                shape.ROuter.Add(lROut[li + 1]);
            }
            return shape;
        }


        // Объём тела вращения: V = π ∮ r² dz по контуру профиля
        // Контур: наружная кромка (rOut) по возрастанию z,
        //         внутренняя кромка (rIn) по убыванию z
        private static double ComputeProfileVolume(
            double[] z, double[] rIn, double[] rOut, int n)
        {
            int pc = 2 * n;
            double[] pr = new double[pc];
            double[] pz = new double[pc];

            for (int i = 0; i < n; i++)
            {
                pr[i] = rOut[i];
                pz[i] = z[i];
            }
            for (int i = 0; i < n; i++)
            {
                pr[n + i] = rIn[n - 1 - i];
                pz[n + i] = z[n - 1 - i];
            }

            // По Паппу: V = π Σ (Δz_i)·(r_i² + r_i·r_j + r_j²)/3
            double sum = 0.0;
            for (int i = 0; i < pc; i++)
            {
                int j = (i + 1) % pc;
                double dz = pz[j] - pz[i];
                double ri = pr[i];
                double rj = pr[j];
                sum += dz * (ri * ri + ri * rj + rj * rj) / 3.0;
            }
            return Math.PI * sum;
        }
    }
}
