using System;
using Godot;
using PlanetGame.Planet.Rendering.Drawing;

namespace PlanetGame.Data
{
    public class Tile
    {
        public const int TILE_SIZE = 256;

        public readonly record struct TileID(uint Value)
        {
            public const int ENCODING_BITS = 11;
            private const uint ENCODING_MASK = (1u << ENCODING_BITS) - 1;

            public TileID(uint normalId, uint encoding) : this((normalId << ENCODING_BITS) | encoding) { }

            public uint NormalId => Value >> ENCODING_BITS;
            public uint Encoding => Value & ENCODING_MASK;
            public int Mip => FindMSB(Encoding) / 2;

            private static int FindMSB(uint n)
            {
                int msb = 0;

                while (n > 1)
                {
                    n >>= 1;
                    msb++;
                }

                return msb;
            }

            public Vector2I GetTileCoordinate()
            {
                uint encoding = Encoding;
                int depth = FindMSB(encoding) / 2;

                uint rangeStart = 1u << (depth * 2);
                uint local = encoding - rangeStart;

                int x = (int)(local >> depth);
                int y = (int)(local & ((1u << depth) - 1));

                return new(x, y);
            }

            public static uint GetTileEncoding(uint mipIndex, uint xIndex, uint yIndex)
            {
                uint rangeStart = 1u << ((int)mipIndex * 2);
                uint local = (xIndex << (int)mipIndex) | yIndex;

                return rangeStart + local;
            }

            public string GetTileName()
            {
                Vector2I tileCoordinate = GetTileCoordinate();
                return $"{Mip}-{NormalId}-{tileCoordinate.X}-{tileCoordinate.Y}";
            }

            public override string ToString()
            {
                return $"Normal ID: {NormalId} Encoding: {Convert.ToString(Encoding, 2).PadZeros(ENCODING_BITS)}";
                // return GetTileName();
            }
        }

        private static VirtualTextureData VirtualTextureData => SaveManager.VirtualTextureData;

        public bool IsResident;
        // public uint? Slot { get; set; }
        public bool IsDirty { get; private set; } = false;

        public readonly TileMipType TileType;

        private readonly TileID _id;

        public uint NormalId => _id.NormalId;
        public uint Encoding => _id.Encoding;
        public int MipIndex => _id.Mip;
        public uint Value => _id.Value;

        public enum TileMipType
        {
            Base,
            Detail
        }

        public Tile(uint normalId, uint encoding)
        {
            _id = new(normalId, encoding);
        }

        public Tile(uint fullEncoding)
        {
            _id = new(fullEncoding);
        }

        public Tile(uint mipIndex, uint normalId, uint xIndex, uint yIndex)
        {
            _id = new(normalId, TileID.GetTileEncoding(mipIndex, xIndex, yIndex));
        }

        public Tile(string name)
        {
            (uint mipIndex, uint normalId, uint xIndex, uint yIndex) = VirtualTextureData.GetTileName(name);

            _id = new(normalId, TileID.GetTileEncoding(mipIndex, xIndex, yIndex));
        }

        public static string GetTileName(PlanetQuery.PlanetSurfacePoint surfacePoint)
        {
            uint mipIndex = surfacePoint.MipIndex;
            int gridSize = VirtualTextureData.GetMipSize(mipIndex);
            Vector2I tileCoordinates = (Vector2I)(surfacePoint.UV * gridSize).Floor();

            return $"{mipIndex}_{surfacePoint.NormalId}_{tileCoordinates.X}_{tileCoordinates.Y}";
        }

        // public Tile(string tileName, uint? slot, TileCache tileCache, TileMipType tileType)
        // {
        //     TileName = tileName;

        //     TileCache = tileCache;

        //     Slot = slot;
        //     TileType = tileType;
        // }

        public override bool Equals(object obj)
        {
            return obj is Tile other && _id == other._id;
        }

        public override int GetHashCode()
        {
            return _id.GetHashCode();
        }

        public void Draw(DrawCommand.BrushStroke stroke, Tile tile, FileAccess file)
        {
            // Image image = GetImage(file);

            // string[] tileData = TileName.Split('_');

            // int realMipIndex = int.Parse(tileData[0]);
            // int tileX = int.Parse(tileData[2]);
            // int tileY = int.Parse(tileData[3]);

            // uint mipIndex = VirtualTextureData.GetMipIndex(realMipIndex);
            // int gridSize = VirtualTextureData.GetMipSize(mipIndex);

            // float brushSize = stroke.Brush.Size / Mathf.Pow(2, realMipIndex);
            // int radius = Mathf.CeilToInt(brushSize);
            // float opacity = stroke.Brush.Opacity;
            // float hardness = stroke.Brush.Hardness;

            // foreach (Vector2 point in stroke.Points)
            // {
            //     Vector2 tileUv = point * gridSize - new Vector2(tileX, tileY);
            //     Vector2 origin = tileUv * image.GetWidth();

            //     int minX = Mathf.Max(0, Mathf.FloorToInt(origin.X) - radius);
            //     int maxX = Mathf.Min(image.GetWidth() - 1, Mathf.CeilToInt(origin.X) + radius);
            //     int minY = Mathf.Max(0, Mathf.FloorToInt(origin.Y) - radius);
            //     int maxY = Mathf.Min(image.GetHeight() - 1, Mathf.CeilToInt(origin.Y) + radius);

            //     for (int y = minY; y <= maxY; y++)
            //     {
            //         for (int x = minX; x <= maxX; x++)
            //         {
            //             Vector2 pixelPosition = new(x, y);
            //             Vector2 brushPosition = (pixelPosition - origin) / brushSize;

            //             float distance = brushPosition.Length();

            //             if (distance > 1.0f)
            //                 continue;

            //             Color brushColor = stroke.Brush.Sample(brushPosition);

            //             if (brushColor.A <= 0.0f)
            //                 continue;

            //             float hardnessOpacity = 1.0f;

            //             if (distance > hardness)
            //                 hardnessOpacity = 1.0f - ((distance - hardness) / (1.0f - hardness));

            //             float finalOpacity = opacity * hardnessOpacity * brushColor.A;

            //             if (finalOpacity <= 0.0f)
            //                 continue;

            //             Color currentColor = image.GetPixel(x, y);
            //             Color finalColor = currentColor.Lerp(brushColor, finalOpacity);

            //             image.SetPixel(x, y, finalColor);
            //         }
            //     }
            // }

            // SaveImage(image);
        }

        public override string ToString()
        {
            Vector2 coordinate = GetTileCoordinate();
            return $"{MipIndex}-{NormalId}-{coordinate.X}-{coordinate.Y}";
        }

        public Vector2I GetTileCoordinate() => _id.GetTileCoordinate();

        public uint GetTileIndex(uint tileCount)
        {
            uint pow4 = 1u << (MipIndex * 2);
            uint normalizedIndex = Encoding - (2 * pow4 + 1) / 3;
            uint normalIdOffset = tileCount / 6 * NormalId;

            return normalizedIndex + normalIdOffset;
        }

        public static uint GetTileIndex(uint mipIndex, uint normalId, uint xIndex, uint yIndex, uint tileCount)
        {
            uint pow4 = 1u << ((int)mipIndex * 2);
            uint encoding = TileID.GetTileEncoding(mipIndex, xIndex, yIndex);

            uint normalizedIndex = encoding - (2 * pow4 + 1) / 3;
            uint normalIdOffset = tileCount / 6 * normalId;

            return normalizedIndex + normalIdOffset;
        }

        public static Tile GetTileByIndex(uint index, uint tileCount)
        {
            uint normalId = index * 6 / tileCount;
            uint normalizedIndex = index % (tileCount / 6);

            int mipIndex = (int)(Mathf.Log(3 * normalizedIndex + 1) / Mathf.Log(4));
            uint rangeStart = 1u << (mipIndex * 2);
            uint previousCount = (rangeStart - 1) / 3;

            uint encoding = index - previousCount + rangeStart;

            Tile tile = new(normalId, encoding);
            Vector2 a = tile.GetTileCoordinate();

            uint encoding2 = TileID.GetTileEncoding((uint)tile.MipIndex, (uint)a.X, (uint)a.Y);
            GD.PrintS(encoding, encoding2);
            

            return tile;
        }
    }
}