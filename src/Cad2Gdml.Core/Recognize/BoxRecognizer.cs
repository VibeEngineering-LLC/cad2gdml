using System;
using System.Collections.Generic;
using System.Globalization;
using Cad2Gdml.Geometry;
using Cad2Gdml.Meshes;
using Cad2Gdml.Scene;

namespace Cad2Gdml.Recognize
{
    // Распознавание бруска, выровненного по осям кадра
    public static class BoxRecognizer
    {
        public static SolidShape TryRecognize(Part part, double tolMm, out string whyNot)
        {
            whyNot = null;

            // Проверка входных данных
            if (part == null)
            {
                whyNot = "Деталь не задана.";
                return null;
            }

            if (part.Mesh == null || part.Mesh.Vertices.Count == 0)
            {
                whyNot = "Сетка пуста.";
                return null;
            }

            // Гранирующий параллелепипед
            Vec3d min, max;
            part.Mesh.GetBounds(out min, out max);

            double bx = max.X - min.X;
            double by = max.Y - min.Y;
            double bz = max.Z - min.Z;

            // Объём ограничивающего бокса
            double bboxVol = bx * by * bz;
            if (bboxVol <= 0.0)
            {
                whyNot = "Вырожденный бокс (нулевой объём).";
                return null;
            }

            // Объём сетки из Part
            double meshVol = part.VolumeMm3;
            if (meshVol <= 0.0)
            {
                whyNot = "Объём сетки не определён или нулевой.";
                return null;
            }

            // Условие 1: объёмы совпадают с относительной погрешностью 1e-4
            double relDiff = Math.Abs(bboxVol - meshVol) / bboxVol;
            if (relDiff > 1e-4)
            {
                whyNot = "Объём сетки (" + meshVol.ToString("F6", CultureInfo.InvariantCulture)
                         + " мм³) не совпадает с объёмом бокса (" + bboxVol.ToString("F6", CultureInfo.InvariantCulture)
                         + " мм³) в пределах 1e-4.";
                return null;
            }

            // Условие 2: каждая вершина — угол бокса (каждая координата на мин/макс)
            for (int i = 0; i < part.Mesh.Vertices.Count; i++)
            {
                Vec3d v = part.Mesh.Vertices[i];

                bool xOk = Math.Abs(v.X - min.X) <= tolMm || Math.Abs(v.X - max.X) <= tolMm;
                bool yOk = Math.Abs(v.Y - min.Y) <= tolMm || Math.Abs(v.Y - max.Y) <= tolMm;
                bool zOk = Math.Abs(v.Z - min.Z) <= tolMm || Math.Abs(v.Z - max.Z) <= tolMm;

                if (!xOk || !yOk || !zOk)
                {
                    whyNot = "Вершина " + i.ToString(CultureInfo.InvariantCulture)
                             + " (" + v.ToString() + ") не является углом бокса.";
                    return null;
                }
            }

            // Успех: формируем SolidShape вида Box
            double cx = (min.X + max.X) * 0.5;
            double cy = (min.Y + max.Y) * 0.5;
            double cz = (min.Z + max.Z) * 0.5;

            SolidShape shape = new SolidShape();
            shape.Kind = SolidKind.Box;
            shape.BoxX = bx;
            shape.BoxY = by;
            shape.BoxZ = bz;
            shape.X0 = cx;
            shape.Y0 = cy;
            shape.Z0 = cz;
            shape.Z1 = cz;

            return shape;
        }
    }
}
