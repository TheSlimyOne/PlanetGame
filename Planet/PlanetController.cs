using Godot;
using PlanetGame.Rendering.Surface;
using Shaders;
using PlanetGame.ComputeShaders;
using PlanetGame.Util;
using PlanetGame.Rendering.VirtualTexturing;
using System;
using System.Collections.Generic;
using PlanetGame.ComputeShaders.Dispatcher;
using Godot.Collections;

public partial class PlanetController : Node3D
{
    [ExportGroup("Planet Settings")]
    [Export(PropertyHint.Range, "1,8000")] public float Radius { get; set; }
    [Export] public float HeightScale { get; set; }

    [ExportGroup("Controllers")]
    [Export] public CameraController CameraController { get; private set; }
    [Export] public Node3D SurfaceAttachment { get; private set; }
    [Export] public UIController UIController { get; private set; }
    public OrbitalCamera3D MainCamera { get; private set; }

    public StaticBody3D InnerCollision { get; set; }
    public StaticBody3D OuterCollision { get; set; }
    public StaticBody3D CubicalCollision { get; set; }
    [ExportGroup("Collider Settings")]
    [Export] public float RayLength = 5000;
    [Export] public float PointRadius { get; set; }
    private MultiMeshInstance3D _debugPlot = new();

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
    [Export] public float NormalStrength { get; set; } = 1;

    [ExportGroup("Debug Settings")]
    [Export] public bool ShowDebugWindow { get; set; } = false;
    [Export] public bool IsCube { get; set; } = false;
    [Export] public bool IsCulling { get; set; } = true;
    [Export] public bool IsMorphing { get; set; } = true;
    [Export] public bool DisableVirtualTexturing { get; set; } = false;
    [Export] public bool DisableTesselation { get; set; } = false;

    [Export] public float Bias1 { get; set; }
    [Export] public float Bias2 { get; set; }
    [Export] public float Bias3 { get; set; }
    [Export] public bool Verbose { get => ComputeShaderDispatcher<Enum>.Verbose; set => ComputeShaderDispatcher<Enum>.Verbose = value; }


    public BindableShaderMaterial SurfaceShader { get; set; }
    public BindableShaderMaterial FramebufferShader { get; set; }

    public TerrainTessellator TerrainTessellator { get; private set; }
    public SparseVirtualTexture SparseVirtualTexture { get; private set; }

    public MultiMeshRD PlanetMultiMesh { get; private set; }

    private bool Quiting = false;

    #region  Planet Transforms
    public Transform3D PlanetTranslation { get; private set; } = Transform3D.Identity;
    public Transform3D PlanetRotation { get; private set; } = Transform3D.Identity;
    public Transform3D PlanetScale { get; private set; } = Transform3D.Identity;

    [Export] public TextureRect A;

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

    public Transform3D GetPlanetTransformMatrix()
    {
        return PlanetTranslation * PlanetRotation * PlanetScale;
    }
    public Transform3D GetPlanetTRMatrix()
    {
        return PlanetTranslation * PlanetRotation;
    }
    public Vector3 TransformPoint(Vector3 point)
    {
        return GetPlanetTRMatrix().Inverse() * point;
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
            if (FramebufferShader != null)
            {
                FramebufferShader.UnbindAll();
                RenderingServer.FreeRid(FramebufferShader.GetRid());
            }

            PlanetMultiMesh?.CleanupGPU();
            PlanetMultiMesh = null;

            SurfaceShader = null;
            FramebufferShader = null;

            TerrainTessellator?.CleanupGPUResources();
            SparseVirtualTexture?.CleanupGPUResources();

            TerrainTessellator = null;
            SparseVirtualTexture = null;
        }
    }

    private readonly List<Rid> _terrainInstances = [];
    public override void _Ready()
    {
        SurfaceShader = new() { Shader = GD.Load<Shader>(ShaderPaths.SURFACE_SHADER_PATH) };
        FramebufferShader = new BindableShaderMaterial() { Shader = GD.Load<Shader>(ShaderPaths.FRAME_BUFFER_SHADER) };

        SetupCameras();
        SetupColliders();

        ScalePlanet(Vector3.One * Radius);
        TranslatePlanet(Vector3.Back * (1 - Radius));
        PlanetMultiMesh = new(MaximumKeys, Key.GetTriangleMesh(Resolution), -1);
        _terrainInstances.Add(PlanetMultiMesh.CreateMultimeshInstance(Transform3D.Identity, SurfaceShader.GetRid(), GetWorld3D().Scenario, 2 * Radius, 0b1u));
        _terrainInstances.Add(PlanetMultiMesh.CreateMultimeshInstance(Transform3D.Identity, FramebufferShader.GetRid(), CameraController.GetCamera("Lookup").GetWorld3D().Scenario, 2 * Radius, 0b1u));

        string saveName = !string.IsNullOrWhiteSpace(SaveManager.CurrentSave) ? SaveManager.CurrentSave : "Test";
        TerrainTessellator = new(this, PlanetMultiMesh, MainCamera, CameraController.GetCamera("Helper"));
        SparseVirtualTexture = new(SaveManager.GetSave(saveName), CameraController.GetCamera("Lookup").GetViewport());

        SurfaceShaderBindParameters();
        FramebufferShaderBindParameters();

        if (ShowDebugWindow)
            SparseVirtualTexture.CreateDebugWindow(this);
    }

    #region Process
    public override void _Process(double delta)
    {
        if (Quiting)
            return;

        SparseVirtualTexture.Paused = DisableVirtualTexturing;
        TerrainTessellator.Paused = DisableTesselation;

        InvokeTerrainRenderer();
    }
    #endregion

    public void InvokeTerrainRenderer()
    {
        if (TerrainTessellator == null || SparseVirtualTexture == null) return;
        if (TerrainTessellator == null) return;
        TerrainTessellator.Invoke();
        SparseVirtualTexture.Invoke();

        UIController.SetCurrentLOD(TerrainTessellator.CurrentLod);
        UIController.SetLabelKeyCount(TerrainTessellator.CulledCount, TerrainTessellator.TotalCount);

        SurfaceShader?.UpdateFrameDependentParameters();
        FramebufferShader?.UpdateFrameDependentParameters();
    }

    public void SetupCameras()
    {
        MainCamera = (OrbitalCamera3D)CameraController.GetCamera("Main");
        CustomCamera helperCamera = CameraController.GetCamera("Helper");
        CustomCamera lookupCamera = CameraController.GetCamera("Lookup");

        helperCamera.Follow(MainCamera);
        lookupCamera.Follow(MainCamera);

        MainCamera.Far = 32768; // Max far value for cameras
        helperCamera.Far = MainCamera.Far;
        lookupCamera.Far = MainCamera.Far;

        MainCamera.MinDistance = Radius + 0.999f;
        MainCamera.MaxDistance = MainCamera.Far - Radius;

        MainCamera.GlobalPosition = Vector3.Back * MainCamera.DistanceFromTarget;
        CameraController.SetCurrent("Main");

        MainCamera.AddChild(helperCamera.GetFrustumMeshInstance());

        lookupCamera.SetSize(DisplayServer.WindowGetSize() / 4);
    }

    public const float MINIMUM_RADIUS_SCALE = 0.999f;
    public void SetupColliders()
    {
        SurfaceAttachment.CallDeferred("add_child", _debugPlot);
        _debugPlot.ExtraCullMargin = 2 * Radius;
        _debugPlot.Multimesh = new MultiMesh() { UseColors = true, Mesh = new SphereMesh() { RadialSegments = 8, Rings = 4, Material = new StandardMaterial3D() { VertexColorUseAsAlbedo = true }, Radius = PointRadius, Height = 2 * PointRadius }, TransformFormat = MultiMesh.TransformFormatEnum.Transform3D };

        InnerCollision = new();
        OuterCollision = new();
        CubicalCollision = new();

        CollisionShape3D InnerCollisionShape = new() { Shape = new SphereShape3D() { Radius = MINIMUM_RADIUS_SCALE * Radius } };
        CollisionShape3D OuterCollisionShape = new() { Shape = new SphereShape3D() { Radius = Radius + HeightScale } };
        CollisionShape3D CubicalCollisionShape = new() { Shape = new BoxShape3D() { Size = Vector3.One * 2 * Radius } };

        InnerCollision.CallDeferred("add_child", InnerCollisionShape);
        OuterCollision.CallDeferred("add_child", OuterCollisionShape);
        CubicalCollision.CallDeferred("add_child", CubicalCollisionShape);

        SurfaceAttachment.CallDeferred("add_child", InnerCollision);
        SurfaceAttachment.CallDeferred("add_child", OuterCollision);
        SurfaceAttachment.CallDeferred("add_child", CubicalCollision);
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
        Vector3 right =MainCamera.Basis.X;
        Vector3 up = forward.Cross(right).Normalized();
        

        RotatePlanet(right, rotationSpeed * by * _direction.Z);
        RotatePlanet(up, rotationSpeed * by * _direction.X);

        // External Objects that need to rotate to simulate the effect
        WorldEnvironment.Environment.SkyRotation = PlanetRotation.Basis.GetEuler();
        SurfaceAttachment.Transform = GetPlanetTRMatrix();
        MainCamera.DistanceFromTarget += zoomSpeed * Radius * _direction.Y * by;

        MainCamera.DistanceFromTarget = Mathf.Clamp(MainCamera.DistanceFromTarget, 1.06f, MainCamera.MaxDistance);
        UIController.SetDistance(MainCamera.DistanceFromTarget);

        HasMoved = !_direction.IsZeroApprox();

        _direction = _direction.Lerp(Vector3.Zero, (float)(MovementEasing * delta));
    }

    public override async void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent)
        {
            if (mouseEvent.ButtonIndex == MouseButton.Right && mouseEvent.Pressed)
            {
                MainLightSource.Transform = PlanetRotation.Inverse();
            }
            if (mouseEvent.ButtonIndex == MouseButton.Left && mouseEvent.Pressed)
            {
                // CustomCamera lookupCamera = CameraController.GetCamera("Lookup");

                // Vector2 mousePosition = MainCamera.GetViewport().GetMousePosition();
                // Vector2 viewportUV = mousePosition / (MainCamera.GetViewport().GetVisibleRect().Size - Vector2.One);


                // Vector2I viewportSize = (Vector2I)lookupCamera.GetViewport().GetVisibleRect().Size;
                // SparseVirtualTexture.ReadFramebuffer.Viewport.GetTexture().GetImage().SavePng("user://Snapshot.png");

                // Color color = SparseVirtualTexture.ReadFramebuffer.GetPixelAt((Vector2I)(viewportSize * viewportUV));


                // if (color == Colors.White)
                //     return;

                // int gridSize =  (int)SparseVirtualTexture.IndirectionTable.GridSize;
                // int totalTextureSubdivisions =  (int)SparseVirtualTexture.IndirectionTable.MipDepth;
                // int packed = Mathf.RoundToInt(color.B);
                // uint mipIndex = (uint)(packed >> 4) & 0xF;
                // uint normalId = (uint)(packed & 0xF);

                // int xCoord = (int)(color.R * gridSize) & 0xF; 
                // int yCoord = (int)(color.G * gridSize) & 0xF; 

                // string tilePath = $"{mipIndex}-{normalId}-{xCoord}-{yCoord}.png";
                // // GD.PrintS(tilePath, viewportUV, viewportSize);
                // uint slot = SparseVirtualTexture.IndirectionTable.GetSlot(new Vector3I(xCoord, yCoord, (int)(totalTextureSubdivisions * normalId + mipIndex)));
                // Image image = SparseVirtualTexture.HeightTileCache.GetTile(slot);

                // // int lod_size = (int)(Mathf.Pow(2, mipIndex));
                // // int lod_scale = (int)(Mathf.Pow(2, totalMips - 1 - mipIndex));



                // Vector3 rayOrigin = MainCamera.ProjectRayOrigin(mousePosition);
                // Vector3 rayEnd = rayOrigin + MainCamera.ProjectRayNormal(mousePosition) * RayLength;

                // Vector3 point = FindPointOnPlanetSurface(rayOrigin, rayEnd);

            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Quiting)
            return;

        ProcessMovement(delta);
    }

    #region Material Settings

    public void BindSharedShaderParameters(BindableShaderMaterial bindableShaderMaterial, CustomCamera main, CustomCamera helper)
    {
        bindableShaderMaterial.Bind("radius", () => Radius);
        bindableShaderMaterial.FrameDependentBind("height_scale", () => HeightScale);
        bindableShaderMaterial.Bind("resolution", () => Resolution);
        bindableShaderMaterial.Bind("maximum_lod", () => MaximumLod);
        bindableShaderMaterial.Bind("minimum_lod", () => MinimumLod);
        bindableShaderMaterial.FrameDependentBind("planet_transform_matrix", () => Utilities.ToProjection(GetPlanetTransformMatrix()));

        bindableShaderMaterial.FrameDependentBind("is_cube", () => IsCube);

        bindableShaderMaterial.FrameDependentBind("is_morphing", () => IsMorphing);
        bindableShaderMaterial.FrameDependentBind("morph_range", () => MorphRange);

        bindableShaderMaterial.FrameDependentBind("camera_position", () => main.GlobalPosition);
        bindableShaderMaterial.FrameDependentBind("fovy", () => Mathf.Tan(helper.GetCameraFov(true) / 2));
        bindableShaderMaterial.Bind("sub_factor", () => SubFactor);
        bindableShaderMaterial.Bind("total_texture_subdivisions", () => SparseVirtualTexture.IndirectionTable.MipDepth);
        bindableShaderMaterial.FrameDependentBind("lod_to_mip_map", () => SaveManager.GetCurrentSave().LodToMipMap);

        bindableShaderMaterial.Bind("tile_size", () => SparseVirtualTexture.TileSize);

        bindableShaderMaterial.Bind("height_map_tile_cache", () => SparseVirtualTexture.HeightTileCache.Cache);
        bindableShaderMaterial.Bind("terrain_indirection_table", () => SparseVirtualTexture.IndirectionTable.Table);
        bindableShaderMaterial.Bind("base_mesh_data", () => TerrainTessellator.ExecuteTessellationPass.MeshImageData);

    }

    public void SurfaceShaderBindParameters()
    {
        BindSharedShaderParameters(SurfaceShader, CameraController.GetCamera("Main"), CameraController.GetCamera("Helper"));
        SurfaceShader.Bind("albedo_tile_cache", () => SparseVirtualTexture.AlbedoTileCache.Cache);
        SurfaceShader.FrameDependentBind("normal_strength", () => NormalStrength);
        SurfaceShader.UpdateAllParameters();
    }

    public void FramebufferShaderBindParameters()
    {
        BindSharedShaderParameters(FramebufferShader, CameraController.GetCamera("Main"), CameraController.GetCamera("Helper"));
        FramebufferShader.UpdateAllParameters();
    }

    private const string POSITION = "position";
    public Vector3 FindPointOnPlanetSurface(Vector3 from, Vector3 to)
    {
        PhysicsDirectSpaceState3D spaceState = GetWorld3D().DirectSpaceState;
        Rid inner = InnerCollision.GetRid();
        Rid outer = OuterCollision.GetRid();
        Rid cube = CubicalCollision.GetRid();

        Dictionary[] intersections =
        [
            spaceState.IntersectRay(new PhysicsRayQueryParameters3D()
            {
                To = to,
                From = from,
                Exclude = [inner, cube]
            }),
            spaceState.IntersectRay(new PhysicsRayQueryParameters3D()
            {
                To = to,
                From = from,
                Exclude = [outer, cube]
            }),
            spaceState.IntersectRay(new PhysicsRayQueryParameters3D()
            {
                To = from,
                From = to,
                Exclude = [outer, cube]
            }),
            spaceState.IntersectRay(new PhysicsRayQueryParameters3D()
            {
                To = from,
                From = to,
                Exclude = [inner, cube]
            }),
        ];

        int identity = (intersections[0].ContainsKey(POSITION) ? 1 << 3 : 0)
                     | (intersections[1].ContainsKey(POSITION) ? 1 << 2 : 0)
                     | (intersections[2].ContainsKey(POSITION) ? 1 << 1 : 0)
                     | (intersections[3].ContainsKey(POSITION) ? 1 : 0);

        Vector3 start;
        Vector3 end;



        switch (identity)
        {
            case 15:
                start = (Vector3)intersections[0][POSITION];
                end = (Vector3)intersections[1][POSITION];
                break;
            case 9:
                start = (Vector3)intersections[0][POSITION];
                end = (Vector3)intersections[3][POSITION];
                break;
            case 7:
                start = from;
                end = (Vector3)intersections[1][POSITION];
                break;
            case 1:
                start = from;
                end = (Vector3)intersections[3][POSITION];
                break;
            default:
                _debugPlot.Multimesh.InstanceCount = 0;
                return Vector3.Inf;
        }


        // GD.Print(start);

        start = GetPlanetTRMatrix().Inverse() * start;
        end = GetPlanetTRMatrix().Inverse() * end;

        int amount = 10 * Mathf.RoundToInt(start.DistanceTo(end));
        GD.PrintS(amount, Mathf.RoundToInt(start.DistanceTo(end)), 2 * Radius / Mathf.Pow(2, TerrainTessellator.CurrentLod));

        // amount = (int)(Mathf.RoundToInt(start.DistanceTo(end)) / (2 * Radius / TerrainTessellator.CurrentLod));
        _debugPlot.Multimesh.InstanceCount = 2 * amount;

        // Vector2I size = heightMap.GetSize();
        for (int i = 0; i < amount; i++)
        {
            Vector3 localPosition = start.Lerp(end, i / (amount - 1f));

            Vector3 directPath = localPosition;
            Vector3 terrainPath = localPosition.Normalized();

            // Vector2 uv = VectorUtils.PointOnSphereToUV(terrainPath);
            // Vector2I pixel = new(Mathf.RoundToInt(size.X * uv.X), Mathf.RoundToInt(size.Y * uv.Y));
            // pixel = pixel.Clamp(Vector2I.Zero, size - Vector2I.One);
            // float height = heightMap.GetPixelv(pixel).R * HeightScale;

            terrainPath *= Radius;// + height;

            _debugPlot.Multimesh.SetInstanceColor(2 * i + 0, Colors.Red);
            _debugPlot.Multimesh.SetInstanceTransform(2 * i + 0, new(Basis.Identity, directPath));
            _debugPlot.Multimesh.SetInstanceColor(2 * i + 1, Colors.Blue);
            _debugPlot.Multimesh.SetInstanceTransform(2 * i + 1, new(Basis.Identity, terrainPath));

            if (terrainPath.Length() >= directPath.Length())
            {
                return terrainPath;
            }

        }

        // _debugPlot.Multimesh.InstanceCount = 0;
        return Vector3.Inf;

    }
    #endregion
}
