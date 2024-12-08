using Godot;
using Planet;
using Uniform;
using System;
using Dispatcher;

public partial class UIElements : CanvasLayer
{
	[Export] private PlanetController _planetController;
	[ExportGroup("Labels")]
	[Export] private Label _lblTriangleCount;
	[Export] private Label _lblFPS;
	[Export] private Label _lblDistance;
	[Export] private Label _lblLOD;
	[Export] private Label _lblCameraMode;
	[ExportGroup("Buttons")]
	[Export] private Button _btnProcessLod;
	[Export] private Button _btnColorizeLod;
	[Export] private Button _btnCubeMode;
	[Export] private Button _btnCulling;
	[Export] private Button _btnGenerateNormals;
	[Export] private Button _btnMorphing;
	[Export] private Button _btnGenerateCubeMap;
	[Export] private Button _btnGenerateChunks;

	[ExportGroup("Extra")]
	[Export] public HBoxContainer ImageContainer;

	[ExportGroup("Compute Shader")]
	[Export(PropertyHint.File)] private string _normalPath;
	[Export(PropertyHint.File)] private string _cubeMapPath;

	private int _all_max;
	private int _culled_max;

	public void SetLabelTriangleCount(int culled, int all)
	{
		_culled_max = culled > _culled_max ? culled : _culled_max;
		_all_max = all > _all_max ? all : _all_max;
		_lblTriangleCount.Text = $"Triangles: {culled}/{all} | Max: {_culled_max}/{_all_max}";
	}

	public void SetFPSCount(int amount)
	{
		_lblFPS.Text = $"FPS: {amount}";
	}

	public void SetDistance(float distance)
	{
		_lblDistance.Text = $"Distance: {distance}";
	}

	public void SetCurrentLOD(float lod)
	{
		_lblLOD.Text = $"Current LOD: {lod}";
	}

	public override void _Process(double delta)
	{
		SetFPSCount((int)Engine.GetFramesPerSecond());
		SetCameraMode();
		// SetProfiler();
	}

	public void SetCameraMode()
	{
		_lblCameraMode.Text = $"Camera Mode: {_planetController.CameraController.GetViewport().DebugDraw}";
	}

	public void SetProfiler()
	{
		GD.Print(Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame));
		GD.Print(Performance.GetMonitor(Performance.Monitor.ObjectCount));
	}

	public void EnableOrDisableProcessing()
	{
		bool currentSetting = !_planetController.SurfaceController.Processing;
		_planetController.SurfaceController.Processing = currentSetting;
		_btnProcessLod.Text = currentSetting ? "Stop Processing LOD" : "Start Processing LOD";
		_planetController.PlanetData.SetMaterialParameters();
	}

	public void UpdateProcessingText()
	{
		_btnProcessLod.Text = _planetController.SurfaceController.Processing ? "Stop Processing LOD" : "Start Processing LOD";
	}

	public void EnableOrDisableLodColorize()
	{
		bool currentSetting = !_planetController.PlanetData.ColorizeLod;
		_planetController.PlanetData.ColorizeLod = currentSetting;
		_btnColorizeLod.Text = currentSetting ? "Disable Lod Color" : "Enable Lod Color";
		_planetController.PlanetData.SetMaterialParameters();
	}

	public void EnableOrDisableCubeMode()
	{
		bool currentSetting = !_planetController.PlanetData.CubeMode;
		_planetController.PlanetData.CubeMode = currentSetting;
		_btnCubeMode.Text = currentSetting ? "Disable Cube Mode" : "Enable Cube Mode";
		_planetController.PlanetData.SetMaterialParameters();
	}

	public void EnableOrDisableCulling()
	{
		bool currentSetting = !_planetController.PlanetData.Culling;
		_planetController.PlanetData.Culling = currentSetting;
		_btnCulling.Text = currentSetting ? "Disable Culling" : "Enable Culling";
		_planetController.PlanetData.SetMaterialParameters();
	}

	public void EnableOrDisableMorphing()
	{
		bool currentSetting = !_planetController.PlanetData.Morphing;
		_planetController.PlanetData.Morphing = currentSetting;
		_btnMorphing.Text = currentSetting ? "Disable Morphing" : "Enable Morphing";
		_planetController.PlanetData.SetMaterialParameters();
	}

	public void GenerateNormals()
	{
		RenderingDevice rd = RenderingServer.CreateLocalRenderingDevice();
		CalculateNormalsDispatcher computeNormals = new(_normalPath, ref rd)
		{
			InputTexture = _planetController.PlanetData.HeightMap,
			Radius = _planetController.PlanetData.Radius,
			HeightScale = _planetController.PlanetData.HeightScale
		};
		computeNormals.CreateUniforms();
		computeNormals.Ready();
		rd.Submit();
		rd.Sync();
		computeNormals.SaveNormalMap("Normal");
		computeNormals.CleanupGPU();
		rd.Free();
	}

	public void GenerateCubeMap()
	{
		RenderingDevice rd = RenderingServer.CreateLocalRenderingDevice();
		CalculateCubeMapDispatcher computeCubeMap = new(_cubeMapPath, ref rd)
		{
			InputTexture = _planetController.PlanetData.AlbedoMap,
		};
		computeCubeMap.CreateUniforms();
		computeCubeMap.Ready();
		rd.Submit();
		rd.Sync();
		computeCubeMap.SaveCubeMap("Cubemap");
		computeCubeMap.CleanupGPU();
		rd.Free();
	}

	
	// convert to compressed textures
	// look into resource loader
	public void GenerateChunks()
	{
		// if (chunkedClipmap == null) return;
		// // chunkedClipmap.GenerateImageChunks("My_Planet");
		// tileCache.UpdateCache(_planetController);
		// ClearImages();
		// InsertImage(tileCache.Cache);
		// for (int i = 0; i < chunkedClipmap.TotalSubdivisions * 6; i++)
		// {
		// 	InsertImage(_planetController.PlanetData.IndirectionTable.Table[i]);
		// }
		// _planetController.PlanetData.ShaderMaterial.SetShaderParameter("indirection_table", _planetController.PlanetData.IndirectionTable.ToTexture2DArray());
		// _planetController.PlanetData.ShaderMaterial.SetShaderParameter("tile_cache", tileCache.GetTexture());

	}

	public async void GenerateIndirectionTable()
	{
		Viewport viewport = _planetController.CameraController.GetViewport();
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		ViewportTexture viewportTexture = viewport.GetTexture();
		
		var image = viewportTexture.GetImage();
		image.SavePng("user://Screenshot.png");
	}

	public void InsertImage(Image image)
	{
		TextureRect textureRect = new();
		ImageTexture imageTexture = new();
		imageTexture.SetImage(image);
		textureRect.Texture = imageTexture;
		textureRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		textureRect.StretchMode = TextureRect.StretchModeEnum.KeepAspect;
		textureRect.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		textureRect.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;

		ImageContainer.AddChild(textureRect);
	}
	public void ClearImages()
	{
		foreach (var image in ImageContainer.GetChildren())
		{
			image.QueueFree();
		}
	}
}