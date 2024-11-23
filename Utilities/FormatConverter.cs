using Godot;
using System;

public static class FormatConverter
{
    public static int GetBytes(Image.Format format)
    {
        return format switch
        {
            Image.Format.Rgba8 => 4,    // 4 channels (RGBA) * 1 byte per channel = 4 bytes
            Image.Format.Rgb8 => 3,     // 3 channels (RGB) * 1 byte per channel = 3 bytes
            Image.Format.R8 => 1,       // 1 channel (R) * 1 byte = 1 byte
            Image.Format.Rgbaf => 16,   // 4 channels (RGBA) * 4 bytes per channel = 16 bytes
            _ => throw new NotImplementedException($"Unknown format: {format}")
        };
    }

    public static RenderingDevice.DataFormat MatchDataFormat(Image.Format format)
    {
        return format switch
        {
            Image.Format.Rgba8 => RenderingDevice.DataFormat.R8G8B8A8Unorm,
            Image.Format.Rgb8 => RenderingDevice.DataFormat.R8G8B8Unorm,
            Image.Format.R8 => RenderingDevice.DataFormat.R8Unorm,
            Image.Format.Rgbaf => RenderingDevice.DataFormat.R32G32B32A32Sfloat,
            _ => throw new NotImplementedException($"Unknown format: {format}")
        };
    }
}