using Godot;

public struct Rect3
{
    public Vector3 Position;
    public Vector3 Size;

    public readonly Vector3 End => Position + Size;
    public readonly Vector3 Center => Position + Size / 2f;

    public Rect3(Vector3 position, Vector3 size)
    {
        Position = position;
        Size = size;
    }

    public readonly bool Intersects(Rect3 other)
    {
        return Position.X <= other.End.X && End.X >= other.Position.X
            && Position.Y <= other.End.Y && End.Y >= other.Position.Y
            && Position.Z <= other.End.Z && End.Z >= other.Position.Z;
    }

    public readonly bool HasPoint(Vector3 point)
    {
        return point.X >= Position.X && point.X <= End.X
            && point.Y >= Position.Y && point.Y <= End.Y
            && point.Z >= Position.Z && point.Z <= End.Z;
    }

    public readonly Rect3 Translated(Vector3 offset)
    {
        return new Rect3(Position + offset, Size);
    }

    public readonly Rect3 Scaled(Vector3 scale)
    {
        return new Rect3(Position * scale, Size * scale);
    }

    public readonly Rect3 Rotated(Vector3 axis, float angle)
    {
        return Transformed(new Basis(axis.Normalized(), angle));
    }

    public readonly Rect3 Transformed(Basis basis)
    {
        Vector3 min = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3 max = new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = Position + new Vector3(
                (i & 1) != 0 ? Size.X : 0,
                (i & 2) != 0 ? Size.Y : 0,
                (i & 4) != 0 ? Size.Z : 0
            );

            Vector3 point = basis * corner;

            min = min.Min(point);
            max = max.Max(point);
        }

        return new Rect3(min, max - min);
    }

    public readonly Rect3 Transformed(Transform3D transform)
    {
        Rect3 result = Transformed(transform.Basis);
        return result.Translated(transform.Origin);
    }

    public readonly Vector3[] GetCorners()
    {
        return
        [
            Position,
            Position + new Vector3(Size.X, 0, 0),
            Position + new Vector3(0, Size.Y, 0),
            Position + new Vector3(Size.X, Size.Y, 0),
            Position + new Vector3(0, 0, Size.Z),
            Position + new Vector3(Size.X, 0, Size.Z),
            Position + new Vector3(0, Size.Y, Size.Z),
            End
        ];
    }

    public override readonly string ToString()
    {
        return $"Rect3(Position: {Position}, Size: {Size})";
    }
}