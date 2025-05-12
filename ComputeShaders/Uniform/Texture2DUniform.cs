using Godot;
using Godot.Collections;
using PlanetGame.ComputeShaders.Dispatcher;
using UniformException;

namespace Uniform
{
    public partial class Texture2DUniform : ComputeShaderUniform
    {
        public RDTextureFormat TextureFormat { get; protected set; }
        public RDSamplerState SamplerState { get; protected set; }

        // TODO prob should implement perserved lol idk how I missed that
        // got it partial done ig
        public Texture2DUniform(IDispatchable owner, RenderingDevice renderingDevice, int binding, RDTextureFormat format, RenderingDevice.UniformType uniformType, Array<byte[]> textureData = null, bool perserved = false) : base(renderingDevice, binding, owner, perserved)
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
                    // TODO Figure this bs out 
                    //MinFilter = RenderingDevice.SamplerFilter.Linear
                };
                Uniform.AddId(RenderingDevice.SamplerCreate(SamplerState));
            }

            Uniform.AddId(Rid);
        }

        private Texture2DUniform(IDispatchable owner, Texture2DUniform textureUniform, int binding) : base(textureUniform.RenderingDevice, binding, owner, textureUniform.Perserved)
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

        public Texture2DUniform(IDispatchable owner, int binding, Rid rid, RenderingDevice.UniformType uniformType, bool perserved = false) : base(binding, owner, perserved)
        {
            Rid = rid;
            TextureFormat = RenderingDevice.TextureGetFormat(Rid);

            Uniform = new()
            {
                UniformType = uniformType,
                Binding = binding
            };

            Uniform.AddId(Rid);
        }

        public Texture2Drd GetTexture2Drd() => new() { TextureRdRid = Rid };
        public Texture2DArrayRD GetTexture2DArrayRD() => new() { TextureRdRid = Rid };

        public Image GetImage(Image.Format format, uint layer = 0) => Image.CreateFromData((int)TextureFormat.Width, (int)TextureFormat.Height, false, format, GetLayerByteData(layer));

        public Image[] GetImageArray(Image.Format format)
        {
            Image[] images = new Image[TextureFormat.ArrayLayers];
            for (uint i = 0; i < images.Length; i++)
            {
                images[i] = Image.CreateFromData((int)TextureFormat.Width, (int)TextureFormat.Height, false, format, GetLayerByteData(i));
            }
            return images;
        }

        public byte[] GetLayerByteData(uint layer) => RenderingDevice.TextureGetData(Rid, layer);

        public void SaveImage(string path, Image.Format format, uint layer = 0)
        {
            Error error = GetImage(format, layer).SavePng(path + ".png");

            if (error != Error.Ok)
            {
                GD.PrintErr($"Failed to save image: {error}");
            }
            else
            {
                GD.Print($"Image saved successfully to {path}.png");
            }
        }

        public Color GetPixel(int x, int y) => GetTexture2Drd().GetImage().GetPixel(x, y);

        public void ClearTexture(Color color) => RenderingDevice.TextureClear(Rid, color, 0, 1, 0, 1);
        public void ClearTexture(Color color, uint baseMipmap = 0, uint mipmapCount = 1, uint baseLayer = 0, uint layerCount = 1) => RenderingDevice.TextureClear(Rid, color, baseMipmap, mipmapCount, baseLayer, layerCount);

        public override void UpdateUniform(byte[] data) => RenderingDevice.TextureUpdate(Rid, 0, data);
        public void UpdateUniform(uint offset, uint sizeBytes, byte[] data) => RenderingDevice.BufferUpdate(Rid, offset, sizeBytes, data);

        public void SetImage(Image image) => UpdateUniform(image.GetData());

        public void SetImage(Image[] images)
        {
            for (uint i = 0; i < images.Length; i++)
            {
                RenderingDevice.TextureUpdate(Rid, i, images[i].GetData());
            }
        }

        public override Texture2DUniform RebindUniform(IDispatchable owner, RenderingDevice rd, int binding)
        {
            if (rd == RenderingDevice)
                return new Texture2DUniform(Owner, this, binding);
            else if (!UsingMainRenderingDevice)
                return new Texture2DUniform(owner, rd, binding, TextureFormat, Uniform.UniformType, GetByteData());
            else
                throw new InvalidRenderingDeviceException();
        }

        public override Array<byte[]> GetByteData()
        {
            Array<byte[]> data = [];
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