using System.Collections.Generic;
using Godot;
using PlanetGame.Data;
using PlanetGame.Planet.Rendering.Drawing.Brushes;
using PlanetGame.Planet.Rendering.VirtualTexturing;
using PlanetGame.Util;

namespace PlanetGame.Planet.Rendering.Drawing
{
    public class DrawCommand(DrawCommand.DrawParameters parameters, Brush brush)
    {
        private static VirtualTextureData VirtualTextureData => SaveManager.VirtualTextureData;
        private static WorldData WorldData => SaveManager.WorldData;

        public struct DrawParameters
        {
            public TileCache.TileCacheType TargetTile;
        }

        public struct BrushStroke
        {
            public Brush Brush;
            public List<Vector2> Points;
        }

        public readonly Dictionary<Tile, BrushStroke> TileNameToBrushStroke = [];

        public List<PlanetQuery.PlanetSurfacePoint> StrokePoints { get; private set; } = [];

        public DrawParameters Parameters = parameters;
        public Brush Brush = brush;

        public bool TryToAddStroke(Node node, PlanetQuery.PlanetSurfacePoint surfacePoint)
        {

            StrokePoints.Add(surfacePoint);

            Vector3 localCubePoint = WorldData.LocalToPlanet(surfacePoint.LocalCubePoint);
            HashSet<Tile> tiles = GetAffectedTiles(node, Brush.GetDrawingRect(localCubePoint), VirtualTextureData.TotalMipLayersPerFace - 1);

            foreach(var tile in tiles)
            {
                GD.Print(tile);
            }

            return true;
        }

        private static HashSet<Tile> GetAffectedTiles(Node node, Rect3 brushBounds, uint targetMip)
        {
            HashSet<Tile> tiles = [];

            for (uint normalId = 0; normalId < 6; normalId++)
            {
                Tile tile = new(normalId, 1);
                Tile.Traverse(node, tile, brushBounds, targetMip, tiles);
            }

            return tiles;
        }

        private void AddPoint(PlanetQuery.PlanetSurfacePoint surfacePoint)
        {
            Tile tileId = Tile.GetTile(surfacePoint);
            if (!TileNameToBrushStroke.TryGetValue(tileId, out BrushStroke stroke))
            {
                stroke = new BrushStroke
                {
                    Brush = Brush,
                    Points = []
                };
            }

            stroke.Points.Add(surfacePoint.UV);
            TileNameToBrushStroke[tileId] = stroke;
        }

        public Tile[] GetStrokeTileNames()
        {
            return [.. TileNameToBrushStroke.Keys];
        }
    }
}