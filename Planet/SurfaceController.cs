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
	[Export] public Node3D PlanetaryGimbal;

	[Export] public Node3D LightGimbal;
	[Export] public Vector3 axis;

	[ExportGroup("Movement Settings")]
	[Export] private Vector2 MouseSensitivity = new Vector2(0.09f, 0.09f);
	[Export] public float DecayConstant;
	[Export] public float BaseSpeed;

	[Export] private float _minDistancePadding;
	[Export] public float DistanceFromSurface { get; private set; }
	// [Export] public float DistanceFromOrigin { get; private set; }

	private Vector2 _mouseCameraRotation;
	private Vector2 _keyCameraRotation;

  

    public override void _PhysicsProcess(double delta)
	{
		Vector3 direction = Vector3.Zero;
		float by = (float)delta;

		direction.X = Input.GetActionStrength("move_left") - Input.GetActionStrength("move_right");
		direction.Y = Input.GetActionStrength("move_up") - Input.GetActionStrength("move_down");
		direction.Z = Input.GetActionStrength("move_forward") - Input.GetActionStrength("move_backward");

		_keyCameraRotation.X = Input.GetActionStrength("rotate_right") - Input.GetActionStrength("rotate_left");
		_keyCameraRotation.Y = Input.GetActionStrength("rotate_up") - Input.GetActionStrength("rotate_down");
	
		_mouseCameraRotation = Camera.Locked ? _mouseCameraRotation : Vector2.Zero;
		
		float speed = BaseSpeed * Mathf.Abs(1 - Mathf.Exp(-DecayConstant * DistanceFromSurface));	
		// speed = desiredSpeed;
		// DistanceFromSurface += direction.Y * by * speed;

		Camera.UIElements.SetDistance(DistanceFromSurface);

		PlanetaryGimbal.RotateX(speed * by * direction.Z);
		PlanetaryGimbal.RotateY(speed * by * direction.X);
		PlanetaryGimbal.RotateZ(by * (_keyCameraRotation.X + _mouseCameraRotation.X));
		
		Surface.Scale = (Surface.Scale - Vector3.One * direction.Y * speed).Clamp(Vector3.One, Vector3.Inf);


		Vector3 planetToCameraVector = Surface.GlobalPosition - Camera.GlobalPosition;
		PlanetaryGimbal.GlobalPosition = planetToCameraVector.Normalized() * (DistanceFromSurface + PlanetData.Radius * Surface.Scale.X);
		Rotation = Rotation with { X = Mathf.Clamp(Rotation.X - (by * (_keyCameraRotation.Y + _mouseCameraRotation.Y)), -Mathf.Pi, 0)};
		
		WorldEnvironment.Environment.SkyRotation = PlanetaryGimbal.GlobalRotation;
		LightGimbal.GlobalRotation = PlanetaryGimbal.GlobalRotation;
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

		if (@event is InputEventMouseButton mouseButtonEvent)
		{
			// if (mouseButtonEvent.ButtonIndex == MouseButton.WheelUp)
			// {
			// 	desiredSpeed = Mathf.Clamp(desiredSpeed + 0.5f, 0.001f, 10);
			// }
			// if (mouseButtonEvent.ButtonIndex == MouseButton.WheelDown)
			// {
			// 	desiredSpeed = Mathf.Clamp(desiredSpeed - 0.5f, 0.001f, 10);
			// }
		}


	}


}
