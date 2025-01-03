using Godot;
using System;
using Planet;
using Godot.Collections;

public partial class PlanetController : Node3D
{
	[Export] public PlanetData PlanetData { get; private set; }
	[Export] public CameraController CameraController { get; private set; }
	[Export] public SurfaceController SurfaceController { get; private set; }
	[Export] public Node3D SurfaceAttachment { get; private set; }
	public CustomCamera MainCamera { get; private set; }

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
	

	public override void _Ready()
    {
		SetupCameras();
		// PlanetData.InitializeVirtualTextures();

		// Godot.Collections.Array d = PlanetData.RenderSurface.Shader.GetShaderUniformList();
		// foreach (var z in d)
		// {
		// 	GD.Print(z);
		// }
		PlanetData.PopulateShaderParameters();
		SurfaceController.InvokeComputeShaders();
		SurfaceController.Processing = true;
		Rid mainInstance = SurfaceController.CreateMultimeshInstance(
			Transform3D.Identity, GetWorld3D().Scenario, 2 * PlanetData.Radius, 0b1u
		);
		// RenderingServer.InstanceGeometrySetMaterialOverride(mainInstance, );
		
		Rid helperInstance = SurfaceController.CreateMultimeshInstance(
			Transform3D.Identity.Translated(new Vector3(0, 2 * PlanetData.Radius, 0)), GetWorld3D().Scenario, 2 * PlanetData.Radius, 0b10u
		);
		// RenderingServer.InstanceGeometrySetMaterialOverride(helperInstance, );

		
	}

    public override void _Process(double delta)
    {
        CustomCamera helperCamera = CameraController.GetCamera("Helper");
		PlanetData.RenderSurface.SetShaderParameter("camera_position", helperCamera.GlobalPosition);
		PlanetData.RenderSurface.SetShaderParameter("sub_factor", PlanetData.SubFactor);
		PlanetData.RenderSurface.SetShaderParameter("radius", PlanetData.Radius);
		PlanetData.RenderSurface.SetShaderParameter("fovy", helperCamera.GetCameraFov(true));
    }

    public void SetupCameras()
	{
		MainCamera = CameraController.GetCamera("Main");
		CustomCamera helperCamera = CameraController.GetCamera("Helper");
		helperCamera.Follow(MainCamera);

       	MainCamera.DistanceFromTarget = PlanetData.Radius * 2f;
		MainCamera.MinDistance = PlanetData.Radius + 0.999f;
		MainCamera.MaxDistance = PlanetData.Radius * 10f;
		MainCamera.GlobalPosition = Vector3.Back * MainCamera.DistanceFromTarget;
		CameraController.SetCurrent("Main");
		MainCamera.LockMouse();

	}

    public override void _Input(InputEvent @event)
    {
        if (Input.IsActionJustReleased("cam_exit"))
		{
			// GD.Print("HES DOING IT WTF");
			// RenderingDevice rd = RenderingServer.GetRenderingDevice();
			// Rid a = rd.TextureCreate(new RDTextureFormat() { Width = 512, Height = 512 , UsageBits = RenderingDevice.TextureUsageBits.ColorAttachmentBit }, new RDTextureView());
			// Rid framebuffer = rd.FramebufferCreate(new Array<Rid>() { a, b});
			// rd.framebuffer ;

			// rd.FreeRid(framebuffer);
		}

    }
}
// FramebufferCacheRD