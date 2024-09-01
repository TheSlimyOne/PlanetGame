using Godot;
using Godot.Collections;
using Godot.NativeInterop;
using System;

namespace ComputeShaderClasses;

public partial class Texture2DUniform : ComputeShaderUniform
{
    public Texture2DUniform(RenderingDevice renderingDevice, int binding, Texture2D texture) : base(renderingDevice, binding)
    {
        Image image = RenderingServer.Texture2DGet(texture.GetRid());
        RDTextureFormat format = new RDTextureFormat()
        {
            Width = (uint)image.GetWidth(),
            Height = (uint)image.GetHeight(),
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

    public Texture2DUniform(RenderingDevice renderingDevice, int binding, RDTextureFormat format) : base(renderingDevice, binding)
    {
        Rid = renderingDevice.TextureCreate(format, new RDTextureView(), null);

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
}