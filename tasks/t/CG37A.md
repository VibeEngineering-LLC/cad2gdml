TARGET: src/Cad2Gdml.Core/Pipeline/InputListOps.cs

CREATE `src/Cad2Gdml.Core/Pipeline/InputListOps.cs` (namespace `Cad2Gdml.Pipeline`, `public static class InputListOps`; usings System, System.Collections.Generic). Pure logic for the GUI button «Заменить…» (#CG-37), no WinForms.

1. `public static bool TryPickOne(int itemCount, IList<int> selected, out int index, out string hint)`: true only when `selected` is not null and contains exactly one index and that index is in `[0, itemCount)`; then `index` = it, `hint` = null. Otherwise false, `index` = -1 and `hint`: if `itemCount == 0` -> "Список пуст: сначала добавьте файл."; else if selection is empty -> "Выберите в списке слева один файл, затем «Заменить…»."; else (two or more, or index out of range) -> "Для замены выберите ровно один файл."
2. `public static bool TryReplace(IList<string> items, int index, string newPath, out string hint)`: false (items untouched) with a Russian `hint` when: `items` null or `index` out of range or `newPath` null/empty/whitespace ("Не указан новый файл."); when `newPath` equals (ordinal ignore case) `items[index]` ("Выбран тот же файл."); when `newPath` equals (ordinal ignore case) ANY OTHER item ("Файл уже есть в списке: " + newPath). Otherwise set `items[index] = newPath` (same position, order of the others unchanged), `hint` = null, return true.

Keep it compact, comments in Russian, short. Think briefly.
