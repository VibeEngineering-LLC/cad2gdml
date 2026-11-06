using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Cad2Gdml.Geometry;
using Cad2Gdml.Import;
using Cad2Gdml.Meshes;
using Cad2Gdml.Scene;

namespace Cad2Gdml.Tests
{
    public static class ChecksOverlap
    {
        public static List<KeyValuePair<string, Action>> All(string tempDir)
        {
            var list = new List<KeyValuePair<string, Action>>();
            list.Add(new KeyValuePair<string, Action>("overlap_boxes_flagged", () => OverlapBoxesFlagged(tempDir)));
            list.Add(new KeyValuePair<string, Action>("overlap_touch_face_ok", () => OverlapTouchFaceOk(tempDir)));
            list.Add(new KeyValuePair<string, Action>("overlap_far_ok", () => OverlapFarOk(tempDir)));
            list.Add(new KeyValuePair<string, Action>("overlap_inside_flagged", () => OverlapInsideFlagged(tempDir)));
            list.Add(new KeyValuePair<string, Action>("overlap_cavity_ok", () => OverlapCavityOk(tempDir)));
            list.Add(new KeyValuePair<string, Action>("overlap_open_mesh_skipped", () => OverlapOpenMeshSkipped(tempDir)));
            list.Add(new KeyValuePair<string, Action>("overlap_gap_vs_penetration", () => OverlapGapVsPenetration(tempDir)));
            list.Add(new KeyValuePair<string, Action>("gdml_import_counts_and_closed", () => GdmlImportCountsAndClosed(tempDir)));
            list.Add(new KeyValuePair<string, Action>("gdml_polycone_volume", () => GdmlPolyconeVolume(tempDir)));
            list.Add(new KeyValuePair<string, Action>("gdml_overlap_detected", () => GdmlOverlapDetected(tempDir)));
            return list;
        }

        // --- Тест 1: две коробки пересекаются ---
        private static void OverlapBoxesFlagged(string tempDir)
        {
            var a = P("A", Box(0, 0, 0, 10, 10, 10));
            var b = P("B", Box(7, 2, 2, 17, 8, 8));
            var s = Scene(a, b);
            OverlapCheck.Run(s);
            if (Count(s, "W_OVERLAP") != 1)
                throw new Exception("overlap_boxes_flagged: ожидалось W_OVERLAP == 1, получено " + Count(s, "W_OVERLAP"));
            if (Count(s, "W_INSIDE") != 0)
                throw new Exception("overlap_boxes_flagged: ожидалось W_INSIDE == 0, получено " + Count(s, "W_INSIDE"));
            var diag = s.Diagnostics.FirstOrDefault(d => d.Code == "W_OVERLAP");
            if (diag == null || (!diag.PartName.Contains("A") || !diag.PartName.Contains("B")))
                throw new Exception("overlap_boxes_flagged: PartName не содержит оба имени");
        }

        // --- Тест 2: касание гранью ---
        private static void OverlapTouchFaceOk(string tempDir)
        {
            var a = P("A", Box(0, 0, 0, 10, 10, 10));
            var b = P("B", Box(10, 0, 0, 20, 10, 10));
            var s = Scene(a, b);
            OverlapCheck.Run(s);
            if (Count(s, "W_OVERLAP") != 0)
                throw new Exception("overlap_touch_face_ok: касание гранью не должно давать W_OVERLAP");
            if (Count(s, "W_INSIDE") != 0)
                throw new Exception("overlap_touch_face_ok: касание гранью не должно давать W_INSIDE");
        }

        // --- Тест 3: коробки далеко друг от друга ---
        private static void OverlapFarOk(string tempDir)
        {
            var a = P("A", Box(0, 0, 0, 10, 10, 10));
            var b = P("B", Box(100, 100, 100, 110, 110, 110));
            var s = Scene(a, b);
            OverlapCheck.Run(s);
            if (Count(s, "W_OVERLAP") != 0)
                throw new Exception("overlap_far_ok: далёкие коробки не должны давать W_OVERLAP");
            if (Count(s, "W_INSIDE") != 0)
                throw new Exception("overlap_far_ok: далёкие коробки не должны давать W_INSIDE");
        }

        // --- Тест 4: одна коробка внутри другой ---
        private static void OverlapInsideFlagged(string tempDir)
        {
            var a = P("Outer", Box(0, 0, 0, 100, 100, 100));
            var b = P("Inner", Box(40, 40, 40, 60, 60, 60));
            var s = Scene(a, b);
            OverlapCheck.Run(s);
            if (Count(s, "W_OVERLAP") != 0)
                throw new Exception("overlap_inside_flagged: вложенность не должна давать W_OVERLAP");
            if (Count(s, "W_INSIDE") != 1)
                throw new Exception("overlap_inside_flagged: ожидалось W_INSIDE == 1, получено " + Count(s, "W_INSIDE"));
            var diag = s.Diagnostics.FirstOrDefault(d => d.Code == "W_INSIDE");
            if (diag == null || diag.PartName != "Inner")
                throw new Exception("overlap_inside_flagged: PartName должен быть 'Inner'");
        }

        // --- Тест 5: полость ---
        private static void OverlapCavityOk(string tempDir)
        {
            var outer = Box(0, 0, 0, 100, 100, 100);
            var cav = Box(20, 20, 20, 80, 80, 80);
            cav.FlipWinding();
            outer.Append(cav);
            var a = P("Cavity", outer);
            var b = P("B", Box(40, 40, 40, 60, 60, 60));
            var s = Scene(a, b);
            OverlapCheck.Run(s);
            if (Count(s, "W_OVERLAP") != 0)
                throw new Exception("overlap_cavity_ok: тело в полости не должно давать W_OVERLAP");
            if (Count(s, "W_INSIDE") != 0)
                throw new Exception("overlap_cavity_ok: тело в полости не должно давать W_INSIDE");
        }

        // --- Тест 6: открытая сетка пропускается ---
        private static void OverlapOpenMeshSkipped(string tempDir)
        {
            var a = P("A", Box(0, 0, 0, 10, 10, 10));
            var b = P("B", Box(7, 2, 2, 17, 8, 8));
            b.MeshOpen = true;
            var s = Scene(a, b);
            OverlapCheck.Run(s);
            if (Count(s, "W_OVERLAP") != 0)
                throw new Exception("overlap_open_mesh_skipped: открытая сетка не должна давать W_OVERLAP");
            if (Count(s, "W_INSIDE") != 0)
                throw new Exception("overlap_open_mesh_skipped: открытая сетка не должна давать W_INSIDE");
        }

        // --- Тест 7: зазор vs проникновение ---
        private static void OverlapGapVsPenetration(string tempDir)
        {
            // Проникновение 0.5
            var a1 = P("A", Box(0, 0, 0, 10, 10, 10));
            var b1 = P("B1", Box(9.5, 2, 2, 19.5, 8, 8));
            var s1 = Scene(a1, b1);
            OverlapCheck.Run(s1);
            if (Count(s1, "W_OVERLAP") != 1)
                throw new Exception("overlap_gap_vs_penetration: проникновение 0.5 должно дать W_OVERLAP == 1");

            // Зазор 0.5
            var a2 = P("A", Box(0, 0, 0, 10, 10, 10));
            var b2 = P("B2", Box(10.5, 2, 2, 20.5, 8, 8));
            var s2 = Scene(a2, b2);
            OverlapCheck.Run(s2);
            if (Count(s2, "W_OVERLAP") != 0)
                throw new Exception("overlap_gap_vs_penetration: зазор 0.5 не должен давать W_OVERLAP");
        }

        // --- Тест 8a: импорт GDML, количество и замкнутость ---
        private static void GdmlImportCountsAndClosed(string tempDir)
        {
            var path = WriteGdml(tempDir);
            IList<string> skipped;
            var parts = GdmlImporter.Load(path, out skipped);
            if (parts.Count != 2)
                throw new Exception("gdml_import_counts_and_closed: ожидалось 2 детали, получено " + parts.Count);
            foreach (var p in parts)
            {
                var welded = MeshWelder.Weld(p.Mesh, 1e-6);
                var topo = MeshTopology.Analyze(welded);
                if (!topo.IsClosedManifold)
                    throw new Exception("gdml_import_counts_and_closed: деталь " + p.Name + " не является замкнутым многообразием");
            }
        }

        // --- Тест 8b: объёмы поликонуса и цилиндра ---
        private static void GdmlPolyconeVolume(string tempDir)
        {
            var path = WriteGdml(tempDir);
            IList<string> skipped;
            var parts = GdmlImporter.Load(path, out skipped);
            var t1 = parts.FirstOrDefault(p => p.Name == "T1v");
            var pc = parts.FirstOrDefault(p => p.Name == "PCv");
            if (t1 == null || pc == null)
                throw new Exception("gdml_polycone_volume: не найдены детали T1v или PCv");

            // Цилиндр r=10, h=20: V = pi*100*20
            double t1Exact = Math.PI * 100.0 * 20.0;
            double t1Vol = Volume(t1.Mesh);
            CheckVolume("gdml_polycone_volume: T1v", t1Vol, t1Exact);

            // Труба rmin=5, rmax=10, h=20: V = pi*(100-25)*20
            double pcExact = Math.PI * (100.0 - 25.0) * 20.0;
            double pcVol = Volume(pc.Mesh);
            CheckVolume("gdml_polycone_volume: PCv", pcVol, pcExact);
        }

        private static void CheckVolume(string label, double measured, double exact)
        {
            // Поправка на многоугольник из 48 сегментов: sin(2*pi/48)*48/(2*pi)
            double n = 48.0;
            double factor = Math.Sin(2.0 * Math.PI / n) * n / (2.0 * Math.PI);
            double expected = exact * factor;
            double tol = 0.005 * expected; // ±0.5 %
            if (Math.Abs(measured - expected) > tol)
                throw new Exception(label + ": объём " + measured.ToString("F3", CultureInfo.InvariantCulture)
                    + " вне допуска от " + expected.ToString("F3", CultureInfo.InvariantCulture));
        }

        // --- Тест 8c: перекрытие цилиндра и трубы ---
        private static void GdmlOverlapDetected(string tempDir)
        {
            var path = WriteGdml(tempDir);
            IList<string> skipped;
            var parts = GdmlImporter.Load(path, out skipped);
            var scene = new SceneModel(); scene.Parts.AddRange(parts);
            OverlapCheck.Run(scene);
            int cnt = Count(scene, "W_OVERLAP");
            if (cnt != 1)
                throw new Exception("gdml_overlap_detected: ожидалось W_OVERLAP == 1, получено " + cnt);
        }

        // --- Вспомогательные методы ---

        // Замкнутая коробка, нормали наружу
        private static Mesh Box(double x0, double y0, double z0, double x1, double y1, double z1)
        {
            var m = new Mesh();
            var v0 = new Vec3d(x0, y0, z0);
            var v1 = new Vec3d(x1, y0, z0);
            var v2 = new Vec3d(x1, y1, z0);
            var v3 = new Vec3d(x0, y1, z0);
            var v4 = new Vec3d(x0, y0, z1);
            var v5 = new Vec3d(x1, y0, z1);
            var v6 = new Vec3d(x1, y1, z1);
            var v7 = new Vec3d(x0, y1, z1);

            // Нижняя грань (нормаль -Z)
            m.AddTriangle(v0, v2, v1);
            m.AddTriangle(v0, v3, v2);
            // Верхняя грань (нормаль +Z)
            m.AddTriangle(v4, v5, v6);
            m.AddTriangle(v4, v6, v7);
            // Передняя грань y=y0 (нормаль -Y)
            m.AddTriangle(v0, v5, v4);
            m.AddTriangle(v0, v1, v5);
            // Задняя грань y=y1 (нормаль +Y)
            m.AddTriangle(v2, v7, v6);
            m.AddTriangle(v2, v3, v7);
            // Левая грань x=x0 (нормаль -X)
            m.AddTriangle(v0, v4, v7);
            m.AddTriangle(v0, v7, v3);
            // Правая грань x=x1 (нормаль +X)
            m.AddTriangle(v1, v6, v5);
            m.AddTriangle(v1, v2, v6);
            return m;
        }

        private static SceneModel Scene(params Part[] parts)
        {
            var sm = new SceneModel(); sm.Parts.AddRange(parts); return sm;
        }

        private static Part P(string name, Mesh m)
        {
            var p = new Part();
            p.Name = name;
            p.Mesh = m;
            p.MeshOpen = false;
            return p;
        }

        private static int Count(SceneModel s, string code)
        {
            return s.Diagnostics.Count(d => d.Code == code);
        }

        // Объём по сумме знаковых объёмов тетраэдров
        private static double Volume(Mesh m)
        {
            double vol = 0.0;
            for (int i = 0; i < m.TriangleCount; i++)
            {
                Vec3d a, b, c;
                m.GetTriangle(i, out a, out b, out c);
                // V = a · (b × c) / 6
                double bx_c = b.Y * c.Z - b.Z * c.Y;
                double by_c = b.Z * c.X - b.X * c.Z;
                double bz_c = b.X * c.Y - b.Y * c.X;
                vol += (a.X * bx_c + a.Y * by_c + a.Z * bz_c) / 6.0;
            }
            return Math.Abs(vol);
        }

        // Запись GDML-файла
        private static string WriteGdml(string tempDir)
        {
            Directory.CreateDirectory(tempDir);
            string path = Path.Combine(tempDir, "ov_test.gdml");
            string xml =
                "<gdml>" +
                "<define/>" +
                "<materials/>" +
                "<solids>" +
                "<tube name=\"T1\" lunit=\"mm\" aunit=\"deg\" rmin=\"0\" rmax=\"10\" z=\"20\" startphi=\"0\" deltaphi=\"360\"/>" +
                "<polycone name=\"PC\" lunit=\"mm\" aunit=\"deg\" startphi=\"0\" deltaphi=\"360\">" +
                "<zplane z=\"-10\" rmin=\"5\" rmax=\"10\"/>" +
                "<zplane z=\"10\" rmin=\"5\" rmax=\"10\"/>" +
                "</polycone>" +
                "<box name=\"W\" lunit=\"mm\" x=\"200\" y=\"200\" z=\"200\"/>" +
                "</solids>" +
                "<structure>" +
                "<volume name=\"T1v\"><materialref ref=\"G4_Pb\"/><solidref ref=\"T1\"/></volume>" +
                "<volume name=\"PCv\"><materialref ref=\"G4_Pb\"/><solidref ref=\"PC\"/></volume>" +
                "<volume name=\"World\"><materialref ref=\"G4_AIR\"/><solidref ref=\"W\"/>" +
                "<physvol name=\"a\"><volumeref ref=\"T1v\"/><position x=\"0\" y=\"0\" z=\"0\" unit=\"mm\"/></physvol>" +
                "<physvol name=\"b\"><volumeref ref=\"PCv\"/><position x=\"6\" y=\"0\" z=\"0\" unit=\"mm\"/></physvol>" +
                "</volume>" +
                "</structure>" +
                "<setup name=\"Default\" version=\"1.0\"><world ref=\"World\"/></setup>" +
                "</gdml>";
            File.WriteAllText(path, xml);
            return path;
        }
    }
}
