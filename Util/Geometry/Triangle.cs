using System.Collections.Generic;
using System.Linq;
using Godot;
namespace PlanetGame.Util.Geometry
{
    public struct Triangle
    {
        public readonly Vector3 PointA;
        public readonly Vector3 PointB;
        public readonly Vector3 PointC;

        public Triangle(Vector3 pointA, Vector3 pointB, Vector3 pointC)
        {

            float area = (pointB.X - pointA.X) * (pointC.Y - pointA.Y) - (pointB.Y - pointA.Y) * (pointC.X - pointA.X);
            if (area < 0)
                (pointB, pointC) = (pointC, pointB);

            PointA = pointA;
            PointB = pointB;
            PointC = pointC;
        }

        public Vector3[] GetPoints()
        {
            return  [PointA, PointB, PointC];
        }

        public Edge[] GetEdges()
        {
            return [
                new(PointA, PointB),
                new(PointB, PointC),
                new(PointC, PointA)
            ];
        }

        public override bool Equals(object obj)
        {
            if (obj is Triangle other)
            {
                HashSet<Vector3> points = [.. GetPoints()];
                HashSet<Vector3> otherPoints = [.. other.GetPoints()];
                return points.SetEquals(otherPoints);
            }
            return false;
        }

        public override int GetHashCode()
        {
            int hash1 = PointA.GetHashCode();
            int hash2 = PointB.GetHashCode();
            int hash3 = PointC.GetHashCode();
            return hash1 ^ hash2 ^ hash3;
        }

        public override string ToString()
        {
            return $"triangle({PointA}, {PointB}, {PointC})";
        }

        public Vector3 GetCentroid()
        {
            return (PointA + PointB + PointC) / 3;
        }

        public static bool operator <(Triangle a, Triangle b)
        {
            return a.GetCentroid() < b.GetCentroid();
        }

        public static bool operator >(Triangle a, Triangle b)
        {
            return a.GetCentroid() > b.GetCentroid();
        }

        public static bool operator <=(Triangle a, Triangle b)
        {
            return a.GetCentroid() <= b.GetCentroid();
        }

        public static bool operator >=(Triangle a, Triangle b)
        {
            return a.GetCentroid() >= b.GetCentroid();
        }
    }
}