using System;
using System.Collections.Generic;
using System.Globalization;
using Cad2Gdml.Geometry;
using Cad2Gdml.Meshes;

namespace Cad2Gdml.Scene
{
    // Параметры построения кадра
    public sealed class FrameOptions
    {
        public string FrontSign = "auto";
        public double SnapAxisMm = 0.01;
    }

    // Построитель кадра координат
    public static class FrameBuilder
    {
        // Порог относительной близости собственных значений
        private const double EigenTol = 1e-4;

        // Построить преобразование: сцена → кадр (ось z на кристалле, z=0 на переднем торце)
        public static Affine3 Build(SceneModel scene, Part crystal, FrameOptions opt)
        {
            // 1. Ось кристалла
            Vec3d axisDir;
            if (!TryDetermineAxis(scene, crystal, out axisDir))
            {
                throw new InvalidOperationException("E_AXIS_AMBIG: ось кристалла не определена.");
            }
            axisDir = axisDir.Normalized();

            // 2. Центр и полудлина
            Vec3d center = ComputeCentroid(crystal.Mesh);
            double halfLen = ComputeHalfExtent(crystal.Mesh, axisDir);

            Vec3d facePlus = center + axisDir * halfLen;
            Vec3d faceMinus = center - axisDir * halfLen;

            // 3. Передний торец (z = 0)
            Vec3d frontCenter;
            if (!TryDetermineFront(scene, crystal, opt, facePlus, faceMinus, out frontCenter))
            {
                throw new InvalidOperationException("E_FRONT_AMBIG: передний торец не определён.");
            }

            // 4. Базис кадра
            Vec3d zF = axisDir;
            Vec3d fileX = new Vec3d(1.0, 0.0, 0.0);
            Vec3d xProj = fileX - zF * (fileX.Dot(zF));
            if (xProj.LengthSq() < 1e-12)
            {
                Vec3d fileY = new Vec3d(0.0, 1.0, 0.0);
                xProj = fileY - zF * (fileY.Dot(zF));
            }
            Vec3d xF = xProj.Normalized();
            Vec3d yF = zF.Cross(xF);

            // 5. Аффинное преобразование: p_frame = R*(p_file - frontCenter)
            double tx = -(xF.X * frontCenter.X + xF.Y * frontCenter.Y + xF.Z * frontCenter.Z);
            double ty = -(yF.X * frontCenter.X + yF.Y * frontCenter.Y + yF.Z * frontCenter.Z);
            double tz = -(zF.X * frontCenter.X + zF.Y * frontCenter.Y + zF.Z * frontCenter.Z);

            return new Affine3(
                xF.X, xF.Y, xF.Z,
                yF.X, yF.Y, yF.Z,
                zF.X, zF.Y, zF.Z,
                new Vec3d(tx, ty, tz));
        }

        // Применить преобразование ко всем деталям сцены
        public static void Apply(SceneModel scene, Affine3 t)
        {
            for (int i = 0; i < scene.Parts.Count; i++)
            {
                Part p = scene.Parts[i];
                if (p.Mesh != null)
                {
                    p.Mesh = p.Mesh.Transformed(t);
                }
            }
        }

        // Определить ось кристалла через тензор инерции или bbox
        private static bool TryDetermineAxis(SceneModel scene, Part crystal, out Vec3d axis)
        {
            axis = Vec3d.Zero;

            if (crystal.Mesh == null || crystal.Mesh.Vertices.Count < 3)
            {
                scene.Add("E_AXIS_AMBIG", DiagSeverity.Error, crystal.Name,
                    "Сетка пуста — ось не определена.");
                return false;
            }

            double[,] tensor = MeshProperties.InertiaTensorAboutCentroid(crystal.Mesh);
            double[] ev;
            Vec3d[] vecs;
            Eigen3.Jacobi(tensor, out ev, out vecs);
            double l0 = ev[0], l1 = ev[1], l2 = ev[2];
            Vec3d v0 = vecs[0], v1 = vecs[1], v2 = vecs[2];

            bool pair01 = ValuesClose(l0, l1);
            bool pair12 = ValuesClose(l1, l2);

            if (pair01 && !pair12)
            {
                axis = v2;
                return true;
            }
            if (pair12 && !pair01)
            {
                axis = v0;
                return true;
            }

            // Изотропный или брусок — берём longest bbox edge
            return TryAxisFromBBox(crystal.Mesh, scene, crystal.Name, out axis);
        }

        // Определить передний торец
        private static bool TryDetermineFront(SceneModel scene, Part crystal, FrameOptions opt,
            Vec3d facePlus, Vec3d faceMinus, out Vec3d front)
        {
            front = Vec3d.Zero;
            string sign = (opt.FrontSign ?? "auto").Trim();

            if (sign == "+z" || sign == "-z")
            {
                double dz = facePlus.Z - faceMinus.Z;
                if (Math.Abs(dz) < 1e-9)
                {
                    scene.Add("E_FRONT_AMBIG", DiagSeverity.Error, crystal.Name,
                        "Ось перпендикулярна Z файла — торец неоднозначен.");
                    return false;
                }
                bool wantPlus = (sign == "+z");
                front = (dz > 0.0 == wantPlus) ? facePlus : faceMinus;
                return true;
            }

            // auto — к ЦМ не-кристаллических деталей
            Vec3d com;
            double mass;
            if (!TryNonCrystalCom(scene, crystal, out com, out mass))
            {
                scene.Add("E_FRONT_AMBIG", DiagSeverity.Error, crystal.Name,
                    "Нет соседних деталей — передний торец не определён.");
                return false;
            }

            double dP = (facePlus - com).Length();
            double dM = (faceMinus - com).Length();
            if (Math.Abs(dP - dM) < 1e-9)
            {
                scene.Add("E_FRONT_AMBIG", DiagSeverity.Error, crystal.Name,
                    "ЦМ соседей на оси кристалла — неоднозначно.");
                return false;
            }

            front = (dP < dM) ? facePlus : faceMinus;
            return true;
        }

        // ЦМ всех деталей, кроме кристалла
        private static bool TryNonCrystalCom(SceneModel scene, Part crystal, out Vec3d com, out double totalMass)
        {
            com = Vec3d.Zero;
            totalMass = 0.0;
            double sx = 0.0, sy = 0.0, sz = 0.0;
            bool any = false;

            for (int i = 0; i < scene.Parts.Count; i++)
            {
                Part p = scene.Parts[i];
                if (p == crystal) continue;
                if (p.Mesh == null || p.Mesh.Vertices.Count == 0) continue;

                double m = (p.VolumeMm3 > 0.0) ? p.VolumeMm3 : 1.0;
                Vec3d c = ComputeCentroid(p.Mesh);
                sx += c.X * m;
                sy += c.Y * m;
                sz += c.Z * m;
                totalMass += m;
                any = true;
            }

            if (!any) return false;
            com = new Vec3d(sx / totalMass, sy / totalMass, sz / totalMass);
            return true;
        }

        // Среднее вершин
        private static Vec3d ComputeCentroid(Mesh mesh)
        {
            int n = mesh.Vertices.Count;
            double sx = 0.0, sy = 0.0, sz = 0.0;
            for (int i = 0; i < n; i++)
            {
                Vec3d v = mesh.Vertices[i];
                sx += v.X; sy += v.Y; sz += v.Z;
            }
            return new Vec3d(sx / n, sy / n, sz / n);
        }

        // Полудлина проекции на направление
        private static double ComputeHalfExtent(Mesh mesh, Vec3d dir)
        {
            double mn = double.MaxValue, mx = double.MinValue;
            for (int i = 0; i < mesh.Vertices.Count; i++)
            {
                double d = mesh.Vertices[i].Dot(dir);
                if (d < mn) mn = d;
                if (d > mx) mx = d;
            }
            return (mx - mn) * 0.5;
        }

        // Ось — самое длинное ребро bbox
        private static bool TryAxisFromBBox(Mesh mesh, SceneModel scene, string name, out Vec3d axis)
        {
            axis = Vec3d.Zero;
            Vec3d lo, hi;
            mesh.GetBounds(out lo, out hi);

            double ex = Math.Abs(hi.X - lo.X);
            double ey = Math.Abs(hi.Y - lo.Y);
            double ez = Math.Abs(hi.Z - lo.Z);
            double longest = Math.Max(ex, Math.Max(ey, ez));
            double tol = 1e-6 * Math.Max(longest, 1.0);

            int cnt = 0;
            if (Math.Abs(ex - longest) < tol) cnt++;
            if (Math.Abs(ey - longest) < tol) cnt++;
            if (Math.Abs(ez - longest) < tol) cnt++;

            if (cnt > 1)
            {
                scene.Add("E_AXIS_AMBIG", DiagSeverity.Error, name,
                    "Рёбра bbox близки — ось неоднозначна.");
                return false;
            }

            if (Math.Abs(ex - longest) < tol) axis = new Vec3d(1, 0, 0);
            else if (Math.Abs(ey - longest) < tol) axis = new Vec3d(0, 1, 0);
            else axis = new Vec3d(0, 0, 1);
            return true;
        }

        // Тензор инерции (точечное приближение по вершинам)
        private static void ComputeInertiaTensor(Mesh mesh, out double a, out double b, out double c,
            out double d, out double e, out double f)
        {
            Vec3d com = ComputeCentroid(mesh);
            double Ixx = 0, Iyy = 0, Izz = 0, Ixy = 0, Ixz = 0, Iyz = 0;

            for (int i = 0; i < mesh.Vertices.Count; i++)
            {
                double x = mesh.Vertices[i].X - com.X;
                double y = mesh.Vertices[i].Y - com.Y;
                double z = mesh.Vertices[i].Z - com.Z;
                double r2 = x * x + y * y + z * z;
                Ixx += y * y + z * z;
                Iyy += x * x + z * z;
                Izz += x * x + y * y;
                Ixy += x * y;
                Ixz += x * z;
                Iyz += y * z;
            }

            a = Ixx; b = Iyy; c = Izz;
            f = -Ixy; e = -Ixz; d = -Iyz;
        }

        // Собственные значения и векторы 3×3 симметричной матрицы
        private static void SolveEigen3(double a, double b, double c, double d, double e, double f,
            out double l0, out Vec3d v0, out double l1, out Vec3d v1, out double l2, out Vec3d v2)
        {
            double s1 = a + b + c;
            double s2 = a * b + a * c + b * c - d * d - e * e - f * f;
            double s3 = a * b * c + 2.0 * d * e * f - a * d * d - b * e * e - c * f * f;

            double p = s2 - s1 * s1 / 3.0;
            double q = 2.0 * s1 * s1 * s1 / 27.0 - s1 * s2 / 3.0 + s3;

            if (Math.Abs(p) < 1e-30 && Math.Abs(q) < 1e-30)
            {
                double l = s1 / 3.0;
                l0 = l; l1 = l; l2 = l;
                v0 = new Vec3d(1, 0, 0); v1 = new Vec3d(0, 1, 0); v2 = new Vec3d(0, 0, 1);
                return;
            }

            double r = Math.Sqrt(-p / 3.0);
            double cosArg = -q / (2.0 * r * r * r);
            if (cosArg > 1.0) cosArg = 1.0;
            if (cosArg < -1.0) cosArg = -1.0;
            double theta = Math.Acos(cosArg) / 3.0;

            l0 = 2.0 * r * Math.Cos(theta) + s1 / 3.0;
            l1 = 2.0 * r * Math.Cos(theta - 2.0 * Math.PI / 3.0) + s1 / 3.0;
            l2 = 2.0 * r * Math.Cos(theta + 2.0 * Math.PI / 3.0) + s1 / 3.0;

            v0 = Eigenvector(a, b, c, d, e, f, l0);
            v1 = Eigenvector(a, b, c, d, e, f, l1);
            v2 = Eigenvector(a, b, c, d, e, f, l2);
        }

        // Собственный вектор для заданного λ
        private static Vec3d Eigenvector(double a, double b, double c, double d, double e, double f, double l)
        {
            Vec3d r0 = new Vec3d(a - l, f, e);
            Vec3d r1 = new Vec3d(f, b - l, d);
            Vec3d r2 = new Vec3d(e, d, c - l);

            double n0 = r0.LengthSq(), n1 = r1.LengthSq(), n2 = r2.LengthSq();
            Vec3d res;

            if (n0 >= n1 && n0 >= n2)
            {
                res = r0.Cross(r1);
                if (res.LengthSq() < 1e-20) res = r0.Cross(r2);
            }
            else if (n1 >= n2)
            {
                res = r1.Cross(r2);
                if (res.LengthSq() < 1e-20) res = r1.Cross(r0);
            }
            else
            {
                res = r2.Cross(r0);
                if (res.LengthSq() < 1e-20) res = r2.Cross(r1);
            }

            return res.Normalized();
        }

        // Сортировка по возрастанию λ
        private static void SortEigen(ref double l0, ref Vec3d v0, ref double l1, ref Vec3d v1, ref double l2, ref Vec3d v2)
        {
            if (l0 > l1) { double t = l0; l0 = l1; l1 = t; Vec3d tv = v0; v0 = v1; v1 = tv; }
            if (l1 > l2) { double t = l1; l1 = l2; l2 = t; Vec3d tv = v1; v1 = v2; v2 = tv; }
            if (l0 > l1) { double t = l0; l0 = l1; l1 = t; Vec3d tv = v0; v0 = v1; v1 = tv; }
        }

        // Относительная близость двух скаляров
        private static bool ValuesClose(double x, double y)
        {
            double denom = Math.Max(Math.Abs(x), Math.Abs(y));
            if (denom < 1e-30) denom = 1e-30;
            return Math.Abs(x - y) / denom < EigenTol;
        }
    }
}
