using Godot;
using System;
using System.Collections.Generic;

[Tool]
public partial class MeshVisualizer : Node3D
{
	[Export]
	private float Radius
	{
		get => _radius;
		set { _radius = value; CreateConvexHull(); }
	}

	[Export(PropertyHint.Range, "4,500,")]
	private int Amount
	{
		get => _amount;
		set { _amount = value; CreateConvexHull(); }
	}

	[Export]
	private bool isVoronoi
	{
		get => _isVoronoi;
		set { _isVoronoi = value; CreateConvexHull(); }
	}

	[Export]
	private bool isCentroid
	{
		get => _isCentroid;
		set { _isCentroid = value; CreateConvexHull(); }
	}

	[Export]
	private int ShowIndex
	{
		get => _showIndex;
		set { _showIndex = value; CreateConvexHull(); }
	}

	[Export]
	private ShaderMaterial material;


	private float _radius = 1;
	private int _amount = 4;
	private bool _isVoronoi;
	private bool _isCentroid;
	private int _showIndex;

	private MeshVisualizer() { }

	private void CreateConvexHull()
	{
		foreach(var child in GetChildren())
		{
			child.QueueFree();
		}

		Vector3[] seeds = RandomSeeds(_amount);
		ConvexHull convexHull = new ConvexHull(seeds);
		convexHull.Hull.GetMesh(_showIndex, this, _radius, material, isVoronoi, isCentroid);

		Vector3[] voronoiSeeds = convexHull.Hull.GetVoronoiSeeds(isCentroid);
		Tetrahedron.AddAllChildren(this, Tetrahedron.CreatePoint(voronoiSeeds, 0.125f, Colors.Red));
		// ConvexHull voronoiHull = new ConvexHull(voronoiSeeds, true);
		GD.Print(convexHull.Hull);

	}

	internal List<Vector3> FibonacciSeeds(int amount)
	{
		List<Vector3> points = new List<Vector3>();
		float phi = Mathf.Pi * (Mathf.Sqrt(5) - 1);

		for (int i = 0; i < amount; i++)
		{
			float y = 1 - (float)i / (amount - 1) * 2;
			float radius = Mathf.Sqrt(1 - y * y);
			float theta = phi * i;

			float x = Mathf.Cos(theta) * radius;
			float z = Mathf.Sin(theta) * radius;

			points.Add(new Vector3(x, y, z));
		}

		return points;
	}

	internal Vector3[] RandomSeeds(int amount)
	{
		List<Vector3> seeds = new List<Vector3>();
		Random random = new Random(1207);

		for (int i = 0; i < amount; i++)
		{
			float a = random.NextSingle();
			float latitude = random.NextSingle() * 360 - 180;
			float longitude = Mathf.Acos(2 * a - 1);

			float x = Mathf.Cos(latitude) * Mathf.Cos(longitude);
			float y = Mathf.Cos(latitude) * Mathf.Sin(longitude);
			float z = Mathf.Sin(latitude);
			seeds.Add(new Vector3(x, y, z).Normalized());
		}

		return seeds.ToArray();
	}


	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
