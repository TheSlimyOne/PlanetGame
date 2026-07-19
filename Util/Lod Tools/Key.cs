using Godot;
using PlanetGame.Util;
using System;

public readonly struct Key(uint msb, uint lsb, uint meshPolygonID, uint meshData)
{
    readonly public uint MSB = msb;
    readonly public uint LSB = lsb;
    readonly public uint MeshPolygonID = meshPolygonID;
    readonly public uint MeshData = meshData; // FFF0000000000000000000000000RRRR

    readonly public uint Flags => MeshData >> 29;
    readonly public uint RootID => MeshData & 0xF;


    public Key(Vector4I vector4) : this(BitConverter.SingleToUInt32Bits(vector4.X), BitConverter.SingleToUInt32Bits(vector4.Y), BitConverter.SingleToUInt32Bits(vector4.Z), BitConverter.SingleToUInt32Bits(vector4.W)) { }
    public Key(Color color) : this(color.R, color.G, color.B, color.A) { }
    public Key(float msb, float lsb, float meshPolygonID, float meshData) : this(BitConverter.SingleToUInt32Bits(msb), BitConverter.SingleToUInt32Bits(lsb), BitConverter.SingleToUInt32Bits(meshPolygonID), BitConverter.SingleToUInt32Bits(meshData)) { }
    public Key(int msb, int lsb, int meshPolygonID, int meshData) : this((uint)msb, (uint)lsb, (uint)meshPolygonID, (uint)meshData) { }

    public readonly uint[] GetNodeID()
    {
        return [MSB, LSB];
    }

    public readonly Color ToColor()
    {
        return new Color(
            BitConverter.UInt32BitsToSingle(MSB),
            BitConverter.UInt32BitsToSingle(LSB),
            BitConverter.UInt32BitsToSingle(MeshPolygonID),
            BitConverter.UInt32BitsToSingle(MeshData)
        );
    }

    public readonly float[] ToSingles()
    {
        return [
            BitConverter.UInt32BitsToSingle(MSB),
            BitConverter.UInt32BitsToSingle(LSB),
            BitConverter.UInt32BitsToSingle(MeshPolygonID),
            BitConverter.UInt32BitsToSingle(MeshData)
        ];
    }

    public readonly int GetLevelInKey() => FindMSB64() / 2;

    public readonly int FindMSB64() => (MSB == 0) ? FindMSB(LSB) : (FindMSB(MSB) + 32);

    public readonly float GetScale() => Mathf.Pow(0.5f, FindMSB64() / 2f);

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

    public readonly uint GetBranching(int level) => RightShift64(GetNodeID(), FindMSB64() - 2 - (level * 2))[1] & 0x3;

    public static Vector2 GetTranslation(uint b1) => new(b1 & 0x1, b1 ^ 0x1);

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

    public static Vector2 Rotate(int rotationIndex, Vector2 translation)
    {
        Vector2I trig = QuickPI2(rotationIndex);
        Vector2 r = new(
            trig.X * translation.X - trig.Y * translation.Y,
            trig.Y * translation.X + trig.X * translation.Y);

        return r;
    }

    public static int GetRotation(uint b1b2)
    {
        uint b1 = b1b2 >> 1;
        uint b2 = b1b2 & 1;

        uint a = b1b2 ^ 0x2;
        uint b = a | 0x1;
        uint c = b1 ^ b2;
        return (int)(b * c);
    }

    public static Vector2I QuickPI2(int a)
    {
        int b = a & 3;
        int b1 = b >> 1;
        int b2 = b & 1;
        int bn2 = b2 ^ 1;
        int c = bn2 - (2 * (b1 & bn2));
        int s = b2 - (2 * (b1 & b2));
        return new Vector2I(c, s);
    }

    public readonly Vector4 GetTransformation()
    {
        Vector2 translation = new(0, 0);
        Vector2 temp;
        int theta = 0;
        float scale = 1.0f;

        for (int i = 0; i < FindMSB64() / 2; i++)
        {
            uint b1b2 = GetBranching(i);
            uint b1 = b1b2 >> 1;

            temp = scale * GetTranslation(b1) * 0.5f;

            translation += Rotate(theta, temp);
            theta += GetRotation(b1b2);
            scale *= 0.5f;
        }

        return new Vector4(theta, scale, translation.X, translation.Y);
    }


    public enum Normal
    {
        RIGHT, LEFT, UP, DOWN, BACK, FORWARD
    }

    public static readonly Vector3[] Normals = [
        Vector3.Right,
        Vector3.Left,
        Vector3.Up,
        Vector3.Down,
        Vector3.Back,
        Vector3.Forward
    ];

    public readonly Vector2 GetQuadtreeSpacePoint(Vector2 point) => VectorUtils.toVector2(VectorUtils.ToVector3(point, 1) * LeafSpaceToQuadtreeSpace());

    public Vector3 GetPolygonSpacePoint(Vector2 point, Image baseVertices, Image baseIndices) => QuadtreeSpaceToPolygonSpace(baseVertices, baseIndices) * VectorUtils.ToVector3(point, 1);

    public readonly Basis LeafSpaceToQuadtreeSpace()
    {
        int msb = FindMSB64();

        Vector2 translation = new(0, 0);
        Vector2 temp;
        int theta = 0;
        float scale = 1.0f;

        for (int i = 0; i < msb / 2; i++)
        {
            uint b1b2 = GetBranching(i);
            uint b1 = b1b2 >> 1;

            temp = scale * GetTranslation(b1) * 0.5f;

            translation += Rotate(theta, temp);
            theta += GetRotation(b1b2);
            scale *= 0.5f;
        }

        Vector2I trig = QuickPI2(theta);
        Basis transform_matrix = new(
            new Vector3(trig.X * scale, -trig.Y * scale, translation.X),
            new Vector3(trig.Y * scale, trig.X * scale, translation.Y),
            new Vector3(0.0f, 0.0f, 1.0f)
        );

        return transform_matrix;
    }

    public Basis QuadtreeSpaceToPolygonSpace(Image baseVertices, Image baseIndices)
    {
        Vector3[] basePrimitives = GetBasePrimitives(baseVertices, baseIndices);
        Vector3 polygonVertextA = basePrimitives[(RootID + 2) % 3];
        Vector3 polygonVertextB = basePrimitives[(RootID + 1) % 3];
        Vector3 polygonVertextC = basePrimitives[RootID % 3];

        return new Basis(
            polygonVertextC - polygonVertextB,
            polygonVertextA - polygonVertextB,
            polygonVertextB
        );
    }

    public readonly Vector3[] GetBasePrimitives(Image baseVertices, Image baseIndices)
    {
        int baseIndex = (int)MeshPolygonID * 3;
        int i0 = (int)baseIndices.GetPixel(baseIndex + 0, 0).R;
        int i1 = (int)baseIndices.GetPixel(baseIndex + 1, 0).R;
        int i2 = (int)baseIndices.GetPixel(baseIndex + 2, 0).R;


        Vector3[] triangle = [
            VectorUtils.ToVector3(VectorUtils.ToVector4(baseVertices.GetPixel(i0, 0))),
            VectorUtils.ToVector3(VectorUtils.ToVector4(baseVertices.GetPixel(i1, 0))),
            VectorUtils.ToVector3(VectorUtils.ToVector4(baseVertices.GetPixel(i2, 0)))
        ];
        return triangle;
    }

    public readonly Color Colorize()
    {
        int msb = GetLevelInKey();
        int a = (msb & 1) != 0 ? 1 : 0;
        int b = (msb & 2) != 0 ? 1 : 0;
        int c = (msb & 4) != 0 ? 1 : 0;
        return new Color(a, b, c);
    }

    // TODO
    public Mesh CreateTriangle(Transform3D origin, Image baseVertices, Image baseIndices, bool isCube = false)
    {
        Vector2 point_a = GetQuadtreeSpacePoint(new(0, 0));
        Vector2 point_b = GetQuadtreeSpacePoint(new(1, 0));
        Vector2 point_c = GetQuadtreeSpacePoint(new(0, 1));

        Vector3 base_Triangle_a = GetPolygonSpacePoint(point_a, baseVertices, baseIndices);
        Vector3 base_Triangle_b = GetPolygonSpacePoint(point_b, baseVertices, baseIndices);
        Vector3 base_Triangle_c = GetPolygonSpacePoint(point_c, baseVertices, baseIndices);

        // base_Triangle_a = isCube ? base_Triangle_a : VectorUtils.PointOnCubeToPointOnSphere(base_Triangle_a);
        // base_Triangle_b = isCube ? base_Triangle_b : VectorUtils.PointOnCubeToPointOnSphere(base_Triangle_b);
        // base_Triangle_c = isCube ? base_Triangle_c : VectorUtils.PointOnCubeToPointOnSphere(base_Triangle_c);
        // GD.Print()

        Vector3 normal = VectorUtils.GetTriangularNormal([base_Triangle_a, base_Triangle_b, base_Triangle_c]);

        Godot.Collections.Array arrays = [];
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = new Vector3[] { base_Triangle_a, base_Triangle_b, base_Triangle_c };
        arrays[(int)Mesh.ArrayType.Index] = new int[] { 0, 1, 2 };
        arrays[(int)Mesh.ArrayType.Normal] = new Vector3[] { normal, normal, normal };

        ArrayMesh triangle = new();
        triangle.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return triangle;
    }

    public static Vector3 LocalPointToWorldPoint(Vector2 point, Vector3 vertexA, Vector3 vertexB, Vector3 vertexC) => vertexA * point.X + vertexB * point.Y + vertexC * (1 - point.X - point.Y);

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

    public static ArrayMesh GetTriangleMesh(int resolution)
    {
        Vector3[] vertices = new Vector3[resolution * (resolution + 1) / 2];
        Vector3[] normals = new Vector3[resolution * (resolution + 1) / 2];
        int[] triangles = new int[(resolution - 1) * (resolution - 1) * 6 / 2];

        Vector3 normal = Vector3.Back;
        Vector3 axisA = new(normal.Y, normal.Z, normal.X);
        Vector3 axisB = normal.Cross(axisA).Abs();
        int triIndex = 0;
        int vertexIndex = 0;
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution - y; x++)
            {
                int currentIndex = vertexIndex++;
                Vector2 percentage = new Vector2(x, y) / (resolution - 1);
                vertices[currentIndex] = normal + (percentage.X * axisA + percentage.Y * axisB);
                normals[currentIndex] = normal;

                // GD.Print(vertices[currentIndex]);

                if (x != resolution - y - 1)
                {
                    if (x == resolution - y - 2)
                    {
                        triangles[triIndex++] = currentIndex;
                        triangles[triIndex++] = currentIndex + 1;
                        triangles[triIndex++] = currentIndex + resolution - y;
                    }
                    else
                    {
                        bool isXEven = x % 2 == 0;
                        bool isYEven = y % 2 == 0;

                        if ((isXEven && isYEven) || (!isXEven && !isYEven))
                        {
                            triangles[triIndex++] = currentIndex;
                            triangles[triIndex++] = currentIndex + resolution - y + 1;
                            triangles[triIndex++] = currentIndex + resolution - y;
                            triangles[triIndex++] = currentIndex;
                            triangles[triIndex++] = currentIndex + 1;
                            triangles[triIndex++] = currentIndex + resolution - y + 1;
                        }
                        else
                        {
                            triangles[triIndex++] = currentIndex;
                            triangles[triIndex++] = currentIndex + 1;
                            triangles[triIndex++] = currentIndex + resolution - y;
                            triangles[triIndex++] = currentIndex + 1;
                            triangles[triIndex++] = currentIndex + resolution - y + 1;
                            triangles[triIndex++] = currentIndex + resolution - y;
                        }
                    }
                }
            }
        }


        // GD.Print(FormatForDesmos(vertices, triangles));
    
        Godot.Collections.Array arrays = [];
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Index] = triangles;
        arrays[(int)Mesh.ArrayType.Normal] = normals;

        ArrayMesh triangleMesh = new();
        triangleMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return triangleMesh;
    }

    public static string FormatForDesmos(Vector3[] vertices, int[] triangles)
    {
        string s = "[";
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector2 A = VectorUtils.toVector2(vertices[triangles[i + 0]]);
            Vector2 B = VectorUtils.toVector2(vertices[triangles[i + 1]]);
            Vector2 C = VectorUtils.toVector2(vertices[triangles[i + 2]]);
            s += $"polygon({A}, {B}, {C}), ";
        }
        s = s[..^2] + "]";
        return s;
    }

    public override readonly string ToString() => $"{Convert.ToString(MSB, 2).PadZeros(32)}, {Convert.ToString(LSB, 2).PadZeros(32)}, {MeshPolygonID}, {RootID}, {Convert.ToString(Flags, 2).PadZeros(3)}";
}