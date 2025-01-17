using System;
using System.Linq;
using Godot;
using Planet;
using Uniform;

public class IndirectionTable
{
    public Image[] Table { get; private set; }
    public Texture2Drd[] newTable;
    public int GridSize { get; private set; }
    public int MipDepth { get; private set; }

    public IndirectionTable(Node node, RenderingDevice rd, int gridSize, int mipDepth)
    {
        Random random = new Random(1207);
        //TODO make sure gridSize is a power of 2?
        GD.Print($"Creating 6 sets {mipDepth} textures, with size of {gridSize}");
        GridSize = gridSize;
        MipDepth = mipDepth;

        Table = new Image[6 * MipDepth];

        // Rid tableRid = rd.TextureCreate(
        //     new RDTextureFormat() {
        //         Format = RenderingDevice.DataFormat.R32G32Sfloat,
        //         Width = (uint)GridSize,
        //         Height = (uint)GridSize,
        //         Depth = 1,
        //         ArrayLayers = (uint)(6 * MipDepth),
        //         Mipmaps = 1,
        //         TextureType = RenderingDevice.TextureType.Type2DArray,
        //         UsageBits = RenderingDevice.TextureUsageBits.StorageBit | RenderingDevice.TextureUsageBits.CanCopyFromBit | RenderingDevice.TextureUsageBits.CanUpdateBit | RenderingDevice.TextureUsageBits.SamplingBit
        //     },
        //     new RDTextureView()
        // );

        // GD.Print(rd.TextureIsValid(tableRid));

        newTable = new Texture2Drd[6 * MipDepth]; // { TextureRdRid = tableRid };

        // Rid newTableRid = RenderingServer.TextureGetRdTexture(newTable.GetRid());

        Window scene = GD.Load<PackedScene>("res://Scenes/window.tscn").Instantiate<Window>();
        node.AddChild(scene);

        for (int pageIndex = 0; pageIndex < 6; pageIndex++)
        {
            GridContainer grid = new() { Columns = mipDepth };
            for (int mipIndex = 0; mipIndex < mipDepth; mipIndex++)
            {
                Rid imageRid = rd.TextureCreate(
                    new RDTextureFormat()
                    {
                        Format = RenderingDevice.DataFormat.R32G32Sfloat,
                        Width = (uint)GridSize,
                        Height = (uint)GridSize,
                        ArrayLayers = 0,
                        Depth = 1,
                        Mipmaps = 1,
                        TextureType = RenderingDevice.TextureType.Type2D,
                        UsageBits = RenderingDevice.TextureUsageBits.StorageBit | RenderingDevice.TextureUsageBits.CanCopyFromBit | RenderingDevice.TextureUsageBits.CanUpdateBit | RenderingDevice.TextureUsageBits.SamplingBit
                    },
                    new RDTextureView()
                );

                uint index = (uint)(mipDepth * pageIndex + (mipDepth - mipIndex - 1));
                newTable[index] = new Texture2Drd() { TextureRdRid = imageRid };
                grid.AddChild(new TextureRect() { Texture = newTable[index] });
            }
            scene.GetChild(0).AddChild(grid);
        }
        GD.Print($"Total textures {Table.Length}");

    }

    public Color GetDebugColor(Vector3 normal)
    {
        if (normal.X == -1 || normal.Y == -1 || normal.Z == -1)
            normal = VectorUtils.GenerateVectorExclusionMaskFrom(normal);

        return new Color(normal.X, normal.Y, normal.Z);
    }


    // public Texture2DArray ToTexture2DArray()
    // {
    //     Godot.Collections.Array<Image> images = new(Table);
    //     Texture2DArray texture2Darray = new();
    //     texture2Darray.CreateFromImages(images);
    //     return texture2Darray;
    // }
}

