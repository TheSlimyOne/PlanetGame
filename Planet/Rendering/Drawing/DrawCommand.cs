using System.Collections.Generic;
using Godot;
using PlanetGame.Data;
using PlanetGame.Planet.Rendering.Drawing.Brushes;
using PlanetGame.Planet.Rendering.VirtualTexturing;

namespace PlanetGame.Planet.Rendering.Drawing
{
    public class DrawCommand(DrawCommand.DrawParameters parameters, Brush brush)
    {
        private static VirtualTextureData VirtualTextureData => SaveManager.VirtualTextureData;

        public struct DrawParameters
        {
            public TileCache.TileCacheType TargetTile;
        }

        public struct BrushStroke
        {
            public Brush Brush;
            public List<Vector2> Points;
        }

        public readonly Dictionary<string, BrushStroke> TileNameToBrushStroke = [];

        public List<PlanetQuery.PlanetSurfacePoint> StrokePoints { get; private set; } = [];

        public DrawParameters Parameters = parameters;
        public Brush Brush = brush;

        public bool TryToAddStroke(PlanetQuery.PlanetSurfacePoint surfacePoint)
        {
            
            StrokePoints.Add(surfacePoint);

            AddPoint(surfacePoint);

            int nextMipIndex = (int)surfacePoint.MipIndex - 1;

            if (nextMipIndex >= 0)
            {
                PlanetQuery.PlanetSurfacePoint parentPoint = surfacePoint;
                parentPoint.MipIndex = (uint)nextMipIndex;

                TryToAddStroke(parentPoint);
            }

            return true;
        }

        private void AddPoint(PlanetQuery.PlanetSurfacePoint surfacePoint)
        {
            GD.Print(surfacePoint);

            // string tileName = TileManager.GetTileNameFromSurfacePoint(surfacePoint);

            // if (!TileNameToBrushStroke.TryGetValue(tileName, out BrushStroke stroke))
            // {
            //     stroke = new BrushStroke
            //     {
            //         Brush = Brush,
            //         Points = []
            //     };
            // }

            // stroke.Points.Add(surfacePoint.UV);

            // TileNameToBrushStroke[tileName] = stroke;
        }

        public string[] GetStrokeTileNames()
        {
            return [.. TileNameToBrushStroke.Keys];
        }
    }
}