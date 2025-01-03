using Godot;
using System;

public partial class CustomCamera : Camera3D
{
	[Export] public float DistanceFromTarget { get; set; }
	[Export] public float BaseZoomSpeed { get; set; }
	[Export] public float MaxDistance { get; set; }
	[Export] public float MinDistance { get; set; }

	[Export] public bool Locked { get; private set; }
	[Export] public float Sensitivity { get; private set; }

	private Vector3 _lookRotation;

	[Export] public Vector2 RotationRangeX { get; set; }
	[Export] public Vector2 RotationRangeY { get; set; }
	[Export] public Vector2 RotationRangeZ { get; set; }

	public override void _Ready()
	{
		// ProcessPriority = 5;

	}

	public override void _PhysicsProcess(double delta)
	{
		// _cameraController.Rotation = _cameraController.Rotation with { X = Mathf.Clamp(_cameraController.Rotation.X + (by * (_keyCameraRotation.Y + _mouseCameraRotation.Y)), 0, Mathf.Pi - 0.0001f) };

		if (Current)
		{
			Quaternion pitch = new Quaternion(Vector3.Right, Mathf.DegToRad(_lookRotation.X));
			Quaternion yaw = new Quaternion(Vector3.Up, Mathf.DegToRad(_lookRotation.Y));
			Quaternion roll = new Quaternion(Vector3.Forward, Mathf.DegToRad(_lookRotation.Z));

			Quaternion combinedRotation = yaw * roll * pitch;
			Basis basis = new Basis(combinedRotation);

			Transform3D transform = GlobalTransform;
			transform.Basis = basis;
			GlobalTransform = transform;


		}


		// LookRotation = Vector3.Zero;
	}

	public void Follow(CustomCamera otherCamera)
	{
		RemoteTransform3D remote = new();
		otherCamera.AddChild(remote);
		remote.GlobalPosition = GlobalPosition;
		remote.RemotePath = GetPath();
	}

	public override void _Input(InputEvent @event)
	{
		if (Input.IsActionJustReleased("cam_exit") && Current)
		{
			if (Locked)
				UnlockMouse();
			else
			{
				LockMouse();
			}
		}

		if (Locked && @event is InputEventMouseMotion mouseMotionEvent)
		{
			SetLookRotation(x: -mouseMotionEvent.Relative.Y * Sensitivity, z: mouseMotionEvent.Relative.X * Sensitivity);
		}
	}


	public void UpdateLookRotation(Vector3 rotation) => SetLookRotation(rotation.X, rotation.Y, rotation.Z);

	public void SetLookRotation(float x = 0, float y = 0, float z = 0)
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



}
