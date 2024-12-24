using System;
using System.Linq;
using Godot;
using Godot.Collections;
using Planet;


public partial class CameraController : Camera3D
{
	[Export] public UIElements UIElements;
	[Export] public WorldEnvironment WorldEnvironment;
	[Export] public float DistanceFromSurface { get; set; }
	[Export] public Camera3D InnerCamera { get; set; }
	private PlanetController _planetController;

	// [Export] public MeshInstance3D InnerCameraFrustum;
	[Export] public MeshInstance3D Frustum;
	private MultiMeshInstance3D _planetLine = new();
	[Export] private MultiMeshInstance3D _debugPlot = new();

	[Export] public float BaseZoomSpeed;
	[Export] public float RayLength = 5000;
	[Export] public float pointRadius = 0.1f;
	Image heightMap;


	[Export] public bool Locked { get; private set; }
	private const string POSITION = "position";

	public override void _Ready()
	{
		_planetController = (PlanetController)GetParent();
		_planetController.SurfaceAttachment.CallDeferred("add_child", _planetLine);
		// _planetController.SurfaceAttachment.CallDeferred("add_child", _debugPlot);
		Frustum.ExtraCullMargin = 2 * _planetController.PlanetData.Radius;
		// InnerCameraFrustum.ExtraCullMargin = 2 * _planetController.PlanetData.Radius;
		_debugPlot.ExtraCullMargin = 2 * _planetController.PlanetData.Radius;

		heightMap = _planetController.PlanetData.HeightMap.GetImage();

		_debugPlot.Multimesh = new MultiMesh() { UseColors = true, Mesh = new SphereMesh() { RadialSegments = 8, Rings = 4, Material = new StandardMaterial3D() { VertexColorUseAsAlbedo = true }, Radius = pointRadius, Height = 2 * pointRadius }, TransformFormat = MultiMesh.TransformFormatEnum.Transform3D };
	}

	public void CalculateRayToPlanet(Vector3 from, Vector3 to)
	{
		PhysicsDirectSpaceState3D spaceState = GetWorld3D().DirectSpaceState;
		Rid inner = _planetController.SurfaceController.InnerCollision.GetRid();
		Rid outer = _planetController.SurfaceController.OuterCollision.GetRid();
		Rid cube = _planetController.SurfaceController.CubicalCollision.GetRid();

		Dictionary[] intersections = new Dictionary[4];

		intersections[0] = spaceState.IntersectRay(new PhysicsRayQueryParameters3D()
		{
			To = to,
			From = from,
			Exclude = new Array<Rid>() { inner, cube }
		});
		intersections[1] = spaceState.IntersectRay(new PhysicsRayQueryParameters3D()
		{
			To = to,
			From = from,
			Exclude = new Array<Rid>() { outer, cube }
		});
		intersections[2] = spaceState.IntersectRay(new PhysicsRayQueryParameters3D()
		{
			To = from,
			From = to,
			Exclude = new Array<Rid>() { inner, cube }
		});

		int identity = (intersections[0].ContainsKey(POSITION) ? 1 << 2 : 0) | (intersections[1].ContainsKey(POSITION) ? 1 << 1 : 0) | (intersections[2].ContainsKey(POSITION) ? 1 : 0);

		Vector3 start;
		Vector3 end;

		switch (identity)
		{
			case 7:
				start = (Vector3)intersections[0][POSITION];
				end = (Vector3)intersections[1][POSITION];
				break;
			case 5:
				start = (Vector3)intersections[0][POSITION];
				end = (Vector3)intersections[2][POSITION];
				break;
			case 3:
				start = from;
				end = (Vector3)intersections[1][POSITION];
				break;
			case 1:
				start = from;
				end = (Vector3)intersections[2][POSITION];
				break;
			default:
				_debugPlot.Multimesh.InstanceCount = 0;
				return;
		}

		start = _planetController.PlanetData.GetPlanetTRMatrix().Inverse() * start;
		end = _planetController.PlanetData.GetPlanetTRMatrix().Inverse() * end;

		int amount = 10 * Mathf.RoundToInt(start.DistanceTo(end));
		_debugPlot.Multimesh.InstanceCount = 2 * amount;

		Vector2I size = heightMap.GetSize();
		for (int i = 0; i < amount; i++)
		{
			Vector3 localPosition = start.Lerp(end, i / (amount - 1f));

			Vector3 directPath = localPosition;
			Vector3 terrainPath = localPosition.Normalized();

			Vector2 uv = VectorUtils.PointOnSphereToUV(terrainPath);
			Vector2I pixel = new(Mathf.RoundToInt(size.X * uv.X), Mathf.RoundToInt(size.Y * uv.Y));
			pixel = pixel.Clamp(Vector2I.Zero, size - Vector2I.One);
			float height = heightMap.GetPixelv(pixel).R * _planetController.PlanetData.HeightScale;

			terrainPath *= _planetController.PlanetData.Radius + height;

			_debugPlot.Multimesh.SetInstanceColor(2 * i + 0, Colors.Red);
			_debugPlot.Multimesh.SetInstanceTransform(2 * i + 0, new(Basis.Identity, directPath));
			_debugPlot.Multimesh.SetInstanceColor(2 * i + 1, Colors.Blue);
			_debugPlot.Multimesh.SetInstanceTransform(2 * i + 1, new(Basis.Identity, terrainPath));

			if (terrainPath.Length() >= directPath.Length())
			{
				return;
			}

		}
		_debugPlot.Multimesh.InstanceCount = 0;
	}
	bool done = false;
	public override void _Input(InputEvent @event)
	{
		if (Input.IsActionJustReleased("cam_exit"))
		{
			if (Locked)
				UnlockMouse();
			else
			{
				LockMouse();
			}
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
				viewport.DebugDraw = Viewport.DebugDrawEnum.Unshaded;
			else if (viewport.DebugDraw == Viewport.DebugDrawEnum.Unshaded)
				viewport.DebugDraw = Viewport.DebugDrawEnum.Wireframe;
		}

		// if (@event is InputEventMouseButton mouseEvent)
		// {
		// 	if (mouseEvent.ButtonIndex == MouseButton.Left && mouseEvent.Pressed && !done)
		// 	{
		// 		// Vector2 mousePosition = GetViewport().GetMousePosition();
		// 		// Vector3 rayOrigin = ProjectRayOrigin(mousePosition);
		// 		// Vector3 rayEnd = rayOrigin + ProjectRayNormal(mousePosition) * RayLength;
		// 		// CalculateRayToPlanet(rayOrigin, rayEnd);
		// 		done = false;


		// 		int currentLod = _planetController.PlanetData.CurrentLod;
		// 		Vector2[] points = GetSquare(Vector2.Zero, 1 / Mathf.Pow(2, currentLod)).ToArray();
		// 		_debugPlot.Multimesh.InstanceCount = 6 * points.Length;

		// 		Color[] colors = new Color[] { Colors.Red, Colors.Blue, Colors.Yellow, Colors.Green, Colors.White, Colors.Black };
		// 		Vector3[] normals = new Vector3[] { Vector3.Up, Vector3.Down, Vector3.Left, Vector3.Right, Vector3.Forward, Vector3.Back };

		// 		for (int i = 0; i < points.Length; i++)
		// 		{
		// 			for (int j = 0; j < 6; j++)
		// 			{
		// 				Transform3D transform = Transform3D.Identity;

		// 				Vector3 position = VectorUtils.UVToPointOnCube(normals[j], points[i]);
		// 				// GD.Print(position);
		// 				position = VectorUtils.PointOnCubeToPointOnSphere(position);
		// 				position *= _planetController.PlanetData.Radius;
		// 				transform.Origin = position;
		// 				_debugPlot.Multimesh.SetInstanceTransform(6 * i + j, transform);
		// 				_debugPlot.Multimesh.SetInstanceColor(6 * i + j, colors[j]);
		// 			}
		// 		}



		// 		// string s = "[";

		// 		// s = s.Remove(s.Length - 2) + "]";			
		// 		// GD.Print(s);
		// 	}
		// }

		if (Input.IsActionJustPressed("switch_to_debug_cam"))
		{
			if (!Current)
			{
				MakeCurrent();
			}
			else
			{
				_planetController.DebugCamera.MakeCurrent();
			}
		}
	}

	public Array<Vector2> GetSquare(Vector2 origin, float smallestScale, float scale = 1)
	{
		if (scale == smallestScale)
			return new Array<Vector2>() {
				origin + new Vector2(scale, 0),
				origin + new Vector2(-scale, 0),
				origin + new Vector2(0, scale),
				origin + new Vector2(0, -scale),
			};

		scale /= 2;

		return
			GetSquare(origin + new Vector2(scale, scale), smallestScale, scale) +
			GetSquare(origin + new Vector2(-scale, scale), smallestScale, scale) +
			GetSquare(origin + new Vector2(-scale, -scale), smallestScale, scale) +
			GetSquare(origin + new Vector2(scale, -scale), smallestScale, scale);
	}

	public override void _PhysicsProcess(double delta)
	{
		UIElements.SetDistance(DistanceFromSurface);
		GlobalPosition = Vector3.Back * DistanceFromSurface;

		Frustum.Mesh = CreateFrustumMesh(ProjectPoints(new Vector3[] {
			new(-1, -1, -1), // near-bottom-left
			new(1, -1, -1),  // near-bottom-right
			new(-1, 1, -1),  // near-top-left
			new(1, 1, -1),   // near-top-right
			new(-1, -1, 1),  // far-bottom-left
			new(1, -1, 1),   // far-bottom-right
			new(-1, 1, 1),   // far-top-left
			new(1, 1, 1)     // far-top-right
		}, InnerCamera.GetCameraProjection()));
	}

	public static Vector3[] ProjectPoints(Vector3[] points, Projection viewProjectionMatrix)
	{
		Vector3[] projectedPoints = new Vector3[points.Length];
		Projection projection = viewProjectionMatrix.Inverse();
		for (int i = 0; i < points.Length; i++)
		{
			Vector4 corner4D = VectorUtils.toVector4(points[i], 1);
			corner4D = projection * corner4D;
			projectedPoints[i] = VectorUtils.toVector3(corner4D) / corner4D.W;
		}
		return projectedPoints;
	}

	private static Mesh CreateFrustumMesh(Vector3[] frustumPoints)
	{
		ArrayMesh frustumMesh = new();
		Vector3[] vertices = new Vector3[]
		{
			frustumPoints[0], frustumPoints[1], frustumPoints[1], frustumPoints[3], frustumPoints[3], frustumPoints[2], frustumPoints[2], frustumPoints[0],
			frustumPoints[4], frustumPoints[5], frustumPoints[5], frustumPoints[7], frustumPoints[7], frustumPoints[6], frustumPoints[6], frustumPoints[4],

			frustumPoints[0], frustumPoints[4],
			frustumPoints[1], frustumPoints[5],
			frustumPoints[2], frustumPoints[6],
			frustumPoints[3], frustumPoints[7]
		};
		var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);
		arrays[(int)Mesh.ArrayType.Vertex] = vertices;
		frustumMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Lines, arrays);

		return frustumMesh;
	}

	public Projection GetViewProjectionMatrix()
	{
		Transform3D viewMatrix = InnerCamera.GlobalTransform.AffineInverse();
		Projection projectionMatrix = InnerCamera.GetCameraProjection();

		Projection viewMatrix4 = new(
			new Vector4(viewMatrix[0].X, viewMatrix[0].Y, viewMatrix[0].Z, 0),
			new Vector4(viewMatrix[1].X, viewMatrix[1].Y, viewMatrix[1].Z, 0),
			new Vector4(viewMatrix[2].X, viewMatrix[2].Y, viewMatrix[2].Z, 0),
			new Vector4(viewMatrix[3].X, viewMatrix[3].Y, viewMatrix[3].Z, 1)
		);

		return projectionMatrix * viewMatrix4;
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

	public float CalculateLODToCam(Vector3 from)
	{
		float distance = from.DistanceTo(_planetController.PlanetData.GetPlanetTRMatrix().Inverse() * GlobalPosition);
		float num = distance * Mathf.Tan(Mathf.DegToRad(Fov) / 2);
		float dom = Mathf.Sqrt2 * _planetController.PlanetData.SubFactor * _planetController.PlanetData.Radius;
		return Mathf.Clamp(-MathF.Log2(num / dom), 0, _planetController.PlanetData.MaximumLOD);
	}

	public float CalculateDistanceToCam(Vector3 from) => from.DistanceTo(GlobalPosition);

}
