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

        public static int GetMipSize(uint mipIndex) => 1 << (int)mipIndex;

        /// <summary>
        /// Serializes this data to match the expected GPU buffer layout.
        /// </summary>
        /// <remarks>
        /// <code>
        /// layout(set = X, binding = X, std430) readonly buffer VirtualTextureData {
        ///     uint low_resolution_mip_count;
        ///     uint high_resolution_mip_count;
        ///     uint grid_size;
        ///     uint total_fallback_tiles;
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
                .. Utilities.ToBytes(LodToMipMap),
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