using Godot;
using PlanetGame.Util;

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
	public bool HasMoved { get; private set; }


	public override void _PhysicsProcess(double delta)
	{
		// if (GetTree().Root.GetViewport().GetCamera3D() == this)
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
			HasMoved = !_keyRotation.IsZeroApprox();
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
			HasMoved = true;
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

	public Camera3D GetCurrent()
	{
		return GetTree().Root.GetViewport().GetCamera3D();
	}

	public Vector3[] ProjectPoints(Vector3[] points)
	{
		Vector3[] projectedPoints = new Vector3[points.Length];
		Projection projection = GetViewProjectionMatrix().Inverse();
		for (int i = 0; i < points.Length; i++)
		{
			Vector4 point = VectorUtils.ToVector4(points[i], 1);
			point = projection * point;
			projectedPoints[i] = VectorUtils.ToVector3(point) / point.W;
		}
		return projectedPoints;
	}

	public ArrayMesh CreateFrustumMesh()
	{
		Vector3 origin = GlobalTransform.Origin;
		Vector3 forward = -GlobalTransform.Basis.Z;
		Vector3 right = GlobalTransform.Basis.X;
		Vector3 up = GlobalTransform.Basis.Y;

		float fov = GetCameraFov(true);
		float aspect = GetViewport().GetVisibleRect().Size.X / GetViewport().GetVisibleRect().Size.Y;

		float near = Near;
		float far = Far;

		float hNear = 2.0f * Mathf.Tan(fov / 2.0f) * near;
		float wNear = hNear * aspect;
		float hFar = 2.0f * Mathf.Tan(fov / 2.0f) * far;
		float wFar = hFar * aspect;

		Vector3 centerNear = origin + forward * near;
		Vector3 centerFar = origin + forward * far;

		Vector3 ntl = centerNear + (up * hNear / 2) - (right * wNear / 2);
		Vector3 ntr = centerNear + (up * hNear / 2) + (right * wNear / 2);
		Vector3 nbl = centerNear - (up * hNear / 2) - (right * wNear / 2);
		Vector3 nbr = centerNear - (up * hNear / 2) + (right * wNear / 2);

		Vector3 ftl = centerFar + (up * hFar / 2) - (right * wFar / 2);
		Vector3 ftr = centerFar + (up * hFar / 2) + (right * wFar / 2);
		Vector3 fbl = centerFar - (up * hFar / 2) - (right * wFar / 2);
		Vector3 fbr = centerFar - (up * hFar / 2) + (right * wFar / 2);

		Vector3 centerLineEnd = centerFar;

		Vector3[] vertices = [
			ntl, ntr, ntr, nbr, nbr, nbl, nbl, ntl,

			ftl, ftr, ftr, fbr, fbr, fbl, fbl, ftl,

			ntl, ftl,
			ntr, ftr,
			nbl, fbl,
			nbr, fbr,

			// origin, centerLineEnd
		];

		ArrayMesh frustumMesh = new();
		var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);
		arrays[(int)Mesh.ArrayType.Vertex] = vertices;
		frustumMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Lines, arrays);
		frustumMesh.SurfaceSetMaterial(0, new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded });

		return frustumMesh;
	}

	public MeshInstance3D GetFrustumMeshInstance()
	{
		ArrayMesh mesh = CreateFrustumMesh();
		MeshInstance3D meshInstance = new()
		{
			Mesh = mesh,
			Name = "Frustum",
			Layers = 4
		};
		return meshInstance;
	}
}
