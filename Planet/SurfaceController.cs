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

	[Export] public Node3D LightGimbal;

	[ExportGroup("Movement Settings")]
	[Export] private Vector2 MouseSensitivity = new Vector2(0.09f, 0.09f);

	[ExportSubgroup("Orbit Settings")]
	[Export] public float BaseOrbitSpeed;
	[Export] public float OrbitSpeedModifier;
	[Export] public float weight;

	[ExportGroup("Shaders")]
	[Export(PropertyHint.File)] private string _computeCullShaderPath;
	[Export(PropertyHint.File)] private string _computeCopyShaderPath;
	[Export(PropertyHint.File)] private string _computeGenerateNormalsPath;

	private ComputeCullShader _computeCullShader;
	private ComputeCopyShader _computeCopyShader;
	private ComputeGenerateNormals _computeGenerateNormals;
	private bool _processing;
	private RenderingDevice _rd;

	private Vector2 _mouseCameraRotation;
	private Vector2 _keyCameraRotation;
	private Vector3 _direction = Vector3.Zero;

	public override void _Ready()
	{
		_planetController = (PlanetController)GetParent().GetParent();

		float radius = _planetController.PlanetData.Radius;
		_planetController.PlanetData.Scaled(Vector3.One * radius);

		Vector3 scaleFromPoint = Vector3.Back;
		_planetController.PlanetData.Translation = new Transform3D
		(
			1, 0, 0,
			0, 1, 0,
			0, 0, 1,
			scaleFromPoint.X - radius * scaleFromPoint.X, scaleFromPoint.Y - radius * scaleFromPoint.Y, scaleFromPoint.Z - radius * scaleFromPoint.Z - 1
		);

		ExtraCullMargin = 2 * _planetController.PlanetData.Radius;
		
		InitializeComputeShaders();
	}

    private void InitializeComputeShaders()
    {
        Multimesh = _planetController.PlanetData.GenerateMulitMesh();

		_planetController.PlanetData.SetMaterialParameters();

		_rd = RenderingServer.GetRenderingDevice();
		_computeCullShader = new ComputeCullShader(_computeCullShaderPath, ref _rd);
		_computeCopyShader = new ComputeCopyShader(_computeCopyShaderPath, ref _rd);
		_computeGenerateNormals = new ComputeGenerateNormals(_computeGenerateNormalsPath, ref _rd);

		_computeCullShader.ComputeCopyShader = _computeCopyShader;
		_computeCullShader.PlanetController = _planetController;
		_computeCopyShader.ComputeCullShader = _computeCullShader;
		_computeGenerateNormals.ComputeCullShader = _computeCullShader;
		_computeGenerateNormals.PlanetController = _planetController;

		_computeCullShader.CreateUniforms();
		_computeCopyShader.CreateUniforms();
		_computeGenerateNormals.CreateUniforms();

		_computeGenerateNormals.Ready();
		_rd.Submit();
		_rd.Sync();

		_computeGenerateNormals.SaveNormalMap("Normal.png");


		Texture2Drd displayKeyData = _computeCullShader.GetUniform<TextureUniform>(ComputeCullShader.BufferNames.KEYS).GetTexture2Drd();
		Texture2Drd globalKeyData = _computeCullShader.GetUniform<TextureUniform>(ComputeCullShader.BufferNames.KEYS).GetTexture2Drd();
		// Texture2Drd normals = _computeGenerateNormals.GetUniform<TextureUniform>(ComputeGenerateNormals.BufferNames.NORMAL_MAP).GetTexture2Drd();

		_planetController.PlanetData.ShaderMaterial.SetShaderParameter("key_image", displayKeyData);
		_planetController.PlanetData.ShaderMaterial.SetShaderParameter("global_key_data", globalKeyData);
		// _planetController.CameraController.GetChild(0).GetChild<TextureRect>(1).Texture = normals;
		
		// _computeGenerateNormals.CleanupGPU();
		_processing = true;
    }

    public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseMotion mouseMotionEvent)
		{
			_mouseCameraRotation = new Vector2(mouseMotionEvent.Relative.X, -mouseMotionEvent.Relative.Y) * MouseSensitivity;
		}
		if (@event.IsActionPressed("step"))
		{
			_processing = !_processing;
		}
		if (@event.IsActionPressed("debug_mode"))
		{
			_planetController.PlanetData.DebugMode = !_planetController.PlanetData.DebugMode;
			_planetController.PlanetData.ShaderMaterial.SetShaderParameter("is_debug", _planetController.PlanetData.DebugMode);
		}
		if (@event.IsActionPressed("cube_mode"))
		{
			_planetController.PlanetData.CubeMode = !_planetController.PlanetData.CubeMode;
			_planetController.PlanetData.ShaderMaterial.SetShaderParameter("is_cube", _planetController.PlanetData.CubeMode);
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
			_processing = false;
			_computeCullShader.CleanupGPU();
			_computeCopyShader.CleanupGPU();
			_computeGenerateNormals.CleanupGPU();
		}

	}

	public override void _PhysicsProcess(double delta)
	{
		bool locked = _processing;
		
		if (locked)
		{
			_computeCullShader.GetUniform<TextureUniform>(ComputeCullShader.BufferNames.KEYS).ClearTexture(Colors.Black);
			_computeCullShader.GetUniform<TextureUniform>(ComputeCullShader.BufferNames.GLOBAL_KEYS_DATA).ClearTexture(Colors.Black);
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

		_planetController.PlanetData.ShaderMaterial.SetShaderParameter("transformations", Utilities.ToProjection(_planetController.PlanetData.Rotation));

		// Look up and down rotations
		_planetController.CameraController.Rotation = _planetController.CameraController.Rotation with { X = Mathf.Clamp(_planetController.CameraController.Rotation.X + (by * (_keyCameraRotation.Y + _mouseCameraRotation.Y)), 0, Mathf.Pi - 0.0001f) };

		// External Objects that need to rotate to simulate the effect
		WorldEnvironment.Environment.SkyRotation = _planetController.PlanetData.Rotation.Basis.GetEuler();
		LightGimbal.GlobalRotation = _planetController.PlanetData.Rotation.Basis.GetEuler();

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
		uint[] indices = _computeCullShader.GetUniformData<uint>(ComputeCullShader.BufferNames.INDICES);
		uint[] primCounts = _computeCullShader.GetUniformData<uint>(ComputeCullShader.BufferNames.ATOMIC_COUNTER);
		_planetController.CameraController.UIElements.SetCurrentLOD(_computeCullShader.GetUniform<TextureUniform>(ComputeCullShader.BufferNames.GLOBAL_KEYS_DATA).GetPixel(0, 0).R);

		int all = (int)primCounts[indices[1]];
		int culled = (int)primCounts[indices[1] + 16];

		_planetController.CameraController.UIElements.SetLabelTriangleCount(culled, all);
		// Key[] keys = _computeCullShader.GetUniformData<Key>(ComputeCullShader.BufferNames.WRITE_FULL_LIST);

		// _processing = false;
		InstanceAllTriangles(culled);
		// InstanceAllTriangles(keys, all);
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
