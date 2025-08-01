using System;
using Godot;

public static class VectorUtils
{
    public const double INVERSE_SQUARE_ROOT_2 = 0.70710677;
    public const double SQUARE_ROOT_2_2 = 0.707106781187;
    public const double SQUARE_ROOT_2 = 1.41421356237;

    public static readonly Vector3[] Corners =
    [
        new (-1,  1,  1), // Left-Top-Front
        new (-1,  1, -1), // Left-Top-Back
        new ( 1,  1,  1), // Right-Top-Front
        new ( 1,  1, -1), // Right-Top-Back
        new (-1, -1,  1), // Left-Bottom-Front
        new (-1, -1, -1), // Left-Bottom-Back
        new ( 1, -1,  1), // Right-Bottom-Front
        new ( 1, -1, -1), // Right-Bottom-Back
    ];

    public static bool IsGreaterVector3(Vector3 vectorA, Vector3 vectorB) => vectorA.X > vectorB.X && vectorA.Y > vectorB.Y && vectorA.Z > vectorB.Z;
    public static bool IsGreaterVector2(Vector2 vectorA, Vector2 vectorB) => vectorA.X > vectorB.X && vectorA.Y > vectorB.Y;

    public static bool IsLesserVector3(Vector3 vectorA, Vector3 vectorB) => vectorA.X < vectorB.X && vectorA.Y < vectorB.Y && vectorA.Z < vectorB.Z;
    public static bool IsLesserVector2(Vector2 vectorA, Vector2 vectorB) => vectorA.X < vectorB.X && vectorA.Y < vectorB.Y;

    public static bool IsEqualVector3(Vector3 vectorA, Vector3 vectorB) => vectorA.X == vectorB.X && vectorA.Y == vectorB.Y && vectorA.Z == vectorB.Z;
    public static bool IsEqualVector2(Vector2 vectorA, Vector2 vectorB) => vectorA.X == vectorB.X && vectorA.Y == vectorB.Y;

    public static bool IsGreaterEqualVector3(Vector3 vectorA, Vector3 vectorB) => vectorA.X >= vectorB.X && vectorA.Y >= vectorB.Y && vectorA.Z >= vectorB.Z;
    public static bool IsGreaterEqualVector2(Vector2 vectorA, Vector2 vectorB) => vectorA.X >= vectorB.X && vectorA.Y >= vectorB.Y;

    public static bool IsLesserEqualVector3(Vector3 vectorA, Vector3 vectorB) => vectorA.X <= vectorB.X && vectorA.Y <= vectorB.Y && vectorA.Z <= vectorB.Z;
    public static bool IsLesserEqualVector2(Vector2 vectorA, Vector2 vectorB) => vectorA.X <= vectorB.X && vectorA.Y <= vectorB.Y;

    public static Vector3 GetCentroid(Vector3[] vectors)
    {
        Vector3 centroid = Vector3.Zero;
        foreach (Vector3 vector in vectors)
            centroid += vector;

        return centroid / vectors.Length;
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

    public static int SignOfNormal(Vector3 vector) => (int)vector[GetIndexOfNormalComponent(vector)];

    public static Vector4 ToVector4(Vector3 vector, float padValue) => new(vector.X, vector.Y, vector.Z, padValue);

    public static Vector4 ToVector4(Color color) => new(color.R, color.G, color.B, color.A);

    public static Vector3 ToVector3(Vector4 vector) => new(vector.X, vector.Y, vector.Z);

    public static Vector3 ToVector3(Vector2 vector, float padding) => new(vector.X, vector.Y, padding);

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

    public static Vector3 PointOnSphereToPointOnCube(Vector3 point)
    {
        Vector3 cubePoint = Vector3.Zero;
        double x = point.X;
        double y = point.Y;
        double z = point.Z;

        float fx = Math.Abs(point.X);
        float fy = Math.Abs(point.Y);
        float fz = Math.Abs(point.Z);

        float signX = x > 0 ? 1 : -1;
        float signY = y > 0 ? 1 : -1;
        float signZ = z > 0 ? 1 : -1;

        if (fx >= fy && fx >= fz)
        {
            double a2 = y * y * 2.0;
            double b2 = z * z * 2.0;
            double inner = -a2 + b2 - 3;
            double innersqrt = -Math.Sqrt((inner * inner) - 12.0 * a2);

            cubePoint.X = signX;
            cubePoint.Y = (float)(y == 0.0 || y == -0.0 ? 0.0f : signY * Mathf.Clamp(Math.Sqrt(innersqrt + a2 - b2 + 3.0) * INVERSE_SQUARE_ROOT_2, -1, 1));
            cubePoint.Z = (float)(z == 0.0 || z == -0.0 ? 0.0f : signZ * Mathf.Clamp(Math.Sqrt(innersqrt - a2 + b2 + 3.0) * INVERSE_SQUARE_ROOT_2, -1, 1));
        }
        else if (fy >= fx && fy >= fz)
        {
            double a2 = x * x * 2.0;
            double b2 = z * z * 2.0;
            double inner = -a2 + b2 - 3;
            double innersqrt = -Math.Sqrt((inner * inner) - 12.0 * a2);

            cubePoint.X = (float)(x == 0.0 || x == -0.0 ? 0.0f : signX * Mathf.Clamp(Math.Sqrt(innersqrt + a2 - b2 + 3.0) * INVERSE_SQUARE_ROOT_2, -1, 1));
            cubePoint.Y = signY;
            cubePoint.Z = (float)(z == 0.0 || z == -0.0 ? 0.0f : signZ * Mathf.Clamp(Math.Sqrt(innersqrt - a2 + b2 + 3.0) * INVERSE_SQUARE_ROOT_2, -1, 1));
        }
        else if (fz >= fx && fz >= fy)
        {
            double a2 = x * x * 2.0;
            double b2 = y * y * 2.0;
            double inner = -a2 + b2 - 3;
            double innersqrt = -Math.Sqrt((inner * inner) - 12.0 * a2);

            cubePoint.X = (float)(x == 0.0 || x == -0.0 ? 0.0f : signX * Mathf.Clamp(Math.Sqrt(innersqrt + a2 - b2 + 3.0) * INVERSE_SQUARE_ROOT_2, -1, 1));
            cubePoint.Y = (float)(y == 0.0 || y == -0.0 ? 0.0f : signY * Mathf.Clamp(Math.Sqrt(innersqrt - a2 + b2 + 3.0) * INVERSE_SQUARE_ROOT_2, -1, 1));
            cubePoint.Z = signZ;
        }
        return cubePoint;
    }

    public static Vector2 PointOnCubeToUV(Vector3 point)
    {
        if (point.X == 1 || point.X == -1) return (new Vector2(point.Y, point.Z) + Vector2.One) / 2;
        if (point.Y == 1 || point.Y == -1) return (new Vector2(point.X, point.Y) + Vector2.One) / 2;
        if (point.Z == 1 || point.Z == -1) return (new Vector2(point.X, point.Y) + Vector2.One) / 2;
        return Vector2.Zero;
    }

    public static Vector2 PointOnSphereToUV(Vector3 point)
    {
        float longitude = Mathf.Atan2(point.X, point.Z);
        float latitude = Mathf.Asin(-point.Y);
        float u = (longitude / Mathf.Pi + 1) * 0.5f;
        float v = latitude / Mathf.Pi + 0.5f;
        return new Vector2(u, v);
    }

    public static Vector3 IsolateNormal(Vector3 point)
    {
        if (point.X == 1 || point.X == -1) return new Vector3(point.X, 0, 0);
        if (point.Y == 1 || point.Y == -1) return new Vector3(0, point.Y, 0);
        if (point.Z == 1 || point.Z == -1) return new Vector3(0, 0, point.Z);
        return Vector3.Zero;
    }

    public static Color ToColor(Vector4 vector) => new(vector.X, vector.Y, vector.Z, vector.W);

    public static Vector2 Rotate45(Vector2 vector)
    {
        float cos45 = (float)SQUARE_ROOT_2_2;
        float sin45 = (float)SQUARE_ROOT_2_2;

        float xNew = vector.X * cos45 - vector.Y * sin45;
        float yNew = vector.X * sin45 + vector.Y * cos45;

        return new Vector2(xNew, yNew);
    }

    public static Vector3 UVToPointOnCube(int normalId, Vector2 uv)
    {
        Vector3 point = Vector3.Zero;

        switch (normalId)
        {
            case 0:
                point = new Vector3(1.0f, 1.0f - uv.Y, 1.0f - uv.X);
                point.Y = 2.0f * point.Y - 1.0f;
                point.Z = 2.0f * point.Z - 1.0f;
                break;

            case 1:
                point = new Vector3(-1.0f, 1.0f - uv.Y, uv.X);
                point.Y = 2.0f * point.Y - 1.0f;
                point.Z = 2.0f * point.Z - 1.0f;
                break;

            case 2:
                point = new Vector3(1.0f - uv.X, 1.0f, 1.0f - uv.Y);
                point.X = 2.0f * point.X - 1.0f;
                point.Z = 2.0f * point.Z - 1.0f;
                break;

            case 3:
                point = new Vector3(uv.X, -1.0f, 1.0f - uv.Y);
                point.X = 2.0f * point.X - 1.0f;
                point.Z = 2.0f * point.Z - 1.0f;
                break;

            case 4:
                point = new Vector3(uv.X, 1.0f - uv.Y, 1.0f);
                point.X = 2.0f * point.X - 1.0f;
                point.Y = 2.0f * point.Y - 1.0f;
                break;

            case 5:
                point = new Vector3(1.0f - uv.X, 1.0f - uv.Y, -1.0f);
                point.X = 2.0f * point.X - 1.0f;
                point.Y = 2.0f * point.Y - 1.0f;
                break;
        }
        return point;
    }

    public static Vector3 PointOnPlaneToPointOnCube(float x, float y, int size, int normalId)
    {
        float half = (size - 1) / 2.0f;
        float fx = (x - half) / (size - 1);
        float fy = (y - half) / (size - 1);

        return normalId switch
        {
            0 => new Vector3(0.5f, -fy, -fx),  // +X
            1 => new Vector3(-0.5f, -fy, fx),  // -X
            2 => new Vector3(-fx, 0.5f, -fy),  // +Y
            3 => new Vector3(fx, -0.5f, -fy),  // -Y
            4 => new Vector3(fx, -fy, 0.5f),   // +Z
            5 => new Vector3(-fx, -fy, -0.5f), // -Z
            _ => Vector3.Zero,
        };
    }
    
    public static Vector3 PointOnPlaneToPointOnSphere(float x, float y, int size, int normalId)
	{
		Vector3 cubePoint = PointOnPlaneToPointOnCube(x, y, size, normalId);
		return PointOnCubeToPointOnSphere(cubePoint) * size;
	}

}

