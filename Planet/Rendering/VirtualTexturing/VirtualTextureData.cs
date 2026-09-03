using System;
using System.Linq;
using Godot;

namespace PlanetGame.Rendering.VirtualTexturing
{
    public class VirtualTextureData
    {
        public uint TileSize;
        public uint HighResolutionMipCount;
        public uint LowResolutionMipCount;
        public int[] LodToMipMap = new int[32];
        public string[] FallBackTiles = new string[6];

        public uint TotalMipLayersPerFace => LowResolutionMipCount + HighResolutionMipCount;
        public uint BaseGridSize => (uint)Mathf.Pow(2, TotalMipLayersPerFace - 1);
        public uint TotalMipLayers => TotalMipLayersPerFace * 6;

        public VirtualTextureData() { }

        public VirtualTextureData(uint tileSize, uint lowResolutionMipCount, uint highResolutionMipCount, int[] lodToMipMap, string[] fallBackTiles)
        {
            TileSize = tileSize;
            LowResolutionMipCount = lowResolutionMipCount;
            HighResolutionMipCount = highResolutionMipCount;
            LodToMipMap = lodToMipMap;

            FallBackTiles =
            [
                .. fallBackTiles
                    .OrderByDescending(x => int.Parse(x.Split('_')[0]))
                    .ThenByDescending(x => int.Parse(x.Split('_')[1]))
                    .ThenByDescending(x => int.Parse(x.Split('_')[2]))
                    .ThenByDescending(x => int.Parse(x.Split('_')[3]))
            ];
        }

        public uint GetMipIndex(int mip)
        {
            return (uint)(mip + HighResolutionMipCount);
        }

        public int GetRealMipIndex(uint mipIndex)
        {
            return (int)(mipIndex - HighResolutionMipCount);
        }

        public int GetMipSize(uint mipIndex)
        {
            return (int)(BaseGridSize / Mathf.Pow(2, mipIndex));
        }

        public bool IsValidTileName(string tileName)
        {
            string[] tileData = tileName.Split('_');
            if (tileData.Length != 4)
                return false;

            if (!int.TryParse(tileData[0], out int realMipIndex)) return false;
            if (!int.TryParse(tileData[1], out int normalId)) return false;
            if (!int.TryParse(tileData[2], out int tileX)) return false;
            if (!int.TryParse(tileData[3], out int tileY)) return false;

            if (normalId < 0 || normalId >= 6) return false;

            if (realMipIndex < 0 && Mathf.Abs(realMipIndex) > HighResolutionMipCount)
                return false;

            if (realMipIndex >= 0 && realMipIndex >= LowResolutionMipCount)
                return false;

            uint mipIndex = GetMipIndex(realMipIndex);
            int mipSize = GetMipSize(mipIndex);

            if (tileX < 0 || tileX >= mipSize) return false;
            if (tileY < 0 || tileY >= mipSize) return false;

            return true;
        }

        public override string ToString()
        {
            return $"""
            TileSize: {TileSize}
            LowResolutionMipCount: {LowResolutionMipCount}
            HighResolutionMipCount: {HighResolutionMipCount}
            TotalMipLayersPerFace: {TotalMipLayersPerFace}
            BaseGridSize: {BaseGridSize}
            TotalMipLayers: {TotalMipLayers}
            LodToMipMap: [{string.Join(", ", LodToMipMap ?? [])}]
            FallBackTiles: [{string.Join(", ", FallBackTiles ?? [])}]
            """;
        }
    }
}