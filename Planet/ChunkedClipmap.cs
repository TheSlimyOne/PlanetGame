using System;
using System.Collections.Generic;
using Godot;
using Planet;

public class ChunkedClipmap
{
    public int DesiredChunkSize { get; private set; }
    public int CenterSize { get; private set; }
    public int BorderSize { get; private set; }
    public string ImagePath { get; private set; }
    
    public Vector2I ImageSize { get; private set; }
    public int TotalSubdivisions { get; private set; }


    public ChunkedClipmap(int desiredChunkSize, int centerSize, int borderSize, string imagePath)
    {
        DesiredChunkSize = desiredChunkSize;
        CenterSize = centerSize;
        BorderSize = borderSize;
        ImagePath = imagePath;

        Image image = LoadImage();
        ImageSize = image.GetSize();
        
        (DesiredChunkSize, TotalSubdivisions) = GetRatio(desiredChunkSize, ImageSize.X);
        if (DesiredChunkSize != desiredChunkSize)
            GD.PushWarning($"Image does not scale down evenly to chunk size of {desiredChunkSize}. Falling back to size of {DesiredChunkSize}.");
        
    }

    public void GenerateImageChunks(string destination)
    {
        Image image = LoadImage();

        if (image.IsCompressed())
            image.Decompress();
        if (!image.HasMipmaps())
            image.GenerateMipmaps();
        if (image.GetFormat() != Image.Format.Rgba8)
            image.Convert(Image.Format.Rgba8);

        Image[] mipmaps = GetMipmaps(image, DesiredChunkSize);
        int centerChunkSize = DesiredChunkSize;
        int borderPixelSize = DesiredChunkSize / CenterSize * BorderSize;

        GD.Print($"Grid size should be {2 * BorderSize + CenterSize}");

        for (int i = 0; i < mipmaps.Length; i++)
        {
            Vector2I mipSize = mipmaps[i].GetSize();
            for (int j = 0; j < mipSize.X; j += centerChunkSize)
            {
                for (int k = 0; k < mipSize.Y; k += centerChunkSize)
                {
                    int fullSize = centerChunkSize + 2 * borderPixelSize;
                    Image chunk = Image.CreateEmpty(fullSize, fullSize, false, image.GetFormat());
                    chunk.Fill(new Color(0, 0, 0, 0));
                    Rect2I chunkDim = new(j, k, centerChunkSize, centerChunkSize);
                    chunk.BlitRect(mipmaps[i], chunkDim, new Vector2I(borderPixelSize, borderPixelSize));

                    // Image leftSection = Image.CreateEmpty(borderPixelSize, fullSize, false, image.GetFormat());
                    // Image rightSection = Image.CreateEmpty(borderPixelSize, fullSize, false, image.GetFormat());
                    // Image downSection = Image.CreateEmpty(CenterSize * borderPixelSize, borderPixelSize, false, image.GetFormat());
                    // Image upSection = Image.CreateEmpty(CenterSize * borderPixelSize, borderPixelSize, false, image.GetFormat());

                    // Rect2I leftSectionChunkDim = new(j - borderPixelSize, k - borderPixelSize, borderPixelSize, fullSize);
                    // Rect2I rightSectionChunkDim = new(j + centerChunkSize, k - borderPixelSize, borderPixelSize, fullSize);
                    // Rect2I downSectionChunkDim = new(j, k + centerChunkSize,  CenterSize * borderPixelSize, borderPixelSize);
                    // Rect2I upSectionChunkDim = new(j, k - borderPixelSize,  CenterSize * borderPixelSize, borderPixelSize);

                    // leftSection.BlitRect(mipmaps[i], leftSectionChunkDim, new Vector2I(0, 0));
                    // rightSection.BlitRect(mipmaps[i], rightSectionChunkDim, new Vector2I(0, 0));
                    // downSection.BlitRect(mipmaps[i], downSectionChunkDim, new Vector2I(0, 0));
                    // upSection.BlitRect(mipmaps[i], upSectionChunkDim, new Vector2I(0, 0));

                    // chunk.BlitRect(leftSection, new Rect2I(0, 0, leftSection.GetSize()), new Vector2I(0, 0));
                    // chunk.BlitRect(rightSection, new Rect2I(0, 0, rightSection.GetSize()), new Vector2I(centerChunkSize + borderPixelSize, 0));
                    // chunk.BlitRect(downSection, new Rect2I(0, 0, downSection.GetSize()), new Vector2I(borderPixelSize, centerChunkSize + borderPixelSize));
                    // chunk.BlitRect(upSection, new Rect2I(0, 0, upSection.GetSize()), new Vector2I(borderPixelSize, 0));

                    chunk.SavePng($"res://mips/{destination}/{i}-{j}{k}.png");
                }
            }
        }


    }

    private static Image[] GetMipmaps(Image image, int target)
    {
        int closestNumber = image.GetSize().Y;
        int closestDifference = Math.Abs(target - closestNumber);
        int mipMapCount = image.GetMipmapCount();

        int bytesPerPixel = FormatConverter.GetBytes(image.GetFormat());
        List<Image> mipmaps = new();

        // Iterate through the mip maps to find the mip level closest to target
        for (int i = 0; i < mipMapCount; i++)
        {
            int width = image.GetSize().X / (int)Mathf.Pow(2, i);
            int height = image.GetSize().Y / (int)Mathf.Pow(2, i);
            int difference = Math.Abs(target - height);

            if (difference <= closestDifference)
            {
                closestNumber = height;
                closestDifference = difference;
            }
            else
                break;

            int mipOffset = (int)image.GetMipmapOffset(i);
            byte[] buffer = new byte[bytesPerPixel * width * height];
            Array.Copy(image.GetData(), mipOffset, buffer, 0, buffer.Length);

            Image mip = Image.CreateFromData(width, height, false, image.GetFormat(), buffer);
            mipmaps.Add(mip);
        }
        return mipmaps.ToArray();
    }

    public static (int, int) GetRatio(int desiredChunkSize, int imageWidth)
    {
        int amount = 0;
        while(true)
        {
            int current =  imageWidth / (int)Mathf.Pow(2, amount);
            if (current <= desiredChunkSize)
                break;
            amount++;
        }
        return (imageWidth / (int)Mathf.Pow(2, amount), amount);
    }

    private Image LoadImage()
    {
        Image image = new();
        Error error = image.Load(ImagePath); // Possible issue TODO
        if (error != Error.Ok)
        {
            GD.PrintErr($"Failed to load image: {error}");
            return null;
        }
        return image;
    }
}