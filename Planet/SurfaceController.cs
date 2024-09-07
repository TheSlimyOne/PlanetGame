using Godot;
using System;
using Shader;
using Uniform;
using Planet;

public partial class SurfaceController : MultiMeshInstance3D
{
	[ExportGroup("Planet Properties")]
	private PlanetController _planetController;

	[Export] public WorldEnvironment WorldEnvironment;
	[Export] public DirectionalLight3D MainLightSource;

	[Export] public Node3D RotationGimbal;

	[ExportGroup("Colliders")]
	[Export] public StaticBody3D InnerCollision;
	[Export] public StaticBody3D OuterCollision;

	[ExportGroup("Movement Settings")]
	[Export] private Vector2 MouseSensitivity = new Vector2(0.09f, 0.09f);

	[ExportSubgroup("Orbit Settings")]
	[Export] public float BaseOrbitSpeed;
	[Export] public float OrbitSpeedModifier;
	[Export] public float weight;

	[ExportGroup("Shaders")]
	[Export(PropertyHint.File)] private string _computeCullShaderPath;
	[Export(PropertyHint.File)] private string _computeCopyShaderPath;

	private ComputeCull _computeCullShader;
	private ComputeCopy _computeCopyShader;

	public bool Processing;
	private RenderingDevice _rd;
	private RenderingDevice _normalRd;

	private Vector2 _mouseCameraRotation;
	private Vector2 _keyCameraRotation;
	private Vector3 _direction = Vector3.Zero;

	public override void _Ready()
	{
		_planetController = (PlanetController)GetParent().GetParent();

		float radius = _planetController.PlanetData.Radius;

		_planetController.PlanetData.Scaled(Vector3.One * radius);
		_planetController.PlanetData.Translate(Vector3.Back * (1 - radius));

		ExtraCullMargin = 2 * _planetController.PlanetData.Radius;

		InnerCollision.Transform = _planetController.PlanetData.Translation;
		OuterCollision.Transform = _planetController.PlanetData.Translation;
	
		CollisionShape3D InnerCollisionShape = InnerCollision.GetChild<CollisionShape3D>(0);
		CollisionShape3D OuterCollisionShape = OuterCollision.GetChild<CollisionShape3D>(0);

		((SphereShape3D)InnerCollisionShape.Shape).Radius = 0.99f * radius;
		((SphereShape3D)OuterCollisionShape.Shape).Radius = radius + _planetController.PlanetData.HeightScale;


		InitializeComputeShaders();
	}

    private void InitializeComputeShaders()
    {
        Multimesh = _planetController.PlanetData.GenerateMulitMesh();

		_planetController.PlanetData.SetMaterialParameters();

		_rd = RenderingServer.GetRenderingDevice();
		_computeCullShader = new ComputeCull(_computeCullShaderPath, ref _rd);
		_computeCopyShader = new ComputeCopy(_computeCopyShaderPath, ref _rd);

		_computeCullShader.ComputeCopyShader = _computeCopyShader;
		_computeCullShader.PlanetController = _planetController;
		_computeCopyShader.ComputeCullShader = _computeCullShader;
	
		_computeCullShader.CreateUniforms();
		_computeCopyShader.CreateUniforms();
		
		Texture2Drd displayKeyData = _computeCullShader.GetUniform<TextureUniform>(ComputeCull.BufferNames.KEYS).GetTexture2Drd();
		Texture2Drd globalKeyData = _computeCullShader.GetUniform<TextureUniform>(ComputeCull.BufferNames.KEYS).GetTexture2Drd();

		_planetController.PlanetData.ShaderMaterial.SetShaderParameter("key_image", displayKeyData);
		_planetController.PlanetData.ShaderMaterial.SetShaderParameter("global_key_data", globalKeyData);
		_planetController.CameraController.GetChild(0).GetChild<TextureRect>(1).Texture = displayKeyData;
		
		Processing = true;
    }

    public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseMotion mouseMotionEvent)
		{
			_mouseCameraRotation = new Vector2(mouseMotionEvent.Relative.X, -mouseMotionEvent.Relative.Y) * MouseSensitivity;
		}
	
		if (@event is InputEventMouseButton mouseEvent)
        {
            if (mouseEvent.ButtonIndex == MouseButton.Right && mouseEvent.Pressed)
            {
				MainLightSource.Transform = _planetController.PlanetData.Rotation.Inverse();
            }
        }
	}

	public override void _Notification(int what)
	{
		if (what == NotificationWMCloseRequest || what == NotificationPredelete)
		{
			Processing = false;
			_computeCullShader.CleanupGPU();
			_computeCopyShader.CleanupGPU();
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		bool locked = Processing;
		
		if (locked)
		{
			_computeCullShader.GetUniform<TextureUniform>(ComputeCull.BufferNames.KEYS).ClearTexture(Colors.Black);
			_computeCullShader.GetUniform<TextureUniform>(ComputeCull.BufferNames.GLOBAL_KEYS_DATA).ClearTexture(Colors.Black);
			_computeCopyShader.Ready();
			_rd.Submit();
			_rd.Sync();
			_computeCullShader.Ready();
			_rd.Submit();
		}
		
		ProcessMovement(delta);

		if (locked)
		{
			_rd.Sync();
			Render();
			_computeCullShader.UpdateUniforms();
		}
		// _processing = false;
	}

	void ProcessMovement(double delta)
	{
		float by = (float)delta;

		_direction.X += Input.GetActionStrength("move_left") - Input.GetActionStrength("move_right");
		_direction.Z += Input.GetActionStrength("move_forward") - Input.GetActionStrength("move_backward");
		_direction = _direction.Clamp(-1, 1);
		

		_keyCameraRotation.X += Input.GetActionStrength("rotate_right") - Input.GetActionStrength("rotate_left");
		_keyCameraRotation.Y += Input.GetActionStrength("rotate_up") - Input.GetActionStrength("rotate_down");
		_keyCameraRotation = _keyCameraRotation.Clamp(-1, 1);

		_mouseCameraRotation = _planetController.CameraController.Locked ? _mouseCameraRotation : Vector2.Zero;

		float adjectedOrbitSpeed = BaseOrbitSpeed * _planetController.CameraController.DistanceFromSurface / OrbitSpeedModifier;
		
		_planetController.PlanetData.Rotate(Vector3.Right, adjectedOrbitSpeed * by * _direction.Z);
		_planetController.PlanetData.Rotate(Vector3.Up, adjectedOrbitSpeed * by * _direction.X);
		_planetController.PlanetData.Rotate(Vector3.Back, by * (_keyCameraRotation.X + _mouseCameraRotation.X));

		_planetController.PlanetData.ShaderMaterial.SetShaderParameter("transformations", Utilities.ToProjection(_planetController.PlanetData.GetPlanetTransformMatrix()));

		// Look up and down rotations
		_planetController.CameraController.Rotation = _planetController.CameraController.Rotation with { X = Mathf.Clamp(_planetController.CameraController.Rotation.X + (by * (_keyCameraRotation.Y + _mouseCameraRotation.Y)), 0, Mathf.Pi - 0.0001f) };

		// External Objects that need to rotate to simulate the effect
		WorldEnvironment.Environment.SkyRotation = _planetController.PlanetData.Rotation.Basis.GetEuler();
		RotationGimbal.GlobalRotation = _planetController.PlanetData.Rotation.Basis.GetEuler();

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
		uint[] indices = _computeCullShader.GetUniformData<uint>(ComputeCull.BufferNames.INDICES);
		uint[] primCounts = _computeCullShader.GetUniformData<uint>(ComputeCull.BufferNames.ATOMIC_COUNTER);
		_planetController.CameraController.UIElements.SetCurrentLOD(_computeCullShader.GetUniform<TextureUniform>(ComputeCull.BufferNames.GLOBAL_KEYS_DATA).GetPixel(0, 0).R);

		int all = (int)primCounts[indices[1]];
		int culled = (int)primCounts[indices[1] + 3];

		_planetController.CameraController.UIElements.SetLabelTriangleCount(culled, all);
		// GD.Print(_planetController.PlanetData.Culling);
		if (_planetController.PlanetData.Culling)
		{
			InstanceAllTriangles(culled);
		} else {
			Key[] keys = _computeCullShader.GetUniformData<Key>(ComputeCull.BufferNames.WRITE_FULL_LIST);
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
}
