using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

namespace PlanetGame.Util.Orometry
{
    public class Manifold
    {
        private class ManifoldNode(Simplex simplex, Simplex startingSimplex = null)
        {
            public Simplex Simplex { get; set; } = simplex;
            public List<ManifoldNode> Paths { get; set; } = [];
            public Simplex StartingSimplex { get; set; } = startingSimplex;

            public List<List<Simplex>> Traverse()
            {
                List<List<Simplex>> result = [];
                List<Simplex> currentPath = [];

                void DFS(ManifoldNode node)
                {
                    if (node.Simplex != null)
                        currentPath.Add(node.Simplex);
                    if (node.Paths == null || node.Paths.Count == 0)
                        result.Add([.. currentPath]);
                    else
                        foreach (ManifoldNode child in node.Paths)
                            DFS(child);

                    if (node.Simplex != null)
                        currentPath.RemoveAt(currentPath.Count - 1);
                }

                DFS(this);
                return result;
            }

            public void RemoveChild(Simplex simplex)
            {
                Paths = [.. Paths.Where(n => n.Simplex != simplex)];
            }

            public override string ToString()
            {
                StringBuilder builder = new();
                BuildString(this, builder, 0);
                return builder.ToString();
            }

            private static void BuildString(ManifoldNode node, StringBuilder builder, int indentLevel)
            {
                string indent = new(' ', indentLevel * 2);
                builder.Append($"{indent} -> {node.Simplex}\n");

                if (node.Paths != null)
                {
                    foreach (ManifoldNode child in node.Paths)
                    {
                        BuildString(child, builder, indentLevel + 1);
                    }
                }
            }

            public override bool Equals(object obj)
            {
                if (obj is ManifoldNode other)
                    return Simplex == other.Simplex;
                return false;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Simplex);
            }
        }

        private class JunctionNode(Simplex simplex) : ManifoldNode(simplex)
        {
            public readonly SortedSet<ManifoldNode> EndPoints = new(Comparer<ManifoldNode>.Create((a, b) => a.Simplex.GetUpperValue().CompareTo(b.Simplex.GetUpperValue())));

            public List<List<Simplex>> TraverseFromJunction()
            {
                return Traverse();
            }

            public void AddEndPoints(ManifoldNode node)
            {
                EndPoints.Add(node);
            }
        }

        private readonly Dictionary<Simplex, JunctionNode> _junctionLookup = [];
        private readonly Dictionary<Simplex, List<JunctionNode>> _connectedJunctions = [];
        private readonly Dictionary<(Simplex, Simplex), ManifoldNode> _simplexToManifoldNode = [];
        public const int LENGTH_CUTOFF = 200;

        public void CreateJunction(Simplex origin, Simplex start, Func<Simplex, bool> isCritical, Func<Simplex, Simplex> getNext)
        {
            if (!_junctionLookup.TryGetValue(origin, out JunctionNode junction))
            {
                junction = new(origin);
                _junctionLookup[origin] = junction;
            }

            ManifoldNode currentNode = junction;
            Simplex nextSimplex = start;

            int counter = 0;
            bool isTooLong = false;
            while (nextSimplex != null)
            {
                counter++;
                ManifoldNode child = new(nextSimplex, start);
                _simplexToManifoldNode.TryAdd((nextSimplex, origin), child);

                currentNode.Paths.Add(child);
                currentNode = child;
                nextSimplex = getNext(nextSimplex);

                if (counter > LENGTH_CUTOFF)
                {
                    GD.PrintRaw(origin + "\n");
                    isTooLong = true;
                    break;
                }
            }

            if (!isCritical(currentNode.Simplex) || isTooLong)
            {
                junction.RemoveChild(start);
                return;
            }
            junction.AddEndPoints(currentNode);

            if (!_connectedJunctions.TryGetValue(currentNode.Simplex, out List<JunctionNode> junctions))
            {
                junctions = [];
                _connectedJunctions[currentNode.Simplex] = junctions;
            }
            junctions.Add(junction);

        }

        public List<List<Simplex>> GetPaths()
        {
            List<List<Simplex>> paths = [];

            foreach (JunctionNode root in _junctionLookup.Values)
            {
                List<List<Simplex>> rootPaths = root.TraverseFromJunction();
                if (rootPaths.Count > 0)
                    paths.AddRange(rootPaths);
            }

            return paths;
        }

        public override string ToString()
        {
            StringBuilder builder = new();
            builder.Append("Manifold: \n");

            foreach (ManifoldNode root in _junctionLookup.Values)
            {
                builder.Append(root.ToString() + "\n");
            }

            return builder.ToString();
        }

        public int Count()
        {
            List<List<Simplex>> paths = GetPaths();
            int totalNodeCount = 0;

            foreach (List<Simplex> path in paths)
            {
                totalNodeCount += path.Count;
            }

            return totalNodeCount;
        }

        public void Simplify(float persistenceThreshold = 0.5f)
        {
            HashSet<(Simplex, float, JunctionNode)> persistencePairs = [];
            float maxPersistence = float.MinValue;

            foreach ((Simplex simplex, List<JunctionNode> junctions) in _connectedJunctions.Where(x => x.Key is not Edge))
            {
                foreach (JunctionNode junction in junctions)
                {
                    float persistence = Mathf.Abs(junction.Simplex.GetAverageValue() - simplex.GetAverageValue());
                    persistencePairs.Add((simplex, persistence, junction));
                    maxPersistence = persistence > maxPersistence ? persistence : maxPersistence;
                }
            }

            foreach ((Simplex simplex, float persistence, JunctionNode junction) in persistencePairs.OrderBy(x => x.Item2))
            {
                float normalizePersistence = persistence / maxPersistence;
                GD.PrintRaw(normalizePersistence + "\n");
                if (normalizePersistence < persistenceThreshold)
                {
                    Simplex startingSimplex = _simplexToManifoldNode[(simplex, junction.Simplex)].StartingSimplex;
                    junction.RemoveChild(startingSimplex);

                    if (junction.Paths.Count == 0)
                    {
                        _junctionLookup.Remove(junction.Simplex);
                    }
                }
            }
        }
    }
}