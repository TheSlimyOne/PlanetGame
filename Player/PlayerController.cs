using Godot;
[Tool]
public partial class PlayerController : Node3D
{
	[Export] public Camera3D Camera { get; set; }

	[ExportGroup("Gimbals")] 
	[Export] private Node3D orbitGimbal;
	[Export] private Node3D rotationGimbal;
	
	[ExportGroup("Control Settings")] 
	[Export] private Vector2 MouseSensitivity = new Vector2(0.09f, 0.09f);
	[Export] private Vector2 CameraRange = new Vector2(1, 100);
	[Export] private Vector3 CameraPosition = Vector3.Zero;
	[Export] private Vector2 CameraZoomSpeed = new Vector2(0.01f, 1000);
	[Export] private float ZoomToRotationRatio;

	[ExportGroup("Focus Info")]	
    [Export] public Node3D Focus { get; set; }
	[Export] private float FocusRadius;


    Vector2 mouseCameraRotation = Vector2.Zero;
	private float horizontalRotation = 0;
	private float verticalRotation = 0;

	private bool isLocked = false;
	private bool isReady = false;


	[Signal] public delegate void CameraMovementEventHandler(Camera3D camera);

	public override void _Ready()
	{
		if (Engine.IsEditorHint()) return;
		RenderingServer.SetDebugGenerateWireframes(true);
		rotationGimbal.Position = rotationGimbal.Position with { Y = CameraRange.Y };
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
		Vector2 keyCameraRotation = Vector2.Zero;

		float by = (float)delta;

		direction.X = Input.GetActionStrength("move_right") - Input.GetActionStrength("move_left");
		direction.Y = Input.GetActionStrength("move_up") - Input.GetActionStrength("move_down");
		direction.Z = Input.GetActionStrength("move_backward") - Input.GetActionStrength("move_forward");
		
		keyCameraRotation.X = Input.GetActionStrength("rotate_left") - Input.GetActionStrength("rotate_right");
		keyCameraRotation.Y = Input.GetActionStrength("rotate_up") - Input.GetActionStrength("rotate_down");

		orbitGimbal.RotateObjectLocal(Vector3.Up, by * (keyCameraRotation.X + mouseCameraRotation.X));
		Camera.RotateObjectLocal(Vector3.Right, by * (keyCameraRotation.Y + mouseCameraRotation.Y));
		Camera.RotationDegrees = Camera.RotationDegrees with { X = Mathf.Clamp(Camera.RotationDegrees.X, -90, 90) };
		
		float targetDistance = Camera.GlobalPosition.DistanceTo(Focus.GlobalPosition) - FocusRadius;
		float moveSpeed = Mathf.Lerp(CameraZoomSpeed.X, CameraZoomSpeed.Y, targetDistance / CameraRange.Y);

		orbitGimbal.RotateObjectLocal(Vector3.Right, by * direction.Z * moveSpeed * ZoomToRotationRatio);
		orbitGimbal.RotateObjectLocal(Vector3.Forward, by * direction.X * moveSpeed * ZoomToRotationRatio);

		rotationGimbal.Position = rotationGimbal.Position with { Y = rotationGimbal.Position.Y + direction.Y * moveSpeed };
		rotationGimbal.Position = rotationGimbal.Position with { Y = Mathf.Clamp(rotationGimbal.Position.Y, CameraRange.X, CameraRange.Y) };

		// GD.Print(Camera.GlobalPosition, Camera.GlobalPosition.Length());

		if (direction != Vector3.Zero || keyCameraRotation != Vector2.Zero || mouseCameraRotation != Vector2.Zero)
		{
			EmitSignal("CameraMovement", Camera);
			// GD.Print(moveSpeed);
		}

		mouseCameraRotation = Vector2.Zero;
		ResizeGimbalMesh();
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
				mouseCameraRotation = new Vector2(-mouseMotionEvent.Relative.X * MouseSensitivity.X, -mouseMotionEvent.Relative.Y * MouseSensitivity.Y);
				return;
			}

			

		}

		if (@event is InputEventMouseButton mouseButtonEvent)
		{
			// if (mouseButtonEvent.ButtonIndex == MouseButton.WheelUp)
			// {
			// 	CameraZoomSpeed.Y += 0.05f;
			// }
			// else if (mouseButtonEvent.ButtonIndex == MouseButton.WheelDown)
			// {

			// 	CameraZoomSpeed.X = CameraZoomSpeed.X - 0.01f <= 0 ? 0.01f :  CameraZoomSpeed.X - 0.01f;
			

			// }

		}


		

		if (Input.IsActionJustPressed("change_view"))
		{
			Viewport viewport = GetViewport();

			if (viewport.DebugDraw == Viewport.DebugDrawEnum.Wireframe)
				viewport.DebugDraw = Viewport.DebugDrawEnum.NormalBuffer;
			else if (viewport.DebugDraw == Viewport.DebugDrawEnum.NormalBuffer)
				viewport.DebugDraw = Viewport.DebugDrawEnum.Disabled;
			else if (viewport.DebugDraw == Viewport.DebugDrawEnum.Disabled)
				viewport.DebugDraw = Viewport.DebugDrawEnum.Overdraw;
			else if (viewport.DebugDraw == Viewport.DebugDrawEnum.Overdraw)
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

	float easeOutQuart(float number)
	{
		return 1 - Mathf.Pow(1 - number, 4);
	}
}
