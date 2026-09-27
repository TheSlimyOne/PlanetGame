using Godot;
using System;
using System.Linq;

namespace PlanetGame.Util.Geometry
{
    public struct Edge
    {
        public readonly Vector3 PointA;
        public readonly Vector3 PointB;

        public Edge(Vector3 pointA, Vector3 pointB)
        {
            PointA = pointA;
            PointB = pointB;
        }

        public readonly Vector3[] GetPoints()
        {
            return [PointA, PointB];
        }

        public readonly Vector3 GetOtherPoint(Vector3 point)
        {
            Vector3[] points = GetPoints();
            if (!points.Contains(point))
                throw new ArgumentException("Point is not part of the edge.");

            return points.First(p => p != point);
        }

        public override readonly bool Equals(object obj)
        {
            if (obj is Edge other)
                return (PointA.Equals(other.PointA) && PointB.Equals(other.PointB)) ||
                       (PointA.Equals(other.PointB) && PointB.Equals(other.PointA));
            return false;
        }

        public override readonly int GetHashCode()
        {
            int hash1 = PointA.GetHashCode();
            int hash2 = PointB.GetHashCode();
            return hash1 ^ hash2;
        }

        public override readonly string ToString()
        {
            return $"Edge({PointA}, {PointB})";
        }
    }
}