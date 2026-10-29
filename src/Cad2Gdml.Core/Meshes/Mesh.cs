using System;
using System.Collections.Generic;
using System.Globalization;
using Cad2Gdml.Geometry;

namespace Cad2Gdml.Meshes
{
    // Сетка: список вершин + тройки индексов треугольников
    public sealed class Mesh
    {
        public List<Vec3d> Vertices { get; }
        public List<int> Indices { get; }

        public Mesh()
        {
            Vertices = new List<Vec3d>();
            Indices = new List<int>();
        }

        // Число треугольников
        public int TriangleCount
        {
            get { return Indices.Count / 3; }
        }

        // Добавить треугольник — три новые вершины без повторного использования
        public void AddTriangle(Vec3d a, Vec3d b, Vec3d c)
        {
            int baseIdx = Vertices.Count;
            Vertices.Add(a);
            Vertices.Add(b);
            Vertices.Add(c);
            Indices.Add(baseIdx);
            Indices.Add(baseIdx + 1);
            Indices.Add(baseIdx + 2);
        }

        // Получить вершины i-го треугольника
        public void GetTriangle(int i, out Vec3d a, out Vec3d b, out Vec3d c)
        {
            int i0 = Indices[i * 3];
            int i1 = Indices[i * 3 + 1];
            int i2 = Indices[i * 3 + 2];
            a = Vertices[i0];
            b = Vertices[i1];
            c = Vertices[i2];
        }

        // Копирование
        public Mesh Clone()
        {
            Mesh m = new Mesh();
            m.Vertices.AddRange(Vertices);
            m.Indices.AddRange(Indices);
            return m;
        }

        // Преобразованная копия; при отрицательном определителе разворачивает хрен
        public Mesh Transformed(Affine3 t)
        {
            Mesh m = new Mesh();
            for (int i = 0; i < Vertices.Count; i++)
            {
                m.Vertices.Add(t.Apply(Vertices[i]));
            }

            bool flip = t.Determinant() < 0.0;
            if (flip)
            {
                for (int i = 0; i < Indices.Count; i += 3)
                {
                    m.Indices.Add(Indices[i]);
                    m.Indices.Add(Indices[i + 2]);
                    m.Indices.Add(Indices[i + 1]);
                }
            }
            else
            {
                m.Indices.AddRange(Indices);
            }

            return m;
        }

        // Разворот хрена на месте (swap индексов 1 и 2 в каждой тройке)
        public void FlipWinding()
        {
            for (int i = 0; i < Indices.Count; i += 3)
            {
                int tmp = Indices[i + 1];
                Indices[i + 1] = Indices[i + 2];
                Indices[i + 2] = tmp;
            }
        }

        // Ограничивающий параллелепипед; исключение при пустой сетке
        public void GetBounds(out Vec3d min, out Vec3d max)
        {
            if (Vertices.Count == 0)
            {
                throw new InvalidOperationException("Сетка пуста — границы неопределены.");
            }

            double minX = Vertices[0].X;
            double minY = Vertices[0].Y;
            double minZ = Vertices[0].Z;
            double maxX = minX;
            double maxY = minY;
            double maxZ = minZ;

            for (int i = 1; i < Vertices.Count; i++)
            {
                Vec3d v = Vertices[i];
                if (v.X < minX) { minX = v.X; }
                if (v.Y < minY) { minY = v.Y; }
                if (v.Z < minZ) { minZ = v.Z; }
                if (v.X > maxX) { maxX = v.X; }
                if (v.Y > maxY) { maxY = v.Y; }
                if (v.Z > maxZ) { maxZ = v.Z; }
            }

            min = new Vec3d(minX, minY, minZ);
            max = new Vec3d(maxX, maxY, maxZ);
        }

        // Догрузить другую сетку (вершины + смещённые индексы)
        public void Append(Mesh other)
        {
            int offset = Vertices.Count;
            Vertices.AddRange(other.Vertices);
            for (int i = 0; i < other.Indices.Count; i++)
            {
                Indices.Add(other.Indices[i] + offset);
            }
        }
    }

    // Сварка: объединение близлежащих вершин через грид-хэш
    public static class MeshWelder
    {
        // Ключ 3D-ячейки грида
        private struct CellKey
        {
            public int X;
            public int Y;
            public int Z;

            public override bool Equals(object obj)
            {
                if (obj is CellKey o)
                {
                    return X == o.X && Y == o.Y && Z == o.Z;
                }
                return false;
            }

            public override int GetHashCode()
            {
                int h = X;
                h = (h * 397) ^ Y;
                h = (h * 397) ^ Z;
                return h;
            }
        }

        // Сварка: вершины ближе tolerance объединяются, вырожденные треугольники удаляются
        public static Mesh Weld(Mesh src, double tolerance)
        {
            if (tolerance <= 0.0)
            {
                tolerance = 1e-12;
            }

            double tolSq = tolerance * tolerance;
            double invTol = 1.0 / tolerance;

            // Грид: ячейка -> список индексов уже принятых вершин
            Dictionary<CellKey, List<int>> grid = new Dictionary<CellKey, List<int>>();
            List<Vec3d> newVerts = new List<Vec3d>();
            int[] indexMap = new int[src.Vertices.Count];

            for (int vi = 0; vi < src.Vertices.Count; vi++)
            {
                Vec3d v = src.Vertices[vi];
                int cx = (int)Math.Floor(v.X * invTol);
                int cy = (int)Math.Floor(v.Y * invTol);
                int cz = (int)Math.Floor(v.Z * invTol);

                int mergedIdx = -1;

                // Обход 27 соседних ячеек
                for (int dx = -1; dx <= 1 && mergedIdx < 0; dx++)
                {
                    for (int dy = -1; dy <= 1 && mergedIdx < 0; dy++)
                    {
                        for (int dz = -1; dz <= 1 && mergedIdx < 0; dz++)
                        {
                            CellKey key = new CellKey();
                            key.X = cx + dx;
                            key.Y = cy + dy;
                            key.Z = cz + dz;

                            List<int> bucket;
                            if (grid.TryGetValue(key, out bucket))
                            {
                                for (int j = 0; j < bucket.Count; j++)
                                {
                                    Vec3d ex = newVerts[bucket[j]];
                                    double ddx = v.X - ex.X;
                                    double ddy = v.Y - ex.Y;
                                    double ddz = v.Z - ex.Z;
                                    if (ddx * ddx + ddy * ddy + ddz * ddz <= tolSq)
                                    {
                                        mergedIdx = bucket[j];
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }

                if (mergedIdx < 0)
                {
                    // Новая уникальная вершина
                    mergedIdx = newVerts.Count;
                    newVerts.Add(v);

                    CellKey ck = new CellKey();
                    ck.X = cx;
                    ck.Y = cy;
                    ck.Z = cz;

                    List<int> bucket;
                    if (!grid.TryGetValue(ck, out bucket))
                    {
                        bucket = new List<int>();
                        grid[ck] = bucket;
                    }
                    bucket.Add(mergedIdx);
                }

                indexMap[vi] = mergedIdx;
            }

            // Сборка результата; пропуск вырожденных треугольников
            Mesh result = new Mesh();
            result.Vertices.AddRange(newVerts);

            for (int i = 0; i < src.Indices.Count; i += 3)
            {
                int i0 = indexMap[src.Indices[i]];
                int i1 = indexMap[src.Indices[i + 1]];
                int i2 = indexMap[src.Indices[i + 2]];

                // Две совпадающие вершины — треугольник вырожден
                if (i0 == i1 || i0 == i2 || i1 == i2)
                {
                    continue;
                }

                result.Indices.Add(i0);
                result.Indices.Add(i1);
                result.Indices.Add(i2);
            }

            return result;
        }
    }
}
