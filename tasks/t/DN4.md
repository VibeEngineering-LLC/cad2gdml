TARGET: src/Cad2Gdml.App/MainForm.cs
DEPENDS: src/Cad2Gdml.App/MainForm.cs, src/Cad2Gdml.App/SceneViewerForm.cs, src/Cad2Gdml.Core/Pipeline/Cad2GdmlPipeline.cs

FIX the existing file (return the COMPLETE file, keep every other behaviour). Four changes:

(1) Densities: add field `private List<KeyValuePair<string, double>> _densityOverrides = new List<KeyValuePair<string, double>>();` cleared wherever `_matOverrides.Clear()` is called. Before `dlg.ShowDialog(...)` of the scene/materials dialog call `dlg.SetInitialDensities(_densityOverrides);`. After the dialog returns OK, merge `dlg.DensityOverrides` into `_densityOverrides` by key exactly like `_matOverrides` is merged from `dlg.Overrides` (replace existing key, else add), and include `dlg.DensityOverrides.Count > 0` in the condition that decides the scene changed. When building `PipelineOptions`, if `_densityOverrides.Count > 0` set `opts.DensityOverrides = new List<KeyValuePair<string, double>>(_densityOverrides);`.

(2) Orientation: `_cmbFront.Items` become `"auto", "+z", "-z", "+x", "-x", "+y", "-y"` and the label text "Фронт:" becomes "Ось/фронт:"; the hint text at the top that mentions STEP in millimetres gets the appended sentence " Ось/фронт: буква — ось кристалла в файле, знак — передний торец." (keep it on the same label).

(3) Blender input: everywhere the program accepts extensions (file dialog `Filter` — add "Blender|*.blend" and `*.blend` to the "3D-файлы" group, `SafeGetFiles`, the status text "Нет поддерживаемых файлов (...)", and any place where `.step`/`.stp` is treated as "units come from the file") treat `.blend` like `.step`.

(4) Nothing else changes. Keep it compact; think briefly.
