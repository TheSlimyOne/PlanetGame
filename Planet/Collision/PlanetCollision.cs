using Godot;
using Godot.NativeInterop;
using System;
using System.Runtime.InteropServices;
[Tool]

public partial class PlanetCollision : CollisionShape3D
{
	private int _resolution;
	private CompressedTexture2D _heightMap;

	
	public void Initalize(int resolution, CompressedTexture2D heightMap)
	{
		_resolution = resolution;
		_heightMap = heightMap;
	}

	public static void CreateCollisionChunk(int subdivisionLevel, Vector3 normal)
	{
	
	
		// RenderingDevice renderingDevice = RenderingServer.CreateLocalRenderingDevice();
		// RDShaderFile shaderFile = GD.Load<RDShaderFile>("res://Planet/Collision/computeCollision.glsl");
		// RDShaderSpirV shaderBytecode = shaderFile.GetSpirV();
		// Rid shaderRID = renderingDevice.ShaderCreateFromSpirV(shaderBytecode);

		
		// Godot.Collections.Array<Vector3> test2 = new Godot.Collections.Array<Vector3>();
		

		// Rid storageBufferRID = renderingDevice.StorageBufferCreate((uint)floats.Length, floats);
		// Godot.Collections.Array<RDUniform> uniforms = new Godot.Collections.Array<RDUniform>();
        // RDUniform uniform = new RDUniform
        // {
        //     UniformType = RenderingDevice.UniformType.StorageBuffer,
        //     Binding = 0
        // };
		// uniform.AddId(storageBufferRID);
		// uniforms.Add(uniform);

		// Rid uniformSetRID = renderingDevice.UniformSetCreate(uniforms, shaderRID, 0);

        // Rid pipeLineRID = renderingDevice.ComputePipelineCreate(shaderRID);
		// long computeList = renderingDevice.ComputeListBegin();

		// renderingDevice.ComputeListBindComputePipeline(computeList, pipeLineRID);
		// renderingDevice.ComputeListBindUniformSet(computeList, uniformSetRID, 0);
		// renderingDevice.ComputeListDispatch(computeList, 1, 1, 1);
		// renderingDevice.ComputeListEnd();

		// renderingDevice.Submit();
		// renderingDevice.Sync();

		// float[] outputFloat = Utilities.ByteToFloatToArray(renderingDevice.BufferGetData(storageBufferRID));

		// for(int i = 0; i < outputFloat.Length; i++)
		// {
		// 	GD.Print(outputFloat[i]);
		// }
	}

	
}
