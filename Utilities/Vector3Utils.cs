using Godot;

public static class Vector3Utils
{
    public static bool IsGreaterVector3(Vector3 vectorA, Vector3 vectorB)
    {
        return vectorA.X > vectorB.X && vectorA.Y > vectorB.Y && vectorA.Z > vectorB.Z;
    }

    public static bool IsLesserVector3(Vector3 vectorA, Vector3 vectorB)
    {
        return vectorA.X < vectorB.X && vectorA.Y < vectorB.Y && vectorA.Z < vectorB.Z;
    }

    public static bool IsEqualVector3(Vector3 vectorA, Vector3 vectorB)
    {
        return vectorA.X == vectorB.X && vectorA.Y == vectorB.Y && vectorA.Z == vectorB.Z;
    }

    public static bool IsGreaterEqualVector3(Vector3 vectorA, Vector3 vectorB)
    {
        return vectorA.X >= vectorB.X && vectorA.Y >= vectorB.Y && vectorA.Z >= vectorB.Z;
    }

    public static bool IsLesserEqualVector3(Vector3 vectorA, Vector3 vectorB)
    {
        return vectorA.X <= vectorB.X && vectorA.Y <= vectorB.Y && vectorA.Z <= vectorB.Z;
    }

    public static Vector3 GetCentroid(Vector3[] vectors)
    {
        Vector3 centroid = Vector3.Zero;
        foreach (Vector3 vector in vectors)
            centroid += vector;
            
        return centroid / vectors.Length;
    }

    public static float CondenseVector3(Vector3 vectorA)
    {
        for (int i = 0; i < 3; i++)
        {
            if (vectorA[i] != 0)
            {
                return vectorA[i];
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

    public static Vector3 GenerateVectorMaskFrom(Vector3 vector)
    {
        Vector3 mask = Vector3.Zero;

        for (int i = 0; i < 3; i++)
        {
            mask[i] = vector[i] == 0 ? 1 : 0;
        }

        return mask;
    }

    public static bool ContainsValue(Vector3 vector, float value, Vector3 mask)
    {
        for (int i = 0; i < 3; i++)
        {
            if (mask[i] == 1 && vector[i] == value)
            {
                return true;
            }
        }
        return false;
    }
}

