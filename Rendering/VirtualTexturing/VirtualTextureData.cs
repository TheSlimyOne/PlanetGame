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

        public uint TotalSubdivisions => LowResolutionMipCount + HighResolutionMipCount;
        public uint GridSize => (uint)Mathf.Pow(2, TotalSubdivisions - 1);
        public uint TotalMipLayers => TotalSubdivisions * 6;

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

        public uint GetNonNegativeMip(int mip)
        {
            return (uint)(mip + HighResolutionMipCount);
        }
        public int GetNegativeMip(uint nonNegativeMip)
        {
            return (int)(nonNegativeMip - HighResolutionMipCount);
        }

        public float GetMipGridSize(uint nonNegativeMipIndex)
        {
            return GridSize / Mathf.Pow(2, nonNegativeMipIndex);
        }

        public override string ToString()
        {
            return $"""
            TileSize: {TileSize}
            LowResolutionMipCount: {LowResolutionMipCount}
            HighResolutionMipCount: {HighResolutionMipCount}
            TotalSubdivisions: {TotalSubdivisions}
            GridSize: {GridSize}
            TotalMipLayers: {TotalMipLayers}
            LodToMipMap: [{string.Join(", ", LodToMipMap ?? [])}]
            FallBackTiles: [{string.Join(", ", FallBackTiles ?? [])}]
            """;
        }
    }
}