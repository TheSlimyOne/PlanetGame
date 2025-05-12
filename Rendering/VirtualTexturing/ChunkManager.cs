using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PlanetGame.ComputeShaders.Dispatcher;
using Godot;
namespace PlanetGame.Rendering.VirtualTexturing
{
    // TODO check if image supplied is 2:1
    // TODO need to make sure the image isnt larger than 16k x 16k and if it is ask for it subdivided
    public class ChunkManager(Vector2I textureSize, int desiredChunkSize, int centerSize, int borderSize)
    {
        public int DesiredChunkSize { get; private set; } = desiredChunkSize;
        public int CenterSize { get; private set; } = centerSize;
        public int BorderSize { get; private set; } = borderSize;

        public Vector2I TextureSize { get; private set; } = textureSize;

        public CalculateCubeMapDispatcher CalculateCubeMapDispatcher  { get; private set; }

        public void CreateCubeMapDispatcher()
        {
            CalculateCubeMapDispatcher = new()
            {
                TileAmount = 8,
                TileSize = 4096
            };
            CalculateCubeMapDispatcher.CreateUniforms();
        }

        private readonly List<Task> _tasks = [];
        public async Task CreateChunks()
        {
            GD.PrintS($"Generating chunks: {_tasks.Count}");
            _tasks.ForEach(x => x.Start());
            await Task.WhenAll(_tasks);
            GD.Print(_tasks.Count);
            GD.PrintS("Chunk generation complete");
            _tasks.Clear();
        }

        private void GenerateImageChunkFromCubeMap(string cubeMapPath, string chunkDestination, int normalID)
        {
            Image image = LoadImage(cubeMapPath);
            if (image.IsCompressed())
                image.Decompress();
            if (!image.HasMipmaps())
                image.GenerateMipmaps();

            Image[] mipmaps = GetMipmaps(image, DesiredChunkSize);

            int centerChunkSize = DesiredChunkSize;
            int borderPixelSize = DesiredChunkSize / CenterSize * BorderSize;

            for (int mipIndex = 0; mipIndex < mipmaps.Length; mipIndex++)
            {
                Vector2I mipSize = mipmaps[mipIndex].GetSize();
                for (int y = 0; y < mipSize.Y; y += centerChunkSize)
                {
                    for (int x = 0; x < mipSize.X; x += centerChunkSize)
                    {
                        int localX = x;
                        int localY = y;
                        int localMipIndex = mipIndex;
                        int localCenterSize = centerChunkSize;
                        int localBorderPixelSize = borderPixelSize;
                        string name = $"{chunkDestination}/{mipIndex}-{normalID}-{x / centerChunkSize}-{y / centerChunkSize}.png";

                        _tasks.Add(new Task(() =>
                        {
                            GenerateImageChunks
                            (
                                localX, localY, name,
                                localCenterSize,
                                localBorderPixelSize,
                                mipmaps[localMipIndex]
                            );
                        }));
                    }
                }
            }
        }

        private void GenerateImageChunks(int x, int y, string destination, int centerChunkSize, int borderPixelSize, Image mipmap)
        {
            Image.Format format = mipmap.GetFormat();
            int fullSize = centerChunkSize + 2 * borderPixelSize;

            Image chunk = Image.CreateEmpty(fullSize, fullSize, false, format);
            chunk.Fill(new Color(0, 0, 0, 0));

            chunk.BlitRect(mipmap, new(x, y, centerChunkSize, centerChunkSize), new Vector2I(borderPixelSize, borderPixelSize));

            if (borderPixelSize > 0)
            {
                Image leftSection = Image.CreateEmpty(borderPixelSize, fullSize, false, format);
                Rect2I leftSectionChunkDim = new(x - borderPixelSize, y - borderPixelSize, borderPixelSize, fullSize);
                leftSection.BlitRect(mipmap, leftSectionChunkDim, Vector2I.Zero);
                chunk.BlitRect(leftSection, new Rect2I(0, 0, leftSection.GetSize()), new Vector2I(0, 0));

                Image rightSection = Image.CreateEmpty(borderPixelSize, fullSize, false, format);
                Rect2I rightSectionChunkDim = new(x + centerChunkSize, y - borderPixelSize, borderPixelSize, fullSize);
                rightSection.BlitRect(mipmap, rightSectionChunkDim, Vector2I.Zero);
                chunk.BlitRect(rightSection, new Rect2I(0, 0, rightSection.GetSize()), new Vector2I(centerChunkSize + borderPixelSize, 0));

                Image downSection = Image.CreateEmpty(CenterSize * borderPixelSize, borderPixelSize, false, format);
                Rect2I downSectionChunkDim = new(x, y + centerChunkSize, CenterSize * borderPixelSize, borderPixelSize);
                downSection.BlitRect(mipmap, downSectionChunkDim, Vector2I.Zero);
                chunk.BlitRect(downSection, new Rect2I(0, 0, downSection.GetSize()), new Vector2I(borderPixelSize, centerChunkSize + borderPixelSize));

                Image upSection = Image.CreateEmpty(CenterSize * borderPixelSize, borderPixelSize, false, format);
                Rect2I upSectionChunkDim = new(x, y - borderPixelSize, CenterSize * borderPixelSize, borderPixelSize);
                upSection.BlitRect(mipmap, upSectionChunkDim, Vector2I.Zero);
                chunk.BlitRect(upSection, new Rect2I(0, 0, upSection.GetSize()), new Vector2I(borderPixelSize, 0));
            }

            chunk.SavePng(destination);
        }

        private Image[] GetMipmaps(Image image, int target)
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

        private Image LoadImage(string path)
        {
            Image image = Image.LoadFromFile(path);
            return image;
        }

        public Image LoadTile(string path)
        {
            Image image = Image.LoadFromFile(path);
            return image;
        }
    
        public void QueueGenerateChunksFromImage(string rootPath, string fileBaseName, string sourceImagePath, string chunkDestinationPath, string cubeMapDestinationPath, Image.Interpolation interpolation = Image.Interpolation.Bilinear)
        {
            if (CalculateCubeMapDispatcher == null)
                CreateCubeMapDispatcher();


            DirAccess directory = DirAccess.Open(rootPath);

            if (DirAccess.Open(cubeMapDestinationPath) == null)
                directory.MakeDir(cubeMapDestinationPath);

            if (DirAccess.Open(cubeMapDestinationPath) == null)
                directory.MakeDir(cubeMapDestinationPath);

            sourceImagePath = $"{rootPath}/{sourceImagePath}";
            cubeMapDestinationPath = $"{rootPath}/{cubeMapDestinationPath}";
            chunkDestinationPath = $"{rootPath}/{chunkDestinationPath}";

            CalculateCubeMapDispatcher.CreateCubeMaps(LoadImage(sourceImagePath), fileBaseName, cubeMapDestinationPath, interpolation: interpolation);

            for (int i = 0; i < 6; i++)
            {
                GenerateImageChunkFromCubeMap($"{cubeMapDestinationPath}/{fileBaseName}-{i}.png", chunkDestinationPath, i);
            }
        }

        public void CleanupGPUResources()
        {
            CalculateCubeMapDispatcher.CleanupGPU();
        }
    
    }
}