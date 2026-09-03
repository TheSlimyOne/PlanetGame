using System.Collections.Generic;
using Godot;
using PlanetGame.Planet;
using PlanetGame.Rendering.Surface;
using PlanetGame.Rendering.VirtualTexturing;
using PlanetGame.Rendering.VirtualTexturing.Drawing;
using static PlanetController;

public partial class DrawingController : Node
{
    private static TessellationData TessellationData => SaveManager.CurrentWorldSave.TessellationData;
    private static VirtualTextureData VirtualTextureData => SaveManager.CurrentWorldSave.VirtualTextureData;
    public PlanetController PlanetController { get; set; }
    public PlanetRenderer PlanetRenderer { get; set; }

    public DrawingController() { }

    private bool _isDrawing;
    private DrawCommand _drawCommand;
    private void BeginStroke()
    {
        if (!PlanetController.TryGetMouseSurfacePoint(out PlanetSurfacePoint surfacePoint, true, desiredMipIndex: 0))
        {
            GD.Print("Nothing to draw");
            return;
        }

        _isDrawing = true;
        _drawCommand = new DrawCommand(
            new DrawCommand.DrawParameters() { Spacing = 0.05f },
            PlanetRenderer.SparseVirtualTexture.AlbedoTileCache
        );
        GD.Print("stoke start");

        AddStrokePoint(surfacePoint);
    }

    private void EndStroke()
    {
        if (!_isDrawing && _drawCommand != null)
            return;

        _isDrawing = false;

        ProcessStroke();

        _drawCommand = null;

        GD.Print("stoke end");
    }

    private void ProcessStroke()
    {
        if (_drawCommand == null || _isDrawing)
            return;

        _drawCommand.DrawOnTiles();
    }

    private void AddStrokePoint(PlanetSurfacePoint surfacePoint)
    {
        _drawCommand.TryToAddStroke(surfacePoint);
    }

    public override void _Process(double delta)
    {
        if (!_isDrawing || PlanetController == null)
            return;

        if (!PlanetController.TryGetMouseSurfacePoint(out PlanetSurfacePoint surfacePoint, true, desiredMipIndex: 0))
            return;


        AddStrokePoint(surfacePoint);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (PlanetController == null)
            return;

        if (@event is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Left)
        {
            if (mouseButton.Pressed)
                BeginStroke();
            else
                EndStroke();
        }
    }
}