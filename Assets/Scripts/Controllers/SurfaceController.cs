using Godot;
using System;

[Tool]
public partial class SurfaceController : Node3D
{
	public void GeneratePlanetSurfaces(float _radius, int _resolution, Material _material, Node3D _target)
	{
		foreach (SurfaceComponent surface in GetChildren())
			surface.GenerateSurface(_radius, _resolution, _material, _target);
		
	}
}
