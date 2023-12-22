using Godot;
using Godot.Collections;
using System;

[Tool]
public partial class Planet : Node3D
{
	private int _radius = 1;
	private int _resolution = 5;
	private int _maxSubdivisionLevel = 7;

	[Export] private Surface[] _surfaces = new Surface[6];

	[Export]
	public int Radius
	{
		get => _radius;
		set { _radius = value; UpdatePlanetData(); }
	}

	[Export(PropertyHint.Range, "2,100,")]
	public int Resolution
	{
		get => _resolution;
		set { _resolution = value; UpdatePlanetData(); }
	}


	private void UpdatePlanetData()
	{
		for (int i = 0; i < _surfaces.Length; i++)
			_surfaces[i]?.InitializeQuadTree(Position, _radius, _resolution);
	}	

	private Vector3 previousPosition = Vector3.Inf;
	private float movementThreshold = 10;
	private void OnTargetMovement(Vector3 position)
	{
		// if (position.DistanceTo(previousPosition) >= movementThreshold)
		{
			for (int i = 0; i < _surfaces.Length; i++)
				_surfaces[i].UpdateQuadTree(position);
			// previousPosition = position;
		}

	}

	public override void _Ready()
	{
		UpdatePlanetData();
	}

	public override void _Process(double delta)
	{
		// Rotate(Vector3.Up, _rotationAmount * (float) delta);
	}
}
