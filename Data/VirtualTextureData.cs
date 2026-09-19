using System;
using System.Linq;
using Godot;
using PlanetGame.Util;

namespace PlanetGame.Data
{
    public class VirtualTextureData : ISavable
    {
        public uint HighResolutionMipCount;
        public uint LowResolutionMipCount;
        public uint[] LodToMipMap = new uint[32];
        public string[] FallBackTiles = new string[6];

        public uint TotalMipLayersPerFace => LowResolutionMipCount + HighResolutionMipCount;
        public uint BaseGridSize => (uint)Mathf.Pow(2, TotalMipLayersPerFace - 1);
        public uint TotalMipLayers => TotalMipLayersPerFace * 6;

        public VirtualTextureData() { }

        public VirtualTextureData(uint lowResolutionMipCount, uint highResolutionMipCount, uint[] lodToMipMap, string[] fallBackTiles)
        {
            LowResolutionMipCount = lowResolutionMipCount;
            HighResolutionMipCount = highResolutionMipCount;
            LodToMipMap = lodToMipMap;

            FallBackTiles =
            [
                .. fallBackTiles
                    .OrderByDescending(x => uint.Parse(x.Split('_')[0]))
                    .ThenByDescending(x => uint.Parse(x.Split('_')[1]))
                    .ThenByDescending(x => uint.Parse(x.Split('_')[2]))
                    .ThenByDescending(x => uint.Parse(x.Split('_')[3]))
            ];
        }

        public int GetMipSize(uint mipIndex)
        {
            return 1 << (int)mipIndex;
        }

        public bool IsValidTileName(string name)
        {
            string[] tileData = name.Split('_');
            if (tileData.Length != 4)
                return false;

            if (!uint.TryParse(tileData[0], out uint mipIndex)) return false;
            if (!uint.TryParse(tileData[1], out uint normalId)) return false;
            if (!uint.TryParse(tileData[2], out uint tileX)) return false;
            if (!uint.TryParse(tileData[3], out uint tileY)) return false;

            if (normalId < 0 || normalId >= 6) return false;

            if (mipIndex < 0 || mipIndex > TotalMipLayersPerFace)
                return false;

            int mipSize = GetMipSize(mipIndex);

            if (tileX < 0 || tileX >= mipSize) return false;
            if (tileY < 0 || tileY >= mipSize) return false;

            return true;
        }

        public (uint mipIndex, uint normalId, uint xIndex, uint yIndex) GetTileName(string name)
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

        /// <summary>
        /// Serializes this data to match the expected GPU buffer layout.
        /// </summary>
        /// <remarks>
        /// <code>
        /// layout(std430, binding = X) readonly buffer VirtualTextureData {
        ///     uint low_resolution_mip_count;
        ///     uint high_resolution_mip_count;
        ///     uint grid_size;
        ///     uint total_fallback_tiles;
        ///
        ///     uint lod_to_mip_map[32];
        /// };
        /// </code>
        /// </remarks>
        public byte[] ToBytes()
        {
            return [
                .. Utilities.ToBytesSingle(LowResolutionMipCount),
                .. Utilities.ToBytesSingle(HighResolutionMipCount),
                .. Utilities.ToBytesSingle(BaseGridSize),
                .. Utilities.ToBytesSingle(FallBackTiles.Length),

                .. Utilities.ToBytes<uint>(LodToMipMap),
            ];
        }

        public override string ToString()
        {
            return $"""
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