using System;
using System.Collections.Generic;
using Godot;
using PlanetGame.Rendering.Surface;
using PlanetGame.Rendering.VirtualTexturing;
using PlanetGame.Rendering.VirtualTexturing.Drawing;

namespace PlanetGame.Rendering.VirtualTexturing
{
    public class Tile
    {

        private static VirtualTextureData VirtualTextureData => SaveManager.CurrentWorldSave.VirtualTextureData;
        private static TessellationData TessellationData => SaveManager.CurrentWorldSave.TessellationData;

        public string TileName { get; private set; }
        public Image Image;
        public bool IsResident;
        public uint? Slot { get; set; }
        public bool IsDirty { get; private set; } = false;

        public readonly TileMipType TileType;
        public readonly TileCache TileCache;

        public enum TileMipType
        {
            Base,
            Detail
        }

        public Tile(string tileName, uint? slot, TileCache tileCache, TileMipType tileType)
        {
            TileName = tileName;
            Image = tileCache.GetTileImage(tileName);

            TileCache = tileCache;

            Slot = slot;
            TileType = tileType;
        }

        public Tile(string tileName, Image image, uint? slot, TileCache tileCache, TileMipType tileType)
        {
            TileName = tileName;
            Image = image;

            TileCache = tileCache;

            Slot = slot;
            TileType = tileType;
        }

        public override bool Equals(object obj)
        {
            return obj is Tile other && TileName == other.TileName;
        }

        public override int GetHashCode()
        {
            return TileName.GetHashCode();
        }

        public void Draw(List<DrawCommand.BrushStroke> strokes)
        {


            
            string[] tileData = TileName.Split('_');

            int realMipIndex = int.Parse(tileData[0]);
            int tileX = int.Parse(tileData[2]);
            int tileY = int.Parse(tileData[3]);

            uint mipIndex = VirtualTextureData.GetMipIndex(realMipIndex);
            int gridSize = VirtualTextureData.GetMipSize(mipIndex);

            foreach (DrawCommand.BrushStroke stroke in strokes)
            {
                Vector2 tileUv = stroke.Origin * gridSize - new Vector2(tileX, tileY);
                Vector2 origin = tileUv * Image.GetWidth();

                int radius = Mathf.CeilToInt(stroke.BrushSize);

                int minX = Mathf.Max(0, Mathf.FloorToInt(origin.X) - radius);
                int maxX = Mathf.Min(Image.GetWidth() - 1, Mathf.CeilToInt(origin.X) + radius);
                int minY = Mathf.Max(0, Mathf.FloorToInt(origin.Y) - radius);
                int maxY = Mathf.Min(Image.GetHeight() - 1, Mathf.CeilToInt(origin.Y) + radius);

                float radiusSquared = stroke.BrushSize * stroke.BrushSize;

                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        Vector2 pixelPosition = new(x, y);

                        if (pixelPosition.DistanceSquaredTo(origin) > radiusSquared)
                            continue;

                        Image.SetPixel(x, y, stroke.Color);
                    }
                }
            }

            Save();
        }

        public void Save()
        {
            GD.Print($"Saving {TileName}");
            Image.SavePng(TileManager.GenerateTileImagePath(TileCache.TileDirectory, TileName));
        }

        
    }
}