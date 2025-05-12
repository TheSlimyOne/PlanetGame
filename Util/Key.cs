using Godot;
using System;

public struct Key
{
    readonly public uint MSB;
    readonly public uint LSB;
    readonly public uint MeshPolygonID;
    readonly public uint RootID; // FFF0000000000000000000000000RRRR

    public Key(Color color) : this(color.R, color.G, color.B, color.A) { }
    private Key(float msb, float lsb, float meshPolygonID, float rootID) : this(BitConverter.SingleToUInt32Bits(msb), BitConverter.SingleToUInt32Bits(lsb), BitConverter.SingleToUInt32Bits(meshPolygonID), BitConverter.SingleToUInt32Bits(rootID)) { }
    public Key(int msb, int lsb, int meshPolygonID, int rootID) : this((uint)msb, (uint)lsb, (uint)meshPolygonID, (uint)rootID) { }
    public Key(uint msb, uint lsb, uint meshPolygonID, uint rootID)
    {
        MSB = msb;
        LSB = lsb;
        MeshPolygonID = meshPolygonID;
        RootID = rootID;
    }

    public readonly uint[] GetNodeID()
    {
        return new uint[] { MSB, LSB };
    }

    public readonly Color ToColor()
    {
        return new Color(
            BitConverter.UInt32BitsToSingle(MSB),
            BitConverter.UInt32BitsToSingle(LSB),
            BitConverter.UInt32BitsToSingle(MeshPolygonID),
            BitConverter.UInt32BitsToSingle(RootID)
        );
    }

    public readonly float[] ToSingles()
    {
        return new float[]{

            BitConverter.UInt32BitsToSingle(MSB),
            BitConverter.UInt32BitsToSingle(LSB),
            BitConverter.UInt32BitsToSingle(MeshPolygonID),
            BitConverter.UInt32BitsToSingle(RootID)
        };
    }

    public readonly uint GetRootID()
    {
        return RootID;
    }

    public int GetLevelInKey()
    {
        return FindMSB64() / 2;
    }

    public readonly int FindMSB64()
    {
        return (MSB == 0) ? FindMSB(LSB) : (FindMSB(MSB) + 32);
    }

    public readonly float GetScale()
    {
        return Mathf.Pow(0.5f, FindMSB64() / 2f);
    }

    public static int FindMSB(uint n)
    {
        int msb = 0;
        while (n > 1)
        {
            n >>= 1;
            msb++;
        }

        return msb;
    }

    public readonly uint GetBranching(int level)
    {
        int msb = FindMSB64();
        return RightShift64(GetNodeID(), (msb - 2) - (level * 2))[1] & 0x3;
    }

    public static Vector2 GetTranslation(uint b1)
    {
        Vector2 translation = new Vector2(b1 & 0x1, b1 ^ 0x1);
        return translation * 0.5f;
    }

    public static uint[] LeftShift64(uint[] nodeID, int shift)
    {
        uint[] result = new uint[2];

        if (shift == 0)
        {
            result[0] = nodeID[0];
            result[1] = nodeID[1];
        }
        else if (shift < 32)
        {
            result[0] = nodeID[0] << shift | nodeID[1] >> (32 - shift);
            result[1] = nodeID[1] << shift;
        }
        else
        {
            result[0] = nodeID[1] << (shift - 32);
            result[1] = 0;
        }

        return result;
    }

    public static uint[] RightShift64(uint[] nodeID, int shift)
    {
        uint[] result = new uint[2];

        if (shift == 0)
        {
            result[0] = nodeID[0];
            result[1] = nodeID[1];
        }
        else if (shift < 32)
        {
            result[1] = nodeID[1] >> shift | nodeID[0] << (32 - shift);
            result[0] = nodeID[0] >> shift;
        }
        else
        {
            result[1] = nodeID[0] >> (shift - 32);
            result[0] = 0;
        }

        return result;
    }

    public static Vector2 rotate(int rotationIndex, Vector2 translation)
    {
        Vector2I trig = QuickPI_2(rotationIndex);
        Vector2 r = new Vector2(
            trig.X * translation.X - trig.Y * translation.Y,
            trig.Y * translation.X + trig.X * translation.Y);

        return r;
    }

    public static int getRotation(uint b1b2, uint b1, uint b2)
    {
        uint a = b1b2 ^ 0x2;
        uint b = a | 0x1;
        uint c = b1 ^ b2;
        return (int)(b * c);
    }

    public static Vector2I QuickPI_2(int a)
    {
        int b = a & 3;
        int b1 = b >> 1;
        int b2 = b & 1;
        int bn2 = b2 ^ 1;
        int c = bn2 - (2 * (b1 & bn2));
        int s = b2 - (2 * (b1 & b2));
        return new Vector2I(c, s);
    }

    public Vector4 GetTransformation()
    {
        int msb = FindMSB64();
        Vector2 translation = new Vector2(0, 0);
        Vector2 temp;
        int theta = 0;
        float scale = 1.0f;

        for (int i = 0; i < msb / 2; i++)
        {
            uint b1b2 = GetBranching(i);

            uint b1 = b1b2 >> 1;
            uint b2 = b1b2 & 1;
            temp = scale * GetTranslation(b1);

            translation += rotate(theta, temp);
            theta += getRotation(b1b2, b1, b2);
            scale *= 0.5f;
        }

        return new Vector4(theta, scale, translation.X, translation.Y);
    }

    public Basis LeafSpaceToWorldSpace()
    {
        int msb = FindMSB64();

        Vector2 translation = new Vector2(0, 0);
        Vector2 temp;
        int theta = 0;
        float scale = 1.0f;

        for (int i = 0; i < msb / 2; i++)
        {
            uint b1b2 = GetBranching(i);
            uint b1 = b1b2 >> 1;
            uint b2 = b1b2 & 1;
            temp = scale * GetTranslation(b1);

            translation += rotate(theta, temp);
            theta += getRotation(b1b2, b1, b2);
            scale *= 0.5f;
        }

        Vector2I trig = QuickPI_2(theta);
        Basis transform_matrix = new Basis(
            new Vector3(trig.X * scale, -trig.Y * scale, translation.X),
            new Vector3(trig.Y * scale, trig.X * scale, translation.Y),
            new Vector3(0.0f, 0.0f, 1.0f)
        );

        return transform_matrix;
    }

    public Color Colorize()
    {
        int msb = GetLevelInKey();
        int a = (msb & 1) != 0 ? 1 : 0;
        int b = (msb & 2) != 0 ? 1 : 0;
        int c = (msb & 4) != 0 ? 1 : 0;
        return new Color(a, b, c);
    }

    // public Triangle CreateTriangle(float radius, Vector4[] position_list, Vector3 origin, bool isCube = false)
    // {
    //     Basis transform_matrix = LeafSpaceToWorldSpace();

    //     Vector2 point_a = VectorUtils.toVector2(new Vector3(0, 0, 1) * transform_matrix);
    //     Vector2 point_b = VectorUtils.toVector2(new Vector3(0, 1, 1) * transform_matrix);
    //     Vector2 point_c = VectorUtils.toVector2(new Vector3(1, 0, 1) * transform_matrix);

    //     Vector2 point_d = VectorUtils.toVector2(new Vector3(0.5f, 0.5f, 1) * transform_matrix);

    //     Vector2 point_e = VectorUtils.toVector2(new Vector3(-0.5f, 0.5f, 1) * transform_matrix);
    //     Vector2 point_f = VectorUtils.toVector2(new Vector3(0.5f, -0.5f, 1) * transform_matrix);

    //     uint rootID = GetRootID();
    //     uint vertexBaseIndex = MeshPolygonID * 5;
    //     uint vertexKeyA = rootID;
    //     uint vertexKeyB = ((rootID >> 1) ^ 1) + ((rootID & 1) << 1);

    //     Vector3 base_Triangle_a = VectorUtils.toVector3(position_list[vertexBaseIndex + vertexKeyA + 1]);
    //     Vector3 base_Triangle_b = VectorUtils.toVector3(position_list[vertexBaseIndex + vertexKeyB + 1]);
    //     Vector3 base_Triangle_c = VectorUtils.toVector3(position_list[vertexBaseIndex]);

    //     Vector3 point_A = LocalPointToWorldPoint(point_a, base_Triangle_a, base_Triangle_b, base_Triangle_c);
    //     Vector3 point_B = LocalPointToWorldPoint(point_b, base_Triangle_a, base_Triangle_b, base_Triangle_c);
    //     Vector3 point_C = LocalPointToWorldPoint(point_c, base_Triangle_a, base_Triangle_b, base_Triangle_c);
    //     Vector3 point_D = LocalPointToWorldPoint(point_d, base_Triangle_a, base_Triangle_b, base_Triangle_c);

    //     Vector3 point_E = LocalPointToWorldPoint(point_e, base_Triangle_a, base_Triangle_b, base_Triangle_c);
    //     Vector3 point_F = LocalPointToWorldPoint(point_f, base_Triangle_a, base_Triangle_b, base_Triangle_c);

    //     Triangle t = new Triangle(new Vector3[] {
    //         (isCube ? point_A : VectorUtils.PointOnCubeToPointOnSphere(point_A)) * radius,
    //         (isCube ? point_B : VectorUtils.PointOnCubeToPointOnSphere(point_B)) * radius,
    //         (isCube ? point_C : VectorUtils.PointOnCubeToPointOnSphere(point_C)) * radius
    //     }, origin
    //     );

    //     return t;
    // }

    public static Vector3 LocalPointToWorldPoint(Vector2 point, Vector3 vertexA, Vector3 vertexB, Vector3 vertexC)
    {
        return vertexA * point.X + vertexB * point.Y + vertexC * (1 - point.X - point.Y);
    }

    public static Key[] GenerateFullFace(int lod, int meshPolygonID)
    {
        if (lod > 7 || lod < 0) throw new ArgumentException($"Lod of {lod} is out of bounds.");

        int amount = (int)Mathf.Pow(4.0, lod);
        Key[] keys = new Key[4 * amount];

        int index = 0;
        for (int i = 0; i < amount; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                keys[index++] = new Key(0, amount + i, meshPolygonID, j);
            }
        }

        return keys;
    }

    public override string ToString()
    {
        uint rootID = GetRootID();
        return $"{Convert.ToString(MSB, 2).PadZeros(32)}, {Convert.ToString(LSB, 2).PadZeros(32)}, {MeshPolygonID,-4}, {rootID}";
    }
}