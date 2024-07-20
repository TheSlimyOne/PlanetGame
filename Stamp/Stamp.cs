using Godot;
using Godot.Collections;
using System.Threading.Tasks;

[Tool]
public partial class Stamp : Node3D
{
    [Export] SubViewport viewport;
    [Export] Camera3D camera;
    [Export] MeshInstance3D stampMesh;
    [Export] float stampSize;

    public override async void _Ready()
    {
        ((PlaneMesh)stampMesh.Mesh).Size = Vector2.One * stampSize;
        camera.Size = stampSize; 


        




        if (viewport != null)
        {
            // viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
            // await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);

            // ViewportTexture texture = viewport.GetTexture();
            // Image newHeightmapData = texture.GetImage();
            // GD.Print("hi");
            // newHeightmapData.SavePng("THIS_IMAGE.png");
            // GD.Print("hi");

        }
    }
}