using System;
using System.Globalization;

namespace Cad2Gdml.Geometry
{
    // Трёхмерный вектор с координатами X, Y, Z
    public struct Vec3d
    {
        public readonly double X;
        public readonly double Y;
        public readonly double Z;

        // Конструктор
        public Vec3d(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        // Нулевой вектор
        public static readonly Vec3d Zero = new Vec3d(0, 0, 0);

        // Базисные векторы
        public static readonly Vec3d UnitX = new Vec3d(1, 0, 0);
        public static readonly Vec3d UnitY = new Vec3d(0, 1, 0);
        public static readonly Vec3d UnitZ = new Vec3d(0, 0, 1);

        // Сложение векторов
        public static Vec3d operator +(Vec3d a, Vec3d b)
        {
            return new Vec3d(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        }

        // Вычитание векторов
        public static Vec3d operator -(Vec3d a, Vec3d b)
        {
            return new Vec3d(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        // Унарный минус
        public static Vec3d operator -(Vec3d a)
        {
            return new Vec3d(-a.X, -a.Y, -a.Z);
        }

        // Умножение вектора на скаляр
        public static Vec3d operator *(Vec3d a, double s)
        {
            return new Vec3d(a.X * s, a.Y * s, a.Z * s);
        }

        // Умножение скаляра на вектор
        public static Vec3d operator *(double s, Vec3d a)
        {
            return new Vec3d(a.X * s, a.Y * s, a.Z * s);
        }

        // Деление вектора на скаляр
        public static Vec3d operator /(Vec3d a, double s)
        {
            return new Vec3d(a.X / s, a.Y / s, a.Z / s);
        }

        // Скалярное произведение
        public double Dot(Vec3d other)
        {
            return X * other.X + Y * other.Y + Z * other.Z;
        }

        // Векторное произведение
        public Vec3d Cross(Vec3d other)
        {
            return new Vec3d(
                Y * other.Z - Z * other.Y,
                Z * other.X - X * other.Z,
                X * other.Y - Y * other.X);
        }

        // Квадрат длины
        public double LengthSq()
        {
            return X * X + Y * Y + Z * Z;
        }

        // Длина
        public double Length()
        {
            return Math.Sqrt(LengthSq());
        }

        // Нормализация; нулевой вектор остаётся нулевым
        public Vec3d Normalized()
        {
            double len = Length();
            if (len == 0.0)
            {
                return Zero;
            }
            return this / len;
        }

        // Расстояние до другой точки
        public double DistanceTo(Vec3d other)
        {
            Vec3d d = this - other;
            return d.Length();
        }

        // Проверка равенства
        public bool Equals(Vec3d other)
        {
            return X == other.X && Y == other.Y && Z == other.Z;
        }

        // Переопределение Equals(object)
        public override bool Equals(object obj)
        {
            if (obj is Vec3d v)
            {
                return Equals(v);
            }
            return false;
        }

        // Хеш-код
        public override int GetHashCode()
        {
            // Сложение хеш-кодов координат
            int h = X.GetHashCode();
            h = (h * 397) ^ Y.GetHashCode();
            h = (h * 397) ^ Z.GetHashCode();
            return h;
        }

        // Строковое представление "(x, y, z)"
        public override string ToString()
        {
            string xs = X.ToString(CultureInfo.InvariantCulture);
            string ys = Y.ToString(CultureInfo.InvariantCulture);
            string zs = Z.ToString(CultureInfo.InvariantCulture);
            return "(" + xs + ", " + ys + ", " + zs + ")";
        }
    }
}
