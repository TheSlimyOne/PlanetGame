using Godot;
using PlanetGame.Util;
using System.Linq;
using PlanetGame.Util.DebugUIComponents;
using PlanetGame;
using PlanetGame.Data;
using PlanetGame.Planet.Rendering;

public partial class PlanetController : Node
{
    private static TessellationData TessellationData => SaveManager.TessellationData;
    private static VirtualTextureData VirtualTextureData => SaveManager.VirtualTextureData;

    public float Radius => TessellationData.Radius;

    [ExportGroup("Controllers")]
    [Export] public CameraController CameraController { get; private set; }
    [Export] public Node3D SurfaceAttachment { get; private set; }
    public PlanetDrawingController PlanetDrawingController { get; private set; }
    public Node3D CollisionTestSpheres = new();
    public PlanetQuery PlanetQuery;
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

    PlanetRenderer PlanetRenderer;
    PlanetSpatial PlanetSpatial;

    private bool Quiting = false;


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

        PlanetSpatial = new();
        PlanetRenderer = new(MainCamera, PlanetSpatial);
        PlanetQuery = new(MainCamera, PlanetSpatial, PlanetRenderer);
        PlanetCollisionController = new(PlanetQuery);

        PlanetDrawingController = ScenePaths.InstantiateScene<PlanetDrawingController>(ScenePaths.PLANET_DRAWING_CONTROLLER);
        PlanetDrawingController.Initialize(PlanetRenderer, PlanetQuery);
        AddChild(PlanetDrawingController);

        SurfaceAttachment.AddChild(PlanetCollisionController.CollisionBody);
        SurfaceAttachment.AddChild(CollisionTestSpheres);
        BindDebugSettings();
    }

    private void BindDebugSettings()
    {
        // DebugMenuController.Instance.AddActionButton("Save Current Settings", null, () =>
        // {
        //     SaveManager.OverrideSave(SaveManager.CurrentSave, SaveManager.CurrentWorldSave);
        // }, 999);

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

    public void UpdateHeightOffset(double delta)
    {
        _heightUpdateTimer += delta;

        if (_heightUpdateTimer >= HEIGHT_UPDATE_INTERVAL)
        {
            _heightUpdateTimer = 0;

            float heightOffset = PlanetQuery.GetHeightAtPoint(MainCamera.GlobalPosition);

            if (!float.IsNaN(heightOffset))
            {
                _targetHeightOffset = heightOffset;

                _heightInterpolationSpeed = MainCamera.DistanceFromTarget > heightOffset
                    ? HEIGHT_INTERPOLATION_SPEED
                    : HEIGHT_INTERPOLATION_SPEED * HEIGHT_UNDERGROUND_INTERPOLATION_SPEED;
            }
        }

        PlanetRenderer.HeightOffset = Mathf.Lerp(
            PlanetRenderer.HeightOffset,
            _targetHeightOffset,
            1.0f - Mathf.Exp(-_heightInterpolationSpeed * (float)delta)
        );
    }

    public override void _Process(double delta)
    {
        if (Quiting)
            return;

        PlanetSpatial.ReorientatePlanet();

        UpdateHeightOffset(delta);

        PlanetRenderer?.Invoke();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Quiting)
            return;

        ProcessMovement(delta);
        UpdateCamera();

        Vector3 planetCenter = PlanetSpatial.PlanetToWorld(Vector3.Zero);

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

    #endregion

    public void SetupCameras()
    {
        MainCamera = (OrbitalCamera3D)CameraController.GetCamera("Main");
        MainCamera.GlobalPosition = Vector3.Back * MainCamera.DistanceFromTarget;

        CameraController.SetCurrent("Main");
        MainCamera.DistanceFromTarget = Radius;

        UpdateCamera();

        MainCamera.SetFrustumMeshInstance(
            TessellationData.CullingMargin,
            TessellationData.CullingDepth
        );
    }

    public void UpdateCamera()
    {
        MainCamera.MinDistance = Radius + 0.999f;
        MainCamera.MaxDistance = Radius * 10.0f;

        MainCamera.Far = MainCamera.DistanceFromTarget + Radius;
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

        float minimumDistance = PlanetRenderer.HeightOffset + MinDistanceFromSurface;

        if (MainCamera.DistanceFromTarget < minimumDistance)
            MainCamera.DistanceFromTarget = minimumDistance;

        float altitude = Mathf.Max(MainCamera.DistanceFromTarget - PlanetRenderer.HeightOffset, 1.0f);
        float altitudeRatio = Mathf.Max(altitude / Radius, 0.0001f);
        float speedScale = Mathf.Pow(altitudeRatio, 1.1f);

        float zoomSpeed = BaseZoomSpeed * speedScale;
        float rotationSpeed = BaseRotationSpeed * speedScale;

        Vector3 forward = PlanetSpatial.PlanetTranslation.Origin.DirectionTo(MainCamera.GlobalPosition);
        Vector3 right = MainCamera.Basis.X;
        Vector3 up = forward.Cross(right).Normalized();

        PlanetSpatial.RotatePlanet(right, rotationSpeed * by * _direction.Z);
        PlanetSpatial.RotatePlanet(up, rotationSpeed * by * _direction.X);

        WorldEnvironment.Environment.SkyRotation = PlanetSpatial.PlanetRotation.Basis.GetEuler();

        SurfaceAttachment.Transform = PlanetSpatial.PlanetTranslation * PlanetSpatial.PlanetRotation;

        MainCamera.DistanceFromTarget += zoomSpeed * Radius * _direction.Y * by;

        MainCamera.DistanceFromTarget = Mathf.Clamp(
            MainCamera.DistanceFromTarget,
            PlanetRenderer.HeightOffset + MinDistanceFromSurface,
            MainCamera.MaxDistance
        );

        HasMoved = !_direction.IsZeroApprox() || MainCamera.HasMoved;

        _direction = _direction.Lerp(
            Vector3.Zero,
            (float)(MovementEasing * delta)
        );
    }

    private bool _leftMouseHeld = false;

    public override async void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent)
        {
            if (mouseEvent.ButtonIndex == MouseButton.Right && mouseEvent.Pressed)
            {
                MainLightSource.Transform = PlanetSpatial.PlanetRotation.Inverse();

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
                            PlanetSpatial.WorldToPlanet(MainCamera.GlobalPosition),
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

    private void SpawnSphereAtMouse()
    {
        Vector3 planetSpacePoint = PlanetQuery.GetPlanetMousePosition();

        if (!planetSpacePoint.IsFinite())
            return;

        Vector3 localSpacePoint = PlanetSpatial.PlanetToLocal(planetSpacePoint);

        if (!PlanetQuery.TryGetSurfacePoint(localSpacePoint, out PlanetQuery.PlanetSurfacePoint surfacePoint, true))
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
}