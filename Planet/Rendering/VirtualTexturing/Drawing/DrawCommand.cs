using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using static PlanetController;

namespace PlanetGame.Rendering.VirtualTexturing.Drawing;

public class DrawCommand
{
    public struct DrawParameters
    {
        public float Spacing;
    }

    public struct BrushStroke
    {
        public Vector2 Origin;
        public Color Color;
        public float BrushSize;
    }

    public TileCache TileCache { get; private set; }

    public List<PlanetSurfacePoint> StrokePoints { get; private set; } = [];

    private readonly Dictionary<Tile, List<BrushStroke>> TileToBrushStrokes = [];

    public DrawParameters BrushSettings;

    public DrawCommand(DrawParameters drawParameters, TileCache tileCache)
    {
        BrushSettings = drawParameters;
        TileCache = tileCache;
    }

    public bool TryToAddStroke(PlanetSurfacePoint surfacePoint)
    {
        // if (StrokePoints.Count > 0)
        // {
        //     PlanetSurfacePoint previousPoint = StrokePoints[^1];

        //     float distance = previousPoint.LocalSpherePoint.DistanceTo(surfacePoint.LocalSpherePoint);

        //     // if (distance < BrushSettings.Spacing)
        //         // return false;
        //     GD.PrintS(surfacePoint.UV, distance, BrushSettings.Spacing);
        // }


        StrokePoints.Add(surfacePoint);

        string tileName = TileManager.GetTileNameFromSurfacePoint(surfacePoint);
        Tile tile = TileCache.GetTile(tileName);

        BrushStroke stoke = new()
        {
            Origin = surfacePoint.UV,
            Color = Colors.Black,
            BrushSize = 5
        };

        if (!TileToBrushStrokes.TryGetValue(tile, out List<BrushStroke> strokes))
        {
            strokes = [];
            TileToBrushStrokes.Add(tile, strokes);
        }


        // GD.PrintS(strokes.Count, TileToBrushStrokes[tile].Count);
        strokes.Add(stoke);

        return true;
    }

    public string[] GetStrokeTileNames()
    {
        return [
            .. StrokePoints
                .Select(TileManager.GetTileNameFromSurfacePoint)
                .Distinct()
        ];
    }

    public async Task DrawOnTiles()
    {
        foreach ((Tile tile, List<BrushStroke> strokes) in TileToBrushStrokes)
        {
            // GD.Print(tile);
            // strokes.ForEach(x => GD.Print(x));
            // GD.Print("=================================================");
            tile.Draw(strokes);
        }

        TileToBrushStrokes.Clear();
    }
}