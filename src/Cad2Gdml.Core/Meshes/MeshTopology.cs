using System;
using System.Collections.Generic;
using System.Globalization;
using Cad2Gdml.Geometry;

namespace Cad2Gdml.Meshes
{
    // Отчёт о топологии: открытые, неманifold, неконсистентные рёбра
    public sealed class TopologyReport
    {
        public int OpenEdges { get; }
        public int NonManifoldEdges { get; }
        public int InconsistentEdges { get; }

        public bool IsClosedManifold
        {
            get { return OpenEdges == 0 && NonManifoldEdges == 0 && InconsistentEdges == 0; }
        }

        public TopologyReport(int openEdges, int nonManifoldEdges, int inconsistentEdges)
        {
            OpenEdges = openEdges;
            NonManifoldEdges = nonManifoldEdges;
            InconsistentEdges = inconsistentEdges;
        }
    }

    // Анализ топологии уже сваpленной (индексированной) сетки
    public static class MeshTopology
    {
        // Ключ неориентированного ребра: (меньший, больший)
        private struct EdgeKey : IEquatable<EdgeKey>
        {
            public int Lo;
            public int Hi;

            public bool Equals(EdgeKey other)
            {
                return Lo == other.Lo && Hi == other.Hi;
            }

            public override bool Equals(object obj)
            {
                if (obj is EdgeKey o)
                {
                    return Equals(o);
                }
                return false;
            }

            public override int GetHashCode()
            {
                return (Lo * 397) ^ Hi;
            }
        }

        // Счётчики направлений: Lo->Hi и Hi->Lo
        private struct EdgeData
        {
            public int Forward;
            public int Backward;
        }

        public static TopologyReport Analyze(Mesh welded)
        {
            Dictionary<EdgeKey, EdgeData> map = new Dictionary<EdgeKey, EdgeData>();
            int idxCount = welded.Indices.Count;

            for (int i = 0; i < idxCount; i += 3)
            {
                int i0 = welded.Indices[i];
                int i1 = welded.Indices[i + 1];
                int i2 = welded.Indices[i + 2];

                // Три направленных ребра треугольника
                AddDirected(map, i0, i1);
                AddDirected(map, i1, i2);
                AddDirected(map, i2, i0);
            }

            int open = 0;
            int nonManifold = 0;
            int inconsistent = 0;

            foreach (KeyValuePair<EdgeKey, EdgeData> kv in map)
            {
                int total = kv.Value.Forward + kv.Value.Backward;

                if (total == 1)
                {
                    open++;
                }
                else if (total > 2)
                {
                    nonManifold++;
                }
                else if (total == 2 && (kv.Value.Forward == 2 || kv.Value.Backward == 2))
                {
                    inconsistent++;
                }
            }

            return new TopologyReport(open, nonManifold, inconsistent);
        }

        // Учёт направленного ребра src->dst
        private static void AddDirected(Dictionary<EdgeKey, EdgeData> map, int src, int dst)
        {
            EdgeKey key;
            bool forward;

            if (src < dst)
            {
                key.Lo = src;
                key.Hi = dst;
                forward = true;
            }
            else
            {
                key.Lo = dst;
                key.Hi = src;
                forward = false;
            }

            EdgeData d;
            if (!map.TryGetValue(key, out d))
            {
                d.Forward = 0;
                d.Backward = 0;
            }

            if (forward)
            {
                d.Forward++;
            }
            else
            {
                d.Backward++;
            }

            map[key] = d;
        }
    }
}
