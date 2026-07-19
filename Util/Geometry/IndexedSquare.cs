using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
namespace PlanetGame.Util.Geometry
{
    public struct IndexedSquare(int indexA, int indexB, int indexC, int indexD)
    {
        public readonly int IndexA = indexA;
        public readonly int IndexB = indexB;
        public readonly int IndexC = indexC;
        public readonly int IndexD = indexD;

        public int[] GetPoints()
        {
            return [IndexA, IndexB, IndexC, IndexD];
        }

        public int[] GetOtherPoints(IndexedEdge edge)
        {
            IndexedEdge[] edges = GetEdges();
            if (!edges.Contains(edge))
                throw new ArgumentException("Edge is not part of the square.");

            return [.. GetPoints().Where(p => p != edge.IndexA && p != edge.IndexB)];
        }

        public IndexedEdge[] GetEdges()
        {
            return [
                new(IndexA, IndexB),
                new(IndexB, IndexC),
                new(IndexC, IndexD),
                new(IndexD, IndexA)
            ];
        }

        public override bool Equals(object obj)
        {
            if (obj is IndexedSquare other)
            {
                HashSet<int> points = [.. GetPoints()];
                HashSet<int> otherPoints = [.. other.GetPoints()];
                return points.SetEquals(otherPoints);
            }
            return false;
        }

        public override int GetHashCode()
        {
            int hash1 = IndexA.GetHashCode();
            int hash2 = IndexB.GetHashCode();
            int hash3 = IndexC.GetHashCode();
            int hash4 = IndexD.GetHashCode();
            return hash1 ^ hash2 ^ hash3 ^ hash4;
        }

        public override string ToString()
        {
            return $"Square({IndexA}, {IndexB}, {IndexC}, {IndexD})";
        }

        public IndexedEdge[] GetOtherEdges(IndexedEdge edge)
        {
            return [.. GetEdges().Where(e => !e.Equals(edge))];
        }
    }
}