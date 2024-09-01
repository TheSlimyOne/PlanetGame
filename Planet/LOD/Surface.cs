using Godot;
using Godot.Collections;
using System;
using System.Linq;
using ComputeShaderClasses;
using BufferClasses;

public partial class Surface : MultiMeshInstance3D
{
	[ExportGroup("Required")]
	[Export]private PlanetController _planetController;

	[ExportGroup("Shaders")]
	[Export(PropertyHint.File)] private string _computeCullShaderPath;
	[Export(PropertyHint.File)] private string _computeCopyShaderPath;

	ComputeCullShader _computeCullShader;
	ComputeCopyShader _computeCopyShader;
	private bool _processing;

	public override void _Ready()
	{
		ExtraCullMargin = 2 * _planetController.PlanetData.Radius;
		Multimesh = _planetController.PlanetData.GenerateMulitMesh();

		_planetController.PlanetData.SetMaterialParameters();

		_computeCullShader = new ComputeCullShader(_computeCullShaderPath, _planetController);
		_computeCopyShader = new ComputeCopyShader(_computeCopyShaderPath);

		_computeCullShader.SetComputeCopyShader(_computeCopyShader);
		_computeCopyShader.SetComputeCullShader(_computeCullShader);

		_computeCullShader.CreateUniforms();
		_computeCopyShader.CreateUniforms();

		Texture2Drd displayKeyData = _computeCullShader.GetUniform<Texture2DUniform>(ComputeCullShader.BufferNames.KEYS).GetTexture2Drd();
		Texture2Drd globalKeyData = _computeCullShader.GetUniform<Texture2DUniform>(ComputeCullShader.BufferNames.KEYS).GetTexture2Drd();

		_planetController.PlanetData.ShaderMaterial.SetShaderParameter("key_image", displayKeyData);
		_planetController.PlanetData.ShaderMaterial.SetShaderParameter("global_key_data", globalKeyData);
		_planetController.CameraController.GetChild(0).GetChild<TextureRect>(1).Texture = displayKeyData;
		_processing = true;
	}

	public override void _Notification(int what)
	{
		if (what == NotificationWMCloseRequest || what == NotificationPredelete)
		{
			_processing = false;
			_computeCullShader.CleanupGPU();
			_computeCopyShader.CleanupGPU();
		}

	}

	public override void _Input(InputEvent @event)
	{
		if (@event.IsActionPressed("step"))
		{
			_processing = !_processing;
		}
		if (@event.IsActionPressed("debug_mode"))
		{
			_planetController.PlanetData.DebugMode = !_planetController.PlanetData.DebugMode;
			_planetController.PlanetData.ShaderMaterial.SetShaderParameter("is_debug", _planetController.PlanetData.DebugMode);
		}
		if (@event.IsActionPressed("cube_mode"))
		{
			_planetController.PlanetData.CubeMode = _planetController.PlanetData.CubeMode;
			_planetController.PlanetData.ShaderMaterial.SetShaderParameter("is_cube", _planetController.PlanetData.CubeMode);
		}
	}



	

	public override void _PhysicsProcess(double delta)
	{
		if (_processing)
		{
			_computeCullShader.GetUniform<Texture2DUniform>(ComputeCullShader.BufferNames.KEYS).ClearTexture(Colors.Black);
			_computeCopyShader.Run();
			_computeCullShader.Run();
			Render();
			_computeCullShader.UpdateUniforms();
			// _processing = false;
		}
	}

	private void Render()
	{
		uint[] indices = _computeCullShader.GetUniformData<uint>(ComputeCullShader.BufferNames.INDICES);
		uint[] primCounts = _computeCullShader.GetUniformData<uint>(ComputeCullShader.BufferNames.ATOMIC_COUNTER);
		_planetController.CameraController.UIElements.SetCurrentLOD(_computeCullShader.GetUniform<Texture2DUniform>(ComputeCullShader.BufferNames.GLOBAL_KEYS_DATA).GetPixel(0, 0).R);

		int all = (int)primCounts[indices[1]];
		int culled = (int)primCounts[indices[1] + 16];

		_planetController.CameraController.UIElements.SetLabelTriangleCount(culled, all);


		// _processing = false;
		InstanceAllTriangles(culled);
		// InstanceAllTriangles(data, all);
	}

	public void InstanceAllTriangles(Key[] keys, int amount)
	{
		Multimesh.InstanceCount = amount;
		Transform3D transform = new(Basis.Identity, Vector3.Zero);
		for (int i = 0; i < amount; i++)
		{
			Multimesh.SetInstanceTransform(i, transform);
			Multimesh.SetInstanceCustomData(i, keys[i].ToColor());
		}
	}

	// public Color GetGlobalPixelData(int x, int y)
	// {
	// 	return RenderingServer.Texture2DGet(_globalKeyData.GetRid()).GetPixel(x, y);
	// }


	public void InstanceAllTriangles(int amount)
	{
		Multimesh.InstanceCount = amount;
		Transform3D transform = new(Basis.Identity, Vector3.Zero);
		for (int i = 0; i < amount; i++)
		{
			Multimesh.SetInstanceTransform(i, transform);
		}
	}
}
