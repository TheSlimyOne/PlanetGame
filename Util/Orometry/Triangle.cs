using System.Collections.Generic;
using System.Linq;
using Godot;
namespace PlanetGame.Util.Orometry
{
    public class Triangle : Simplex
    {
        public readonly Point PointA;
        public readonly Point PointB;
        public readonly Point PointC;

        public Triangle(Point pointA, Point pointB, Point pointC)
        {

            float area = (pointB.X - pointA.X) * (pointC.Y - pointA.Y) - (pointB.Y - pointA.Y) * (pointC.X - pointA.X);
            if (area < 0)
                (pointB, pointC) = (pointC, pointB);

            PointA = pointA;
            PointB = pointB;
            PointC = pointC;
        }

        public Point[] GetPoints()
        {
            List<Point> points = [PointA, PointB, PointC];
            return [..points.OrderBy(p => p.GetLowerValue())];
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
                HashSet<Point> points = [.. GetPoints()];
                HashSet<Point> otherPoints = [.. other.GetPoints()];
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

        public override float GetUpperValue()
        {
            List<Point> points = [PointA, PointB, PointC];
            return points.Max(p => p.GetUpperValue());
        }

        public override float GetLowerValue()
        {
            List<Point> points = [PointA, PointB, PointC];
            return points.Min(p => p.GetLowerValue());
        }
        
        public override float GetAverageValue()
        {
            List<Point> points = [PointA, PointB, PointC];
            return points.Average(p => p.Elevation);
        }

        public override Vector3 GetCentroid()
        {
            Vector2 positionCentroid = PointA.Position + PointB.Position + PointC.Position;
            float elevationCentroid = PointA.Elevation + PointB.Elevation + PointC.Elevation;
            Vector3 centroid = new(positionCentroid.X, elevationCentroid, positionCentroid.Y);
            return centroid / 3;
        }
    }
}