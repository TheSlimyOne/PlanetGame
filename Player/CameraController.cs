using System;
using Godot;
using Godot.Collections;


public partial class CameraController : Camera3D
{
	[Export] public UIElements UIElements;
	[Export] public WorldEnvironment WorldEnvironment;
	[Export] public float DistanceFromSurface { get; set; }
	private PlanetController _planetController;

	private MultiMeshInstance3D _cameraLine = new();
	private MultiMeshInstance3D _planetLine = new();
	private MultiMeshInstance3D _debugPlot = new();

	[Export] public float BaseZoomSpeed;
	[Export] public float RayLength = 5000;
	Image heightMap;


	[Export] public bool Locked { get; private set; }

	public override void _Ready()
	{
		_planetController = (PlanetController)GetParent();

		_planetController.SurfaceAttachment.CallDeferred("add_child", _cameraLine);
		_planetController.SurfaceAttachment.CallDeferred("add_child", _planetLine);
		_planetController.SurfaceAttachment.CallDeferred("add_child", _debugPlot);
		_debugPlot.ExtraCullMargin = 2 * _planetController.PlanetData.Radius;

		heightMap = _planetController.PlanetData.HeightMap.GetImage();

		_debugPlot.Multimesh = new MultiMesh() { UseColors = true, Mesh = new SphereMesh() { RadialSegments = 8, Rings = 4, Material = new StandardMaterial3D() { VertexColorUseAsAlbedo = true }, Radius = 0.25f, Height = 0.5f }, TransformFormat = MultiMesh.TransformFormatEnum.Transform3D };
	}

	public void CalculateRayToPlanet(Vector3 from, Vector3 to)
	{
		PhysicsDirectSpaceState3D spaceState = GetWorld3D().DirectSpaceState;
		Rid inner = _planetController.SurfaceController.InnerCollision.GetRid();
		Rid outer = _planetController.SurfaceController.OuterCollision.GetRid();
		Dictionary[] intersections = new Dictionary[4];

		intersections[0] = spaceState.IntersectRay(new PhysicsRayQueryParameters3D()
		{
			To = to,
			From = from,
			Exclude = new Array<Rid>() { inner }
		});
		intersections[1] = spaceState.IntersectRay(new PhysicsRayQueryParameters3D()
		{
			To = to,
			From = from,
			Exclude = new Array<Rid>() { outer }
		});
		intersections[2] = spaceState.IntersectRay(new PhysicsRayQueryParameters3D()
		{
			To = from,
			From = to,
			Exclude = new Array<Rid>() { inner }
		});

		int identity = (intersections[0].ContainsKey("position") ? 1 << 2 : 0) | (intersections[1].ContainsKey("position") ? 1 << 1 : 0) | (intersections[2].ContainsKey("position") ? 1 : 0);

		Vector3 start;
		Vector3 end;

		switch (identity)
		{
			case 7:
				start = (Vector3)intersections[0]["position"];
				end = (Vector3)intersections[1]["position"];
				break;
			case 5:
				start = (Vector3)intersections[0]["position"];
				end = (Vector3)intersections[2]["position"];
				break;
			case 3:
				start = from;
				end = (Vector3)intersections[1]["position"];
				break;
			case 1:
				start = from;
				end = (Vector3)intersections[2]["position"];
				break;
			default:
				_debugPlot.Multimesh.InstanceCount = 0;
				return;
		}

		int amount = 30 * Mathf.RoundToInt(start.DistanceTo(end));
	
		_debugPlot.Multimesh.InstanceCount = 1;

		for (int i = 0; i < amount; i++)
		{

			Vector3 position = start.Lerp(end, i / (amount - 1f));

			position -= _planetController.PlanetData.Translation.Origin;
			position = _planetController.PlanetData.Rotation.Basis.Transposed().Orthonormalized() * position;

			Vector3 directPath = position;

			Vector3 surfacePath = position.Normalized();
			Vector2I size = heightMap.GetSize();
			Vector2 uv = VectorUtils.PointOnSphereToUV(surfacePath);
			
			float height = heightMap.GetPixelv(new Vector2I(Mathf.RoundToInt(size.X * uv.X), Mathf.RoundToInt(size.Y * uv.Y))).R * _planetController.PlanetData.HeightScale;

			surfacePath = surfacePath * _planetController.PlanetData.Radius + surfacePath * height;

			if (surfacePath.Length() >= directPath.Length())
			{
				Transform3D directTransform = new(Basis.Identity, directPath);
				_debugPlot.Multimesh.SetInstanceColor(0, Colors.Red);
				_debugPlot.Multimesh.SetInstanceTransform(0, directTransform);
				return;
			}
			// Transform3D directTransform = new(Basis.Identity, directPath);
			// Transform3D surfaceTransform = new(Basis.Identity, surfacePath);


			// _debugPlot.Multimesh.SetInstanceColor(2 * i + 0, Colors.Red);
			// _debugPlot.Multimesh.SetInstanceColor(2 * i + 1, Colors.Blue);
			// _debugPlot.Multimesh.SetInstanceTransform(2 * i + 0, directTransform);
			// _debugPlot.Multimesh.SetInstanceTransform(2 * i + 1, surfaceTransform);

		}
		_debugPlot.Multimesh.InstanceCount = 0;

	}

	float CalculateSpeed(float distanceFromSurface, float d_min, float d_max, float v_max)
	{
		// Ensure the distance is within the min and max range
		distanceFromSurface = Mathf.Clamp(distanceFromSurface, d_min, d_max);

		// Inverse relationship: speed decreases as distance decreases
		float t = (distanceFromSurface - d_min) / (d_max - d_min);

		// Calculate the speed
		return Mathf.Lerp(0.001f, v_max, t);
	}

	public override void _Input(InputEvent @event)
	{
		if (Input.IsActionJustReleased("cam_exit"))
		{
			if (Locked)
				UnlockMouse();
			else
				LockMouse();
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

		if (@event is InputEventMouseButton mouseEvent)
		{
			if (mouseEvent.ButtonIndex == MouseButton.Left && mouseEvent.Pressed)
			{
				Vector2 mousePosition = GetViewport().GetMousePosition();
				Vector3 rayOrigin = ProjectRayOrigin(mousePosition);
				Vector3 rayEnd = rayOrigin + ProjectRayNormal(mousePosition) * RayLength;
				CalculateRayToPlanet(rayOrigin, rayEnd);
			}
		}
	}

	public override void _PhysicsProcess(double delta)
	{		
		UIElements.SetDistance(DistanceFromSurface);
		GlobalPosition = Vector3.Back * DistanceFromSurface;
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
}
