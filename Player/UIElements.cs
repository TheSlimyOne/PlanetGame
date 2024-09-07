using Godot;
using Planet;
using System;

public partial class UIElements : CanvasLayer
{
	[Export] private PlanetController _planetController;
	[ExportGroup("Labels")]
	[Export] private Label _lblTriangleCount;
	[Export] private Label _lblFPS;
	[Export] private Label _lblDistance;
	[Export] private Label _lblLOD;
	[ExportGroup("Buttons")]
	[Export] private Button _btnProcessLod;
	[Export] private Button _btnColorizeLod;
	[Export] private Button _btnCubeMode;
	[Export] private Button _btnCulling;

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
	}

	public void EnableOrDisableProcessing()
	{
		bool currentSetting = !_planetController.SurfaceController.Processing;
		_planetController.SurfaceController.Processing = currentSetting;
		_btnProcessLod.Text = currentSetting ? "Stop Processing LOD" : "Start Processing LOD";
		_planetController.PlanetData.SetMaterialParameters();
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
}
