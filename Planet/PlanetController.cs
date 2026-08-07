using Godot;
using PlanetGame.Rendering.Surface;
using Shaders;
using PlanetGame.Shaders;
using PlanetGame.Util;
using PlanetGame.Rendering.VirtualTexturing;
using System;
using System.Collections.Generic;
using PlanetGame.Shaders.Dispatchers;
using Godot.Collections;

public partial class PlanetController : Node3D
{
    [ExportGroup("Planet Settings")]
    [Export(PropertyHint.Range, "1,100000")] public float Radius { get; set; }
    [Export] public float HeightScale { get; set; }

    [ExportGroup("Controllers")]
    [Export] public CameraController CameraController { get; private set; }
    [Export] public Node3D SurfaceAttachment { get; private set; }
    [Export] public UIController UIController { get; private set; }
    public OrbitalCamera3D MainCamera { get; private set; }

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
    [Export] public bool IsCube { get; set; } = false;
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
    private Image _heightmap;
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
        
        TerrainTessellator = new(this, save, PlanetMultiMesh, MainCamera);

        SparseVirtualTexture = new(TerrainTessellator, save, PlanetMultiMesh.Mesh);

        BindShaderParameters(SurfaceShader, CameraController.GetCamera("Main"));

        _heightmap = SaveManager.GetBaseImages(SaveManager.CurrentSave)[SaveManager.SaveDataIdentifier.BASE_HEIGHT_MAP].GetImage();

        SparseVirtualTexture.CreateDebugWindow(UIController.DebugContainer, CameraController.GetCamera("Main"));
    }

    #region Process
    public override void _Process(double delta)
    {
        if (Quiting)
            return;

        ReorientatePlanet();
        SparseVirtualTexture.Paused = DisableVirtualTexturing;
        TerrainTessellator.Paused = DisableTesselation;


        InvokeTerrainRenderer();
    }
    #endregion

    public void InvokeTerrainRenderer()
    {
        if (TerrainTessellator == null || SparseVirtualTexture == null) return;

        // Vector3 point = FindPointOnPlanetSurface(MainCamera.GlobalPosition, GlobalPosition, 1);
        // float elevationAtPoint = point.DistanceTo(GlobalPosition) - Radius;
        // GD.Print(elevationAtPoint);

        TerrainTessellator.Invoke();
        SparseVirtualTexture.Invoke();

        UIController.SetCurrentLOD(TerrainTessellator.CurrentLod);
        UIController.SetLabelKeyCount(TerrainTessellator.CulledCount, TerrainTessellator.TotalCount);

        SurfaceShader?.UpdateFrameDependentParameters();
    }


    public void SetupCameras()
    {
        MainCamera = (OrbitalCamera3D)CameraController.GetCamera("Main");
        MainCamera.Far = 32768; // Max far value for cameras

        MainCamera.MinDistance = Radius + 0.999f;
        MainCamera.MaxDistance = MainCamera.Far - Radius;

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


        // Vector3 point = FindPointOnPlanetSurface(MainCamera.GlobalPosition, GlobalPosition, 1);

        // float distanceMetric = MainCamera.GlobalPosition.DistanceTo(point);
        // if (point == Vector3.Inf)  
        //     distanceMetric = MainCamera.DistanceFromTarget;
        // float elevationAtPoint = point.DistanceTo(GlobalPosition) - Radius;
        // GD.Print(elevationAtPoint);
        // distanceMetric = distanceMetric == float.MaxValue ? MainCamera.DistanceFromTarget : distanceMetric;

        float zoomSpeed = BaseZoomSpeed * (MainCamera.DistanceFromTarget / Radius);
        float rotationSpeed = BaseRotationSpeed * (MainCamera.DistanceFromTarget / Radius);

        Vector3 forward = PlanetTranslation.Origin.DirectionTo(MainCamera.GlobalPosition);
        Vector3 right = MainCamera.Basis.X;
        Vector3 up = forward.Cross(right).Normalized();


        RotatePlanet(right, rotationSpeed * by * _direction.Z);
        RotatePlanet(up, rotationSpeed * by * _direction.X);

        if (IsSimulateRotation)
        {
            // External Objects that need to rotate to simulate the rotation effect
            WorldEnvironment.Environment.SkyRotation = PlanetRotation.Basis.GetEuler();
            SurfaceAttachment.Transform = GetPlanetTRMatrix();
        }

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
            if (DisableInputMouseInput)
                return;

            if (mouseEvent.ButtonIndex == MouseButton.Right && mouseEvent.Pressed)
            {
                MainLightSource.Transform = PlanetRotation.Inverse();
            }
            if (mouseEvent.ButtonIndex == MouseButton.Left && mouseEvent.Pressed)
            {



                Image image = SparseVirtualTexture.SvtFeedbackRenderPass.GetPickingImage();


                Vector2 mousePosition = MainCamera.GetViewport().GetMousePosition();
                Vector2 viewportSize = MainCamera.GetViewport().GetVisibleRect().Size;

                Vector2 normalizedMousePosition = mousePosition / viewportSize;

                Vector2I pixelPosition = new(
                    Mathf.Clamp((int)(normalizedMousePosition.X * image.GetWidth()), 0, image.GetWidth() - 1),
                    Mathf.Clamp((int)(normalizedMousePosition.Y * image.GetHeight()), 0, image.GetHeight() - 1)
                );

                Color pickingData = image.GetPixelv(pixelPosition);

                // Vector3 coordinate =  new Vector3(0, 0, 1) * PlanetRotation;
                // Color pickingData = new(coordinate.X, coordinate.Y, coordinate.Z, 1);
                if (pickingData.A != -1)
                {
                    Vector3 localSpaceMousePosition = new(pickingData.R, pickingData.G, pickingData.B);

                    Vector3 worldSpacePosition = PlanetTranslation * PlanetRotation * PlanetScale * localSpaceMousePosition;

    
                    float distance = worldSpacePosition.DistanceTo(MainCamera.GlobalPosition);

                    float num = Mathf.Sqrt2 * SubFactor * Radius;
                    float den = distance * Mathf.Tan(MainCamera.GetCameraFov(true) / 2);

                    int lod = Mathf.FloorToInt(Mathf.Log(num / den) / Mathf.Log(2));

                    Vector2 uv = VectorUtils.PointOnSphereToUV(localSpaceMousePosition);
                    

                    GD.Print(SaveManager.GetCurrentSave().LodToMipMap[lod]);
                    // GD.Print(uv);







                    _debugPlot.Multimesh.InstanceCount = 1;
                    _debugPlot.Multimesh.SetInstanceColor(0, new Color(uv.X, uv.Y, 0, 1));
                    _debugPlot.Multimesh.SetInstanceTransform(0, new(Basis.Identity, localSpaceMousePosition * Radius));
                }


                // CustomCamera lookupCamera = CameraController.GetCamera("Lookup");

                // Vector2 viewportUV = mousePosition / (MainCamera.GetViewport().GetVisibleRect().Size - Vector2.One);


                // Vector2I viewportSize = (Vector2I)lookupCamera.GetViewport().GetVisibleRect().Size;
                // // SparseVirtualTexture.ReadFramebuffer.Viewport.GetTexture().GetImage().SavePng("user://Snapshot.png");

                // Color color = SparseVirtualTexture.ReadFramebuffer.GetPixelAt((Vector2I)(viewportSize * viewportUV));

                // Vector3 rayOrigin = MainCamera.ProjectRayOrigin(mousePosition);
                // Vector3 rayEnd = rayOrigin + MainCamera.ProjectRayNormal(mousePosition) * RayLength;

                // Vector3 point = FindPointOnPlanetSurface(rayOrigin, rayEnd, 10);

                // if (color == Colors.White)
                //     return;


                // int gridSize = (int)SparseVirtualTexture.IndirectionTable.GridSize;
                // int totalTextureSubdivisions = (int)SparseVirtualTexture.IndirectionTable.MipDepth;
                // int packed = Mathf.RoundToInt(color.B);
                // uint mipIndex = (uint)(packed >> 4) & 0xF;
                // uint normalId = (uint)(packed & 0xF);

                // int xCoord = (int)(color.R * gridSize) & 0xF;
                // int yCoord = (int)(color.G * gridSize) & 0xF;

                // string tilePath = $"{mipIndex}-{normalId}-{xCoord}-{yCoord}.png";
                // GD.PrintS(tilePath);
                // uint slot = SparseVirtualTexture.IndirectionTable.GetSlot(new Vector3I(xCoord, yCoord, (int)(totalTextureSubdivisions * normalId + mipIndex)));
                // Image image = SparseVirtualTexture.HeightTileCache.GetTile(slot);

                // // int lod_size = (int)(Mathf.Pow(2, mipIndex));
                // // int lod_scale = (int)(Mathf.Pow(2, totalMips - 1 - mipIndex));





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

    public void BindShaderParameters(BindableShaderMaterial bindableShaderMaterial, CustomCamera main)
    {
        VTData vtData = SaveManager.GetSVTData(SaveManager.GetCurrentSave());

        bindableShaderMaterial.FrameDependentBind("radius", () => Radius);
        bindableShaderMaterial.FrameDependentBind("height_scale", () => Radius * HeightScale);
        bindableShaderMaterial.FrameDependentBind("resolution", () => Resolution);
        bindableShaderMaterial.FrameDependentBind("maximum_lod", () => MaximumLod);
        bindableShaderMaterial.FrameDependentBind("minimum_lod", () => MinimumLod);
        bindableShaderMaterial.FrameDependentBind("planet_transform_matrix", () => Utilities.ToProjection(GetPlanetTransformMatrix()));

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
