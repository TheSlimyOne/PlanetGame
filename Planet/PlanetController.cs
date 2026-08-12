using System;
using Godot;
using PlanetGame.Rendering.Surface;
using Shaders;
using PlanetGame.Shaders;
using PlanetGame.Util;
using PlanetGame.Rendering.VirtualTexturing;
using PlanetGame.Shaders.Dispatchers;

public partial class PlanetController : Node3D
{
    [ExportGroup("Planet Settings")]
    [Export(PropertyHint.Range, "1,100000")] public float Radius { get; set; } = 8000;
    [Export] public float HeightScale { get; set; } = 0.025f;

    [ExportGroup("Controllers")]
    [Export] public CameraController CameraController { get; private set; }
    [Export] public Node3D SurfaceAttachment { get; private set; }
    [Export] public UIController UIController { get; private set; }

    public PlanetCollisionController PlanetCollisionController { get; private set; }

    public OrbitalCamera3D MainCamera { get; private set; }

    [Export] public float PointRadius { get; set; }
    [Export] public int TotalDebugInstances { get; set; }

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
        PlanetTranslation = Transform3D.Identity.Translated(Vector3.Back * (1 - Radius));
        PlanetScale = Transform3D.Identity.Scaled(Vector3.One * Radius);

        PlanetMultiMesh.SetExtraVisibilityMargin(2 * Radius);
    }

    public Transform3D GetPlanetLocalToWorldTransform()
    {
        return PlanetTranslation * PlanetRotation * PlanetScale;
    }

    public Transform3D GetPlanetLocalToWorldTransformWithoutScale()
    {
        return PlanetTranslation * PlanetRotation;
    }

    public Vector3 WorldToPlanetLocal(Vector3 worldPoint)
    {
        return GetPlanetLocalToWorldTransform().AffineInverse() * worldPoint;
    }

    public Vector3 WorldToPlanetLocalWithoutScale(Vector3 worldPoint)
    {
        return GetPlanetLocalToWorldTransformWithoutScale().AffineInverse() * worldPoint;
    }

    public Vector3 PlanetLocalToWorld(Vector3 localPoint)
    {
        return GetPlanetLocalToWorldTransform() * localPoint;
    }

    public Vector3 PlanetLocalToWorldWithoutScale(Vector3 localPoint)
    {
        return GetPlanetLocalToWorldTransformWithoutScale() * localPoint;
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

        SparseVirtualTexture.Paused = DisableVirtualTexturing;
        TerrainTessellator.Paused = DisableTesselation;


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
            SurfaceAttachment.Transform = GetPlanetLocalToWorldTransformWithoutScale();
        }

        MainCamera.DistanceFromTarget += zoomSpeed * Radius * _direction.Y * by;

        MainCamera.DistanceFromTarget = Mathf.Clamp(MainCamera.DistanceFromTarget, 1.06f, MainCamera.MaxDistance);
        UIController.SetDistance(MainCamera.DistanceFromTarget);

        HasMoved = !_direction.IsZeroApprox() || MainCamera.HasMoved;


        _direction = _direction.Lerp(Vector3.Zero, (float)(MovementEasing * delta));
    }

    int instanceIndex = 0;
    bool test = false;
    public override async void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent)
        {
            if (DisableInputMouseInput)
                return;

            if (mouseEvent.ButtonIndex == MouseButton.Right && mouseEvent.Pressed)
            {
                MainLightSource.Transform = PlanetRotation.Inverse();
            }
            if (mouseEvent.ButtonIndex == MouseButton.Left && mouseEvent.Pressed)
            {
                // Vector3 localPickedPosition = SparseVirtualTexture.SvtFeedbackRenderPass.GetLocalMousePosition
                // (
                //     MainCamera.GetViewport().GetMousePosition(),
                //     MainCamera.GetViewport().GetVisibleRect().Size
                // );

                // GetPointOnPlanet(localPickedPosition, true);
                // (Vector3 localSpherePoint, Vector3 localCubePoint) = GetLocalPointsOnPlanet(localPickedPosition, true);
                // (Vector3 localSpherePoint, Vector3 localCubePoint) = GetLocalPointsOnPlanet(MainCamera.GlobalPosition, false);

                // if (localSpherePoint == Vector3.Inf || localCubePoint == Vector3.Inf)
                //     return;


                PlanetCollisionController.CreateCollisionPlane();
                // if (!test)
                // {
                //     PlanetCollisionController.CreateCollisionPointReferences();
                //     test = true;
                // }



            }
        }
    }

    public (Vector3 localSpherePoint, Vector3 localCubePoint) GetLocalPointsOnPlanet(Vector3 point, bool isLocalSpace)
    {
        Vector3 localPoint = isLocalSpace ? point : WorldToPlanetLocal(point);

        if (localPoint.IsZeroApprox() || !localPoint.IsFinite())
            return (Vector3.Inf, Vector3.Inf);

        Vector3 direction = localPoint.Normalized();

        float maximumComponent = Mathf.Max(
            Mathf.Abs(direction.X),
            Mathf.Max(Mathf.Abs(direction.Y), Mathf.Abs(direction.Z))
        );

        Vector3 localSpherePoint = direction;
        Vector3 localCubePoint = direction / maximumComponent;

        return (localSpherePoint, localCubePoint);
    }
    public override void _PhysicsProcess(double delta)
    {
        if (Quiting)
            return;

        ProcessMovement(delta);
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
        bindableShaderMaterial.FrameDependentBind("planet_transform_matrix", () => Utilities.ToProjection(GetPlanetLocalToWorldTransform()));

        bindableShaderMaterial.FrameDependentBind("is_cube", () => IsCube);

        bindableShaderMaterial.FrameDependentBind("is_morphing", () => IsMorphing);
        bindableShaderMaterial.FrameDependentBind("morph_range", () => MorphRange);

        bindableShaderMaterial.FrameDependentBind("camera_position", () => main.GlobalPosition);
        bindableShaderMaterial.FrameDependentBind("fovy", () => Mathf.Tan(MainCamera.GetCameraFov(true) / 2));
        bindableShaderMaterial.FrameDependentBind("sub_factor", () => SubFactor);

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