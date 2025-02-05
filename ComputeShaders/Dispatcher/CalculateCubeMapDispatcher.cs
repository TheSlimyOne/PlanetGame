using System;
using Uniform;
using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
namespace Dispatcher
{
    public partial class CalculateCubeMapDispatcher : ComputeShaderDispatcher<CalculateCubeMapDispatcher.BufferNames>
    {
        public enum BufferNames
        {
            IMAGE_TEXTURE,
            CUBE_TEXTURE,
        }

        public const int INVOCATIONS = 16;
        public Vector2I BaseImageSize { get; set; }

        public Queue<Image> ImageQueue { get; private set; } = new();

        public CalculateCubeMapDispatcher(string shaderFilePath, RenderingDevice rd) : base(shaderFilePath, rd)
        {
            SetupComputeShader();
        }

        public override void CreateUniforms()
        {
            _computeShaderUniforms = new Dictionary<Enum, ComputeShaderUniform>()
            {

                [BufferNames.IMAGE_TEXTURE] = new Texture2DUniform(this, _RenderingDevice, (int)BufferNames.IMAGE_TEXTURE,
                    new RDTextureFormat()
                    {
                        Width = (uint)BaseImageSize.X,
                        Height = (uint)BaseImageSize.Y,
                        TextureType = RenderingDevice.TextureType.Type2D,
                        Format = RenderingDevice.DataFormat.R32G32B32A32Sfloat,
                        UsageBits = RenderingDevice.TextureUsageBits.StorageBit | RenderingDevice.TextureUsageBits.SamplingBit | RenderingDevice.TextureUsageBits.CanCopyFromBit | RenderingDevice.TextureUsageBits.CanUpdateBit
                    }, RenderingDevice.UniformType.SamplerWithTexture),


                [BufferNames.CUBE_TEXTURE] = new Texture2DUniform(this, _RenderingDevice, (int)BufferNames.CUBE_TEXTURE,
                    new RDTextureFormat()
                    {
                        Width = (uint)BaseImageSize.Y,
                        Height = (uint)BaseImageSize.Y,
                        ArrayLayers = 6,
                        TextureType = RenderingDevice.TextureType.Type2DArray,
                        Format = RenderingDevice.DataFormat.R32G32B32A32Sfloat,
                        UsageBits = RenderingDevice.TextureUsageBits.StorageBit | RenderingDevice.TextureUsageBits.SamplingBit | RenderingDevice.TextureUsageBits.CanCopyFromBit | RenderingDevice.TextureUsageBits.CanCopyToBit | RenderingDevice.TextureUsageBits.CanUpdateBit
                    }, RenderingDevice.UniformType.Image
                )
            };
            _RenderingDevice.TextureClear(GetUniform(BufferNames.CUBE_TEXTURE).Rid, Colors.Orange, 0, 1, 0, 6);
            CreateUniformSet();
        }

        public override void Invoke()
        {
            Vector2I numThreads = new(BaseImageSize.Y / INVOCATIONS, BaseImageSize.Y / INVOCATIONS);
            long computeList = _RenderingDevice.ComputeListBegin();
            _RenderingDevice.ComputeListBindComputePipeline(computeList, _pipeline);
            _RenderingDevice.ComputeListBindUniformSet(computeList, _uniformSet, 0);
            _RenderingDevice.ComputeListDispatch(computeList, (uint)numThreads.X, (uint)numThreads.Y, 6);
            _RenderingDevice.ComputeListEnd();
        }

        public override void UpdateUniforms()
        {
            Image image = ImageQueue.Dequeue();
            GetUniform<Texture2DUniform>(BufferNames.IMAGE_TEXTURE).ReplaceImage(image);
        }

        public void SaveCubeMap(string path)
        {

            GetUniform<Texture2DUniform>(BufferNames.CUBE_TEXTURE).SaveImage(path + "0", Image.Format.Rgbaf, layer: 0);
            GetUniform<Texture2DUniform>(BufferNames.CUBE_TEXTURE).SaveImage(path + "1", Image.Format.Rgbaf, layer: 1);
            GetUniform<Texture2DUniform>(BufferNames.CUBE_TEXTURE).SaveImage(path + "2", Image.Format.Rgbaf, layer: 2);
            GetUniform<Texture2DUniform>(BufferNames.CUBE_TEXTURE).SaveImage(path + "3", Image.Format.Rgbaf, layer: 3);
            GetUniform<Texture2DUniform>(BufferNames.CUBE_TEXTURE).SaveImage(path + "4", Image.Format.Rgbaf, layer: 4);
            GetUniform<Texture2DUniform>(BufferNames.CUBE_TEXTURE).SaveImage(path + "5", Image.Format.Rgbaf, layer: 5);
        }

        public void CreateCubeMaps(string destination)
        {
            // Thread thread = new(() =>
            // {
            Stopwatch stopwatch = new();
            stopwatch.Start();
            GD.PrintS($"Generating cube map: {ImageQueue.Count}");
            while (ImageQueue.Count > 0)
            {
                RenderingServer.CallOnRenderThread(Callable.From(() =>
                {
                    UpdateUniforms();
                    Invoke();
                    Submit();
                    Sync();
                }));
            }


            // SaveCubeMap(destination);
            GD.PrintS("Cube map generation complete");
            stopwatch.Stop();
            GD.Print(stopwatch.Elapsed);
            // CleanupGPU();

            // });
            // thread.Start();
        }
    }
}

