using System;
using Godot;

public static class VectorUtils
{
    public static bool IsGreaterVector3(Vector3 vectorA, Vector3 vectorB) => vectorA.X > vectorB.X && vectorA.Y > vectorB.Y && vectorA.Z > vectorB.Z;
    

    public static bool IsLesserVector3(Vector3 vectorA, Vector3 vectorB) => vectorA.X < vectorB.X && vectorA.Y < vectorB.Y && vectorA.Z < vectorB.Z;
    

    public static bool IsEqualVector3(Vector3 vectorA, Vector3 vectorB) => vectorA.X == vectorB.X && vectorA.Y == vectorB.Y && vectorA.Z == vectorB.Z;
    

    public static bool IsGreaterEqualVector3(Vector3 vectorA, Vector3 vectorB) => vectorA.X >= vectorB.X && vectorA.Y >= vectorB.Y && vectorA.Z >= vectorB.Z;
    

    public static bool IsLesserEqualVector3(Vector3 vectorA, Vector3 vectorB) => vectorA.X <= vectorB.X && vectorA.Y <= vectorB.Y && vectorA.Z <= vectorB.Z;

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

    public static Vector3 GenerateVectorExclusionMaskFrom(Vector3 vector)
    {
        Vector3 mask = Vector3.Zero;

        for (int i = 0; i < 3; i++)
        {
            mask[i] = vector[i] == 0 ? 1 : 0;
        }

        return mask;
    }

    public static int GetIndexOfNormalComponent(Vector3 vector)
    {
        for (int i = 0; i < 3; i++)
        {
            if (vector[i] != 0)
                return i;
        }
        return -1;
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

    public static int SignOfNormal(Vector3 vector)=> (int)vector[GetIndexOfNormalComponent(vector)];

    public static Vector4 toVector4(Vector3 vector, float padValue) => new(vector.X, vector.Y, vector.Z, padValue);

    public static Vector3 toVector3(Vector4 vector) => new(vector.X, vector.Y, vector.Z);

    public static Vector3 toVector3(Vector2 vector, float padding) => new(vector.X, vector.Y, padding);

    public static Vector2 toVector2(Vector3 vector) => new(vector.X, vector.Y);


    public static Vector3 PointOnCubeToPointOnSphere(Vector3 point)
    {
        float x2 = point.X * point.X;
        float y2 = point.Y * point.Y;
        float z2 = point.Z * point.Z;

        float x = point.X * Mathf.Sqrt(1 - (y2 + z2) / 2 + y2 * z2 / 3);
        float y = point.Y * Mathf.Sqrt(1 - (z2 + x2) / 2 + z2 * x2 / 3);
        float z = point.Z * Mathf.Sqrt(1 - (x2 + y2) / 2 + x2 * y2 / 3);

        return new Vector3(x, y, z);
    }

    public static Vector2 PointOnSphereToUV(Vector3 point)
    {
        float longitude = Mathf.Atan2(point.X, point.Z);
        float latitude = Mathf.Asin(-point.Y);
        float u = (longitude / Mathf.Pi + 1) * 0.5f;
        float v = latitude / Mathf.Pi + 0.5f;

        return new Vector2(u, v);
    }

    public static Color ToColor(Vector4 vector) => new(vector.X, vector.Y, vector.Z, vector.W);
    

}

