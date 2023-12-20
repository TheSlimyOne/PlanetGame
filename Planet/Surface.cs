using Godot;
using System;
using System.Collections;
[Tool]
public partial class Surface : Node3D
{

	[Export] private Vector3 _normal;
	private QuadTree _quadTree;
	[Export] private bool disabled = false;


	public void InitializeQuadTree(float radius, Material material, int resolution, int maxSubdivisionLevel, Curve distanceCurve)
	{
		if (!disabled)
		{
			_quadTree?.Free();
			
			foreach (var child in GetChildren())
			{
				child.QueueFree();
			}
				
			_quadTree = new QuadTree(this, _normal, radius, material, resolution, maxSubdivisionLevel, distanceCurve);
			_quadTree.SpawnChildNodes(this);
		}
	}

	public void UpdateQuadTree(Vector3 position)
	{
		
		if (!disabled)
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
}
