using Godot;
using System;
using Shader;
using Uniform;
using Planet;

public partial class SurfaceController : MultiMeshInstance3D
{
	private PlanetController _planetController;
	private SurfaceController _surfaceController;
	private CameraController _cameraController;
	private PlanetData _planetData;

	[Export] public WorldEnvironment WorldEnvironment;
	[Export] public DirectionalLight3D MainLightSource;

	[ExportGroup("Colliders")]
	[Export] public StaticBody3D InnerCollision;
	[Export] public StaticBody3D OuterCollision;
	[Export] private MeshInstance3D _shadowCaster;

	[ExportGroup("Movement Settings")]
	[Export] private Vector2 MouseSensitivity = new Vector2(0.09f, 0.09f);

	[ExportSubgroup("Orbit Settings")]
	[Export] public float BaseOrbitSpeed;
	[Export] public float OrbitSpeedModifier;
	[Export] public float weight;

	[ExportGroup("Shaders")]
	[Export(PropertyHint.File)] private string _computeCullShaderPath;
	[Export(PropertyHint.File)] private string _computeCopyShaderPath;

	private CalculateSurfaceDispatcher _computeCullShader;
	private ComputeCopy _computeCopyShader;

	public bool Processing;
	private RenderingDevice _rd;

	private Vector2 _mouseCameraRotation;
	private Vector2 _keyCameraRotation;
	private Vector3 _direction = Vector3.Zero;
	public bool HasMoved { get; private set; }

	const float MINIMUM_RADIUS_SCALE = 0.999f;

	public override void _Ready()
	{
		_planetController = (PlanetController)GetParent();
		_surfaceController = _planetController.SurfaceController;
		_cameraController = _planetController.CameraController;
		_planetData = _planetController.PlanetData;

		_planetData.Scaled(Vector3.One * _planetData.Radius);
		_planetData.Translate(Vector3.Back * (1 - _planetData.Radius));
		UpdateColliders();
		InitializeComputeShaders();
	}

	public void UpdateColliders()
	{
		ExtraCullMargin = 2 * _planetData.Radius;

		CollisionShape3D InnerCollisionShape = InnerCollision.GetChild<CollisionShape3D>(0);
		CollisionShape3D OuterCollisionShape = OuterCollision.GetChild<CollisionShape3D>(0);

		_shadowCaster.Mesh = new SphereMesh() { Radius = MINIMUM_RADIUS_SCALE * _planetData.Radius, Height = 2 * MINIMUM_RADIUS_SCALE * _planetData.Radius };

		((SphereShape3D)InnerCollisionShape.Shape).Radius = MINIMUM_RADIUS_SCALE * _planetData.Radius;
		((SphereShape3D)OuterCollisionShape.Shape).Radius = _planetData.Radius + _planetData.HeightScale;
		
		GD.PrintS(((SphereShape3D)InnerCollisionShape.Shape).Radius, ((SphereShape3D)OuterCollisionShape.Shape).Radius);
	}

	private void InitializeComputeShaders()
	{
		Multimesh = _planetData.MultiMesh;
		_planetData.GenerateMulitMesh();

		_planetData.SetMaterialParameters();

		_rd = RenderingServer.GetRenderingDevice();
		_computeCullShader = new CalculateSurfaceDispatcher(_computeCullShaderPath, ref _rd);
		_computeCopyShader = new ComputeCopy(_computeCopyShaderPath, ref _rd);

		_computeCullShader.ComputeCopyShader = _computeCopyShader;
		_computeCullShader.PlanetController = _planetController;
		_computeCopyShader.ComputeCullShader = _computeCullShader;

		_computeCullShader.CreateUniforms();
		_computeCopyShader.CreateUniforms();

		Texture2Drd displayKeyData = _computeCullShader.GetUniform<Texture2DUniform>(CalculateSurfaceDispatcher.BufferNames.KEYS).GetTexture2Drd();
		Texture2Drd globalKeyData = _computeCullShader.GetUniform<Texture2DUniform>(CalculateSurfaceDispatcher.BufferNames.GLOBAL_KEYS_DATA).GetTexture2Drd();

		_planetData.ShaderMaterial.SetShaderParameter("key_image", displayKeyData);
		_planetData.ShaderMaterial.SetShaderParameter("global_key_data", globalKeyData);

		Processing = true;
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

	public override void _Notification(int what)
	{
		if (what == NotificationWMCloseRequest || what == NotificationPredelete)
		{
			Processing = false;
			_planetData.ShaderMaterial.SetShaderParameter("key_image", new PlaceholderTexture2D());
			_planetData.ShaderMaterial.SetShaderParameter("global_key_data", new PlaceholderTexture2D());
			_computeCullShader.CleanupGPU();
			_computeCopyShader.CleanupGPU();
			_rd.Free();
			_rd = null;
		}
	}
	int counter = 24;
	public override void _PhysicsProcess(double delta)
	{
		bool locked = Processing;// && HasMoved;
		ProcessMovement(delta);
		_planetData.ShaderMaterial.SetShaderParameter("camera_position", _cameraController.GlobalPosition);
		_planetData.ShaderMaterial.SetShaderParameter("fov", Mathf.DegToRad(_cameraController.Fov));
		_planetData.ShaderMaterial.SetShaderParameter("sub_factor", _planetData.SubFactor * _planetData.Radius);
		if (locked)
		{
			_computeCullShader.GetUniform<Texture2DUniform>(CalculateSurfaceDispatcher.BufferNames.KEYS).ClearTexture(Colors.Black);
			_computeCullShader.GetUniform<Texture2DUniform>(CalculateSurfaceDispatcher.BufferNames.GLOBAL_KEYS_DATA).ClearTexture(Colors.Black);
			_computeCopyShader.Ready();
			_rd.Submit();
			_rd.Sync();
			_computeCullShader.Ready();
			_rd.Submit();
			_rd.Sync();
			Render();
			_computeCullShader.UpdateUniforms();
		}
		// GD.Print($"After: {counter}"); // TODO maybe???
		// _processing = false;
	}

	void ProcessMovement(double delta)
	{
		float by = (float)delta;

		_direction.X += Input.GetActionStrength("move_left") - Input.GetActionStrength("move_right");
		_direction.Y = Input.GetActionStrength("move_up") - Input.GetActionStrength("move_down");
		_direction.Z += Input.GetActionStrength("move_forward") - Input.GetActionStrength("move_backward");
		_direction = _direction.Clamp(-1, 1);

		_keyCameraRotation.X += Input.GetActionStrength("rotate_right") - Input.GetActionStrength("rotate_left");
		_keyCameraRotation.Y += Input.GetActionStrength("rotate_up") - Input.GetActionStrength("rotate_down");
		_keyCameraRotation = _keyCameraRotation.Clamp(-1, 1);

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

		InnerCollision.Transform = _planetData.Translation;
		OuterCollision.Transform = _planetData.Translation;

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

	private void Render()
	{
		_cameraController.UIElements.SetCurrentLOD(_computeCullShader.GetUniform<Texture2DUniform>(CalculateSurfaceDispatcher.BufferNames.GLOBAL_KEYS_DATA).GetPixel(0, 0).R);

		(int all, int culled) = _computeCullShader.GetPrimitiveCounts();
		counter = all * 4;
		_cameraController.UIElements.SetLabelTriangleCount(culled, all);

		if (_planetData.Culling)
		{
			InstanceAllTriangles(culled);
		}
		else
		{
			Key[] keys = _computeCullShader.GetUniformData<Key>(CalculateSurfaceDispatcher.BufferNames.WRITE_FULL_LIST);
			InstanceAllTriangles(keys, all);
		}
	}

	public void InstanceAllTriangles(Key[] keys, int amount)
	{
		Multimesh.InstanceCount = amount;
		Transform3D transform = new(Basis.Identity, Vector3.Zero);
		for (int i = 0; i < amount; i++)
		{
			Multimesh.SetInstanceTransform(i, transform);
			Multimesh.SetInstanceCustomData(i, keys[i].ToColor());
		}
	}

	public void InstanceAllTriangles(int amount)
	{
		Multimesh.InstanceCount = amount;
		Transform3D transform = new(Basis.Identity, Vector3.Zero);
		for (int i = 0; i < amount; i++)
		{
			Multimesh.SetInstanceTransform(i, transform);
		}
	}

	public void GenerateNodeAtlas()
	{

	}
}
