using Godot;
using System;

public partial class Camera : Node3D
{


	private float cameraSpeedX = 1f;
	private float cameraSpeedY = 1f;
	private float cameraRotationSpeed = 2f;
	private float zoomSpeed = 1f;

	[Export] private Node3D xGimbal;
	[Export] private Node3D yGimbal;
	[Export] private Node3D zGimbal;
	[Export] private Camera3D camera;

	private float horizontalRotation = 0;
	private float verticalRotation = 0;

	private bool isLocked = false;

	[Signal] public delegate void CameraMovementEventHandler(Vector3 position);

	public override void _Ready()
	{
		RenderingServer.SetDebugGenerateWireframes(true);
		
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

	public override void _PhysicsProcess(double delta)
	{

		Vector3 direction = Vector3.Zero;
		Vector3 rotation = Vector3.Zero;
		direction.X = Input.GetActionStrength("move_right") - Input.GetActionStrength("move_left");
		direction.Y = Input.GetActionStrength("move_up") - Input.GetActionStrength("move_down");
		direction.Z = Input.GetActionStrength("move_backward") - Input.GetActionStrength("move_forward");
		rotation.Y = Input.GetActionStrength("rotate_right") - Input.GetActionStrength("rotate_left");

		yGimbal.RotateObjectLocal(Vector3.Up, (float)delta * direction.X * cameraSpeedX);
		yGimbal.RotateObjectLocal(Vector3.Right, (float)delta * direction.Z * cameraSpeedY);

		yGimbal.RotateObjectLocal(Vector3.Forward, (float)delta * rotation.Y * cameraRotationSpeed);

		zGimbal.Position = zGimbal.Position with { Z = zGimbal.Position.Z + direction.Y * zoomSpeed };
		if (zGimbal.Position.Z < 0)
			zGimbal.Position = zGimbal.Position with { Z = 0 };

		if (direction != Vector3.Zero)
		{
			EmitSignal("CameraMovement", camera.GlobalPosition);
		}

	}


	public override void _UnhandledInput(InputEvent @event)
	{
		if (Input.IsKeyPressed(Key.P))
		{
			Viewport viewport = GetViewport();

			if (viewport.DebugDraw == Viewport.DebugDrawEnum.Wireframe)
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
}
