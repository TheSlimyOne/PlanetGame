using Godot;
using PlanetGame;
using PlanetGame.Data;
using PlanetGame.Planet.Rendering;
using PlanetGame.Planet.Rendering.Drawing;
using PlanetGame.Planet.Rendering.Drawing.Brushes;
using PlanetGame.Planet.Rendering.VirtualTexturing;

public partial class PlanetDrawingController : Control
{
    private static TessellationData TessellationData => SaveManager.TessellationData; 
    private static VirtualTextureData VirtualTextureData => SaveManager.VirtualTextureData;
    
    private PlanetRenderer _planetRenderer;
    private PlanetQuery _planetQuery;

    public Brush CurrentBrush;

    private readonly BrushOptions _brushOptions = ScenePaths.InstantiateScene<BrushOptions>(ScenePaths.BRUSH_OPTIONS);

    public bool IsDrawing { get; private set; }

    private DrawCommand _drawCommand;

    public void Initialize(PlanetRenderer planetRenderer, PlanetQuery planetQuery)
    {
        _planetRenderer = planetRenderer;
        _planetQuery = planetQuery;

        SetupBrushOptions();
        ConnectSignals();

        CurrentBrush = new BasicBrush();

        _planetRenderer.SurfaceShader.FrameDependentBind("brush_size", () => CurrentBrush.Size);
        _planetRenderer.SurfaceShader.FrameDependentBind("brush_opacity", () => CurrentBrush.Opacity);
        _planetRenderer.SurfaceShader.FrameDependentBind("brush_hardness", () => CurrentBrush.Hardness);
    }

    public override void _ExitTree()
    {
        DisconnectSignals();
    }

    private void SetupBrushOptions()
    {
        AddChild(_brushOptions);
        _brushOptions.Visible = false;
    }

    private void ConnectSignals()
    {
        _brushOptions.SizeChanged += OnBrushSizeChanged;
        _brushOptions.HardnessChanged += OnBrushHardnessChanged;
        _brushOptions.OpacityChanged += OnBrushOpacityChanged;
    }

    private void DisconnectSignals()
    {
        _brushOptions.SizeChanged -= OnBrushSizeChanged;
        _brushOptions.HardnessChanged -= OnBrushHardnessChanged;
        _brushOptions.OpacityChanged -= OnBrushOpacityChanged;
    }

    private void OnBrushSizeChanged(float size)
    {
        CurrentBrush.Size = size;
    }

    private void OnBrushHardnessChanged(float hardness)
    {
        CurrentBrush.Hardness = hardness;
    }

    private void OnBrushOpacityChanged(float opacity)
    {
        CurrentBrush.Opacity = opacity;
    }

    public void BeginStroke()
    {
        if (!IsValid() || IsDrawing)
            return;

        if (!_planetQuery.TryGetMouseSurfacePoint(out PlanetQuery.PlanetSurfacePoint surfacePoint, true, desiredMipIndex: VirtualTextureData.TotalMipLayersPerFace - 1))
        {
            GD.Print("Nothing to draw");
            return;
        }

        IsDrawing = true;

        _drawCommand = new DrawCommand(
            new DrawCommand.DrawParameters
            {
                TargetTile = TileCache.TileCacheType.HEIGHTMAP
            },
            CurrentBrush
        );

        GD.Print("stroke start");
        AddStrokePoint(surfacePoint);
    }

    public void EndStroke()
    {
        if (!IsValid() || !IsDrawing || _drawCommand == null)
            return;

        IsDrawing = false;

        _planetRenderer.ProcessDrawCommand(_drawCommand);
        _drawCommand = null;

        GD.Print("stroke end");
    }

    public void AddStrokePoint(PlanetQuery.PlanetSurfacePoint surfacePoint)
    {
        if (_drawCommand == null)
            return;

        _drawCommand.TryToAddStroke(surfacePoint);
    }

    public override void _Process(double delta)
    {
        if (!IsValid() || !IsDrawing)
            return;

        if (_planetQuery.TryGetMouseSurfacePoint(out PlanetQuery.PlanetSurfacePoint surfacePoint, true, desiredMipIndex: VirtualTextureData.TotalMipLayersPerFace - 1))
            AddStrokePoint(surfacePoint);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsValid() || @event is not InputEventMouseButton mouseButton)
            return;

        if (mouseButton.ButtonIndex == MouseButton.Left)
        {
            if (mouseButton.Pressed)
                BeginStroke();
            else
                EndStroke();

            return;
        }

        // if (mouseButton.ButtonIndex == MouseButton.Right && mouseButton.Pressed)
        //     ToggleBrushOptions();
    }

    public bool IsValid()
    {
        return _planetRenderer != null && _planetQuery != null;
    }

    #region UI

    public void ToggleBrushOptions()
    {
        _brushOptions.Visible = !_brushOptions.Visible;

        if (!_brushOptions.Visible)
        {
            _brushOptions.GlobalPosition = Vector2.Zero;
            return;
        }

        Vector2 mousePosition = GetViewport().GetMousePosition();
        Vector2 viewportSize = GetViewportRect().Size;
        Vector2 panelSize = _brushOptions.Size;

        Vector2 position = mousePosition;

        position.X = Mathf.Clamp(position.X, 0, viewportSize.X - panelSize.X);
        position.Y = Mathf.Clamp(position.Y, 0, viewportSize.Y - panelSize.Y);

        _brushOptions.GlobalPosition = position;
    }

    #endregion
}