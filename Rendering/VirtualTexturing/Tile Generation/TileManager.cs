using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PlanetGame.Shaders.Dispatchers;
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
                            GenerateTile(parameters);
                            int current = Interlocked.Increment(ref processedTiles);
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
            public Image.Format SourceFormat { get; set; }
            public int TilesPerSide { get; set; }
            public int TileSize { get; set; }
            public int Padding { get; set; }
            public string Destination { get; set; }
        }

        public static Image GenerateBlankTile(TileGenerationParams parameters)
        {
            int paddedSize = parameters.TileSize + 2 * parameters.Padding;
            Image tile = Image.CreateEmpty(paddedSize, paddedSize, false, parameters.SourceFormat);
            tile.Fill(new(0, 0, 0, 0));
            tile.SavePng($"{parameters.Destination}/{parameters.MipIndex}_{parameters.NormalId}_{parameters.TileIndexX}_{parameters.TileIndexY}.png");
            return tile;
        }

        public static Image GenerateTile(TileGenerationParams parameters)
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

            tile.SavePng($"{parameters.Destination}//{parameters.MipIndex}_{parameters.NormalId}-{parameters.TileIndexX}-{parameters.TileIndexY}.png");

            return tile;
        }

        public static int GetValidMipIndex(Image image, int mipCount)
        {
            if (!image.HasMipmaps())
                image.GenerateMipmaps();

            int realMipCount = image.GetMipmapCount() + 1;
            return Mathf.Clamp(mipCount, 0, realMipCount - 1);
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

        public static ArrayMesh GetPlanetMesh(int resolution, Image heightmap, float strength)
        {
            int faces = 6;
            Vector3[] vertices = new Vector3[faces * resolution * resolution];
            Vector3[] normals = new Vector3[faces * resolution * resolution];
            Vector2[] uvs = new Vector2[faces * resolution * resolution];
            int[] triangles = new int[faces * (resolution - 1) * (resolution - 1) * 6];

            int triIndex = 0;
            int vertexIndex = 0;

            for (int i = 0; i < faces; i++)
            {
                for (int y = 0; y < resolution; y++)
                {
                    for (int x = 0; x < resolution; x++)
                    {
                        int currentIndex = vertexIndex++;
                        Vector2 percentage = new Vector2(x, y) / (resolution - 1);
                        Vector3 cubePoint = VectorUtils.UVToPointOnCube(i, percentage);
                        Vector3 spherePoint = VectorUtils.PointOnCubeToPointOnSphere(cubePoint);
                        Vector2 uv = VectorUtils.PointOnSphereToUV(spherePoint);

                        Color pixel = Sampler.SampleBilinear(heightmap, uv);
                        Vector3 vertex = spherePoint + spherePoint.Normalized() * pixel.R * strength;

                        vertices[currentIndex] = vertex;
                        uvs[currentIndex] = uv;
                        normals[currentIndex] = Vector3.Zero;

                        if (x != resolution - 1 && y != resolution - 1)
                        {
                            bool isXEven = x % 2 == 0;
                            bool isYEven = y % 2 == 0;
                            if ((isXEven && isYEven) || (!isXEven && !isYEven))
                            {
                                triangles[triIndex++] = currentIndex;
                                triangles[triIndex++] = currentIndex + resolution + 1;
                                triangles[triIndex++] = currentIndex + resolution;

                                triangles[triIndex++] = currentIndex;
                                triangles[triIndex++] = currentIndex + 1;
                                triangles[triIndex++] = currentIndex + resolution + 1;
                            }
                            else
                            {
                                triangles[triIndex++] = currentIndex;
                                triangles[triIndex++] = currentIndex + 1;
                                triangles[triIndex++] = currentIndex + resolution;

                                triangles[triIndex++] = currentIndex + 1;
                                triangles[triIndex++] = currentIndex + resolution + 1;
                                triangles[triIndex++] = currentIndex + resolution;
                            }
                        }
                    }
                }
            }

            CalculateNormals(vertices, triangles, normals, resolution);

            Godot.Collections.Array arrays = [];
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = vertices;
            arrays[(int)Mesh.ArrayType.Index] = triangles;
            arrays[(int)Mesh.ArrayType.Normal] = normals;
            arrays[(int)Mesh.ArrayType.TexUV] = uvs;

            ArrayMesh triangleMesh = new();
            triangleMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            return triangleMesh;
        }


        private enum Direction
        {
            up, down, left, right,
            bottom_left, bottom_right, top_left, top_right,
            _
        }

        private static void CalculateNormals(Vector3[] vertices, int[] triangles, Vector3[] normals, int resolution)
        {
            (int faceA, int faceB, Direction directionA, Direction directionB, bool isReversedA, bool isReversedB)[] adjecencies = [
                (0, 2, Direction.down,  Direction.left,  false, false),
                (0, 3, Direction.up,    Direction.right, false, false),
                (0, 4, Direction.left,  Direction.right, false, false),
                (0, 5, Direction.right, Direction.left,  false, false),

                (1, 2, Direction.down,  Direction.right, true,  false),
                (1, 3, Direction.up,    Direction.left,  true,  false),
                (1, 4, Direction.right, Direction.left,  false, false),
                (1, 5, Direction.left,  Direction.right, false, false),

                (2, 4, Direction.down,  Direction.down,  false, true),
                (2, 5, Direction.up,    Direction.down,  false, false),
                (3, 4, Direction.down,  Direction.up,    true,  true),
                (3, 5, Direction.up,    Direction.up,    true,  false),
            ];

            (int faceA, int faceB, int faceC, Direction directionA, Direction directionB, Direction directionC)[] corners = [
                (0, 2, 4, Direction.bottom_left,  Direction.bottom_left,  Direction.bottom_right),
                (1, 2, 4, Direction.bottom_right, Direction.bottom_right, Direction.bottom_left),
                (1, 3, 4, Direction.top_right,    Direction.bottom_left,  Direction.top_left),
                (1, 3, 5, Direction.top_left,     Direction.top_left,     Direction.top_right),
                (1, 2, 5, Direction.bottom_left,  Direction.top_right,    Direction.bottom_right),
                (0, 2, 5, Direction.bottom_right, Direction.top_left,     Direction.bottom_left),
                (0, 3, 4, Direction.top_left,     Direction.bottom_right, Direction.top_right),
                (0, 3, 5, Direction.top_right,    Direction.top_right,    Direction.top_left),

            ];

            for (int i = 0; i < triangles.Length; i += 3)
            {
                int indexA = triangles[i];
                int indexB = triangles[i + 1];
                int indexC = triangles[i + 2];

                Vector3 edge1 = vertices[indexB] - vertices[indexA];
                Vector3 edge2 = vertices[indexC] - vertices[indexA];

                Vector3 faceNormal = edge2.Cross(edge1).Normalized();

                normals[indexA] += faceNormal;
                normals[indexB] += faceNormal;
                normals[indexC] += faceNormal;
            }

            static List<int> GetIndicesFromDirection(Direction direction, int resolution, bool isReversed)
            {
                IEnumerable<int> indices = direction switch
                {
                    Direction.up => Enumerable.Range(0, resolution).Select(value => resolution * (resolution - 1) + value),
                    Direction.down => Enumerable.Range(0, resolution),
                    Direction.left => Enumerable.Range(0, resolution).Select(value => value * resolution),
                    Direction.right => Enumerable.Range(0, resolution).Select(value => value * resolution + resolution - 1),
                    _ => []
                };

                indices = indices.Skip(1).SkipLast(1);
                return [.. isReversed ? indices.Reverse() : indices];
            }

            static int GetCornerIndex(Direction direction, int resolution)
            {
                int indices = direction switch
                {
                    Direction.bottom_left => 0,
                    Direction.bottom_right => resolution - 1,
                    Direction.top_left => resolution * (resolution - 1),
                    Direction.top_right => resolution * resolution - 1,

                    _ => -1
                };


                return indices;
            }

            foreach ((int faceA, int faceB, Direction directionA, Direction directionB, bool isReversedA, bool isReversedB) in adjecencies)
            {
                List<int> faceAIndices = GetIndicesFromDirection(directionA, resolution, isReversedA);
                List<int> faceBIndices = GetIndicesFromDirection(directionB, resolution, isReversedB);

                for (int i = 0; i < faceAIndices.Count; i++)
                {
                    int vertexIndexA = faceA * resolution * resolution + faceAIndices[i];
                    int vertexIndexB = faceB * resolution * resolution + faceBIndices[i];

                    Vector3 newNormal = normals[vertexIndexA] + normals[vertexIndexB];

                    normals[vertexIndexA] = newNormal;
                    normals[vertexIndexB] = newNormal;
                }
            }

            foreach ((int faceA, int faceB, int faceC, Direction directionA, Direction directionB, Direction directionC) in corners)
            {
                int localCornerAIndex = GetCornerIndex(directionA, resolution);
                int localCornerBIndex = GetCornerIndex(directionB, resolution);
                int localCornerCIndex = GetCornerIndex(directionC, resolution);

                int vertexIndexA = faceA * resolution * resolution + localCornerAIndex;
                int vertexIndexB = faceB * resolution * resolution + localCornerBIndex;
                int vertexIndexC = faceC * resolution * resolution + localCornerCIndex;

                Vector3 newNormal = normals[vertexIndexA] + normals[vertexIndexB] + normals[vertexIndexC];

                normals[vertexIndexA] = newNormal;
                normals[vertexIndexB] = newNormal;
                normals[vertexIndexC] = newNormal;
            }

            for (int i = 0; i < normals.Length; i++)
                normals[i] = normals[i].Normalized();

        }

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
                            tile.SavePng($"{parameters.Destination}//{parameters.MipIndex}-{parameters.NormalId}-{parameters.TileIndexX}-{parameters.TileIndexY}.png");
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