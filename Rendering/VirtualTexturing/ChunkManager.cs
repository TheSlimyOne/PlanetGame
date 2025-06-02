using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PlanetGame.ComputeShaders.Dispatcher;
using Godot;

namespace PlanetGame.Rendering.VirtualTexturing
{
    // TODO check if image supplied is 2:1
    // TODO need to make sure the image isnt larger than 16k x 16k and if it is ask for it subdivided
    public class ChunkManager
    {
        public CalculateNormalsDispatcher CalculateNormalsDispatcher { get; private set; }

        private readonly List<Task> _tasks = [];
        public async Task CreateVirtualTextureTiles()
        {
            GD.PrintS($"Generating chunks: {_tasks.Count}");
            _tasks.ForEach(x => x.Start());
            await Task.WhenAll(_tasks);
            GD.Print(_tasks.Count);
            GD.PrintS("Chunk generation complete");
            _tasks.Clear();
        }

        public static void QueueVirtualTextureGeneration(int centerSize, int borderSize, Image[] cubeMap, string chunkDestination)
        {
            for (int normalId = 0; normalId < 6; normalId++)
            {
                Image image = cubeMap[normalId];
                if (image.GetSize().X != image.GetSize().Y)
                    throw new ArgumentException("The provided image must be 1:1.");
                if (image.IsCompressed())
                    image.Decompress();
                if (!image.HasMipmaps())
                    image.GenerateMipmaps();

                Image[] mipmaps = GetMipmaps(image, centerSize);

                for (int mipIndex = 0; mipIndex < mipmaps.Length; mipIndex++)
                {
                    int mipSize = mipmaps[mipIndex].GetSize().X;
                    for (int y = 0; y < mipSize; y += centerSize)
                    {
                        for (int x = 0; x < mipSize; x += centerSize)
                        {
                            int localX = x;
                            int localY = y;
                            int localMipIndex = mipIndex;
                            int localCenterSize = centerSize;
                            int localBorderSize = borderSize;
                            string name = $"{chunkDestination}/{mipIndex}-{normalId}-{x / centerSize}-{y / centerSize}.png";

                            // _tasks.Add(new Task(() =>
                            {
                                GenerateImageChunks
                                (
                                    localX, localY, name,
                                    localCenterSize,
                                    localBorderSize,
                                    mipmaps[localMipIndex]
                                );
                            }
                            // ));
                        }
                    }
                }
            }
        }

        private static void GenerateImageChunks(int x, int y, string destination, int centerSize, int borderSize, Image mipmap)
        {
            Image.Format format = mipmap.GetFormat();
            int fullSize = centerSize + 2 * borderSize;

            Image chunk = Image.CreateEmpty(fullSize, fullSize, false, format);
            chunk.Fill(new Color(0, 0, 0, 0));

            chunk.BlitRect(mipmap, new(x, y, centerSize, centerSize), new Vector2I(borderSize, borderSize));

            if (borderSize > 0)
            {
                Image leftSection = Image.CreateEmpty(borderSize, fullSize, false, format);
                Rect2I leftSectionChunkDim = new(x - borderSize, y - borderSize, borderSize, fullSize);
                leftSection.BlitRect(mipmap, leftSectionChunkDim, Vector2I.Zero);
                chunk.BlitRect(leftSection, new Rect2I(0, 0, leftSection.GetSize()), new Vector2I(0, 0));

                Image rightSection = Image.CreateEmpty(borderSize, fullSize, false, format);
                Rect2I rightSectionChunkDim = new(x + centerSize, y - borderSize, borderSize, fullSize);
                rightSection.BlitRect(mipmap, rightSectionChunkDim, Vector2I.Zero);
                chunk.BlitRect(rightSection, new Rect2I(0, 0, rightSection.GetSize()), new Vector2I(centerSize + borderSize, 0));

                Image downSection = Image.CreateEmpty(centerSize * borderSize, borderSize, false, format);
                Rect2I downSectionChunkDim = new(x, y + centerSize, centerSize * borderSize, borderSize);
                downSection.BlitRect(mipmap, downSectionChunkDim, Vector2I.Zero);
                chunk.BlitRect(downSection, new Rect2I(0, 0, downSection.GetSize()), new Vector2I(borderSize, centerSize + borderSize));

                Image upSection = Image.CreateEmpty(centerSize * borderSize, borderSize, false, format);
                Rect2I upSectionChunkDim = new(x, y - borderSize, centerSize * borderSize, borderSize);
                upSection.BlitRect(mipmap, upSectionChunkDim, Vector2I.Zero);
                chunk.BlitRect(upSection, new Rect2I(0, 0, upSection.GetSize()), new Vector2I(borderSize, 0));
            }

            chunk.SavePng(destination);
        }

        private static Image[] GetMipmaps(Image image, int target)
        {
            int bytesPerPixel = FormatConverter.GetBytes(image.GetFormat());
            List<Image> mipmaps = [];

            for (int i = 0; i < image.GetMipmapCount(); i++)
            {
                int size = image.GetSize().X / (int)Mathf.Pow(2, i);

                int mipOffset = (int)image.GetMipmapOffset(i);
                byte[] buffer = new byte[bytesPerPixel * size * size];
                Array.Copy(image.GetData(), mipOffset, buffer, 0, buffer.Length);

                Image mip = Image.CreateFromData(size, size, false, image.GetFormat(), buffer);
                mipmaps.Add(mip);

                if (size == target)
                    break;
            }
            return [.. mipmaps];
        }

        public static Image[] GenerateCubeMapFromImage(Image image, Image.Interpolation interpolation = Image.Interpolation.Bilinear)
        {
            Vector2I subTileParitionSize = new(16, 8);   
            CalculateCubeMapDispatcher calculateCubeMapDispatcher = new()
            {
                SubTileParitionSize = subTileParitionSize,
                TileSize = CalculateCubeMapDispatcher.GetTileSize(image, subTileParitionSize).X,
                BorderSize = 0
            };
            calculateCubeMapDispatcher.CreateUniforms();
            Image[] cubeMap = calculateCubeMapDispatcher.CreateCubeMap(image, interpolation: interpolation);
            calculateCubeMapDispatcher.CleanupGPU();
            return cubeMap;
        }
    }
}