using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Cad2Gdml.Geometry;
using Cad2Gdml.Meshes;
using Cad2Gdml.Scene;
using Cad2Gdml.Materials;
using Cad2Gdml.Recognize;
using Cad2Gdml.Gdml;
using Cad2Gdml.Report;
using Cad2Gdml.Sketch;
using Cad2Gdml.Import;

namespace Cad2Gdml.Pipeline
{
    // Параметры конвейера
    public sealed class PipelineOptions
    {
        public List<string> Inputs;
        public string OutDir;
        public string BaseName;
        public double UnitToMm;
        public string UnitName;
        public string CrystalName;
        public string FrontSign;
        public string MaterialsJsonPath;
        public bool AllowDefaultMaterial;
        public bool DropOpen;
    }

    // Результат конвейера
    public sealed class PipelineResult
    {
        public int ExitCode;
        public string MainErrorId;
        public SceneModel Scene;
        public string GdmlPath;
        public string PngPath;
        public string ReportPath;
        public GdmlWriteResult Gdml;
        public List<Diagnostic> Diagnostics;
    }

    // Конвейер CAD → GDML
    public static class Cad2GdmlPipeline
    {
        public static PipelineResult Run(PipelineOptions o)
        {
            var result = new PipelineResult();
            var scene = new SceneModel();
            result.Scene = scene;
            result.Diagnostics = scene.Diagnostics;

            try
            {
                // 1. Загрузка входов
                bool loaded;
                try
                {
                    loaded = LoadInputs(o, scene);
                }
                catch (Exception lex) when (lex is FileNotFoundException || lex is DirectoryNotFoundException
                    || lex is InvalidDataException || lex is FormatException || lex is IOException
                    || lex is OverflowException || lex is IndexOutOfRangeException
                    || lex is ArgumentOutOfRangeException)
                {
                    scene.Add(MapExceptionCode(lex), DiagSeverity.Error, "", lex.Message);
                    return Fail(result);
                }
                if (!loaded)
                    return Fail(result);

                // 2. Сшивка и топология
                if (!ProcessMeshes(scene))
                    return Fail(result);

                // 3. Роли и открытые сетки
                if (!AssignRoles(o, scene))
                    return Fail(result);

                // 4. Кадр координат
                if (!BuildFrame(o, scene))
                    return Fail(result);

                // 5. Распознавание тел
                RecognizeShapes(scene);

                // 6. Вещества
                if (!ResolveMaterials(o, scene))
                    return Fail(result);

                // 7. Запись выходных
                try
                {
                    WriteOutputs(o, scene, result);
                }
                catch (Exception lex) when (lex is FileNotFoundException || lex is DirectoryNotFoundException
                    || lex is InvalidDataException || lex is FormatException || lex is IOException
                    || lex is OverflowException || lex is IndexOutOfRangeException
                    || lex is ArgumentOutOfRangeException)
                {
                    scene.Add(MapExceptionCode(lex), DiagSeverity.Error, "", lex.Message);
                    return Fail(result);
                }

                result.ExitCode = 0;
                return result;
            }
            catch (Exception ex)
            {
                scene.Add("E_INTERNAL", DiagSeverity.Error, "", ex.Message);
                return Fail(result);
            }
        }

        // Код из исключения; InvalidDataException с "E_" → сам код
        private static string MapExceptionCode(Exception ex)
        {
            if (ex is InvalidDataException)
            {
                string msg = ex.Message;
                if (msg != null && msg.StartsWith("E_"))
                    return msg;
            }
            return "E_FORMAT";
        }

        // Загрузка файлов по расширению
        private static bool LoadInputs(PipelineOptions o, SceneModel scene)
        {
            if (o.Inputs == null || o.Inputs.Count == 0)
            {
                scene.Add("E_FORMAT", DiagSeverity.Error, "", "Нет входных файлов.");
                return false;
            }

            foreach (string input in o.Inputs)
            {
                string ext = Directory.Exists(input) ? ".stl" : GetExt(input);

                if (ext == ".step" || ext == ".stp")
                {
                    scene.Add("E_STEP_NOT_SUPPORTED", DiagSeverity.Error, input,
                        "Формат STEP не поддерживается в v0.1.");
                    return false;
                }

                if (ext == ".stl")
                {
                    if (o.UnitToMm == 0.0)
                    {
                        scene.Add("E_UNIT_MISSING", DiagSeverity.Error, input, "Единицы не заданы для STL.");
                        return false;
                    }
                    if (Directory.Exists(input))
                    {
                        var parts = StlImporter.LoadFolder(input, o.UnitToMm);
                        AddParts(scene, parts, input);
                    }
                    else
                    {
                        var part = StlImporter.Load(input, o.UnitToMm);
                        if (part != null)
                        {
                            if (part.SourceFile == null) part.SourceFile = input;
                            scene.Parts.Add(part);
                        }
                    }
                }
                else if (ext == ".obj")
                {
                    if (o.UnitToMm == 0.0)
                    {
                        scene.Add("E_UNIT_MISSING", DiagSeverity.Error, input, "Единицы не заданы для OBJ.");
                        return false;
                    }
                    var parts = ObjImporter.Load(input, o.UnitToMm);
                    AddParts(scene, parts, input);
                }
                else if (ext == ".3mf")
                {
                    var parts = ThreeMfImporter.Load(input, o.UnitToMm);
                    AddParts(scene, parts, input);
                }
                else
                {
                    scene.Add("E_FORMAT", DiagSeverity.Error, input, "Неподдерживаемое расширение: " + ext);
                    return false;
                }
            }

            if (scene.Parts.Count == 0)
            {
                scene.Add("E_FORMAT", DiagSeverity.Error, "", "Нет деталей после загрузки.");
                return false;
            }

            return true;
        }

        // Добавление списка деталей в сцену
        private static void AddParts(SceneModel scene, IList<Part> parts, string src)
        {
            foreach (Part p in parts)
            {
                if (p.SourceFile == null) p.SourceFile = src;
                scene.Parts.Add(p);
            }
        }

        // Сшивка, топология, объём
        private static bool ProcessMeshes(SceneModel scene)
        {
            const double weldTol = 1e-5;

            for (int i = 0; i < scene.Parts.Count; i++)
            {
                Part p = scene.Parts[i];
                if (p.Mesh == null) continue;

                // Нечисловые координаты
                foreach (Vec3d vv in p.Mesh.Vertices)
                {
                    if (double.IsNaN(vv.X + vv.Y + vv.Z) || double.IsInfinity(vv.X + vv.Y + vv.Z))
                    {
                        scene.Add("E_MESH_NAN", DiagSeverity.Error, p.Name, "Во входе NaN/∞ в координатах.");
                        return false;
                    }
                }

                // Сшивка вершин
                p.Mesh = MeshWelder.Weld(p.Mesh, weldTol);

                // Топология
                var topo = MeshTopology.Analyze(p.Mesh);

                if (topo.NonManifoldEdges > 0)
                {
                    scene.Add("E_MESH_NONMANIFOLD", DiagSeverity.Error, p.Name,
                        "Неманifold рёбра: " + topo.NonManifoldEdges.ToString(CultureInfo.InvariantCulture));
                    return false;
                }

                if (topo.InconsistentEdges > 0)
                {
                    scene.Add("E_MESH_ORIENT", DiagSeverity.Error, p.Name,
                        "Несогласованная ориентация: " + topo.InconsistentEdges.ToString(CultureInfo.InvariantCulture));
                    return false;
                }

                if (topo.OpenEdges > 0)
                {
                    p.MeshOpen = true;
                }

                // Пустая деталь: < 4 треугольников или |V| < 1e-6
                if (p.Mesh.TriangleCount < 4)
                {
                    scene.Add("E_MESH_EMPTY", DiagSeverity.Error, p.Name,
                        "Менее 4 треугольников: " + p.Mesh.TriangleCount.ToString(CultureInfo.InvariantCulture));
                    return false;
                }

                double vol = SignedVolume(p.Mesh);
                if (Math.Abs(vol) < 1e-6)
                {
                    scene.Add("E_MESH_EMPTY", DiagSeverity.Error, p.Name,
                        "Объём < 1e-6 мм³: " + vol.ToString("R", CultureInfo.InvariantCulture));
                    return false;
                }

                // Отрицательный объём — инверсия
                if (vol < 0.0)
                {
                    p.Mesh.FlipWinding();
                    scene.Add("W_MESH_FLIPPED", DiagSeverity.Warning, p.Name,
                        "Объём отрицательный — ориентация инвертирована.");
                    vol = -vol;
                }
                p.VolumeMm3 = vol;
            }

            return true;
        }

        // Знаковый объём: (1/6) Σ a·(b×c)
        private static double SignedVolume(Mesh mesh)
        {
            double vol = 0.0;
            int n = mesh.TriangleCount;
            for (int i = 0; i < n; i++)
            {
                Vec3d a, b, c;
                mesh.GetTriangle(i, out a, out b, out c);
                vol += a.Dot(b.Cross(c));
            }
            return vol / 6.0;
        }

        // Назначение ролей и обработка открытых сеток
        private static bool AssignRoles(PipelineOptions o, SceneModel scene)
        {
            var roleOpt = new RoleOptions();
            RoleAssigner.Assign(scene, roleOpt, o.CrystalName);

            // Открытые сетки: по умолчанию — ошибка для любой детали;
            // при DropOpen — неключевые отбрасываются с предупреждением
            for (int oi = scene.Parts.Count - 1; oi >= 0; oi--)
            {
                Part op = scene.Parts[oi];
                if (!op.MeshOpen) continue;

                bool isKey = (op.Role == PartRole.Crystal || op.Role == PartRole.Source
                    || op.Role == PartRole.Container);

                if (isKey || !o.DropOpen)
                {
                    scene.Add("E_MESH_OPEN", DiagSeverity.Error, op.Name, "Открытая сетка.");
                    return false;
                }

                scene.Add("W_MESH_OPEN_DROPPED", DiagSeverity.Warning, op.Name,
                    "Сетка открыта — деталь исключена из сцены.");
                scene.Parts.RemoveAt(oi);
            }

            // Ошибки ролей из диагностики
            for (int i = 0; i < scene.Diagnostics.Count; i++)
            {
                Diagnostic d = scene.Diagnostics[i];
                if (d.Severity == DiagSeverity.Error &&
                    (d.Code == "E_ROLE_NONE" || d.Code == "E_ROLE_AMBIG"))
                {
                    return false;
                }
            }

            // Кристалл найден
            bool found = false;
            for (int i = 0; i < scene.Parts.Count; i++)
            {
                if (scene.Parts[i].Role == PartRole.Crystal)
                {
                    found = true;
                    break;
                }
            }
            return found;
        }

        // Построение и применение кадра
        private static bool BuildFrame(PipelineOptions o, SceneModel scene)
        {
            Part crystal = null;
            for (int i = 0; i < scene.Parts.Count; i++)
            {
                if (scene.Parts[i].Role == PartRole.Crystal)
                {
                    crystal = scene.Parts[i];
                    break;
                }
            }
            if (crystal == null) return false;

            var frameOpt = new FrameOptions();
            frameOpt.FrontSign = (o.FrontSign != null && o.FrontSign.Length > 0)
                ? o.FrontSign : "auto";

            Affine3 t;
            try
            {
                t = FrameBuilder.Build(scene, crystal, frameOpt);
            }
            catch (InvalidOperationException)
            {
                scene.Add("E_FRONT_AMBIG", DiagSeverity.Error, "", "Не удалось построить кадр.");
                return false;
            }

            FrameBuilder.Apply(scene, t);
            return true;
        }

        // Распознавание: Tube/Polycone → Box → Tessellated
        private static void RecognizeShapes(SceneModel scene)
        {
            var revOpt = new RecognizeOptions();
            const double boxTol = 0.01;

            for (int i = 0; i < scene.Parts.Count; i++)
            {
                Part p = scene.Parts[i];

                string whyNot;
                SolidShape shape = RevolutionRecognizer.TryRecognize(p, revOpt, out whyNot);
                if (shape != null)
                {
                    p.Shape = shape;
                    continue;
                }

                string boxWhy;
                shape = BoxRecognizer.TryRecognize(p, boxTol, out boxWhy);
                if (shape != null)
                {
                    p.Shape = shape;
                    continue;
                }

                p.Shape = new SolidShape();
                p.Shape.Kind = SolidKind.Tessellated;
            }
        }

        // Назначение веществ по правилам
        private static bool ResolveMaterials(PipelineOptions o, SceneModel scene)
        {
            MaterialLibrary lib;
            if (!string.IsNullOrEmpty(o.MaterialsJsonPath))
            {
                lib = MaterialLibrary.LoadJson(o.MaterialsJsonPath);
            }
            else
            {
                lib = MaterialLibrary.Default();
            }

            for (int i = 0; i < scene.Parts.Count; i++)
            {
                Part p = scene.Parts[i];

                if (!string.IsNullOrEmpty(p.Material))
                    continue;

                lib.LastResolveWasGuess = false;
                string mat = lib.Resolve(p.Name);

                if (mat != null)
                {
                    p.Material = mat;
                    // Предположительное назначение кристалла
                    if (p.Role == PartRole.Crystal && lib.LastResolveWasGuess)
                    {
                        scene.Add("W_MAT_GUESS", DiagSeverity.Warning, p.Name,
                            "Вещество кристалла назначено по общим словам: " + mat);
                    }
                    continue;
                }

                if (o.AllowDefaultMaterial)
                {
                    p.Material = lib.DefaultMaterial;
                    scene.Add("W_MAT_DEFAULT", DiagSeverity.Warning, p.Name,
                        "Не назначено — использовано " + lib.DefaultMaterial);
                }
                else
                {
                    scene.Add("E_MAT_UNASSIGNED", DiagSeverity.Error, p.Name, "Вещество не назначено.");
                    return false;
                }
            }

            return true;
        }

        // Запись GDML, PNG, отчёта
        private static void WriteOutputs(PipelineOptions o, SceneModel scene, PipelineResult result)
        {
            string outDir = (o.OutDir != null && o.OutDir.Length > 0) ? o.OutDir : ".";
            Directory.CreateDirectory(outDir);

            string baseName = o.BaseName;
            if (baseName == null || baseName.Length == 0)
            {
                baseName = DeriveBaseName(o);
            }

            string gdmlPath = Path.Combine(outDir, baseName + ".gdml");
            string pngPath = Path.Combine(outDir, baseName + ".png");
            string reportPath = Path.Combine(outDir, baseName + ".report.txt");

            // GDML
            MaterialLibrary lib;
            if (!string.IsNullOrEmpty(o.MaterialsJsonPath))
                lib = MaterialLibrary.LoadJson(o.MaterialsJsonPath);
            else
                lib = MaterialLibrary.Default();

            result.Gdml = GdmlWriter.Write(scene, lib, gdmlPath);
            result.GdmlPath = gdmlPath;

            // PNG — сечение 1200×800
            var sketch = SketchBuilder.Build(scene);
            GdiSketchRenderer.SavePng(sketch, pngPath, 1200, 800);
            result.PngPath = pngPath;

            // Текстовый отчёт
            string srcDesc = string.Join("; ", GetInputNames(scene));
            string report = SceneDescriber.Describe(scene, srcDesc, gdmlPath);
            File.WriteAllText(reportPath, report, new UTF8Encoding(false));
            result.ReportPath = reportPath;
        }

        // Базовое имя из первого входа
        private static string DeriveBaseName(PipelineOptions o)
        {
            if (o.Inputs != null && o.Inputs.Count > 0)
            {
                string first = o.Inputs[0];
                string name;
                if (Directory.Exists(first))
                {
                    name = Path.GetFileName(first.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                }
                else
                {
                    name = Path.GetFileNameWithoutExtension(first);
                }
                if (!string.IsNullOrEmpty(name))
                    return name;
            }
            return "output";
        }

        // Уникальные имена входов
        private static List<string> GetInputNames(SceneModel scene)
        {
            var names = new List<string>();
            for (int i = 0; i < scene.Parts.Count; i++)
            {
                string sf = scene.Parts[i].SourceFile;
                if (!string.IsNullOrEmpty(sf) && !names.Contains(sf))
                    names.Add(sf);
            }
            return names;
        }

        // Расширение в нижнем регистре
        private static string GetExt(string path)
        {
            string ext = Path.GetExtension(path);
            return (ext != null) ? ext.ToLowerInvariant() : "";
        }

        // Выход: код из первой Error-диагностики по таблице префиксов
        private static PipelineResult Fail(PipelineResult result)
        {
            string firstError = null;
            for (int i = 0; i < result.Diagnostics.Count; i++)
            {
                if (result.Diagnostics[i].Severity == DiagSeverity.Error)
                {
                    firstError = result.Diagnostics[i].Code;
                    break;
                }
            }
            result.MainErrorId = firstError;
            result.ExitCode = DeriveExitCode(firstError);
            return result;
        }

        // Таблица префиксов → код выхода
        private static int DeriveExitCode(string code)
        {
            if (code == null) return 9;
            if (code.StartsWith("E_FORMAT") || code.StartsWith("E_UNIT_MISSING")
                || code.StartsWith("E_STEP_NOT_SUPPORTED"))
                return 2;
            if (code.StartsWith("E_MESH"))
                return 3;
            if (code.StartsWith("E_ROLE") || code.StartsWith("E_FRONT_AMBIG")
                || code.StartsWith("E_AXIS_AMBIG"))
                return 4;
            if (code.StartsWith("E_MAT") || code.StartsWith("E_ELEMENT_UNKNOWN"))
                return 5;
            return 9;
        }
    }
}
