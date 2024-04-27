using Godot;


[Tool]
public partial class Planet : StaticBody3D
{

	private bool _isReady;
	private float _radius = 1;
	private float _heightScale = 1;
	private int _resolution = 5;
	private ShaderMaterial _material;
	private Surface _surface;
	private Node _quadTreesContainer;
	private CollisionShape3D _gravityCollision;
	private float _gravityRadius;
	private float[] _subdivision;
	private float _distanceFactor = 0.001f;
	private CompressedTexture2D _heightMap;
	private CompressedTexture2D _albedoMap;
	private bool _isDebug;
	private bool _isCube;

	// Properties
	[Export]
	public float GravityRadius
	{
		get => _gravityRadius;
		set { _gravityRadius = value; Initialize(); }
	}

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
	public float[] Subdivision
	{
		get => _subdivision;
		set => _subdivision = value;
	}

	[Export(PropertyHint.Range, "0,1,0.001")]
	public float DistanceFactor
	{
		get => _distanceFactor;
		set => _distanceFactor = value;
	}

	[Export]
	public CompressedTexture2D HeightMap
	{
		get => _heightMap;
		set { _heightMap = value; Initialize(); }
	}

	[Export]
	public CompressedTexture2D AlbedoMap
	{
		get => _albedoMap;
		set { _albedoMap = value; Initialize(); }
	}

	[Export]
	public bool IsDebug
	{
		get => _isDebug;
		set { _isDebug = value; Initialize(); }
	}

	[Export]
	public bool IsCube
	{
		get => _isCube;
		set { _isCube = value; Initialize(); }
	}

	private void Initialize()
	{
		if (_isReady)
		{
			if (_material == null) return;

			_material.SetShaderParameter("radius", _radius);
			_material.SetShaderParameter("albedo_map", _albedoMap);
			_material.SetShaderParameter("image_texture", _heightMap);
			_material.SetShaderParameter("height_scale", _radius * _heightScale);
			_material.SetShaderParameter("is_debug", _isDebug);
			_material.SetShaderParameter("is_cube", _isCube);

			_surface = GetNode<Surface>("Surface");
			QuadTreesContainer = GetNode<Node>("QuadTrees");

			_gravityCollision = GetNode<CollisionShape3D>("GravityArea/GravityCollisionShape");
			_gravityCollision.Shape = new SphereShape3D();
			((SphereShape3D)_gravityCollision.Shape).Radius = _radius + _gravityRadius;

			_surface.Initialize(_radius, _resolution, _material, _heightMap);
			_surface.UpdateQuadTrees(null);

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
	private void OnTargetMovement(Camera3D camera)
	{
	
		_surface.UpdateQuadTrees(camera);
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
