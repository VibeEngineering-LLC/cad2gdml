TARGET: src/Cad2Gdml.Cli/Program.cs
DEPENDS: src/Cad2Gdml.Cli/Program.cs, src/Cad2Gdml.Core/Pipeline/Cad2GdmlPipeline.cs

FIX the existing file (return the COMPLETE file, keep every other behaviour). Three changes:

(1) New repeatable option `--density <вещество>=<г/см3>`: parse like `--material` (split at the first '=', both parts trimmed and non-empty, value parsed with `double.TryParse(..., NumberStyles.Float, CultureInfo.InvariantCulture, ...)` after replacing ',' by '.', must be > 0; otherwise print "Формат --density: <вещество>=<г/см3>. Получено: ..." to stderr and return 2). Collect into `var densityOverrides = new List<KeyValuePair<string, double>>();` and pass `DensityOverrides = (densityOverrides.Count > 0) ? densityOverrides : null` in the options. Help line: `  --density <вещество>=<г/см3> Плотность вещества вместо справочной (повторяемый)`.

(2) `--front` now accepts `+x, -x, +y, -y, +z, -z, auto`; update the validation, the error text and the help line to `  --front +z|-z|+x|-x|+y|-y|auto  Ось кристалла в файле (буква) и передний торец (знак)`.

(3) Wherever the CLI lists or checks supported extensions or decides that units are not required (STEP), add `.blend` alongside `.step`/`.stp`; help text for inputs mentions `.blend`. Keep it compact; think briefly.
