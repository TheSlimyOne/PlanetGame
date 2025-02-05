using Godot;
using System;
using Planet;
using Godot.Collections;
using System.Threading.Tasks;

public partial class PlanetController : Node3D
{
	[Export] public PlanetData PlanetData { get; private set; }
	[Export] public CameraController CameraController { get; private set; }
	[Export] public SurfaceController SurfaceController { get; private set; }
	[Export] public Node3D SurfaceAttachment { get; private set; }
	[Export] public UIController UIController { get; private set; }
	public OrbitalCamera3D MainCamera { get; private set; }
	private MeshInstance3D sphere;


	float radius = 5;
	public MeshInstance3D InsertSphereAt(Vector3 position, Color color, bool attachToPlanet = true)
	{
		MeshInstance3D mesh = new()
		{
			Mesh = new SphereMesh() { Radius = radius, Height = radius * 2, Material = new StandardMaterial3D() { AlbedoColor = color } }
		};

		if (attachToPlanet)
		{
			SurfaceAttachment.AddChild(mesh);
		}
		else
		{
			AddChild(mesh);
		}

		mesh.Position = position;
		return mesh;
	}

	public override void _Ready()
	{
		SetupCameras();
		PlanetData.Scaled(Vector3.One * PlanetData.Radius);
		PlanetData.Translate(Vector3.Back * (1 - PlanetData.Radius));
		PlanetData.InitializeVirtualTextures(CameraController.GetCamera("Lookup").GetViewport(), this);
		PlanetData.SurfaceShaderBindParameters(CameraController.GetCamera("Main"), CameraController.GetCamera("Helper"));
		PlanetData.FramebufferShaderBindParameters(CameraController.GetCamera("Main"), CameraController.GetCamera("Helper"));
		PlanetData.UpdateShaderParameters();

		SurfaceController.InitializeComputeShaders();
		SurfaceController.Processing = true;

		Rid mainInstance = SurfaceController.CreateMultimeshInstance(
			Transform3D.Identity, GetWorld3D().Scenario, 2 * PlanetData.Radius, 0b1u
		);
		RenderingServer.InstanceGeometrySetMaterialOverride(mainInstance, PlanetData.SurfaceShader.GetRid());

		CustomCamera lookupViewport = CameraController.GetCamera("Lookup");

		Rid lookupInstance = SurfaceController.CreateMultimeshInstance(
			Transform3D.Identity, lookupViewport.GetWorld3D().Scenario, 2 * PlanetData.Radius, 0b1u
		);
		RenderingServer.InstanceGeometrySetMaterialOverride(lookupInstance, PlanetData.FramebufferShader.GetRid());
	}

	Vector3[] normals = [Vector3.Right, Vector3.Left, Vector3.Up, Vector3.Down, Vector3.Back, Vector3.Forward];
	public override void _Process(double delta)
	{
		PlanetData.SurfaceShader.UpdateFrameDependentParameters();
		PlanetData.FramebufferShader.UpdateFrameDependentParameters();
	}

	public void SetupCameras()
	{
		MainCamera = (OrbitalCamera3D)CameraController.GetCamera("Main");
		CustomCamera helperCamera = CameraController.GetCamera("Helper");
		CustomCamera lookupCamera = CameraController.GetCamera("Lookup");


		helperCamera.Follow(MainCamera);
		lookupCamera.Follow(MainCamera);

		// MainCamera.DistanceFromTarget = PlanetData.Radius + 5;
		MainCamera.MinDistance = PlanetData.Radius + 0.999f;
		MainCamera.MaxDistance = PlanetData.Radius * 10f;

		MainCamera.GlobalPosition = Vector3.Back * MainCamera.DistanceFromTarget;
		CameraController.SetCurrent("Main");
	}

    public override void _Input(InputEvent @event)
    {
		if (@event is InputEventMouseButton mouseEvent)
		{
			if (mouseEvent.ButtonIndex == MouseButton.Left && mouseEvent.Pressed)
			{
				
			}
		}
    }

}
