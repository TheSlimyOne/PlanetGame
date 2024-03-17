using Godot;


[Tool]
public partial class Planet : Node3D
{
	private float _radius = 1;
	private int _resolution = 5;
	private ShaderMaterial _material;
	[Export] public Surface Surface { get => _surface; set { _surface = value; } }
	[Export] public Node QuadTreesContainer { get => _quadTreesContainer; set { _quadTreesContainer = value; } }
	private Surface _surface;
	private Node _quadTreesContainer;

	private bool _isReady;

	[Export] private float RotationAmount = 0.1f;

	[Export] private Curve _distanceCurve;

	[Export]
	public float Radius
	{
		get => _radius;
		set { _radius = value; Initialize(); }
	}

	[Export(PropertyHint.Range, "3,1000,")]
	public int Resolution
	{
		get => _resolution;
		set { _resolution = value; Initialize(); }
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
		set { _material = value; Initialize(); }
	}

	private void Initialize()
	{

		if (_isReady)
		{
			if (_material == null) return;

			_material.SetShaderParameter("radius", _radius);


			_surface = GetChild<Surface>(0);
			QuadTreesContainer = GetChild<Node>(1);

			_surface.Initialize(_radius, _resolution, _material);
			_surface.UpdateQuadTrees(Vector3.Inf);

		}
	}

	private void DisableOrEnableSurfaceFace(bool selection, int index)
	{
		QuadTree quadTree = QuadTreesContainer.GetChild<QuadTree>(index);
		quadTree.IsDisabled = selection;
	}

	private bool IsSurfaceFaceDisabled(int index)
	{
		return QuadTreesContainer.GetChild<QuadTree>(index).IsDisabled;
	}


	private void OnTargetMovement(Vector3 position)
	{
		_surface.UpdateQuadTrees(position);
	}

	public override void _Ready()
	{
		_surface = _surface == null ? GetChild<Surface>(0) : _surface;
		_quadTreesContainer = _quadTreesContainer == null ? GetChild<Node>(1) : _quadTreesContainer;
		_isReady = true;

		Initialize();
	}

	public override void _Process(double delta)
	{
		//Rotate(Vector3.Up, _rotationAmount * (float) delta);
	}
}
