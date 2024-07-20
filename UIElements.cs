using Godot;
using System;

public partial class UIElements : CanvasLayer
{
	[ExportGroup("Labels")]
	[Export] private Label _lblTriangleCount;
	[Export] private Label _lblFPS;
	[Export] private Label _lblDistance;
	private int _unloaded_max;
	private int _loaded_max;

	public void SetLabelTriangleCount(int loaded, int unloaded)
	{
		_loaded_max = loaded > _loaded_max ? loaded : _loaded_max;
		_unloaded_max = unloaded > _unloaded_max ? unloaded : _unloaded_max;
		_lblTriangleCount.Text = $"Triangles: {loaded}/{unloaded} | Max: {_loaded_max}/{_unloaded_max}";
	}

	public void SetFPSCount(int amount)
	{
		_lblFPS.Text = $"FPS: {amount}";
	}

	public void SetDistance(float distance)
	{
		_lblDistance.Text = $"Distance: {distance}";
	}

	public override void _Process(double delta)
	{
		SetFPSCount((int)Engine.GetFramesPerSecond());
	}
}
