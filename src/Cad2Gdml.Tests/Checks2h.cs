using System;
using System.Collections.Generic;
using Cad2Gdml.Geometry;
using Cad2Gdml.Meshes;
using Cad2Gdml.Scene;
using Cad2Gdml.Report;
using Cad2Gdml.Materials;

namespace Cad2Gdml.Tests
{
    // Проверки 2h: HoleFiller, DiagnosticGrouper, MaterialNames, MaterialLibrary
    public static class Checks2h
    {
        public static List<KeyValuePair<string, Action>> All(string tempDir)
        {
            var list = new List<KeyValuePair<string, Action>>();
            list.Add(new KeyValuePair<string, Action>("holefiller_closes_box", HoleFillsClosesBox));
            list.Add(new KeyValuePair<string, Action>("holefiller_keeps_closed", HoleFillsKeepsClosed));
            list.Add(new KeyValuePair<string, Action>("grouper_collapses", GrouperCollapses));
            list.Add(new KeyValuePair<string, Action>("material_names_roundtrip", MaterialNamesRoundtrip));
            list.Add(new KeyValuePair<string, Action>("material_rules_order", MaterialRulesOrder));
            return list;
        }

        // Бокс без верхней грани → HoleFiller закрывает
        private static void HoleFillsClosesBox()
        {
            IList<double[]> tris = Fixtures.Box(20, 20, 20, 0, 0, 0);
            // Убираем последние 2 треугольника (верхняя грань +z)
            var mesh = new Mesh();
            for (int i = 0; i < tris.Count - 2; i++)
            {
                double[] t = tris[i];
                mesh.AddTriangle(
                    new Vec3d(t[0], t[1], t[2]),
                    new Vec3d(t[3], t[4], t[5]),
                    new Vec3d(t[6], t[7], t[8]));
            }

            Mesh welded = MeshWelder.Weld(mesh, 1e-6);
            var report = MeshTopology.Analyze(welded);
            if (report.OpenEdges <= 0)
                throw new Exception("holefiller_closes_box: ожидались открытые рёбра");

            int filled, skipped;
            Mesh fixedMesh = HoleFiller.Fill(welded, 1000, out filled, out skipped);
            if (filled < 1)
                throw new Exception("holefiller_closes_box: filled=" + filled);
            if (skipped != 0)
                throw new Exception("holefiller_closes_box: skipped=" + skipped);
            if (fixedMesh == null)
                throw new Exception("holefiller_closes_box: fixedMesh null");
            if (!MeshTopology.Analyze(fixedMesh).IsClosedManifold)
                throw new Exception("holefiller_closes_box: не замкнутый манифолд");
        }

        // Закрытый бокс → Fill ничего не меняет
        private static void HoleFillsKeepsClosed()
        {
            IList<double[]> tris = Fixtures.Box(20, 20, 20, 0, 0, 0);
            var mesh = new Mesh();
            for (int i = 0; i < tris.Count; i++)
            {
                double[] t = tris[i];
                mesh.AddTriangle(
                    new Vec3d(t[0], t[1], t[2]),
                    new Vec3d(t[3], t[4], t[5]),
                    new Vec3d(t[6], t[7], t[8]));
            }

            int origCount = mesh.TriangleCount;
            Mesh welded = MeshWelder.Weld(mesh, 1e-6);

            int filled, skipped;
            Mesh fixedMesh = HoleFiller.Fill(welded, 1000, out filled, out skipped);
            if (filled != 0)
                throw new Exception("holefiller_keeps_closed: filled=" + filled);
            if (fixedMesh != null && fixedMesh.TriangleCount != origCount)
                throw new Exception("holefiller_keeps_closed: изменилось число треугольников");
        }

        // DiagnosticGrouper: сворачивание и фильтрация Info
        private static void GrouperCollapses()
        {
            var diags = new List<Diagnostic>
            {
                new Diagnostic("W_X", DiagSeverity.Warning, "p1", "msg"),
                new Diagnostic("W_X", DiagSeverity.Warning, "p2", "msg"),
                new Diagnostic("W_X", DiagSeverity.Warning, "p3", "msg"),
                new Diagnostic("I_Y", DiagSeverity.Info, "p4", "info msg")
            };

            // Без Info
            var lines = DiagnosticGrouper.Format(diags, false);
            bool hasWx = false, hasIy = false;
            foreach (string line in lines)
            {
                if (line.Contains("W_X") && line.Contains("3")) hasWx = true;
                if (line.Contains("I_Y")) hasIy = true;
            }
            if (!hasWx)
                throw new Exception("grouper_collapses: нет строки W_X с ×3");
            if (hasIy)
                throw new Exception("grouper_collapses: I_Y не должен присутствовать");

            // С Info
            var lines2 = DiagnosticGrouper.Format(diags, true);
            bool hasIy2 = false;
            foreach (string line in lines2)
            {
                if (line.Contains("I_Y")) hasIy2 = true;
            }
            if (!hasIy2)
                throw new Exception("grouper_collapses: I_Y должен присутствовать при includeInfo=true");

            // Null-список → пустой результат
            var lines3 = DiagnosticGrouper.Format(null, false);
            if (lines3 != null && lines3.Count != 0)
                throw new Exception("grouper_collapses: null → не пустой результат");
        }

        // MaterialNames: roundtrip и обработка null/unknown
        private static void MaterialNamesRoundtrip()
        {
            if (MaterialNames.ToRu(null) != "")
                throw new Exception("material_names_roundtrip: ToRu(null) != ''");

            string ru = MaterialNames.ToRu("G4_Al");
            if (ru.Length == 0)
                throw new Exception("material_names_roundtrip: ToRu(G4_Al) пусто");
            string back = MaterialNames.ToId(ru);
            if (back != "G4_Al")
                throw new Exception("material_names_roundtrip: ToId(ToRu(G4_Al))=" + back);

            // Неизвестный текст проходит без изменений
            string unknown = "XyzUnknown";
            if (MaterialNames.ToRu(unknown) != unknown)
                throw new Exception("material_names_roundtrip: ToRu(unknown) изменился");
            if (MaterialNames.ToId(unknown) != unknown)
                throw new Exception("material_names_roundtrip: ToId(unknown) изменился");
        }

        // Порядок правил в MaterialLibrary.Default()
        private static void MaterialRulesOrder()
        {
            MaterialLibrary lib = MaterialLibrary.Default();

            string r1 = lib.Resolve("Crystal_LaBr3");
            if (r1 == null || !r1.Contains("LaBr3"))
                throw new Exception("material_rules_order: Crystal_LaBr3 → " + r1);

            string r2 = lib.Resolve("LaBr3_housing");
            if (r2 != "G4_Al")
                throw new Exception("material_rules_order: LaBr3_housing → " + r2);

            string r3 = lib.Resolve("\u041A\u043E\u0440\u043F\u0443\u0441_LaBr3");
            if (r3 != "G4_Al")
                throw new Exception("material_rules_order: \u041A\u043E\u0440\u043F\u0443\u0441_LaBr3 → " + r3);

            string r4 = lib.Resolve("CsI_shell");
            if (r4 != "G4_Al")
                throw new Exception("material_rules_order: CsI_shell → " + r4);

            string r5 = lib.Resolve("NaI_crystal");
            if (r5 == null || !r5.Contains("NaI"))
                throw new Exception("material_rules_order: NaI_crystal → " + r5);
        }
    }
}
