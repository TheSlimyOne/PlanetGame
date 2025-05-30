using System.Collections.Generic;
using Godot;
using PlanetGame.ComputeShaders;
using static SaveManager;

public partial class DemoPlanet : SubViewport
{
	[Export] public float RotationSpeed;
	[Export] public MeshInstance3D Planet;
	
	public override void _Ready()
	{
		
	}

	public void SetThumbnails(Dictionary<SaveDataIdentifier, Texture2D> images)
	{
		ShaderMaterial shaderMaterial = (ShaderMaterial)Planet.Mesh.SurfaceGetMaterial(0);

		shaderMaterial.SetShaderParameter("albedo", images[SaveDataIdentifier.THUMBNAIL_ALEBDO]);
		shaderMaterial.SetShaderParameter("height_map", images[SaveDataIdentifier.THUMBNAIL_HEIGHT_MAP]);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		Planet.Rotate(Vector3.Up, (float)delta * RotationSpeed);
	}
}
