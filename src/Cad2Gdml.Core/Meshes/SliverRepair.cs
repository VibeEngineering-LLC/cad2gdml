using System;
using System.Collections.Generic;
using Cad2Gdml.Geometry;

namespace Cad2Gdml.Meshes
{
    // Удаление щелевидных треугольников без образования дыр
    public static class SliverRepair
    {
        public static Mesh Fix(Mesh src, out int fixedCount)
        {
            fixedCount = 0;
            if (src == null || src.Indices.Count < 3) return src;

            // Копия индексов в виде списка троек
            List<int[]> tris = new List<int[]>(src.Indices.Count / 3);
            for (int i = 0; i < src.Indices.Count; i += 3)
                tris.Add(new int[] { src.Indices[i], src.Indices[i + 1], src.Indices[i + 2] });

            long bigV = (long)src.Vertices.Count + 1;

            for (int pass = 0; pass < 20; pass++)
            {
                int n = tris.Count;
                if (n == 0) break;

                // Определяем, какие треугольники — сливеры
                bool[] isSliver = new bool[n];
                bool[] hasDup = new bool[n];

                for (int i = 0; i < n; i++)
                {
                    int[] t = tris[i];
                    if (t[0] == t[1] || t[1] == t[2] || t[0] == t[2])
                    { isSliver[i] = true; hasDup[i] = true; continue; }

                    Vec3d P = src.Vertices[t[0]];
                    Vec3d Q = src.Vertices[t[1]];
                    Vec3d R = src.Vertices[t[2]];
                    Vec3d cross = (Q - P).Cross(R - P);
                    double area = cross.Length() * 0.5;

                    if (area < 1e-9) { isSliver[i] = true; continue; }

                    double l01 = (P - Q).Length();
                    double l12 = (Q - R).Length();
                    double l20 = (R - P).Length();
                    double longest = Math.Max(l01, Math.Max(l12, l20));
                    if (longest > 0.0)
                    {
                        double h = 2.0 * area / longest;
                        if (h / longest < 1e-7) isSliver[i] = true;
                    }
                }

                // Словарь направленных рёбер -> список треугольников
                Dictionary<long, List<int>> edgeMap = new Dictionary<long, List<int>>();
                for (int i = 0; i < n; i++)
                {
                    int[] t = tris[i];
                    AddEdge(edgeMap, (long)t[0] * bigV + t[1], i);
                    AddEdge(edgeMap, (long)t[1] * bigV + t[2], i);
                    AddEdge(edgeMap, (long)t[2] * bigV + t[0], i);
                }

                HashSet<int> removed = new HashSet<int>();
                List<int[]> newTris = new List<int[]>(n + 4);
                List<int[]> repl = new List<int[]>();
                bool changed = false;

                for (int i = 0; i < n; i++)
                {
                    if (removed.Contains(i)) continue;

                    if (!isSliver[i])
                    {
                        continue;
                        continue;
                    }

                    // Дубликаты индексов — просто удаляем
                    if (hasDup[i])
                    {
                        removed.Add(i);
                        fixedCount++;
                        changed = true;
                        continue;
                    }

                    // Коллинеарный сливер: находим A, C (концы длиннейшей стороны) и M
                    int[] t = tris[i];
                    double l01 = (src.Vertices[t[0]] - src.Vertices[t[1]]).Length();
                    double l12 = (src.Vertices[t[1]] - src.Vertices[t[2]]).Length();
                    double l20 = (src.Vertices[t[2]] - src.Vertices[t[0]]).Length();

                    int A, C, M;
                    if (l01 >= l12 && l01 >= l20) { A = t[0]; C = t[1]; M = t[2]; }
                    else if (l12 >= l01 && l12 >= l20) { A = t[1]; C = t[2]; M = t[0]; }
                    else { A = t[2]; C = t[0]; M = t[1]; }

                    // Проверка: проекция M на AC строго внутри
                    Vec3d VA = src.Vertices[A], VC = src.Vertices[C], VM = src.Vertices[M];
                    Vec3d AC = VC - VA;
                    double acSq = AC.LengthSq();
                    double tParam = (VM - VA).Dot(AC) / acSq;
                    if (tParam <= 0.0 || tParam >= 1.0)
                    {
                        continue;
                        continue;
                    }

                    // Поиск соседа с ребром {A,C} (предпочитаем не-сливер)
                    int neighbor = -1, u = 0, v = 0;
                    List<int> cand;

                    if (edgeMap.TryGetValue((long)A * bigV + C, out cand))
                    {
                        for (int j = 0; j < cand.Count; j++)
                        {
                            int c = cand[j];
                            if (c != i && !removed.Contains(c) && !isSliver[c]) { neighbor = c; u = A; v = C; break; }
                        }
                    }
                    if (neighbor < 0 && edgeMap.TryGetValue((long)C * bigV + A, out cand))
                    {
                        for (int j = 0; j < cand.Count; j++)
                        {
                            int c = cand[j];
                            if (c != i && !removed.Contains(c) && !isSliver[c]) { neighbor = c; u = C; v = A; break; }
                        }
                    }
                    // Если не-сливер не нашёлся — любой
                    if (neighbor < 0 && edgeMap.TryGetValue((long)A * bigV + C, out cand))
                    {
                        for (int j = 0; j < cand.Count; j++)
                        {
                            int c = cand[j];
                            if (c != i && !removed.Contains(c)) { neighbor = c; u = A; v = C; break; }
                        }
                    }
                    if (neighbor < 0 && edgeMap.TryGetValue((long)C * bigV + A, out cand))
                    {
                        for (int j = 0; j < cand.Count; j++)
                        {
                            int c = cand[j];
                            if (c != i && !removed.Contains(c)) { neighbor = c; u = C; v = A; break; }
                        }
                    }

                    if (neighbor < 0)
                    {
                        // Нет соседа — оставляем сливер
                        continue;
                        continue;
                    }

                    // Третья вершина соседа X
                    int[] nt = tris[neighbor];
                    int X = -1;
                    if (nt[0] == u && nt[1] == v) X = nt[2];
                    else if (nt[1] == u && nt[2] == v) X = nt[0];
                    else if (nt[2] == u && nt[0] == v) X = nt[1];
                    else if (nt[0] == v && nt[1] == u) X = nt[2];
                    else if (nt[1] == v && nt[2] == u) X = nt[0];
                    else if (nt[2] == v && nt[0] == u) X = nt[1];

                    if (X < 0)
                    {
                        continue;
                        continue;
                    }

                    // Замена: сосед -> (u,M,X) + (M,v,X), сливер удаляется
                    repl.Add(new int[] { u, M, X });
                    repl.Add(new int[] { M, v, X });
                    removed.Add(neighbor);
                    removed.Add(i);
                    fixedCount++;
                    changed = true;
                }

                if (!changed) break;
                for (int i = 0; i < n; i++) if (!removed.Contains(i)) newTris.Add(tris[i]);
                newTris.AddRange(repl);
                tris = newTris;
            }

            if (fixedCount == 0) return src;

            // Сборка результата
            Mesh result = new Mesh();
            result.Vertices.AddRange(src.Vertices);
            for (int i = 0; i < tris.Count; i++)
            {
                result.Indices.Add(tris[i][0]);
                result.Indices.Add(tris[i][1]);
                result.Indices.Add(tris[i][2]);
            }
            return result;
        }

        // Добавление треугольника в словарь рёбер
        private static void AddEdge(Dictionary<long, List<int>> map, long key, int tri)
        {
            List<int> lst;
            if (!map.TryGetValue(key, out lst))
            {
                lst = new List<int>();
                map[key] = lst;
            }
            lst.Add(tri);
        }
    }
}
