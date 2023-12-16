using Godot;

public static class Vector3Utils
{
    public static bool isGreaterVector3(Vector3 a, Vector3 b)
    {
        return a.X > b.X && a.Y > b.Y && a.Z > b.Z;
    }

    public static bool isLesserVector3(Vector3 a, Vector3 b)
    {
        return a.X < b.X && a.Y < b.Y && a.Z < b.Z;
    }

    public static bool isEqualVector3(Vector3 a, Vector3 b)
    {
        return a.X == b.X && a.Y == b.Y && a.Z == b.Z;
    }

    public static bool isGreaterEqualVector3(Vector3 a, Vector3 b)
    {
        return a.X >= b.X && a.Y >= b.Y && a.Z >= b.Z;
    }

    public static bool isLesserEqualVector3(Vector3 a, Vector3 b)
    {
        return a.X <= b.X && a.Y <= b.Y && a.Z <= b.Z;
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
}

