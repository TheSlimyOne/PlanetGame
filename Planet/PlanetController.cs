using System;
using Godot;
using PlanetGame.Rendering.Surface;
using Shaders;
using PlanetGame.Shaders;
using PlanetGame.Util;
using PlanetGame.Rendering.VirtualTexturing;
using System.Linq;
using PlanetGame.Util.DebugUIComponents;

public partial class PlanetController : Node3D
{
    [ExportGroup("Planet Settings")]
    [Export(PropertyHint.Range, "1,100000")] public float Radius { get; set; } = 8000;
    [Export] public float HeightScale { get; set; } = 0.025f;

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
    [Export] public int MaximumKeys { get; set; } = 40000;
    [Export(PropertyHint.Range, "2,500,")] public int Resolution { get; set; } = 3;
    [Export] public Vector2 MorphRange { get; set; } = new(0, 0);
    [Export(PropertyHint.Range, "1, 10")] public float SubFactor { get; set; } = 4;
    [Export(PropertyHint.Range, "0, 31")] public int MaximumLod { get; set; } = 12;
    [Export(PropertyHint.Range, "0, 31")] public int MinimumLod { get; set; } = 0;
    [Export(PropertyHint.Range, "0, 4")] public int StartingLod { get; set; } = 1;

    [ExportGroup("Debug Settings")]
    [Export] public float PointRadius { get; set; }
    [Export] public bool IsCube { get; set; } = false;
    [Export] public bool IsCulling { get; set; } = true;
    [Export] public bool IsMorphing { get; set; } = true;

    private int _resolution;

    public float HeightOffset { get; private set; }

    public BindableShaderMaterial SurfaceShader { get; set; }
    public TerrainTessellator TerrainTessellator { get; private set; }
    public SparseVirtualTexture SparseVirtualTexture { get; private set; }

    public MultiMeshRD PlanetMultiMesh { get; private set; }

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

        PlanetMultiMesh.SetExtraVisibilityMargin(20 * Radius);
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

            if (SurfaceShader != null)
            {
                SurfaceShader.UnbindAll();
                RenderingServer.FreeRid(SurfaceShader.GetRid());
            }

            PlanetMultiMesh?.CleanupGPU();
            PlanetMultiMesh = null;

            SurfaceShader = null;

            TerrainTessellator?.CleanupGPUResources();
            SparseVirtualTexture?.CleanupGPUResources();

            TerrainTessellator = null;
            SparseVirtualTexture = null;
        }
    }

    private void BindDebugSettings()
    {        
        

        DebugMenuController.Instance.AddSection("Planet", 0);
        DebugMenuController.Instance.AddSlider("Radius", "Planet", () => Radius, value => Radius = value, 1.0f, 8000.0f, 1.0f);
        DebugMenuController.Instance.AddSlider("Height Scale", "Planet", () => HeightScale, value => HeightScale = value, 0.0f, 0.25f, 0.005f);
        DebugMenuController.Instance.AddActionButton("Quit", "Planet", () => GetTree().ChangeSceneToFile("res://main.tscn"));

        DebugMenuController.Instance.AddSection("Rendering", 0);
        DebugMenuController.Instance.AddButton("Render Cube Mode", "Rendering", () => IsCube, () => IsCube = !IsCube);
        DebugMenuController.Instance.AddButton("Render Culling", "Rendering", () => IsCulling, () => IsCulling = !IsCulling);
        DebugMenuController.Instance.AddButton("Render Morphing", "Rendering", () => IsMorphing, () => IsMorphing = !IsMorphing);
        DebugMenuController.Instance.AddButton("Render Tile UVs", "Rendering", () => SurfaceShader.GetParameter<bool>("render_tile_uvs"), () => SurfaceShader.SetParameter("render_tile_uvs", !SurfaceShader.GetParameter<bool>("render_tile_uvs")));
        DebugMenuController.Instance.AddButton("Render Keys", "Rendering", () => SurfaceShader.GetParameter<bool>("show_keys"), () => SurfaceShader.SetParameter("show_keys", !SurfaceShader.GetParameter<bool>("show_keys")));
        DebugMenuController.Instance.AddButton("Render Indirection Age", "Rendering", () => SurfaceShader.GetParameter<bool>("show_indirection_age"), () => SurfaceShader.SetParameter("show_indirection_age", !SurfaceShader.GetParameter<bool>("show_indirection_age")));
        DebugMenuController.Instance.AddButton("Render Cached Tiles", "Rendering", () => SurfaceShader.GetParameter<bool>("show_in_cache"), () => SurfaceShader.SetParameter("show_in_cache", !SurfaceShader.GetParameter<bool>("show_in_cache")));

        DebugMenuController.Instance.AddSection("Tessellation", 0);
        DebugMenuController.Instance.AddSlider("Resolution", "Tessellation", () => Resolution, value => Resolution = value, 2, 17, 1);
        DebugMenuController.Instance.AddButton("Enable Tessellation", "Tessellation", () => !TerrainTessellator.Paused, () => TerrainTessellator.Paused = !TerrainTessellator.Paused);

        DebugMenuController.Instance.AddSection("Virtual Texturing", 0);
        DebugMenuController.Instance.AddActionButton("Wipe Virtual Texture", "Virtual Texturing", SparseVirtualTexture.ClearVirtualTexture);
        DebugMenuController.Instance.AddButton("Enable Virtual Texturing", "Virtual Texturing", () => !SparseVirtualTexture.Paused, () => SparseVirtualTexture.Paused = !SparseVirtualTexture.Paused);

        DebugMenuController.Instance.AddTexture("State Table", "Virtual Texturing", SparseVirtualTexture.StateTable.CreateVisualization());
        DebugMenuController.Instance.AddTexture("Indirection Table", "Virtual Texturing", SparseVirtualTexture.IndirectionTable.CreateVisualization());
        DebugMenuController.Instance.AddTexture("Residency Table", "Virtual Texturing", SparseVirtualTexture.ResidencyTable.CreateVisualization());
        DebugMenuController.Instance.AddTexture("Albedo Tile Cache", "Virtual Texturing", SparseVirtualTexture.AlbedoTileCache.CreateVisualization("Albedo"));
        DebugMenuController.Instance.AddTexture("Height Tile Cache", "Virtual Texturing", SparseVirtualTexture.HeightTileCache.CreateVisualization("Height"));

        DebugMenuController.Instance.AddTexture("Flatten Indirection Table", "Virtual Texturing", SparseVirtualTexture.ConsolidatedIndirectionTable.CreateVisualization());

        DebugMenuController.Instance.AddTexture("Picking Texture", "Virtual Texturing",  new TextureRect() { Texture = SparseVirtualTexture.SvtFeedbackRenderPass.GetPickingTexture() });
    }

    private Rid _terrainInstance;
    public override void _Ready()
    {
        //TODO move this somewhere else
        string saveName = SaveManager.CurrentSave;
        if (string.IsNullOrWhiteSpace(saveName))
        {
            saveName = "Earth";
            SaveManager.CurrentSave = saveName;

        }

        SurfaceShader = new() { Shader = GD.Load<Shader>(ShaderPaths.GD_PLANET_TESSELLATION_PATH) };

        SetupCameras();
        SetupMultimesh();
        ReorientatePlanet();

        _resolution = Resolution;

        TerrainTessellator = new(this);
        TerrainTessellator.CreateUniforms();

        Vector2I viewSize = new(1024, 512);
        SparseVirtualTexture = new(this, viewSize);
        SparseVirtualTexture.CreateUniforms();

        BindShaderParameters(SurfaceShader, CameraController.GetCamera("Main"));

        PlanetCollisionController = new(this);
        PlanetCollisionController.GenerateBaseCollisionMesh();
        SurfaceAttachment.AddChild(PlanetCollisionController.CollisionBody);
        SurfaceAttachment.AddChild(CollisionTestSpheres);

        BindDebugSettings();
    }

    #region Process

    private double _heightUpdateTimer;
    private float _targetHeightOffset;
    private bool _hasTargetHeightOffset;
    private bool _hasTargetUnderground;

    private const double HEIGHT_UPDATE_INTERVAL = 0.5;
    private const float HEIGHT_INTERPOLATION_SPEED = 2;

    public override void _Process(double delta)
    {
        if (Quiting)
            return;

        ReorientatePlanet();

        if (Resolution != _resolution)
        {
            _resolution = Resolution;
            PlanetMultiMesh.SetMesh(Key.GetTriangleMesh(Resolution));
            TerrainTessellator.ExecuteTessellationPass.CreateUniforms();
            TerrainTessellator.PrepareTessellationPass.CreateUniforms();
            SparseVirtualTexture.ResolveTileRequest.CreateUniforms();
            SparseVirtualTexture.ValidateTileCache.CreateUniforms();
            SparseVirtualTexture.SvtFeedbackRenderPass.CreateUniforms();
            SparseVirtualTexture.ClearVirtualTexture();
        }

        if (TerrainTessellator == null || SparseVirtualTexture == null)
            return;

        VTData virtualTextureData = SaveManager.GetCurrentSave().GetSVTData();

        _heightUpdateTimer += delta;
        if (_heightUpdateTimer >= HEIGHT_UPDATE_INTERVAL)
        {
            _heightUpdateTimer = 0;

            float heightOffset = GetHeightAtPoint(MainCamera.GlobalPosition, TerrainTessellator.MaxLod, virtualTextureData);

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
            HeightOffset = _targetHeightOffset;
        }


        TerrainTessellator.Invoke();
        SparseVirtualTexture.Invoke();

        UIController.SetCurrentLOD(TerrainTessellator.MaxLod);
        UIController.SetLodCounts(TerrainTessellator.LodCounts);
        UIController.SetLabelKeyCount(TerrainTessellator.CulledCount, TerrainTessellator.TotalCount);

        SurfaceShader?.UpdateFrameDependentParameters();
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

    public void SetupMultimesh()
    {
        PlanetMultiMesh = new(MaximumKeys, Key.GetTriangleMesh(Resolution), -1);
        _terrainInstance = PlanetMultiMesh.CreateMultimeshInstance(Transform3D.Identity, SurfaceShader.GetRid(), GetWorld3D().Scenario, 2 * Radius, 0b1u);
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

        // GD.PrintS(MainCamera.DistanceFromTarget, minimumDistance);

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
                    VTData virtualTextureData = SaveManager.GetCurrentSave().GetSVTData();
                    PlanetCollisionController.CreateCollisionPlane(virtualTextureData);

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
        Vector3 localPickedPosition = SparseVirtualTexture.GetLocalMousePosition
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
        Vector3 localPickedPosition = SparseVirtualTexture.GetLocalMousePosition
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

    public float GetHeightAtPoint(Vector3 point, int lod, VTData vtData)
    {
        (_, Vector3 localCubePoint) = GetLocalPointsOnPlanet(point, false);

        if (!localCubePoint.IsFinite())
            return float.NaN;

        Vector3 normal = VectorUtils.IsolateNormal(localCubePoint);
        int normalId = VectorUtils.NormalToNormalID[normal];

        int mip = SaveManager.GetCurrentSave().LodToMipMap[lod];

        Vector2 uv = VectorUtils.PointOnCubeToUV(normalId, localCubePoint);

        float mipGridSize = vtData.GetMipGridSize((uint)mip);

        Vector2I tileCoords = (Vector2I)(uv * mipGridSize).Floor();

        string path = $"{mip}_{normalId}_{tileCoords.X}_{tileCoords.Y}";
        Image heightmap = SparseVirtualTexture.HeightTileCache.GetTile(path);

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

    public void BindShaderParameters(BindableShaderMaterial bindableShaderMaterial, CustomCamera main)
    {
        VTData vtData = SaveManager.GetCurrentSave().GetSVTData();

        bindableShaderMaterial.FrameDependentBind("radius", () => Radius);
        bindableShaderMaterial.FrameDependentBind("height_scale", () => Radius * HeightScale);
        bindableShaderMaterial.FrameDependentBind("resolution", () => Resolution);
        bindableShaderMaterial.FrameDependentBind("maximum_lod", () => MaximumLod);
        bindableShaderMaterial.FrameDependentBind("minimum_lod", () => MinimumLod);
        bindableShaderMaterial.FrameDependentBind("planet_transform_matrix", () => Utilities.ToProjection(GetPlanetTransform()));

        bindableShaderMaterial.FrameDependentBind("is_cube", () => IsCube);

        bindableShaderMaterial.FrameDependentBind("is_morphing", () => IsMorphing);
        bindableShaderMaterial.FrameDependentBind("morph_range", () => MorphRange);

        bindableShaderMaterial.FrameDependentBind("camera_position", () => main.GlobalPosition);
        bindableShaderMaterial.FrameDependentBind("fovy", () => Mathf.Tan(MainCamera.GetCameraFov(true) / 2));
        bindableShaderMaterial.FrameDependentBind("sub_factor", () => SubFactor);
        bindableShaderMaterial.FrameDependentBind("current_lod", () => TerrainTessellator.MaxLod);

        bindableShaderMaterial.FrameDependentBind("lod_to_mip_map", () => vtData.LodToMipMap);

        bindableShaderMaterial.Bind("height_map_tile_cache", () => SparseVirtualTexture.HeightTileCache.Cache);
        bindableShaderMaterial.Bind("terrain_indirection_table", () => SparseVirtualTexture.ConsolidatedIndirectionTable.Table);

        bindableShaderMaterial.Bind("low_resolution_mip_count", () => vtData.LowResolutionMipCount);
        bindableShaderMaterial.Bind("high_resolution_mip_count", () => vtData.HighResolutionMipCount);
        bindableShaderMaterial.Bind("total_tile_slots", () => TileCache.DEFAULT_TILE_SLOTS_COUNT);

        bindableShaderMaterial.Bind("albedo_tile_cache", () => SparseVirtualTexture.AlbedoTileCache.Cache);
        bindableShaderMaterial.UpdateAllParameters();
    }
    #endregion
}