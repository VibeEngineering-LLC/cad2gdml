using System;
using System.Globalization;

namespace Cad2Gdml.Geometry
{
    // Аффинное преобразование: матрица 3x3 + параллельный перенос
    public struct Affine3
    {
        // Элементы матрицы (по строкам)
        public double M00;
        public double M01;
        public double M02;
        public double M10;
        public double M11;
        public double M12;
        public double M20;
        public double M21;
        public double M22;

        // Вектор переноса
        public Vec3d T;

        // Конструктор
        public Affine3(double m00, double m01, double m02,
                       double m10, double m11, double m12,
                       double m20, double m21, double m22,
                       Vec3d t)
        {
            M00 = m00; M01 = m01; M02 = m02;
            M10 = m10; M11 = m11; M12 = m12;
            M20 = m20; M21 = m21; M22 = m22;
            T = t;
        }

        // Единичное преобразование
        public static Affine3 Identity
        {
            get
            {
                return new Affine3(
                    1, 0, 0,
                    0, 1, 0,
                    0, 0, 1,
                    Vec3d.Zero);
            }
        }

        // Только перенос
        public static Affine3 Translation(Vec3d t)
        {
            return new Affine3(
                1, 0, 0,
                0, 1, 0,
                0, 0, 1,
                t);
        }

        // Равномерное масштабирование
        public static Affine3 Scale(double s)
        {
            return new Affine3(
                s, 0, 0,
                0, s, 0,
                0, 0, s,
                Vec3d.Zero);
        }

        // Поворот вокруг оси — формула Родригеса
        public static Affine3 RotationAxisAngle(Vec3d axis, double radians)
        {
            Vec3d k = axis.Normalized();
            double c = Math.Cos(radians);
            double s = Math.Sin(radians);
            double omc = 1.0 - c;

            double x = k.X, y = k.Y, z = k.Z;

            return new Affine3(
                c + x * x * omc,       x * y * omc - z * s,  x * z * omc + y * s,
                x * y * omc + z * s,   c + y * y * omc,      y * z * omc - x * s,
                x * z * omc - y * s,   y * z * omc + x * s,  c + z * z * omc,
                Vec3d.Zero);
        }

        // Кратчайший поворот из направления a в направление b
        public static Affine3 RotationFromTo(Vec3d a, Vec3d b)
        {
            Vec3d ua = a.Normalized();
            Vec3d ub = b.Normalized();
            double c = ua.Dot(ub);

            // Совпадающие направления
            if (c >= 1.0 - 1e-12)
            {
                return Identity;
            }

            // Противоположные направления — поворот на 180°
            if (c <= -1.0 + 1e-12)
            {
                // Базисный вектор, не параллельный ua
                Vec3d refV;
                if (Math.Abs(ua.X) < 0.9)
                {
                    refV = Vec3d.UnitX;
                }
                else
                {
                    refV = Vec3d.UnitY;
                }
                Vec3d ax = ua.Cross(refV).Normalized();
                double axx = ax.X, axy = ax.Y, axz = ax.Z;
                // R = 2*k*k^T - I для угла π
                return new Affine3(
                    2 * axx * axx - 1,  2 * axx * axy,      2 * axx * axz,
                    2 * axy * axx,      2 * axy * axy - 1,  2 * axy * axz,
                    2 * axz * axx,      2 * axz * axy,      2 * axz * axz - 1,
                    Vec3d.Zero);
            }

            // Общий случай: R = c*I + [v]x + (v*v^T)*(1-c)/|v|^2
            Vec3d v = ua.Cross(ub);
            double dotVV = v.Dot(v);
            double invVV = 1.0 / dotVV;
            double omc = 1.0 - c;

            double vx = v.X, vy = v.Y, vz = v.Z;

            double r00 = c + vx * vx * omc * invVV;
            double r01 = -vz + vx * vy * omc * invVV;
            double r02 = vy + vx * vz * omc * invVV;
            double r10 = vz + vy * vx * omc * invVV;
            double r11 = c + vy * vy * omc * invVV;
            double r12 = -vx + vy * vz * omc * invVV;
            double r20 = -vy + vz * vx * omc * invVV;
            double r21 = vx + vz * vy * omc * invVV;
            double r22 = c + vz * vz * omc * invVV;

            return new Affine3(r00, r01, r02, r10, r11, r12, r20, r21, r22, Vec3d.Zero);
        }

        // Сборка из столбцов матрицы и переноса
        public static Affine3 FromColumns(Vec3d cx, Vec3d cy, Vec3d cz, Vec3d t)
        {
            return new Affine3(
                cx.X, cy.X, cz.X,
                cx.Y, cy.Y, cz.Y,
                cx.Z, cy.Z, cz.Z,
                t);
        }

        // Применить к точке (с учётом переноса)
        public Vec3d Apply(Vec3d point)
        {
            double x = M00 * point.X + M01 * point.Y + M02 * point.Z + T.X;
            double y = M10 * point.X + M11 * point.Y + M12 * point.Z + T.Y;
            double z = M20 * point.X + M21 * point.Y + M22 * point.Z + T.Z;
            return new Vec3d(x, y, z);
        }

        // Применить к направлению (без переноса)
        public Vec3d ApplyDirection(Vec3d d)
        {
            double x = M00 * d.X + M01 * d.Y + M02 * d.Z;
            double y = M10 * d.X + M11 * d.Y + M12 * d.Z;
            double z = M20 * d.X + M21 * d.Y + M22 * d.Z;
            return new Vec3d(x, y, z);
        }

        // this * other: this применяется после other
        public Affine3 Multiply(Affine3 other)
        {
            // Новый блок 3x3 = M_this * M_other
            double n00 = M00 * other.M00 + M01 * other.M10 + M02 * other.M20;
            double n01 = M00 * other.M01 + M01 * other.M11 + M02 * other.M21;
            double n02 = M00 * other.M02 + M01 * other.M12 + M02 * other.M22;
            double n10 = M10 * other.M00 + M11 * other.M10 + M12 * other.M20;
            double n11 = M10 * other.M01 + M11 * other.M11 + M12 * other.M21;
            double n12 = M10 * other.M02 + M11 * other.M12 + M12 * other.M22;
            double n20 = M20 * other.M00 + M21 * other.M10 + M22 * other.M20;
            double n21 = M20 * other.M01 + M21 * other.M11 + M22 * other.M21;
            double n22 = M20 * other.M02 + M21 * other.M12 + M22 * other.M22;

            // Новый перенос = M_this * T_other + T_this
            double tx = M00 * other.T.X + M01 * other.T.Y + M02 * other.T.Z + T.X;
            double ty = M10 * other.T.X + M11 * other.T.Y + M12 * other.T.Z + T.Y;
            double tz = M20 * other.T.X + M21 * other.T.Y + M22 * other.T.Z + T.Z;

            return new Affine3(n00, n01, n02, n10, n11, n12, n20, n21, n22, new Vec3d(tx, ty, tz));
        }

        // Определитель матрицы 3x3
        public double Determinant()
        {
            return M00 * (M11 * M22 - M12 * M21)
                 - M01 * (M10 * M22 - M12 * M20)
                 + M02 * (M10 * M21 - M11 * M20);
        }

        // Обратное преобразование; бросает исключение при вырожденной матрице
        public Affine3 Inverse()
        {
            double det = Determinant();
            if (Math.Abs(det) < 1e-15)
            {
                throw new InvalidOperationException("Сингулярная матрица — обращение невозможно.");
            }

            double invDet = 1.0 / det;

            // Обратная через алгебраические дополнения
            double i00 = (M11 * M22 - M12 * M21) * invDet;
            double i01 = (M02 * M21 - M01 * M22) * invDet;
            double i02 = (M01 * M12 - M02 * M11) * invDet;
            double i10 = (M12 * M20 - M10 * M22) * invDet;
            double i11 = (M00 * M22 - M02 * M20) * invDet;
            double i12 = (M01 * M20 - M00 * M21) * invDet;
            double i20 = (M10 * M21 - M11 * M20) * invDet;
            double i21 = (M02 * M10 - M00 * M12) * invDet;
            double i22 = (M00 * M11 - M01 * M10) * invDet;

            // Новый перенос = -invM * T
            double tx = -(i00 * T.X + i01 * T.Y + i02 * T.Z);
            double ty = -(i10 * T.X + i11 * T.Y + i12 * T.Z);
            double tz = -(i20 * T.X + i21 * T.Y + i22 * T.Z);

            return new Affine3(i00, i01, i02, i10, i11, i12, i20, i21, i22, new Vec3d(tx, ty, tz));
        }
    }
}
