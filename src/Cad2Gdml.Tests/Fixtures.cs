using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Cad2Gdml.Tests
{
    public static class Fixtures
    {
        // Запись бинарного STL (float32)
        public static string WriteBinaryStl(string path, IList<double[]> triangles)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs))
            {
                // Заголовок 80 байт
                for (int i = 0; i < 80; i++) bw.Write((byte)0);
                bw.Write((uint)triangles.Count);

                foreach (var t in triangles)
                {
                    // Нормаль из рёбер треугольника
                    double ex1 = t[3] - t[0], ey1 = t[4] - t[1], ez1 = t[5] - t[2];
                    double ex2 = t[6] - t[0], ey2 = t[7] - t[1], ez2 = t[8] - t[2];
                    double nx = ey1 * ez2 - ez1 * ey2;
                    double ny = ez1 * ex2 - ex1 * ez2;
                    double nz = ex1 * ey2 - ey1 * ex2;
                    double len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                    if (len > 0) { nx /= len; ny /= len; nz /= len; }

                    bw.Write((float)nx);
                    bw.Write((float)ny);
                    bw.Write((float)nz);
                    bw.Write((float)t[0]);
                    bw.Write((float)t[1]);
                    bw.Write((float)t[2]);
                    bw.Write((float)t[3]);
                    bw.Write((float)t[4]);
                    bw.Write((float)t[5]);
                    bw.Write((float)t[6]);
                    bw.Write((float)t[7]);
                    bw.Write((float)t[8]);
                    bw.Write((ushort)0);
                }
            }
            return path;
        }

        // Цилиндр: ось z, центр x=y=0, основание z0
        public static IList<double[]> Cylinder(double radius, double height, int segments, double z0)
        {
            var tris = new List<double[]>();
            double z1 = z0 + height;
            double dT = 2.0 * Math.PI / segments;

            // Боковая поверхность
            for (int i = 0; i < segments; i++)
            {
                double a0 = i * dT, a1 = (i + 1) * dT;
                double[] v0 = { radius * Math.Cos(a0), radius * Math.Sin(a0), z0 };
                double[] v1 = { radius * Math.Cos(a1), radius * Math.Sin(a1), z0 };
                double[] v2 = { radius * Math.Cos(a1), radius * Math.Sin(a1), z1 };
                double[] v3 = { radius * Math.Cos(a0), radius * Math.Sin(a0), z1 };
                tris.Add(MakeTri(v0, v1, v2));
                tris.Add(MakeTri(v0, v2, v3));
            }

            // Верхняя крышка (нормаль +z)
            double[] ctr = { 0.0, 0.0, z1 };
            for (int i = 0; i < segments; i++)
            {
                double a0 = i * dT, a1 = (i + 1) * dT;
                double[] p0 = { radius * Math.Cos(a0), radius * Math.Sin(a0), z1 };
                double[] p1 = { radius * Math.Cos(a1), radius * Math.Sin(a1), z1 };
                tris.Add(MakeTri(ctr, p0, p1));
            }

            // Нижняя крышка (нормаль -z)
            double[] ctrB = { 0.0, 0.0, z0 };
            for (int i = 0; i < segments; i++)
            {
                double a0 = i * dT, a1 = (i + 1) * dT;
                double[] p0 = { radius * Math.Cos(a0), radius * Math.Sin(a0), z0 };
                double[] p1 = { radius * Math.Cos(a1), radius * Math.Sin(a1), z0 };
                tris.Add(MakeTri(ctrB, p1, p0));
            }

            return tris;
        }

        // Призма — цилиндр с малым числом сторон
        public static IList<double[]> Prism(double radius, double height, int sides)
        {
            return Cylinder(radius, height, sides, 0.0);
        }

        // Параллелепипед с центром (cx,cy,cz)
        public static IList<double[]> Box(double sx, double sy, double sz, double cx, double cy, double cz)
        {
            double hx = sx / 2.0, hy = sy / 2.0, hz = sz / 2.0;
            double x0 = cx - hx, x1 = cx + hx;
            double y0 = cy - hy, y1 = cy + hy;
            double z0 = cz - hz, z1 = cz + hz;
            var tris = new List<double[]>();

            // Лицо -x
            tris.Add(MakeTri(new double[] { x0, y0, z1 }, new double[] { x0, y1, z1 }, new double[] { x0, y1, z0 }));
            tris.Add(MakeTri(new double[] { x0, y0, z1 }, new double[] { x0, y1, z0 }, new double[] { x0, y0, z0 }));
            // Лицо +x
            tris.Add(MakeTri(new double[] { x1, y0, z0 }, new double[] { x1, y1, z0 }, new double[] { x1, y1, z1 }));
            tris.Add(MakeTri(new double[] { x1, y0, z0 }, new double[] { x1, y1, z1 }, new double[] { x1, y0, z1 }));
            // Лицо -y
            tris.Add(MakeTri(new double[] { x0, y0, z0 }, new double[] { x1, y0, z0 }, new double[] { x1, y0, z1 }));
            tris.Add(MakeTri(new double[] { x0, y0, z0 }, new double[] { x1, y0, z1 }, new double[] { x0, y0, z1 }));
            // Лицо +y
            tris.Add(MakeTri(new double[] { x1, y1, z0 }, new double[] { x0, y1, z0 }, new double[] { x0, y1, z1 }));
            tris.Add(MakeTri(new double[] { x1, y1, z0 }, new double[] { x0, y1, z1 }, new double[] { x1, y1, z1 }));
            // Лицо -z
            tris.Add(MakeTri(new double[] { x0, y1, z0 }, new double[] { x1, y1, z0 }, new double[] { x1, y0, z0 }));
            tris.Add(MakeTri(new double[] { x0, y1, z0 }, new double[] { x1, y0, z0 }, new double[] { x0, y0, z0 }));
            // Лицо +z
            tris.Add(MakeTri(new double[] { x0, y0, z1 }, new double[] { x1, y0, z1 }, new double[] { x1, y1, z1 }));
            tris.Add(MakeTri(new double[] { x0, y0, z1 }, new double[] { x1, y1, z1 }, new double[] { x0, y1, z1 }));

            return tris;
        }

        // Пустотелый цилиндр: внешняя и внутренняя стенки + кольца
        public static IList<double[]> HollowCylinder(double rOut, double rIn, double height, int segments, double z0)
        {
            var tris = new List<double[]>();
            double z1 = z0 + height;
            double dT = 2.0 * Math.PI / segments;

            // Внешняя стенка (нормаль наружу)
            for (int i = 0; i < segments; i++)
            {
                double a0 = i * dT, a1 = (i + 1) * dT;
                double[] v0 = { rOut * Math.Cos(a0), rOut * Math.Sin(a0), z0 };
                double[] v1 = { rOut * Math.Cos(a1), rOut * Math.Sin(a1), z0 };
                double[] v2 = { rOut * Math.Cos(a1), rOut * Math.Sin(a1), z1 };
                double[] v3 = { rOut * Math.Cos(a0), rOut * Math.Sin(a0), z1 };
                tris.Add(MakeTri(v0, v1, v2));
                tris.Add(MakeTri(v0, v2, v3));
            }

            // Внутренняя стенка (нормаль внутрь)
            for (int i = 0; i < segments; i++)
            {
                double a0 = i * dT, a1 = (i + 1) * dT;
                double[] v0 = { rIn * Math.Cos(a0), rIn * Math.Sin(a0), z0 };
                double[] v1 = { rIn * Math.Cos(a1), rIn * Math.Sin(a1), z0 };
                double[] v2 = { rIn * Math.Cos(a1), rIn * Math.Sin(a1), z1 };
                double[] v3 = { rIn * Math.Cos(a0), rIn * Math.Sin(a0), z1 };
                tris.Add(MakeTri(v0, v2, v1));
                tris.Add(MakeTri(v0, v3, v2));
            }

            // Верхний кольцевой пояс (нормаль +z)
            for (int i = 0; i < segments; i++)
            {
                double a0 = i * dT, a1 = (i + 1) * dT;
                double[] o0 = { rOut * Math.Cos(a0), rOut * Math.Sin(a0), z1 };
                double[] o1 = { rOut * Math.Cos(a1), rOut * Math.Sin(a1), z1 };
                double[] i0 = { rIn * Math.Cos(a0), rIn * Math.Sin(a0), z1 };
                double[] i1 = { rIn * Math.Cos(a1), rIn * Math.Sin(a1), z1 };
                tris.Add(MakeTri(o0, o1, i1));
                tris.Add(MakeTri(o0, i1, i0));
            }

            // Нижний кольцевой пояс (нормаль -z)
            for (int i = 0; i < segments; i++)
            {
                double a0 = i * dT, a1 = (i + 1) * dT;
                double[] o0 = { rOut * Math.Cos(a0), rOut * Math.Sin(a0), z0 };
                double[] o1 = { rOut * Math.Cos(a1), rOut * Math.Sin(a1), z0 };
                double[] i0 = { rIn * Math.Cos(a0), rIn * Math.Sin(a0), z0 };
                double[] i1 = { rIn * Math.Cos(a1), rIn * Math.Sin(a1), z0 };
                tris.Add(MakeTri(o1, o0, i0));
                tris.Add(MakeTri(o1, i0, i1));
            }

            return tris;
        }

        // Инверсия обёртки каждого треугольника
        public static IList<double[]> FlipAll(IList<double[]> tris)
        {
            var result = new List<double[]>(tris.Count);
            for (int i = 0; i < tris.Count; i++)
            {
                double[] t = tris[i];
                double[] f = new double[9];
                // (a, b, c) -> (a, c, b)
                f[0] = t[0]; f[1] = t[1]; f[2] = t[2];
                f[3] = t[6]; f[4] = t[7]; f[5] = t[8];
                f[6] = t[3]; f[7] = t[4]; f[8] = t[5];
                result.Add(f);
            }
            return result;
        }

        // Удаление треугольника по индексу
        public static IList<double[]> RemoveOne(IList<double[]> tris, int index)
        {
            var result = new List<double[]>(tris.Count - 1);
            for (int i = 0; i < tris.Count; i++)
            {
                if (i == index) continue;
                result.Add(tris[i]);
            }
            return result;
        }

        // Создание тестовой сцены в каталоге
        public static string WriteScene(string dir)
        {
            Directory.CreateDirectory(dir);

            // Кристалл
            WriteBinaryStl(Path.Combine(dir, "crystal.stl"), Cylinder(25.4, 50.8, 64, 10.0));

            // Корпус — совпадает с регуляркой housing
            WriteBinaryStl(Path.Combine(dir, "shell.stl"), HollowCylinder(28.0, 25.4 + 0.5, 60.0, 64, 5.0));

            // Вода-образец
            WriteBinaryStl(Path.Combine(dir, "water_sample.stl"), Cylinder(30.0, 40.0, 64, 70.0));

            return dir;
        }

        // Вспомогательный: сборка double[9] из трёх вершин
        private static double[] MakeTri(double[] a, double[] b, double[] c)
        {
            double[] t = new double[9];
            t[0] = a[0]; t[1] = a[1]; t[2] = a[2];
            t[3] = b[0]; t[4] = b[1]; t[5] = b[2];
            t[6] = c[0]; t[7] = c[1]; t[8] = c[2];
            return t;
        }
    }
}
