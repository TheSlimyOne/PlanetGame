using System.Collections.Generic;
namespace PlanetGame.Util.Geometry
{
    public struct IndexedTriangle(int pointA, int pointB, int pointC)
    {
        public readonly int IndexA = pointA;
        public readonly int IndexB = pointB;
        public readonly int IndexC = pointC;

        public readonly int[] GetPoints()
        {
            return [IndexA, IndexB, IndexC];
        }

        public readonly IndexedEdge[] GetEdges()
        {
            return [
                new(IndexA, IndexB),
                new(IndexB, IndexC),
                new(IndexC, IndexA)
            ];
        }

        public override readonly bool Equals(object obj)
        {
            if (obj is IndexedTriangle other)
            {
                HashSet<int> points = [.. GetPoints()];
                HashSet<int> otherPoints = [.. other.GetPoints()];
                return points.SetEquals(otherPoints);
            }
            return false;
        }

        public override readonly int GetHashCode()
        {
            int hash1 = IndexA.GetHashCode();
            int hash2 = IndexB.GetHashCode();
            int hash3 = IndexC.GetHashCode();
            return hash1 ^ hash2 ^ hash3;
        }

        public override readonly string ToString()
        {
            return $"triangle({IndexA}, {IndexB}, {IndexC})";
        }
    }
}