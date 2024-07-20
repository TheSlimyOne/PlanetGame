using Godot;
using System;

public partial class CameraController : Camera3D
{
	[Export] public SurfaceController SurfaceController;
	[Export] public UIElements UIElements;
	[Export] public WorldEnvironment WorldEnvironment;

	[Export] public bool Locked { get; private set; }

    public override void _Ready()
	{
		RenderingServer.SetDebugGenerateWireframes(true);
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
