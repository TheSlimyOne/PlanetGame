using Godot;


public partial class Planet : Node3D
{

	private bool _isReady;
	private float _radius = 1;
	private float _heightScale = 1;
	private int _resolution = 5;
	private ShaderMaterial _material;
	private Surface _surface;
	private Node _quadTreesContainer;

	private float[] _distance;
	private CompressedTexture2D _heightMap;


	[Export(PropertyHint.Range, "1,1000")]
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

	[Export(PropertyHint.Range, "0,1")]
	public float HeightScale
	{
		get => _heightScale;
		set { _heightScale = value; Initialize(); }
	}

	[Export]
	public ShaderMaterial Material
	{
		get => _material;
		set { _material = value; Initialize(); }
	}

	[Export]
	public Surface Surface
	{
		get => _surface;
		private set { _surface = value; }
	}

	[Export]
	public Node QuadTreesContainer
	{
		get => _quadTreesContainer;
		set { _quadTreesContainer = value; }
	}


	[Export]
	public float[] Distance
	{
		get => _distance;
		set => _distance = value;
	}

	[Export]
	public CompressedTexture2D HeightMap
	{
		get => _heightMap;
		set { _heightMap = value; Initialize(); }
	}

	private void Initialize()
	{
		if (_isReady)
		{
			if (_material == null) return;

			_material.SetShaderParameter("radius", _radius);
			_material.SetShaderParameter("image_texture", _heightMap);
			_material.SetShaderParameter("height_scale", _radius * _heightScale);


			_surface = GetNode<Surface>("Surface");
			QuadTreesContainer = GetNode<Node>("QuadTrees");

			_surface.Initialize(_radius, _resolution, _material, _heightMap);
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

	private Vector3 previousPosition = Vector3.Inf;
	private float movementThreshold = 10;
	private void OnTargetMovement(Vector3 position)
	{
		// GD.Print(position);
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

		// Rotate(Vector3.Up, _rotationAmount * (float) delta);
	}
}
