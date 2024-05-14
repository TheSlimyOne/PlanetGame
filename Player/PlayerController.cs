using Godot;
using System;
using System.Linq;

public partial class PlayerController : Node3D
{
	[Export] public float CameraRotationSpeed = 1f;
	[Export] public float CameraZoomSpeed = 1f;
	[Export] public Vector2 MouseSensitivity = new Vector2(0.09f, 0.09f);
	[Export] public DirectionalLight3D LightSource;
	[Export] public float ZoomFactor;
	[Export] private Node3D orbitGimbal;
	[Export] private Node3D rotationGimbal;
	[Export] public Camera3D Camera;
	[Export] public float MinViewDistance;
	[Export] public float MaxViewDistance;
	[Export] public Vector2 CameraRange = new Vector2(1, 100);
	[Export] public float ViewDistanceFactor = 1;

	[Export] public Vector3 LightSourceOffset;

	[Export] public Node3D Focus;
	Vector2 mouseRotation = Vector2.Zero;

	private float horizontalRotation = 0;
	private float verticalRotation = 0;

	private bool isLocked = false;
	private bool isReady = false;

	[Signal] public delegate void CameraMovementEventHandler(Camera3D camera);

	public override void _Ready()
	{
		RenderingServer.SetDebugGenerateWireframes(true);
		rotationGimbal.Position = rotationGimbal.Position with { Y = CameraRange.Y };
		Plane[] frustum = Camera.GetFrustum().ToArray();
		// camera.SetFrustum()

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

		Camera.RotateObjectLocal(Vector3.Right, by * (keyRotation.Y + mouseRotation.Y));
		Camera.RotationDegrees = Camera.RotationDegrees with { X = Mathf.Clamp(Camera.RotationDegrees.X, -90, 90) };

		orbitGimbal.RotateObjectLocal(Vector3.Right, by * direction.Z * CameraRotationSpeed);
		orbitGimbal.RotateObjectLocal(Vector3.Forward, by * direction.X * CameraRotationSpeed);

		rotationGimbal.Position = rotationGimbal.Position with { Y = rotationGimbal.Position.Y + direction.Y * CameraZoomSpeed };
		rotationGimbal.Position = rotationGimbal.Position with { Y = Mathf.Clamp(rotationGimbal.Position.Y, CameraRange.X, CameraRange.Y) };

		if (direction != Vector3.Zero || keyRotation != Vector2.Zero || mouseRotation != Vector2.Zero)
		{
			EmitSignal("CameraMovement", Camera);

			Vector3 normal = (Focus.GlobalPosition - Camera.GlobalPosition).Normalized();
			if (Focus != null && LightSource != null)
			{
				LightSource.LookAt(Focus.Transform.Origin);
				
			}
		}
		mouseRotation = Vector2.Zero;
	}

	private void ResizeGimbalMesh()
	{
		MeshInstance3D xMesh = orbitGimbal.GetChild<MeshInstance3D>(0);
		if (((TorusMesh)xMesh.Mesh).InnerRadius - 0.5 > 0)
		{
			((TorusMesh)xMesh.Mesh).InnerRadius = rotationGimbal.Position.Length() - 0.5f;
			((TorusMesh)xMesh.Mesh).OuterRadius = rotationGimbal.Position.Length();
		}

		MeshInstance3D yMesh = orbitGimbal.GetChild<MeshInstance3D>(1);
		if (((TorusMesh)yMesh.Mesh).InnerRadius - 0.5 > 0)
		{
			((TorusMesh)yMesh.Mesh).InnerRadius = rotationGimbal.Position.Length() - 0.5f;
			((TorusMesh)yMesh.Mesh).OuterRadius = rotationGimbal.Position.Length();
		}

		MeshInstance3D zMesh = orbitGimbal.GetChild<MeshInstance3D>(2);
		if (((TorusMesh)zMesh.Mesh).InnerRadius - 0.5 > 0)
		{
			((TorusMesh)zMesh.Mesh).InnerRadius = rotationGimbal.Position.Length() - 0.5f;
			((TorusMesh)zMesh.Mesh).OuterRadius = rotationGimbal.Position.Length();
		}
	}


	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseMotion mouseMotionEvent)
		{
			if (isLocked)
			{
				mouseRotation = new Vector2(-mouseMotionEvent.Relative.X * MouseSensitivity.X, -mouseMotionEvent.Relative.Y * MouseSensitivity.Y);
			}


		}

		if (@event is InputEventMouseButton mouseButtonEvent)
		{
			if (mouseButtonEvent.ButtonIndex == MouseButton.WheelUp)
			{
				CameraRotationSpeed += ZoomFactor;
				CameraZoomSpeed += ZoomFactor;
			}
			else if (mouseButtonEvent.ButtonIndex == MouseButton.WheelDown)
			{
				CameraRotationSpeed -= ZoomFactor;
				CameraZoomSpeed -= ZoomFactor;

			}

			CameraRotationSpeed = Mathf.Clamp(CameraRotationSpeed, 0.01f, 5);
			CameraZoomSpeed = Mathf.Clamp(CameraZoomSpeed, 0.01f, 5);
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
		
		}
		if (Input.IsActionJustReleased("cam_exit"))
		{
			if (isLocked)
				UnlockMouse();
			else
				LockMouse();

		}

	}

	float easeOutQuart(float number)
	{
		return 1 - Mathf.Pow(1 - number, 4);
	}
}
