using Godot;
using System;

public partial class UIElements : CanvasLayer
{
	[ExportGroup("Labels")]
	[Export] private Label _lblTriangleCount;
	[Export] private Label _lblFPS;
	[Export] private Label _lblDistance;
	[Export] private Label _lblLOD;
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
}
