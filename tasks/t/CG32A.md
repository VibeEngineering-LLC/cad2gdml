TARGET: src/Cad2Gdml.Core/Materials/CustomMaterialParser.cs
DEPENDS: src/Cad2Gdml.Core/Materials/MaterialLibrary.cs

CREATE `src/Cad2Gdml.Core/Materials/CustomMaterialParser.cs` (namespace `Cad2Gdml.Materials`, `public static class CustomMaterialParser`; usings System, System.Collections.Generic, System.Globalization, System.Text.RegularExpressions, Cad2Gdml.Gdml). Pure validation for the GUI button «Своё вещество…» (#CG-32), no WinForms. `MaterialDef` (fields `Name`, `DensityGcm3`, `MassFractions` as `List<KeyValuePair<string,double>>`, `IsNist`) already exists in `MaterialLibrary.cs`; `GdmlWriter.IsKnownElement(string symbol)` (public static bool, case-insensitive) exists in namespace `Cad2Gdml.Gdml`.

`public static bool TryParse(string name, string densityText, string compositionText, out MaterialDef def, out string error)`: on success true, `error` = null, `def` filled (`IsNist` = false); on any failure false, `def` = null, `error` = ONE Russian message (return at the first failed rule, in this order, never throw):
1. Name: `name` trimmed must match `^[A-Za-z][A-Za-z0-9_\-]{0,40}$` and must not start with "G4_" (ordinal ignore case). Error: "Имя: латиница, цифры, _ и -, первая буква; не G4_…; до 41 символа."
2. Density: trimmed `densityText`, replace ',' by '.', parse with `NumberStyles.Float` + `CultureInfo.InvariantCulture`; value must be finite, > 0 and <= 30. Error: "Плотность: число г/см³, больше 0 и не более 30."
3. Composition: split `compositionText` (null/blank is an error) by ';' and by line breaks into non-empty pairs; each pair `Symbol=Fraction` or `Symbol:Fraction` (split on the first '=' or ':'); fraction text: trim, ',' -> '.', invariant parse, must be > 0 and <= 1. Symbol: trim, canonical form = first letter upper-case + rest lower-case; must satisfy `GdmlWriter.IsKnownElement`; the same symbol twice is an error. Errors: "Состав: пусто. Пример: La=0.36688; Br=0.63312" for empty; "Состав: не разобрано «" + pair + "»." for a bad pair or fraction; "Состав: неизвестный элемент «" + symbol + "»." ; "Состав: элемент " + symbol + " повторяется."
4. Sum of fractions must be within 1.0 ± 1e-4 (inclusive). Error: "Состав: сумма долей = " + sum.ToString("0.#####", invariant) + ", нужно 1 ± 0,0001."
The resulting `MassFractions` keep input order with canonical symbols.

Keep it compact, comments in Russian, short. Think briefly.
