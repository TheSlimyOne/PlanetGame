using System;
using Godot;

public static class Sampler
{
    public static Color SampleNearest(Image image, float u, float v)
    {
        int width = image.GetWidth();
        int height = image.GetHeight();

        int x = Mathf.Clamp(Mathf.RoundToInt(u * (width - 1)), 0, width - 1);
        int y = Mathf.Clamp(Mathf.RoundToInt(v * (height - 1)), 0, height - 1);

        return image.GetPixel(x, y);
    }

    public static Color SampleBilinear(Image image, float u, float v)
    {
        int width = image.GetWidth();
        int height = image.GetHeight();

        u = Mathf.Clamp(u, 0, 1);
        v = Mathf.Clamp(v, 0, 1);

        float x = u * (width - 1);
        float y = v * (height - 1);

        int x0 = Mathf.FloorToInt(x);
        int y0 = Mathf.FloorToInt(y);
        int x1 = Mathf.Min(x0 + 1, width - 1);
        int y1 = Mathf.Min(y0 + 1, height - 1);

        float tx = x - x0;
        float ty = y - y0;

        Color c00 = image.GetPixel(x0, y0);
        Color c10 = image.GetPixel(x1, y0);
        Color c01 = image.GetPixel(x0, y1);
        Color c11 = image.GetPixel(x1, y1);

        Color cx0 = c00.Lerp(c10, tx);
        Color cx1 = c01.Lerp(c11, tx);
        return cx0.Lerp(cx1, ty);
    }

    public static Color SampleBicubic(Image image, float u, float v)
    {
        int width = image.GetWidth();
        int height = image.GetHeight();

        float x = u * (width - 1);
        float y = v * (height - 1);

        int xInt = Mathf.FloorToInt(x);
        int yInt = Mathf.FloorToInt(y);

        float tx = x - xInt;
        float ty = y - yInt;

        static Color BicubicInterpolate(float t, Color c0, Color c1, Color c2, Color c3)
        {
            Color a0 = c3 - c2 - c0 + c1;
            Color a1 = c0 - c1 - a0;
            Color a2 = c2 - c0;
            Color a3 = c1;

            return ((a0 * t + a1) * t + a2) * t + a3;
        }

        Color[] samples = new Color[4 * 4];
        for (int j = -1; j <= 2; j++)
        {
            for (int i = -1; i <= 2; i++)
            {
                int xi = Mathf.Clamp(xInt + i, 0, width - 1);
                int yj = Mathf.Clamp(yInt + j, 0, height - 1);
                samples[(j + 1) * 4 + (i + 1)] = image.GetPixel(xi, yj);
            }
        }

        Color[] col = new Color[4];
        for (int j = 0; j < 4; j++)
        {
            col[j] = BicubicInterpolate(tx, samples[j * 4 + 0], samples[j * 4 + 1], samples[j * 4 + 2], samples[j * 4 + 3]);
        }

        return BicubicInterpolate(ty, col[0], col[1], col[2], col[3]);
    }

    public static Color SampleTrilinear(Image image, float u, float v, float mip)
    {
        int mip0 = Mathf.Clamp((int)Mathf.Floor(mip), 0, image.GetMipmapCount() - 1);
        int mip1 = Mathf.Min(mip0 + 1, image.GetMipmapCount() - 1);
        float t = mip - mip0;

        Image mipImage0 = GetMipImage(image, mip0);
        Image mipImage1 = GetMipImage(image, mip1);

        Color c0 = SampleBilinear(mipImage0, u, v);
        Color c1 = SampleBilinear(mipImage1, u, v);

        return c0.Lerp(c1, t);
    }

    public static Image GetMipImage(Image original, int mipIndex)
    {
        if (!original.HasMipmaps())
            original.GenerateMipmaps();

        int baseWidth = original.GetWidth();
        int baseHeight = original.GetHeight();
        int mipWidth = Mathf.Max(1, baseWidth >> mipIndex);
        int mipHeight = Mathf.Max(1, baseHeight >> mipIndex);

        int bytesPerPixel = FormatConverter.GetBytes(original.GetFormat());
        int mipOffset = (int)original.GetMipmapOffset(mipIndex);
        byte[] data = original.GetData();

        byte[] mipData = new byte[mipWidth * mipHeight * bytesPerPixel];
        Array.Copy(data, mipOffset, mipData, 0, mipData.Length);

        Image mipImage = Image.CreateFromData(mipWidth, mipHeight, false, original.GetFormat(), mipData);

        original.ClearMipmaps();
        return mipImage;
    }

}
