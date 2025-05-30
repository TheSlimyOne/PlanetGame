using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PlanetGame.Util.Orometry
{
    public class Edge : Simplex
    {
        public readonly Point PointA;
        public readonly Point PointB;

        public Edge(Point pointA, Point pointB)
        {
            PointA = pointA;
            PointB = pointB;
        }

        public Point[] GetPoints()
        {
            List<Point> points = [PointA, PointB];
            return [.. points.OrderBy(p => p.GetLowerValue())];
        }

        public Point GetOtherPoint(Point point)
        {
            Point[] points = GetPoints();
            if (!points.Contains(point))
                throw new ArgumentException("Point is not part of the edge.");

            return points.First(p => p != point);
        }

        public override bool Equals(object obj)
        {
            if (obj is Edge other)
                return (PointA.Equals(other.PointA) && PointB.Equals(other.PointB)) ||
                       (PointA.Equals(other.PointB) && PointB.Equals(other.PointA));
            return false;
        }

        public override int GetHashCode()
        {
            int hash1 = PointA.GetHashCode();
            int hash2 = PointB.GetHashCode();
            return hash1 ^ hash2;
        }

        public override string ToString()
        {
            return $"Edge({PointA}, {PointB})";
        }

        public override float GetUpperValue()
        {
            List<Point> points = [PointA, PointB];
            return points.Max(p => p.GetUpperValue());
        }

        public override float GetLowerValue()
        {
            List<Point> points = [PointA, PointB];
            return points.Min(p => p.GetLowerValue());
        }

        public override float GetAverageValue()
        {
            List<Point> points = [PointA, PointB];
            return points.Average(p => p.Elevation);
        }

        public override Vector3 GetCentroid()
        {
            Vector2 midPosition = PointA.Position + PointB.Position;
            float elevationCentroid = PointA.Elevation + PointB.Elevation;
            return new Vector3(midPosition.X, elevationCentroid, midPosition.Y) / 2;
        }
    }
}