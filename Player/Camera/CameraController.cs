using Godot;
using Godot.Collections;


public partial class CameraController : Node
{
	public Dictionary<string, CustomCamera> Cameras { get; private set; } = new();
	[Export] public bool Debug { get; set; }

	public override void _Ready()
	{
		foreach (Node node in GetChildren())
		{
			if (node is CustomCamera camera)
			{
				Cameras[camera.Name] = camera;
				if (Cameras.Count == 1)
				{
					SetCurrent(camera.Name);
				}
			} else if (node is SubViewport subViewport) 
			{
				camera = subViewport.GetChild<CustomCamera>(0);
				Cameras[camera.Name] = camera;
				if (Cameras.Count == 1)
				{
					SetCurrent(camera.Name);
				}
			}else if (node is SubViewportContainer subViewportContainer) 
			{
				camera = subViewportContainer.GetChild(0).GetChild<CustomCamera>(0);
				Cameras[camera.Name] = camera;
				if (Cameras.Count == 1)
				{
					SetCurrent(camera.Name);
				}
			}
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (Debug && Input.IsActionJustPressed("switch_cam"))
		{
			if (GetCurrent() is CustomCamera camera)
			{
				var cameraNames = new System.Collections.Generic.List<string>(Cameras.Keys);
				int currentIndex = cameraNames.IndexOf(camera.Name);
				int nextIndex = currentIndex + 1 % cameraNames.Count;
				SetCurrent(cameraNames[nextIndex]);
			}
		}
		if (Input.IsActionJustPressed("change_view"))
		{
			Viewport viewport = GetCurrent().GetViewport();
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
	}

	public CustomCamera GetCamera(string cameraName) => Cameras[cameraName];

	public CustomCamera GetMainCamera() => Cameras["Main"];

	public float GetCameraFov(string cameraName, bool inRadians) => Cameras[cameraName].GetCameraFov(inRadians);

	public Projection GetViewProjectionMatrix(string cameraName) => Cameras[cameraName].GetViewProjectionMatrix();

	public void SetCurrent(string cameraName)
	{
		Cameras[cameraName].MakeCurrent();
		GD.Print($"Switched to camera: {cameraName} at {Cameras[cameraName].GlobalPosition}");
	}

	public Camera3D GetCurrent(){
		return GetTree().Root.GetViewport().GetCamera3D();
	}

}
