using Godot;
using System;
using System.Runtime.Intrinsics.Arm;
using Uniform;

namespace Dispatcher
{
    public class BlurImageDispatcher : ComputeShaderDispatcher<BlurImageDispatcher.BufferNames>
    {
        public enum BufferNames
        {
            HEIGHT_MAP,
            BLURRED_HEIGHT_MAP,
            IMAGE_PADDING
        }

        public Image HeightMap { get; set; }
        public int Padding { get; set; }

        public BlurImageDispatcher(string shaderFilePath, RenderingDevice rd) : base(shaderFilePath, rd)
        {
            SetupComputeShader();
        }

        public override void CreateUniforms()
        {
            _computeShaderUniforms = new System.Collections.Generic.Dictionary<Enum, ComputeShaderUniform>()
            {


                [BufferNames.HEIGHT_MAP] = new Func<Texture2DUniform>(() =>
                {
                    Image image = HeightMap;
                    image.ClearMipmaps();
                    image.Convert(Image.Format.L8);

                    return new Texture2DUniform(this, _RenderingDevice, (int)BufferNames.HEIGHT_MAP,
                        new RDTextureFormat()
                        {
                            Width = (uint)image.GetWidth(),
                            Height = (uint)image.GetHeight(),
                            TextureType = RenderingDevice.TextureType.Type2D,
                            Format = RenderingDevice.DataFormat.R8Unorm,
                            UsageBits = RenderingDevice.TextureUsageBits.StorageBit | RenderingDevice.TextureUsageBits.SamplingBit | RenderingDevice.TextureUsageBits.CanCopyFromBit
                        }, RenderingDevice.UniformType.Image, textureData: new() { image.GetData() });
                }).Invoke(),

                [BufferNames.BLURRED_HEIGHT_MAP] = new Texture2DUniform(this, _RenderingDevice, (int)BufferNames.BLURRED_HEIGHT_MAP,
                    new RDTextureFormat()
                    {
                        Width = (uint)(HeightMap.GetWidth() - 2 * Padding),
                        Height = (uint)(HeightMap.GetHeight() - 2 * Padding),
                        TextureType = RenderingDevice.TextureType.Type2D,
                        Format = RenderingDevice.DataFormat.R8Unorm,
                        UsageBits = RenderingDevice.TextureUsageBits.SamplingBit |
                            RenderingDevice.TextureUsageBits.StorageBit |
                            RenderingDevice.TextureUsageBits.CanUpdateBit |
                            RenderingDevice.TextureUsageBits.CanCopyToBit |
                            RenderingDevice.TextureUsageBits.CanCopyFromBit |
                            RenderingDevice.TextureUsageBits.ColorAttachmentBit
                    }, RenderingDevice.UniformType.Image
                ),

                [BufferNames.IMAGE_PADDING] = new StorageBufferUniform(this, _RenderingDevice, (int)BufferNames.IMAGE_PADDING,
                    Utilities.ToBytes<int>(new int[] { Padding }).ToArray()
                )
            };
            CreateUniformSet();
        }

        public Image GetBlurredHeightMap()
        {
            Texture2DUniform texture2DUniform = GetUniform<Texture2DUniform>(BufferNames.BLURRED_HEIGHT_MAP);
            return texture2DUniform.GetImage(FormatConverter.MatchDataFormat(texture2DUniform.TextureFormat.Format), 0);
        }

        public void SaveBlurredHeightMap(string path) => GetUniform<Texture2DUniform>(BufferNames.BLURRED_HEIGHT_MAP).SaveImage(path, Image.Format.L8);

        public override void Invoke()
        {
            Vector2I numThreads = new((HeightMap.GetWidth() - 2 * Padding) / 8, (HeightMap.GetHeight() - 2 * Padding) / 8);
            long computeList = _RenderingDevice.ComputeListBegin();
            _RenderingDevice.ComputeListBindComputePipeline(computeList, _pipeline);
            _RenderingDevice.ComputeListBindUniformSet(computeList, _uniformSet, 0);
            _RenderingDevice.ComputeListDispatch(computeList, (uint)numThreads.X, (uint)numThreads.Y, 1);
            _RenderingDevice.ComputeListEnd();
        }

        public override void UpdateUniforms()
        {
            throw new NotImplementedException();
        }
    }
}
