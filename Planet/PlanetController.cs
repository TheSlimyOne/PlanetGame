using System;
using Godot;
using PlanetGame.Rendering.Surface;
using Shaders;
using PlanetGame.Shaders;
using PlanetGame.Util;
using PlanetGame.Rendering.VirtualTexturing;
using System.Linq;
using PlanetGame.Util.DebugUIComponents;
using PlanetGame.Planet;

public partial class PlanetController : Node3D
{
    private static TessellationData TessellationData => SaveManager.CurrentWorldSave.TessellationData;
    private static VirtualTextureData VirtualTextureData => SaveManager.CurrentWorldSave.VirtualTextureData;
    
    [Export(PropertyHint.Range, "1,100000")]
    public float Radius
    {
        get => TessellationData.Radius;
        set => TessellationData.Radius = value;
    }

    [Export]
    public float HeightScale
    {
        get => TessellationData.HeightScale;
        set => TessellationData.HeightScale = value;
    }

    [ExportGroup("Controllers")]
    [Export] public CameraController CameraController { get; private set; }
    [Export] public Node3D SurfaceAttachment { get; private set; }
    public Node3D CollisionTestSpheres = new();

    [Export] public UIController UIController { get; private set; }

    public PlanetCollisionController PlanetCollisionController { get; private set; }

    public OrbitalCamera3D MainCamera { get; private set; }


    [ExportGroup("Lighting")]
    [Export] public DirectionalLight3D MainLightSource { get; set; }
    [Export] public WorldEnvironment WorldEnvironment { get; set; }

    [ExportGroup("Movement Settings")]
    [Export] public float BaseZoomSpeed { get; set; }
    [Export] public float BaseRotationSpeed { get; set; }
    [Export] public float MovementEasing { get; set; }
    [Export] public float MinDistanceFromSurface { get; set; } = 0.05f;


   [ExportGroup("Rendering Settings")]

    [Export]
    public uint MaximumKeys
    {
        get => TessellationData.MaximumKeys;
        set => TessellationData.MaximumKeys = value;
    }

    [Export(PropertyHint.Range, "2,500")]
    public uint Resolution
    {
        get => TessellationData.Resolution;
        set => TessellationData.Resolution = value;
    }

    [Export(PropertyHint.Range, "1,10")]
    public float SubFactor
    {
        get => TessellationData.SubFactor;
        set => TessellationData.SubFactor = value;
    }

    [Export(PropertyHint.Range, "0,31")]
    public uint MaximumLod
    {
        get => TessellationData.MaximumLod;
        set => TessellationData.MaximumLod = value;
    }

    [Export(PropertyHint.Range, "0,31")]
    public uint MinimumLod
    {
        get => TessellationData.MinimumLod;
        set => TessellationData.MinimumLod = value;
    }

    [Export(PropertyHint.Range, "0,4")]
    public uint StartingLod
    {
        get => TessellationData.StartingLod;
        set => TessellationData.StartingLod = value;
    }

    [ExportGroup("Debug Settings")]
    [Export] public float PointRadius { get; set; }

    private float HeightOffset;

    PlanetRenderer PlanetRenderer;

    private bool Quiting = false;

    #region  Planet Transforms
    public Transform3D PlanetTranslation { get; private set; } = Transform3D.Identity;
    public Transform3D PlanetRotation { get; private set; } = Transform3D.Identity;
    public Transform3D PlanetScale { get; private set; } = Transform3D.Identity;

    public void TranslatePlanet(Vector3 offset)
    {
        PlanetTranslation = PlanetTranslation.Translated(offset);
    }
    public void RotatePlanet(Vector3 axis, float angle)
    {
        PlanetRotation = PlanetRotation.Rotated(axis, angle).Orthonormalized();
    }
    public void ScalePlanet(Vector3 scale)
    {
        PlanetScale = PlanetScale.Scaled(scale);
    }

    public void ReorientatePlanet()
    {
        PlanetTranslation = Transform3D.Identity.Translated(Vector3.Back * (-Radius));
        PlanetScale = Transform3D.Identity.Scaled(Vector3.One * Radius);
    }

    public Transform3D GetPlanetTransform(bool translation = true, bool rotation = true, bool scale = true)
    {
        Transform3D transform = Transform3D.Identity;

        if (translation)
            transform *= PlanetTranslation;

        if (rotation)
            transform *= PlanetRotation;

        if (scale)
            transform *= PlanetScale;

        return transform;
    }

    public Vector3 WorldToPlanet(Vector3 worldPoint, bool translation = true, bool rotation = true, bool scale = true)
    {
        return GetPlanetTransform(translation, rotation, scale).AffineInverse() * worldPoint;
    }

    public Vector3 PlanetToWorld(Vector3 planetPoint, bool translation = true, bool rotation = true, bool scale = true)
    {
        return GetPlanetTransform(translation, rotation, scale) * planetPoint;
    }

    #endregion

    //TODO really look into cleaning the gpu idk why these come here null
    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest || what == NotificationPredelete)
        {
            Quiting = true;
            // PlanetRenderer.Clean();
        }
    }

    private Rid _terrainInstance;
    public override void _Ready()
    {
        SetupCameras();
        ReorientatePlanet();

        PlanetRenderer = new(this);
        PlanetCollisionController = new(this);
        PlanetCollisionController.GenerateBaseCollisionMesh();
        SurfaceAttachment.AddChild(PlanetCollisionController.CollisionBody);
        SurfaceAttachment.AddChild(CollisionTestSpheres);

        BindDebugSettings();
    }

    private void BindDebugSettings()
    {
        DebugMenuController.Instance.AddSection("Save Settings", 0, false, null, 900);
        DebugMenuController.Instance.AddActionButton("Save Current Settings", "Save Settings", () =>
        {
            SaveManager.OverrideSave(SaveManager.CurrentSave, SaveManager.CurrentWorldSave);
        }, 1);

        DebugMenuController.Instance.AddActionButton("Quit", null, () => {
            DebugMenuController.Instance.Clear();
            PlanetRenderer.CleanupGPU();
            GetTree().ChangeSceneToFile("res://main.tscn");
        }, 1000);
    }

    #region Process


    private double _heightUpdateTimer;
    private float _targetHeightOffset;
    private bool _hasTargetHeightOffset;
    private bool _hasTargetUnderground;

    private const double HEIGHT_UPDATE_INTERVAL = 0.5;
    private const float HEIGHT_INTERPOLATION_SPEED = 2;
    private const float HEIGHT_UNDERGROUND_INTERPOLATION_SPEED = 4;

    public override void _Process(double delta)
    {
        if (Quiting)
            return;

        ReorientatePlanet();

        _heightUpdateTimer += delta;
        if (_heightUpdateTimer >= HEIGHT_UPDATE_INTERVAL)
        {
            _heightUpdateTimer = 0;

            float heightOffset = GetHeightAtPoint(MainCamera.GlobalPosition, Mathf.FloorToInt(CalculateLodOfPoint(Vector3.Zero, true)));

            if (!float.IsNaN(heightOffset))
            {
                _targetHeightOffset = heightOffset;

                if (MainCamera.DistanceFromTarget > heightOffset)
                {
                    _hasTargetHeightOffset = true;
                    _hasTargetUnderground = false;
                }
                else
                {
                    _hasTargetHeightOffset = false;
                    _hasTargetUnderground = true;
                }
            }
        }
        if (_hasTargetHeightOffset)
        {
            HeightOffset = Mathf.Lerp(
                HeightOffset,
                _targetHeightOffset,
                1.0f - Mathf.Exp(-HEIGHT_INTERPOLATION_SPEED * (float)delta)
            );
        }
        else if (_hasTargetUnderground)
        {
            HeightOffset = Mathf.Lerp(
            HeightOffset,
            _targetHeightOffset,
            1.0f - Mathf.Exp(-HEIGHT_INTERPOLATION_SPEED * HEIGHT_UNDERGROUND_INTERPOLATION_SPEED * (float)delta)
        );
        }

        // HeightOffset = 0;

        if (PlanetRenderer != null)
        {
            PlanetRenderer.Invoke(MainCamera, HeightOffset, GetPlanetTransform());

            UIController.SetCurrentLOD(PlanetRenderer.TerrainTessellator.MaxLod);
            // UIController.SetLodCounts(PlanetRenderer.TerrainTessellator.LodCounts);
            UIController.SetLabelKeyCount(PlanetRenderer.TerrainTessellator.CulledCount, PlanetRenderer.TerrainTessellator.TotalCount);
        }
    }
    #endregion


    public void SetupCameras()
    {
        MainCamera = (OrbitalCamera3D)CameraController.GetCamera("Main");
        MainCamera.Far = 32768; // Max far value for cameras

        MainCamera.MinDistance = Radius + 0.999f;
        MainCamera.MaxDistance = MainCamera.Far - Radius;

        MainCamera.DistanceFromTarget = Radius;

        MainCamera.GlobalPosition = Vector3.Back * MainCamera.DistanceFromTarget;
        CameraController.SetCurrent("Main");

        MeshInstance3D frustum = MainCamera.GetFrustumMeshInstance();
        MainCamera.AddChild(frustum);
        frustum.GlobalPosition = new Vector3(0.0f, 0.0f, -MainCamera.Near);
    }

    private Vector3 _direction = Vector3.Zero;
    public bool HasMoved { get; private set; }

    void ProcessMovement(double delta)
    {
        float by = (float)delta;

        _direction.X += Input.GetActionStrength("move_left") - Input.GetActionStrength("move_right");
        _direction.Y = Input.GetActionStrength("move_up") - Input.GetActionStrength("move_down");
        _direction.Z += Input.GetActionStrength("move_forward") - Input.GetActionStrength("move_backward");
        _direction = _direction.Clamp(-1, 1);



        float minimumDistance = HeightOffset + MinDistanceFromSurface;

        if (MainCamera.DistanceFromTarget < minimumDistance)
            MainCamera.DistanceFromTarget = minimumDistance;

        float altitude = Mathf.Max(MainCamera.DistanceFromTarget - HeightOffset, 1.0f);
        float altitudeRatio = Mathf.Max(altitude / Radius, 0.0001f);
        float speedScale = Mathf.Pow(altitudeRatio, 1.1f);

        float zoomSpeed = BaseZoomSpeed * speedScale;
        float rotationSpeed = BaseRotationSpeed * speedScale;

        Vector3 forward = PlanetTranslation.Origin.DirectionTo(MainCamera.GlobalPosition);
        Vector3 right = MainCamera.Basis.X;
        Vector3 up = forward.Cross(right).Normalized();

        RotatePlanet(right, rotationSpeed * by * _direction.Z);
        RotatePlanet(up, rotationSpeed * by * _direction.X);

    
        WorldEnvironment.Environment.SkyRotation = PlanetRotation.Basis.GetEuler();
        SurfaceAttachment.Transform = GetPlanetTransform(scale: false);
        

        MainCamera.DistanceFromTarget += zoomSpeed * Radius * _direction.Y * by;

        MainCamera.DistanceFromTarget = Mathf.Clamp(MainCamera.DistanceFromTarget, HeightOffset + MinDistanceFromSurface, MainCamera.MaxDistance);

        UIController.SetDistance(MainCamera.DistanceFromTarget);

        HasMoved = !_direction.IsZeroApprox() || MainCamera.HasMoved;

        _direction = _direction.Lerp(Vector3.Zero, (float)(MovementEasing * delta));
    }

    Color[] colors =
    [
        Colors.Red,
        Colors.Green,
        Colors.Blue,
        Colors.Yellow,
        Colors.Orange,
        Colors.Purple,
        Colors.Cyan,
        Colors.Magenta,
        Colors.White,
        Colors.Black
    ];

    public override async void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent)
        {
            if (mouseEvent.ButtonIndex == MouseButton.Right && mouseEvent.Pressed)
            {
                MainLightSource.Transform = PlanetRotation.Inverse();

                if (false)
                {
                    Godot.Collections.Dictionary result = Utilities.RaycastFromMouse(MainCamera, 1_000_000);

                    if (result.Count > 0)
                    {
                        Vector3 hitPosition = (Vector3)result["position"];
                        Vector3 hitNormal = (Vector3)result["normal"];

                        GD.Print("Collision hit: ", hitPosition);
                        GD.Print("Normal: ", hitNormal);
                    }
                    else
                    {
                        GD.Print("No collision");
                    }

                }

                if (false)
                    CollisionTestSpheres.AddChild(Utilities.SpawnTestSphere(WorldToPlanet(MainCamera.GlobalPosition, scale: false), 1));

            }

            if (mouseEvent.ButtonIndex == MouseButton.Left && mouseEvent.Pressed)
            {
                if (false)
                {
                    Vector3 planetSpacePoint = GetPlanetMousePosition();
                    if (!planetSpacePoint.IsFinite())
                        return;

                    int lod = Mathf.FloorToInt(CalculateLodOfPoint(planetSpacePoint, false));

                    Vector2 uv = VectorUtils.PointOnSphereToUV(planetSpacePoint.Normalized());
                    MeshInstance3D mesh = new()
                    {
                        Mesh = new SphereMesh() { Radius = PointRadius, Height = PointRadius * 2 },
                        Position = planetSpacePoint,
                        MaterialOverride = new StandardMaterial3D()
                        {
                            AlbedoColor = new Color(uv.X, uv.Y, 0)
                        }
                    };

                    SurfaceAttachment.AddChild(mesh);
                }

                if (false)
                {
                    Vector3 position = MainCamera.GlobalPosition;
                    PlanetCollisionController.CreateCollisionPlane(position);

                }
            }
        }
    }

    public (Vector3 localSpherePoint, Vector3 localCubePoint) GetLocalPointsOnPlanet(Vector3 point, bool isLocalSpace)
    {
        Vector3 localPoint = isLocalSpace ? point : WorldToPlanet(point);

        if (localPoint.IsZeroApprox() || !localPoint.IsFinite())
            return (Vector3.Inf, Vector3.Inf);

        Vector3 localSpherePoint = localPoint.Normalized();
        Vector3 localCubePoint = VectorUtils.PointOnSphereToPointOnCube(localSpherePoint);

        return (localSpherePoint, localCubePoint);
    }

    public Vector3 GetPlanetMousePosition()
    {
        Vector3 localPickedPosition = PlanetRenderer.SparseVirtualTexture.GetLocalMousePosition
        (
            MainCamera.GetViewport().GetMousePosition(),
            MainCamera.GetViewport().GetVisibleRect().Size
        );

        if (!localPickedPosition.IsFinite())
            return Vector3.Inf;

        return Radius * localPickedPosition;
    }

    public Vector3 GetPlanetScreenPosition(Vector2 position)
    {
        Vector3 localPickedPosition = PlanetRenderer.SparseVirtualTexture.GetLocalMousePosition
        (
            position,
            MainCamera.GetViewport().GetVisibleRect().Size
        );

        if (!localPickedPosition.IsFinite())
            return Vector3.Inf;

        return Radius * localPickedPosition;
    }

    public float CalculateLodOfPoint(Vector3 point, bool inWorldSpace)
    {

        Vector3 planetPoint = inWorldSpace ? WorldToPlanet(point, scale: false) : point;
        Vector3 cameraPlanetPoint = WorldToPlanet(MainCamera.GlobalPosition, scale: false);

        float distance = planetPoint.DistanceTo(cameraPlanetPoint);

        float num = Mathf.Sqrt2 * SubFactor * Radius;
        float den = distance * Mathf.Tan(MainCamera.GetCameraFov(true) / 2);

        return Mathf.Log(num / den) / Mathf.Log(2);
    }

    public float GetHeightAtPoint(Vector3 point, int lod)
    {
        (_, Vector3 localCubePoint) = GetLocalPointsOnPlanet(point, false);

        if (!localCubePoint.IsFinite())
            return float.NaN;

        Vector3 normal = VectorUtils.IsolateNormal(localCubePoint);
        int normalId = VectorUtils.NormalToNormalID[normal];

        Vector2 uv = VectorUtils.PointOnCubeToPlaneUV(normalId, localCubePoint);

        uint mip = PlanetRenderer.SparseVirtualTexture.SampleConsolidatedIndirectionTexture(normalId, uv);

        float mipGridSize = VirtualTextureData.GetMipGridSize(mip);

        Vector2I tileCoords = (Vector2I)(uv * mipGridSize).Floor();

        string path = $"{VirtualTextureData.GetNegativeMip(mip)}_{normalId}_{tileCoords.X}_{tileCoords.Y}";

        Image heightmap = PlanetRenderer.SparseVirtualTexture.HeightTileCache.GetTile(path);

        if (heightmap == null)
            return float.NaN;

        Vector2 tileMinUV = new Vector2(tileCoords.X, tileCoords.Y) / mipGridSize;
        Vector2 tileLocalUV = (uv - tileMinUV) * mipGridSize;

        Vector2I pixelCoords = new(
            (int)(tileLocalUV.X * (heightmap.GetWidth() - 1)),
            (int)(tileLocalUV.Y * (heightmap.GetHeight() - 1))
        );

        float elevation = heightmap.GetPixelv(pixelCoords).R;

        return elevation * Radius * HeightScale;
    }
    public override void _PhysicsProcess(double delta)
    {
        if (Quiting)
            return;

        ProcessMovement(delta);

        Vector3 planetCenter = PlanetToWorld(Vector3.Zero, scale: false);

        foreach (RigidBody3D body in CollisionTestSpheres.GetChildren().Cast<RigidBody3D>())
        {
            Vector3 toPlanet = planetCenter - body.GlobalPosition;
            float distance = toPlanet.Length();

            if (distance <= 0.0001f)
                continue;

            Vector3 direction = toPlanet / distance;
            body.ApplyCentralForce(direction * 50.0f);
        }
    }

    #region Material Settings

    
    #endregion
}