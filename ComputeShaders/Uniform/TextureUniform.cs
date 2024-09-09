using Godot;
using Godot.Collections;

namespace Uniform;

public partial class TextureUniform : ComputeShaderUniform
{
    public RDTextureFormat TextureFormat { get; private set; }
    public RDSamplerState SamplerState { get; private set; }

    public TextureUniform(RenderingDevice renderingDevice, int binding, Texture2D texture, Image.Format imageFormat = Image.Format.Max, bool clearMipmaps = false, bool isSampler = false) : base(renderingDevice, binding)
    {
        Image image = texture.GetImage();

        TextureFormat = new RDTextureFormat()
        {
            Width = (uint)image.GetWidth(),
            Height = (uint)image.GetHeight(),
            Format = RenderingDevice.DataFormat.R8Unorm,
            UsageBits = RenderingDevice.TextureUsageBits.StorageBit | RenderingDevice.TextureUsageBits.SamplingBit | RenderingDevice.TextureUsageBits.CanCopyFromBit
        };
        
        if (clearMipmaps)
            image.ClearMipmaps();
        
        image.Convert(imageFormat == Image.Format.Max ? image.GetFormat() : imageFormat);
    
        Rid = renderingDevice.TextureCreate(TextureFormat, new RDTextureView(), new Array<byte[]>(){ image.GetData() });

        Uniform = new()
        {
            UniformType = isSampler ? RenderingDevice.UniformType.SamplerWithTexture : RenderingDevice.UniformType.Image,
            Binding = binding
        };

        if (isSampler)
        {
            SamplerState = new RDSamplerState() { MagFilter = RenderingDevice.SamplerFilter.Linear, MinFilter = RenderingDevice.SamplerFilter.Linear };
            Uniform.AddId(_rd.SamplerCreate(SamplerState));
        }
        Uniform.AddId(Rid);

    }
 
    public TextureUniform(RenderingDevice renderingDevice, int binding, RDTextureFormat format, bool isSampler = false, byte[] textureData = null) : base(renderingDevice, binding)
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

    private TextureUniform(TextureUniform textureUniform, int binding) : base(textureUniform._rd, binding)
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

    public Texture2Drd GetTexture2Drd()
    {
        return new Texture2Drd() { TextureRdRid = Rid };
    }

    public Image GetImage(Image.Format format)
    {
        byte[] bytes = _rd.TextureGetData(Rid, 0);
        return Image.CreateFromData((int)TextureFormat.Width, (int)TextureFormat.Height, false, format, bytes); ;
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
        return GetTexture2Drd().GetImage().GetPixel(x, y);
    }

    public void ClearTexture(Color color)
    {
        _rd.TextureClear(Rid, color, 0, 1, 0, 1);
    }

    public override void UpdateUniform(byte[] data)
    {
        _rd.BufferUpdate(Rid, 0, (uint)data.Length, data);
    }

    public override TextureUniform RebindUniform(RenderingDevice rd, int binding)
    {
        if (rd == _rd)
            return new TextureUniform(this, binding);
        else
        {
            bool isSampler = Uniform.UniformType == RenderingDevice.UniformType.SamplerWithTexture;
            return new TextureUniform(rd, binding, TextureFormat, isSampler, GetByteData());
        }
    }

    public override byte[] GetByteData()
    {
        return _rd.TextureGetData(Rid, 0);
    }

}