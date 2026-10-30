TARGET: src/Cad2Gdml.App/MainForm.Custom.cs
DEPENDS: src/Cad2Gdml.App/SceneViewerForm.Custom.cs

REWRITE `src/Cad2Gdml.App/MainForm.Custom.cs` completely: it must contain ONLY the second half of `public sealed partial class MainForm : Form` (namespace `Cad2Gdml.App`; usings System.Collections.Generic, System.Windows.Forms, Cad2Gdml.Materials) with exactly ONE member: `private List<MaterialDef> _customMaterials = SceneViewerForm.SessionCustom;` with a short Russian comment above (#CG-32: the same list that the «Сцена и вещества» window fills; it goes to PipelineOptions.CustomMaterials). No methods. Keep it compact.
