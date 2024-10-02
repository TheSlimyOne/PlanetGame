using System;
using Uniform;
using Godot;
namespace Dispatcher
{
    public partial class CalculateNormalsDispatcher : ComputeShaderDispatcher<CalculateNormalsDispatcher.BufferNames>
    {
        public enum BufferNames
        {
            HEIGHT_MAP_DATA,
            HEIGHT_MAP,
            NORMAL_MAP
        }

        public float Radius;
        public float HeightScale;
        public Texture2D InputTexture;

        public CalculateNormalsDispatcher(string shaderFilePath, ref RenderingDevice rd) : base(shaderFilePath, ref rd)
        {
            SetupComputeShader();
        }

        public override void CreateUniforms()
        {
            _computeShaderUniforms = new System.Collections.Generic.Dictionary<Enum, ComputeShaderUniform>()
            {
                [BufferNames.HEIGHT_MAP_DATA] = new StorageBufferUniform(this, _rd, (int)BufferNames.HEIGHT_MAP_DATA, Utilities.ToBytes<float>(new float[] {
                    Radius,
                    HeightScale
                }).ToArray()),

                [BufferNames.HEIGHT_MAP] = new Func<Texture2DUniform>(() =>
                {
                    Image image = InputTexture.GetImage();
                    image.ClearMipmaps();
                    image.Convert(Image.Format.L8);

                    return new Texture2DUniform(this, _rd, (int)BufferNames.HEIGHT_MAP,
                        new RDTextureFormat()
                        {
                            Width = (uint)image.GetWidth(),
                            Height = (uint)image.GetHeight(),
                            TextureType = RenderingDevice.TextureType.Type2D,
                            Format = RenderingDevice.DataFormat.R8Unorm,
                            UsageBits = RenderingDevice.TextureUsageBits.StorageBit | RenderingDevice.TextureUsageBits.SamplingBit | RenderingDevice.TextureUsageBits.CanCopyFromBit
                        }, RenderingDevice.UniformType.SamplerWithTexture, textureData: new() { image.GetData() } );
                }).Invoke(),

                [BufferNames.NORMAL_MAP] = new Texture2DUniform(this, _rd, (int)BufferNames.NORMAL_MAP,
                    new RDTextureFormat()
                    {
                        Width = (uint)InputTexture.GetWidth(),
                        Height = (uint)InputTexture.GetHeight(),
                        TextureType = RenderingDevice.TextureType.Type2D,
                        Format = RenderingDevice.DataFormat.R32G32B32A32Sfloat,
                        UsageBits = RenderingDevice.TextureUsageBits.SamplingBit |
                                    RenderingDevice.TextureUsageBits.StorageBit |
                                    RenderingDevice.TextureUsageBits.CanUpdateBit |
                                    RenderingDevice.TextureUsageBits.CanCopyToBit |
                                    RenderingDevice.TextureUsageBits.CanCopyFromBit |
                                    RenderingDevice.TextureUsageBits.ColorAttachmentBit
                    }, RenderingDevice.UniformType.Image
                ),
            };
            CreateUniformSet();
        }

        public void SaveNormalMap(string path)
        {
            GetUniform<Texture2DUniform>(BufferNames.NORMAL_MAP).SaveImage(path, Image.Format.Rgbaf);
        }

        public override void Ready()
        {
            Vector2I numThreads = new Vector2I(InputTexture.GetWidth() / 8, InputTexture.GetHeight() / 8);
            long computeList = _rd.ComputeListBegin();
            _rd.ComputeListBindComputePipeline(computeList, _pipeline);
            _rd.ComputeListBindUniformSet(computeList, _uniformSet, 0);
            _rd.ComputeListDispatch(computeList, (uint)numThreads.X, (uint)numThreads.Y, 1);
            _rd.ComputeListEnd();
        }

        public override void UpdateUniforms()
        {
            throw new NotImplementedException();
        }
    }
}