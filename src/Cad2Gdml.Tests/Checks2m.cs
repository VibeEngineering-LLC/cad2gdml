using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Cad2Gdml.Geometry;
using Cad2Gdml.Pipeline;
using Cad2Gdml.Scene;
using Cad2Gdml.Sketch;

namespace Cad2Gdml.Tests
{
    // Проверки 2m: шов в плоскости y=0 не дробит корпус на отдельные тела
    public static class Checks2m
    {
        public static List<KeyValuePair<string, Action>> All(string tempDir)
        {
            var list = new List<KeyValuePair<string, Action>>();
            list.Add(new KeyValuePair<string, Action>("sketch_seam_in_plane", () => SketchSeamInPlane(tempDir)));
            list.Add(new KeyValuePair<string, Action>("stl_multibody_not_split", () => StlMultibodyNotSplit(tempDir)));
            return list;
        }

        // STL с двумя отдельными телами остаётся одной деталью + предупреждение (#CG-33)
        private static void StlMultibodyNotSplit(string tempDir)
        {
            string dir = System.IO.Path.Combine(tempDir, "stl_multibody");
            System.IO.Directory.CreateDirectory(dir);
            var tris = new List<double[]>(Fixtures.Box(10, 10, 10, 0, 0, 0));
            tris.AddRange(Fixtures.Box(10, 10, 10, 50, 0, 0));
            Fixtures.WriteBinaryStl(System.IO.Path.Combine(dir, "pair.stl"), tris);
            PipelineResult r = CheckUtil.Run(dir, new[] { "pair.stl" }, 1.0, "+z", true, false);
            int warn = 0, parts = 0;
            for (int i = 0; i < r.Scene.Diagnostics.Count; i++)
                if (r.Scene.Diagnostics[i].Code == "W_STL_MULTIBODY") warn++;
            for (int i = 0; i < r.Scene.Parts.Count; i++)
                if (r.Scene.Parts[i].Name.StartsWith("pair")) parts++;
            if (warn != 1 || parts != 1)
                throw new Exception("stl_multibody_not_split: W_STL_MULTIBODY=" + warn + ", деталей=" + parts + ", ожидаем 1 и 1");
        }

        // Корпус с рёбрами в плоскости y=0: шов не создаёт отдельный регион
        private static void SketchSeamInPlane(string tempDir)
        {
            string dir = Path.Combine(tempDir, "seam_plane");
            Directory.CreateDirectory(dir);

            // Кристалл: коробка 10×10×20 в центре
            Fixtures.WriteBinaryStl(Path.Combine(dir, "Crystal_NaI.stl"),
                Fixtures.Box(10, 10, 20, 0, 0, 0));

            // Корпус 40×40×60, боковые грани разбиты по y=0
            Fixtures.WriteBinaryStl(Path.Combine(dir, "Housing_Al.stl"), BuildSplitHousing());

            // Конвейер: NoRepair=true уже внутри CheckUtil.Run
            PipelineResult r = CheckUtil.Run(dir,
                new[] { "Crystal_NaI.stl", "Housing_Al.stl" },
                1.0, "+z", true, false);

            // Строим эскиз сечения
            SketchModel sketch = SketchBuilder.Build(r.Scene);

            // Собираем регионы Housing
            List<SketchRegion> housing = new List<SketchRegion>();
            if (sketch != null && sketch.Regions != null)
            {
                for (int i = 0; i < sketch.Regions.Count; i++)
                {
                    if (sketch.Regions[i].Role == PartRole.Housing && sketch.Regions[i].Label == null)
                        housing.Add(sketch.Regions[i]);
                }
            }

            if (housing.Count == 0)
                throw new Exception("sketch_seam_in_plane: нет ни одного региона PartRole.Housing");

            if (housing.Count != 1)
                throw new Exception("sketch_seam_in_plane: регионов Housing = "
                    + housing.Count.ToString(CultureInfo.InvariantCulture) + ", ожидаем 1");

            SketchRegion region = housing[0];

            if (!region.IsClosed)
                throw new Exception("sketch_seam_in_plane: IsClosed = false, ожидаем true");

            // Граничный框 по X и Z
            double minX = double.MaxValue, maxX = double.MinValue;
            double minZ = double.MaxValue, maxZ = double.MinValue;
            foreach (Vec3d p in region.Points)
            {
                if (p.X < minX) minX = p.X;
                if (p.X > maxX) maxX = p.X;
                if (p.Z < minZ) minZ = p.Z;
                if (p.Z > maxZ) maxZ = p.Z;
            }

            double widthX = maxX - minX;
            double heightZ = maxZ - minZ;

            if (Math.Abs(widthX - 40.0) > 0.01)
                throw new Exception("sketch_seam_in_plane: ширина по X = "
                    + widthX.ToString(CultureInfo.InvariantCulture) + ", ожидаем 40 ± 0.01");

            if (Math.Abs(heightZ - 60.0) > 0.01)
                throw new Exception("sketch_seam_in_plane: высота по Z = "
                    + heightZ.ToString(CultureInfo.InvariantCulture) + ", ожидаем 60 ± 0.01");
        }

        // Корпус 40×40×60: 4 боковые грани × 2 прямоугольника + 2 торца = 20 треугольников
        private static IList<double[]> BuildSplitHousing()
        {
            var t = new List<double[]>();

            // x = -20 (нормаль -x)
            t.Add(new double[] { -20,-20,-30, -20,0,30, -20,0,-30 });
            t.Add(new double[] { -20,-20,-30, -20,-20,30, -20,0,30 });
            t.Add(new double[] { -20,0,-30, -20,20,30, -20,20,-30 });
            t.Add(new double[] { -20,0,-30, -20,0,30, -20,20,30 });

            // x = +20 (нормаль +x)
            t.Add(new double[] { 20,-20,-30, 20,0,-30, 20,0,30 });
            t.Add(new double[] { 20,-20,-30, 20,0,30, 20,-20,30 });
            t.Add(new double[] { 20,0,-30, 20,20,-30, 20,20,30 });
            t.Add(new double[] { 20,0,-30, 20,20,30, 20,0,30 });

            // z = -30 (нормаль -z)
            t.Add(new double[] { -20,-20,-30, 20,0,-30, 20,-20,-30 });
            t.Add(new double[] { -20,-20,-30, -20,0,-30, 20,0,-30 });
            t.Add(new double[] { -20,0,-30, 20,20,-30, 20,0,-30 });
            t.Add(new double[] { -20,0,-30, -20,20,-30, 20,20,-30 });

            // z = +30 (нормаль +z)
            t.Add(new double[] { -20,-20,30, 20,-20,30, 20,0,30 });
            t.Add(new double[] { -20,-20,30, 20,0,30, -20,0,30 });
            t.Add(new double[] { -20,0,30, 20,0,30, 20,20,30 });
            t.Add(new double[] { -20,0,30, 20,20,30, -20,20,30 });

            // y = +20 (нормаль +y), цельная грань
            t.Add(new double[] { -20,20,-30, 20,20,30, 20,20,-30 });
            t.Add(new double[] { -20,20,-30, -20,20,30, 20,20,30 });

            // y = -20 (нормаль -y), цельная грань
            t.Add(new double[] { -20,-20,-30, 20,-20,-30, 20,-20,30 });
            t.Add(new double[] { -20,-20,-30, 20,-20,30, -20,-20,30 });

            return t;
        }
    }
}
