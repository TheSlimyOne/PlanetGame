using System;
using Uniform;
using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;
using PlanetGame.Util;

namespace PlanetGame.ComputeShaders.Dispatcher
{
    public partial class CalculateCubeMapDispatcher : ComputeShaderDispatcher<CalculateCubeMapDispatcher.BufferNames>
    {
        public enum BufferNames
        {
            IMAGE_TEXTURE,
            PLANE,
            NORMAL_ID,
            TILE_DATA
        }

        public const int INVOCATIONS = 16;
        // public Vector2I BaseImageSize { get; set; }
        public int TileSize { get; set; }
        public int TileAmount { get; set; }

        private uint _cubeMapTiles = 4;

        public CalculateCubeMapDispatcher() : base(ShaderPaths.CREATE_CUBE_MAP, RenderingServer.CreateLocalRenderingDevice())
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
                        Width = (uint)TileSize,
                        Height = (uint)TileSize,
                        ArrayLayers = (uint)TileAmount,
                        TextureType = RenderingDevice.TextureType.Type2DArray,
                        Format = RenderingDevice.DataFormat.R8G8B8A8Unorm,
                        UsageBits = RenderingDevice.TextureUsageBits.StorageBit | RenderingDevice.TextureUsageBits.SamplingBit | RenderingDevice.TextureUsageBits.CanCopyFromBit | RenderingDevice.TextureUsageBits.CanUpdateBit
                    }, RenderingDevice.UniformType.SamplerWithTexture),


                [BufferNames.PLANE] = new Texture2DUniform(this, _RenderingDevice, (int)BufferNames.PLANE,
                    new RDTextureFormat()
                    {
                        Width = (uint)TileSize,
                        Height = (uint)TileSize,
                        ArrayLayers = _cubeMapTiles,
                        TextureType = RenderingDevice.TextureType.Type2DArray,
                        Format = RenderingDevice.DataFormat.R8G8B8A8Unorm,
                        UsageBits = RenderingDevice.TextureUsageBits.StorageBit | RenderingDevice.TextureUsageBits.SamplingBit | RenderingDevice.TextureUsageBits.CanCopyFromBit | RenderingDevice.TextureUsageBits.CanCopyToBit | RenderingDevice.TextureUsageBits.CanUpdateBit
                    }, RenderingDevice.UniformType.Image
                ),
                [BufferNames.NORMAL_ID] = new StorageBufferUniform(this, _RenderingDevice, (int)BufferNames.NORMAL_ID, [.. Utilities.ToBytes<uint>([0])]),

                [BufferNames.TILE_DATA] = new StorageBufferUniform(this, _RenderingDevice, (int)BufferNames.TILE_DATA, [.. Utilities.ToBytes<uint>([4, 2])])
            };
            _RenderingDevice.TextureClear(this[BufferNames.PLANE].Rid, Colors.Orange, 0, 1, 0, _cubeMapTiles);
            CreateUniformSet();
        }

        public override void Invoke()
        {
            Vector2I numThreads = new(TileSize / INVOCATIONS, TileSize / INVOCATIONS);
            long computeList = _RenderingDevice.ComputeListBegin();
            _RenderingDevice.ComputeListBindComputePipeline(computeList, _pipeline);
            _RenderingDevice.ComputeListBindUniformSet(computeList, _uniformSet, 0);
            _RenderingDevice.ComputeListDispatch(computeList, (uint)numThreads.X, (uint)numThreads.Y, _cubeMapTiles);
            _RenderingDevice.ComputeListEnd();
            SubmitThenSync();
        }

        public override void UpdateUniforms()
        {
            throw new NotImplementedException();
        }

        public void SaveCubeMap(string fileBaseName, string fileDestination)
        {
            Image[] image = GetUniform<Texture2DUniform>(BufferNames.PLANE).GetImageArray(Image.Format.Rgba8);
            Task.Run(() =>
            {
                // TODO Hard coded
                Image fullPlane = Image.CreateEmpty(2 * TileSize, 2 * TileSize, false, Image.Format.Rgba8);
                for (int i = 0; i < _cubeMapTiles; i++)
                {
                    int x = i % ((int)_cubeMapTiles / 2);
                    int y = i / ((int)_cubeMapTiles / 2);
                    fullPlane.BlitRect(image[i], new(0, 0, Vector2I.One * TileSize), new Vector2I(x, y) * TileSize);
                }
                fullPlane.SavePng($"{fileDestination}/{fileBaseName}.png");
            });
        }

        public void CreateCubeMaps(Image image, string fileBaseName, string fileDestination, Image.Interpolation interpolation = Image.Interpolation.Bilinear)
        {
            if (image.IsCompressed())
                image.Decompress();
            if (image.HasMipmaps())
                image.ClearMipmaps();
            if (image.GetFormat() != Image.Format.Rgba8)
                image.Convert(Image.Format.Rgba8);
            if (image.GetSize() != new Vector2I(16384, 8192))
                image.Resize(16384, 8192, interpolation);

            // TODO need to un hardcode sub image right now it only supports 4 x 2
            Image[] images = new Image[8];
            for (int i = 0; i < 8; i++)
            {
                images[i] = Image.CreateEmpty(4096, 4096, false, image.GetFormat());
                int x = i % 4 * 4096;
                int y = i / 4 * 4096;
                images[i].BlitRect(image, new Rect2I(x, y, Vector2I.One * 4096), Vector2I.Zero);
                // images[i].SavePng($"user://test/myworld/{i}.png");
            }
            GetUniform<Texture2DUniform>(BufferNames.IMAGE_TEXTURE).SetImage(images);

            Stopwatch stopwatch = new();
            stopwatch.Start();

            for (int i = 0; i < 6; i++)
            {
                GetUniform<StorageBufferUniform>(BufferNames.NORMAL_ID).UpdateUniform([.. Utilities.ToBytes([i])]);
                Invoke();
                SubmitThenSync();
                SaveCubeMap($"{fileBaseName}-{i}", fileDestination);
            }

            GD.PrintS("Cube map generation complete");
            stopwatch.Stop();
            GD.Print(stopwatch.Elapsed);
        }
    }
}

