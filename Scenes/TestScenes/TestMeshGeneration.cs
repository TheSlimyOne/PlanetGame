using Godot;
using PlanetGame.ComputeShaders.Dispatcher;
using PlanetGame.Rendering.VirtualTexturing;
using System;
[Tool]
public partial class TestMeshGeneration : MeshInstance3D
{
	[ExportToolButton("Generate")]
    public Callable ClickMeButton => Callable.From(ClickMe);
	[Export] public int Resolution { get; set; } = 5;
	[Export] public float Strength { get; set; } = 1;
	[Export] public CompressedTexture2D Texture { get; set; }
	
	
    public void ClickMe()
    {
		Image image = Texture.GetImage();
		image.Decompress();

		Mesh mesh = ExecuteTessellationPassDispatcher.GeneratePlanetMesh(image, Resolution, Strength);
		Godot.Collections.Array surfaceArrays = mesh.SurfaceGetArrays(0);
		int vertexCount = surfaceArrays[(int)Mesh.ArrayType.Vertex].AsVector3Array().Length;
		int triangleCount = surfaceArrays[(int)Mesh.ArrayType.Index].AsInt32Array().Length;
		int normalCount = surfaceArrays[(int)Mesh.ArrayType.Normal].AsVector3Array().Length;
		int uvCount = surfaceArrays[(int)Mesh.ArrayType.TexUV].AsVector2Array().Length;
		GD.PrintS($"vertices: {vertexCount}, triangles: {triangleCount / 3}, normals: {normalCount}, uvs: {uvCount}");
		
		Mesh = mesh;

    }
}
