using System;
using Godot;
using Planet;
using Uniform;

public class IndirectionTable
{
    public Texture2DArray Table { get; private set; }

    public IndirectionTable(RenderingDevice rd, int gridSize, int mipDepth)
    {
        Godot.Collections.Array<Image> images = new();
        Color[] colors = new Color[]
        {
            Colors.Red, Colors.DarkRed, Colors.IndianRed, 
            Colors.Green, Colors.DarkGreen, Colors.LightGreen, 
            Colors.Blue, Colors.DarkBlue, Colors.SkyBlue,
            Colors.Red, Colors.DarkRed, Colors.IndianRed,
            Colors.Green, Colors.DarkGreen, Colors.LightGreen, 
            Colors.Blue, Colors.DarkBlue, Colors.SkyBlue,

         
        };
        Random random = new(12);
        GD.Print($"Creating 6 sets {mipDepth} textures");
        for (int i = 0; i < 6; i++)
        {

            for (int j = 0; j < mipDepth; j++)
            {
                images.Add(Image.CreateEmpty(gridSize, gridSize, false, Image.Format.Rgbaf));
                // for (int j = 0; j < gridSize; j++)
                // {
                //     for (int k = 0; k < gridSize; k++)
                //     {
                //         images[i].SetPixel(j, k, new Color(i / (float)mipDepth, random.NextSingle(), random.NextSingle()));
                //     }
                // }
                images[3 * i + j].Fill(colors[3 * i + j]);
            }
        }
        Table = new();
        Table.CreateFromImages(images);
    }

    public void UpdateTable()
    {

    }

}