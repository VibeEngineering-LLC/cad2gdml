=== T01 ===
TARGET: src/Cad2Gdml.Core/Geometry/Vec3d.cs
SECTIONS: 3.1

Namespace Cad2Gdml.Geometry. `public struct Vec3d` (readonly fields X,Y,Z double): ctor, static Zero/UnitX/UnitY/UnitZ,
operators + - (binary and unary) * (vector*double, double*vector) /, Dot, Cross, Length, LengthSq, Normalized() (zero vector stays zero),
DistanceTo, Equals/GetHashCode/ToString (InvariantCulture "(x, y, z)").
=== T02 ===
TARGET: src/Cad2Gdml.Core/Geometry/Affine3.cs
DEPENDS: src/Cad2Gdml.Core/Geometry/Vec3d.cs

Namespace Cad2Gdml.Geometry. `public struct Affine3`: 3x3 matrix M (row-major double[9] copy semantics via fields M00..M22) + translation T (Vec3d).
Members: static Identity, Translation(Vec3d), Scale(double), RotationAxisAngle(Vec3d axis, double radians) (Rodrigues),
RotationFromTo(Vec3d a, Vec3d b) (shortest rotation, handles parallel and antiparallel), FromColumns(Vec3d cx,Vec3d cy,Vec3d cz,Vec3d t),
Apply(Vec3d point), ApplyDirection(Vec3d), Multiply(Affine3 other) (this applied after other), Determinant(), Inverse() (throws InvalidOperationException if singular).
=== T03 ===
TARGET: src/Cad2Gdml.Core/Meshes/Mesh.cs
DEPENDS: src/Cad2Gdml.Core/Geometry/Vec3d.cs, src/Cad2Gdml.Core/Geometry/Affine3.cs

Namespace Cad2Gdml.Meshes. `public sealed class Mesh`: `List<Vec3d> Vertices`, `List<int> Indices` (triples), TriangleCount,
AddTriangle(Vec3d a, Vec3d b, Vec3d c) (appends 3 new vertices, no sharing), GetTriangle(int i, out Vec3d a, out Vec3d b, out Vec3d c),
Clone(), Transformed(Affine3 t) (new mesh; if t.Determinant()<0 also reverses winding of every triangle so volume stays positive),
FlipWinding() (in place), GetBounds(out Vec3d min, out Vec3d max) (throws if empty), Append(Mesh other).
Also `public static class MeshWelder` with `public static Mesh Weld(Mesh src, double tolerance)`: merge vertices closer than tolerance on a grid hash
(cell = tolerance, check 27 neighbour cells), drop triangles that become degenerate (two equal indices) and return a new indexed mesh.
=== T04 ===
TARGET: src/Cad2Gdml.Core/Meshes/MeshProperties.cs
DEPENDS: src/Cad2Gdml.Core/Geometry/Vec3d.cs, src/Cad2Gdml.Core/Meshes/Mesh.cs

Namespace Cad2Gdml.Meshes. `public static class MeshProperties`: SignedVolume(Mesh) (sum of tetrahedra with origin, mm^3), SurfaceArea(Mesh),
Centroid(Mesh) (volume-weighted; if |volume|<1e-12 returns average of vertices), InertiaTensorAboutCentroid(Mesh, out double[,] tensor) (unit density, 3x3),
and `public static class Eigen3` with `Jacobi(double[,] symmetric3x3, out double[] values, out Vec3d[] vectors)` (cyclic Jacobi, sorted ascending by value, unit vectors).
Use double precision, shift vertices to the centroid before integrating to limit round-off.
=== T05 ===
TARGET: src/Cad2Gdml.Core/Meshes/MeshTopology.cs
DEPENDS: src/Cad2Gdml.Core/Meshes/Mesh.cs

Namespace Cad2Gdml.Meshes. `public sealed class TopologyReport { int OpenEdges; int NonManifoldEdges; int InconsistentEdges; bool IsClosedManifold {get;} }`
and `public static class MeshTopology { public static TopologyReport Analyze(Mesh welded) }` for an INDEXED (welded) mesh: count directed edges;
an edge used once = open, used more than twice (undirected) = non-manifold, an undirected edge whose two uses go the same direction = inconsistent orientation.
=== T06 ===
TARGET: src/Cad2Gdml.Core/Scene/SceneModel.cs
SECTIONS: 5, 6
DEPENDS: src/Cad2Gdml.Core/Geometry/Vec3d.cs, src/Cad2Gdml.Core/Geometry/Affine3.cs, src/Cad2Gdml.Core/Meshes/Mesh.cs

Namespace Cad2Gdml.Scene. One file with these public types:
`enum PartRole { Other, Crystal, Source, Container, Shield, Housing }`; `enum DiagSeverity { Info, Warning, Error }`;
`sealed class Diagnostic { string Code; DiagSeverity Severity; string PartName; string Message; }` with ctor and ToString "[Code] part: message";
`enum SolidKind { Tessellated, Tube, Polycone, Box }`; `sealed class SolidShape { SolidKind Kind; double RMin, RMax, Z0, Z1 (tube: mm, z from..to, centre of axis at X0,Y0);
double X0, Y0; List<double> ZPlane, RInner, ROuter (polycone); double BoxX, BoxY, BoxZ; }`;
`sealed class Part { string Name; string OrigName; Mesh Mesh (in mm, in frame coordinates after framing); PartRole Role; string Material; SolidShape Shape; double VolumeMm3; string SourceFile; }`;
`sealed class SceneModel { List<Part> Parts; List<Diagnostic> Diagnostics; string Title; string SourceDescription; void Add(string code, DiagSeverity s, string part, string message); }`.
All lists initialised in constructors.
=== T07 ===
TARGET: src/Cad2Gdml.Core/Import/StlImporter.cs
DEPENDS: src/Cad2Gdml.Core/Scene/SceneModel.cs, src/Cad2Gdml.Core/Meshes/Mesh.cs

Namespace Cad2Gdml.Import. `public static class StlImporter { public static Part Load(string path, double unitToMm) }`:
detect binary vs ASCII (binary if file length == 84 + 50*count at the header count; otherwise ASCII text starting with "solid"),
read float32 triangles (BinaryReader, little endian) or ASCII `vertex x y z` lines (InvariantCulture, tolerate CRLF and extra spaces),
multiply coordinates by unitToMm, name = file name without extension, throw InvalidDataException for empty/garbage files.
Also `public static IList<Part> LoadFolder(string folder, double unitToMm)` that loads every *.stl in a folder sorted by ordinal file name.
=== T08 ===
TARGET: src/Cad2Gdml.Core/Import/ObjImporter.cs
DEPENDS: src/Cad2Gdml.Core/Scene/SceneModel.cs, src/Cad2Gdml.Core/Meshes/Mesh.cs

Namespace Cad2Gdml.Import. `public static class ObjImporter { public static IList<Part> Load(string path, double unitToMm) }`:
parse `v x y z`, `o name`/`g name` (each starts a new Part; faces before any o/g go to a part named after the file), `f` with v, v/vt, v//vn, v/vt/vn forms,
negative indices (relative), polygons with >3 vertices fan-triangulated; ignore vt/vn/usemtl/mtllib/comments; InvariantCulture; scale by unitToMm; skip empty groups.
=== T09 ===
TARGET: src/Cad2Gdml.Core/Import/ThreeMfImporter.cs
DEPENDS: src/Cad2Gdml.Core/Scene/SceneModel.cs, src/Cad2Gdml.Core/Meshes/Mesh.cs, src/Cad2Gdml.Core/Geometry/Affine3.cs, src/Cad2Gdml.Core/Geometry/Vec3d.cs

Namespace Cad2Gdml.Import. `public static class ThreeMfImporter { public static IList<Part> Load(string path, double unitOverrideToMm /* <=0 means: use the model unit attribute */) }`:
open the file as a ZIP (System.IO.Compression.ZipFile), find the model part (3D/3dmodel.model, or the path from _rels/.rels), parse with XmlReader/XDocument ignoring XML namespaces,
`unit` attribute of <model> (micron=0.001, millimeter=1, centimeter=10, inch=25.4, foot=304.8, meter=1000, default millimeter) -> mm,
<resources><object id name type><mesh><vertices><vertex x y z/> <triangles><triangle v1 v2 v3/>; objects with <components><component objectid transform/> are
resolved recursively (transform "m00 m01 m02 m10 ... m22 m30 m31 m32": row-vector convention, last three numbers are translation); <build><item objectid transform/>
instantiates objects. One Part per build item, name = object name or "obj"+id (add "_n" suffix when the same name repeats). InvariantCulture everywhere.
=== T10 ===
TARGET: src/Cad2Gdml.Core/Json/MiniJson.cs

Namespace Cad2Gdml.Json. Minimal JSON reader/writer, no external packages: `public static class MiniJson { public static object Parse(string text); public static string Serialize(object value, bool indented); }`.
Parse returns Dictionary<string,object>, List<object>, string, double, bool or null; supports \uXXXX escapes, numbers via InvariantCulture, throws FormatException with position on errors.
Serialize accepts the same types plus int/long/IEnumerable<KeyValuePair<string,object>>; strings escaped; UTF-8 text is NOT escaped.
=== T11 ===
TARGET: src/Cad2Gdml.Core/Recognize/RevolutionRecognizer.cs
SECTIONS: 4.1
DEPENDS: src/Cad2Gdml.Core/Scene/SceneModel.cs, src/Cad2Gdml.Core/Meshes/Mesh.cs, src/Cad2Gdml.Core/Geometry/Vec3d.cs

Namespace Cad2Gdml.Recognize. `public sealed class RecognizeOptions { double RevTolMm = 0.01; int RevMinSegments = 24; }`
and `public static class RevolutionRecognizer { public static SolidShape TryRecognize(Part part, RecognizeOptions opt, out string whyNot) }`.
The part mesh is already in the frame: the axis is parallel to z; first find the axis position as the mean of x,y of the vertices' bounding box centre (x0,y0).
Simplified algorithm for v0.1 (documented in a comment): vertices -> (r,z) with r = distance to the axis; group vertices by z rounded to RevTolMm into levels;
n_min = smallest vertex count among levels that carry vertices; if n_min < RevMinSegments -> return null (whyNot "I_REV_COARSE"); at every level all vertices must have the same
radius within RevTolMm per ring (a level may hold one outer ring and optionally one inner ring: split the radii into two clusters by gap > 10*RevTolMm); build the profile as
pairs (rInner, rOuter) per level; ring radius = maximal vertex radius of the ring (vertices lie on the surface, chords inside); then check (a) every vertex lies on its level's ring
and (b) volume: mesh volume <= profile volume and (profileVolume-meshVolume)/profileVolume <= 1.5*(2*pi/n_min)^2/6 + 1e-6 (profile volume by Pappus). If two levels only (z0,z1) and inner radius 0 or constant:
return Kind=Tube (RMin,RMax,Z0,Z1,X0,Y0); if more levels with consistent steps: Kind=Polycone with ZPlane/RInner/ROuter lists (a step uses the same z twice); else null.
Do not recognize a part with fewer than 24 vertices per ring as revolution (a hexagonal prism must NOT become a cylinder).
=== T12 ===
TARGET: src/Cad2Gdml.Core/Recognize/BoxRecognizer.cs
SECTIONS: 4.2
DEPENDS: src/Cad2Gdml.Core/Scene/SceneModel.cs, src/Cad2Gdml.Core/Meshes/Mesh.cs, src/Cad2Gdml.Core/Geometry/Vec3d.cs

Namespace Cad2Gdml.Recognize. `public static class BoxRecognizer { public static SolidShape TryRecognize(Part part, double tolMm, out string whyNot) }`:
the part is a box aligned with the frame axes if its bounding box volume equals the mesh volume within 1e-4 relative and every vertex coordinate lies on one of the
six bounding planes within tolMm on all three axes (i.e. each vertex is a bounding box corner). Result Kind=Box, BoxX/Y/Z full lengths, centre = bounding box centre stored in X0,Y0 and Z0 (Z0=centre z, Z1=centre z).
=== T13 ===
TARGET: src/Cad2Gdml.Core/Scene/FrameBuilder.cs
SECTIONS: 3.2
DEPENDS: src/Cad2Gdml.Core/Scene/SceneModel.cs, src/Cad2Gdml.Core/Meshes/Mesh.cs, src/Cad2Gdml.Core/Meshes/MeshProperties.cs, src/Cad2Gdml.Core/Geometry/Vec3d.cs, src/Cad2Gdml.Core/Geometry/Affine3.cs

Namespace Cad2Gdml.Scene. `public sealed class FrameOptions { string FrontSign = "auto" /* "+z","-z","auto" */; double SnapAxisMm = 0.01; }`
`public static class FrameBuilder { public static Affine3 Build(SceneModel scene, Part crystal, FrameOptions opt) }` returns the transform that moves the whole scene into the frame:
crystal axis -> z (axis from inertia tensor eigenvector whose eigenvalue differs from the two others; for a near isotropic tensor use bounding box longest... if axis ambiguous add Diagnostic E_AXIS_AMBIG and throw InvalidOperationException),
x=y=0 on the crystal axis, z=0 at the crystal face that looks toward the other parts ("front"): with FrontSign auto, the face toward the centre of mass of all non-crystal parts
(if there are none, the face with less neighbour mass; if undecidable throw E_FRONT_AMBIG). +z points from the crystal toward the source. Also `public static void Apply(SceneModel scene, Affine3 t)` that
replaces every Part.Mesh with Mesh.Transformed(t).
=== T14 ===
TARGET: src/Cad2Gdml.Core/Materials/MaterialLibrary.cs
SECTIONS: 5

Namespace Cad2Gdml.Materials. `public sealed class MaterialDef { string Name; double DensityGcm3; List<KeyValuePair<string,double>> MassFractions; bool IsNist; }` (MassFractions by element symbol, sum 1).
`public sealed class MaterialRule { string Pattern; string Material; }` (case-insensitive Regex on the part name, first match wins),
`public sealed class MaterialLibrary { static MaterialLibrary Default(); Dictionary<string,MaterialDef> Materials; List<MaterialRule> Rules; string DefaultMaterial; MaterialDef Find(string name);
string Resolve(string partName) /* rule -> material name or null */; static MaterialLibrary LoadJson(string path) /* uses Cad2Gdml.Json.MiniJson, keys: custom{name:{density,elements{sym:frac}}}, stl_rules[[regex,material]], stl_default */; }`.
Default(): built-in G4-style materials with realistic data: G4_AIR(0.001205), G4_WATER(1.0), G4_Al(2.699), G4_Fe(7.874), G4_Cu(8.96), G4_Pb(11.35), G4_Sn(7.31), G4_Si(2.33), G4_POLYETHYLENE(0.94, H0.143711 C0.856289),
NaI (3.67, Na 0.1534 I 0.8466), NaI_Tl same, CsI(4.51, Cs 0.5113 I 0.4887), and rules: "crystal|nai|scint|кристалл" -> NaI, "shell|housing|case|корпус|оболоч" -> G4_Al, "water|вода|sample|проб" -> G4_WATER,
"lead|pb|свинец" -> G4_Pb, "cu|copper|медь" -> G4_Cu, "air|воздух" -> G4_AIR.
=== T15 ===
TARGET: src/Cad2Gdml.Core/Scene/RoleAssigner.cs
SECTIONS: 6
DEPENDS: src/Cad2Gdml.Core/Scene/SceneModel.cs

Namespace Cad2Gdml.Scene. `public sealed class RoleOptions { string CrystalRegex = "crystal|кристалл|nai|csi|labr|ceBr|scint"; string SourceRegex = "water|sample|source|проб|вода|marinelli|маринелли"; string ContainerRegex = "vessel|beaker|container|стакан|сосуд|bottle|кювет"; string ShieldRegex = "lead|pb|shield|защит|свинец"; string HousingRegex = "shell|housing|case|корпус|оболоч"; }`
`public static class RoleAssigner { public static void Assign(SceneModel scene, RoleOptions opt, string crystalNameOverride) }`: first match by order Crystal, Container, Source, Shield, Housing on Part.Name (case-insensitive), others Other;
if crystalNameOverride is non-empty that part is the crystal. Add Diagnostic E_ROLE_NONE (Error) when no crystal, E_ROLE_AMBIG (Error) when more than one crystal match, I_ROLE (Info) per assignment.
=== T16 ===
TARGET: src/Cad2Gdml.Core/Gdml/GdmlWriter.cs
SECTIONS: 7.1, 7.2
DEPENDS: src/Cad2Gdml.Core/Scene/SceneModel.cs, src/Cad2Gdml.Core/Materials/MaterialLibrary.cs, src/Cad2Gdml.Core/Meshes/Mesh.cs

Namespace Cad2Gdml.Gdml. `public sealed class GdmlWriteResult { int Volumes; int Tessellated; int Triangles; int Tubes; int Polycones; int Boxes; string Sha256; }`
`public static class GdmlName { public static string Make(string raw, HashSet<string> used) }` (ASCII-only: Cyrillic transliterated, other chars -> '_', leading digit prefixed with 'p', unique with _2,_3).
`public static class GdmlWriter { public static GdmlWriteResult Write(SceneModel scene, MaterialLibrary lib, string path) }`: write UTF-8 (no BOM, LF newlines) GDML 3.x: <define> (positions in mm only when non-zero),
<materials> (elements by symbol with Z from a built-in table H..U for the symbols used, atomic mass A; materials with density g/cm3 and <fraction ref>; G4_* names used by reference only when IsNist, otherwise define them),
<solids> (tube -> <tube aunit="rad" lunit="mm" rmin rmax z (full length) startphi=0 deltaphi=2*pi>, polycone -> <polycone> with <zplane rmin rmax z>, box -> <box x y z full lengths>,
tessellated -> <tessellated> with <triangular vertex1..3 type="ABSOLUTE"> over named <position> entries in define), plus a world <box> 2x the scene bounding box with G4_AIR,
<structure> (one <volume> per part with materialref, solidref and <auxiliary auxtype="Cad2Gdml:Role" auxvalue=role/>; world volume with <physvol> and <position> where needed), <setup name="Default" version="1.0"><world ref=.../>.
All numbers InvariantCulture "R" with up to 9 significant digits; sha256 of the written bytes returned in Sha256; output deterministic (same input -> same bytes).
=== T17 ===
TARGET: src/Cad2Gdml.Core/Report/SceneDescriber.cs
SECTIONS: 9
DEPENDS: src/Cad2Gdml.Core/Scene/SceneModel.cs

Namespace Cad2Gdml.Report. `public static class SceneDescriber { public static string Describe(SceneModel scene, string sourceFile, string gdmlFile) }` returns a Russian text report (LF newlines):
title, source file, number of parts, table of parts (name, role, material, kind: tube/polycone/box/tessellated, volume cm3 with 2 decimals InvariantCulture, bbox size mm),
a "Детектор" line (crystal: `Детектор: ⌀D × H мм (материал)` for a tube with D=2*RMax formatted "0.##" InvariantCulture, H = Z1-Z0), a "Образец" line if a Source exists with volume in ml,
a "Предупреждения" section listing the Diagnostics with severity Warning/Error, and a final section "Что не проверено" with fixed lines (physics of the materials is not checked; overlaps are not checked in v0.1).
=== T18 ===
TARGET: src/Cad2Gdml.Core/Sketch/SketchBuilder.cs
SECTIONS: 8.1
DEPENDS: src/Cad2Gdml.Core/Scene/SceneModel.cs, src/Cad2Gdml.Core/Geometry/Vec3d.cs, src/Cad2Gdml.Core/Meshes/Mesh.cs

Namespace Cad2Gdml.Sketch. Model of the 2D section drawing (section by the plane y=0, view x horizontal, z vertical, z up toward the source):
`sealed class SketchPoly { string PartName; PartRole Role; List<double[]> Points /*x,z*/; bool Closed; }`, `sealed class SketchLabel { string Text; double X, Z; }`,
`sealed class SketchModel { List<SketchPoly> Polys; List<SketchLabel> Labels; double MinX, MaxX, MinZ, MaxZ; List<string> CaptionLines; }`,
`static class Palette { static string ColorHex(PartRole role, string partName) }` (Crystal blue 5B8DEF, Source teal 2BB7A3, Container grey 9AA5B1, Shield dark grey 5A6370, Housing orange F2A154, Other: deterministic hash of the name to a hue),
`static class CaptionFormatter { static List<string> Lines(SceneModel scene) }` (line 1 "Детектор: ⌀50.8 × 50.8 мм" with "0.##" InvariantCulture, line 2 "Стакан Маринелли: ⌀145 × 109 мм" for a Container tube; then "Образец: ..." and "Не в расчёте: ..." only when applicable),
`static class SketchBuilder { static SketchModel Build(SceneModel scene) }`: tube -> mirrored outline rectangles (outer, minus inner for hollow: two rectangles per side), polycone -> mirrored outline polygon from the zplanes,
box -> rectangle, tessellated -> section segments of triangles with y=0 chained into polylines (joined by endpoint within 1e-4 mm); compute extents; add the labels at centres of parts.
=== T19 ===
TARGET: src/Cad2Gdml.Core/Sketch/GdiSketchRenderer.cs
SECTIONS: 8.1
DEPENDS: src/Cad2Gdml.Core/Sketch/SketchBuilder.cs

Namespace Cad2Gdml.Sketch. `public static class GdiSketchRenderer { public static System.Drawing.Bitmap Render(SketchModel model, int width, int height); public static void SavePng(SketchModel model, string path, int width, int height); }`
using System.Drawing (GDI+): white background, uniform scale to fit with a 40 px margin and equal x/z scale, anti-aliasing on, fill closed polygons with the role colour at alpha 120 and 1.5 px dark outline,
draw polylines of open sections, part labels in 9 pt font centred, the caption lines at the top-left in 10 pt bold, a scale bar (round length 10/20/50/100 mm) bottom-left, axis z arrow. Never throw for an empty model (draw the text "пусто").
=== T20 ===
TARGET: src/Cad2Gdml.Core/Pipeline/Cad2GdmlPipeline.cs
SECTIONS: 10.1
DEPENDS: src/Cad2Gdml.Core/Scene/SceneModel.cs, src/Cad2Gdml.Core/Scene/FrameBuilder.cs, src/Cad2Gdml.Core/Scene/RoleAssigner.cs, src/Cad2Gdml.Core/Materials/MaterialLibrary.cs, src/Cad2Gdml.Core/Recognize/RevolutionRecognizer.cs, src/Cad2Gdml.Core/Recognize/BoxRecognizer.cs, src/Cad2Gdml.Core/Gdml/GdmlWriter.cs, src/Cad2Gdml.Core/Report/SceneDescriber.cs, src/Cad2Gdml.Core/Sketch/SketchBuilder.cs, src/Cad2Gdml.Core/Sketch/GdiSketchRenderer.cs

Namespace Cad2Gdml.Pipeline. `sealed class PipelineOptions { List<string> Inputs; string OutDir; string BaseName; double UnitToMm (0 = missing -> error E_UNIT_MISSING for STL/OBJ); string UnitName; string CrystalName; string FrontSign; string MaterialsJsonPath; bool AllowDefaultMaterial; }`,
`sealed class PipelineResult { int ExitCode; SceneModel Scene; string GdmlPath, PngPath, ReportPath; GdmlWriteResult Gdml; List<Diagnostic> Diagnostics; }`,
`static class Cad2GdmlPipeline { static PipelineResult Run(PipelineOptions o) }`: load inputs by extension (.stl file or folder, .obj, .3mf; .step/.stp -> Diagnostic E_STEP_NOT_SUPPORTED Error exit 2),
weld each mesh (1e-5 mm), compute topology: open edges -> Error E_MESH_OPEN exit 3, negative volume -> flip with W_MESH_FLIPPED; assign roles, find crystal (exit 4 on role errors), build the frame and apply,
recognize Tube/Polycone then Box, else Tessellated; resolve materials by rules (unresolved -> E_MAT_UNASSIGNED exit 5 unless AllowDefaultMaterial -> default with Warning), write <base>.gdml, <base>.png (1200x800), <base>.report.txt.
Exit codes: 0 ok, 2 input/unit/format error, 3 mesh error, 4 role/frame error, 5 material error. Catch all exceptions into an Error diagnostic E_INTERNAL with exit 9. Must be deterministic.
=== T21 ===
TARGET: src/Cad2Gdml.Cli/Program.cs
SECTIONS: 10.1
DEPENDS: src/Cad2Gdml.Core/Pipeline/Cad2GdmlPipeline.cs

Namespace Cad2Gdml.Cli. Console `Main(string[] args)`: usage `cad2gdml <input>... --out <dir> [--name base] [--unit mm|cm|m|inch] [--crystal <part>] [--front +z|-z] [--materials file.json] [--allow-default-material]`;
inputs are files or folders; prints a Russian summary (counts, GDML path, sha256, diagnostics with codes) and returns the pipeline ExitCode; `--help` returns 0; unknown option returns 2 with a message.
=== T22 ===
TARGET: src/Cad2Gdml.App/MainForm.cs
SECTIONS: 10.2
DEPENDS: src/Cad2Gdml.Core/Pipeline/Cad2GdmlPipeline.cs, src/Cad2Gdml.Core/Sketch/SketchBuilder.cs, src/Cad2Gdml.Core/Sketch/GdiSketchRenderer.cs

Namespace Cad2Gdml.App. WinForms `public sealed class MainForm : Form` built in code (no designer): top panel with "Добавить файлы…" (OpenFileDialog stl/obj/3mf), "Добавить папку…", unit ComboBox (мм/см/м/дюйм, default мм), "Кристалл:" TextBox,
"Фронт:" ComboBox (auto,+z,-z), "Папка вывода:" TextBox + "Обзор…", button "Конвертировать". Left: ListBox of inputs. Centre: PictureBox (Zoom) with the sketch PNG. Bottom: multiline read-only TextBox with the report text and diagnostics.
Convert runs the pipeline on a background Task (BackgroundWorker is fine), disables controls while running, shows the picture and report afterwards, red status line on errors. Also `public static class Program { [STAThread] static void Main() }`.
Russian UI strings. Remember the last folder in the form only (no registry).
