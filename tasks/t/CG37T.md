TARGET: src/Cad2Gdml.Tests/Checks2n.cs
DEPENDS: src/Cad2Gdml.Tests/Checks2l.cs, src/Cad2Gdml.Core/Pipeline/InputListOps.cs

CREATE `src/Cad2Gdml.Tests/Checks2n.cs` (namespace `Cad2Gdml.Tests`, `public static class Checks2n`, `All(string tempDir)` returning `List<KeyValuePair<string, Action>>` in the style of `Checks2l`; usings System, System.Collections.Generic, Cad2Gdml.Pipeline). Four checks on `InputListOps` (each throws `Exception` with the check name and actual values on failure):
- `replace_ok`: items [`a.stl`,`b.stl`,`c.stl`]; `TryPickOne(3, new[]{1}, ...)` is true with index 1 and hint null; `TryReplace(items, 1, "x.stl", ...)` is true, items are exactly [`a.stl`,`x.stl`,`c.stl`] (count 3, order kept), hint null.
- `replace_pick_rejects`: `TryPickOne(3, new int[0], ...)` false, index -1, hint non-empty; `TryPickOne(3, new[]{0,2}, ...)` false, hint non-empty; `TryPickOne(0, new int[0], ...)` false; `TryPickOne(3, new[]{5}, ...)` false.
- `replace_duplicate_rejected`: items [`a.stl`,`b.stl`]; `TryReplace(items, 0, "B.STL", ...)` is false (case-insensitive duplicate of another item), hint non-empty, items unchanged; also `TryReplace(items, 0, "a.stl", ...)` false (same file), items unchanged.
- `replace_bad_args`: `TryReplace(items, 7, "x.stl")` false; `TryReplace(items, 0, "  ")` false; `TryReplace(null, 0, "x.stl")` false; items unchanged in the first two cases.
Register nothing else (the caller adds the registration line). Keep it compact; think briefly.
