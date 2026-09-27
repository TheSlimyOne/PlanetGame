using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
namespace PlanetGame.Util.Geometry
{
    public struct Square(Vector3 pointA, Vector3 pointB, Vector3 pointC, Vector3 pointD)
    {
        public readonly Vector3 PointA = pointA;
        public readonly Vector3 PointB = pointB;
        public readonly Vector3 PointC = pointC;
        public readonly Vector3 PointD = pointD;

        public readonly Vector3[] GetPoints()
        {
            return [PointA, PointB, PointC, PointD];
        }

        public readonly Vector3[] GetOtherPoints(Edge edge)
        {
            Edge[] edges = GetEdges();
            if (!edges.Contains(edge))
                throw new ArgumentException("Edge is not part of the square.");

            return [.. GetPoints().Where(p => p != edge.PointA && p != edge.PointB)];
        }

        public readonly Edge[] GetEdges()
        {
            return [
                new(PointA, PointB),
                new(PointB, PointC),
                new(PointC, PointD),
                new(PointD, PointA)
            ];
        }

        public override readonly bool Equals(object obj)
        {
            if (obj is Square other)
            {
                HashSet<Vector3> points = [.. GetPoints()];
                HashSet<Vector3> otherPoints = [.. other.GetPoints()];
                return points.SetEquals(otherPoints);
            }
            return false;
        }

        public override readonly int GetHashCode()
        {
            int hash1 = PointA.GetHashCode();
            int hash2 = PointB.GetHashCode();
            int hash3 = PointC.GetHashCode();
            int hash4 = PointD.GetHashCode();
            return hash1 ^ hash2 ^ hash3 ^ hash4;
        }

        public override readonly string ToString()
        {
            Vector3 position = (PointA + PointB + PointC + PointD) / 4;
            position = position.Floor();
            return $"Square({position})";
            // return $"Square({PointA}, {PointB}, {PointC}, {PointD})";
        }

        public readonly Edge[] GetOtherEdges(Edge edge)
        {
            return [.. GetEdges().Where(e => !e.Equals(edge))];
        }

        public readonly Vector3 GetCentroid()
        {
            return (PointA + PointB + PointC + PointD) / 4;
        }

        public static bool operator <(Square a, Square b)
        {
            return a.GetCentroid() < b.GetCentroid();
        }

        public static bool operator >(Square a, Square b)
        {
            return a.GetCentroid() > b.GetCentroid();
        }

        public static bool operator <=(Square a, Square b)
        {
            return a.GetCentroid() <= b.GetCentroid();
        }

        public static bool operator >=(Square a, Square b)
        {
            return a.GetCentroid() >= b.GetCentroid();
        }
    }
}