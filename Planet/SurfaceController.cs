using Godot;
using System;
using Dispatcher;
using Uniform;
using Planet;
using System.Threading.Tasks;

public partial class SurfaceController : Node3D
{
	private PlanetController _planetController;
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

	private Vector2 _mouseCameraRotation;
	private Vector2 _keyCameraRotation;
	private Vector3 _direction = Vector3.Zero;
	public bool HasMoved { get; private set; }

	public const float MINIMUM_RADIUS_SCALE = 0.999f;

	private Callable _executeRenderSurface;
	private Callable _executeCopyKeys;

	public override void _Ready()
	{
		_planetController = (PlanetController)GetParent();
		_surfaceController = _planetController.SurfaceController;
		_cameraController = _planetController.CameraController;
		_planetData = _planetController.PlanetData;

		_planetData.ShaderMaterial.SetShaderParameter("fov", Mathf.Tan(Mathf.DegToRad(_cameraController.Fov) / 2));

		_planetData.Scaled(Vector3.One * _planetData.Radius);
		_planetData.Translate(Vector3.Back * (1 - _planetData.Radius));
		UpdateColliders();
		_planetData.InitializeVirtualTextures();
		InitializeComputeShaders();
		InvokeComputeShaders();
		Processing = true;
	}

	public void UpdateColliders()
	{
		CollisionShape3D InnerCollisionShape = InnerCollision.GetChild<CollisionShape3D>(0);
		CollisionShape3D OuterCollisionShape = OuterCollision.GetChild<CollisionShape3D>(0);
		CollisionShape3D CubicalCollisionShape = CubicalCollision.GetChild<CollisionShape3D>(0);

		// _shadowCaster.Mesh = new SphereMesh() { Radius = MINIMUM_RADIUS_SCALE * _planetData.Radius, Height = 2 * MINIMUM_RADIUS_SCALE * _planetData.Radius };

		((SphereShape3D)InnerCollisionShape.Shape).Radius = MINIMUM_RADIUS_SCALE * _planetData.Radius;
		((SphereShape3D)OuterCollisionShape.Shape).Radius = _planetData.Radius + _planetData.HeightScale;
		((BoxShape3D)CubicalCollisionShape.Shape).Size *= 2 * _planetData.Radius;
	}

	private void InitializeComputeShaders()
	{
		_rd = RenderingServer.GetRenderingDevice();

		SetUpMultimesh();

		_copyKeys = new CopyKeysDispatcher(_copyKeysShaderPath, ref _rd);
		_renderSurface = new RenderSurfaceDispatcher(_renderSurfaceShaderPath, ref _rd);

		_copyKeys.RenderSurfaceDispatcher = _renderSurface;
		_copyKeys.PlanetController = _planetController;

		_renderSurface.CopyKeysDispatcher = _copyKeys;
		_renderSurface.PlanetController = _planetController;

		_renderSurface.CreateUniforms();
		_copyKeys.CreateUniforms();

		Texture2Drd globalKeyData = _renderSurface.GetUniform<Texture2DUniform>(RenderSurfaceDispatcher.BufferNames.GLOBAL_KEYS_DATA).GetTexture2Drd();

		_planetData.ShaderMaterial.SetShaderParameter("global_key_data", globalKeyData);

		_executeRenderSurface = Callable.From(() => { _renderSurface.Ready(); });
		_executeCopyKeys = Callable.From(() => { _copyKeys.Ready(); });
	}

	private void SetUpMultimesh()
	{
		_planetData.GenerateMesh();
		_planetData.SetMaterialParameters();
	}

	public override void _Input(InputEvent @event)
	{

		if (_cameraController.Locked && @event is InputEventMouseMotion mouseMotionEvent)
		{
			_mouseCameraRotation = new Vector2(mouseMotionEvent.Relative.X, -mouseMotionEvent.Relative.Y) * MouseSensitivity;
		}

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

		// if (!_cameraController.Current) return;
		float by = (float)delta;

		_direction.X += Input.GetActionStrength("move_left") - Input.GetActionStrength("move_right");
		_direction.Y = Input.GetActionStrength("move_up") - Input.GetActionStrength("move_down");
		_direction.Z += Input.GetActionStrength("move_forward") - Input.GetActionStrength("move_backward");
		_direction = _direction.Clamp(-1, 1);

		_keyCameraRotation.X += Input.GetActionStrength("rotate_right") - Input.GetActionStrength("rotate_left");
		_keyCameraRotation.Y += Input.GetActionStrength("rotate_up") - Input.GetActionStrength("rotate_down");
		_keyCameraRotation = _keyCameraRotation.Clamp(-1, 1);

		if (_cameraController.Current)
			_mouseCameraRotation = _cameraController.Locked ? _mouseCameraRotation : Vector2.Zero;

		float adjectedOrbitSpeed = BaseOrbitSpeed * _cameraController.DistanceFromSurface / OrbitSpeedModifier;

		_planetData.Rotate(Vector3.Right, adjectedOrbitSpeed * by * _direction.Z);
		_planetData.Rotate(Vector3.Up, adjectedOrbitSpeed * by * _direction.X);
		_planetData.Rotate(Vector3.Back, by * (_keyCameraRotation.X + _mouseCameraRotation.X));


		_planetData.ShaderMaterial.SetShaderParameter("planet_transform_matrix", Utilities.ToProjection(_planetData.GetPlanetTransformMatrix()));

		// Look up and down rotations
		_cameraController.Rotation = _cameraController.Rotation with { X = Mathf.Clamp(_cameraController.Rotation.X + (by * (_keyCameraRotation.Y + _mouseCameraRotation.Y)), 0, Mathf.Pi - 0.0001f) };

		// External Objects that need to rotate to simulate the effect
		WorldEnvironment.Environment.SkyRotation = _planetData.Rotation.Basis.GetEuler();
		_planetController.SurfaceAttachment.Transform = _planetData.GetPlanetTRMatrix();

		_cameraController.DistanceFromSurface += _direction.Y * by * CalculateSpeed(_cameraController.DistanceFromSurface, 0, _planetData.Radius * 2, _cameraController.BaseZoomSpeed);
		_cameraController.DistanceFromSurface = Mathf.Clamp(_cameraController.DistanceFromSurface, 0, float.MaxValue);

		HasMoved = !_direction.IsZeroApprox() || !_keyCameraRotation.IsZeroApprox() || !_mouseCameraRotation.IsZeroApprox();
		// Reset or Lerp movement
		_mouseCameraRotation = Vector2.Zero;
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

			_planetData.ShaderMaterial.SetShaderParameter("global_key_data", new PlaceholderTexture2D());
			_copyKeys.CleanupGPU();
			_renderSurface.CleanupGPU();
		}
	}

	public bool Locked { get; private set; }
	public override void _PhysicsProcess(double delta)
	{
		ProcessMovement(delta);
		Locked = Processing;// && HasMoved;
		_planetData.ShaderMaterial.SetShaderParameter("camera_position", _cameraController.GlobalPosition);
		_planetData.ShaderMaterial.SetShaderParameter("sub_factor", _planetData.SubFactor * _planetData.Radius);

		if (Locked)
		{
			
			
			for (int i = 0; i < 3; i++)
			{
				
				InvokeComputeShaders();
			}

			Render();
			_planetController.CameraController.UIElements.UpdateProcessingText();
			Locked = false;
			// Processing = false;
		}
	}

	public void InvokeComputeShaders()
	{
		_renderSurface.GetUniform<Texture2DUniform>(RenderSurfaceDispatcher.BufferNames.GLOBAL_KEYS_DATA).ClearTexture(Colors.Black);
		RenderingServer.CallOnRenderThread(_executeRenderSurface);
		RenderingServer.CallOnRenderThread(_executeCopyKeys);
		_renderSurface.UpdateUniforms();
	}

	private void Render()
	{
		_cameraController.UIElements.SetCurrentLOD(_renderSurface.GetUniform<Texture2DUniform>(RenderSurfaceDispatcher.BufferNames.GLOBAL_KEYS_DATA).GetPixel(0, 0).R);

		(int all, int culled) = _renderSurface.GetPrimitiveCounts();

		_cameraController.UIElements.SetLabelTriangleCount(culled, all);
	}
}
