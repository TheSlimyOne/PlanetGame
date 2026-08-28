using System;
using Uniform;
using Godot;
using PlanetGame.Util;
using PlanetGame.Planet;
using System.Collections.Generic;
using PlanetGame.Rendering.Surface;

namespace PlanetGame.Shaders.Dispatchers
{
	public class ExecuteTessellationPassDispatcher : Dispatcher<ExecuteTessellationPassDispatcher.BufferNames>
	{
		private static ShaderProgramPaths _shaderPath = new() { Compute = ShaderPaths.EXECUTE_TESSELLATION_PASS };
		private static TessellationData TessellationData => SaveManager.CurrentWorldSave.TessellationData;
		
		public enum BufferNames
		{
			ATOMIC_COUNTER,
			KEY_INDICES,
			READ_LIST,
			WRITE_FULL_LIST,
			WRITE_CULL_LIST,
			EXTERNAL_DATA,
			MULTIMESH_BUFFER,
			GLOBAL_KEYS_DATA,
		}

		private MultiMeshRD _triangleMultiMesh;
		private readonly Dictionary<PlanetRenderer.BufferNames, ShaderUniform> _shaderedShaderUniforms;

		public ExecuteTessellationPassDispatcher(MultiMeshRD triangleMultiMesh, Dictionary<PlanetRenderer.BufferNames, ShaderUniform> shaderedShaderUniforms) : base(_shaderPath)
		{
			_triangleMultiMesh = triangleMultiMesh;
			_shaderedShaderUniforms = shaderedShaderUniforms;
			SetupShader();

			_triangleMultiMesh.BuffersChanged += CreateUniformSet;
		}

		public override void CreateUniforms()
		{
			_shaderUniforms = [];
			
			_shaderUniforms[BufferNames.ATOMIC_COUNTER] = _shaderedShaderUniforms[PlanetRenderer.BufferNames.EXEC_ATOMIC_COUNTER];

			_shaderUniforms[BufferNames.KEY_INDICES] = _shaderedShaderUniforms[PlanetRenderer.BufferNames.EXEC_KEY_INDICES];

			_shaderUniforms[BufferNames.READ_LIST] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.READ_LIST,
				CreateReadList()
			);

			_shaderUniforms[BufferNames.WRITE_FULL_LIST] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.WRITE_FULL_LIST,
				[.. Utilities.ToBytes<Key>(TessellationData.MaximumKeys)]
			);

			_shaderUniforms[BufferNames.WRITE_CULL_LIST] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.WRITE_CULL_LIST,
				[.. Utilities.ToBytes<Key>(TessellationData.MaximumKeys)]
			);

			_shaderUniforms[BufferNames.EXTERNAL_DATA] = _shaderedShaderUniforms[PlanetRenderer.BufferNames.EXTERNAL_DATA];

			_shaderUniforms[BufferNames.MULTIMESH_BUFFER] = _triangleMultiMesh.BufferUniform;

			_shaderUniforms[BufferNames.GLOBAL_KEYS_DATA] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.GLOBAL_KEYS_DATA,
				GetInitialGlobalKeyData()
			);
		
			CreateUniformSet();
		}

#nullable enable
		public override void Invoke(byte[]? pushConstants = null)
		{
			long computeList = RenderingDevice.ComputeListBegin();
			RenderingDevice.ComputeListBindComputePipeline(computeList, _pipeline);
			RenderingDevice.ComputeListBindUniformSet(computeList, _uniformSet, 0);
			
			if (pushConstants != null)
				RenderingDevice.ComputeListSetPushConstant(computeList, pushConstants, (uint)pushConstants.Length);

			RenderingDevice.ComputeListAddBarrier(computeList);
			RenderingDevice.ComputeListDispatchIndirect(computeList, _shaderedShaderUniforms[PlanetRenderer.BufferNames.EXEC_DISPATCH_BUFFER].Rid, 0);
			RenderingDevice.ComputeListEnd();
		}

		public override void UpdateUniforms()
		{
			_shaderUniforms[BufferNames.READ_LIST].UpdateUniform(
				_shaderUniforms[BufferNames.WRITE_FULL_LIST].GetByteData()[0]
			);
		}
		
		private static byte[] GetInitialGlobalKeyData()
		{
			return [.. Utilities.ToBytes([
				3u,
				uint.MaxValue,
				0u,
				.. new uint[32]
			])];
		}

		public void ResetGlobalKeyData()
		{
			GetUniform<StorageBufferUniform>(BufferNames.GLOBAL_KEYS_DATA).UpdateUniform(GetInitialGlobalKeyData());
		}

		public (int fullPrimCount, int culledPrimCount, int renderedPrimCount) GetPrimitiveCounts()
		{
			uint[] indices = GetUniform<StorageBufferUniform>(BufferNames.KEY_INDICES).GetData<uint>();
			uint[] primCounts = GetUniform<StorageBufferUniform>(BufferNames.ATOMIC_COUNTER).GetData<uint>();

			return ((int)primCounts[indices[0]], (int)primCounts[indices[0] + 3], (int)primCounts[indices[0] + 6]);
		}

		public (int MaxLod, int MinLod, int StableCount, int[] LodCount) GetGlobalKeyData()
		{
			uint[] data = GetUniform<StorageBufferUniform>(BufferNames.GLOBAL_KEYS_DATA).GetData<uint>();

			int[] lodCount = new int[TessellationData.MaximumLod + 1];

			for (uint i = TessellationData.MinimumLod; i <= TessellationData.MaximumLod; i++)
				lodCount[i] = (int)data[3 + i];

			return (
				(int)data[0],
				(int)data[1],
				(int)data[2],
				lodCount
			);
		}

		private byte[] CreateReadList()
		{
			Key[] readList = new Key[TessellationData.MaximumKeys];

			for (int i = 0; i < 6; i++)
			{
				Key[] faceData = Key.GenerateFullFace((int)TessellationData.StartingLod, i);
				Array.Copy(faceData, 0, readList, i * faceData.Length, faceData.Length);
			}

			return [.. Utilities.ToBytes<Key>(readList)];
		}

        public override void CleanupGPU()
        {
			_triangleMultiMesh.BuffersChanged -= CreateUniformSet;
            base.CleanupGPU();
        }
    }
}