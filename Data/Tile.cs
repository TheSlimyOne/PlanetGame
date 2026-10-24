using System;
using System.Collections.Generic;
using Godot;

namespace PlanetGame.Data
{
    public class Tile(uint value)
    {
        public const int TILE_SIZE = 256;
        public const int ENCODING_BITS = 11;
        private const uint ENCODING_MASK = (1u << ENCODING_BITS) - 1;

        public readonly uint Value = value;

        public Tile(uint normalId, uint encoding) : this((normalId << ENCODING_BITS) | encoding) { }

        public Tile(uint mipIndex, uint normalId, uint xIndex, uint yIndex) : this(normalId, GetTileEncoding(mipIndex, xIndex, yIndex)) { }

        public Tile(string name) : this(GetValue(name)) { }

        public static Tile GetTileByIndex(uint index, uint tileCount)
        {
            uint normalId = index * 6 / tileCount;
            uint normalizedIndex = index % (tileCount / 6);

            int mipIndex = (int)(Mathf.Log(3 * normalizedIndex + 1) / Mathf.Log(4));
            uint rangeStart = 1u << (mipIndex * 2);
            uint previousCount = (rangeStart - 1) / 3;

            uint encoding = normalizedIndex - previousCount + rangeStart;

            Tile tile = new(normalId, encoding);
            return tile;
        }

        public uint NormalId => Value >> ENCODING_BITS;
        public uint Encoding => Value & ENCODING_MASK;
        public uint MipIndex => (uint)(FindMSB(Encoding) / 2);
        public float Size => 2 * WorldData.Radius * Mathf.Pow(2, (int)MipIndex - ((int)VirtualTextureData.TotalMipLayersPerFace - 1));

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

        public static bool IsValidTileName(string name)
        {
            string[] tileData = name.Split('_');
            if (tileData.Length != 4)
                return false;

            if (!uint.TryParse(tileData[0], out uint mipIndex)) return false;
            if (!uint.TryParse(tileData[1], out uint normalId)) return false;
            if (!uint.TryParse(tileData[2], out uint tileX)) return false;
            if (!uint.TryParse(tileData[3], out uint tileY)) return false;

            if (normalId >= 6) return false;

            if (mipIndex >= VirtualTextureData.TotalMipLayersPerFace)
                return false;

            int mipSize = VirtualTextureData.GetMipSize(mipIndex);

            if (tileX >= mipSize) return false;
            if (tileY >= mipSize) return false;

            return true;
        }

        public static (uint mipIndex, uint normalId, uint xIndex, uint yIndex) GetTileData(string name)
        {
            if (!IsValidTileName(name))
                throw new Exception($"Tile name {name} is not valid");

            string[] tileData = name.Split('_');

            uint mipIndex = uint.Parse(tileData[0]);
            uint normalId = uint.Parse(tileData[1]);
            uint xIndex = uint.Parse(tileData[2]);
            uint yIndex = uint.Parse(tileData[3]);

            return (mipIndex, normalId, xIndex, yIndex);
        }

        public static uint GetTileEncoding(uint mipIndex, uint xIndex, uint yIndex)
        {
            if (mipIndex == 0)
                return 1;

            uint child = (xIndex % 2) | ((yIndex % 2) << 1);

            int mipSize = VirtualTextureData.GetMipSize(mipIndex);
            int baseValue = mipSize * mipSize;

            uint offset = GetTileEncoding(mipIndex - 1, xIndex / 2, yIndex / 2);
            uint parentBaseValue = (uint)VirtualTextureData.GetMipSize(mipIndex - 1);
            parentBaseValue *= parentBaseValue;

            return (uint)baseValue + (offset - parentBaseValue) * 4 + child;
        }

        public Vector2I GetTileCoordinate()
        {
            uint encoding = Encoding;
            uint mipIndex = MipIndex;

            if (mipIndex == 0)
                return Vector2I.Zero;

            uint child = encoding % 4;

            uint parentEncoding = encoding >> 2;
            Vector2I parent = new Tile(NormalId, parentEncoding).GetTileCoordinate();

            int x = parent.X * 2 + (int)(child & 1u);
            int y = parent.Y * 2 + (int)(child >> 1);

            return new(x, y);
        }

        public string GetTileName()
        {
            Vector2I tileCoordinate = GetTileCoordinate();
            return $"{MipIndex}_{NormalId}_{tileCoordinate.X}_{tileCoordinate.Y}";
        }

        public static Tile GetTile(PlanetQuery.PlanetSurfacePoint surfacePoint)
        {
            uint mipIndex = surfacePoint.MipIndex;
            float mipGridSize = VirtualTextureData.GetMipSize(mipIndex);
            Vector2I tileCoords = (Vector2I)(surfacePoint.UV * mipGridSize).Floor();

            return new Tile(mipIndex, surfacePoint.NormalId, (uint)tileCoords.X, (uint)tileCoords.Y);
        }

        public Tile GetRootTile() => new(0, NormalId, 0, 0);

        public Tile[] GetLineage()
        {
            if (MipIndex == 0)
                return [this];

            return [this, .. GetParentTile().GetLineage()];
        }

        public Tile GetParentTile()
        {
            if (MipIndex == 0)
                throw new InvalidOperationException("Cannot get the parent of a root tile.");

            return new Tile(NormalId, Encoding >> 2);
        }

        public Tile[] GetChildTiles()
        {
            if (((Encoding << 2) | 3u) >= (1u << ENCODING_BITS))
                throw new InvalidOperationException($"Cannot subdivide tile {this}: maximum mip reached.");

            return [
                new Tile(NormalId, (Encoding << 2) | 0u),
                new Tile(NormalId, (Encoding << 2) | 1u),
                new Tile(NormalId, (Encoding << 2) | 2u),
                new Tile(NormalId, (Encoding << 2) | 3u)
            ];
        }


        public static void Traverse(Node node, Tile tile, Rect3 bounds, uint targetMip, HashSet<Tile> tiles)
        {
            bool intersects = tile.GetTileBounds3D().Intersects(bounds);

            // GD.Print(new string(' ', (int)tile.MipIndex), $"{tile} of bounds {tile.GetTileBounds3D()} Intersects: {intersects} of bounds {bounds}");
            if (!intersects)
                return;

            if (tile.MipIndex == targetMip)
            {
                tiles.Add(tile);
                return;
            }

            if (tile.MipIndex > targetMip)
            {
                Traverse(node, tile.GetParentTile(), bounds, targetMip, tiles);
            }
            else
            {
                foreach (Tile child in tile.GetChildTiles())
                    Traverse(node, child, bounds, targetMip, tiles);
            }
        }

        public Rect3 GetTileBounds3D()
        {
            Vector2 coordinates = GetTileCoordinate();

            float mipGridSize = VirtualTextureData.GetMipSize(MipIndex);

            Vector2 min = coordinates / mipGridSize;
            Vector2 max = (coordinates + Vector2.One) / mipGridSize;

            Vector3 a = WorldData.LocalToPlanet(VectorUtils.PointOnPlaneToPointOnCube(new Vector2(min.X, min.Y), (int)NormalId));
            Vector3 b = WorldData.LocalToPlanet(VectorUtils.PointOnPlaneToPointOnCube(new Vector2(max.X, min.Y), (int)NormalId));
            Vector3 c = WorldData.LocalToPlanet(VectorUtils.PointOnPlaneToPointOnCube(new Vector2(min.X, max.Y), (int)NormalId));
            Vector3 d = WorldData.LocalToPlanet(VectorUtils.PointOnPlaneToPointOnCube(new Vector2(max.X, max.Y), (int)NormalId));

            Vector3 minimum = a.Min(b).Min(c).Min(d);
            Vector3 maximum = a.Max(b).Max(c).Max(d);

            return new Rect3(minimum, maximum - minimum);
        }

        public bool IsRectInBounds(Rect3 bounds) => GetTileBounds3D().Intersects(bounds);

        public bool IsRectOutBounds(Rect3 bounds) => !GetTileBounds3D().Intersects(bounds);

        private static VirtualTextureData VirtualTextureData => SaveManager.VirtualTextureData;
        private static WorldData WorldData => SaveManager.WorldData;

        public bool IsResident;
        public bool IsDirty { get; private set; } = false;

        public readonly TileMipType TileType;

        public enum TileMipType
        {
            Base,
            Detail
        }

        private static uint GetValue(string name)
        {
            (uint mipIndex, uint normalId, uint xIndex, uint yIndex) = GetTileData(name);
            return (normalId << ENCODING_BITS) | GetTileEncoding(mipIndex, xIndex, yIndex);
        }

        public static string GetTileName(PlanetQuery.PlanetSurfacePoint surfacePoint)
        {
            uint mipIndex = surfacePoint.MipIndex;
            int gridSize = VirtualTextureData.GetMipSize(mipIndex);
            Vector2I tileCoordinates = (Vector2I)(surfacePoint.UV * gridSize).Floor();

            return $"{mipIndex}_{surfacePoint.NormalId}_{tileCoordinates.X}_{tileCoordinates.Y}";
        }

        public void Draw(Planet.Rendering.Drawing.DrawCommand.BrushStroke stroke, Tile tile, FileAccess file)
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

        public uint GetTileIndex(uint tileCount)
        {
            uint pow4 = 1u << (int)(MipIndex * 2);
            uint normalizedIndex = Encoding - (2 * pow4 + 1) / 3;
            uint normalIdOffset = tileCount / 6 * NormalId;

            return normalizedIndex + normalIdOffset;
        }

        public static uint GetTileIndex(uint mipIndex, uint normalId, uint xIndex, uint yIndex, uint tileCount)
        {
            uint pow4 = 1u << ((int)mipIndex * 2);
            uint encoding = GetTileEncoding(mipIndex, xIndex, yIndex);

            uint normalizedIndex = encoding - (2 * pow4 + 1) / 3;
            uint normalIdOffset = tileCount / 6 * normalId;

            return normalizedIndex + normalIdOffset;
        }

        public override string ToString() => GetTileName();
        public override int GetHashCode() => Value.GetHashCode();
        public override bool Equals(object obj) => obj is Tile other && Value == other.Value;
    }
}