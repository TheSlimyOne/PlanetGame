using Godot;
using System;

[Tool]
public partial class Camera : Node3D
{


	[Export] public float cameraSpeedX = 1f;
	[Export] public float cameraSpeedY = 1f;
	private float cameraRotationSpeed = 2f;
	private float zoomSpeed = 1f;

	[Export] private Node3D xGimbal;
	[Export] private Node3D yGimbal;
	[Export] private Node3D zGimbal;
	[Export] private Camera3D camera;
	[Export] private Vector2 cameraRange;

	[Export] public bool EmitSignal
	{
		get => true;
		set { if (isReady) EmitSignal("CameraMovement", GlobalPosition); }
	}

	private float horizontalRotation = 0;
	private float verticalRotation = 0;

	private bool isLocked = false;
	private bool isReady = false;

	[Signal] public delegate void CameraMovementEventHandler(Vector3 position);

	public override void _Ready()
	{
		RenderingServer.SetDebugGenerateWireframes(true);
		zGimbal.Position = zGimbal.Position with { Z = cameraRange.Y };
		
		isReady = true;
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
		if (Engine.IsEditorHint()) return;

		Vector3 direction = Vector3.Zero;
		Vector3 rotation = Vector3.Zero;

		float by = (float)delta;
		
		direction.X = Input.GetActionStrength("move_right") - Input.GetActionStrength("move_left");
		direction.Y = Input.GetActionStrength("move_up") - Input.GetActionStrength("move_down");
		direction.Z = Input.GetActionStrength("move_backward") - Input.GetActionStrength("move_forward");
		rotation.Y = Input.GetActionStrength("rotate_right") - Input.GetActionStrength("rotate_left");

		yGimbal.RotateObjectLocal(Vector3.Up, by * direction.X * cameraSpeedX);
		yGimbal.RotateObjectLocal(Vector3.Right, by * direction.Z * cameraSpeedY);

		yGimbal.RotateObjectLocal(Vector3.Forward, by * rotation.Y * cameraRotationSpeed);

		zGimbal.Position = zGimbal.Position with { Z = zGimbal.Position.Z + direction.Y * zoomSpeed };
		if (zGimbal.Position.Z < cameraRange.X)
			zGimbal.Position = zGimbal.Position with { Z = cameraRange.X };

		else if (zGimbal.Position.Z > cameraRange.Y)
			zGimbal.Position = zGimbal.Position with { Z = cameraRange.Y };

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
				viewport.DebugDraw = Viewport.DebugDrawEnum.NormalBuffer;
			else if (viewport.DebugDraw == Viewport.DebugDrawEnum.NormalBuffer)
				viewport.DebugDraw = Viewport.DebugDrawEnum.Disabled;
			else if (viewport.DebugDraw == Viewport.DebugDrawEnum.Disabled)
				viewport.DebugDraw = Viewport.DebugDrawEnum.Wireframe;

		}
		if (Input.IsActionJustPressed("speed_up_cam"))
		{
			if (zoomSpeed <= 10)
				zoomSpeed += 0.05f;
		}
		if (Input.IsActionJustPressed("speed_down_cam"))
		{
			if (zoomSpeed > 0.001f)
				zoomSpeed -= 0.05f;
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
