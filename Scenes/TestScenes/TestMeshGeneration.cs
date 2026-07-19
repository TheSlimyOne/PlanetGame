using Godot;
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
		Mesh = TileManager.GetPlanetMesh(Resolution, Texture.GetImage(), Strength);
    }
}
