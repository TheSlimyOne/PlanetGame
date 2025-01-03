using Godot;
using System;
using Dispatcher;
using Uniform;
using Planet;
using System.Threading.Tasks;

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

	[ExportGroup("Movement Settings")]
	[Export] private Vector2 MouseSensitivity = new Vector2(0.09f, 0.09f);

	[ExportSubgroup("Orbit Settings")]
	[Export] public float BaseOrbitSpeed { get; set; }
	[Export] public float OrbitSpeedModifier { get; set; }
	[Export] public float weight { get; set; }

	[ExportGroup("Shaders")]
	[Export(PropertyHint.File, "*.glsl")] private string _renderSurfaceShaderPath;
	[Export(PropertyHint.File, "*.glsl")] private string _copyKeysShaderPath;

	private RenderSurfaceDispatcher _renderSurface;
	private CopyKeysDispatcher _copyKeys;

	public bool Processing { get; set; }
	private RenderingDevice _rd;

	private Vector2 _keyCameraRotation;
	private Vector3 _direction = Vector3.Zero;
	public bool HasMoved { get; private set; }

	public const float MINIMUM_RADIUS_SCALE = 0.999f;

	private Callable _executeRenderSurface;
	private Callable _executeCopyKeys;

	public override void _Ready()
	{
		PlanetController = (PlanetController)GetParent();

		_planetData = PlanetController.PlanetData;

		_planetData.Scaled(Vector3.One * _planetData.Radius);
		_planetData.Translate(Vector3.Back * (1 - _planetData.Radius));
		InitializeComputeShaders();
	}

	private void InitializeComputeShaders()
	{
		_rd = RenderingServer.GetRenderingDevice();

		SetUpMultimesh();

		_copyKeys = new CopyKeysDispatcher(_copyKeysShaderPath, ref _rd);
		_renderSurface = new RenderSurfaceDispatcher(_renderSurfaceShaderPath, ref _rd);

		_copyKeys.RenderSurfaceDispatcher = _renderSurface;
		_copyKeys.PlanetData = _planetData;

		_renderSurface.CopyKeysDispatcher = _copyKeys;
		_renderSurface.PlanetData = _planetData;
		_renderSurface.Camera = PlanetController.CameraController.GetCamera("Helper");

		_renderSurface.CreateUniforms();
		_copyKeys.CreateUniforms();

		Texture2Drd globalKeyData = _renderSurface.GetUniform<Texture2DUniform>(RenderSurfaceDispatcher.BufferNames.GLOBAL_KEYS_DATA).GetTexture2Drd();

		_executeRenderSurface = Callable.From(() => { _renderSurface.Ready(); });
		_executeCopyKeys = Callable.From(() => { _copyKeys.Ready(); });
	}

	public Rid CreateMultimeshInstance(Transform3D transform, Rid senario, float extraVisibilityMargin, uint layerMask)
	{
		return _renderSurface.CreateMultimeshInstance(transform, senario, extraVisibilityMargin, layerMask);
	}

	private void SetUpMultimesh()
	{
		_planetData.GenerateMesh();
		_planetData.SetRenderSurfaceMaterialParameters();
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

		// float by = (float)delta;

		// _direction.X += Input.GetActionStrength("move_left") - Input.GetActionStrength("move_right");
		// _direction.Y = Input.GetActionStrength("move_up") - Input.GetActionStrength("move_down");
		// _direction.Z += Input.GetActionStrength("move_forward") - Input.GetActionStrength("move_backward");
		// _direction = _direction.Clamp(-1, 1);

		// _keyCameraRotation.X += Input.GetActionStrength("rotate_right") - Input.GetActionStrength("rotate_left");
		// _keyCameraRotation.Y += Input.GetActionStrength("rotate_up") - Input.GetActionStrength("rotate_down");
		// _keyCameraRotation = _keyCameraRotation.Clamp(-1, 1);



		// float adjectedOrbitSpeed = BaseOrbitSpeed * MainCamera.DistanceFromTarget / OrbitSpeedModifier;

		// _planetData.Rotate(Vector3.Right, adjectedOrbitSpeed * by * _direction.Z);
		// _planetData.Rotate(Vector3.Up, adjectedOrbitSpeed * by * _direction.X);
		// _planetData.Rotate(Vector3.Back, by * (_keyCameraRotation.X + MainCamera.MouseMotion.X));

		_planetData.RenderSurface.SetShaderParameter("planet_transform_matrix", Utilities.ToProjection(_planetData.GetPlanetTransformMatrix()));

		// Look up and down rotations
		// MainCamera.UpdateLookRotation(y: _keyCameraRotation.Y);

		// External Objects that need to rotate to simulate the effect
		WorldEnvironment.Environment.SkyRotation = _planetData.Rotation.Basis.GetEuler();
		PlanetController.SurfaceAttachment.Transform = _planetData.GetPlanetTRMatrix();

		// MainCamera.DistanceFromSurface += _direction.Y * by * CalculateSpeed(_cameraController.DistanceFromSurface, 0, _planetData.Radius * 2, _cameraController.BaseZoomSpeed);
		// _cameraController.DistanceFromSurface = Mathf.Clamp(_cameraController.DistanceFromSurface, 0, _cameraController.MaxDistance);

		HasMoved = !_direction.IsZeroApprox() || !_keyCameraRotation.IsZeroApprox();
		// Reset or Lerp movement
		// _mouseCameraRotation = Vector2.Zero;
		_keyCameraRotation = _keyCameraRotation.Lerp(Vector2.Zero, weight);
		_direction = _direction.Lerp(Vector3.Zero, weight);
	}

	float CalculateSpeed(float distanceFromSurface, float d_min, float d_max, float v_max)
	{
		// Ensure the distance is within the min and max range
		distanceFromSurface = Mathf.Clamp(distanceFromSurface, d_min, d_max);

		// Inverse relationship: speed decreases as distance decreases
		float t = (distanceFromSurface - d_min) / (d_max - d_min);

		// Calculate the speed
		return Mathf.Lerp(0.001f, v_max, t);
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
			(int all, int culled) = _renderSurface.GetPrimitiveCounts();
			// _cameraController.UIElements.SetLabelTriangleCount(culled, all);

			// PlanetController.CameraController.UIElements.UpdateProcessingText();
			Locked = false;
		}
	}

	public void InvokeComputeShaders()
	{
		_renderSurface.GetUniform<Texture2DUniform>(RenderSurfaceDispatcher.BufferNames.GLOBAL_KEYS_DATA).ClearTexture(Colors.Black);
		RenderingServer.CallOnRenderThread(_executeRenderSurface);
		_planetData.CurrentLod = _renderSurface.GetCurrentMaxLod();

		// _cameraController.UIElements.SetCurrentLOD(_planetData.CurrentLod);

		RenderingServer.CallOnRenderThread(_executeCopyKeys);
		_renderSurface.UpdateUniforms();
	}
}
