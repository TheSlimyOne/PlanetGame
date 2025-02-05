using Godot;
using Planet;
using Uniform;
using System;
using Dispatcher;

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

	public void EnableOrDisableProcessing()
	{
		bool currentSetting = !PlanetController.SurfaceController.Processing;
		PlanetController.SurfaceController.Processing = currentSetting;
		_btnProcessLod.Text = currentSetting ? "Stop Processing LOD" : "Start Processing LOD";

	}

	public void EnableOrDisableVirtualTexturing()
	{
		bool currentSetting = !PlanetController.PlanetData.SparseVirtualTexture.Enabled;
		PlanetController.PlanetData.SparseVirtualTexture.Enabled = currentSetting;
		_btnVirtualTexturing.Text = currentSetting ? "Disable Virtual Texturing" : "Enable Virtual Texturing";

	}

	public void UpdateProcessingText()
	{
		_btnProcessLod.Text = PlanetController.SurfaceController.Processing ? "Stop Processing LOD" : "Start Processing LOD";
	}

	public void EnableOrDisableCubeMode()
	{
		bool currentSetting = !PlanetController.PlanetData.CubeMode;
		PlanetController.PlanetData.CubeMode = currentSetting;
		_btnCubeMode.Text = currentSetting ? "Disable Cube Mode" : "Enable Cube Mode";
	}

	public void EnableOrDisableCulling()
	{
		bool currentSetting = !PlanetController.PlanetData.Culling;
		PlanetController.PlanetData.Culling = currentSetting;
		_btnCulling.Text = currentSetting ? "Disable Culling" : "Enable Culling";
	}

	public void EnableOrDisableMorphing()
	{
		bool currentSetting = !PlanetController.PlanetData.Morphing;
		PlanetController.PlanetData.Morphing = currentSetting;
		_btnMorphing.Text = currentSetting ? "Disable Morphing" : "Enable Morphing";
	}

	
	public void RenderFramebuffer()
	{
		RenderingServer.InstanceGeometrySetMaterialOverride(
			PlanetController.SurfaceController.Surfaces[0], 
			PlanetController.PlanetData.FramebufferShader.GetRid());
	}
	public void RenderSurface()
	{
		RenderingServer.InstanceGeometrySetMaterialOverride(
			PlanetController.SurfaceController.Surfaces[0], 
			PlanetController.PlanetData.SurfaceShader.GetRid());
	}

}