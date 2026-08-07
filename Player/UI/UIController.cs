using Godot;
using System;
using System.Reflection;
using System.Collections.Generic;

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
	[Export] private Button _btnTerrainTesselation;
	[Export] private Button _btnCubeMode;
	[Export] private Button _btnCulling;
	[Export] private Button _btnMorphing;
	[Export] private Button _btnShowTileCache;
	[Export] private Button _btnVirtualTexturing;
	[Export] private Button _btnWipeVirtualTexutre;
	[Export] private Button _btnDebug;
	[Export] private Button _btnQuit;
	[Export] private Button _btnSimulateRotation;

	[ExportGroup("Containers")]
	[Export] public Control DebugContainer;

	private int _allMax;
	private int _culledMax;

	public override void _Ready()
	{
		ConnectButton(_btnCubeMode, EnableOrDisableCubeMode);
		ConnectButton(_btnCulling, EnableOrDisableCulling);
		ConnectButton(_btnMorphing, EnableOrDisableMorphing);
		ConnectButton(_btnSimulateRotation, EnableOrDisableRotationEffect);
		ConnectButton(_btnTerrainTesselation, EnableOrDisableTerrainTesselation);
		ConnectButton(_btnShowTileCache, HideOrShowTilesInCache);
		ConnectButton(_btnVirtualTexturing, EnableOrDisableVirtualTexturing);
		ConnectButton(_btnWipeVirtualTexutre, WipeVirtualTexture);
		ConnectButton(_btnDebug, EnableOrDisableDebug);
		ConnectButton(_btnQuit, Quit);

		UpdateButtonLabels();
	}


	public override void _ExitTree()
	{
		DisconnectButton(_btnCubeMode, EnableOrDisableCubeMode);
		DisconnectButton(_btnCulling, EnableOrDisableCulling);
		DisconnectButton(_btnMorphing, EnableOrDisableMorphing);
		DisconnectButton(_btnSimulateRotation, EnableOrDisableRotationEffect);
		DisconnectButton(_btnShowTileCache, HideOrShowTilesInCache);
		DisconnectButton(_btnTerrainTesselation, EnableOrDisableTerrainTesselation);
		DisconnectButton(_btnVirtualTexturing, EnableOrDisableVirtualTexturing);
		DisconnectButton(_btnWipeVirtualTexutre, WipeVirtualTexture);
		DisconnectButton(_btnDebug, EnableOrDisableDebug);
		DisconnectButton(_btnQuit, Quit);
	}

	public override void _Process(double delta)
	{
		SetFPSCount((int)Engine.GetFramesPerSecond());
		SetCameraMode();
	}

	public void SetLabelKeyCount(int culled, int all)
	{
		_culledMax = Math.Max(_culledMax, culled);
		_allMax = Math.Max(_allMax, all);

		_lblKeyCount.Text =
			$"Keys: {culled}/{all} | Max: {_culledMax}/{_allMax}";
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

	public void SetCameraMode()
	{
		_lblCameraMode.Text =
			$"Camera Mode: {PlanetController.CameraController.GetViewport().DebugDraw}";
	}

	public void EnableOrDisableTerrainTesselation()
	{
		PlanetController.DisableTesselation =
			!PlanetController.DisableTesselation;

		UpdateToggleButton(
			_btnTerrainTesselation,
			!PlanetController.DisableTesselation,
			"Terrain Tesselation"
		);
	}
	public void EnableOrDisableVirtualTexturing()
	{
		PlanetController.DisableVirtualTexturing =
			!PlanetController.DisableVirtualTexturing;

		UpdateToggleButton(
			_btnVirtualTexturing,
			PlanetController.DisableVirtualTexturing,
			"Virtual Texturing"
		);
	}

	public void WipeVirtualTexture()
	{
		PlanetController.SparseVirtualTexture.ClearVirtualTexture();
	}

	public void EnableOrDisableCubeMode()
	{
		PlanetController.IsCube = !PlanetController.IsCube;

		UpdateToggleButton(
			_btnCubeMode,
			PlanetController.IsCube,
			"Cube Mode"
		);
	}

	public void EnableOrDisableCulling()
	{
		PlanetController.IsCulling = !PlanetController.IsCulling;

		UpdateToggleButton(
			_btnCulling,
			PlanetController.IsCulling,
			"Culling"
		);
	}

	public void EnableOrDisableMorphing()
	{
		PlanetController.IsMorphing = !PlanetController.IsMorphing;

		UpdateToggleButton(
			_btnMorphing,
			PlanetController.IsMorphing,
			"Morphing"
		);
	}

	public void HideOrShowTilesInCache()
	{
		bool newValue = !PlanetController.SurfaceShader.GetShaderParameter("show_in_cache").AsBool();
		PlanetController.SurfaceShader.SetShaderParameter("show_in_cache", newValue);

		UpdateToggleButton(
			_btnShowTileCache,
			newValue,
			"Tiles in Cache"
		);
	}


	public void EnableOrDisableRotationEffect()
	{
		PlanetController.IsSimulateRotation = !PlanetController.IsSimulateRotation;

		UpdateToggleButton(
			_btnSimulateRotation,
			PlanetController.IsSimulateRotation,
			"Simulate Rotation"
		);
	}

	public void Quit()
	{
		GetTree().ChangeSceneToFile("res://Scenes/Main.tscn");
	}
	public void EnableOrDisableDebug()
	{
		Control parent = DebugContainer.GetParent<Control>();
		parent.Visible = !parent.Visible;

		PlanetController.SurfaceShader.SetShaderParameter("render_tile_uvs", parent.Visible);

		UpdateToggleButton(
			_btnDebug,
			parent.Visible,
			"Debug View"
		);
	}

	private void UpdateButtonLabels()
	{
		UpdateToggleButton(
			_btnTerrainTesselation,
			!PlanetController.DisableTesselation,
			"Terrain Tesselation"
		);

		UpdateToggleButton(
			_btnCubeMode,
			PlanetController.IsCube,
			"Cube Mode"
		);

		UpdateToggleButton(
			_btnCulling,
			PlanetController.IsCulling,
			"Culling"
		);

		UpdateToggleButton(
			_btnMorphing,
			PlanetController.IsMorphing,
			"Morphing"
		);

		if (PlanetController.SurfaceShader != null)
		UpdateToggleButton(
			_btnShowTileCache,
			PlanetController.SurfaceShader
				.GetShaderParameter("show_in_cache")
				.AsBool(),
			"Tiles in Cache"
		);

		UpdateToggleButton(
			_btnVirtualTexturing,
			!PlanetController.DisableVirtualTexturing,
			"Virtual Texturing"
		);

		UpdateToggleButton(
			_btnDebug,
			DebugContainer.GetParent<Control>().Visible,
			"Debug View"
		);

		UpdateToggleButton(
			_btnSimulateRotation,
			PlanetController.IsSimulateRotation,
			"Simulate Rotation"
		);
	}

	private static void ConnectButton(Button button, Action handler)
	{
		button.Pressed += handler;
	}

	private static void DisconnectButton(Button button, Action handler)
	{
		button.Pressed -= handler;
	}

	private static void UpdateToggleButton(
		Button button,
		bool isEnabled,
		string settingName,
		string on = "on", string off = "off")
	{
		button.Text = isEnabled
			? $"{on}: {settingName}"
			: $"{off}: {settingName}";
	}
}