using Godot;
using System;
using Dispatcher;
using Uniform;
using Planet;
using System.Threading.Tasks;
using Godot.Collections;

public partial class SurfaceController : Node3D
{
	public PlanetController PlanetController { get; set; }
	private SurfaceController _surfaceController;
	private CameraController _cameraController;
	private PlanetData _planetData;

	[Export] public WorldEnvironment WorldEnvironment { get; set; }
	[Export] public DirectionalLight3D MainLightSource { get; set; }

	[ExportGroup("Colliders")]
	[Export] public StaticBody3D InnerCollision { get; set; }
	[Export] public StaticBody3D OuterCollision { get; set; }
	[Export] public StaticBody3D CubicalCollision { get; set; }
	[Export] private MeshInstance3D _shadowCaster;

	[ExportGroup("Shaders")]
	[Export(PropertyHint.File, "*.glsl")] private string _renderSurfaceShaderPath;
	[Export(PropertyHint.File, "*.glsl")] private string _copyKeysShaderPath;
	[Export(PropertyHint.File, "*.glsl")] private string _readFramebufferPath;

	private RenderSurfaceDispatcher _renderSurface;
	private CopyKeysDispatcher _copyKeys;
	private ReadFramebufferDispatcher _readFramebuffer;

	public bool Processing { get; set; }
	private RenderingDevice _rd;

	private Vector3 _direction = Vector3.Zero;
	public bool HasMoved { get; private set; }

	public const float MINIMUM_RADIUS_SCALE = 0.999f;

	private Callable _executeRenderSurface;
	private Callable _executeCopyKeys;
	private Callable _executeReadFramebuffer;

	public Array<Rid> Surfaces { get; private set; } = [];

	public override void _Ready()
	{
		PlanetController = (PlanetController)GetParent();

		_planetData = PlanetController.PlanetData;

		InitializeComputeShaders();
	}

	private void InitializeComputeShaders()
	{
		_rd = RenderingServer.GetRenderingDevice();

		SetUpMultimesh();

		_copyKeys = new CopyKeysDispatcher(_copyKeysShaderPath, ref _rd);
		_renderSurface = new RenderSurfaceDispatcher(_renderSurfaceShaderPath, ref _rd);
		_readFramebuffer = new ReadFramebufferDispatcher(_readFramebufferPath, ref _rd);

		_copyKeys.RenderSurfaceDispatcher = _renderSurface;
		_copyKeys.PlanetData = _planetData;

		_renderSurface.CopyKeysDispatcher = _copyKeys;
		_renderSurface.PlanetData = _planetData;
		_renderSurface.MainCamera = PlanetController.CameraController.GetCamera("Main");
		_renderSurface.HelperCamera = PlanetController.CameraController.GetCamera("Helper");

		_readFramebuffer.Viewport = PlanetController.CameraController.GetCamera("Lookup").GetViewport();

		_executeRenderSurface = Callable.From(_renderSurface.Ready);
		_executeCopyKeys = Callable.From(_copyKeys.Ready);
		_executeReadFramebuffer = Callable.From(_readFramebuffer.Ready);

		_renderSurface.CreateUniforms();
		_copyKeys.CreateUniforms();
		_readFramebuffer.CreateUniforms();

		// Texture2Drd globalKeyData = _renderSurface.GetUniform<Texture2DUniform>(RenderSurfaceDispatcher.BufferNames.GLOBAL_KEYS_DATA).GetTexture2Drd();
	}

	public Rid CreateMultimeshInstance(Transform3D transform, Rid senario, float extraVisibilityMargin, uint layerMask)
	{
		Rid surface = _renderSurface.CreateMultimeshInstance(transform, senario, extraVisibilityMargin, layerMask);
		Surfaces.Add(surface);
		return surface;
	}

	private void SetUpMultimesh()
	{
		_planetData.GenerateMesh();
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseEvent)
		{
			if (mouseEvent.ButtonIndex == MouseButton.Right && mouseEvent.Pressed)
			{
				MainLightSource.Transform = _planetData.Rotation.Inverse();
			}
		}
	}

	void ProcessMovement(double delta)
	{
		float by = (float)delta;
		_direction.X += Input.GetActionStrength("move_left") - Input.GetActionStrength("move_right");
		_direction.Y = Input.GetActionStrength("move_up") - Input.GetActionStrength("move_down");
		_direction.Z += Input.GetActionStrength("move_forward") - Input.GetActionStrength("move_backward");
		_direction = _direction.Clamp(-1, 1);

		float movementSpeed = CalculateSpeed(PlanetController.MainCamera.DistanceFromTarget);

		Vector3 right = PlanetController.MainCamera.Basis.X.Cross(Vector3.Forward);
		_planetData.Rotate(PlanetController.MainCamera.Basis.X, movementSpeed * by * _direction.Z);
		_planetData.Rotate(right, movementSpeed * by * _direction.X);

		// External Objects that need to rotate to simulate the effect
		WorldEnvironment.Environment.SkyRotation = _planetData.Rotation.Basis.GetEuler();
		PlanetController.SurfaceAttachment.Transform = _planetData.GetPlanetTRMatrix();
		PlanetController.MainCamera.DistanceFromTarget += ZoomSpeed * movementSpeed * _direction.Y * by;

		PlanetController.MainCamera.DistanceFromTarget = Mathf.Clamp(PlanetController.MainCamera.DistanceFromTarget, 0, PlanetController.MainCamera.MaxDistance);
		PlanetController.UIController.SetDistance(PlanetController.MainCamera.DistanceFromTarget);

		HasMoved = !_direction.IsZeroApprox();

		_direction = _direction.Lerp(Vector3.Zero, MovementEasing);
	}

	[ExportGroup("Movement Settings")]
	[Export] public float MaxSpeed { get; set; }
	[Export] public float ZoomSpeed { get; set; }
	[Export] public float MovementEasing { get; set; }
	public float CalculateSpeed(float distanceFromSurface)
	{
		float normalized = distanceFromSurface / PlanetController.MainCamera.MaxDistance;
		return 1 - Mathf.Pow(1 - normalized, 4);
	}

	public override void _Notification(int what)
	{
		if (what == NotificationWMCloseRequest || what == NotificationPredelete)
		{
			Processing = false;
			while (Locked)
			{
				Task.Delay(100);
			}

			_copyKeys.CleanupGPU();
			_renderSurface.CleanupGPU();
		}
	}

	public bool Locked { get; private set; }
	public override void _PhysicsProcess(double delta)
	{
		ProcessMovement(delta);



		Locked = Processing;// && HasMoved;

		if (Locked)
		{
			InvokeComputeShaders();
			Locked = false;
		}
	}

	public void InvokeComputeShaders()
	{
		_renderSurface.GetUniform<Texture2DUniform>(RenderSurfaceDispatcher.BufferNames.GLOBAL_KEYS_DATA).ClearTexture(Colors.Black);
		RenderingServer.CallOnRenderThread(_executeRenderSurface);

		_planetData.CurrentLod = _renderSurface.GetCurrentLod();
		PlanetController.UIController.SetCurrentLOD(_planetData.CurrentLod - 1);

		RenderingServer.CallOnRenderThread(_executeCopyKeys);
		_renderSurface.UpdateUniforms();

		(int all, int culled) = _renderSurface.GetPrimitiveCounts();
		PlanetController.UIController.SetLabelKeyCount(culled, all);
		PlanetController.UIController.UpdateProcessingText();

		_readFramebuffer.UpdateUniforms();
		RenderingServer.CallOnRenderThread(_executeReadFramebuffer);
	}
}
