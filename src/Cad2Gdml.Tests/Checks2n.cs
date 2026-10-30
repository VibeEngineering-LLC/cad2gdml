using System;
using System.Collections.Generic;
using Cad2Gdml.Pipeline;

namespace Cad2Gdml.Tests
{
    // Проверки 2n: операции со списком входных файлов (InputListOps)
    public static class Checks2n
    {
        public static List<KeyValuePair<string, Action>> All(string tempDir)
        {
            var list = new List<KeyValuePair<string, Action>>();
            list.Add(new KeyValuePair<string, Action>("replace_ok", () => ReplaceOk()));
            list.Add(new KeyValuePair<string, Action>("replace_pick_rejects", () => ReplacePickRejects()));
            list.Add(new KeyValuePair<string, Action>("replace_duplicate_rejected", () => ReplaceDuplicateRejected()));
            list.Add(new KeyValuePair<string, Action>("replace_bad_args", () => ReplaceBadArgs()));
            return list;
        }

        // Успешная замена: выбор индекса и замена элемента
        private static void ReplaceOk()
        {
            var items = new List<string> { "a.stl", "b.stl", "c.stl" };
            int idx;
            string hint;

            bool picked = InputListOps.TryPickOne(3, new[] { 1 }, out idx, out hint);
            if (!picked || idx != 1 || hint != null)
                throw new Exception("replace_ok: TryPickOne(3,{1}) → picked=" + picked + ", idx=" + idx + ", hint=" + hint);

            bool replaced = InputListOps.TryReplace(items, 1, "x.stl", out hint);
            if (!replaced)
                throw new Exception("replace_ok: TryReplace → false, hint=" + hint);
            if (hint != null)
                throw new Exception("replace_ok: hint не null: " + hint);
            if (items.Count != 3)
                throw new Exception("replace_ok: Count=" + items.Count + ", ожидаем 3");
            if (items[0] != "a.stl" || items[1] != "x.stl" || items[2] != "c.stl")
                throw new Exception("replace_ok: содержимое: " + items[0] + "," + items[1] + "," + items[2]);
        }

        // Отказ при неверном выборе индекса
        private static void ReplacePickRejects()
        {
            int idx;
            string hint;

            // Пустой выбор
            bool p1 = InputListOps.TryPickOne(3, new int[0], out idx, out hint);
            if (p1) throw new Exception("replace_pick_rejects: TryPickOne(3,empty) должен быть false");
            if (idx != -1) throw new Exception("replace_pick_rejects: idx=" + idx + ", ожидаем -1");
            if (string.IsNullOrEmpty(hint)) throw new Exception("replace_pick_rejects: hint пуст (1)");

            // Два индекса
            bool p2 = InputListOps.TryPickOne(3, new[] { 0, 2 }, out idx, out hint);
            if (p2) throw new Exception("replace_pick_rejects: TryPickOne(3,{0,2}) должен быть false");
            if (string.IsNullOrEmpty(hint)) throw new Exception("replace_pick_rejects: hint пуст (2)");

            // Пустой список
            bool p3 = InputListOps.TryPickOne(0, new int[0], out idx, out hint);
            if (p3) throw new Exception("replace_pick_rejects: TryPickOne(0,empty) должен быть false");

            // Индекс вне диапазона
            bool p4 = InputListOps.TryPickOne(3, new[] { 5 }, out idx, out hint);
            if (p4) throw new Exception("replace_pick_rejects: TryPickOne(3,{5}) должен быть false");
        }

        // Отказ при дубле (case-insensitive) и при замене на сам файл
        private static void ReplaceDuplicateRejected()
        {
            var items = new List<string> { "a.stl", "b.stl" };
            string hint;

            // Case-insensitive дубль другого элемента
            bool r1 = InputListOps.TryReplace(items, 0, "B.STL", out hint);
            if (r1) throw new Exception("replace_duplicate_rejected: B.STL не должен пройти");
            if (string.IsNullOrEmpty(hint)) throw new Exception("replace_duplicate_rejected: hint пуст (1)");
            if (items[0] != "a.stl" || items[1] != "b.stl")
                throw new Exception("replace_duplicate_rejected: список изменился: " + items[0] + "," + items[1]);

            // Замена на тот же файл
            bool r2 = InputListOps.TryReplace(items, 0, "a.stl", out hint);
            if (r2) throw new Exception("replace_duplicate_rejected: замена на сам себя должна быть false");
            if (items[0] != "a.stl") throw new Exception("replace_duplicate_rejected: список изменился (2)");
        }

        // Отказ при некорректных аргументах
        private static void ReplaceBadArgs()
        {
            var items = new List<string> { "a.stl", "b.stl" };
            string hint;

            // Индекс вне диапазона
            bool r1 = InputListOps.TryReplace(items, 7, "x.stl", out hint);
            if (r1) throw new Exception("replace_bad_args: индекс 7 должен быть отклонён");
            if (items.Count != 2) throw new Exception("replace_bad_args: Count изменился: " + items.Count);

            // Пустой/whitespace путь
            bool r2 = InputListOps.TryReplace(items, 0, "  ", out hint);
            if (r2) throw new Exception("replace_bad_args: whitespace путь должен быть отклонён");
            if (items[0] != "a.stl") throw new Exception("replace_bad_args: список изменился (2)");

            // Null список
            bool r3 = InputListOps.TryReplace(null, 0, "x.stl", out hint);
            if (r3) throw new Exception("replace_bad_args: null items должен быть отклонён");
        }
    }
}
