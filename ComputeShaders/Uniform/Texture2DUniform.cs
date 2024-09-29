using Godot;
using Godot.Collections;

namespace Uniform;

public partial class Texture2DUniform : ComputeShaderUniform
{
    public RDTextureFormat TextureFormat { get; private set; }
    public RDSamplerState SamplerState { get; private set; }

    public Texture2DUniform(RenderingDevice renderingDevice, int binding, RDTextureFormat format, bool isSampler = false, byte[] textureData = null) : base(renderingDevice, binding)
    {
        TextureFormat = format;
        Rid = renderingDevice.TextureCreate(TextureFormat, new RDTextureView(), textureData != null ? new Array<byte[]>() { textureData } : null);

        Uniform = new()
        {
            UniformType = isSampler ? RenderingDevice.UniformType.SamplerWithTexture : RenderingDevice.UniformType.Image,
            Binding = binding
        };

        if (isSampler)
        {
            SamplerState = new RDSamplerState();
            Uniform.AddId(_rd.SamplerCreate(SamplerState));
        }
        
        Uniform.AddId(Rid);
    }

    private Texture2DUniform(Texture2DUniform textureUniform, int binding) : base(textureUniform._rd, binding)
    {
        TextureFormat = textureUniform.TextureFormat;
        Rid = textureUniform.Rid;

        Uniform = new()
        {
            UniformType = textureUniform.Uniform.UniformType,
            Binding = binding
        };

        if (textureUniform.SamplerState != null)
        {
            SamplerState = textureUniform.SamplerState;
            Uniform.AddId(_rd.SamplerCreate(SamplerState));
        }
        Uniform.AddId(Rid);

    }

    public Texture2Drd GetTexture2Drd() => new() { TextureRdRid = Rid };

    public Image GetImage(Image.Format format) => Image.CreateFromData((int)TextureFormat.Width, (int)TextureFormat.Height, false, format,  _rd.TextureGetData(Rid, 0));
    
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

    public Color GetPixel(int x, int y) => GetTexture2Drd().GetImage().GetPixel(x, y);

    public void ClearTexture(Color color) => _rd.TextureClear(Rid, color, 0, 1, 0, 1);

    public override void UpdateUniform(byte[] data, uint layer = 0) => _rd.BufferUpdate(Rid, 0, (uint)data.Length, data);

    public override Texture2DUniform RebindUniform(RenderingDevice rd, int binding)
    {
        if (rd == _rd)
            return new Texture2DUniform(this, binding);
        else
        {
            bool isSampler = Uniform.UniformType == RenderingDevice.UniformType.SamplerWithTexture;
            return new Texture2DUniform(rd, binding, TextureFormat, isSampler, GetByteData());
        }
    }

    public override byte[] GetByteData(uint index = 0) => _rd.TextureGetData(Rid, 0);
}