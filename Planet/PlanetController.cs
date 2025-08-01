using Godot;
using PlanetGame.Rendering.Surface;
using Shaders;
using PlanetGame.ComputeShaders;
using PlanetGame.Util;
using PlanetGame.Rendering.VirtualTexturing;
using System;
using System.Collections.Generic;
using Uniform;
using System.Linq;
using PlanetGame.ComputeShaders.Dispatcher;

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

    [ExportGroup("Lighting")]
    [Export] public DirectionalLight3D MainLightSource { get; set; }
    [Export] public WorldEnvironment WorldEnvironment { get; set; }

    [ExportGroup("Movement Settings")]
    [Export] public float BaseZoomSpeed { get; set; }
    [Export] public float BaseRotationSpeed { get; set; }
    [Export] public float MovementEasing { get; set; }

    [ExportGroup("Rendering Settings")]
    [Export] public int MaximumNodes { get; set; } = 40000;
    [Export(PropertyHint.Range, "2,500,")] public int Resolution { get; set; } = 3;
    [Export] public Vector2 MorphRange { get; set; } = new(0, 0);
    [Export(PropertyHint.Range, "1, 10")] public float SubFactor { get; set; } = 4;
    [Export(PropertyHint.Range, "0, 31")] public int MaximumLOD { get; set; } = 12;
    [Export(PropertyHint.Range, "0, 4")] public int StartingLod { get; set; } = 1;
    [Export] public float NormalStrength { get; set; } = 1;

    [ExportGroup("Debug Settings")]
    [Export] public bool CubeMode { get; set; } = false;
    [Export] public bool Culling { get; set; } = true;
    [Export] public bool Morphing { get; set; } = true;
    [Export] public bool RenderFrameBufferOnTop { get; set; } = true;
    [Export] public float Bias1 { get; set; }
    [Export] public float Bias2 { get; set; }
    [Export] public float Bias3 { get; set; }
    [Export] public bool Verbose { get => ComputeShaderDispatcher<Enum>.Verbose; set => ComputeShaderDispatcher<Enum>.Verbose = value; }

    public BindableShaderMaterial SurfaceShader { get; set; }
    public BindableShaderMaterial FramebufferShader { get; set; }

    public TerrainTessellator TerrainTessellator { get; private set; }
    public SparseVirtualTexture SparseVirtualTexture { get; private set; }

    public MultiMeshRD PlanetMultiMesh { get; private set; }
    public ArrayMesh TriangleMesh { get; private set; }

    [Export] public bool Paused { get; set; } = false;
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


    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest || what == NotificationPredelete)
        {
            Quiting = true;
            // _terrainInstances.ForEach(x => RenderingServer.InstanceGeometrySetMaterialOverride(x, new Rid()));

            SurfaceShader.UnbindAll();
            FramebufferShader.UnbindAll();

            RenderingServer.FreeRid(SurfaceShader.GetRid());
            RenderingServer.FreeRid(FramebufferShader.GetRid());

            PlanetMultiMesh.CleanupGPU();
            PlanetMultiMesh = null;

            SurfaceShader = null;
            FramebufferShader = null;

            TerrainTessellator.CleanupGPUResources();
            SparseVirtualTexture.CleanupGPUResources();

            TerrainTessellator = null;
            SparseVirtualTexture = null;

            // ComputeShaderUniform.Uniforms.ForEach(x =>
            // {
            //     GD.PrintT(x);
            // });
        }
    }

    private readonly List<Rid> _terrainInstances = [];

    public override void _Ready()
    {
        SurfaceShader = new BindableShaderMaterial() { Shader = GD.Load<Shader>(ShaderPaths.SURFACE_SHADER_PATH) };
        FramebufferShader = new BindableShaderMaterial() { Shader = GD.Load<Shader>(ShaderPaths.FRAME_BUFFER_SHADER) };

        SetupCameras();

        ScalePlanet(Vector3.One * Radius);
        TranslatePlanet(Vector3.Back * (1 - Radius));

        TriangleMesh = GetTriangleMesh();

        PlanetMultiMesh = new(MaximumNodes, TriangleMesh.GetRid(), -1);
        _terrainInstances.Add(PlanetMultiMesh.CreateMultimeshInstance(Transform3D.Identity, SurfaceShader.GetRid(), GetWorld3D().Scenario, 2 * Radius, 0b1u));
        _terrainInstances.Add(PlanetMultiMesh.CreateMultimeshInstance(Transform3D.Identity, FramebufferShader.GetRid(), CameraController.GetCamera("Lookup").GetWorld3D().Scenario, 2 * Radius, 0b1u));

        string saveName = !string.IsNullOrWhiteSpace(SaveManager.CurrentSave) ? SaveManager.CurrentSave : "Test";
        TerrainTessellator = new(this, PlanetMultiMesh, MainCamera, CameraController.GetCamera("Helper"));
        SparseVirtualTexture = new(SaveManager.GetSave(saveName), CameraController.GetCamera("Lookup").GetViewport());

        SurfaceShaderBindParameters();
        FramebufferShaderBindParameters();
        SparseVirtualTexture.CreateDebugWindow(this);
    }

    public override void _Process(double delta)
    {
        if (Quiting || Paused)
            return;

        SurfaceShader.UpdateFrameDependentParameters();
        FramebufferShader.UpdateFrameDependentParameters();

        TerrainTessellator.Invoke();
        SparseVirtualTexture.Invoke();

        UIController.SetCurrentLOD(TerrainTessellator.CurrentLod);
        UIController.SetLabelKeyCount(TerrainTessellator.CulledCount, TerrainTessellator.TotalCount);
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

        Vector3 right = MainCamera.Basis.X.Cross(Vector3.Forward);
        RotatePlanet(MainCamera.Basis.X, rotationSpeed * by * _direction.Z);
        RotatePlanet(right, rotationSpeed * by * _direction.X);

        // External Objects that need to rotate to simulate the effect
        WorldEnvironment.Environment.SkyRotation = PlanetRotation.Basis.GetEuler();
        SurfaceAttachment.Transform = GetPlanetTRMatrix();
        MainCamera.DistanceFromTarget += zoomSpeed * Radius * _direction.Y * by;

        MainCamera.DistanceFromTarget = Mathf.Clamp(MainCamera.DistanceFromTarget, 0, MainCamera.MaxDistance);
        UIController.SetDistance(MainCamera.DistanceFromTarget);

        HasMoved = !_direction.IsZeroApprox();

        _direction = _direction.Lerp(Vector3.Zero, (float)(MovementEasing * delta));
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent)
        {
            if (mouseEvent.ButtonIndex == MouseButton.Right && mouseEvent.Pressed)
            {
                MainLightSource.Transform = PlanetRotation.Inverse();
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
        bindableShaderMaterial.Bind("maximum_lod", () => MaximumLOD);
        bindableShaderMaterial.FrameDependentBind("planet_transform_matrix", () => Utilities.ToProjection(GetPlanetTransformMatrix()));

        bindableShaderMaterial.FrameDependentBind("is_cube", () => CubeMode);
        bindableShaderMaterial.FrameDependentBind("is_culling", () => Culling);

        bindableShaderMaterial.FrameDependentBind("is_morphing", () => Morphing);
        bindableShaderMaterial.FrameDependentBind("morph_range", () => MorphRange);

        bindableShaderMaterial.FrameDependentBind("camera_position", () => main.GlobalPosition);
        bindableShaderMaterial.FrameDependentBind("fovy", () => Mathf.Tan(helper.GetCameraFov(true) / 2));
        bindableShaderMaterial.Bind("sub_factor", () => SubFactor);
        bindableShaderMaterial.Bind("total_texture_subdivisions", () => SparseVirtualTexture.IndirectionTable.MipDepth);
        bindableShaderMaterial.FrameDependentBind("lod_to_mip_map", () => SaveManager.GetCurrentSave().LodToMipMap);

        bindableShaderMaterial.Bind("tile_padding", () => SparseVirtualTexture.TilePadding);

        SurfaceShader.Bind("height_map_tile_cache", () => SparseVirtualTexture.HeightTileCache.Cache);
        SurfaceShader.Bind("terrain_indirection_table", () => SparseVirtualTexture.IndirectionTable.Table);
    }

    public void SurfaceShaderBindParameters()
    {
        BindSharedShaderParameters(SurfaceShader, CameraController.GetCamera("Main"), CameraController.GetCamera("Helper"));
        SurfaceShader.Bind("albedo_tile_cache", () => SparseVirtualTexture.AlbedoTileCache.Cache);
        SurfaceShader.FrameDependentBind("normal_strength", () => NormalStrength);
        SurfaceShader.FrameDependentBind("render_frame_buffer_on_top", () => RenderFrameBufferOnTop);
        SurfaceShader.UpdateAllParameters();
    }

    public void FramebufferShaderBindParameters()
    {
        BindSharedShaderParameters(FramebufferShader, CameraController.GetCamera("Main"), CameraController.GetCamera("Helper"));
        FramebufferShader.UpdateAllParameters();
    }

    public ArrayMesh GetTriangleMesh()
    {
        Vector3[] vertices = new Vector3[Resolution * (Resolution + 1) / 2];
        Vector3[] normals = new Vector3[Resolution * (Resolution + 1) / 2];
        int[] triangles = new int[(Resolution - 1) * (Resolution - 1) * 6 / 2];

        Vector3 normal = Vector3.Back;
        Vector3 axisA = new(normal.Y, normal.Z, normal.X);
        Vector3 axisB = normal.Cross(axisA).Abs();
        int triIndex = 0;
        int vertexIndex = 0;
        for (int y = 0; y < Resolution; y++)
        {
            for (int x = 0; x < Resolution - y; x++)
            {
                int currentIndex = vertexIndex++;
                Vector2 percentage = new Vector2(x, y) / (Resolution - 1);
                vertices[currentIndex] = normal + (percentage.X * axisA + percentage.Y * axisB);
                normals[currentIndex] = normal;

                if (x != Resolution - y - 1)
                {
                    if (x == Resolution - y - 2)
                    {
                        triangles[triIndex++] = currentIndex;
                        triangles[triIndex++] = currentIndex + 1;
                        triangles[triIndex++] = currentIndex + Resolution - y;
                    }
                    else
                    {
                        bool isXEven = x % 2 == 0;
                        bool isYEven = y % 2 == 0;

                        if ((isXEven && isYEven) || (!isXEven && !isYEven))
                        {
                            triangles[triIndex++] = currentIndex;
                            triangles[triIndex++] = currentIndex + Resolution - y + 1;
                            triangles[triIndex++] = currentIndex + Resolution - y;
                            triangles[triIndex++] = currentIndex;
                            triangles[triIndex++] = currentIndex + 1;
                            triangles[triIndex++] = currentIndex + Resolution - y + 1;
                        }
                        else
                        {
                            triangles[triIndex++] = currentIndex;
                            triangles[triIndex++] = currentIndex + 1;
                            triangles[triIndex++] = currentIndex + Resolution - y;
                            triangles[triIndex++] = currentIndex + 1;
                            triangles[triIndex++] = currentIndex + Resolution - y + 1;
                            triangles[triIndex++] = currentIndex + Resolution - y;
                        }
                    }
                }
            }
        }

        // string s = "[";
        // for (int i = 0; i < triangles.Length; i+=3)
        // {
        //     Vector2 A = VectorUtils.toVector2(vertices[triangles[i + 0]]);
        //     Vector2 B = VectorUtils.toVector2(vertices[triangles[i + 1]]);
        //     Vector2 C = VectorUtils.toVector2(vertices[triangles[i + 2]]);
        //     s += $"polygon({A}, {B}, {C}), ";
        // }
        // s = s[..^2] + "]";
        // GD.Print(s);

        Godot.Collections.Array arrays = [];
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Index] = triangles;
        arrays[(int)Mesh.ArrayType.Normal] = normals;

        ArrayMesh triangleMesh = new();
        triangleMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return triangleMesh;
    }

    #endregion
}
