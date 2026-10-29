using System;
using System.Collections.Generic;
using System.Globalization;
using Cad2Gdml.Geometry;

namespace Cad2Gdml.Meshes
{
    // Свойства меша: объём, площадь, центроид, инерция
    public static class MeshProperties
    {
        // Знаковый объём (сумма тетраэдров с O), мм³
        public static double SignedVolume(Mesh m)
        {
            double vol = 0.0;
            for (int i = 0; i < m.TriangleCount; i++)
            {
                Vec3d a, b, c;
                m.GetTriangle(i, out a, out b, out c);
                vol += a.Dot(b.Cross(c)) / 6.0;
            }
            return vol;
        }

        // Площадь поверхности, мм²
        public static double SurfaceArea(Mesh m)
        {
            double area = 0.0;
            for (int i = 0; i < m.TriangleCount; i++)
            {
                Vec3d a, b, c;
                m.GetTriangle(i, out a, out b, out c);
                area += (b - a).Cross(c - a).Length() * 0.5;
            }
            return area;
        }

        // Центроид: объёмно-взвешенный; при |V|<1e-12 — среднее вершин
        public static Vec3d Centroid(Mesh m)
        {
            double vol = SignedVolume(m);
            if (Math.Abs(vol) < 1e-12)
            {
                if (m.Vertices.Count == 0)
                {
                    return Vec3d.Zero;
                }
                Vec3d sum = Vec3d.Zero;
                for (int i = 0; i < m.Vertices.Count; i++)
                {
                    sum = sum + m.Vertices[i];
                }
                return sum / m.Vertices.Count;
            }

            // Центроид тетраэдра OABC = (a+b+c)/4
            Vec3d acc = Vec3d.Zero;
            for (int i = 0; i < m.TriangleCount; i++)
            {
                Vec3d a, b, c;
                m.GetTriangle(i, out a, out b, out c);
                double v = a.Dot(b.Cross(c)) / 6.0;
                Vec3d tCentroid = (a + b + c) / 4.0;
                acc = acc + tCentroid * v;
            }
            return acc / vol;
        }

        // Тензор инерции относительно центроида (ρ=1), double[3,3]
        public static double[,] InertiaTensorAboutCentroid(Mesh m)
        {
            double vol = SignedVolume(m);
            Vec3d G = Centroid(m);

            // Суммируем вклад каждого тетраэдра (O, a, b, c)
            double I00 = 0.0, I01 = 0.0, I02 = 0.0;
            double I11 = 0.0, I12 = 0.0, I22 = 0.0;

            for (int i = 0; i < m.TriangleCount; i++)
            {
                Vec3d a, b, c;
                m.GetTriangle(i, out a, out b, out c);
                double v = a.Dot(b.Cross(c)) / 6.0;
                if (v == 0.0)
                {
                    continue;
                }

                // Сумма вершин (без O)
                Vec3d C = a + b + c;
                double Csq = C.LengthSq();

                // Q = a⊗a + b⊗b + c⊗c
                double Q00 = a.X * a.X + b.X * b.X + c.X * c.X;
                double Q01 = a.X * a.Y + b.X * b.Y + c.X * c.Y;
                double Q02 = a.X * a.Z + b.X * b.Z + c.X * c.Z;
                double Q11 = a.Y * a.Y + b.Y * b.Y + c.Y * c.Y;
                double Q12 = a.Y * a.Z + b.Y * b.Z + c.Y * c.Z;
                double Q22 = a.Z * a.Z + b.Z * b.Z + c.Z * c.Z;
                double Qtr = Q00 + Q11 + Q22;

                // I_{αβ} = v/20 * [δ_{αβ}(Qtr+|C|²) − Q_{αβ} − C_α C_β]
                double f = v / 20.0;
                double diag = Qtr + Csq;

                I00 += f * (diag - Q00 - C.X * C.X);
                I01 += f * (-(Q01 + C.X * C.Y));
                I02 += f * (-(Q02 + C.X * C.Z));
                I11 += f * (diag - Q11 - C.Y * C.Y);
                I12 += f * (-(Q12 + C.Y * C.Z));
                I22 += f * (diag - Q22 - C.Z * C.Z);
            }

            // Теорема переноса: I_G = I_O − M(|G|²·I₃ − G⊗G)
            double Gsq = G.LengthSq();
            I00 -= vol * (Gsq - G.X * G.X);
            I01 += vol * G.X * G.Y;
            I02 += vol * G.X * G.Z;
            I11 -= vol * (Gsq - G.Y * G.Y);
            I12 += vol * G.Y * G.Z;
            I22 -= vol * (Gsq - G.Z * G.Z);

            // Симметрия
            double[,] I = new double[3, 3];
            I[0, 0] = I00; I[0, 1] = I01; I[0, 2] = I02;
            I[1, 0] = I01; I[1, 1] = I11; I[1, 2] = I12;
            I[2, 0] = I02; I[2, 1] = I12; I[2, 2] = I22;
            return I;
        }
    }
}
