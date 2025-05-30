using Godot;
using PlanetGame.Rendering.VirtualTexturing;

public partial class UIController : Control
{
	[Export] public PlanetController PlanetController { get; private set; }
	[ExportGroup("Labels")]
	[Export] private Label _lblKeyCount;
	[Export] private Label _lblFPS;
	[Export] private Label _lblDistance;
	[Export] private Label _lblLOD;
	[Export] private Label _lblCameraMode;
	[ExportGroup("Buttons")]
	[Export] private Button _btnProcessLod;
	[Export] private Button _btnCubeMode;
	[Export] private Button _btnCulling;
	[Export] private Button _btnMorphing;
	[Export] private Button _btnRenderFramebuffer;
	[Export] private Button _btnRenderSurface;
	[Export] private Button _btnVirtualTexturing;

	private int _all_max;
	private int _culled_max;

	public void SetLabelKeyCount(int culled, int all)
	{
		_culled_max = culled > _culled_max ? culled : _culled_max;
		_all_max = all > _all_max ? all : _all_max;
		_lblKeyCount.Text = $"Keys: {culled}/{all} | Max: {_culled_max}/{_all_max}";
	}

	public void SetFPSCount(int amount)
	{
		_lblFPS.Text = $"FPS: {amount}";
	}

	public void SetDistance(float distance)
	{
		_lblDistance.Text = $"Distance: {distance}";
	}

	public void SetCurrentLOD(int current)
	{
		_lblLOD.Text = $"Current LOD: {current}";
	}

	public override void _Process(double delta)
	{
		SetFPSCount((int)Engine.GetFramesPerSecond());
		SetCameraMode();
	}

	public void SetCameraMode()
	{
		_lblCameraMode.Text = $"Camera Mode: {PlanetController.CameraController.GetViewport().DebugDraw}";
	}

	// public void EnableOrDisableProcessing()
	// {
	// 	if (PlanetController.TerrainTessellator.Paused)
	// 		PlanetController.TerrainTessellator.Resume();
	// 	else
	// 		PlanetController.TerrainTessellator.Pause();

	// 	_btnProcessLod.Text = PlanetController.TerrainTessellator.Paused ? "Stop Processing LOD" : "Start Processing LOD";
	// }

	// public void EnableOrDisableVirtualTexturing()
	// {
	// 	GD.Print(PlanetController.SparseVirtualTexture.Paused);
	// 	if (PlanetController.SparseVirtualTexture.Paused)
	// 		PlanetController.SparseVirtualTexture.Resume();
	// 	else
	// 		PlanetController.SparseVirtualTexture.Pause();

	// 	_btnVirtualTexturing.Text = PlanetController.SparseVirtualTexture.Paused ? "Enable Virtual Texturing" : "Disable Virtual Texturing";
	// }

	// public void UpdateProcessingText()
	// {
	// 	_btnProcessLod.Text = PlanetController.TerrainTessellator.Paused ? "Stop Processing LOD" : "Start Processing LOD";
	// }

	public void EnableOrDisableCubeMode()
	{
		bool currentSetting = !PlanetController.CubeMode;
		PlanetController.CubeMode = currentSetting;
		_btnCubeMode.Text = currentSetting ? "Disable Cube Mode" : "Enable Cube Mode";
	}

	public void EnableOrDisableCulling()
	{
		bool currentSetting = !PlanetController.Culling;
		PlanetController.Culling = currentSetting;
		_btnCulling.Text = currentSetting ? "Disable Culling" : "Enable Culling";
	}

	public void EnableOrDisableMorphing()
	{
		bool currentSetting = !PlanetController.Morphing;
		PlanetController.Morphing = currentSetting;
		_btnMorphing.Text = currentSetting ? "Disable Morphing" : "Enable Morphing";
	}


	public void RenderFramebuffer()
	{
		RenderingServer.InstanceGeometrySetMaterialOverride(
			PlanetController.PlanetMultiMesh.Instances[0],
			PlanetController.FramebufferShader.GetRid());
	}

	public void RenderSurface()
	{
		RenderingServer.InstanceGeometrySetMaterialOverride(
			PlanetController.PlanetMultiMesh.Instances[0],
			PlanetController.SurfaceShader.GetRid());
	}

	public void OnClickQuit()
	{
		GetTree().ChangeSceneToFile("res://Scenes/Main.tscn");
	}
	// public void GenerateAlbedoMap()
	// {
	// 	Image image = Image.LoadFromFile($"{PlanetController.SaveRootPath}/Base Images/{PlanetController.BaseAlbedoImageName}");
	// 	image.ResizeToPo2(square: true);
	// 	Vector2I baseImageSize = image.GetSize();
	// 	// ChunkManager chunkManager = new(baseImageSize, PlanetController.TilePartitionCount, PlanetController.BorderSize);
	// 	// chunkManager.BorderSize = 
	// 	// chunkManager.QueueGenerateChunksFromImage(SaveRootPath, "Albedo", $"Base Images/{BaseAlbedoImageName}", "Tiles/Albedo Tiles", "Cube Map/Albedo");
	// 	// _ = chunkManager.CreateChunks().ContinueWith(_ => chunkManager.CleanupGPUResources());
	// }

	// public void GenerateHeightmap()
	// {
	// 	Image image = Image.LoadFromFile($"{PlanetController.SaveRootPath}/Base Images/{PlanetController.BaseHeightmapImageName}");
	// 	image.ResizeToPo2(square: true);
	// 	Vector2I baseImageSize = image.GetSize();

	// 	// ChunkManager chunkManager = new(baseImageSize, CenterSize, BorderSize);
	// 	// chunkManager.QueueGenerateChunksFromImage(SaveRootPath, "Heightmap", $"Base Images/{BaseHeightmapImageName}", "Tiles/Heightmap Tiles", "Cube Map/Heightmap", Image.Interpolation.Trilinear);
	// 	// _ = chunkManager.CreateChunks().ContinueWith(_ => chunkManager.CleanupGPUResources());
	// }
}