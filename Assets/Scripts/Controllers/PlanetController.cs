using Godot;
using Godot.Collections;
using System;
[Tool]
public partial class PlanetController : Node3D
{

	[Export]
	public SurfaceController SurfaceController
	{
		get => _surfaceController;
		set { _surfaceController = value; }
	}

	[Export]
	public Node3D Target 
	{ 
		get => _target;
		set {_target = value; UpdatePlanetData(); }
	}

	[Export(PropertyHint.Range, "0,5000,")]
	public float Radius
	{
		get => _radius;
		set { _radius = value; UpdatePlanetData(); }
	}

	[Export(PropertyHint.Range, "2,500,")]
	public int Resolution
	{
		get => _resolution;
		set { _resolution = value; UpdatePlanetData(); }
	}

	[Export]
	public Material Material
	{
		get => _material;
		set { _material = value; UpdatePlanetData(); }
	}

	private float _radius = 1;
	private int _resolution = 5;
	private Material _material = new PlaceholderMaterial();
	private Node3D _target;
	private SurfaceController _surfaceController;

	public void UpdatePlanetData()
	{
		
		ShaderMaterial shaderMaterial = _material as ShaderMaterial;

		if (shaderMaterial != null)
			shaderMaterial.SetShaderParameter("radius", _radius);


		_surfaceController?.GeneratePlanetSurfaces(_radius, _resolution, _material, _target);
	}

	public override void _Ready()
	{
		UpdatePlanetData();
	}
}
