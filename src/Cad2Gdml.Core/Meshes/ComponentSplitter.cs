using System.Collections.Generic;

namespace Cad2Gdml.Meshes
{
    // Разбиение сетки на связные компоненты по манифольдным рёбрам
    public static class ComponentSplitter
    {
        public static List<List<int>> Components(Mesh welded)
        {
            int t = welded.Indices.Count / 3;
            if (t == 0)
            {
                return new List<List<int>>();
            }

            long vcl = (long)welded.Vertices.Count;

            // Словари рёбер: ключ -> первый/второй треугольник и счётчик
            Dictionary<long, int> first = new Dictionary<long, int>();
            Dictionary<long, int> second = new Dictionary<long, int>();
            Dictionary<long, int> cnt = new Dictionary<long, int>();

            // Union-find
            int[] parent = new int[t];
            int[] rank = new int[t];
            for (int i = 0; i < t; i++)
            {
                parent[i] = i;
            }

            // Сбор рёбер по всем треугольникам
            for (int ti = 0; ti < t; ti++)
            {
                int a = welded.Indices[ti * 3];
                int b = welded.Indices[ti * 3 + 1];
                int c = welded.Indices[ti * 3 + 2];

                // Вырожденный треугольник — без рёбер
                if (a == b || a == c || b == c)
                {
                    continue;
                }

                AddEdge(a, b, ti, vcl, first, second, cnt);
                AddEdge(b, c, ti, vcl, first, second, cnt);
                AddEdge(a, c, ti, vcl, first, second, cnt);
            }

            // Union только для манифольдных рёбер (count == 2)
            foreach (KeyValuePair<long, int> kvp in cnt)
            {
                if (kvp.Value == 2)
                {
                    Union(first[kvp.Key], second[kvp.Key], parent, rank);
                }
            }

            // Группировка по корню
            Dictionary<int, List<int>> groups = new Dictionary<int, List<int>>();
            for (int ti = 0; ti < t; ti++)
            {
                int root = Find(ti, parent);
                List<int> list;
                if (!groups.TryGetValue(root, out list))
                {
                    list = new List<int>();
                    groups[root] = list;
                }
                list.Add(ti);
            }

            // Сортировка: внутри — по возрастанию, компоненты — по мин. индексу
            List<List<int>> result = new List<List<int>>(groups.Values);
            for (int i = 0; i < result.Count; i++)
            {
                result[i].Sort();
            }
            result.Sort(delegate (List<int> x, List<int> y) { return x[0].CompareTo(y[0]); });

            return result;
        }

        // Регистрация ребра в словарях
        private static void AddEdge(int v1, int v2, int ti, long vcl,
            Dictionary<long, int> first, Dictionary<long, int> second, Dictionary<long, int> cnt)
        {
            int lo = v1 < v2 ? v1 : v2;
            int hi = v1 < v2 ? v2 : v1;
            long key = (long)lo * vcl + hi;

            int c;
            if (!cnt.TryGetValue(key, out c))
            {
                cnt[key] = 1;
                first[key] = ti;
            }
            else if (c == 1)
            {
                cnt[key] = 2;
                second[key] = ti;
            }
            else
            {
                cnt[key] = c + 1;
            }
        }

        // Итеративный Find с компрессией пути
        private static int Find(int x, int[] parent)
        {
            int root = x;
            while (parent[root] != root)
            {
                root = parent[root];
            }
            while (x != root)
            {
                int next = parent[x];
                parent[x] = root;
                x = next;
            }
            return root;
        }

        // Union по рангу
        private static void Union(int a, int b, int[] parent, int[] rank)
        {
            int ra = Find(a, parent);
            int rb = Find(b, parent);
            if (ra == rb)
            {
                return;
            }
            if (rank[ra] < rank[rb])
            {
                parent[ra] = rb;
            }
            else if (rank[ra] > rank[rb])
            {
                parent[rb] = ra;
            }
            else
            {
                parent[rb] = ra;
                rank[ra]++;
            }
        }
    }
}
