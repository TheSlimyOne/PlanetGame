using Godot;
using System;
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
	[Export] private Label _lblLods;
	
	private int _allMax;
	private int _culledMax;

	public override void _Process(double delta)
	{
		SetFPSCount((int)Engine.GetFramesPerSecond());
		SetCameraMode();
	}

	public void SetLabelKeyCount(int culled, int all)
	{
		_culledMax = Math.Max(_culledMax, culled);
		_allMax = Math.Max(_allMax, all);

		_lblKeyCount.Text = $"Keys: {culled}/{all} | Max: {_culledMax}/{_allMax}";
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
		_lblCameraMode.Text = $"Camera Mode: {PlanetController.CameraController.GetViewport().DebugDraw}";
	}

	public void SetLodCounts(int[] lodCount)
	{
		string text = "";

		for (int i = 0; i < lodCount.Length; i++)
			text += $"Lod {i}: {lodCount[i]}\n";

		_lblLods.Text = text.StripEdges();
	}
}