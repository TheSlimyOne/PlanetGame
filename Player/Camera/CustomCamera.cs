using Godot;
using System;

public partial class CustomCamera : Camera3D
{
	[Export] public float BaseZoomSpeed { get; set; }
	[Export] public float MaxDistance { get; set; }
	[Export] public float MinDistance { get; set; }

	[Export] public bool Locked { get; private set; }
	[Export] public float Sensitivity { get; private set; }

	private Vector3 _lookRotation;

	[Export] public Vector2 RotationRangeX { get; set; }
	[Export] public Vector2 RotationRangeY { get; set; }
	[Export] public Vector2 RotationRangeZ { get; set; }

	[Export] public float RotationEasing { get; set; }
	private Vector2 _keyRotation;

	[Export] public float DistanceFromTarget { get; set; }
	[Export] public Node3D Target { get; set; }
	
	RemoteTransform3D FollowRemote;

	public override void _PhysicsProcess(double delta)
	{
		if (GetTree().Root.GetViewport().GetCamera3D() == this)
		{
			_keyRotation.X += Input.GetActionStrength("rotate_right") - Input.GetActionStrength("rotate_left");
			_keyRotation.Y += Input.GetActionStrength("rotate_up") - Input.GetActionStrength("rotate_down");

			ApplyRotation(x: _keyRotation.Y, z: _keyRotation.X);

			Quaternion pitch = new(Vector3.Right, Mathf.DegToRad(_lookRotation.X));
			Quaternion yaw = new(Vector3.Up, Mathf.DegToRad(_lookRotation.Y));
			Quaternion roll = new(Vector3.Forward, Mathf.DegToRad(_lookRotation.Z));

			Quaternion combinedRotation = yaw * roll * pitch;
			Basis basis = new(combinedRotation);

			Transform3D transform = GlobalTransform;
			transform.Basis = basis;
			GlobalTransform = transform;


			// float direction = Input.GetActionStrength("move_up") - Input.GetActionStrength("move_down");
			// DistanceFromTarget += direction * (float)delta * 2f;
			_keyRotation = _keyRotation.Lerp(Vector2.Zero, RotationEasing);
		}
		GlobalPosition = GlobalPosition with { Z = DistanceFromTarget };


		// LookRotation = Vector3.Zero;
	}

	public void Follow(CustomCamera otherCamera, bool useGlobalCoordinates = true, bool updatePosition = true, bool updateRotation = true, bool updateScale = true)
	{
        FollowRemote = new()
        {
            RemotePath = GetPath(),
            UseGlobalCoordinates = useGlobalCoordinates,
            UpdatePosition = updatePosition,
            UpdateRotation = updateRotation,
            UpdateScale = updateScale,
        };
        otherCamera.AddChild(FollowRemote);
		FollowRemote.GlobalPosition = GlobalPosition;
	}

	public override void _Input(InputEvent @event)
	{
		if (Input.IsActionJustReleased("cam_exit") && GetCurrent() == this)
		{
			if (Locked)
				UnlockMouse();
			else
				LockMouse();
		}

		if (Locked && @event is InputEventMouseMotion mouseMotionEvent)
		{
			ApplyRotation(x: -mouseMotionEvent.Relative.Y * Sensitivity, z: mouseMotionEvent.Relative.X * Sensitivity);
		}
	}


	public void ApplyRotation(Vector3 rotation) => ApplyRotation(rotation.X, rotation.Y, rotation.Z);

	public void ApplyRotation(float x = 0, float y = 0, float z = 0)
	{
		_lookRotation.X += x;
		_lookRotation.Y += y;
		_lookRotation.Z += z;

		_lookRotation.X = RotationRangeX == Vector2.Zero ? Utilities.LimitRotation(_lookRotation.X) : Mathf.Clamp(_lookRotation.X, RotationRangeX[0], RotationRangeX[1]);
		_lookRotation.Y = RotationRangeY == Vector2.Zero ? Utilities.LimitRotation(_lookRotation.Y) : Mathf.Clamp(_lookRotation.Y, RotationRangeY[0], RotationRangeY[1]);
		_lookRotation.Z = RotationRangeZ == Vector2.Zero ? Utilities.LimitRotation(_lookRotation.Z) : Mathf.Clamp(_lookRotation.Z, RotationRangeZ[0], RotationRangeZ[1]);
	}

	public Vector3 GetLookRotation()
	{
		return _lookRotation;
	}


	public void LockMouse()
	{
		Input.MouseMode = Input.MouseModeEnum.Captured;
		Locked = true;

	}

	public void UnlockMouse()
	{
		Input.MouseMode = Input.MouseModeEnum.Visible;
		Locked = false;
	}

	public float GetCameraFov(bool inRadians) => inRadians ? Mathf.DegToRad(Fov) : Fov;


	public void SetSize(Vector2I size)
	{
		SubViewport viewport = (SubViewport)GetViewport();
		viewport.Size = size;
	}

	public Projection GetViewProjectionMatrix()
	{
		Transform3D viewMatrix = GlobalTransform.AffineInverse();
		Projection projectionMatrix = GetCameraProjection();

		Projection viewMatrix4 = new(
			new Vector4(viewMatrix[0].X, viewMatrix[0].Y, viewMatrix[0].Z, 0),
			new Vector4(viewMatrix[1].X, viewMatrix[1].Y, viewMatrix[1].Z, 0),
			new Vector4(viewMatrix[2].X, viewMatrix[2].Y, viewMatrix[2].Z, 0),
			new Vector4(viewMatrix[3].X, viewMatrix[3].Y, viewMatrix[3].Z, 1)
		);

		return projectionMatrix * viewMatrix4;
	}

	public Camera3D GetCurrent(){
		return GetTree().Root.GetViewport().GetCamera3D();
	}

}
