using Godot;


[Tool]
public partial class Planet : StaticBody3D
{

	private bool _isReady;
	private float _radius = 1;
	private int _resolution = 5;
	private ShaderMaterial _material;
	private Surface _surface;
	private Node _quadTreesContainer;
	private CollisionShape3D _gravityCollision;
	private CollisionShape3D _planetCollision;
	private float _gravityRadius;
	private float[] _subdivision;
	private float[] _distance;

	// Properties
	[Export]
	public float GravityRadius
	{
		get => _gravityRadius;
		set { _gravityRadius = value; Initialize(); }
	}

	[Export (PropertyHint.Range, "1,1000")]
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

	[Export]
	public float[] Distance
	{
		get => _distance;
		set => _distance = value;
	}

	private void Initialize()
	{
		if (_isReady)
		{
			if (_material == null) return;

			_material.SetShaderParameter("radius", _radius);


			_surface = GetNode<Surface>("Surface");
			QuadTreesContainer = GetNode<Node>("QuadTrees");

			_planetCollision = GetNode<CollisionShape3D>("PlanetCollisionShape");
			_planetCollision.Shape = new SphereShape3D();
			((SphereShape3D)_planetCollision.Shape).Radius = _radius;

			
			_gravityCollision = GetNode<CollisionShape3D>("GravityArea/GravityCollisionShape");
			_gravityCollision.Shape = new SphereShape3D();
			((SphereShape3D)_gravityCollision.Shape).Radius = _radius + _gravityRadius;

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
