using System.Collections.Generic;

namespace Cad2Gdml.Meshes
{
    // Разбиение сетки на связные компоненты
    public static class ComponentSplitter
    {
        public static List<List<int>> Components(Mesh welded)
        {
            int t = welded.Indices.Count / 3;
            if (t == 0)
            {
                return new List<List<int>>();
            }

            // Основная попытка: группировка по манифольдным рёбрам
            List<List<int>> edgeResult = GroupByEdge(welded, t);

            // Если все компоненты замкнуты — возвращаем как есть
            if (IsAllClosed(welded, edgeResult))
            {
                return edgeResult;
            }

            // Фолбэк: группировка по вершинам
            return GroupByVertex(welded, t);
        }

        // Группировка по манифольдным рёбрам (count == 2)
        private static List<List<int>> GroupByEdge(Mesh welded, int t)
        {
            long vcl = (long)welded.Vertices.Count;

            Dictionary<long, int> first = new Dictionary<long, int>();
            Dictionary<long, int> second = new Dictionary<long, int>();
            Dictionary<long, int> cnt = new Dictionary<long, int>();

            int[] parent = new int[t];
            int[] rank = new int[t];
            for (int i = 0; i < t; i++)
            {
                parent[i] = i;
            }

            for (int ti = 0; ti < t; ti++)
            {
                int a = welded.Indices[ti * 3];
                int b = welded.Indices[ti * 3 + 1];
                int c = welded.Indices[ti * 3 + 2];

                if (a == b || a == c || b == c)
                {
                    continue;
                }

                AddEdge(a, b, ti, vcl, first, second, cnt);
                AddEdge(b, c, ti, vcl, first, second, cnt);
                AddEdge(a, c, ti, vcl, first, second, cnt);
            }

            foreach (KeyValuePair<long, int> kvp in cnt)
            {
                if (kvp.Value == 2)
                {
                    Union(first[kvp.Key], second[kvp.Key], parent, rank);
                }
            }

            return CollectGroups(parent, t);
        }

        // Группировка по вершинам: объединяем треугольники, имеющие общую вершину
        private static List<List<int>> GroupByVertex(Mesh welded, int t)
        {
            int vCount = welded.Vertices.Count;
            int[] firstTriOfVertex = new int[vCount];
            for (int i = 0; i < vCount; i++)
            {
                firstTriOfVertex[i] = -1;
            }

            int[] parent = new int[t];
            int[] rank = new int[t];
            for (int i = 0; i < t; i++)
            {
                parent[i] = i;
            }

            for (int ti = 0; ti < t; ti++)
            {
                int a = welded.Indices[ti * 3];
                int b = welded.Indices[ti * 3 + 1];
                int c = welded.Indices[ti * 3 + 2];

                if (firstTriOfVertex[a] == -1) { firstTriOfVertex[a] = ti; }
                else { Union(ti, firstTriOfVertex[a], parent, rank); }

                if (firstTriOfVertex[b] == -1) { firstTriOfVertex[b] = ti; }
                else { Union(ti, firstTriOfVertex[b], parent, rank); }

                if (firstTriOfVertex[c] == -1) { firstTriOfVertex[c] = ti; }
                else { Union(ti, firstTriOfVertex[c], parent, rank); }
            }

            return CollectGroups(parent, t);
        }

        // Проверка: в каждой компоненте каждое ребро использовано ровно 2 раза
        private static bool IsAllClosed(Mesh welded, List<List<int>> components)
        {
            long vcl = (long)welded.Vertices.Count;

            for (int ci = 0; ci < components.Count; ci++)
            {
                List<int> comp = components[ci];

                // Вырожденный треугольник делает компоненту не замкнутой
                for (int i = 0; i < comp.Count; i++)
                {
                    int ti = comp[i];
                    int a = welded.Indices[ti * 3];
                    int b = welded.Indices[ti * 3 + 1];
                    int c = welded.Indices[ti * 3 + 2];
                    if (a == b || a == c || b == c)
                    {
                        return false;
                    }
                }

                // Подсчёт рёбер внутри компоненты
                Dictionary<long, int> edgeCount = new Dictionary<long, int>();

                for (int i = 0; i < comp.Count; i++)
                {
                    int ti = comp[i];
                    int a = welded.Indices[ti * 3];
                    int b = welded.Indices[ti * 3 + 1];
                    int c = welded.Indices[ti * 3 + 2];

                    Increment(edgeCount, MakeKey(a, b, vcl));
                    Increment(edgeCount, MakeKey(b, c, vcl));
                    Increment(edgeCount, MakeKey(a, c, vcl));
                }

                foreach (KeyValuePair<long, int> kvp in edgeCount)
                {
                    if (kvp.Value != 2)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        // Сборка отсортированных групп по корню
        private static List<List<int>> CollectGroups(int[] parent, int t)
        {
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

            List<List<int>> result = new List<List<int>>(groups.Values);
            for (int i = 0; i < result.Count; i++)
            {
                result[i].Sort();
            }
            result.Sort(delegate (List<int> x, List<int> y) { return x[0].CompareTo(y[0]); });

            return result;
        }

        // Регистрация ребра
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

        // Ключ ребра (unordered)
        private static long MakeKey(int v1, int v2, long vcl)
        {
            int lo = v1 < v2 ? v1 : v2;
            int hi = v1 < v2 ? v2 : v1;
            return (long)lo * vcl + hi;
        }

        // Инкремент в словаре
        private static void Increment(Dictionary<long, int> d, long key)
        {
            int v;
            if (!d.TryGetValue(key, out v))
            {
                d[key] = 1;
            }
            else
            {
                d[key] = v + 1;
            }
        }
    }
}
