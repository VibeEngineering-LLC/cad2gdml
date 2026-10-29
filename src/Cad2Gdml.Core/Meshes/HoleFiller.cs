using System;
using System.Collections.Generic;
using System.Globalization;
using Cad2Gdml.Geometry;

namespace Cad2Gdml.Meshes
{
    // Заполнение отверстий (boundary loops) в сваренной сетке
    public static class HoleFiller
    {
        // Ключ неориентированного ребра
        private static long EKey(int a, int b)
        {
            if (a > b) { int t = a; a = b; b = t; }
            return ((long)a << 32) | (uint)b;
        }

        public static Mesh Fill(Mesh welded, int maxLoopEdges, out int filled, out int skipped)
        {
            filled = 0;
            skipped = 0;

            if (welded == null || welded.Indices.Count < 3)
                return null;

            int triCount = welded.Indices.Count / 3;

            // Подсчёт неориентированных рёбер
            Dictionary<long, int> ec = new Dictionary<long, int>();
            for (int t = 0; t < triCount; t++)
            {
                int i0 = welded.Indices[t * 3];
                int i1 = welded.Indices[t * 3 + 1];
                int i2 = welded.Indices[t * 3 + 2];
                long k0 = EKey(i0, i1);
                long k1 = EKey(i1, i2);
                long k2 = EKey(i2, i0);
                int v0, v1, v2;
                ec.TryGetValue(k0, out v0); ec[k0] = v0 + 1;
                ec.TryGetValue(k1, out v1); ec[k1] = v1 + 1;
                ec.TryGetValue(k2, out v2); ec[k2] = v2 + 1;
            }

            // Граничные рёбра (count == 1), направление как в треугольнике
            List<int> bA = new List<int>();
            List<int> bB = new List<int>();
            for (int t = 0; t < triCount; t++)
            {
                int i0 = welded.Indices[t * 3];
                int i1 = welded.Indices[t * 3 + 1];
                int i2 = welded.Indices[t * 3 + 2];
                if (ec[EKey(i0, i1)] == 1) { bA.Add(i0); bB.Add(i1); }
                if (ec[EKey(i1, i2)] == 1) { bA.Add(i1); bB.Add(i2); }
                if (ec[EKey(i2, i0)] == 1) { bA.Add(i2); bB.Add(i0); }
            }

            int nE = bA.Count;
            if (nE == 0) return null;

            // Сортировка рёбер по (a, b)
            int[] ord = new int[nE];
            for (int i = 0; i < nE; i++) ord[i] = i;
            Array.Sort(ord, delegate(int i, int j)
            {
                int c = bA[i].CompareTo(bA[j]);
                if (c != 0) return c;
                return bB[i].CompareTo(bB[j]);
            });

            // Соседство: стартовая вершина -> рёбра
            Dictionary<int, List<int>> adj = new Dictionary<int, List<int>>();
            for (int i = 0; i < nE; i++)
            {
                List<int> lst;
                if (!adj.TryGetValue(bA[i], out lst))
                {
                    lst = new List<int>();
                    adj[bA[i]] = lst;
                }
                lst.Add(i);
            }

            // Сортировка списков по целевой вершине
            HashSet<int> svSet = new HashSet<int>();
            for (int i = 0; i < nE; i++) svSet.Add(bA[i]);
            List<int> svList = new List<int>(svSet);
            svList.Sort();
            for (int si = 0; si < svList.Count; si++)
            {
                int sv = svList[si];
                adj[sv].Sort(delegate(int x, int y) { return bB[x].CompareTo(bB[y]); });
            }

            // Поиск циклов
            bool[] used = new bool[nE];
            List<List<int>> caps = new List<List<int>>();

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

                    bool dup = false;
                    for (int k = 0; k < verts.Count; k++)
                    {
                        if (verts[k] == nv) { dup = true; break; }
                    }
                    if (dup)
                    {
                        valid = false;
                        break;
                    }

                    verts.Add(nv);
                    curV = nv;
                }

                if (closed && valid && verts.Count >= 3 && verts.Count <= maxLoopEdges)
                {
                    caps.Add(verts);
                    filled++;
                }
                else
                {
                    skipped++;
                }
            }

            if (caps.Count == 0) return null;

            // Построение результирующей сетки
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
