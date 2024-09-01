using Godot;
using System;

public partial class SurfaceController : Node3D
{
	[ExportGroup("Planet Properties")]
	private PlanetController _planetController;
	[Export] public Surface Surface;
	[Export] public WorldEnvironment WorldEnvironment;
	[Export] public DirectionalLight3D MainLightSource;

	[Export] public Node3D LightGimbal;
	[Export] public Vector3 axis;

	[ExportGroup("Movement Settings")]
	[Export] private Vector2 MouseSensitivity = new Vector2(0.09f, 0.09f);

	[ExportSubgroup("Orbit Settings")]
	[Export] public float BaseOrbitSpeed;
	[Export] public float OrbitSpeedModifier;
	[ExportSubgroup("Zoom Settings")]
	[Export] public float BaseZoomSpeed;
	[Export] public float ZoomSpeedModifier;
	[Export] public float extra;

	[Export] private float _minDistancePadding;

	private Vector2 _mouseCameraRotation;
	private Vector2 _keyCameraRotation;

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

	public override void _PhysicsProcess(double delta)
	{
		Vector3 direction = Vector3.Zero;
		float by = (float)delta;

		direction.X = Input.GetActionStrength("move_left") - Input.GetActionStrength("move_right");
		direction.Z = Input.GetActionStrength("move_forward") - Input.GetActionStrength("move_backward");

		_keyCameraRotation.X = Input.GetActionStrength("rotate_right") - Input.GetActionStrength("rotate_left");
		_keyCameraRotation.Y = Input.GetActionStrength("rotate_up") - Input.GetActionStrength("rotate_down");

		_mouseCameraRotation = _planetController.CameraController.Locked ? _mouseCameraRotation : Vector2.Zero;


		float adjectedOrbitSpeed = BaseOrbitSpeed * _planetController.CameraController.DistanceFromSurface / OrbitSpeedModifier;

		_planetController.PlanetData.Rotate(Vector3.Right, adjectedOrbitSpeed * by * direction.Z);
		_planetController.PlanetData.Rotate(Vector3.Up, adjectedOrbitSpeed * by * direction.X);
		_planetController.PlanetData.Rotate(Vector3.Back, by * (_keyCameraRotation.X + _mouseCameraRotation.X));

		_planetController.PlanetData.ShaderMaterial.SetShaderParameter("transformations", Utilities.ToProjection(_planetController.PlanetData.Rotation));

		// Look up and down rotations
		_planetController.CameraController.Rotation = _planetController.CameraController.Rotation with { X = Mathf.Clamp(_planetController.CameraController.Rotation.X + (by * (_keyCameraRotation.Y + _mouseCameraRotation.Y)), 0, Mathf.Pi - 0.0001f) };

		// External Objects that need to rotate to simulate the effect
		WorldEnvironment.Environment.SkyRotation = _planetController.PlanetData.Rotation.Basis.GetEuler();
		LightGimbal.GlobalRotation = _planetController.PlanetData.Rotation.Basis.GetEuler();
		MainLightSource.RotationDegrees = axis;

		_mouseCameraRotation = Vector2.Zero;
		_keyCameraRotation = Vector2.Zero;
	}


	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseMotion mouseMotionEvent)
		{
			_mouseCameraRotation = new Vector2(mouseMotionEvent.Relative.X, -mouseMotionEvent.Relative.Y) * MouseSensitivity;
		}
	}

	
}
