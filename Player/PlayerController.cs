using Godot;
using System;

public partial class PlayerController : Node3D
{
	[Export] public float CameraRotationSpeed = 1f;
	[Export] public float CameraZoomSpeed = 1f;
	[Export] public Vector2 MouseSensitivity = new Vector2(0.09f, 0.09f);
	[Export] public DirectionalLight3D LightSource;

	[Export] private float zoomChangeRate;
	[Export] private Node3D orbitGimbal;
	[Export] private Node3D rotationGimbal;
	[Export] private Camera3D camera;
	[Export] private Vector2 cameraRange = new Vector2(1, 100);

	public Node3D Focus;
	Vector2 mouseRotation = Vector2.Zero;

	private float horizontalRotation = 0;
	private float verticalRotation = 0;

	private bool isLocked = false;
	private bool isReady = false;

	[Signal] public delegate void CameraMovementEventHandler(Vector3 position);

	public override void _Ready()
	{
		RenderingServer.SetDebugGenerateWireframes(true);
		rotationGimbal.Position = rotationGimbal.Position with { Y = cameraRange.Y };

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
		Vector2 keyRotation = Vector2.Zero;

		float by = (float)delta;

		direction.X = Input.GetActionStrength("move_right") - Input.GetActionStrength("move_left");
		direction.Y = Input.GetActionStrength("move_up") - Input.GetActionStrength("move_down");
		direction.Z = Input.GetActionStrength("move_backward") - Input.GetActionStrength("move_forward");
		keyRotation.X = Input.GetActionStrength("rotate_left") - Input.GetActionStrength("rotate_right");
		keyRotation.Y = Input.GetActionStrength("rotate_up") - Input.GetActionStrength("rotate_down");


		orbitGimbal.RotateObjectLocal(Vector3.Up, by * (keyRotation.X + mouseRotation.X));

		camera.RotateObjectLocal(Vector3.Right, by * (keyRotation.Y + mouseRotation.Y));
		camera.RotationDegrees = camera.RotationDegrees with { X = Mathf.Clamp(camera.RotationDegrees.X, -90, 90) };

		orbitGimbal.RotateObjectLocal(Vector3.Right, by * direction.Z * CameraRotationSpeed);
		orbitGimbal.RotateObjectLocal(Vector3.Forward, by * direction.X * CameraRotationSpeed);

		mouseRotation = Vector2.Zero;
		rotationGimbal.Position = rotationGimbal.Position with { Y = rotationGimbal.Position.Y + direction.Y * CameraZoomSpeed };


		rotationGimbal.Position = rotationGimbal.Position with { Y = Mathf.Clamp(rotationGimbal.Position.Y, cameraRange.X, cameraRange.Y) };
		

		ResizeGimbalMesh();
		if (direction != Vector3.Zero)
		{
			EmitSignal("CameraMovement", camera.GlobalPosition);

			// if (Focus != null)
			// {
			// 	Vector3 normal = (Focus.GlobalPosition - camera.GlobalPosition).Normalized();
			// 	LightSource.LookAt(normal);
			// }
		}

	}

	private void ResizeGimbalMesh()
	{
		MeshInstance3D xMesh = orbitGimbal.GetChild<MeshInstance3D>(0);
		((TorusMesh)xMesh.Mesh).InnerRadius = rotationGimbal.Position.Length() - 0.5f;
		((TorusMesh)xMesh.Mesh).OuterRadius = rotationGimbal.Position.Length();

		MeshInstance3D yMesh = orbitGimbal.GetChild<MeshInstance3D>(1);
		((TorusMesh)yMesh.Mesh).InnerRadius = rotationGimbal.Position.Length() - 0.5f;
		((TorusMesh)yMesh.Mesh).OuterRadius = rotationGimbal.Position.Length();

		MeshInstance3D zMesh = orbitGimbal.GetChild<MeshInstance3D>(2);
		((TorusMesh)zMesh.Mesh).InnerRadius = rotationGimbal.Position.Length() - 0.5f;
		((TorusMesh)zMesh.Mesh).OuterRadius = rotationGimbal.Position.Length();
	}


	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseMotion mouseMotionEvent)
		{
			if (isLocked)
				mouseRotation = new Vector2(-mouseMotionEvent.Relative.X * MouseSensitivity.X, -mouseMotionEvent.Relative.Y * MouseSensitivity.Y);


		}

		if (@event is InputEventMouseButton mouseButtonEvent)
		{
			if (mouseButtonEvent.ButtonIndex == MouseButton.WheelUp)
			{
				CameraRotationSpeed += zoomChangeRate;
				CameraZoomSpeed += zoomChangeRate;
			}
			else if (mouseButtonEvent.ButtonIndex == MouseButton.WheelDown)
			{
				CameraRotationSpeed -= zoomChangeRate;
				CameraZoomSpeed -= zoomChangeRate;
			}

			CameraRotationSpeed = Mathf.Clamp(CameraRotationSpeed, 0.01f, 2);
			CameraZoomSpeed = Mathf.Clamp(CameraRotationSpeed, 0.01f, 2);

		}

		if (Input.IsActionJustPressed("change_view"))
		{
			Viewport viewport = GetViewport();

			if (viewport.DebugDraw == Viewport.DebugDrawEnum.Wireframe)
				viewport.DebugDraw = Viewport.DebugDrawEnum.NormalBuffer;
			else if (viewport.DebugDraw == Viewport.DebugDrawEnum.NormalBuffer)
				viewport.DebugDraw = Viewport.DebugDrawEnum.Disabled;
			else if (viewport.DebugDraw == Viewport.DebugDrawEnum.Disabled)
				viewport.DebugDraw = Viewport.DebugDrawEnum.Wireframe;

		}
		if (Input.IsActionJustPressed("step"))
		{
			PlanetCollision.CreateCollisionChunk(1,Vector3.Zero);
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
