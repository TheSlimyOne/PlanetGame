using Godot;
using PlanetGame.Planet.Rendering.Drawing;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using PlanetGame.Data;

namespace PlanetGame.Planet.Rendering.VirtualTexturing
{
    // TODO check if image supplied is 2:1
    // TODO need to make sure the image isnt larger than 16k x 16k and if it is ask for it subdivided
    public static class TileManager
    {
        private static VirtualTextureData VirtualTextureData => SaveManager.VirtualTextureData;

        // public static Image GetTileImage(string tileDirectory, string tileName, Image.Format format)
        // {
        //     string tilePath = GetTileImagePath(tileDirectory, tileName);
        //     Image tile = FileAccess.FileExists(tilePath) ? Image.LoadFromFile(tilePath) : null;

        //     if (tile == null)
        //     {
        //         GD.PrintErr($"Tile does not exist: {tilePath}");
        //         return null;
        //     }

        //     if (tile.GetFormat() != format)
        //         tile.Convert(format);

        //     if (tile.IsCompressed())
        //         tile.Decompress();

        //     return tile;
        // }

        public static bool TileImageExists(string tileDirectory, string tileName)
        {
            return FileAccess.FileExists(GetTileImagePath(tileDirectory, tileName));
        }

        public static string GetTileImagePath(string tileDirectory, string tileName)
        {
            return $"{tileDirectory}/{tileName}.png";
        }

        public static string[] GetAncestorTileNames(string tileName, bool includeSelf = true)
        {
            if (!VirtualTextureData.IsValidTileName(tileName))
                return [];

            string[] tileData = tileName.Split('_');

            uint mipIndex = uint.Parse(tileData[0]);
            uint normalId = uint.Parse(tileData[1]);
            uint tileX = uint.Parse(tileData[2]);
            uint tileY = uint.Parse(tileData[3]);

            int mipSize = VirtualTextureData.GetMipSize(mipIndex);

            List<string> names = [];

            if (includeSelf)
                names.Add(tileName);

            for (uint currentMip = mipIndex + 1; currentMip < VirtualTextureData.TotalMipLayersPerFace - 1; currentMip++)
            {
                int gridSize = VirtualTextureData.GetMipSize(currentMip);
                int scale = mipSize / gridSize;

                names.Add($"{currentMip}_{normalId}_{tileX / scale}_{tileY / scale}");
            }

            return [.. names.Distinct()];
        }

        public static async Task DrawCommandOnTile(DrawCommand drawCommand, TileCache tileCache, Callable callback)
        {
            // await Parallel.ForEachAsync(
            //     drawCommand.TileNameToBrushStroke,
            //     new ParallelOptions { MaxDegreeOfParallelism = 4 },
            //     async (data, _) =>
            //     {
            //         Tile tile = tileCache.GetTile(data.Key);
            //         // tile.Draw(data.Value);
            //     }
            // );

            // callback.Call();
        }
    }
}