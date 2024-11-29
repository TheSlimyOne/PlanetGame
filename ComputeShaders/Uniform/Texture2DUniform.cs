using System;
using Godot;
using Godot.Collections;
using Dispatcher;

namespace Uniform
{
    public partial class Texture2DUniform : ComputeShaderUniform
    {
        public RDTextureFormat TextureFormat { get; private set; }
        public RDSamplerState SamplerState { get; private set; }

        public Texture2DUniform(IDispatchable owner, RenderingDevice renderingDevice, int binding, RDTextureFormat format, RenderingDevice.UniformType uniformType, Array<byte[]> textureData = null, bool perserved = false) : base(renderingDevice, binding, owner)
        {
            TextureFormat = format;
            Rid = renderingDevice.TextureCreate(TextureFormat, new RDTextureView(), textureData);

            Uniform = new()
            {
                UniformType = uniformType,
                Binding = binding
            };

            if (uniformType == RenderingDevice.UniformType.Sampler || uniformType == RenderingDevice.UniformType.SamplerWithTexture || uniformType == RenderingDevice.UniformType.SamplerWithTextureBuffer)
            {
                SamplerState = new RDSamplerState()
                {
                    // MinFilter = RenderingDevice.SamplerFilter.Linear
                };
                Uniform.AddId(_rd.SamplerCreate(SamplerState));
            }
        
            Uniform.AddId(Rid);
        }

        private Texture2DUniform(IDispatchable owner, Texture2DUniform textureUniform, int binding) : base(textureUniform._rd, binding, owner)
        {
            TextureFormat = textureUniform.TextureFormat;
            Rid = textureUniform.Rid;

            Uniform = new()
            {
                UniformType = textureUniform.Uniform.UniformType,
                Binding = binding
            };

            SamplerState = textureUniform.SamplerState;

            foreach (Rid rid in textureUniform.Uniform.GetIds())
            {
                Uniform.AddId(rid);
            }
        }

        public Texture2Drd GetTexture2Drd() => new() { TextureRdRid = Rid };

        public Image GetImage(Image.Format format, uint layer = 0) => Image.CreateFromData((int)TextureFormat.Width, (int)TextureFormat.Height, false, format, GetLayerByteData(layer));
        public byte[] GetLayerByteData(uint layer) => _rd.TextureGetData(Rid, layer);
    
        public void SaveImage(string path, Image.Format format)
        {
            for (uint i = 0; i < TextureFormat.ArrayLayers; i++)
            {
                Error error = GetImage(format, i).SavePng($"{path}_{i}.png");
                if (error != Error.Ok)
                {
                    GD.PrintErr($"Failed to save image: {error}");
                }
                else
                {
                    GD.Print($"Image saved successfully to {path}_{i}.png");
            }
            }
        }

        public Color GetPixel(int x, int y) => GetTexture2Drd().GetImage().GetPixel(x, y);

        public void ClearTexture(Color color) => _rd.TextureClear(Rid, color, 0, 1, 0, 1);

        public override void UpdateUniform(byte[] data) => _rd.BufferUpdate(Rid, 0, (uint)data.Length, data);

        public override Texture2DUniform RebindUniform(IDispatchable owner, RenderingDevice rd, int binding)
        {
            if (rd == _rd)
                return new Texture2DUniform(Owner, this, binding);
            else
            {
                return new Texture2DUniform(owner, rd, binding, TextureFormat, Uniform.UniformType, GetByteData());
            }
        }

        public override Array<byte[]> GetByteData() 
        {
            Array<byte[]> data = new();
            for (uint i = 0; i < TextureFormat.ArrayLayers; i++)
            {
                data.Add(GetLayerByteData(i));
            }
            return data;
        }

        public static byte[] CreateSolidColorImage(int width, int height, Image.Format format, Color color)
        {
            Image image = Image.CreateEmpty(width, height, false, format);
            image.Fill(color);
            return image.GetData();
        }
    }

}