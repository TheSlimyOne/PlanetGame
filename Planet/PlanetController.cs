using Godot;
using System;
using Planet;

public partial class PlanetController : Node3D
{
	[Export] public PlanetData PlanetData { get; private set; }
	[Export] public CameraController CameraController { get; private set; }
	[Export] public SurfaceController SurfaceController { get; private set; }
	[Export] public Node3D SurfaceAttachment { get; private set; }

	
}
