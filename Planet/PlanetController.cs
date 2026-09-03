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

    public float Radius => TessellationData.Radius;

    [ExportGroup("Controllers")]
    [Export] public CameraController CameraController { get; private set; }
    [Export] public Node3D SurfaceAttachment { get; private set; }
    [Export] public DrawingController DrawingController { get; private set; }
    public Node3D CollisionTestSpheres = new();

    // [Export] public UIController UIController { get; private set; }

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

    [ExportGroup("Debug Settings")]
    [Export] public float PointRadius { get; set; }

    private float HeightOffset;

    PlanetRenderer PlanetRenderer;

    private bool Quiting = false;

    #region Planet Transforms
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

    public Vector3 LocalToPlanet(Vector3 localPoint)
    {
        return PlanetScale * localPoint;
    }

    public Vector3 PlanetToLocal(Vector3 planetPoint)
    {
        return PlanetScale.AffineInverse() * planetPoint;
    }

    public Vector3 PlanetToWorld(Vector3 planetPoint)
    {
        return PlanetTranslation * (PlanetRotation * planetPoint);
    }

    public Vector3 WorldToPlanet(Vector3 worldPoint)
    {
        Vector3 translatedPoint = PlanetTranslation.AffineInverse() * worldPoint;
        return PlanetRotation.AffineInverse() * translatedPoint;
    }

    public Vector3 LocalToWorld(Vector3 localPoint)
    {
        return PlanetToWorld(LocalToPlanet(localPoint));
    }

    public Vector3 WorldToLocal(Vector3 worldPoint)
    {
        return PlanetToLocal(WorldToPlanet(worldPoint));
    }

    #endregion

    //TODO really look into cleaning the gpu idk why these come here null
    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest || what == NotificationPredelete)
        {
            Quiting = true;
            PlanetRenderer.CleanupGPU();
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

        DrawingController.PlanetController = this;
        DrawingController.PlanetRenderer = PlanetRenderer;

        BindDebugSettings();
    }

    private void BindDebugSettings()
    {
        DebugMenuController.Instance.AddActionButton("Save Current Settings", null, () =>
        {
            SaveManager.OverrideSave(SaveManager.CurrentSave, SaveManager.CurrentWorldSave);
        }, 999);

        DebugMenuController.Instance.AddActionButton("Quit", null, () =>
        {
            Quiting = true;

            DebugMenuController.Instance.Clear();
            PlanetRenderer.CleanupGPU();
            GetTree().ChangeSceneToFile("res://main.tscn");
        }, 1000);
    }

    #region Process

    private double _heightUpdateTimer;
    private float _targetHeightOffset;
    private float _heightInterpolationSpeed = HEIGHT_INTERPOLATION_SPEED;

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

            float heightOffset = GetHeightAtPoint(MainCamera.GlobalPosition);

            if (!float.IsNaN(heightOffset))
            {
                _targetHeightOffset = heightOffset;

                _heightInterpolationSpeed = MainCamera.DistanceFromTarget > heightOffset
                    ? HEIGHT_INTERPOLATION_SPEED
                    : HEIGHT_INTERPOLATION_SPEED * HEIGHT_UNDERGROUND_INTERPOLATION_SPEED;
            }
        }

        HeightOffset = Mathf.Lerp(
            HeightOffset,
            _targetHeightOffset,
            1.0f - Mathf.Exp(-_heightInterpolationSpeed * (float)delta)
        );

        if (PlanetRenderer != null)
        {
            PlanetRenderer.Invoke(MainCamera, HeightOffset, GetPlanetTransform());

            // UIController.SetCurrentLOD(PlanetRenderer.TerrainTessellator.MaxLod);
        }

    }

    #endregion

    public void SetupCameras()
    {
        MainCamera = (OrbitalCamera3D)CameraController.GetCamera("Main");
        MainCamera.Far = 8 * Radius;

        MainCamera.MinDistance = Radius + 0.999f;
        MainCamera.MaxDistance = MainCamera.Far - Radius;

        MainCamera.DistanceFromTarget = Radius;

        MainCamera.GlobalPosition = Vector3.Back * MainCamera.DistanceFromTarget;

        CameraController.SetCurrent("Main");

        MainCamera.SetFrustumMeshInstance(
            TessellationData.CullingMargin,
            TessellationData.CullingDepth
        );
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

        SurfaceAttachment.Transform = PlanetTranslation * PlanetRotation;

        MainCamera.DistanceFromTarget += zoomSpeed * Radius * _direction.Y * by;

        MainCamera.DistanceFromTarget = Mathf.Clamp(
            MainCamera.DistanceFromTarget,
            HeightOffset + MinDistanceFromSurface,
            MainCamera.MaxDistance
        );

        HasMoved = !_direction.IsZeroApprox() || MainCamera.HasMoved;

        _direction = _direction.Lerp(
            Vector3.Zero,
            (float)(MovementEasing * delta)
        );
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

    private bool _leftMouseHeld = false;

    public override async void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent)
        {



            if (mouseEvent.ButtonIndex == MouseButton.Right && mouseEvent.Pressed)
            {
                MainLightSource.Transform = PlanetRotation.Inverse();

                if (false)
                {
                    Godot.Collections.Dictionary result = Utilities.RaycastFromMouse(
                        MainCamera,
                        1_000_000
                    );

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
                {
                    CollisionTestSpheres.AddChild(
                        Utilities.SpawnTestSphere(
                            WorldToPlanet(MainCamera.GlobalPosition),
                            1
                        )
                    );
                }
            }

            _leftMouseHeld = mouseEvent.ButtonIndex == MouseButton.Left && mouseEvent.Pressed;


            if (mouseEvent.ButtonIndex == MouseButton.Left && mouseEvent.Pressed)
            {
                // Vector3 planetMousePoint = GetPlanetMousePosition();
                // if (TryGetPlanetSurfacePoint(PlanetToLocal(planetMousePoint), out PlanetSurfacePoint surfacePoint, true))
                //     PlanetRenderer.Draw(surfacePoint);

                if (false)
                {
                    Vector3 position = MainCamera.GlobalPosition;
                    PlanetCollisionController.CreateCollisionPlane(position);
                }
            }
        }
    }

    public readonly struct PlanetSurfacePoint(Vector3 localSpherePoint, Vector3 localCubePoint, int normalId, Vector2 uv, uint mipIndex)
    {
        public readonly Vector3 LocalSpherePoint = localSpherePoint;
        public readonly Vector3 LocalCubePoint = localCubePoint;
        public readonly int NormalId = normalId;
        public readonly Vector2 UV = uv;
        public readonly uint MipIndex = mipIndex;

        public bool Equals(PlanetSurfacePoint other)
        {
            return UV == other.UV &&
                NormalId == other.NormalId &&
                MipIndex == other.MipIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is PlanetSurfacePoint other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(UV, NormalId, MipIndex);
        }

    }

    public bool TryGetMouseSurfacePoint(out PlanetSurfacePoint surfacePoint, bool isLocalSpace = false, uint? desiredMipIndex = null)
    {
        Vector3 planetMousePoint = GetPlanetMousePosition(true);
        return TryGetSurfacePoint(planetMousePoint, out surfacePoint, isLocalSpace, desiredMipIndex);
    }
    public bool TryGetSurfacePoint(Vector3 point, out PlanetSurfacePoint surfacePoint, bool isLocalSpace = false, uint? desiredMipIndex = null)
    {
        (Vector3 localSpherePoint, Vector3 localCubePoint) = GetLocalPointsOnPlanet(point, isLocalSpace);

        if (!localSpherePoint.IsFinite() || !localCubePoint.IsFinite())
        {
            surfacePoint = default;
            return false;
        }

        Vector3 normal = VectorUtils.IsolateNormal(localCubePoint);
        int normalId = VectorUtils.NormalToNormalID[normal];
        Vector2 uv = VectorUtils.PointOnCubeToPlaneUV(normalId, localCubePoint);

        uint mip;
        if (desiredMipIndex != null)
        {
            mip = desiredMipIndex.Value;
        }
        else
        {
            int lod = Mathf.CeilToInt(GetLodOfPoint(LocalToPlanet(localSpherePoint), false));
            mip = VirtualTextureData.GetMipIndex(VirtualTextureData.LodToMipMap[lod]);
        }

        surfacePoint = new PlanetSurfacePoint(
            localSpherePoint,
            localCubePoint,
            normalId,
            uv,
            mip
        );

        return true;
    }

    public (Vector3 localSpherePoint, Vector3 localCubePoint) GetLocalPointsOnPlanet(Vector3 point, bool isLocalSpace)
    {
        Vector3 localPoint = isLocalSpace
            ? point
            : WorldToLocal(point);

        if (localPoint.IsZeroApprox() || !localPoint.IsFinite())
            return (Vector3.Inf, Vector3.Inf);

        Vector3 localSpherePoint = localPoint.Normalized();
        Vector3 localCubePoint = VectorUtils.PointOnSphereToPointOnCube(localSpherePoint);

        return (localSpherePoint, localCubePoint);
    }

    public Vector3 GetPlanetMousePosition(bool localPosition = false)
    {
        Vector3 position = GetPlanetScreenPosition(
            MainCamera.GetViewport().GetMousePosition()
        );

        return localPosition ? PlanetToLocal(position) : position;
    }

    public Vector3 GetPlanetScreenPosition(Vector2 mousePosition)
    {
        Vector3 localPickedPosition = PlanetRenderer.SparseVirtualTexture.GetLocalMousePosition(
            mousePosition,
            MainCamera.GetViewport().GetVisibleRect().Size
        );

        if (!localPickedPosition.IsFinite())
            return Vector3.Inf;

        return LocalToPlanet(localPickedPosition);
    }

    public float GetLodOfPoint(Vector3 point, bool inWorldSpace)
    {
        Vector3 planetPoint = inWorldSpace ? WorldToPlanet(point) : point;
        Vector3 cameraPlanetPoint = WorldToPlanet(MainCamera.GlobalPosition);

        float distanceToCamera = planetPoint.DistanceTo(cameraPlanetPoint);

        float numerator = (distanceToCamera - HeightOffset) * Mathf.Tan(MainCamera.GetCameraFov(true) / 2);
        float denominator = Mathf.Sqrt2 * TessellationData.SubFactor * Radius;

        return Mathf.Clamp(
            -Mathf.Log(numerator / denominator) / Mathf.Log(2.0f),
            TessellationData.MinimumLod,
            TessellationData.MaximumLod
        );
    }

    public float GetHeightAtPoint(Vector3 point)
    {
        if (!TryGetSurfacePoint(point, out PlanetSurfacePoint surfacePoint))
            return float.NaN;

        uint mip = PlanetRenderer.SparseVirtualTexture.SampleConsolidatedIndirectionTexture(surfacePoint.NormalId, surfacePoint.UV);

        float mipGridSize = VirtualTextureData.GetMipSize(mip);

        Vector2I tileCoords = (Vector2I)(surfacePoint.UV * mipGridSize).Floor();

        string path = $"{VirtualTextureData.GetRealMipIndex(mip)}_{surfacePoint.NormalId}_{tileCoords.X}_{tileCoords.Y}";

        Image heightmap = PlanetRenderer.SparseVirtualTexture.HeightTileCache.GetTileImage(path);

        if (heightmap == null)
            return float.NaN;

        Vector2 tileMinUV = new Vector2(tileCoords.X, tileCoords.Y) / mipGridSize;

        Vector2 tileLocalUV = (surfacePoint.UV - tileMinUV) * mipGridSize;

        float elevation = Sampler.SampleBilinear(heightmap, tileLocalUV).R;

        return elevation * Radius * TessellationData.HeightScale;
    }

    private void SpawnSphereAtMouse()
    {
        Vector3 planetSpacePoint = GetPlanetMousePosition();

        if (!planetSpacePoint.IsFinite())
            return;

        Vector3 localSpacePoint = PlanetToLocal(planetSpacePoint);

        if (!TryGetSurfacePoint(localSpacePoint, out PlanetSurfacePoint surfacePoint, true))
            return;

        MeshInstance3D mesh = new()
        {
            Mesh = new SphereMesh()
            {
                Radius = PointRadius,
                Height = PointRadius * 2
            },
            Position = planetSpacePoint,
            MaterialOverride = new StandardMaterial3D()
            {
                AlbedoColor = new Color(
                    surfacePoint.UV.X,
                    surfacePoint.UV.Y,
                    0
                )
            }
        };

        SurfaceAttachment.AddChild(mesh);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Quiting)
            return;

        ProcessMovement(delta);

        Vector3 planetCenter = PlanetToWorld(Vector3.Zero);

        foreach (RigidBody3D body in CollisionTestSpheres.GetChildren().Cast<RigidBody3D>())
        {
            Vector3 toPlanet = planetCenter - body.GlobalPosition;
            float distance = toPlanet.Length();

            if (distance <= 0.0001f)
                continue;

            Vector3 direction = toPlanet / distance;

            body.ApplyCentralForce(
                direction * 50.0f
            );
        }
    }

    #region Material Settings

    #endregion
}