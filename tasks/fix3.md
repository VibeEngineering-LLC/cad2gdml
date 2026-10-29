=== H01 ===
TARGET: src/Cad2Gdml.Core/Gdml/GdmlWriter.cs
DEPENDS: src/Cad2Gdml.Core/Gdml/GdmlWriter.cs, src/Cad2Gdml.Core/Materials/MaterialLibrary.cs, src/Cad2Gdml.Core/Scene/SceneModel.cs, src/Cad2Gdml.Core/Meshes/Mesh.cs

FIX the existing file (return the COMPLETE corrected file, keep the public API and every behaviour not mentioned). Defects: (1) all numbers are rounded to 4 decimals by one Fmt helper; this is right for lengths and coordinates in mm but wrong for densities (g/cm3) and mass fractions: add a second helper `FmtPrecise(double)` = 9 significant digits ("R"-like, InvariantCulture, no exponent notation when |v| >= 1e-4 else use "0.#########" format), and use it for `<D value>`, `<fraction n>`, `<atom value>`; a density or fraction that would print as 0 is an InvalidDataException("E_MAT_BAD_VALUE: ..."). (2) References inside `<fraction ref="...">` and in `<materialref ref>` must use the CANONICAL names of the definitions (MaterialDef.Name / the element name as written in the element table), not the text the user typed (e.g. "nai" -> "NaI", "SI" -> "Si"): resolve through the library's case-insensitive Find and the element table; names starting with "G4_" are written as given. (3) Material-related errors must carry their own ids as exception messages starting with the id: "E_MAT_CYCLE: ...", "E_ELEMENT_UNKNOWN: ...", "E_MAT_UNKNOWN: ..." (as now), so the pipeline can map them to exit code 5.
=== H02 ===
TARGET: src/Cad2Gdml.Core/Materials/MaterialLibrary.cs
DEPENDS: src/Cad2Gdml.Core/Materials/MaterialLibrary.cs, src/Cad2Gdml.Core/Json/MiniJson.cs

FIX the existing file (return the complete corrected file, keep the public API). Defects: (1) `DefaultFromFile` is set to true even when `stl_default` is null or an empty string because of missing braces: set it only when stl_default is a non-empty string. (2) An invalid regular expression in `stl_rules` is silently skipped: throw InvalidDataException("E_FORMAT: bad regex in stl_rules: ..."). (3) Negative or NaN density/fractions -> InvalidDataException("E_MAT_BAD_VALUE: ..."). (4) MiniJson-level problems (including too deep nesting) must surface as InvalidDataException (the file loader wraps FormatException and similar into InvalidDataException("E_FORMAT: ...")).
=== H03 ===
TARGET: src/Cad2Gdml.Core/Json/MiniJson.cs
DEPENDS: src/Cad2Gdml.Core/Json/MiniJson.cs

FIX the existing file (return the complete corrected file, keep the public API). Defect: deeply nested JSON (60000 levels of `[`) crashes the process with StackOverflowException. Add a maximum nesting depth of 200 in the parser: deeper -> throw FormatException("JSON: nesting too deep") (depth counter passed through the recursive calls, decremented on return).
=== H04 ===
TARGET: src/Cad2Gdml.Core/Pipeline/Cad2GdmlPipeline.cs
SECTIONS: 10.1
DEPENDS: src/Cad2Gdml.Core/Pipeline/Cad2GdmlPipeline.cs, src/Cad2Gdml.Core/Scene/SceneModel.cs, src/Cad2Gdml.Core/Materials/MaterialLibrary.cs

FIX the existing file (return the COMPLETE corrected file, keep the public API and every behaviour not mentioned). Defects: (1) Exit codes: an InvalidDataException from the GDML writer whose message starts with "E_MAT_" or "E_ELEMENT_" (E_MAT_CYCLE, E_MAT_UNKNOWN, E_MAT_BAD_VALUE, E_ELEMENT_UNKNOWN) must become a diagnostic with that id and exit code 5, not E_FORMAT/2; UnauthorizedAccessException while loading inputs -> E_FORMAT, exit 2. (2) Output writing is not atomic: the old files are deleted BEFORE the new ones are ready. New procedure: write all three files to ".tmp" names; if all writes succeeded, for each final name use `File.Replace` when the target exists (else `File.Move`); on any exception delete every ".tmp" file that was created, leave the old results untouched and report E_FORMAT (exit 2) "не удалось записать результат: ...". Catch every Exception type in this block (GDI+ ExternalException included), not only IOException. (3) W_SOURCE_BEHIND is a false positive for a container that surrounds the crystal: use the rule "the bounding box of the Source/Container part lies completely at z <= 0 (its maximum z <= 0.001)" instead of the centre test.
=== H05 ===
TARGET: src/Cad2Gdml.Core/Import/ThreeMfImporter.cs
DEPENDS: src/Cad2Gdml.Core/Import/ThreeMfImporter.cs, src/Cad2Gdml.Core/Scene/SceneModel.cs, src/Cad2Gdml.Core/Meshes/Mesh.cs, src/Cad2Gdml.Core/Geometry/Affine3.cs, src/Cad2Gdml.Core/Geometry/Vec3d.cs

FIX the existing file (return the complete corrected file, keep the public API). Defect: the 50-million-triangle limit is checked only after the whole mesh is assembled, so a small file with 22 levels of doubling components needs seconds and gigabytes. Compute the triangle count of every object recursively WITHOUT building meshes first (count(object) = own triangles + sum of count(child) over its components, memoised per object id, using long arithmetic with saturation) and throw InvalidDataException("3MF: too large") before any mesh is built when the count of a build item exceeds 5 million triangles; also keep a running counter while building and throw as soon as the limit is crossed.
=== H06 ===
TARGET: src/Cad2Gdml.App/MainForm.cs
DEPENDS: src/Cad2Gdml.App/MainForm.cs, src/Cad2Gdml.Core/Pipeline/Cad2GdmlPipeline.cs

FIX the existing file (return the complete corrected file). Defects: (1) after a FAILED conversion the picture and the report text of the previous successful run stay on screen: clear the picture (dispose) and show the diagnostics list of the failed run (each as "[CODE] part: message") in the report box. (2) The red status line must read `Сбой (код N): ID — сообщение`; when MainErrorId is empty use the message of the last diagnostic instead of " — ". (3) Mixed inputs (3MF together with STL/OBJ): the unit ComboBox value must apply only to non-3MF files and the 3MF files keep their own unit. The pipeline has one UnitToMm option, so for mixed input show a MessageBox error "Смешанные входы 3MF и STL/OBJ: конвертируйте раздельно" and do not run; also fix the hint label text to "для 3MF единицы берутся из файла (только если все входы 3MF)".
