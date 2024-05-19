using Godot;
using System;

public struct Key
{
    readonly public uint MSB;
    readonly public uint LSB;
    readonly public uint MeshPolygonID;
    readonly public uint RootID;



    public Key(float msb, float lsb, float meshPolygonID, float rootID) : this(BitConverter.SingleToUInt32Bits(msb), BitConverter.SingleToUInt32Bits(lsb), BitConverter.SingleToUInt32Bits(meshPolygonID), BitConverter.SingleToUInt32Bits(rootID)) { }
    public Key(int msb, int lsb, int meshPolygonID, int rootID) : this((uint)msb, (uint)lsb, (uint)meshPolygonID, (uint)rootID) { }
    public Key(uint msb, uint lsb, uint meshPolygonID, uint rootID)
    {
        MSB = msb;
        LSB = lsb;
        MeshPolygonID = meshPolygonID;
        RootID = rootID;
    }

    public uint[] GetNodeID()
    {
        return new uint[] { MSB, LSB };
    }

    public Color ToColor()
    {
        return new Color(
            BitConverter.UInt32BitsToSingle(MSB),
            BitConverter.UInt32BitsToSingle(LSB),
            BitConverter.UInt32BitsToSingle(MeshPolygonID),
            BitConverter.UInt32BitsToSingle(RootID)
        );
    }

    public float[] ToSingles()
    {
        return new float[]{

            BitConverter.UInt32BitsToSingle(MSB),
            BitConverter.UInt32BitsToSingle(LSB),
            BitConverter.UInt32BitsToSingle(MeshPolygonID),
            BitConverter.UInt32BitsToSingle(RootID)
        };
    }


    public int GetLevelInKey()
    {
        return FindMSB64() / 2;
    }

    public int FindMSB64()
    {
        return (MSB == 0) ? findMSB(LSB) : (findMSB(MSB) + 32);
    }

    public static int findMSB(uint n)
    {
        int msb = 0;
        while (n > 1)
        {
            n >>= 1;
            msb++;
        }

        return msb;
    }

    public uint getBranching(int level)
    {
        int msb = FindMSB64();
        uint[] shiftedBits = rightShift64(GetNodeID(), (msb - 2) - (level * 2));

        // GD.Print( (msb - 2) - (level * 2));
        // GD.PrintS(Utilities.ToBinary(shiftedBits[0]), Utilities.ToBinary(shiftedBits[1]));
        // GD.PrintS(Utilities.ToBinary(GetNodeID()[0]), Utilities.ToBinary(GetNodeID()[1]));
        // GD.Print("=============================");

        return rightShift64(GetNodeID(), (msb - 2) - (level * 2))[1] & 0x3;
    }

    public static Vector2 getTranslation(uint b1)
    {
        Vector2 translation = new Vector2(b1 & 0x1, b1 ^ 0x1);
        return translation * 0.5f;
    }



    public static uint[] leftShift64(uint[] nodeID, int shift)
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

    public static uint[] rightShift64(uint[] nodeID, int shift)
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
        Vector2I trig = quickPI_2(rotationIndex);
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

    public static Vector2I quickPI_2(int a)
    {
        int b = a & 3;
        int b1 = b >> 1;
        int b2 = b & 1;
        int bn2 = b2 ^ 1;
        int c = bn2 - (2 * (b1 & bn2));
        int s = b2 - (2 * (b1 & b2));
        return new Vector2I(c, s);
    }

    public Vector4 getTransformation(Vector4I key)
    {
        int msb = FindMSB64();
        Vector2 translation = new Vector2(0, 0);
        Vector2 temp;
        int theta = 0;
        float scale = 1.0f;

        for (int i = 0; i < msb / 2; i++)
        {
            uint b1b2 = getBranching(i);

            uint b1 = b1b2 >> 1;
            uint b2 = b1b2 & 1;
            temp = scale * getTranslation(b1);

            translation += rotate(theta, temp);
            theta += getRotation(b1b2, b1, b2);
            scale *= 0.5f;
        }

        return new Vector4(theta, scale, translation.X, translation.Y);
    }

    public Basis leafSpaceToWorldSpace()
    {
        int msb = FindMSB64();

        Vector2 translation = new Vector2(0, 0);
        Vector2 temp;
        int theta = 0;
        float scale = 1.0f;
        string s = "";
        for (int i = 0; i < msb / 2; i++)
        {
            uint b1b2 = getBranching(i);
            uint b1 = b1b2 >> 1;
            uint b2 = b1b2 & 1;
            s += Convert.ToString(b1b2, 2).PadZeros(2) + " ";
            temp = scale * getTranslation(b1);

            translation += rotate(theta, temp);
            theta += getRotation(b1b2, b1, b2);
            scale *= 0.5f;
        }
        GD.Print(this);
        GD.Print(s);
        GD.Print();

        Vector2I trig = quickPI_2(theta);
        Basis transform_matrix = new Basis(
            new Vector3(trig.X * scale, -trig.Y * scale, translation.X),
            new Vector3(trig.Y * scale, trig.X * scale, translation.Y),
            new Vector3(0.0f, 0.0f, 1.0f)
        );

        return transform_matrix;
    }

    public Color colorize()
    {
        int msb = GetLevelInKey();
        int a = (msb & 1) != 0 ? 1 : 0;
        int b = (msb & 2) != 0 ? 1 : 0;
        int c = (msb & 4) != 0 ? 1 : 0;
        return new Color(a, b, c);
    }

    public Triangle createTriangle(float radius, Vector4[] position_list, Vector3 origin, bool isCube = false)
    {
        Basis transform_matrix = leafSpaceToWorldSpace();

        Vector2 point_a = Vector3Utils.toVector2(new Vector3(0, 0, 1) * transform_matrix);
        Vector2 point_b = Vector3Utils.toVector2(new Vector3(0, 1, 1) * transform_matrix);
        Vector2 point_c = Vector3Utils.toVector2(new Vector3(1, 0, 1) * transform_matrix);

        Vector2 point_d = Vector3Utils.toVector2(new Vector3(0.5f, 0.5f, 1) * transform_matrix);

        uint vertexBaseIndex = MeshPolygonID * 5;
        uint vertexKeyA = RootID;
        uint vertexKeyB = ((RootID >> 1) ^ 1) + ((RootID & 1) << 1);

        Vector3 base_Triangle_a = Vector3Utils.toVector3(position_list[vertexBaseIndex + vertexKeyA + 1]);
        Vector3 base_Triangle_b = Vector3Utils.toVector3(position_list[vertexBaseIndex + vertexKeyB + 1]);
        Vector3 base_Triangle_c = Vector3Utils.toVector3(position_list[vertexBaseIndex]);

        Vector3 point_A = localPointToWorldPoint(point_a, base_Triangle_a, base_Triangle_b, base_Triangle_c);
        Vector3 point_B = localPointToWorldPoint(point_b, base_Triangle_a, base_Triangle_b, base_Triangle_c);
        Vector3 point_C = localPointToWorldPoint(point_c, base_Triangle_a, base_Triangle_b, base_Triangle_c);
        Vector3 point_D = localPointToWorldPoint(point_d, base_Triangle_a, base_Triangle_b, base_Triangle_c);

        Triangle t = new Triangle(new Vector3[] {
            (!isCube ? QuadTree.QuadTreeNode.PointOnCubeToPointOnSphere(point_A) : point_A) * radius,
            (!isCube ? QuadTree.QuadTreeNode.PointOnCubeToPointOnSphere(point_B) : point_B) * radius,
            (!isCube ? QuadTree.QuadTreeNode.PointOnCubeToPointOnSphere(point_C) : point_C) * radius
        }, origin
        );
        t.spawnPoint = point_D * radius;
        // GD.Print(point_a);
        // GD.Print(point_b);
        // GD.Print(point_c);
        // GD.Print(this);
        // GD.Print();
        return t;
    }

    public static Vector3 localPointToWorldPoint(Vector2 point, Vector3 vertexA, Vector3 vertexB, Vector3 vertexC)
    {
        return vertexA * point.X + vertexB * point.Y + vertexC * (1 - point.X - point.Y);
    }

    public override string ToString()
    {

        return $"{Convert.ToString(MSB, 2).PadZeros(32)}, {Convert.ToString(LSB, 2).PadZeros(32)}, {MeshPolygonID}, {RootID}";
    }
}