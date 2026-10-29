using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Linq;
using Cad2Gdml.Geometry;
using Cad2Gdml.Pipeline;
using Cad2Gdml.Scene;

namespace Cad2Gdml.Tests
{
    // Проверки 2d: куб, полый цилиндр, materials.json
    public static class Checks2d
    {
        public static List<KeyValuePair<string, Action>> All(string tempDir)
        {
            var list = new List<KeyValuePair<string, Action>>();
            list.Add(new KeyValuePair<string, Action>("cube_crystal", () => CubeCrystal(tempDir)));
            list.Add(new KeyValuePair<string, Action>("hollow_tube", () => HollowTube(tempDir)));
            list.Add(new KeyValuePair<string, Action>("real_materials_json", () => RealMaterialsJson(tempDir)));
            return list;
        }

        // 1. Кубический кристалл — кадрирование по Z
        private static void CubeCrystal(string tempDir)
        {
            string dir = Path.Combine(tempDir, "cube_crystal");
            Directory.CreateDirectory(dir);

            Fixtures.WriteBinaryStl(Path.Combine(dir, "crystal.stl"), Fixtures.Box(10, 10, 10, 0, 0, 0));
            Fixtures.WriteBinaryStl(Path.Combine(dir, "water_sample.stl"), Fixtures.Box(30, 30, 20, 0, 0, 25));

            var r = CheckUtil.Run(dir, new[] { "crystal.stl", "water_sample.stl" }, 1.0, "auto", true, false);
            CheckUtil.Assert(r.ExitCode == 0, "cube_crystal: ExitCode != 0, код=" + r.MainErrorId);

            Part crystal = FindCrystal(r);
            Vec3d mn, mx;
            crystal.Mesh.GetBounds(out mn, out mx);
            CheckUtil.Near(0.0, mx.Z, 0.01, "max z");
            CheckUtil.Near(-10.0, mn.Z, 0.01, "min z");
        }

        // 2. Полый цилиндр → SolidKind.Tube
        private static void HollowTube(string tempDir)
        {
            string dir = Path.Combine(tempDir, "hollow_tube");
            Directory.CreateDirectory(dir);

            Fixtures.WriteBinaryStl(Path.Combine(dir, "crystal.stl"), Fixtures.HollowCylinder(25.4, 20.0, 50.8, 48, 0.0));
            Fixtures.WriteBinaryStl(Path.Combine(dir, "water_sample.stl"), Fixtures.Box(30, 30, 20, 0, 0, 60));

            var r = CheckUtil.Run(dir, new[] { "crystal.stl", "water_sample.stl" }, 1.0, "+z", true, false);
            CheckUtil.Assert(r.ExitCode == 0, "hollow_tube: ExitCode != 0, код=" + r.MainErrorId);

            Part crystal = FindCrystal(r);
            CheckUtil.Assert(crystal.Shape != null, "hollow_tube: Shape is null");
            CheckUtil.Assert(crystal.Shape.Kind == SolidKind.Tube, "hollow_tube: Kind != Tube, got " + crystal.Shape.Kind);
            CheckUtil.Near(20.0, crystal.Shape.RMin, 0.01, "RMin");
            CheckUtil.Near(25.4, crystal.Shape.RMax, 0.01, "RMax");
        }

        // 3. materials.json: кастомные вещества + стl_default
        private static void RealMaterialsJson(string tempDir)
        {
            string dir = Path.Combine(tempDir, "real_mat");
            Directory.CreateDirectory(dir);

            // Детали
            Fixtures.WriteBinaryStl(Path.Combine(dir, "crystal.stl"), Fixtures.Cylinder(25.4, 50.8, 64, 0.0));
            Fixtures.WriteBinaryStl(Path.Combine(dir, "water_sample.stl"), Fixtures.Cylinder(30.0, 40.0, 64, 70.0));
            Fixtures.WriteBinaryStl(Path.Combine(dir, "other_part.stl"), Fixtures.Box(5, 5, 5, 40, 0, 30));

            // materials.json
            string json = "{\"world_material\":\"G4_AIR\",\"custom\":"
                + "[{\"name\":\"MgO_powder\",\"density_g_cm3\":1.8,\"state\":\"solid\","
                + "\"fractions\":[[\"G4_Mg\",0.603],[\"G4_O\",0.397]]},"
                + "{\"name\":\"Epoxy\",\"density_g_cm3\":1.2,\"state\":\"solid\","
                + "\"fractions\":[[\"G4_C\",0.7],[\"G4_H\",0.1],[\"G4_O\",0.2]]}],"
                + "\"stl_rules\":[[\"^crystal\",\"MgO_powder\"],[\"^water\",\"G4_WATER\"]],"
                + "\"stl_default\":\"Epoxy\"}";
            string jsonPath = Path.Combine(dir, "materials.json");
            File.WriteAllText(jsonPath, json, new UTF8Encoding(false));

            // Ручная сборка опций (нужен MaterialsJsonPath)
            var opt = new PipelineOptions();
            opt.Inputs = new List<string>();
            opt.Inputs.Add(Path.Combine(dir, "crystal.stl"));
            opt.Inputs.Add(Path.Combine(dir, "water_sample.stl"));
            opt.Inputs.Add(Path.Combine(dir, "other_part.stl"));
            opt.UnitToMm = 1.0;
            opt.FrontSign = "+z";
            opt.OutDir = dir;
            opt.BaseName = "o";
            opt.MaterialsJsonPath = jsonPath;
            opt.AllowDefaultMaterial = false;
            opt.DropOpen = false;

            var r = Cad2GdmlPipeline.Run(opt);
            CheckUtil.Assert(r.ExitCode == 0, "real_mat: ExitCode != 0, код=" + r.MainErrorId + " " + r.MainErrorMessage);

            // GDML: <material name="MgO_powder"> содержит <fraction ref="G4_Mg">
            XDocument doc = CheckUtil.Gdml(r);
            XElement matMgO = null;
            foreach (XElement el in doc.Descendants())
            {
                if (el.Name.LocalName == "material")
                {
                    string nm = (string)el.Attribute("name");
                    if (nm == "MgO_powder") { matMgO = el; break; }
                }
            }
            CheckUtil.Assert(matMgO != null, "real_mat: material MgO_powder не найден");

            bool fracOk = false;
            foreach (XElement c in matMgO.Elements())
            {
                if (c.Name.LocalName == "fraction" && (string)c.Attribute("ref") == "G4_Mg")
                { fracOk = true; break; }
            }
            CheckUtil.Assert(fracOk, "real_mat: fraction G4_Mg не найден в MgO_powder");

            // Volume other_part: materialref = Epoxy
            XElement volOther = null;
            foreach (XElement el in doc.Descendants())
            {
                if (el.Name.LocalName == "volume")
                {
                    string nm = (string)el.Attribute("name");
                    if (nm != null && nm.Contains("other_part")) { volOther = el; break; }
                }
            }
            CheckUtil.Assert(volOther != null, "real_mat: volume other_part не найден");

            bool matRefOk = false;
            foreach (XElement c in volOther.Elements())
            {
                if (c.Name.LocalName == "materialref" && (string)c.Attribute("ref") == "Epoxy")
                { matRefOk = true; break; }
            }
            CheckUtil.Assert(matRefOk, "real_mat: materialref Epoxy не найден у other_part");

            // Диагностика: W_MAT_DEFAULT для other_part
            bool diagOk = false;
            foreach (Diagnostic d in r.Diagnostics)
            {
                if (d.Code == "W_MAT_DEFAULT" && d.PartName != null && d.PartName.Contains("other_part"))
                { diagOk = true; break; }
            }
            CheckUtil.Assert(diagOk, "real_mat: W_MAT_DEFAULT для other_part не найден");
        }

        // Поиск детали-кристалла в сцене
        private static Part FindCrystal(PipelineResult r)
        {
            foreach (Part p in r.Scene.Parts)
            {
                if (p.Role == PartRole.Crystal) return p;
            }
            throw new InvalidOperationException("Кристалл не найден в сцене.");
        }
    }
}
