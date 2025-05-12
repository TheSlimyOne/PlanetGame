using Godot;
using Planet;
using System.Threading.Tasks;
using PlanetGame.Rendering.Surface;
using Shaders;
using PlanetGame.ComputeShaders;
using PlanetGame.Util;
using PlanetGame.Rendering.VirtualTexturing;
using System;
using System.Collections.Generic;
using System.Diagnostics;

public partial class PlanetController : Node3D
{
    [ExportGroup("Planet Data")]
    [Export] public PlanetData PlanetData { get; private set; }

    [ExportGroup("Controllers")]
    [Export] public CameraController CameraController { get; private set; }
    [Export] public Node3D SurfaceAttachment { get; private set; }
    [Export] public UIController UIController { get; private set; }
    public OrbitalCamera3D MainCamera { get; private set; }

    [ExportGroup("Lighting")]
    [Export] public DirectionalLight3D MainLightSource { get; set; }
    [Export] public WorldEnvironment WorldEnvironment { get; set; }

    [ExportGroup("Movement Settings")]
    [Export] public float MaxSpeed { get; set; }
    [Export] public float ZoomSpeed { get; set; }
    [Export] public float MovementEasing { get; set; }

    [ExportGroup("Rendering Settings")]
    [Export] public int[] LodSubdivisionMap { get; private set; }


    public TerrainTessellator TerrainTessellator { get; private set; }
    public SparseVirtualTexture SparseVirtualTexture { get; private set; }

    public MultiMeshRD PlanetMultiMesh { get; private set; }
    public string RootPath { get; private set; } = "user://myworld";

    float radius = 5;
    public MeshInstance3D InsertSphereAt(Vector3 position, Color color, bool attachToPlanet = true)
    {
        MeshInstance3D mesh = new()
        {
            Mesh = new SphereMesh() { Radius = radius, Height = radius * 2, Material = new StandardMaterial3D() { AlbedoColor = color } }
        };

        if (attachToPlanet)
        {
            SurfaceAttachment.AddChild(mesh);
        }
        else
        {
            AddChild(mesh);
        }

        mesh.Position = position;
        return mesh;
    }

    private bool Quiting = false;
    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest || what == NotificationPredelete)
        {
            Quiting = true;

            _terrainInstances.ForEach(x => RenderingServer.InstanceGeometrySetMaterialOverride(x, new Rid()));

            SurfaceShader.UnbindAll();
            FramebufferShader.UnbindAll();

            RenderingServer.FreeRid(SurfaceShader.GetRid());
            RenderingServer.FreeRid(FramebufferShader.GetRid());


            PlanetMultiMesh.CleanupGPU();
            PlanetMultiMesh = null;

            RenderingServer.FreeRid(PlanetData.TriangleMesh.GetRid());

            SurfaceShader = null;
            FramebufferShader = null;

            TerrainTessellator.CleanupGPUResources();
            SparseVirtualTexture.CleanupGPUResources();

            TerrainTessellator = null;
            SparseVirtualTexture = null;
        }
    }

    private readonly List<Rid> _terrainInstances = [];
    public override void _Ready()
    {
        Vector2I baseImageSize = new(16384, 8192);

        // chunkManager.CleanupGPUResources();

        SetupCameras();

        PlanetData.Scaled(Vector3.One * PlanetData.Radius);
        PlanetData.Translate(Vector3.Back * (1 - PlanetData.Radius));
        PlanetData.GenerateQuadMesh();

        PlanetMultiMesh = new(PlanetData.MaximumNodes, PlanetData.TriangleMesh.GetRid(), -1);
        PlanetMultiMesh.CreateMultimeshInstance(Transform3D.Identity, SurfaceShader.GetRid(), GetWorld3D().Scenario, 2 * PlanetData.Radius, 0b1u);
        PlanetMultiMesh.CreateMultimeshInstance(Transform3D.Identity, FramebufferShader.GetRid(), CameraController.GetCamera("Lookup").GetWorld3D().Scenario, 2 * PlanetData.Radius, 0b1u);

        TerrainTessellator = new(PlanetData, PlanetMultiMesh, MainCamera, CameraController.GetCamera("Helper"));
        SparseVirtualTexture = new(RootPath, baseImageSize, CameraController.GetCamera("Lookup").GetViewport(), PlanetData.DesiredChunkSize);

        SurfaceShaderBindParameters();
        FramebufferShaderBindParameters();
        SparseVirtualTexture.CreateDebugWindow(this);
    }

    public override void _Process(double delta)
    {
        if (Quiting)
            return;

        SurfaceShader.UpdateFrameDependentParameters();
        FramebufferShader.UpdateFrameDependentParameters();

        TerrainTessellator.Invoke();
        SparseVirtualTexture.Invoke();
    }

    public void SetupCameras()
    {
        MainCamera = (OrbitalCamera3D)CameraController.GetCamera("Main");
        CustomCamera helperCamera = CameraController.GetCamera("Helper");
        CustomCamera lookupCamera = CameraController.GetCamera("Lookup");

        helperCamera.Follow(MainCamera);
        lookupCamera.Follow(MainCamera);

        // MainCamera.DistanceFromTarget = PlanetData.Radius + 5;
        MainCamera.MinDistance = PlanetData.Radius + 0.999f;
        MainCamera.MaxDistance = PlanetData.Radius * 10f;

        MainCamera.GlobalPosition = Vector3.Back * MainCamera.DistanceFromTarget;
        CameraController.SetCurrent("Main");
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

        float movementSpeed = CalculateSpeed(MainCamera.DistanceFromTarget);

        Vector3 right = MainCamera.Basis.X.Cross(Vector3.Forward);
        PlanetData.Rotate(MainCamera.Basis.X, movementSpeed * by * _direction.Z);
        PlanetData.Rotate(right, movementSpeed * by * _direction.X);

        // External Objects that need to rotate to simulate the effect
        WorldEnvironment.Environment.SkyRotation = PlanetData.Rotation.Basis.GetEuler();
        SurfaceAttachment.Transform = PlanetData.GetPlanetTRMatrix();
        MainCamera.DistanceFromTarget += ZoomSpeed * movementSpeed * _direction.Y * by;

        MainCamera.DistanceFromTarget = Mathf.Clamp(MainCamera.DistanceFromTarget, 0, MainCamera.MaxDistance);
        UIController.SetDistance(MainCamera.DistanceFromTarget);

        HasMoved = !_direction.IsZeroApprox();

        _direction = _direction.Lerp(Vector3.Zero, MovementEasing);
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent)
        {
            if (mouseEvent.ButtonIndex == MouseButton.Right && mouseEvent.Pressed)
            {
                MainLightSource.Transform = PlanetData.Rotation.Inverse();
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Quiting)
            return;

        ProcessMovement(delta);
    }

    public float CalculateSpeed(float distanceFromSurface)
    {
        float normalized = distanceFromSurface / MainCamera.MaxDistance;
        return 1 - Mathf.Pow(1 - normalized, 4);
    }

    #region Material Settings

    public BindableShaderMaterial SurfaceShader { get; set; } = new BindableShaderMaterial()
    {
        Shader = GD.Load<Shader>(ShaderPaths.SURFACE_SHADER_PATH)
    };

    public BindableShaderMaterial FramebufferShader { get; set; } = new BindableShaderMaterial()
    {
        Shader = GD.Load<Shader>(ShaderPaths.FRAME_BUFFER_SHADER)
    };

    public void BindSharedShaderParameters(BindableShaderMaterial bindableShaderMaterial, CustomCamera main, CustomCamera helper)
    {
        bindableShaderMaterial.Bind("radius", () => PlanetData.Radius);
        bindableShaderMaterial.FrameDependentBind("height_scale", () => PlanetData.HeightScale);
        bindableShaderMaterial.Bind("resolution", () => PlanetData.Resolution);
        bindableShaderMaterial.Bind("maximum_lod", () => PlanetData.MaximumLOD);
        bindableShaderMaterial.FrameDependentBind("planet_transform_matrix", () => Utilities.ToProjection(PlanetData.GetPlanetTransformMatrix()));

        bindableShaderMaterial.FrameDependentBind("is_cube", () => PlanetData.CubeMode);
        bindableShaderMaterial.FrameDependentBind("is_culling", () => PlanetData.Culling);

        bindableShaderMaterial.FrameDependentBind("is_morphing", () => PlanetData.Morphing);
        bindableShaderMaterial.FrameDependentBind("morph_range", () => PlanetData.MorphRange);

        bindableShaderMaterial.FrameDependentBind("camera_position", () => main.GlobalPosition);
        bindableShaderMaterial.FrameDependentBind("fovy", () => Mathf.Tan(helper.GetCameraFov(true) / 2));
        bindableShaderMaterial.Bind("sub_factor", () => PlanetData.SubFactor);
        bindableShaderMaterial.Bind("total_texture_subdivisions", () => SparseVirtualTexture.IndirectionTable.MipDepth);
        bindableShaderMaterial.FrameDependentBind("lod_subdivision_map", () => LodSubdivisionMap);
        bindableShaderMaterial.Bind("grid_size", () => SparseVirtualTexture.IndirectionTable.GridSize);

        SurfaceShader.Bind("height_map_tile_cache", () => SparseVirtualTexture.AlbedoTileCache.Cache);
        SurfaceShader.Bind("terrain_indirection_table", () => SparseVirtualTexture.IndirectionTable.Table);
    }

    public void SurfaceShaderBindParameters()
    {
        BindSharedShaderParameters(SurfaceShader, CameraController.GetCamera("Main"), CameraController.GetCamera("Helper"));
        SurfaceShader.Bind("albedo_tile_cache", () => SparseVirtualTexture.AlbedoTileCache.Cache);
        SurfaceShader.FrameDependentBind("normal_strength", () => PlanetData.NormalStrength);
        SurfaceShader.UpdateAllParameters();
    }

    public void FramebufferShaderBindParameters()
    {
        BindSharedShaderParameters(FramebufferShader, CameraController.GetCamera("Main"), CameraController.GetCamera("Helper"));
        FramebufferShader.UpdateAllParameters();
    }

    #endregion
}
