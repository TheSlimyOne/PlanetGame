using Godot;
using PlanetGame.Rendering.VirtualTexturing;
using System;
using System.Linq;
[Tool]
public partial class Test6 : Node3D
{
	[ExportToolButton("Do")]
	public Callable Execute => Callable.From(Run);
	[Export] public Texture2D texture;
	[Export] public MeshInstance3D cube;
	[Export] MeshInstance3D[] outputs = new MeshInstance3D[6];
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	public void Run()
	{
		ChunkManager chunkManager = new();
		Texture2DArray cubeMap = new();
		Godot.Collections.Array<Image> images = [.. ChunkManager.GenerateCubeMapFromImage(texture.GetImage())];
		// chunkManager.CleanupGPUResources();
		// cubeMap.CreateFromImages(images);

		// ShaderMaterial material = (ShaderMaterial)cube.Mesh.SurfaceGetMaterial(0);
		// material.SetShaderParameter("images", cubeMap);

		// for (int i = 0; i < 6; i++)
		{
			// outputs[i].MaterialOverride = new StandardMaterial3D() { AlbedoTexture = ImageTexture.CreateFromImage(images[i])};
		}
	}	

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
