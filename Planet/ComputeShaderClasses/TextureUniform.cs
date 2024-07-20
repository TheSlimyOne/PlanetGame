using Godot;
using Godot.Collections;
using System;

namespace ComputeShaderClasses;

public partial class TextureUniform : ComputeShaderUniform
{
    public TextureUniform(RenderingDevice renderingDevice, int binding, Texture2D texture) : base(renderingDevice, binding)
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
        RenderingDevice = renderingDevice;

        Uniform = new()
        {
            UniformType = RenderingDevice.UniformType.Image,
            Binding = binding
        };
        Uniform.AddId(Rid);

    }

    public TextureUniform(RenderingDevice renderingDevice, int binding, ref Texture2Drd texture, RDTextureFormat format) : base(renderingDevice, binding)
    {
        Rid = renderingDevice.TextureCreate(format, new RDTextureView(), null);
        RenderingDevice = renderingDevice;

        Uniform = new()
        {
            UniformType = RenderingDevice.UniformType.Image,
            Binding = binding
        };
        Uniform.AddId(Rid);

        texture = new Texture2Drd() { TextureRdRid = Rid };
    }


    public override void UpdateUniform(byte[] data)
    {
        RenderingDevice.BufferUpdate(Rid, 0, (uint)data.Length, data);
    }
}