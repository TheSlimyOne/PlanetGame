using Godot;
using System;
using Planet;

public partial class PlanetController : Node3D
{
	[Export] public PlanetData PlanetData { get; private set; }
	[Export] public CameraController CameraController { get; private set; }
	[Export] public SurfaceController SurfaceController { get; private set; }
	[Export] public Node3D SurfaceAttachment { get; private set; }
	[Export] public Camera3D DebugCamera { get; private set; }

	float radius = 25;
	public void InsertSphereAt(Vector3 position, Color color, bool attachToPlanet = true)
	{
		MeshInstance3D mesh = new MeshInstance3D()
		{
			Mesh = new SphereMesh() { Radius = radius, Height = radius * 2, Material = new StandardMaterial3D() { AlbedoColor = color }}

		};
		if (attachToPlanet)
		{
			SurfaceAttachment.AddChild(mesh);
		}
		else
		{
			AddChild(mesh);
		}
		mesh.GlobalPosition = SurfaceAttachment.GlobalPosition - position;
	}
	
}
