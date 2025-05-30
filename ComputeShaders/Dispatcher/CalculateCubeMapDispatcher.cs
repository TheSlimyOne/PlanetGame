using System;
using Uniform;
using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;
using static PlanetGame.Util.Utilities;
using System.Linq;

namespace PlanetGame.ComputeShaders.Dispatcher
{
    public partial class CalculateCubeMapDispatcher : ComputeShaderDispatcher<CalculateCubeMapDispatcher.BufferNames>
    {
        public enum BufferNames
        {
            SUB_TILES,
            PLANE,
            PLANE_DATA,
        }

        public const int INVOCATIONS = 16;
        public int TileSize { get; set; }
        public int BorderSize { get; set; }

        public Vector2I SubTileParitionSize = new(4, 2);

        private uint _cubeMapSubTileCount = 4;

        public CalculateCubeMapDispatcher() : base(ShaderPaths.CREATE_CUBE_MAP, RenderingServer.CreateLocalRenderingDevice())
        {
            SetupComputeShader();
        }

        public override void CreateUniforms()
        {
            _computeShaderUniforms = new Dictionary<Enum, ComputeShaderUniform>()
            {
                [BufferNames.SUB_TILES] = new Texture2DUniform(this, _RenderingDevice, (int)BufferNames.SUB_TILES,
                    new RDTextureFormat()
                    {
                        Width = (uint)TileSize,
                        Height = (uint)TileSize,
                        ArrayLayers = (uint)SubTileParitionSize.X * (uint)SubTileParitionSize.Y,
                        TextureType = RenderingDevice.TextureType.Type2DArray,
                        Format = RenderingDevice.DataFormat.R8G8B8A8Unorm,
                        UsageBits = RenderingDevice.TextureUsageBits.StorageBit | RenderingDevice.TextureUsageBits.SamplingBit | RenderingDevice.TextureUsageBits.CanCopyFromBit | RenderingDevice.TextureUsageBits.CanUpdateBit
                    }, RenderingDevice.UniformType.SamplerWithTexture),

                [BufferNames.PLANE] = new Texture2DUniform(this, _RenderingDevice, (int)BufferNames.PLANE,
                    new RDTextureFormat()
                    {
                        Width = (uint)TileSize,
                        Height = (uint)TileSize,
                        ArrayLayers = _cubeMapSubTileCount,
                        TextureType = RenderingDevice.TextureType.Type2DArray,
                        Format = RenderingDevice.DataFormat.R8G8B8A8Unorm,
                        UsageBits = RenderingDevice.TextureUsageBits.StorageBit | RenderingDevice.TextureUsageBits.SamplingBit | RenderingDevice.TextureUsageBits.CanCopyFromBit | RenderingDevice.TextureUsageBits.CanCopyToBit | RenderingDevice.TextureUsageBits.CanUpdateBit
                    }, RenderingDevice.UniformType.Image
                ),

                [BufferNames.PLANE_DATA] = new StorageBufferUniform(this, _RenderingDevice, (int)BufferNames.PLANE_DATA, [
                    .. ToBytes<uint>([(uint)SubTileParitionSize.X, (uint)SubTileParitionSize.Y, 0, 0]),
                ]),
            };

            _RenderingDevice.TextureClear(this[BufferNames.PLANE].Rid, Colors.Orange, 0, 1, 0, _cubeMapSubTileCount);
            CreateUniformSet();
        }

        public override void Invoke()
        {
            Vector2I numThreads = new(TileSize / INVOCATIONS, TileSize / INVOCATIONS);
            long computeList = _RenderingDevice.ComputeListBegin();
            _RenderingDevice.ComputeListBindComputePipeline(computeList, _pipeline);
            _RenderingDevice.ComputeListBindUniformSet(computeList, _uniformSet, 0);
            _RenderingDevice.ComputeListDispatch(computeList, (uint)numThreads.X, (uint)numThreads.Y, _cubeMapSubTileCount);
            _RenderingDevice.ComputeListEnd();
            SubmitThenSync();
        }

        public override void UpdateUniforms()
        {
            throw new NotImplementedException();
        }

        private Image GetLastProcessedCubeFace()
        {
            Image[] image = GetUniform<Texture2DUniform>(BufferNames.PLANE).GetImageArray(Image.Format.Rgba8);

            // TODO Hard coded prob can be a function so to reuse it for saveCubeMap
            Image fullPlane = Image.CreateEmpty(2 * TileSize, 2 * TileSize, false, Image.Format.Rgba8);
            for (int i = 0; i < _cubeMapSubTileCount; i++)
            {
                int x = i % ((int)_cubeMapSubTileCount / 2);
                int y = i / ((int)_cubeMapSubTileCount / 2);
                fullPlane.BlitRect(image[i], new(0, 0, Vector2I.One * TileSize), new Vector2I(x, y) * TileSize);
            }
            return fullPlane;
        }

        // TODO need to un hardcode sub image right now it only supports 4 x 2
        // Maybe bench mark the speeds to see find which configuation is faster 
        // Not really important only happens twice per save
        private Image[] CreateSubTiles(Image baseImage)
        {
            int subTileCount = SubTileParitionSize.X * SubTileParitionSize.Y;
            Vector2I subTileSize = baseImage.GetSize() / SubTileParitionSize;
            Image[] subImages = new Image[subTileCount];
            for (int i = 0; i < subTileCount; i++)
            {
                int x = i % SubTileParitionSize.X * subTileSize.X;
                int y = i / SubTileParitionSize.X * subTileSize.Y;

                subImages[i] = Image.CreateEmpty(subTileSize.X, subTileSize.Y, false, baseImage.GetFormat());
                subImages[i].BlitRect(baseImage, new Rect2I(x, y, subTileSize.X, subTileSize.Y), Vector2I.Zero);
            }
            return subImages;
        }

        public Image[] CreateCubeMap(Image image, Image.Interpolation interpolation = Image.Interpolation.Bilinear)
        {
            // TODO install package for image processing
            if (image.GetSize().X != image.GetSize().Y * 2)
                throw new ArgumentException("The provided image must be 2:1.");

            if (image.IsCompressed())
                image.Decompress();
            if (image.HasMipmaps())
                image.ClearMipmaps();
            if (image.GetFormat() != Image.Format.Rgba8)
                image.Convert(Image.Format.Rgba8);

            image.Resize(16384, 8192, interpolation);

            Image[] subTiles = CreateSubTiles(image);
            for (int i = 0; i < subTiles.Length; i++)
            {
                subTiles[i].SavePng($"user://Tests//subtiles-{i}.png");
                // GD.Print($"{i}");
            }
            GetUniform<Texture2DUniform>(BufferNames.SUB_TILES).SetImage(subTiles);

            // Stopwatch stopwatch = new();
            // stopwatch.Start();
            // Image[] cubeMap = new Image[6];
            // for (int i = 0; i < 6; i++)
            // {
            //     GetUniform<StorageBufferUniform>(BufferNames.PLANE_DATA).UpdateUniform(SizeOf<uint>() * 3, SizeOf<uint>(), [.. ToBytesSingle(i)]);
            //     Invoke();
            //     SubmitThenSync();
            //     cubeMap[i] = GetLastProcessedCubeFace();
            // }

            // GD.PrintS("Cube map generation complete");
            // stopwatch.Stop();
            // GD.Print(stopwatch.Elapsed);
            // return cubeMap;
            
            return [];

        }
    }
}

