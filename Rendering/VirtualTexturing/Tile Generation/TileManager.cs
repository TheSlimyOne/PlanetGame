using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PlanetGame.ComputeShaders.Dispatcher;
using Godot;
using PlanetGame.Util;
using System.Threading;
using System.Linq;
using System.Diagnostics;


namespace PlanetGame.Rendering.VirtualTexturing
{
    // TODO check if image supplied is 2:1
    // TODO need to make sure the image isnt larger than 16k x 16k and if it is ask for it subdivided
    public class TileManager
    {
        // public enum TileSize
        // {
        //     SIZE_64 = 64,
        //     SIZE_128 = 128,
        //     SIZE_256 = 256,
        //     SIZE_512 = 512,
        //     SIZE_1024 = 1024,
        // }

        private static Image[] GetMipmapSections(Image image, int maxMipIndex)
        {
            int bytesPerPixel = FormatConverter.GetBytes(image.GetFormat());
            List<Image> mipmaps = [];

            maxMipIndex = GetValidMipIndex(image, maxMipIndex);

            for (int i = 0; i <= maxMipIndex; i++)
            {
                Vector2I size = image.GetSize() / (1 << i);

                int mipOffset = (int)image.GetMipmapOffset(i);
                byte[] buffer = new byte[bytesPerPixel * size.X * size.Y];
                Array.Copy(image.GetData(), mipOffset, buffer, 0, buffer.Length);

                Image mipmap = Image.CreateFromData(size.X, size.Y, false, image.GetFormat(), buffer);
                mipmaps.Add(mipmap);
            }

            image.ClearMipmaps();
            return [.. mipmaps];
        }

        public static int GetTileCount(int maxMipIndex)
        {
            return 6 * ((int)Mathf.Pow(4, maxMipIndex + 1) - 1) / 3;
        }

        public static event Action<int, string, int> OnTileGeneratedProgress;

        public static async Task GenerateTilesAsync(Image image, int maxMipIndex, string destination, int padding)
        {
            Image[] mipmaps = GetMipmapSections(image, maxMipIndex);

            int processedTiles = 0;
            int totalTiles = GetTileCount(maxMipIndex);
            List<Task> tasks = [];

            for (int mipIndex = mipmaps.Length - 1; mipIndex >= 0; mipIndex--)
            {
                int tilesPerSide = (int)Mathf.Pow(2, mipmaps.Length - 1 - mipIndex);
                Image mipmap = mipmaps[mipIndex];

                for (int normalId = 0; normalId < 6; normalId++)
                {
                    for (int tileIndex = 0; tileIndex < tilesPerSide * tilesPerSide; tileIndex++)
                    {
                        int tileY = tileIndex / tilesPerSide;
                        int tileX = tileIndex % tilesPerSide;

                        TileGenerationParams parameters = new()
                        {
                            TileIndexX = tileX,
                            TileIndexY = tileY,
                            MipIndex = mipIndex,
                            NormalId = normalId,
                            Source = mipmap,
                            TilesPerSide = tilesPerSide,
                            TileSize = mipmap.GetSize().Y / tilesPerSide,
                            Destination = destination,
                            Padding = padding
                        };

                        tasks.Add(new Task(() =>
                        {
                            Image tile = GenerateTile(parameters);

                            int current = Interlocked.Increment(ref processedTiles);
                            tile.SavePng($"{parameters.Destination}\\{parameters.MipIndex}-{parameters.NormalId}-{parameters.TileIndexX}-{parameters.TileIndexY}.png");
                            string outputText = $"Processing Normal: {parameters.NormalId} at Mip: {parameters.MipIndex} for tile coords: ({parameters.TileIndexX}, {parameters.TileIndexY})";
                            OnTileGeneratedProgress?.Invoke(current, outputText, totalTiles);
                        }));
                    }
                }
            }

            Stopwatch stopwatch = new();
            stopwatch.Start();
            const int BATCH_SIZE = 32;
            for (int i = 0; i < tasks.Count; i += BATCH_SIZE)
            {
                List<Task> batch = [.. tasks.Skip(i).Take(BATCH_SIZE)];
                foreach (Task task in batch)
                {
                    task.Start();
                }
                await Task.WhenAll(batch);
            }
            stopwatch.Stop();
            GD.Print($"Done in: {stopwatch.Elapsed}");
        }

        public struct TileGenerationParams
        {
            public int TileIndexX { get; set; }
            public int TileIndexY { get; set; }
            public int NormalId { get; set; }
            public int MipIndex { get; set; }
            public Image Source { get; set; }
            public int TilesPerSide { get; set; }
            public int TileSize { get; set; }
            public int Padding { get; set; }
            public string Destination { get; set; }
        }

        private static Image GenerateTile(TileGenerationParams parameters)
        {
            int paddedSize = parameters.TileSize + 2 * parameters.Padding;
            Image tile = Image.CreateEmpty(paddedSize, paddedSize, false, parameters.Source.GetFormat());

            for (int y = -parameters.Padding; y < parameters.TileSize + parameters.Padding; y++)
            {
                for (int x = -parameters.Padding; x < parameters.TileSize + parameters.Padding; x++)
                {
                    Vector2 coordinates = new(x, y);
                    Vector2 cubePixel = coordinates + (parameters.TileSize - 1) * new Vector2(parameters.TileIndexX, parameters.TileIndexY);
                    Vector2 cubeUV = cubePixel / ((parameters.TileSize - 1) * parameters.TilesPerSide);

                    Vector3 cubePoint = VectorUtils.UVToPointOnCube(parameters.NormalId, cubeUV);
                    Vector3 spherePoint = VectorUtils.PointOnCubeToPointOnSphere(cubePoint);
                    Vector2 sphereUV = VectorUtils.PointOnSphereToUV(spherePoint);

                    Color pixel = Sampler.SampleBilinear(parameters.Source, sphereUV.X, sphereUV.Y);
                    tile.SetPixel(x + parameters.Padding, y + parameters.Padding, pixel);
                }
            }

            return tile;
        }


        public static int GetValidMipIndex(Image image, int mipCount)
        {
            if (!image.HasMipmaps())
                image.GenerateMipmaps();

            int realMipCount = image.GetMipmapCount() + 1;
            return Mathf.Clamp(mipCount, 0, realMipCount - 1);
        }

        public static Color EncodeTilePath(int tileIndexX, int tileIndexY, int normalId, int mipIndex, int totalSubdivisions)
        {
            int tileIndex = totalSubdivisions * normalId + mipIndex;
            uint packed_indirection_index = (uint)(
                ((tileIndexX & 0xFF) << 24) |
                ((tileIndexY & 0xFF) << 16) |
                ((tileIndex & 0xFF) << 8)
            );

            Color encoded = new()
            {
                R = BitConverter.UInt32BitsToSingle(packed_indirection_index),
                G = BitConverter.UInt32BitsToSingle((uint)mipIndex),
                B = BitConverter.UInt32BitsToSingle((uint)normalId),
                A = BitConverter.UInt32BitsToSingle(255)
            };
            return encoded;
        }

        public static (int tileIndexX, int tileIndexY, int normalId, int mipIndex) DecodeTilePath(Color data)
        {
            uint decoded = BitConverter.SingleToUInt32Bits(data.R);
            uint tileIndexX = decoded >> 24 & 0xFF;
            uint tileIndexY = decoded >> 16 & 0xFF;
            uint z = (decoded >> 8) & 0xFF;

            uint mipIndex = BitConverter.SingleToUInt32Bits(data.G);
            uint normalId = BitConverter.SingleToUInt32Bits(data.B);
            return ((int)tileIndexX, (int)tileIndexY, (int)normalId, (int)mipIndex);
        }

        // public static Image GenerateNormalMap(Image heightmap)
        // {

        // }


        public static async Task GenerateMesh(Image image, int maxMipIndex, int padding)
        {
            Image[] mipmaps = GetMipmapSections(image, maxMipIndex);

            int processedTiles = 0;
            int totalTiles = GetTileCount(maxMipIndex);
            List<Task> tasks = [];

            for (int mipIndex = mipmaps.Length - 1; mipIndex >= 0; mipIndex--)
            {
                int tilesPerSide = (int)Mathf.Pow(2, mipmaps.Length - 1 - mipIndex);
                Image mipmap = mipmaps[mipIndex];

                for (int normalId = 0; normalId < 6; normalId++)
                {
                    for (int tileIndex = 0; tileIndex < tilesPerSide * tilesPerSide; tileIndex++)
                    {
                        int tileY = tileIndex / tilesPerSide;
                        int tileX = tileIndex % tilesPerSide;

                        TileGenerationParams parameters = new()
                        {
                            TileIndexX = tileX,
                            TileIndexY = tileY,
                            MipIndex = mipIndex,
                            NormalId = normalId,
                            Source = mipmap,
                            TilesPerSide = tilesPerSide,
                            TileSize = mipmap.GetSize().Y / tilesPerSide,
                            Padding = padding
                        };

                        tasks.Add(new Task(() =>
                        {
                            Image tile = GenerateTile(parameters);

                            int current = Interlocked.Increment(ref processedTiles);
                            tile.SavePng($"{parameters.Destination}\\{parameters.MipIndex}-{parameters.NormalId}-{parameters.TileIndexX}-{parameters.TileIndexY}.png");
                            string outputText = $"Processing Normal: {parameters.NormalId} at Mip: {parameters.MipIndex} for tile coords: ({parameters.TileIndexX}, {parameters.TileIndexY})";
                            OnTileGeneratedProgress?.Invoke(current, outputText, totalTiles);
                        }));
                    }
                }
            }

            Stopwatch stopwatch = new();
            stopwatch.Start();
            const int BATCH_SIZE = 32;
            for (int i = 0; i < tasks.Count; i += BATCH_SIZE)
            {
                List<Task> batch = [.. tasks.Skip(i).Take(BATCH_SIZE)];
                foreach (Task task in batch)
                {
                    task.Start();
                }
                await Task.WhenAll(batch);
            }
            stopwatch.Stop();
            GD.Print($"Done in: {stopwatch.Elapsed}");
        }
    }
}