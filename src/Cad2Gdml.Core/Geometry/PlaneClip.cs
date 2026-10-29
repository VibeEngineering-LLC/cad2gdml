namespace Cad2Gdml.Geometry
{
    // Клип треугольника по полупространству coord(axis) <= planePos
    public static class PlaneClip
    {
        // Sutherland-Hodgman: возвращает 0, 1 или 2 треугольника в outTris (до 6 Vec3d)
        public static int ClipTriangle(Vec3d a, Vec3d b, Vec3d c, int axis, double planePos, Vec3d[] outTris)
        {
            // Координаты вдоль выбранной оси
            double[] coord =
            {
                axis == 0 ? a.X : (axis == 1 ? a.Y : a.Z),
                axis == 0 ? b.X : (axis == 1 ? b.Y : b.Z),
                axis == 0 ? c.X : (axis == 1 ? c.Y : c.Z)
            };
            bool[] inside =
            {
                coord[0] <= planePos,
                coord[1] <= planePos,
                coord[2] <= planePos
            };
            Vec3d[] src = { a, b, c };

            // Выходной буфер (не более 4 вершин после клипа)
            Vec3d[] outV = new Vec3d[4];
            int cn = 0;

            // Обход трёх рёбер: 0->1, 1->2, 2->0
            for (int e = 0; e < 3; e++)
            {
                int i = e;
                int j = (e + 1) % 3;

                if (inside[i] && inside[j])
                {
                    // Оба внутри: берём конечную вершину
                    outV[cn++] = src[j];
                }
                else if (inside[i] && !inside[j])
                {
                    // Изнутри наружу: точка пересечения
                    double t = (planePos - coord[i]) / (coord[j] - coord[i]);
                    outV[cn++] = src[i] + t * (src[j] - src[i]);
                }
                else if (!inside[i] && inside[j])
                {
                    // Снаружи внутрь: пересечение, затем вершина
                    double t = (planePos - coord[i]) / (coord[j] - coord[i]);
                    outV[cn++] = src[i] + t * (src[j] - src[i]);
                    outV[cn++] = src[j];
                }
                // Оба снаружи: ничего не добавляем
            }

            if (cn < 3) return 0;

            if (cn == 3)
            {
                outTris[0] = outV[0];
                outTris[1] = outV[1];
                outTris[2] = outV[2];
                return 1;
            }

            // Четыре вершины: fan от outV[0]
            outTris[0] = outV[0];
            outTris[1] = outV[1];
            outTris[2] = outV[2];
            outTris[3] = outV[0];
            outTris[4] = outV[2];
            outTris[5] = outV[3];
            return 2;
        }
    }
}
