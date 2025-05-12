using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
namespace PlanetGame.Util.Orometry
{
    public class Square : Simplex
    {
        public readonly Point PointA;
        public readonly Point PointB;
        public readonly Point PointC;
        public readonly Point PointD;

        public Square(Point pointA, Point pointB, Point pointC, Point pointD)
        {

            PointA = pointA;
            PointB = pointB;
            PointC = pointC;
            PointD = pointD;
        }

        public Point[] GetPoints()
        {
            List<Point> points = [PointA, PointB, PointC, PointD];
            return [.. points.OrderBy(p => p.GetLowerValue())];
        }

        public Point[] GetOtherPoints(Edge edge)
        {
            Edge[] edges = GetEdges();
            if (!edges.Contains(edge))
                throw new ArgumentException("Edge is not part of the square.");

            return [.. GetPoints().Where(p => p != edge.PointA && p != edge.PointB)];
        }

        public Edge[] GetEdges()
        {
            return [
                new(PointA, PointB),
                new(PointB, PointC),
                new(PointC, PointD),
                new(PointD, PointA)
            ];
        }

        public override bool Equals(object obj)
        {
            if (obj is Square other)
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
            int hash4 = PointD.GetHashCode();
            return hash1 ^ hash2 ^ hash3 ^ hash4;
        }

        public override string ToString()
        {
            Vector2 position = (PointA.Position + PointB.Position + PointC.Position + PointD.Position) / 4;
            position = position.Floor();
            return $"Square({position})";
            // return $"Square({PointA}, {PointB}, {PointC}, {PointD})";
        }

        public override float GetUpperValue()
        {
            List<Point> points = [PointA, PointB, PointC, PointD];
            return points.Max(p => p.GetUpperValue());
        }

        public override float GetLowerValue()
        {
            List<Point> points = [PointA, PointB, PointC, PointD];
            return points.Min(p => p.GetLowerValue());
        }

        public override float GetAverageValue()
        {
            List<Point> points = [PointA, PointB, PointC, PointD];
            return points.Average(p => p.Elevation);
        }

        public override Vector3 GetCentroid()
        {
            Vector2 positionCentroid = PointA.Position + PointB.Position + PointC.Position + PointD.Position;
            float elevationCentroid = PointA.Elevation + PointB.Elevation + PointC.Elevation + PointD.Elevation;
            Vector3 centroid = new(positionCentroid.X, elevationCentroid, positionCentroid.Y);
            return centroid / 4;
        }

        public Edge[] GetOtherEdges(Edge edge)
        {
            return [.. GetEdges().Where(e => !e.Equals(edge))];
        }
    }
}