using Godot;
using Godot.Collections;
using System;

[Tool]
public partial class Planet : Node3D
{
	private float _radius = 1;
	private int _resolution = 5;

	private Node3D _surfaceContainer;
	private ShaderMaterial _material;


	[Export]
	public float Radius
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

	[Export]
	public ShaderMaterial Material
	{
		get => _material;
		set { _material = value; UpdatePlanetData(); }
	}



	private void UpdatePlanetData()
	{
		if (_surfaceContainer != null)
		{

			_material.SetShaderParameter("radius", _radius);
			_material.SetShaderParameter("resolution", _resolution);
			foreach (Surface surface in _surfaceContainer.GetChildren())
			{
				surface?.InitializeQuadTree(Position, _radius, _resolution, _material);
			}
		}
			
	}	

	private Vector3 previousPosition = Vector3.Inf;
	private float movementThreshold = 10;
	private void OnTargetMovement(Vector3 position)
	{
		if (_surfaceContainer != null)
		{
			foreach (Surface surface in _surfaceContainer.GetChildren())
			{
				surface?.UpdateQuadTree(position);
			}
		}

	}

	public override void _Ready()
	{
		_surfaceContainer = GetChild<Node3D>(0);
		UpdatePlanetData();
	}

	public override void _Process(double delta)
	{

		// Rotate(Vector3.Up, _rotationAmount * (float) delta);
	}
}
