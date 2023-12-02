using Godot;
using System;

public partial class CameraComponent : Node3D
{
	[Export]
	public float mouseSensitivity = 0.05f;

	[Export]
	public PlanetController focus;

	[Export]
	public Vector2 zoomLevels;



	public float cameraSpeedX = 1f;
	public float cameraSpeedY = 1f;
	public float cameraRotationSpeed = 2f;
	public float zoomSpeed = 1f;

	public Node3D xGimbal;
	public Node3D yGimbal;
	public Node3D zGimbal;
	public Camera3D camera;

	public float horizontalRotation = 0;
	public float verticalRotation = 0;

	private bool isLocked = false;

	[Signal]
	public delegate void CameraMovementEventHandler();


	public Vector3 GetDirection()
	{
		return xGimbal.RotationDegrees + yGimbal.RotationDegrees;
	}

	public override void _Ready()
	{
		xGimbal = GetChild<Node3D>(0);
		yGimbal = xGimbal.GetChild<Node3D>(0);
		zGimbal = yGimbal.GetChild<Node3D>(0);
		camera = zGimbal.GetChild<Camera3D>(0);
		RenderingServer.SetDebugGenerateWireframes(true);

		GlobalPosition = focus.GlobalPosition;
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
