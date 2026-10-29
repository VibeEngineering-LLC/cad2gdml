using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
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
        public bool KeepOpen;
        public bool NoSplit;
        public double WeldTolMm;
        public List<KeyValuePair<string, string>> MaterialOverrides;
        public bool NoRepair;
        public List<string> ExcludeParts;
    }

    // Результат конвейера
    public sealed class PipelineResult
    {
        public int ExitCode;
        public string MainErrorId;
        public string MainErrorMessage;
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
        // -- трассировка этапов (CAD2GDML_TRACE=1) --
        private static System.Diagnostics.Stopwatch _traceSw;
        private static bool _traceChecked;

        private static void Trace(string stage)
        {
            if (!_traceChecked)
            {
                _traceChecked = true;
                string v = Environment.GetEnvironmentVariable("CAD2GDML_TRACE");
                if (v != "1") return;
                _traceSw = System.Diagnostics.Stopwatch.StartNew();
            }
            if (_traceSw == null) return;
            long ms = _traceSw.ElapsedMilliseconds;
            Console.Error.Write("[trace +" + ms.ToString(CultureInfo.InvariantCulture) + " ms] " + stage + Environment.NewLine);
            Console.Error.Flush();
        }

        public static PipelineResult Run(PipelineOptions o)
        {
            var result = new PipelineResult();
            var scene = new SceneModel();
            result.Scene = scene;
            result.Diagnostics = scene.Diagnostics;

            try
            {
                bool loaded;
                Trace("load");
                try
                {
                    loaded = LoadInputs(o, scene);
                }
                catch (Exception lex) when (lex is FileNotFoundException || lex is DirectoryNotFoundException
                    || lex is InvalidDataException || lex is FormatException || lex is IOException
                    || lex is ArgumentException || lex is OverflowException || lex is IndexOutOfRangeException
                    || lex is ArgumentOutOfRangeException || lex is UnauthorizedAccessException)
                {
                    scene.Add(MapExceptionCode(lex), DiagSeverity.Error, "", lex.Message);
                    return Fail(result);
                }
                if (!loaded)
                    return Fail(result);

                Trace("meshes");
                if (!ProcessMeshes(o, scene))
                    return Fail(result);

                Trace("roles");
                if (!AssignRoles(o, scene))
                    return Fail(result);

                Part crystal = FindCrystal(scene);
                if (crystal != null)
                {
                    Trace("frame");
                    if (!BuildFrame(o, scene, crystal))
                        return Fail(result);
                }

                Trace("recognize");
                RecognizeShapes(scene);

                Trace("materials");
                if (!ResolveMaterials(o, scene))
                    return Fail(result);

                SortParts(scene);

                Trace("write");
                try
                {
                    if (!WriteOutputs(o, scene, result))
                        return Fail(result);
                }
                catch (Exception lex)
                {
                    string code = ExtractMatCode(lex);
                    scene.Add(code, DiagSeverity.Error, "", "Не удалось записать результат: " + lex.Message);
                    return Fail(result);
                }

                result.ExitCode = 0;
                Trace("done");
                return result;
            }
            catch (Exception ex)
            {
                scene.Add("E_INTERNAL", DiagSeverity.Error, "", Environment.GetEnvironmentVariable("CAD2GDML_DEBUG") == "1" ? ex.ToString() : ex.Message);
                return Fail(result);
            }
        }

        private static string MapExceptionCode(Exception ex)
        {
            if (ex is InvalidDataException)
            {
                string msg = ex.Message;
                if (msg != null && msg.StartsWith("E_"))
                {
                    int sep = msg.IndexOfAny(new char[] { ':', ' ' });
                    return (sep > 0) ? msg.Substring(0, sep) : msg;
                }
            }
            return "E_FORMAT";
        }

        private static string ExtractMatCode(Exception ex)
        {
            if (ex is InvalidDataException)
            {
                string msg = ex.Message;
                if (msg != null && (msg.StartsWith("E_MAT_") || msg.StartsWith("E_ELEMENT_")))
                {
                    int sep = msg.IndexOfAny(new char[] { ':', ' ' });
                    return (sep > 0) ? msg.Substring(0, sep) : msg;
                }
            }
            return "E_FORMAT";
        }

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
                    List<Part> parts;
                    string code = null, msg = null;
                    try
                    {
                        parts = StepImporter.Load(input, out code, out msg);
                    }
                    catch (Exception ex)
                    {
                        scene.Add("E_STEP_INTERNAL", DiagSeverity.Error, input, ex.Message);
                        return false;
                    }
                    if (parts == null)
                    {
                        scene.Add(code, DiagSeverity.Error, input, msg);
                        return false;
                    }
                    // предупреждения STEP
                    if (StepImporter.LastWarnings != null)
                    {
                        for (int wi = 0; wi < StepImporter.LastWarnings.Count; wi++)
                        {
                            scene.Add("W_STEP_NOTE", DiagSeverity.Warning, input, StepImporter.LastWarnings[wi]);
                        }
                    }
                    AddParts(scene, parts, input);
                }
                else if (ext == ".stl")
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

        private static void AddParts(SceneModel scene, IList<Part> parts, string src)
        {
            foreach (Part p in parts)
            {
                if (p.SourceFile == null) p.SourceFile = src;
                scene.Parts.Add(p);
            }
        }

        private static bool ProcessMeshes(PipelineOptions o, SceneModel scene)
        {
            double weldTol = o.WeldTolMm > 0.0 ? o.WeldTolMm : 1e-5;

            for (int i = 0; i < scene.Parts.Count; i++)
            {
                Part p = scene.Parts[i];
                if (p.Mesh == null) continue;

                foreach (Vec3d vv in p.Mesh.Vertices)
                {
                    if (double.IsNaN(vv.X + vv.Y + vv.Z) || double.IsInfinity(vv.X + vv.Y + vv.Z))
                    {
                        scene.Add("E_MESH_NAN", DiagSeverity.Error, p.Name, "Во входе NaN/∞ в координатах.");
                        return false;
                    }
                }

                p.Mesh = MeshWelder.Weld(p.Mesh, weldTol);
            }

            if (!o.NoSplit)
            {
                Trace("split");
                SplitConnectedComponents(scene);
            }

            // Исключение деталей по имени
            if (o.ExcludeParts != null && o.ExcludeParts.Count > 0)
            {
                bool[] matched = new bool[o.ExcludeParts.Count];
                for (int i = scene.Parts.Count - 1; i >= 0; i--)
                {
                    Part p = scene.Parts[i];
                    for (int ei = 0; ei < o.ExcludeParts.Count; ei++)
                    {
                        if (string.Equals(p.Name, o.ExcludeParts[ei], StringComparison.OrdinalIgnoreCase))
                        {
                            scene.Add("I_PART_EXCLUDED", DiagSeverity.Info, p.Name, "деталь исключена пользователем");
                            scene.Parts.RemoveAt(i);
                            matched[ei] = true;
                            break;
                        }
                    }
                }
                for (int ei = 0; ei < o.ExcludeParts.Count; ei++)
                {
                    if (!matched[ei])
                        scene.Add("W_EXCLUDE_UNUSED", DiagSeverity.Warning, "", "исключаемая деталь не найдена: " + o.ExcludeParts[ei]);
                }
            }

            Trace("topology");
            for (int i = 0; i < scene.Parts.Count; i++)
            {
                Part p = scene.Parts[i];
                if (p.Mesh == null) continue;

                Trace("topology " + p.Name);

                TopologyReport topo = MeshTopology.Analyze(p.Mesh);

                if (topo.NonManifoldEdges > 0)
                {
                    string nmText = "Неманифольдных рёбер: " + topo.NonManifoldEdges.ToString(CultureInfo.InvariantCulture)
                        + " (ребро у трёх и более граней внутри одного тела); знак объёма проверен только по сумме.";
                    if (o.NoSplit)
                    {
                        scene.Add("E_MESH_NONMANIFOLD", DiagSeverity.Error, p.Name, nmText + " Уберите --no-split: касающиеся тела разделятся.");
                        return false;
                    }
                    scene.Add("W_MESH_NONMANIFOLD", DiagSeverity.Warning, p.Name, nmText);
                }

                if (topo.InconsistentEdges > 0)
                {
                    scene.Add("E_MESH_ORIENT", DiagSeverity.Error, p.Name,
                        "Несогласованная ориентация: " + topo.InconsistentEdges.ToString(CultureInfo.InvariantCulture));
                    return false;
                }

                // Попытка закрыть дыры
                if (topo.OpenEdges > 0 && !o.NoRepair)
                {
                    int filled, skipped;
                    Mesh repaired = HoleFiller.Fill(p.Mesh, 2000, out filled, out skipped);
                    if (repaired != null)
                    {
                        TopologyReport newTopo = MeshTopology.Analyze(repaired);
                        if (newTopo.OpenEdges == 0 && newTopo.InconsistentEdges == 0
                            && Math.Abs(MeshProperties.SignedVolume(repaired)) > 1e-3)
                        {
                            p.Mesh = repaired;
                            topo = newTopo;
                            scene.Add("W_MESH_REPAIRED", DiagSeverity.Warning, p.Name,
                                "закрыто дыр: " + filled.ToString(CultureInfo.InvariantCulture) + "; проверьте деталь на эскизе");
                        }
                        else
                        {
                            scene.Add("W_MESH_REPAIR_FAILED", DiagSeverity.Warning, p.Name,
                                "дыры закрыть не удалось (деталь — тонкая оболочка или сложный контур)");
                        }
                    }
                    else
                    {
                        scene.Add("W_MESH_REPAIR_FAILED", DiagSeverity.Warning, p.Name,
                            "дыры закрыть не удалось (деталь — тонкая оболочка или сложный контур)");
                    }
                }

                if (topo.OpenEdges > 0)
                {
                    p.MeshOpen = true;
                }

                if (p.Mesh.TriangleCount < 4)
                {
                    scene.Add("E_MESH_EMPTY", DiagSeverity.Error, p.Name,
                        "Менее 4 треугольников: " + p.Mesh.TriangleCount.ToString(CultureInfo.InvariantCulture));
                    return false;
                }

                double vol = MeshProperties.SignedVolume(p.Mesh);
                if (Math.Abs(vol) < 1e-6)
                {
                    scene.Add("E_MESH_EMPTY", DiagSeverity.Error, p.Name,
                        "Объём < 1e-6 мм³: " + vol.ToString("R", CultureInfo.InvariantCulture));
                    return false;
                }

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

        private static void SplitConnectedComponents(SceneModel scene)
        {
            for (int pi = scene.Parts.Count - 1; pi >= 0; pi--)
            {
                Part p = scene.Parts[pi];
                if (p.Mesh == null) continue;

                if (p.Mesh.Vertices.Count == 0) continue;

                List<List<int>> found = ComponentSplitter.Components(p.Mesh);
                if (found.Count <= 1) continue;
                var compMap = new Dictionary<int, List<int>>();
                for (int ci0 = 0; ci0 < found.Count; ci0++) compMap[ci0] = found[ci0];

                var comps = new List<KeyValuePair<int, List<int>>>(compMap.Count);
                foreach (KeyValuePair<int, List<int>> kvp in compMap)
                    comps.Add(kvp);

                comps.Sort(delegate (KeyValuePair<int, List<int>> a, KeyValuePair<int, List<int>> b)
                {
                    int cmp = b.Value.Count.CompareTo(a.Value.Count);
                    if (cmp != 0) return cmp;
                    int minA = int.MaxValue;
                    foreach (int t in a.Value)
                    {
                        int idx = p.Mesh.Indices[t * 3];
                        if (idx < minA) minA = idx;
                    }
                    int minB = int.MaxValue;
                    foreach (int t in b.Value)
                    {
                        int idx = p.Mesh.Indices[t * 3];
                        if (idx < minB) minB = idx;
                    }
                    return minA.CompareTo(minB);
                });

                int nComp = comps.Count;
                scene.Add("I_SPLIT", DiagSeverity.Info, p.Name,
                    "деталь " + p.Name + " разбита на " + nComp.ToString(CultureInfo.InvariantCulture) + " связных тел");

                var newParts = new List<Part>();
                int droppedTiny = 0;
                for (int ci = 0; ci < nComp; ci++)
                {
                    List<int> tris = comps[ci].Value;

                    if (tris.Count < 4)
                    {
                        droppedTiny++;
                        continue;
                    }

                    var oldToNew = new Dictionary<int, int>();
                    var newVerts = new List<Vec3d>();
                    var newIndices = new List<int>();

                    for (int tj = 0; tj < tris.Count; tj++)
                    {
                        int ti = tris[tj];
                        int i0 = p.Mesh.Indices[ti * 3];
                        int i1 = p.Mesh.Indices[ti * 3 + 1];
                        int i2 = p.Mesh.Indices[ti * 3 + 2];

                        int n0, n1, n2;
                        if (!oldToNew.TryGetValue(i0, out n0))
                        {
                            n0 = newVerts.Count;
                            newVerts.Add(p.Mesh.Vertices[i0]);
                            oldToNew[i0] = n0;
                        }
                        if (!oldToNew.TryGetValue(i1, out n1))
                        {
                            n1 = newVerts.Count;
                            newVerts.Add(p.Mesh.Vertices[i1]);
                            oldToNew[i1] = n1;
                        }
                        if (!oldToNew.TryGetValue(i2, out n2))
                        {
                            n2 = newVerts.Count;
                            newVerts.Add(p.Mesh.Vertices[i2]);
                            oldToNew[i2] = n2;
                        }

                        newIndices.Add(n0);
                        newIndices.Add(n1);
                        newIndices.Add(n2);
                    }

                    Mesh newMesh = new Mesh();
                    newMesh.Vertices.AddRange(newVerts);
                    newMesh.Indices.AddRange(newIndices);

                    string newName = p.Name + "_c" + (ci + 1).ToString("D" + Math.Max(2, nComp.ToString(CultureInfo.InvariantCulture).Length).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

                    Part np = new Part();
                    np.Name = newName;
                    np.OrigName = p.OrigName;
                    np.Mesh = newMesh;
                    np.Role = p.Role;
                    np.Material = p.Material;
                    np.Shape = null;
                    np.VolumeMm3 = 0.0;
                    np.SourceFile = p.SourceFile;
                    np.MeshOpen = false;
                    np.SplitGroup = p.Name;

                    newParts.Add(np);
                }

                if (droppedTiny > 0)
                {
                    scene.Add("W_COMPONENT_TINY", DiagSeverity.Warning, p.Name,
                        "удалено тел менее чем из 4 треугольников: " + droppedTiny.ToString(CultureInfo.InvariantCulture));
                }

                scene.Parts.RemoveAt(pi);
                for (int ni = 0; ni < newParts.Count; ni++)
                {
                    scene.Parts.Insert(pi + ni, newParts[ni]);
                }
            }
        }

        private static Part FindCrystal(SceneModel scene)
        {
            for (int i = 0; i < scene.Parts.Count; i++)
            {
                if (scene.Parts[i].Role == PartRole.Crystal)
                    return scene.Parts[i];
            }
            return null;
        }

        private static bool AssignRoles(PipelineOptions o, SceneModel scene)
        {
            var roleOpt = new RoleOptions();
            RoleAssigner.Assign(scene, roleOpt, o.CrystalName);

            // Вычисляем, есть ли STEP-вход
            bool anyStepInput = false;
            if (o.Inputs != null)
            {
                for (int si = 0; si < o.Inputs.Count; si++)
                {
                    string inp = o.Inputs[si];
                    if (inp != null && (inp.EndsWith(".step", StringComparison.OrdinalIgnoreCase) || inp.EndsWith(".stp", StringComparison.OrdinalIgnoreCase)))
                    {
                        anyStepInput = true;
                        break;
                    }
                }
            }

            bool keepOpen = o.KeepOpen || (!o.DropOpen && !o.NoRepair && anyStepInput);

            if (keepOpen && !o.KeepOpen)
            {
                scene.Add("I_STEP_KEEP_OPEN", DiagSeverity.Info, "",
                    "Для STEP открытые детали, которые не удалось закрыть, оставлены в сцене (как --keep-open); используйте --drop-open, чтобы исключить их.");
            }

            for (int oi = scene.Parts.Count - 1; oi >= 0; oi--)
            {
                Part op = scene.Parts[oi];
                if (!op.MeshOpen) continue;

                bool isKey = (op.Role == PartRole.Crystal || op.Role == PartRole.Source
                    || op.Role == PartRole.Container);

                if (keepOpen)
                {
                    scene.Add("W_MESH_OPEN_KEPT", DiagSeverity.Warning, op.Name,
                        "Сетка открыта — деталь оставлена как есть (Geant4 может отвергнуть).");
                    continue;
                }

                if (isKey || !o.DropOpen)
                {
                    scene.Add("E_MESH_OPEN", DiagSeverity.Error, op.Name,
                        "Открытая сетка (дыры в поверхности). Для неключевых деталей включите «Пропускать открытые детали» (--drop-open) или «--keep-open».");
                    return false;
                }

                scene.Add("W_MESH_OPEN_DROPPED", DiagSeverity.Warning, op.Name,
                    "Сетка открыта — деталь исключена из сцены.");
                scene.Parts.RemoveAt(oi);
            }

            if (scene.Parts.Count == 0)
            {
                scene.Add("E_SCENE_EMPTY", DiagSeverity.Error, "", "После отбрасывания не осталось ни одной детали.");
                return false;
            }

            for (int i = 0; i < scene.Diagnostics.Count; i++)
            {
                Diagnostic d = scene.Diagnostics[i];
                if (d.Severity == DiagSeverity.Error && d.Code == "E_ROLE_AMBIG")
                    return false;
            }

            bool crystalFound = false;
            for (int i = 0; i < scene.Parts.Count; i++)
            {
                if (scene.Parts[i].Role == PartRole.Crystal)
                {
                    crystalFound = true;
                    break;
                }
            }

            if (!crystalFound)
            {
                if (o.CrystalName != null && o.CrystalName.Length > 0)
                {
                    bool alreadyReported = false;
                    for (int i = 0; i < scene.Diagnostics.Count; i++)
                    {
                        if (scene.Diagnostics[i].Code == "E_ROLE_NONE" && scene.Diagnostics[i].Severity == DiagSeverity.Error)
                        {
                            alreadyReported = true;
                            break;
                        }
                    }
                    if (!alreadyReported)
                        scene.Add("E_ROLE_NONE", DiagSeverity.Error, "", "Кристалл не найден: --crystal задан, но совпадений нет.");
                    return false;
                }
                else
                {
                    RemoveDiagnostics(scene, "E_ROLE_NONE");
                    scene.Add("W_NO_CRYSTAL", DiagSeverity.Warning, "",
                        "кристалл не найден: кадр координат не строится, детали остаются в координатах файла");
                }
            }

            return true;
        }

        private static void RemoveDiagnostics(SceneModel scene, string code)
        {
            for (int i = scene.Diagnostics.Count - 1; i >= 0; i--)
            {
                if (scene.Diagnostics[i].Code == code)
                    scene.Diagnostics.RemoveAt(i);
            }
        }

        private static bool BuildFrame(PipelineOptions o, SceneModel scene, Part crystal)
        {
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

            for (int i = 0; i < scene.Parts.Count; i++)
            {
                Part p = scene.Parts[i];
                if (p.Role != PartRole.Source && p.Role != PartRole.Container) continue;
                if (p.Mesh == null) continue;
                Vec3d mn, mx;
                p.Mesh.GetBounds(out mn, out mx);
                if (mx.Z <= 0.001)
                {
                    scene.Add("W_SOURCE_BEHIND", DiagSeverity.Warning, p.Name,
                        "Источник расположен позади переднего торца кристалла: проверьте --front");
                }
            }

            return true;
        }

        private static void RecognizeShapes(SceneModel scene)
        {
            var revOpt = new RecognizeOptions();
            const double boxTol = 0.01;

            for (int i = 0; i < scene.Parts.Count; i++)
            {
                Part p = scene.Parts[i];

                Trace("recognize " + p.Name);

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
            try
            {
                if (!string.IsNullOrEmpty(o.MaterialsJsonPath))
                {
                    lib = MaterialLibrary.LoadJson(o.MaterialsJsonPath);
                }
                else
                {
                    lib = MaterialLibrary.Default();
                }
            }
            catch (InvalidDataException ex)
            {
                string msg = ex.Message;
                string code;
                if (msg != null && msg.StartsWith("E_"))
                {
                    int sep = msg.IndexOfAny(new char[] { ':', ' ' });
                    code = (sep > 0) ? msg.Substring(0, sep) : msg;
                }
                else
                {
                    code = "E_FORMAT";
                }
                scene.Add(code, DiagSeverity.Error, "", msg);
                return false;
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException
                || ex is UnauthorizedAccessException || ex is IOException || ex is FormatException)
            {
                scene.Add("E_FORMAT", DiagSeverity.Error, "",
                    "Не удалось загрузить файл веществ: " + ex.Message);
                return false;
            }

            // Ручные назначения веществ (overrides)
            if (o.MaterialOverrides != null && o.MaterialOverrides.Count > 0)
            {
                // Валидация значений
                for (int mi = 0; mi < o.MaterialOverrides.Count; mi++)
                {
                    string val = o.MaterialOverrides[mi].Value;
                    if (lib.Find(val) == null && !lib.IsNistRef(val))
                    {
                        scene.Add("E_MAT_UNKNOWN", DiagSeverity.Error, "", "Вещество не найдено: " + val + ".");
                        return false;
                    }
                }

                // Применение: сначала точные, затем подстроки
                bool[] used = new bool[o.MaterialOverrides.Count];
                for (int pi = 0; pi < scene.Parts.Count; pi++)
                {
                    Part p = scene.Parts[pi];
                    if (p.Name == null) continue;

                    int matched = -1;

                    // Точное совпадение (ключ с '=' — без подстроки, без '=' — равенство)
                    for (int mi = 0; mi < o.MaterialOverrides.Count; mi++)
                    {
                        string key = o.MaterialOverrides[mi].Key;
                        if (key == null) continue;
                        string cmp = key.StartsWith("=") ? key.Substring(1) : key;
                        if (p.Name.Equals(cmp, StringComparison.OrdinalIgnoreCase))
                        {
                            matched = mi;
                            break;
                        }
                    }

                    // Подстрока — только для ключей без '='
                    if (matched < 0)
                    {
                        for (int mi = 0; mi < o.MaterialOverrides.Count; mi++)
                        {
                            string key = o.MaterialOverrides[mi].Key;
                            if (key == null) continue;
                            if (key.StartsWith("=")) continue;
                            if (p.Name.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                matched = mi;
                                break;
                            }
                        }
                    }

                    if (matched >= 0)
                    {
                        p.Material = o.MaterialOverrides[matched].Value;
                        scene.Add("I_MAT_MANUAL", DiagSeverity.Info, p.Name,
                            "вещество назначено вручную: " + o.MaterialOverrides[matched].Value);
                        used[matched] = true;
                    }
                }

                // Неиспользованные правила
                for (int mi = 0; mi < o.MaterialOverrides.Count; mi++)
                {
                    if (!used[mi])
                    {
                        scene.Add("W_MAT_OVERRIDE_UNUSED", DiagSeverity.Warning, "",
                            "правило вещества без совпадений: " + o.MaterialOverrides[mi].Key);
                    }
                }
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
                    if (p.Role == PartRole.Crystal && lib.LastResolveWasGuess)
                    {
                        scene.Add("W_MAT_GUESS", DiagSeverity.Warning, p.Name,
                            "Вещество кристалла назначено по общим словам: " + mat);
                    }
                    continue;
                }

                if (o.AllowDefaultMaterial || lib.DefaultFromFile)
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

        private static void SortParts(SceneModel scene)
        {
            scene.Parts.Sort(delegate (Part a, Part b)
            {
                return string.CompareOrdinal(a.Name, b.Name);
            });
        }

        private static bool WriteOutputs(PipelineOptions o, SceneModel scene, PipelineResult result)
        {
            string outDir = (o.OutDir != null && o.OutDir.Length > 0) ? o.OutDir : ".";
            Directory.CreateDirectory(outDir);

            string baseName = o.BaseName;
            if (baseName == null || baseName.Length == 0)
                baseName = DeriveBaseName(o);

            string gdmlPath = Path.Combine(outDir, baseName + ".gdml");
            string pngPath = Path.Combine(outDir, baseName + ".png");
            string reportPath = Path.Combine(outDir, baseName + ".report.txt");

            string gdmlTmp = gdmlPath + ".tmp";
            string pngTmp = pngPath + ".tmp";
            string reportTmp = reportPath + ".tmp";

            MaterialLibrary lib;
            if (!string.IsNullOrEmpty(o.MaterialsJsonPath))
                lib = MaterialLibrary.LoadJson(o.MaterialsJsonPath);
            else
                lib = MaterialLibrary.Default();

            var sketch = SketchBuilder.Build(scene);
            Bitmap pngBitmap = GdiSketchRenderer.Render(sketch, 1200, 800);

            string srcDesc = string.Join("; ", GetInputNames(scene));
            string report = SceneDescriber.Describe(scene, srcDesc, gdmlPath);

            GdmlWriteResult gdmlResult;
            try
            {
                gdmlResult = GdmlWriter.Write(scene, lib, gdmlTmp);
                pngBitmap.Save(pngTmp, ImageFormat.Png);
                File.WriteAllText(reportTmp, report, new UTF8Encoding(false));

                if (File.Exists(gdmlPath))
                    File.Replace(gdmlTmp, gdmlPath, null);
                else
                    File.Move(gdmlTmp, gdmlPath);

                if (File.Exists(pngPath))
                    File.Replace(pngTmp, pngPath, null);
                else
                    File.Move(pngTmp, pngPath);

                if (File.Exists(reportPath))
                    File.Replace(reportTmp, reportPath, null);
                else
                    File.Move(reportTmp, reportPath);
            }
            catch (Exception ex)
            {
                CleanupTemp(gdmlTmp, pngTmp, reportTmp);

                string code = ExtractMatCode(ex);
                scene.Add(code, DiagSeverity.Error, "", "Не удалось записать результат: " + ex.Message);
                return false;
            }
            finally
            {
                if (pngBitmap != null) pngBitmap.Dispose();
            }

            result.Gdml = gdmlResult;
            result.GdmlPath = gdmlPath;
            result.PngPath = pngPath;
            result.ReportPath = reportPath;
            return true;
        }

        private static void CleanupTemp(string a, string b, string c)
        {
            string[] files = new[] { a, b, c };
            for (int i = 0; i < files.Length; i++)
            {
                try
                {
                    if (File.Exists(files[i])) File.Delete(files[i]);
                }
                catch
                {
                }
            }
        }

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

        private static string GetExt(string path)
        {
            string ext = Path.GetExtension(path);
            return (ext != null) ? ext.ToLowerInvariant() : "";
        }

        private static PipelineResult Fail(PipelineResult result)
        {
            string firstError = null;
            string firstErrorMsg = null;
            for (int i = 0; i < result.Diagnostics.Count; i++)
            {
                if (result.Diagnostics[i].Severity == DiagSeverity.Error)
                {
                    firstError = result.Diagnostics[i].Code;
                    firstErrorMsg = result.Diagnostics[i].Message;
                    break;
                }
            }
            result.MainErrorId = firstError;
            result.MainErrorMessage = firstErrorMsg;
            result.ExitCode = DeriveExitCode(firstError);
            return result;
        }

        private static int DeriveExitCode(string code)
        {
            if (code == null) return 9;
            if (code.StartsWith("E_FORMAT") || code.StartsWith("E_UNIT_MISSING")
                || code.StartsWith("E_STEP"))
                return 2;
            if (code.StartsWith("E_MESH") || code.StartsWith("E_SCENE"))
                return 3;
            if (code.StartsWith("E_ROLE") || code.StartsWith("E_FRONT_AMBIG")
                || code.StartsWith("E_AXIS_AMBIG"))
                return 4;
            if (code.StartsWith("E_MAT") || code.StartsWith("E_ELEMENT"))
                return 5;
            return 9;
        }
    }
}
