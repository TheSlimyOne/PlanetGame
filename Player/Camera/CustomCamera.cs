using Godot;
using PlanetGame.Rendering.Surface;
using PlanetGame.Rendering.VirtualTexturing;
using PlanetGame.Util;
using PlanetGame.Util.DebugUIComponents;

public partial class CustomCamera : Camera3D
{
	private static TessellationData TessellationData => SaveManager.CurrentWorldSave.TessellationData;
	private static VirtualTextureData VirtualTextureData => SaveManager.CurrentWorldSave.VirtualTextureData;

	[Export] public float BaseZoomSpeed { get; set; }
	[Export] public float MaxDistance { get; set; }
	[Export] public float MinDistance { get; set; }

	[Export] public bool Locked { get; private set; }
	[Export] public float Sensitivity { get; private set; }

	private Vector3 _lookRotation;

	[Export] public Vector2 RotationRangeX { get; set; }
	[Export] public Vector2 RotationRangeY { get; set; }
	[Export] public Vector2 RotationRangeZ { get; set; }

	[Export] public bool LockXRotation { get; set; }
	[Export] public bool LockYRotation { get; set; }
	[Export] public bool LockZRotation { get; set; }

	[Export] public float RotationEasing { get; set; }
	private Vector2 _keyRotation;

	[Export] public float DistanceFromTarget { get; set; }
	[Export] public Node3D Target { get; set; }

	RemoteTransform3D FollowRemote;
	public bool HasMoved { get; private set; }

	public override void _Ready()
	{
		DebugMenuController.Instance.AddSection("Camera", 0, false, null, 250);
		DebugMenuController.Instance.AddLabel("Distance", "Camera", () => $"{DistanceFromTarget}");
		DebugMenuController.Instance.AddLabel("Camera Mode", "Camera", () => $"{GetViewport().DebugDraw}");
		DebugMenuController.Instance.AddSlider("Culling Margin", "Camera",
		[
			new SliderComponent.SliderBinding<float>(
				() => TessellationData.CullingMargin.X,
				value =>
				{
					Vector4 margin = TessellationData.CullingMargin;
					margin.X = value;
					TessellationData.CullingMargin = margin;
					SetFrustumMeshInstance(TessellationData.CullingMargin, TessellationData.CullingDepth);
				},
				0,
				1,
				0.01f
			),
			new SliderComponent.SliderBinding<float>(
				() => TessellationData.CullingMargin.Y,
				value =>
				{
					Vector4 margin = TessellationData.CullingMargin;
					margin.Y = value;
					TessellationData.CullingMargin = margin;
					SetFrustumMeshInstance(TessellationData.CullingMargin, TessellationData.CullingDepth);
				},
				0,
				1,
				0.01f
			),
			new SliderComponent.SliderBinding<float>(
				() => TessellationData.CullingMargin.Z,
				value =>
				{
					Vector4 margin = TessellationData.CullingMargin;
					margin.Z = value;
					TessellationData.CullingMargin = margin;
					SetFrustumMeshInstance(TessellationData.CullingMargin, TessellationData.CullingDepth);
				},
				0,
				2,
				0.01f
			),
			new SliderComponent.SliderBinding<float>(
				() => TessellationData.CullingMargin.W,
				value =>
				{
					Vector4 margin = TessellationData.CullingMargin;
					margin.W = value;
					TessellationData.CullingMargin = margin;
					SetFrustumMeshInstance(TessellationData.CullingMargin, TessellationData.CullingDepth);
				},
				0.0f,
				100.0f,
				1.0f
			),
			new SliderComponent.SliderBinding<float>(
				() => TessellationData.CullingDepth,
				value =>
				{
					float cullingDepth = TessellationData.CullingDepth;
					cullingDepth = value;
					TessellationData.CullingDepth = cullingDepth;
					SetFrustumMeshInstance(TessellationData.CullingMargin, TessellationData.CullingDepth);
				},
				-10.0f,
				10.0f,
				0.1f
			),
		]);


	}

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

		if (!LockXRotation)
			_lookRotation.X += x;

		if (!LockYRotation)
			_lookRotation.Y += y;

		if (!LockZRotation)
			_lookRotation.Z += z;

		_lookRotation.X = RotationRangeX == Vector2.Zero ? Utilities.NormalizeAngleDegrees(_lookRotation.X) : Mathf.Clamp(_lookRotation.X, RotationRangeX[0], RotationRangeX[1]);
		_lookRotation.Y = RotationRangeY == Vector2.Zero ? Utilities.NormalizeAngleDegrees(_lookRotation.Y) : Mathf.Clamp(_lookRotation.Y, RotationRangeY[0], RotationRangeY[1]);
		_lookRotation.Z = RotationRangeZ == Vector2.Zero ? Utilities.NormalizeAngleDegrees(_lookRotation.Z) : Mathf.Clamp(_lookRotation.Z, RotationRangeZ[0], RotationRangeZ[1]);
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
		return GetViewProjectionMatrix(Vector3.Zero);
	}
	public Projection GetViewProjectionMatrix(Vector3 offset)
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

	public Projection GetCullingViewProjectionMatrix(Vector4 cullingMargin, float cullingDepth)
	{
		float aspect = GetViewport().GetVisibleRect().Size.X / GetViewport().GetVisibleRect().Size.Y;

		float near = Near + cullingMargin.Z;
		float far = Far + cullingMargin.W;

		Projection projectionMatrix = Godot.Projection.CreatePerspective(
			Fov,
			aspect,
			near,
			far,
			false
		);

		projectionMatrix.X.X /= 1.0f + cullingMargin.X;
		projectionMatrix.Y.Y /= 1.0f + cullingMargin.Y;

		Transform3D cameraTransform = GlobalTransform;
		cameraTransform.Origin += cameraTransform.Basis.Z * cullingDepth;

		Transform3D viewMatrix = cameraTransform.AffineInverse();

		Projection viewMatrix4 = new(
			new Vector4(viewMatrix[0].X, viewMatrix[0].Y, viewMatrix[0].Z, 0.0f),
			new Vector4(viewMatrix[1].X, viewMatrix[1].Y, viewMatrix[1].Z, 0.0f),
			new Vector4(viewMatrix[2].X, viewMatrix[2].Y, viewMatrix[2].Z, 0.0f),
			new Vector4(viewMatrix[3].X, viewMatrix[3].Y, viewMatrix[3].Z, 1.0f)
		);

		return projectionMatrix * viewMatrix4;
	}

	private MeshInstance3D _cullingMarginFrustumInstance;
	private MeshInstance3D _frustumInstance;

	public void SetFrustumMeshInstance(Vector4 cullingMargin, float cullingDepth)
	{
		ArrayMesh cullingMarginFrustum = CreateFrustumMesh(cullingMargin, cullingDepth, Colors.Red);

		if (_cullingMarginFrustumInstance == null)
		{
			_cullingMarginFrustumInstance = new()
			{
				Name = "Frustum Culling Margin",
				Layers = 4,
				Position = Vector3.Zero
			};

			AddChild(_cullingMarginFrustumInstance);
		}

		_cullingMarginFrustumInstance.Mesh = cullingMarginFrustum;

		ArrayMesh frustum = CreateFrustumMesh(Vector4.Zero, 0, Colors.Blue);

		if (_frustumInstance == null)
		{
			_frustumInstance = new()
			{
				Name = "Frustum",
				Layers = 4,
				Position = Vector3.Zero
			};

			AddChild(_frustumInstance);
		}

		_frustumInstance.Mesh = frustum;
	}

	public ArrayMesh CreateFrustumMesh(Vector4 cullingMargin, float cullingDepth, Color color)
	{
		static void AddTriangle(SurfaceTool surfaceTool, Vector3 a, Vector3 b, Vector3 c)
		{
			surfaceTool.AddVertex(a);
			surfaceTool.AddVertex(b);
			surfaceTool.AddVertex(c);
		}

		Vector3 forward = Vector3.Forward;
		Vector3 right = Vector3.Right;
		Vector3 up = Vector3.Up;

		float fov = GetCameraFov(true);
		float aspect = GetViewport().GetVisibleRect().Size.X / GetViewport().GetVisibleRect().Size.Y;

		float near = Near + cullingMargin.Z;
		float far = Far + cullingMargin.W;

		Vector3 origin = -forward * cullingDepth;

		float baseHalfNearHeight = Mathf.Tan(fov / 2.0f) * near;
		float baseHalfNearWidth = baseHalfNearHeight * aspect;

		float baseHalfFarHeight = Mathf.Tan(fov / 2.0f) * far;
		float baseHalfFarWidth = baseHalfFarHeight * aspect;

		float halfNearHeight = baseHalfNearHeight * (1.0f + cullingMargin.Y);
		float halfNearWidth = baseHalfNearWidth * (1.0f + cullingMargin.X);

		float halfFarHeight = baseHalfFarHeight * (1.0f + cullingMargin.Y);
		float halfFarWidth = baseHalfFarWidth * (1.0f + cullingMargin.X);

		Vector3 centerNear = origin + forward * near;
		Vector3 centerFar = origin + forward * far;

		Vector3 ntl = centerNear + up * halfNearHeight - right * halfNearWidth;
		Vector3 ntr = centerNear + up * halfNearHeight + right * halfNearWidth;
		Vector3 nbl = centerNear - up * halfNearHeight - right * halfNearWidth;
		Vector3 nbr = centerNear - up * halfNearHeight + right * halfNearWidth;

		Vector3 ftl = centerFar + up * halfFarHeight - right * halfFarWidth;
		Vector3 ftr = centerFar + up * halfFarHeight + right * halfFarWidth;
		Vector3 fbl = centerFar - up * halfFarHeight - right * halfFarWidth;
		Vector3 fbr = centerFar - up * halfFarHeight + right * halfFarWidth;

		SurfaceTool solidSurface = new();
		solidSurface.Begin(Mesh.PrimitiveType.Triangles);

		AddTriangle(solidSurface, ntl, ntr, nbr);
		AddTriangle(solidSurface, ntl, nbr, nbl);

		AddTriangle(solidSurface, ftl, fbl, fbr);
		AddTriangle(solidSurface, ftl, fbr, ftr);

		AddTriangle(solidSurface, ntl, ftl, ftr);
		AddTriangle(solidSurface, ntl, ftr, ntr);

		AddTriangle(solidSurface, nbl, nbr, fbr);
		AddTriangle(solidSurface, nbl, fbr, fbl);

		AddTriangle(solidSurface, ntl, nbl, fbl);
		AddTriangle(solidSurface, ntl, fbl, ftl);

		AddTriangle(solidSurface, ntr, ftr, fbr);
		AddTriangle(solidSurface, ntr, fbr, nbr);

		solidSurface.GenerateNormals();

		ArrayMesh frustumMesh = solidSurface.Commit();

		Vector3[] wireVertices =
		[
			ntl, ntr,
			ntr, nbr,
			nbr, nbl,
			nbl, ntl,

			ftl, ftr,
			ftr, fbr,
			fbr, fbl,
			fbl, ftl,

			ntl, ftl,
			ntr, ftr,
			nbl, fbl,
			nbr, fbr
		];

		Godot.Collections.Array wireArrays = [];
		wireArrays.Resize((int)Mesh.ArrayType.Max);
		wireArrays[(int)Mesh.ArrayType.Vertex] = wireVertices;

		frustumMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Lines, wireArrays);

		Color solidColor = color;
		solidColor.A = 0.2f;

		frustumMesh.SurfaceSetMaterial(0, new StandardMaterial3D
		{
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = solidColor,
			Roughness = 1.0f
		});

		frustumMesh.SurfaceSetMaterial(1, new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = Colors.White
		});

		return frustumMesh;
	}
}
