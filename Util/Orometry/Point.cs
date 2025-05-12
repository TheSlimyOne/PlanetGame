using Godot;
using System;
namespace PlanetGame.Util.Orometry
{
    public class Point : Simplex
    {
        public readonly int X;
        public readonly int Y;
        public readonly float Elevation;

        public Vector2I Position => new(X, Y);

        public Point(int x, int y, float elevation)
        {
            X = x;
            Y = y;
            Elevation = elevation;
        }

        public Point(Vector3 vector) : this((int)vector.X, (int)vector.Z, vector.Y) { }

        public override bool Equals(object obj)
        {
            if (obj is Point other)
                return X == other.X && Y == other.Y;
            return false;
        }

        public bool EqualElevation(Point point)
        {
           return point.Elevation == Elevation;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }

        public override string ToString()
        {
            return $"Point({X}, {Y}, {Elevation})";
        }

        public override float GetUpperValue()
        {
            return Elevation;
        }

        public override float GetLowerValue()
        {
            return Elevation;
        }

        public override float GetAverageValue()
        {
            return Elevation;
        }

        public override Vector3 GetCentroid()
        {
            return new (X, Elevation, Y);
        }

        public static bool operator <(Point a, Point b)
        {
            return a.Elevation < b.Elevation;
        }

        public static bool operator >(Point a, Point b)
        {
            return a.Elevation > b.Elevation;
        }

        public static bool operator <=(Point a, Point b)
        {
            return a.Elevation <= b.Elevation;
        }

        public static bool operator >=(Point a, Point b)
        {
            return a.Elevation >= b.Elevation;
        }
    }
}