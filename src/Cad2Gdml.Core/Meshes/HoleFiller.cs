using System;
using System.Collections.Generic;
using System.Globalization;
using Cad2Gdml.Geometry;

namespace Cad2Gdml.Meshes
{
    // Заполнение отверстий (boundary loops) в сваренной сетке
    public static class HoleFiller
    {
        // Неориентированный ключ ребра: lo < hi
        private static long UndirKey(int a, int b, long vc)
        {
            long lo, hi;
            if (a <= b) { lo = a; hi = b; }
            else { lo = b; hi = a; }
            return lo * vc + hi;
        }

        // Увеличить счётчик ребра
        private static void IncEdge(Dictionary<long, int> ec, int a, int b, long vc)
        {
            long k = UndirKey(a, b, vc);
            int v;
            ec.TryGetValue(k, out v);
            ec[k] = v + 1;
        }

        public static Mesh Fill(Mesh welded, int maxLoopEdges, out int filled, out int skipped)
        {
            filled = 0;
            skipped = 0;

            if (welded == null || welded.Indices.Count < 3)
                return null;

            int triCount = welded.Indices.Count / 3;
            long vc = (long)welded.Vertices.Count;
            int cap = triCount * 3 / 2 + 1;

            // Подсчёт неориентированных рёбер
            Dictionary<long, int> ec = new Dictionary<long, int>(cap);
            for (int t = 0; t < triCount; t++)
            {
                int i0 = welded.Indices[t * 3];
                int i1 = welded.Indices[t * 3 + 1];
                int i2 = welded.Indices[t * 3 + 2];
                IncEdge(ec, i0, i1, vc);
                IncEdge(ec, i1, i2, vc);
                IncEdge(ec, i2, i0, vc);
            }

            // Граничные рёбра (count == 1), направление как в треугольнике
            List<int> bA = new List<int>();
            List<int> bB = new List<int>();
            for (int t = 0; t < triCount; t++)
            {
                int i0 = welded.Indices[t * 3];
                int i1 = welded.Indices[t * 3 + 1];
                int i2 = welded.Indices[t * 3 + 2];
                if (ec[UndirKey(i0, i1, vc)] == 1) { bA.Add(i0); bB.Add(i1); }
                if (ec[UndirKey(i1, i2, vc)] == 1) { bA.Add(i1); bB.Add(i2); }
                if (ec[UndirKey(i2, i0, vc)] == 1) { bA.Add(i2); bB.Add(i0); }
            }

            int nE = bA.Count;
            if (nE == 0) return null;

            // Сортировка рёбер по (a, b) — детерминированный порядок
            int[] ord = new int[nE];
            for (int i = 0; i < nE; i++) ord[i] = i;
            Array.Sort(ord, delegate(int x, int y)
            {
                int c = bA[x].CompareTo(bA[y]);
                if (c != 0) return c;
                return bB[x].CompareTo(bB[y]);
            });

            // Соседство: стартовая вершина -> рёбра (уже отсортированы по b)
            Dictionary<int, List<int>> adj = new Dictionary<int, List<int>>(nE);
            for (int oi = 0; oi < nE; oi++)
            {
                int ei = ord[oi];
                List<int> lst;
                if (!adj.TryGetValue(bA[ei], out lst))
                {
                    lst = new List<int>();
                    adj[bA[ei]] = lst;
                }
                lst.Add(ei);
            }

            // Поиск циклов среди граничных рёбер
            bool[] used = new bool[nE];
            List<List<int>> rawLoops = new List<List<int>>();

            for (int oi = 0; oi < nE; oi++)
            {
                int ei = ord[oi];
                if (used[ei]) continue;

                int startV = bA[ei];
                int curV = bB[ei];
                used[ei] = true;

                List<int> verts = new List<int>();
                verts.Add(startV);
                verts.Add(curV);
                int eUsed = 1;
                bool closed = false;
                bool valid = true;
                int repeatPos = -1;

                while (true)
                {
                    if (eUsed >= maxLoopEdges)
                    {
                        valid = false;
                        break;
                    }

                    List<int> outs;
                    if (!adj.TryGetValue(curV, out outs))
                    {
                        valid = false;
                        break;
                    }

                    // Первое неиспользованное ребро (наименьшая целевая вершина)
                    int next = -1;
                    for (int j = 0; j < outs.Count; j++)
                    {
                        if (!used[outs[j]]) { next = outs[j]; break; }
                    }

                    if (next < 0)
                    {
                        valid = false;
                        break;
                    }

                    int nv = bB[next];
                    used[next] = true;
                    eUsed++;

                    if (nv == startV)
                    {
                        closed = true;
                        break;
                    }

                    // Повторная вершина — разбиваем
                    int dup = -1;
                    for (int k = 1; k < verts.Count; k++)
                    {
                        if (verts[k] == nv) { dup = k; break; }
                    }
                    if (dup >= 0)
                    {
                        repeatPos = dup;
                        closed = true;
                        break;
                    }

                    verts.Add(nv);
                    curV = nv;
                }

                if (!closed || !valid)
                {
                    // Не удалось замкнуть — пропускаем
                    continue;
                }

                if (repeatPos < 0)
                {
                    // Обычный замкнутый контур
                    rawLoops.Add(verts);
                }
                else
                {
                    // Разбиение на два подконтoura в точке повтора
                    int len1 = repeatPos + 1;
                    int len2 = verts.Count - repeatPos;

                    if (len1 >= 3)
                    {
                        List<int> l1 = new List<int>(len1);
                        for (int i = 0; i <= repeatPos; i++) l1.Add(verts[i]);
                        rawLoops.Add(l1);
                    }
                    if (len2 >= 3)
                    {
                        List<int> l2 = new List<int>(len2);
                        for (int i = repeatPos; i < verts.Count; i++) l2.Add(verts[i]);
                        rawLoops.Add(l2);
                    }
                }
            }

            // Проверка выпуклости и формирование валидных крышек
            List<List<int>> caps = new List<List<int>>();
            int filledCnt = 0;
            int skippedCnt = 0;

            for (int ci = 0; ci < rawLoops.Count; ci++)
            {
                List<int> loop = rawLoops[ci];
                int k = loop.Count;

                if (k < 3 || k > maxLoopEdges)
                {
                    skippedCnt++;
                    continue;
                }

                if (k > 3)
                {
                    // Нормаль Ньюэлла
                    Vec3d N = new Vec3d(0.0, 0.0, 0.0);
                    for (int i = 0; i < k; i++)
                    {
                        Vec3d vi = welded.Vertices[loop[i]];
                        Vec3d vj = welded.Vertices[loop[(i + 1) % k]];
                        N = N + vi.Cross(vj);
                    }
                    Vec3d negN = -N;

                    // Центроид
                    double cx = 0.0, cy = 0.0, cz = 0.0;
                    for (int i = 0; i < k; i++)
                    {
                        Vec3d v = welded.Vertices[loop[i]];
                        cx += v.X;
                        cy += v.Y;
                        cz += v.Z;
                    }
                    cx /= k;
                    cy /= k;
                    cz /= k;
                    Vec3d centroid = new Vec3d(cx, cy, cz);

                    // Проверка каждого треугольника вентилятора
                    bool ok = true;
                    for (int i = 0; i < k && ok; i++)
                    {
                        Vec3d va = welded.Vertices[loop[(i + 1) % k]];
                        Vec3d vb = welded.Vertices[loop[i]];
                        Vec3d e1 = va - centroid;
                        Vec3d e2 = vb - centroid;
                        Vec3d triN = e1.Cross(e2);
                        if (triN.LengthSq() <= 1e-30) { ok = false; break; }
                        if (triN.Dot(negN) <= 0.0) { ok = false; break; }
                    }

                    if (!ok)
                    {
                        skippedCnt++;
                        continue;
                    }
                }

                caps.Add(loop);
                filledCnt++;
            }

            filled = filledCnt;
            skipped = skippedCnt;

            if (caps.Count == 0) return null;

            // Построение результирующей сетки (вход не изменяется)
            Mesh result = new Mesh();
            result.Vertices.AddRange(welded.Vertices);
            result.Indices.AddRange(welded.Indices);

            // Добавление крышек
            for (int ci = 0; ci < caps.Count; ci++)
            {
                List<int> loop = caps[ci];
                int k = loop.Count;

                if (k == 3)
                {
                    // Треугольник с развёрнутым ходом
                    result.Indices.Add(loop[0]);
                    result.Indices.Add(loop[2]);
                    result.Indices.Add(loop[1]);
                }
                else
                {
                    // Центроид
                    double cx = 0.0, cy = 0.0, cz = 0.0;
                    for (int i = 0; i < k; i++)
                    {
                        Vec3d v = welded.Vertices[loop[i]];
                        cx += v.X;
                        cy += v.Y;
                        cz += v.Z;
                    }
                    cx /= k;
                    cy /= k;
                    cz /= k;

                    int cIdx = result.Vertices.Count;
                    result.Vertices.Add(new Vec3d(cx, cy, cz));

                    // Вентилятор: (c, v(i+1), v(i))
                    for (int i = 0; i < k; i++)
                    {
                        result.Indices.Add(cIdx);
                        result.Indices.Add(loop[(i + 1) % k]);
                        result.Indices.Add(loop[i]);
                    }
                }
            }

            return result;
        }
    }
}
