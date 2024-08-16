using Godot;
using System;

public partial class SurfaceController : Node3D
{
	[Export] public CameraController Camera;
	[ExportGroup("Planet Properties")]
	[Export] public PlanetData PlanetData;
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

	float CalculateSpeed(float distanceFromSurface, float d_min, float d_max, float v_max)
	{
		// Ensure the distance is within the min and max range
		distanceFromSurface = Mathf.Clamp(distanceFromSurface, d_min, d_max);

		// Inverse relationship: speed decreases as distance decreases
		float t = (distanceFromSurface - d_min) / (d_max - d_min);

		// Calculate the speed
		return Mathf.Lerp(0.001f, v_max, t);
	}
	public Transform3D PlanetTransform = Transform3D.Identity;
	public override void _PhysicsProcess(double delta)
	{
		Vector3 direction = Vector3.Zero;
		float by = (float)delta;

		direction.X = Input.GetActionStrength("move_left") - Input.GetActionStrength("move_right");
		direction.Z = Input.GetActionStrength("move_forward") - Input.GetActionStrength("move_backward");

		_keyCameraRotation.X = Input.GetActionStrength("rotate_right") - Input.GetActionStrength("rotate_left");
		_keyCameraRotation.Y = Input.GetActionStrength("rotate_up") - Input.GetActionStrength("rotate_down");

		_mouseCameraRotation = Camera.Locked ? _mouseCameraRotation : Vector2.Zero;


		float adjectedOrbitSpeed = BaseOrbitSpeed * Camera.DistanceFromSurface / OrbitSpeedModifier;
		// float adjectedZoomSpeed = BaseZoomSpeed * DistanceFromSurface / ZoomSpeedModifier;

		PlanetTransform = PlanetTransform
			.Rotated(Vector3.Right, adjectedOrbitSpeed * by * direction.Z)
			.Rotated(Vector3.Up, adjectedOrbitSpeed * by * direction.X)
			.Rotated(Vector3.Back, by * (_keyCameraRotation.X + _mouseCameraRotation.X))
			.Orthonormalized();

		Surface.Material.SetShaderParameter("transformations", GetProjection());


		// Look up and down rotations
		Camera.Rotation = Camera.Rotation with { X = Mathf.Clamp(Camera.Rotation.X + (by * (_keyCameraRotation.Y + _mouseCameraRotation.Y)), 0, Mathf.Pi) };

		// External Objects that need to rotate to simulate the effect
		WorldEnvironment.Environment.SkyRotation = PlanetTransform.Basis.GetEuler();
		LightGimbal.GlobalRotation = PlanetTransform.Basis.GetEuler();
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

	public Projection GetProjection()
	{
		return new(
			new Vector4(PlanetTransform[0].X, PlanetTransform[1].X, PlanetTransform[2].X, PlanetTransform[3].X),
			new Vector4(PlanetTransform[0].Y, PlanetTransform[1].Y, PlanetTransform[2].Y, PlanetTransform[3].Y),
			new Vector4(PlanetTransform[0].Z, PlanetTransform[1].Z, PlanetTransform[2].Z, PlanetTransform[3].Z),
			new Vector4(0, 0, 0, 1)
		);
	}


}
