using Godot;
using Godot.Collections;
using System;

[Tool]
public partial class Planet : Node3D
{
	private float _radius = 1;
	private int _resolution = 5;
	private Material _material = new PlaceholderMaterial();
	private int _maxSubdivisionLevel = 7;

	[Export] private Surface[] _surfaces = new Surface[6];

	[Export] private float RotationAmount = 0.1f;

	[Export] private Curve _distanceCurve;

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
	public int maxSubdivisionLevel
	{
		get => _maxSubdivisionLevel;
		set { _maxSubdivisionLevel = value; UpdatePlanetData(); }
	}

	[Export]
	public Material Material
	{
		get => _material;
		set { _material = value; UpdatePlanetData(); }
	}

	private void UpdatePlanetData()
	{
		ShaderMaterial shaderMaterial = _material as ShaderMaterial;
		shaderMaterial?.SetShaderParameter("radius", _radius);
	
		for (int i = 0; i < _surfaces.Length; i++)
			_surfaces[i]?.InitializeQuadTree(_radius, _material, _resolution, _maxSubdivisionLevel, _distanceCurve);
	}	

	private void OnTargetMovement(Vector3 position)
	{
		for (int i = 0; i < _surfaces.Length; i++)
			_surfaces[i].UpdateQuadTree(position);
	}

	public override void _Ready()
	{
		UpdatePlanetData();
	}

	public override void _Process(double delta)
	{
		//Rotate(Vector3.Up, _rotationAmount * (float) delta);
	}
}
