using System;
using System.Collections.Generic;

namespace Cad2Gdml.Pipeline
{
    // Операции над списком входных файлов (кнопка «Заменить…», #CG-37)
    public static class InputListOps
    {
        // Проверка, что выбран ровно один валидный индекс
        public static bool TryPickOne(int itemCount, IList<int> selected, out int index, out string hint)
        {
            index = -1;

            if (itemCount == 0)
            {
                hint = "Список пуст: сначала добавьте файл.";
                return false;
            }

            if (selected == null || selected.Count == 0)
            {
                hint = "Выберите в списке слева один файл, затем «Заменить…».";
                return false;
            }

            if (selected.Count == 1)
            {
                int idx = selected[0];
                if (idx >= 0 && idx < itemCount)
                {
                    index = idx;
                    hint = null;
                    return true;
                }
            }

            hint = "Для замены выберите ровно один файл.";
            return false;
        }

        // Замена элемента в списке на новый путь (позиция не меняется)
        public static bool TryReplace(IList<string> items, int index, string newPath, out string hint)
        {
            if (items == null || index < 0 || index >= items.Count ||
                string.IsNullOrWhiteSpace(newPath))
            {
                hint = "Не указан новый файл.";
                return false;
            }

            if (string.Equals(newPath, items[index], StringComparison.OrdinalIgnoreCase))
            {
                hint = "Выбран тот же файл.";
                return false;
            }

            // Проверка дубля среди остальных элементов
            for (int i = 0; i < items.Count; i++)
            {
                if (i == index) continue;
                if (string.Equals(newPath, items[i], StringComparison.OrdinalIgnoreCase))
                {
                    hint = "Файл уже есть в списке: " + newPath;
                    return false;
                }
            }

            items[index] = newPath;
            hint = null;
            return true;
        }
    }
}
