using System;
using Godot;

namespace PlanetGame.Util.Orometry
{
    public class Gradient(Simplex higher, Simplex lower, Gradient.GradientDirection direction)
    {
        public Simplex Higher { get; private set; } = higher;
        public Simplex Lower { get; private set; } = lower;
        public GradientDirection Direction { get; private set; } = direction;

        public enum GradientDirection
        {
            UNASSIGNED,
            IGNORED,
            CRITICAL,
            NON_CRITICAL,

            UP,
            DOWN,
            LEFT,
            RIGHT,
            TOP_LEFT,
            TOP_RIGHT,
            BOTTOM_LEFT,
            BOTTOM_RIGHT,
        }

        public static float ToRotation(GradientDirection direction)
        {
            return direction switch
            {
                GradientDirection.UP => 0,
                GradientDirection.DOWN => Mathf.Pi,
                GradientDirection.LEFT => Mathf.Pi / 2,
                GradientDirection.RIGHT => Mathf.Pi * 3 / 2,
                GradientDirection.TOP_LEFT => Mathf.Pi / 4,
                GradientDirection.TOP_RIGHT => Mathf.Pi * 7 / 4,
                GradientDirection.BOTTOM_LEFT => Mathf.Pi * 3 / 4,
                GradientDirection.BOTTOM_RIGHT => Mathf.Pi * 5 / 4,
                _ => 0
            };
        }

        public static GradientDirection GetReverseDirection(GradientDirection direction)
        {
            return direction switch
            {
                GradientDirection.UP => GradientDirection.DOWN,
                GradientDirection.DOWN => GradientDirection.UP,
                GradientDirection.LEFT => GradientDirection.RIGHT,
                GradientDirection.RIGHT => GradientDirection.LEFT,
                GradientDirection.TOP_LEFT => GradientDirection.BOTTOM_RIGHT,
                GradientDirection.BOTTOM_RIGHT => GradientDirection.TOP_LEFT,
                GradientDirection.TOP_RIGHT => GradientDirection.BOTTOM_LEFT,
                GradientDirection.BOTTOM_LEFT => GradientDirection.TOP_RIGHT,
                _ => GradientDirection.IGNORED
            };
        }

        public static GradientDirection VectorToDirection(Vector2I vector)
        {
            return vector switch
            {
                { X: 0, Y: -1 } => GradientDirection.UP,
                { X: 0, Y: 1 } => GradientDirection.DOWN,
                { X: -1, Y: 0 } => GradientDirection.LEFT,
                { X: 1, Y: 0 } => GradientDirection.RIGHT,
                { X: -1, Y: -1 } => GradientDirection.TOP_LEFT,
                { X: 1, Y: 1 } => GradientDirection.BOTTOM_RIGHT,
                { X: 1, Y: -1 } => GradientDirection.TOP_RIGHT,
                { X: -1, Y: 1 } => GradientDirection.BOTTOM_LEFT,
                _ => GradientDirection.IGNORED
            };
        }

        public static Vector2I DirectionToVector(GradientDirection direction)
        {
            return direction switch
            {
                GradientDirection.UP => Vector2I.Up,
                GradientDirection.DOWN => Vector2I.Down,
                GradientDirection.LEFT => Vector2I.Left,
                GradientDirection.RIGHT => Vector2I.Right,
                GradientDirection.TOP_LEFT => Vector2I.Up + Vector2I.Left,
                GradientDirection.BOTTOM_RIGHT => Vector2I.Down + Vector2I.Right,
                GradientDirection.TOP_RIGHT => Vector2I.Up + Vector2I.Right,
                GradientDirection.BOTTOM_LEFT => Vector2I.Down + Vector2I.Left,
                _ => Vector2I.Zero
            };
        }

        public bool IsCritical()
        {
            return Direction == GradientDirection.CRITICAL;
        }

        public bool IsUnassigned()
        {
            return Direction == GradientDirection.UNASSIGNED;
        }

        public bool IsIgnored()
        {
            return Direction == GradientDirection.IGNORED;
        }

        public bool IsNonCritical()
        {
            return Direction == GradientDirection.NON_CRITICAL;
        }

        public bool IsDirectional()
        {
            return Direction >= GradientDirection.UP && Direction <= GradientDirection.BOTTOM_RIGHT;
        }

        public override string ToString()
        {
            string gradient = Lower == null ? "null" : Lower.ToString();
            return $"({gradient} {Direction})";
        }

        public override bool Equals(object obj)
        {
            if (obj is Gradient other)
                return Direction == other.Direction && Lower == other.Lower && Higher == other.Higher;
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Lower, Direction);
        }
    }
}