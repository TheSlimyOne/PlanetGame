using Godot;
using Godot.Collections;
using Godot.NativeInterop;
using System;

namespace Uniform;

public partial class TextureUniform : ComputeShaderUniform
{   
    public int Width {get; private set;}
    public int Height {get; private set;}
    public TextureUniform(RenderingDevice renderingDevice, int binding, Texture2D texture) : base(renderingDevice, binding)
    {
        Image image = RenderingServer.Texture2DGet(texture.GetRid());
        Width = image.GetWidth();
        Height = image.GetHeight();

        RDTextureFormat format = new RDTextureFormat()
        {
            Width = (uint)Width,
            Height = (uint)Height,
            Format = RenderingDevice.DataFormat.R8Unorm,
            UsageBits = RenderingDevice.TextureUsageBits.StorageBit | RenderingDevice.TextureUsageBits.SamplingBit
        };
        image.ClearMipmaps();
        image.Convert(Image.Format.L8);
        Array<byte[]> data = new() { image.GetData() };

        Rid = renderingDevice.TextureCreate(format, new RDTextureView(), data);

        Uniform = new()
        {
            UniformType = RenderingDevice.UniformType.Image,
            Binding = binding
        };
        Uniform.AddId(Rid);

    }

    public TextureUniform(RenderingDevice renderingDevice, int binding, RDTextureFormat format) : base(renderingDevice, binding)
    {
        Width = (int)format.Width;
        Height = (int)format.Height;
        Rid = renderingDevice.TextureCreate(format, new RDTextureView(), null);

        Uniform = new()
        {
            UniformType = RenderingDevice.UniformType.Image,
            Binding = binding
        };
        Uniform.AddId(Rid);
    }

    public TextureUniform(TextureUniform textureUniform, int binding) : base(textureUniform._rd, binding)
    {
        Width = textureUniform.Width;
        Height = textureUniform.Height;
        Rid = textureUniform.Rid;

        Uniform = new()
        {
            UniformType = RenderingDevice.UniformType.Image,
            Binding = binding
        };
        Uniform.AddId(Rid);
    }

    public Texture2Drd GetTexture2Drd()
    {
        return new Texture2Drd() { TextureRdRid = Rid };
    }

    public Image GetImage(Image.Format format)
    {
        byte[] bytes = _rd.TextureGetData(Rid, 0);
        return Image.CreateFromData(Width, Height, false, format, bytes);;
    }

    public void SaveImage(string path, Image.Format format)
    {
        Error error = GetImage(format).SavePng(path);
        if (error != Error.Ok)
        {
            GD.PrintErr($"Failed to save image: {error}");
        }
        else
        {
            GD.Print($"Image saved successfully to {path}");
        }
    }

    public Color GetPixel(int x, int y)
    {
        return RenderingServer.Texture2DGet(GetTexture2Drd().GetRid()).GetPixel(x, y);
    }

    public void ClearTexture(Color color)
    {
        _rd.TextureClear(Rid, color, 0, 1, 0, 1);
    }

    public override void UpdateUniform(byte[] data)
    {
        _rd.BufferUpdate(Rid, 0, (uint)data.Length, data);
    }

    public override TextureUniform RebindUniform(int binding)
    {
        return new TextureUniform(this, binding);
    }

}