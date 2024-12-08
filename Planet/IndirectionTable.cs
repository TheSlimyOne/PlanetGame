using System;
using System.Linq;
using Godot;
using Planet;
using Uniform;

public class IndirectionTable
{
    public Image[] Table { get; private set; }
    public int GridSize { get; private set; } 
    public int MipDepth { get; private set; }
    public int TotalCells { get; private set; }

    public IndirectionTable(RenderingDevice rd, int gridSize, int mipDepth)
    {
        //TODO make sure gridSize is a power of 2?
        GD.Print($"Creating 6 sets {mipDepth} textures, with size of {gridSize}");
        GridSize = gridSize;
        MipDepth = mipDepth;
        Table = new Image[6 * MipDepth];
        TotalCells = 0;
         
        for (int pageIndex = 0; pageIndex < 6; pageIndex++)
        {
            for (int mipIndex = 0; mipIndex < mipDepth; mipIndex++)
            {
                Table[mipDepth * pageIndex + (mipDepth - mipIndex - 1)] = Image.CreateEmpty(gridSize, gridSize, false, Image.Format.Rgbf);
                Table[mipDepth * pageIndex + (mipDepth - mipIndex - 1)].Fill(new Color(-1.0f, -1.0f, -1.0f));
                TotalCells += (int)Mathf.Pow(2, 2 * mipIndex + 2);
            }
        }
        GD.Print($"Total Cells of {TotalCells}");
        
    }

    public Color GetDebugColor(Vector3 normal)
    {
        if (normal.X == -1 || normal.Y == -1 || normal.Z == -1)
            normal = VectorUtils.GenerateVectorExclusionMaskFrom(normal);

        return new Color(normal.X, normal.Y, normal.Z);
    }


    public Texture2DArray ToTexture2DArray()
    {
        Godot.Collections.Array<Image> images = new(Table);
        Texture2DArray texture2Darray = new();
        texture2Darray.CreateFromImages(images);
        return texture2Darray;
    }
}

