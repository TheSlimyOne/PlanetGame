using Godot;

public static class Vector3Utils
{
    public static bool IsGreaterVector3(Vector3 a, Vector3 b)
    {
        return a.X > b.X && a.Y > b.Y && a.Z > b.Z;
    }

    public static bool IsLesserVector3(Vector3 a, Vector3 b)
    {
        return a.X < b.X && a.Y < b.Y && a.Z < b.Z;
    }

    public static bool IsEqualVector3(Vector3 a, Vector3 b)
    {
        return a.X == b.X && a.Y == b.Y && a.Z == b.Z;
    }

    public static bool IsGreaterEqualVector3(Vector3 a, Vector3 b)
    {
        return a.X >= b.X && a.Y >= b.Y && a.Z >= b.Z;
    }

    public static bool IsLesserEqualVector3(Vector3 a, Vector3 b)
    {
        return a.X <= b.X && a.Y <= b.Y && a.Z <= b.Z;
    }

    public static Vector3 GetCentroid(Vector3[] vectors)
    {
        Vector3 centroid = Vector3.Zero;
        foreach (Vector3 vector in vectors)
            centroid += vector;
            
        return centroid / vectors.Length;
    }

    public static float CondenseVector3(Vector3 a)
    {
        for (int i = 0; i < 3; i++)
        {
            if (a[i] != 0)
            {
                return a[i];
            }
        }
        return -1;
    }

    public static Vector3 GetTriangularNormal(Vector3[] vertices)
    {
        Vector3 edge1 = vertices[1] - vertices[0];
        Vector3 edge2 = vertices[2] - vertices[0];

        return edge1.Cross(edge2).Normalized();
    }
}

