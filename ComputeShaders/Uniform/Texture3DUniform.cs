using System;
using Godot;
using Godot.Collections;

namespace Uniform;

public partial class Texture3DUniform : ComputeShaderUniform
{
    public RDTextureFormat TextureFormat { get; private set; }
    public RDSamplerState SamplerState { get; private set; }

    public Texture3DUniform(RenderingDevice renderingDevice, int binding, RDTextureFormat format, bool isSampler = false, Array<byte[]> textureData = null) : base(renderingDevice, binding)
    {
        TextureFormat = format;
        Rid = renderingDevice.TextureCreate(TextureFormat, new RDTextureView(), textureData);

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

    private Texture3DUniform(Texture3DUniform textureUniform, int binding) : base(textureUniform._rd, binding)
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

    public Texture3Drd GetTexture3Drd() => new() { TextureRdRid = Rid };

    public override void UpdateUniform(byte[] data, uint layer)
    {
        _rd.TextureUpdate(Rid, layer, data);
    }

    public override Texture3DUniform RebindUniform(RenderingDevice rd, int binding)
    {
        if (rd == _rd)
            return new Texture3DUniform(this, binding);
        else
        {
            bool isSampler = Uniform.UniformType == RenderingDevice.UniformType.SamplerWithTexture;
            return new Texture3DUniform(rd, binding, TextureFormat, isSampler, GetAllByteData());
        }
        throw new NotImplementedException();
    }

    public override byte[] GetByteData(uint layer) => _rd.TextureGetData(Rid, layer);

    public Array<byte[]> GetAllByteData()
    {
        Array<byte[]> data = new();
        for (uint i = 0; i < _rd.TextureGetFormat(Rid).Depth; i++)
            data.Add(GetByteData(i));
        return data;
    }
}