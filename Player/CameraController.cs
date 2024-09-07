using Godot;
using Godot.Collections;


public partial class CameraController : Camera3D
{
	[Export] public UIElements UIElements;
	[Export] public WorldEnvironment WorldEnvironment;
	[Export] public float DistanceFromSurface { get; private set; }
	private PlanetController _planetController;
	
	private MultiMeshInstance3D _cameraLine = new();
	private MultiMeshInstance3D _planetLine = new();
	private MultiMeshInstance3D _debugLine = new();

	[Export] public float BaseZoomSpeed;
	[Export] public float RayLength = 5000;
	

	[Export] public bool Locked { get; private set; }

    public override void _Ready()
	{
		_planetController = (PlanetController)GetParent().GetParent();

		GetWindow().CallDeferred("add_child", _cameraLine);
		GetWindow().CallDeferred("add_child", _planetLine);
		GetWindow().CallDeferred("add_child", _debugLine);
		_debugLine.Multimesh = new MultiMesh() { UseColors = true, Mesh = new SphereMesh() { Material = new StandardMaterial3D() { VertexColorUseAsAlbedo = true }, Radius = 2, Height = 4 }, TransformFormat = MultiMesh.TransformFormatEnum.Transform3D };
	}

	public void CalculateRayToPlanet(Vector3 from, Vector3 to)
	{
		PhysicsDirectSpaceState3D spaceState = GetWorld3D().DirectSpaceState;
		Rid inner = _planetController.SurfaceController.InnerCollision.GetRid();
		Rid outer = _planetController.SurfaceController.OuterCollision.GetRid();
		Dictionary[] intersections =  new Dictionary[4];

		intersections[0] = spaceState.IntersectRay( new PhysicsRayQueryParameters3D()
		{
			To = to,
			From = from,
			Exclude = new Array<Rid>(){ inner }
		} );
		intersections[1] = spaceState.IntersectRay( new PhysicsRayQueryParameters3D()
		{
			To = from,
			From = to,
			Exclude = new Array<Rid>(){ inner }
		} );
		intersections[2] = spaceState.IntersectRay( new PhysicsRayQueryParameters3D()
		{
			To = to,
			From = from,
			Exclude = new Array<Rid>(){ outer }
		} );
		intersections[3] = spaceState.IntersectRay( new PhysicsRayQueryParameters3D()
		{
			To = from,
			From = to,
			Exclude = new Array<Rid>(){ outer }
		} );
		
		Color[] colors = new Color[] { Colors.Red, Colors.Blue, Colors.Yellow, Colors.Green };
		_debugLine.Multimesh.InstanceCount = 4;
		for (int i = 0; i < 4; i++)
		{
			if (intersections[i].ContainsKey("position"))
			{
				Vector3 position = (Vector3)intersections[i]["position"];
				// position = _planetController.PlanetData.Rotation * position;

				Transform3D transform = new Transform3D(Basis.Identity, position);
				
				_debugLine.Multimesh.SetInstanceColor(i, colors[i]);
				_debugLine.Multimesh.SetInstanceTransform(i, transform);
			}
		}

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
		Vector3 direction = Vector3.Zero;
		float by = (float)delta;
		
		direction.Y = Input.GetActionStrength("move_up") - Input.GetActionStrength("move_down");
		
		DistanceFromSurface += direction.Y * by * CalculateSpeed(DistanceFromSurface, 0, _planetController.PlanetData.Radius * 2, BaseZoomSpeed);
		DistanceFromSurface = Mathf.Clamp(DistanceFromSurface, 0, float.MaxValue);
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
