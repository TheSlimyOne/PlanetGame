using System;
using Godot;
using PlanetGame.Rendering.Surface;
using Shaders;
using PlanetGame.Shaders;
using PlanetGame.Util;
using PlanetGame.Rendering.VirtualTexturing;
using PlanetGame.Shaders.Dispatchers;
using System.Linq;

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
    [Export] public bool IsCubeStep { get; set; } = false;
    [Export] public bool IsCulling { get; set; } = true;
    [Export] public bool IsMorphing { get; set; } = true;
    [Export] public bool DisableVirtualTexturing { get; set; } = false;
    [Export] public bool DisableTesselation { get; set; } = false;
    [Export] public bool DisableInputMouseInput { get; set; } = false;
    [Export] public bool IsSimulateRotation { get; set; } = true;

    [Export] public bool Verbose { get => Dispatcher<Enum>.Verbose; set => Dispatcher<Enum>.Verbose = value; }


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

    // public Transform3D GetPlanetTransformMatrix()
    // {
    //     return PlanetTranslation * PlanetRotation * PlanetScale;
    // }
    // public Transform3D GetPlanetTRMatrix()
    // {
    //     return PlanetTranslation * PlanetRotation;
    // }
    // public Vector3 TransformPoint(Vector3 point)
    // {
    //     return GetPlanetTRMatrix().Inverse() * point;
    // }

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

    private Rid _terrainInstance;
    public override void _Ready()
    {
        string saveName = SaveManager.CurrentSave;
        if (string.IsNullOrWhiteSpace(saveName))
        {
            saveName = "Earth";
            SaveManager.CurrentSave = saveName;

        }
        SaveManager.WorldSave save = SaveManager.GetSave(saveName);

        SurfaceShader = new() { Shader = GD.Load<Shader>(ShaderPaths.SURFACE_SHADER_PATH) };

        SetupCameras();
        SetupMultimesh();
        ReorientatePlanet();

        _resolution = Resolution;

        TerrainTessellator = new(this, save, PlanetMultiMesh, MainCamera);

        SparseVirtualTexture = new(TerrainTessellator, save, PlanetMultiMesh.Mesh);

        BindShaderParameters(SurfaceShader, CameraController.GetCamera("Main"));

        SparseVirtualTexture.CreateDebugWindow(UIController.DebugContainer);

        PlanetCollisionController = new(this);
        PlanetCollisionController.GenerateBaseCollisionMesh();
        SurfaceAttachment.AddChild(PlanetCollisionController.CollisionBody);
        SurfaceAttachment.AddChild(CollisionTestSpheres);
    }

    #region Process
    int _resolution;
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



        if (TerrainTessellator == null || SparseVirtualTexture == null) return;

        TerrainTessellator.Paused = DisableTesselation;
        SparseVirtualTexture.Paused = DisableVirtualTexturing;


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

        float zoomSpeed = BaseZoomSpeed * (MainCamera.DistanceFromTarget / Radius);
        float rotationSpeed = BaseRotationSpeed * (MainCamera.DistanceFromTarget / Radius);

        Vector3 forward = PlanetTranslation.Origin.DirectionTo(MainCamera.GlobalPosition);
        Vector3 right = MainCamera.Basis.X;
        Vector3 up = forward.Cross(right).Normalized();

        if (IsCubeStep)
        {

            float x =
                (Input.IsActionJustPressed("move_left") ? 1 : 0) -
                (Input.IsActionJustPressed("move_right") ? 1 : 0);

            // float y = 

            float z =
                (Input.IsActionJustPressed("move_forward") ? 1 : 0) -
                (Input.IsActionJustPressed("move_backward") ? 1 : 0);

            Vector3 cubeRight = PlanetRotation.Basis.X.Normalized();
            Vector3 cubeUp = PlanetRotation.Basis.Y.Normalized();

            RotatePlanet(Vector3.Right, Mathf.Pi / 2.0f * z);
            RotatePlanet(Vector3.Up, Mathf.Pi / 2.0f * x);

        }
        else
        {
            RotatePlanet(right, rotationSpeed * by * _direction.Z);
            RotatePlanet(up, rotationSpeed * by * _direction.X);
        }


        if (IsSimulateRotation)
        {
            // External Objects that need to rotate to simulate the rotation effect
            WorldEnvironment.Environment.SkyRotation = PlanetRotation.Basis.GetEuler();
            SurfaceAttachment.Transform = GetPlanetTransform(scale: false);
        }

        MainCamera.DistanceFromTarget += zoomSpeed * Radius * _direction.Y * by;

        MainCamera.DistanceFromTarget = Mathf.Clamp(MainCamera.DistanceFromTarget, 0.05f, MainCamera.MaxDistance);
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
            if (DisableInputMouseInput)
                return;

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

                    if (false)
                        CollisionTestSpheres.AddChild(Utilities.SpawnTestSphere(WorldToPlanet(MainCamera.GlobalPosition, scale: false), 1));
                }

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
                    VTData virtualTextureData = SaveManager.GetSVTData(SaveManager.GetCurrentSave());
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
        VTData vtData = SaveManager.GetSVTData(SaveManager.GetCurrentSave());

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

        bindableShaderMaterial.Bind("tile_size", () => vtData.TileSize);

        bindableShaderMaterial.Bind("height_map_tile_cache", () => SparseVirtualTexture.HeightTileCache.Cache);
        bindableShaderMaterial.Bind("terrain_indirection_table", () => SparseVirtualTexture.IndirectionTable.Table);

        bindableShaderMaterial.Bind("low_resolution_mip_count", () => vtData.LowResolutionMipCount);
        bindableShaderMaterial.Bind("high_resolution_mip_count", () => vtData.HighResolutionMipCount);

        bindableShaderMaterial.Bind("albedo_tile_cache", () => SparseVirtualTexture.AlbedoTileCache.Cache);
        bindableShaderMaterial.UpdateAllParameters();
    }
    #endregion
}