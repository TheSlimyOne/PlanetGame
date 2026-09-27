using System;
using System.Linq;

namespace PlanetGame.Util.Geometry
{
    public struct IndexedEdge(int indexA, int indexB)
    {
        public readonly int IndexA = indexA;
        public readonly int IndexB = indexB;

        public readonly int[] GetPoints()
        {
            return [IndexA, IndexB];
        }

        public readonly int GetOtherPoint(int index)
        {
            int[] points = GetPoints();
            if (!points.Contains(index))
                throw new ArgumentException("Point is not part of the edge.");

            return points.First(p => p != index);
        }

        public override readonly bool Equals(object obj)
        {
            if (obj is Edge other)
                return (IndexA.Equals(other.PointA) && IndexB.Equals(other.PointB)) ||
                       (IndexA.Equals(other.PointB) && IndexB.Equals(other.PointA));
            return false;
        }

        public override readonly int GetHashCode()
        {
            int hash1 = IndexA.GetHashCode();
            int hash2 = IndexB.GetHashCode();
            return hash1 ^ hash2;
        }

        public override readonly string ToString()
        {
            return $"Edge({IndexA}, {IndexB})";
        }
    }
}