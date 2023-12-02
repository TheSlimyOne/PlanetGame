using Godot;
using System;
using System.Collections;
[Tool]
public partial class SurfaceComponent : MeshInstance3D
{
	[Export]
	public Vector3 Normal
	{
		get => _normal;
        set { _normal = value; }
	}

    public Node3D Target
    {
        get => _target;
        set { _target = value; }
    }

    public int Resolution
    {
        get => _resolution;
        set { _resolution = value; }
    }

    public float Radius
    {
        get => _radius;
        set { _radius = value; }
    }

    public Material Material
    {
        get => _material;
        set { _material = value; }
    }

    public QuadTree QuadTree
    {
        get => _quadTree;
        set { _quadTree = value; }
    }

#nullable enable
	private Node3D? _target;
#nullable disable

	private Vector3 _normal;
	private int _resolution = 2;
	private float _radius = 20;

	private Material _material;
	private QuadTree _quadTree;

	public void GenerateSurface(float _radius, int _resolution, Material _material, Node3D _target)
	{
		Mesh = new ArrayMesh();
		this._target = _target;
		this._resolution = _resolution;
		this._radius = _radius;
		this._material = _material;
		_quadTree = new QuadTree(this);
		RegenerateSurface();
	}

	public void RegenerateSurface()
	{	
		_quadTree.UpdateQuadTree(_target);
	}

	private int i = 0;
	private int j = 1;
	public override void _Process(double delta)
	{
		if (!Engine.IsEditorHint() && _quadTree != null && i < j)
			RegenerateSurface();
	}
}
