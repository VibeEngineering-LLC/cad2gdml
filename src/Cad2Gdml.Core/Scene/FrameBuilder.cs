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
        // Порог близости собственных значений (float32-шум)
        private const double EigenTol = 1e-3;

        // Построить преобразование: сцена → кадр (z=0 на переднем торце, тело кристалла при z<0)
        public static Affine3 Build(SceneModel scene, Part crystal, FrameOptions opt)
        {
            // 1. Ось кристалла (собственный вектор тензора инерции)
            Vec3d axisDir;
            if (!TryDetermineAxis(scene, crystal, out axisDir))
            {
                throw new InvalidOperationException("E_AXIS_AMBIG: ось кристалла не определена.");
            }
            axisDir = axisDir.Normalized();

            // 2. Центр и полудлина — проекция вершин на ось и перпендикулярную плоскость
            Vec3d center;
            double halfLen;
            ComputeAxisBounds(crystal.Mesh, axisDir, out center, out halfLen);

            Vec3d facePlus = center + axisDir * halfLen;
            Vec3d faceMinus = center - axisDir * halfLen;

            // 3. Передний торец (станет z=0 в кадре)
            Vec3d frontCenter;
            if (!TryDetermineFront(scene, crystal, opt, facePlus, faceMinus, out frontCenter))
            {
                throw new InvalidOperationException("E_FRONT_AMBIG: передний торец не определён.");
            }

            // 4. Ось z кадра: от центра кристалла к переднему торцу
            //    (тело кристалла занимает z ∈ [−L, 0], источники — z > 0)
            Vec3d zF = frontCenter - center;
            if (zF.LengthSq() < 1e-24)
            {
                zF = axisDir;
            }
            zF = zF.Normalized();

            // 5. Ось x: проекция file-X на плоскость ⊥ z; при вырождении — file-Y
            Vec3d fileX = new Vec3d(1.0, 0.0, 0.0);
            Vec3d xProj = fileX - zF * (fileX.Dot(zF));
            if (xProj.LengthSq() < 1e-12)
            {
                Vec3d fileY = new Vec3d(0.0, 1.0, 0.0);
                xProj = fileY - zF * (fileY.Dot(zF));
            }
            Vec3d xF = xProj.Normalized();
            Vec3d yF = zF.Cross(xF); // правая тройка

            // 6. Аффинное преобразование: p_frame = R·(p_file − frontCenter)
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

        // Центр + полудлина через bbox проекций вершин
        private static void ComputeAxisBounds(Mesh mesh, Vec3d d, out Vec3d center, out double halfLen)
        {
            Vec3d u, v;
            BuildPerpBasis(d, out u, out v);

            double zmin = double.MaxValue, zmax = double.MinValue;
            double umin = double.MaxValue, umax = double.MinValue;
            double vmin = double.MaxValue, vmax = double.MinValue;

            for (int i = 0; i < mesh.Vertices.Count; i++)
            {
                Vec3d p = mesh.Vertices[i];
                double pz = p.Dot(d);
                double pu = p.Dot(u);
                double pv = p.Dot(v);
                if (pz < zmin) { zmin = pz; }
                if (pz > zmax) { zmax = pz; }
                if (pu < umin) { umin = pu; }
                if (pu > umax) { umax = pu; }
                if (pv < vmin) { vmin = pv; }
                if (pv > vmax) { vmax = pv; }
            }

            // Центр bbox: середина вдоль оси + центр перпендикулярной проекции
            double cz = (zmin + zmax) * 0.5;
            double cu = (umin + umax) * 0.5;
            double cv = (vmin + vmax) * 0.5;

            center = d * cz + u * cu + v * cv;
            halfLen = (zmax - zmin) * 0.5;
        }

        // Ортогональный базис (u, v) ⊥ d
        private static void BuildPerpBasis(Vec3d d, out Vec3d u, out Vec3d v)
        {
            Vec3d refV;
            if (Math.Abs(d.X) < 0.9)
            {
                refV = Vec3d.UnitX;
            }
            else
            {
                refV = Vec3d.UnitY;
            }
            u = d.Cross(refV).Normalized();
            v = d.Cross(u);
        }

        // Ось кристалла: собственный вектор, отличный от двух близких
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

            // Значения отсортированы по возрастанию: l0 ≤ l1 ≤ l2
            double l0 = ev[0], l1 = ev[1], l2 = ev[2];
            Vec3d v0 = vecs[0], v1 = vecs[1], v2 = vecs[2];

            bool pair01 = ValuesClose(l0, l1);
            bool pair12 = ValuesClose(l1, l2);

            if (pair01 && !pair12)
            {
                // l0 ≈ l1, l2 отличен → ось v2
                axis = v2;
                return true;
            }
            if (pair12 && !pair01)
            {
                // l1 ≈ l2, l0 отличен → ось v0
                axis = v0;
                return true;
            }

            // Все три близки (или все различны) — используем bbox
            return TryAxisFromBBox(crystal.Mesh, scene, crystal.Name, out axis);
        }

        // Ось — самое длинное ребро bounding box
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
            if (Math.Abs(ex - longest) < tol) { cnt++; }
            if (Math.Abs(ey - longest) < tol) { cnt++; }
            if (Math.Abs(ez - longest) < tol) { cnt++; }

            if (cnt > 1)
            {
                scene.Add("E_AXIS_AMBIG", DiagSeverity.Error, name,
                    "Рёбра bbox близки — ось неоднозначна.");
                return false;
            }

            if (Math.Abs(ex - longest) < tol) { axis = new Vec3d(1, 0, 0); }
            else if (Math.Abs(ey - longest) < tol) { axis = new Vec3d(0, 1, 0); }
            else { axis = new Vec3d(0, 0, 1); }
            return true;
        }

        // Относительная близость скаляров
        private static bool ValuesClose(double x, double y)
        {
            double denom = Math.Max(Math.Abs(x), Math.Abs(y));
            if (denom < 1e-30) { denom = 1e-30; }
            return Math.Abs(x - y) / denom < EigenTol;
        }

        // Выбор переднего торца
        private static bool TryDetermineFront(SceneModel scene, Part crystal, FrameOptions opt,
            Vec3d facePlus, Vec3d faceMinus, out Vec3d front)
        {
            front = Vec3d.Zero;
            string sign = (opt.FrontSign ?? "auto").Trim();

            if (sign == "+z" || sign == "-z")
            {
                // +z/-z — знак file-Z компоненты оси;
                // при |d.Z| < 1e-9 — знак доминирующей компоненты оси:
                //   +z → торец, где доминирующая координата положительна
                //   -z → противоположный торец
                Vec3d d = (facePlus - faceMinus).Normalized();
                bool wantPlus = (sign == "+z");

                if (Math.Abs(d.Z) >= 1e-9)
                {
                    // Ось имеет Z-компоненту
                    bool plusIsFront = (d.Z > 0.0);
                    front = (plusIsFront == wantPlus) ? facePlus : faceMinus;
                }
                else
                {
                    // Ось ⟂ Z: берём доминирующую компоненту
                    double ax = Math.Abs(d.X);
                    double ay = Math.Abs(d.Y);
                    double domVal;
                    if (ax >= ay)
                    {
                        domVal = d.X;
                    }
                    else
                    {
                        domVal = d.Y;
                    }

                    bool domPositive = (domVal > 0.0);
                    front = (domPositive == wantPlus) ? facePlus : faceMinus;
                }

                return true;
            }

            // auto: торец, ближайший к ЦМ не-кристаллических деталей
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

        // Объёмно-взвешенный ЦМ деталей, кроме кристалла
        private static bool TryNonCrystalCom(SceneModel scene, Part crystal, out Vec3d com, out double totalMass)
        {
            com = Vec3d.Zero;
            totalMass = 0.0;
            double sx = 0.0, sy = 0.0, sz = 0.0;
            bool any = false;

            for (int i = 0; i < scene.Parts.Count; i++)
            {
                Part p = scene.Parts[i];
                if (p == crystal) { continue; }
                if (p.Mesh == null || p.Mesh.Vertices.Count == 0) { continue; }

                double m = (p.VolumeMm3 > 0.0) ? p.VolumeMm3 : 1.0;
                Vec3d c = MeshProperties.Centroid(p.Mesh);
                sx += c.X * m;
                sy += c.Y * m;
                sz += c.Z * m;
                totalMass += m;
                any = true;
            }

            if (!any) { return false; }
            com = new Vec3d(sx / totalMass, sy / totalMass, sz / totalMass);
            return true;
        }
    }
}
