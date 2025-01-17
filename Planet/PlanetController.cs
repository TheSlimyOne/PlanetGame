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
	[Export] public UIController UIController { get; private set; }
	public OrbitalCamera3D MainCamera { get; private set; }
	

	float radius = 25;
	public void InsertSphereAt(Vector3 position, Color color, bool attachToPlanet = true)
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
		mesh.GlobalPosition = SurfaceAttachment.GlobalPosition - position;
	}

	public async override void _Ready()
	{
		SetupCameras();
		PlanetData.Scaled(Vector3.One * PlanetData.Radius);
		PlanetData.Translate(Vector3.Back * (1 - PlanetData.Radius));
		PlanetData.InitializeVirtualTextures(this);
		PlanetData.SurfaceShaderBindParameters(CameraController.GetCamera("Main"), CameraController.GetCamera("Helper"));
		PlanetData.IndirectShaderBindParameters(CameraController.GetCamera("Main"), CameraController.GetCamera("Helper"));
		PlanetData.UpdateShaderParameters();

		SurfaceController.InvokeComputeShaders();
		SurfaceController.Processing = true;
		Rid mainInstance = SurfaceController.CreateMultimeshInstance(
			Transform3D.Identity, GetWorld3D().Scenario, 2 * PlanetData.Radius, 0b1u
		);
		RenderingServer.InstanceGeometrySetMaterialOverride(mainInstance, PlanetData.FramebufferShader.GetRid());

		CustomCamera lookupViewport = CameraController.GetCamera("Lookup");

		Rid lookupInstance = SurfaceController.CreateMultimeshInstance(
			Transform3D.Identity, lookupViewport.GetWorld3D().Scenario, 2 * PlanetData.Radius, 0b1u
		);
		RenderingServer.InstanceGeometrySetMaterialOverride(lookupInstance, PlanetData.FramebufferShader.GetRid());
	}

	public override void _Process(double delta)
	{
		PlanetData.SurfaceShader.UpdateFrameDependentParameters();
		PlanetData.FramebufferShader.UpdateFrameDependentParameters();
		Image image = CameraController.GetCamera("Lookup").GetViewport().GetTexture().GetImage();
		Vector2 mousePosition = MainCamera.GetViewport().GetMousePosition();
		Vector2 windowSize = DisplayServer.WindowGetSize();

		Vector2 normalizedMousePosition = mousePosition / (windowSize - Vector2.One);

	
		normalizedMousePosition = normalizedMousePosition.Clamp(0, 1);
		

		normalizedMousePosition *= image.GetSize() - Vector2.One;
		ColorRect colorRect = GetNode<ColorRect>("UIController/FramebufferDataVisualizer");
		Color color = image.GetPixelv((Vector2I)normalizedMousePosition);


		colorRect.Color = color;

		int packed = Mathf.RoundToInt(color.B * 255);
		int grid = (packed >> 3) & 0x1f;
		int key = packed & 0x07;

        Vector2I index = new(
            (int)(color.R * Mathf.Pow(2, PlanetData.IndirectionTable.MipDepth - grid)),
            (int)(color.G * Mathf.Pow(2, PlanetData.IndirectionTable.MipDepth - grid))
		);

        // GetNode<VBoxContainer>("CanvasLayer/ColorRect/ColorContainer").Modulate = color.Inverted();
        GetNode<Label>("UIController/FramebufferDataVisualizer/ColorContainer/RValue").Text = $"R: {color.R}";
		GetNode<Label>("UIController/FramebufferDataVisualizer/ColorContainer/GValue").Text = $"G: {color.G}";
		GetNode<Label>("UIController/FramebufferDataVisualizer/ColorContainer/BValue").Text = $"B: {color.B}";
		GetNode<Label>("UIController/FramebufferDataVisualizer/ColorContainer/TextureCoords").Text = $"INDEX: ({index.X}, {index.Y}, {grid})";
		// GetNode<Label>("CanvasLayer/ColorRect/ColorContainer/UnpackedB").Text = $"ID {key}, GRID {grid}";




	}
	
	public void SetupCameras()
	{
		MainCamera = (OrbitalCamera3D)CameraController.GetCamera("Main");
		CustomCamera helperCamera = CameraController.GetCamera("Helper");
		CustomCamera lookupCamera = CameraController.GetCamera("Lookup");


		helperCamera.Follow(MainCamera);
		lookupCamera.Follow(MainCamera);

		MainCamera.DistanceFromTarget = PlanetData.Radius + 5;
		MainCamera.MinDistance = PlanetData.Radius + 0.999f;
		MainCamera.MaxDistance = PlanetData.Radius * 10f;

		MainCamera.GlobalPosition = Vector3.Back * MainCamera.DistanceFromTarget;
		CameraController.SetCurrent("Main");
	}

}
