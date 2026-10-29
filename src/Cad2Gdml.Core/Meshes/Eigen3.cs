using System;
using Cad2Gdml.Geometry;

namespace Cad2Gdml.Meshes
{
    // Собственные значения/векторы симметричной 3x3 методом Якоби
    public static class Eigen3
    {
        public static void Jacobi(double[,] m, out double[] values, out Vec3d[] vectors)
        {
            // Копия с принудительной симметрией
            double[,] a =
            {
                { (m[0,0]+m[0,0])*0.5, (m[0,1]+m[1,0])*0.5, (m[0,2]+m[2,0])*0.5 },
                { (m[1,0]+m[0,1])*0.5, (m[1,1]+m[1,1])*0.5, (m[1,2]+m[2,1])*0.5 },
                { (m[2,0]+m[0,2])*0.5, (m[2,1]+m[1,2])*0.5, (m[2,2]+m[2,2])*0.5 }
            };

            // Матрица накопления собственных векторов
            double[,] v =
            {
                { 1.0, 0.0, 0.0 },
                { 0.0, 1.0, 0.0 },
                { 0.0, 0.0, 1.0 }
            };

            // Циклические пасы, максимум 50
            for (int sweep = 0; sweep < 50; sweep++)
            {
                // Сумма квадратов внедиагональных и диагональных
                double off = a[0,1]*a[0,1] + a[0,2]*a[0,2] + a[1,2]*a[1,2];
                double diag = a[0,0]*a[0,0] + a[1,1]*a[1,1] + a[2,2]*a[2,2];
                if (off < 1e-24 * (diag + 1e-300))
                    break;

                Step(a, v, 0, 1);
                Step(a, v, 0, 2);
                Step(a, v, 1, 2);
            }

            // Диагональ — собственные значения
            double d0 = a[0, 0];
            double d1 = a[1, 1];
            double d2 = a[2, 2];

            // Столбцы v — собственные векторы, нормализация
            Vec3d w0 = Norm(v[0, 0], v[1, 0], v[2, 0]);
            Vec3d w1 = Norm(v[0, 1], v[1, 1], v[2, 1]);
            Vec3d w2 = Norm(v[0, 2], v[1, 2], v[2, 2]);

            // Сортировка по возрастанию (вставка, 3 элемента)
            double s0 = d0, s1 = d1, s2 = d2;
            Vec3d x0 = w0, x1 = w1, x2 = w2;

            if (s0 > s1) { double t = s0; s0 = s1; s1 = t; Vec3d u = x0; x0 = x1; x1 = u; }
            if (s1 > s2) { double t = s1; s1 = s2; s2 = t; Vec3d u = x1; x1 = x2; x2 = u; }
            if (s0 > s1) { double t = s0; s0 = s1; s1 = t; Vec3d u = x0; x0 = x1; x1 = u; }

            values = new double[] { s0, s1, s2 };
            vectors = new Vec3d[] { x0, x1, x2 };
        }

        // Вращение для пары (p, q) — классический алгоритм Якоби
        private static void Step(double[,] a, double[,] v, int p, int q)
        {
            double apq = a[p, q];
            if (apq == 0.0)
                return;

            double app = a[p, p];
            double aqq = a[q, q];

            // Угол через t = tan(θ/2)
            double theta = (aqq - app) / (2.0 * apq);
            double sign = theta >= 0.0 ? 1.0 : -1.0;
            double t = sign / (Math.Abs(theta) + Math.Sqrt(theta * theta + 1.0));
            double c = 1.0 / Math.Sqrt(t * t + 1.0);
            double s = t * c;

            // Обновление диагональных — след сохраняется
            a[p, p] -= t * apq;
            a[q, q] += t * apq;
            a[p, q] = 0.0;
            a[q, p] = 0.0;

            // Обновление внедиагональных для третьего индекса
            int r = 3 - p - q;
            double arp = a[r, p];
            double arq = a[r, q];
            a[r, p] = c * arp - s * arq;
            a[r, q] = s * arp + c * arq;
            a[p, r] = a[r, p];
            a[q, r] = a[r, q];

            // Умножение матрицы векторов справа на поворот
            for (int i = 0; i < 3; i++)
            {
                double vip = v[i, p];
                double viq = v[i, q];
                v[i, p] = c * vip - s * viq;
                v[i, q] = s * vip + c * viq;
            }
        }

        // Нормализация; нулевой вектор → (1,0,0)
        private static Vec3d Norm(double x, double y, double z)
        {
            double len = Math.Sqrt(x * x + y * y + z * z);
            if (len < 1e-15)
                return new Vec3d(1.0, 0.0, 0.0);
            return new Vec3d(x / len, y / len, z / len);
        }
    }
}
