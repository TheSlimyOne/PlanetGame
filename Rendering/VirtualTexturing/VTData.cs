using Godot;
namespace PlanetGame.Rendering.VirtualTexturing
{
    public readonly struct VTData
    (
        uint tileSize, 
        uint LowResolutionMipCount, 
        uint highResolutionMipCount, 
        int[] lodToMipMap,
        string[] fallBackTiles
    )
    {
        public readonly uint TileSize = tileSize;
        public readonly uint TotalSubdivisions = LowResolutionMipCount + highResolutionMipCount;
        public readonly uint GridSize = (uint)Mathf.Pow(2, LowResolutionMipCount + highResolutionMipCount - 1);
        public readonly uint TotalMipLayers = (LowResolutionMipCount + highResolutionMipCount) * 6;
        public readonly int[] LodToMipMap = lodToMipMap;
        public readonly uint HighResolutionMipCount = highResolutionMipCount;
        public readonly uint LowResolutionMipCount = LowResolutionMipCount;
        public readonly string[] FallBackTiles = fallBackTiles;
    }
}