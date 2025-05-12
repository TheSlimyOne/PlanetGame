using Godot;

public partial class TestForMultiFramebuffers : Node3D
{
	[Export] SubViewport[] viewports;

	[Export] ShaderMaterial[] shaders;

	[Export] MultiMeshInstance3D meshInstance;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		for (int i = 0; i < viewports.Length; i++)
		{
			viewports[i].RenderTargetClearMode = SubViewport.ClearMode.Never;
		}
		RenderingServer.FramePreDraw += OnFramePreDraw;	
	}

    public override void _Process(double delta)
    {
        meshInstance.RotateX((float)delta * 0.5f);
    }

    private void OnFramePreDraw()
    {
        // Dynamically assign materials for each viewport
		// for (int i = 0; i < viewports.Length; i++)
		// {
		// 	viewports[i].RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
		// }

		int currentIndex = Engine.GetFramesDrawn() % viewports.Length;
		viewports[currentIndex % 3].RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
		meshInstance.MaterialOverride = shaders[currentIndex % 3];

		// viewports[0].RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
		// meshInstance.MaterialOverride = shaders[0];
    }

	public override void _ExitTree()
    {
        // Disconnect the signal when the node is removed
        RenderingServer.FramePreDraw -= OnFramePreDraw;
    }
}
