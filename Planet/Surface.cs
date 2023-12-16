using Godot;
using System;
using System.Collections;
[Tool]
public partial class Surface : Node3D
{

	[Export] private Vector3 _normal;
	private QuadTree _quadTree;


	public void InitializeQuadTree(float radius, Material material, int resolution)
	{
		
		_quadTree?.Free();
		
		foreach (var child in GetChildren())
		{
			child.QueueFree();
		}
			
		_quadTree = new QuadTree(_normal, radius, material, resolution);
		_quadTree.SpawnChildNodes(this);
	}
	int I = 0;
	public void UpdateQuadTree(Vector3 position)
	{
		
		
		foreach (var child in GetChildren())
		{
			RemoveChild(child);
		}

		if (_quadTree != null)
		{	
			_quadTree.UpdateQuadTree(position);
			_quadTree.SpawnChildNodes(this);
		}

	}
}
