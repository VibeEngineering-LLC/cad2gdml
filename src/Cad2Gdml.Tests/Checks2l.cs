using System;
using System.Collections.Generic;
using System.Globalization;
using Cad2Gdml.Geometry;
using Cad2Gdml.Meshes;

namespace Cad2Gdml.Tests
{
    // Проверки 2l: ремонт сливеров и TJunction
    public static class Checks2l
    {
        public static List<KeyValuePair<string, Action>> All(string tempDir)
        {
            var list = new List<KeyValuePair<string, Action>>();
            list.Add(new KeyValuePair<string, Action>("sliver_tjunction_repaired", () => SliverTjunctionRepaired()));
            list.Add(new KeyValuePair<string, Action>("sliver_clean_mesh_untouched", () => SliverCleanMeshUntouched()));
            return list;
        }

        // Куб 10×10×10, 8 вершин, 12 треугольников наружу
        private static Mesh BuildCube()
        {
            Mesh m = new Mesh();
            m.Vertices.Add(new Vec3d(0, 0, 0));
            m.Vertices.Add(new Vec3d(10, 0, 0));
            m.Vertices.Add(new Vec3d(10, 10, 0));
            m.Vertices.Add(new Vec3d(0, 10, 0));
            m.Vertices.Add(new Vec3d(0, 0, 10));
            m.Vertices.Add(new Vec3d(10, 0, 10));
            m.Vertices.Add(new Vec3d(10, 10, 10));
            m.Vertices.Add(new Vec3d(0, 10, 10));

            // Низ (−z)
            m.Indices.Add(0); m.Indices.Add(2); m.Indices.Add(1);
            m.Indices.Add(0); m.Indices.Add(3); m.Indices.Add(2);
            // Верх (+z)
            m.Indices.Add(4); m.Indices.Add(5); m.Indices.Add(6);
            m.Indices.Add(4); m.Indices.Add(6); m.Indices.Add(7);
            // Перед (−y)
            m.Indices.Add(0); m.Indices.Add(1); m.Indices.Add(5);
            m.Indices.Add(0); m.Indices.Add(5); m.Indices.Add(4);
            // Зад (+y)
            m.Indices.Add(3); m.Indices.Add(7); m.Indices.Add(6);
            m.Indices.Add(3); m.Indices.Add(6); m.Indices.Add(2);
            // Лево (−x)
            m.Indices.Add(0); m.Indices.Add(4); m.Indices.Add(7);
            m.Indices.Add(0); m.Indices.Add(7); m.Indices.Add(3);
            // Право (+x)
            m.Indices.Add(1); m.Indices.Add(2); m.Indices.Add(6);
            m.Indices.Add(1); m.Indices.Add(6); m.Indices.Add(5);

            return m;
        }

        // Число треугольников с площадью < 1e-9
        private static int CountZeroArea(Mesh m)
        {
            int count = 0;
            for (int i = 0; i < m.Indices.Count; i += 3)
            {
                Vec3d a = m.Vertices[m.Indices[i]];
                Vec3d b = m.Vertices[m.Indices[i + 1]];
                Vec3d c = m.Vertices[m.Indices[i + 2]];
                double area = (b - a).Cross(c - a).Length() * 0.5;
                if (area < 1e-9) count++;
            }
            return count;
        }

        // Слив T1=(A,C,X) и T2=(C,A,Y), добавление сливера S=(C,M,A)
        private static void SliverTjunctionRepaired()
        {
            // Вершины: 0=A(0,0,0), 1=C(10,0,0), 2=Y(10,10,0), 3..7 как в кубе, 8=M(5,0,0)
            Mesh mesh = new Mesh();
            mesh.Vertices.Add(new Vec3d(0, 0, 0));    // 0 = A
            mesh.Vertices.Add(new Vec3d(10, 0, 0));   // 1 = C
            mesh.Vertices.Add(new Vec3d(10, 10, 0));  // 2 = Y
            mesh.Vertices.Add(new Vec3d(0, 10, 0));   // 3
            mesh.Vertices.Add(new Vec3d(0, 0, 10));   // 4
            mesh.Vertices.Add(new Vec3d(10, 0, 10));  // 5 = X
            mesh.Vertices.Add(new Vec3d(10, 10, 10)); // 6
            mesh.Vertices.Add(new Vec3d(0, 10, 10));  // 7
            mesh.Vertices.Add(new Vec3d(5, 0, 0));    // 8 = M (середина A–C)

            // Низ: T2=(0,2,1) оставляем, вторая нижняя (0,3,2)
            mesh.Indices.Add(0); mesh.Indices.Add(2); mesh.Indices.Add(1);
            mesh.Indices.Add(0); mesh.Indices.Add(3); mesh.Indices.Add(2);
            // Верх
            mesh.Indices.Add(4); mesh.Indices.Add(5); mesh.Indices.Add(6);
            mesh.Indices.Add(4); mesh.Indices.Add(6); mesh.Indices.Add(7);
            // Перед: T1=(0,1,5) заменяем на (0,8,5) и (8,1,5)
            mesh.Indices.Add(0); mesh.Indices.Add(8); mesh.Indices.Add(5);
            mesh.Indices.Add(8); mesh.Indices.Add(1); mesh.Indices.Add(5);
            // Перед, вторая
            mesh.Indices.Add(0); mesh.Indices.Add(5); mesh.Indices.Add(4);
            // Зад
            mesh.Indices.Add(3); mesh.Indices.Add(7); mesh.Indices.Add(6);
            mesh.Indices.Add(3); mesh.Indices.Add(6); mesh.Indices.Add(2);
            // Лево
            mesh.Indices.Add(0); mesh.Indices.Add(4); mesh.Indices.Add(7);
            mesh.Indices.Add(0); mesh.Indices.Add(7); mesh.Indices.Add(3);
            // Право
            mesh.Indices.Add(1); mesh.Indices.Add(2); mesh.Indices.Add(6);
            mesh.Indices.Add(1); mesh.Indices.Add(6); mesh.Indices.Add(5);
            // Слив S=(C,M,A) — коллинеарный, площадь = 0
            mesh.Indices.Add(1); mesh.Indices.Add(8); mesh.Indices.Add(0);

            // До ремонта: ровно один треугольник с площадью < 1e-9
            int zeroBefore = CountZeroArea(mesh);
            if (zeroBefore != 1)
                throw new Exception("sliver_tjunction_repaired: треугольников с area<1e-9 до ремонта = "
                    + zeroBefore.ToString(CultureInfo.InvariantCulture) + ", ожидаем 1");

            // Ремонт
            int n;
            Mesh result = SliverRepair.Fix(mesh, out n);

            if (n != 1)
                throw new Exception("sliver_tjunction_repaired: fixedCount = "
                    + n.ToString(CultureInfo.InvariantCulture) + ", ожидаем 1");

            int triCount = result.TriangleCount;
            if (triCount != 14)
                throw new Exception("sliver_tjunction_repaired: треугольников после ремонта = "
                    + triCount.ToString(CultureInfo.InvariantCulture) + ", ожидаем 14");

            int zeroAfter = CountZeroArea(result);
            if (zeroAfter != 0)
                throw new Exception("sliver_tjunction_repaired: треугольников с area<1e-9 после ремонта = "
                    + zeroAfter.ToString(CultureInfo.InvariantCulture) + ", ожидаем 0");

            TopologyReport topo = MeshTopology.Analyze(result);
            if (topo.OpenEdges != 0)
                throw new Exception("sliver_tjunction_repaired: OpenEdges = "
                    + topo.OpenEdges.ToString(CultureInfo.InvariantCulture) + ", ожидаем 0");
            if (topo.NonManifoldEdges != 0)
                throw new Exception("sliver_tjunction_repaired: NonManifoldEdges = "
                    + topo.NonManifoldEdges.ToString(CultureInfo.InvariantCulture) + ", ожидаем 0");
            if (topo.InconsistentEdges != 0)
                throw new Exception("sliver_tjunction_repaired: InconsistentEdges = "
                    + topo.InconsistentEdges.ToString(CultureInfo.InvariantCulture) + ", ожидаем 0");

            double vol = MeshProperties.SignedVolume(result);
            if (Math.Abs(vol - 1000.0) > 1e-6)
                throw new Exception("sliver_tjunction_repaired: Volume = "
                    + vol.ToString(CultureInfo.InvariantCulture) + ", ожидаем 1000.0 ± 1e-6");
        }

        // Чистый куб — Fix возвращает тот же объект и count=0
        private static void SliverCleanMeshUntouched()
        {
            Mesh cube = BuildCube();

            int n;
            Mesh result = SliverRepair.Fix(cube, out n);

            if (n != 0)
                throw new Exception("sliver_clean_mesh_untouched: fixedCount = "
                    + n.ToString(CultureInfo.InvariantCulture) + ", ожидаем 0");

            if (!ReferenceEquals(result, cube))
                throw new Exception("sliver_clean_mesh_untouched: возвращён не тот же объект");
        }
    }
}
