using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using Planet;

// TODO check if image supplied is 2:1

public class ChunkedClipmapGenerator
{
    public int DesiredChunkSize { get; private set; }
    public int CenterSize { get; private set; }
    public int BorderSize { get; private set; }
    public string ImagePath { get; private set; }

    public Vector2I ImageSize { get; private set; }
    public int TotalSubdivisions { get; private set; }

    public ChunkedClipmapGenerator(int desiredChunkSize, int centerSize, int borderSize, string squareImagePath)
    {
        DesiredChunkSize = desiredChunkSize;
        CenterSize = centerSize;
        BorderSize = borderSize;
        ImagePath = squareImagePath;

        Image image = LoadImage();
        ImageSize = image.GetSize();

        TotalSubdivisions = (int)(Mathf.Log(ImageSize.X / DesiredChunkSize) / Mathf.Log(2));
        // Image[] images = GetMipmaps(DesiredChunkSize); 
        // for(int i = 0; i < images.Length; i++)
        // {
        //     images[i].SavePng($"user://test/mips/{i}.png");
        // }
    }

    // TODO prop should multi thread this
    // public void GenerateImageChunks(string destination)
    // {
    //     Image image = LoadImage();

    //     if (image.IsCompressed())
    //         image.Decompress();
    //     if (!image.HasMipmaps())
    //         image.GenerateMipmaps();
    //     if (image.GetFormat() != Image.Format.Rgba8)
    //         image.Convert(Image.Format.Rgba8);

    //     Image[] mipmaps = GetMipmaps(image, DesiredChunkSize);
    //     int centerChunkSize = DesiredChunkSize;
    //     int borderPixelSize = DesiredChunkSize / CenterSize * BorderSize;

    //     GD.Print($"Grid size should be {2 * BorderSize + CenterSize}");

    //     for (int mipIndex = 0; mipIndex < mipmaps.Length; mipIndex++)
    //     {
    //         Vector2I mipSize = mipmaps[mipIndex].GetSize();
    //         for (int y = 0; y < mipSize.Y; y += centerChunkSize)
    //         {
    //             for (int x = 0; x < mipSize.X; x += centerChunkSize)
    //             {
    //                 int fullSize = centerChunkSize + 2 * borderPixelSize;
    //                 Image chunk = Image.CreateEmpty(fullSize, fullSize, false, image.GetFormat());
    //                 chunk.Fill(new Color(0, 0, 0, 0));
    //                 Rect2I chunkDim = new(x, y, centerChunkSize, centerChunkSize);
    //                 chunk.BlitRect(mipmaps[mipIndex], chunkDim, new Vector2I(borderPixelSize, borderPixelSize));

    //                 if (borderPixelSize > 0)
    //                 {
    //                     Image leftSection = Image.CreateEmpty(borderPixelSize, fullSize, false, image.GetFormat());
    //                     Image rightSection = Image.CreateEmpty(borderPixelSize, fullSize, false, image.GetFormat());
    //                     Image downSection = Image.CreateEmpty(CenterSize * borderPixelSize, borderPixelSize, false, image.GetFormat());
    //                     Image upSection = Image.CreateEmpty(CenterSize * borderPixelSize, borderPixelSize, false, image.GetFormat());

    //                     Rect2I leftSectionChunkDim = new(x - borderPixelSize, y - borderPixelSize, borderPixelSize, fullSize);
    //                     Rect2I rightSectionChunkDim = new(x + centerChunkSize, y - borderPixelSize, borderPixelSize, fullSize);
    //                     Rect2I downSectionChunkDim = new(x, y + centerChunkSize, CenterSize * borderPixelSize, borderPixelSize);
    //                     Rect2I upSectionChunkDim = new(x, y - borderPixelSize, CenterSize * borderPixelSize, borderPixelSize);

    //                     leftSection.BlitRect(mipmaps[mipIndex], leftSectionChunkDim, new Vector2I(0, 0));
    //                     rightSection.BlitRect(mipmaps[mipIndex], rightSectionChunkDim, new Vector2I(0, 0));
    //                     downSection.BlitRect(mipmaps[mipIndex], downSectionChunkDim, new Vector2I(0, 0));
    //                     upSection.BlitRect(mipmaps[mipIndex], upSectionChunkDim, new Vector2I(0, 0));

    //                     chunk.BlitRect(leftSection, new Rect2I(0, 0, leftSection.GetSize()), new Vector2I(0, 0));
    //                     chunk.BlitRect(rightSection, new Rect2I(0, 0, rightSection.GetSize()), new Vector2I(centerChunkSize + borderPixelSize, 0));
    //                     chunk.BlitRect(downSection, new Rect2I(0, 0, downSection.GetSize()), new Vector2I(borderPixelSize, centerChunkSize + borderPixelSize));
    //                     chunk.BlitRect(upSection, new Rect2I(0, 0, upSection.GetSize()), new Vector2I(borderPixelSize, 0));
    //                 }


    //                 string directory = $"res://mips/{destination}/{mipIndex}-{x / centerChunkSize}-{y / centerChunkSize}.png";
    //                 chunk.SavePng(directory);
    //             }
    //         }
    //     }
    // }

    private Image[] GetMipmaps(int target)
    {
        Image image = LoadImage();
        if (image.IsCompressed())
            image.Decompress();
        if (!image.HasMipmaps())
            image.GenerateMipmaps();

        int bytesPerPixel = FormatConverter.GetBytes(image.GetFormat());
        List<Image> mipmaps = new();
  
        for (int i = 0; i < image.GetMipmapCount(); i++)
        {
            int size = image.GetSize().X / (int)Mathf.Pow(2, i);

            int mipOffset = (int)image.GetMipmapOffset(i);
            byte[] buffer = new byte[bytesPerPixel * size * size];
            Array.Copy(image.GetData(), mipOffset, buffer, 0, buffer.Length);

            Image mip = Image.CreateFromData(size, size, false, image.GetFormat(), buffer);
            mipmaps.Add(mip);

            if (size == target)
                break;
        }

        return mipmaps.ToArray();
    }



    private Image LoadImage()
    {
        Image image = new();

        Error error = image.Load(ImagePath); // Possible issue TODO
        if (error != Error.Ok)
        {
            GD.PrintErr($"Failed to load image: {error}");
        }

        return image;
    }


    public Image LoadTile(string path)
    {
        Image image = Image.LoadFromFile(path);
        image.Convert(Image.Format.Rgbaf);
        return image;
    }
}