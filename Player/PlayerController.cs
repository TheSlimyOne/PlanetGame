using Godot;
using System;


[Tool]
public partial class PlayerController : CharacterBody3D
{

	private Vector2 _currentRotation = Vector2.Zero;

	[Export] public float RotationSpeed;
	[Export] public float MovementSpeed;

	Quaternion alignToPlanetGravity;
	public Node3D Focus;
	private Camera3D _camera;
	private Vector2 _cameraRotation = Vector2.Zero;

	private bool isLocked = false;
	private bool isReady = false;

	[Signal] public delegate void PlayerMovementEventHandler(Vector3 position);

	public override void _Ready()
	{	
		_camera = GetNode<Camera3D>("Camera3D");
		alignToPlanetGravity = Quaternion;
	}
	
	float counterForce = 10;
	
	public override void _PhysicsProcess(double delta)
	{
		if (Engine.IsEditorHint()) return;
		if (Focus == null) GD.Print("No focus");
		

		Vector3 direction = Vector3.Zero;

		direction.X = Input.GetActionStrength("move_right") - Input.GetActionStrength("move_left");
		direction.Y = Input.GetActionStrength("move_up") - Input.GetActionStrength("move_down");
		direction.Z = Input.GetActionStrength("move_backward") - Input.GetActionStrength("move_forward");


		Vector3 normal = (GlobalPosition - Focus.GlobalPosition).Normalized();

		
		// Basis = Transform.Rotated(UpDirection.Cross(normal).Normalized(), UpDirection.AngleTo(normal)).Orthonormalized().Basis;
		direction = Basis * direction;
		
		Position = Position + direction;
	

		UpDirection = normal;
		

	}


	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseMotion _event)
		{
			_cameraRotation.X += -_event.Relative.X;
			_cameraRotation.Y += -_event.Relative.Y;

		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{


		if (Input.IsKeyPressed(Key.P))
		{
			Viewport viewport = GetViewport();

			if (viewport.DebugDraw == Viewport.DebugDrawEnum.Wireframe)
				viewport.DebugDraw = Viewport.DebugDrawEnum.NormalBuffer;
			else if (viewport.DebugDraw == Viewport.DebugDrawEnum.NormalBuffer)
				viewport.DebugDraw = Viewport.DebugDrawEnum.Disabled;
			else if (viewport.DebugDraw == Viewport.DebugDrawEnum.Disabled)
				viewport.DebugDraw = Viewport.DebugDrawEnum.Wireframe;

		}

		if (Input.IsActionJustReleased("cam_exit"))
		{
			if (isLocked)
				UnlockMouse();
			else
				LockMouse();

		}
	}

	public void LockMouse()
	{
		Input.MouseMode = Input.MouseModeEnum.Captured;
		isLocked = true;
	}

	public void UnlockMouse()
	{
		Input.MouseMode = Input.MouseModeEnum.Visible;
		isLocked = false;
	}

}
