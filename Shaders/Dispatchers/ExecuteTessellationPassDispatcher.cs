using System;
using Uniform;
using PlanetGame.Util;
using System.Collections.Generic;
using PlanetGame.Data;
using PlanetGame.Planet.Rendering;

using static PlanetGame.Shaders.Dispatchers.ExecuteTessellationPassDispatcher.BufferNames;
using static PlanetGame.Shaders.Dispatchers.ExecuteTessellationPassDispatcher.BufferSets;

namespace PlanetGame.Shaders.Dispatchers
{
	public class ExecuteTessellationPassDispatcher : Dispatcher<ExecuteTessellationPassDispatcher.BufferNames, ExecuteTessellationPassDispatcher.BufferSets>
	{
		private static ShaderProgramPaths _shaderPath = new() { Compute = ShaderPaths.EXECUTE_TESSELLATION_COMPUTE };
		private static TessellationData TessellationData => SaveManager.TessellationData;
		
		public enum BufferSets
		{
			DEFAULT = 0,
			MESH = 1,
		}

		public enum BufferNames
		{
			ATOMIC_COUNTER,
			KEY_INDICES,
			READ_LIST,
			WRITE_FULL_LIST,
			WRITE_CULL_LIST,
			TESSELLATION_DATA,
			WORLD_DATA,
			RENDER_DATA,
			MULTIMESH_BUFFER,
			GLOBAL_KEYS_DATA,
		}

		private MultiMeshRD _triangleMultiMesh;
		private readonly Dictionary<PlanetRenderer.BufferNames, ShaderUniform> _sharedShaderUniforms;
		private Action _onMeshChange;

		public ExecuteTessellationPassDispatcher(MultiMeshRD triangleMultiMesh, Dictionary<PlanetRenderer.BufferNames, ShaderUniform> sharedShaderUniforms) : base(_shaderPath)
		{
			_triangleMultiMesh = triangleMultiMesh;
			_sharedShaderUniforms = sharedShaderUniforms;
			SetupShader();

			_onMeshChange = () => CreateUniformSet(MESH);
            _triangleMultiMesh.BuffersChanged += _onMeshChange;
		}

		public override void CreateUniforms()
		{			
			this[0, ATOMIC_COUNTER, DEFAULT] = _sharedShaderUniforms[PlanetRenderer.BufferNames.EXEC_ATOMIC_COUNTER];

			this[1, KEY_INDICES, DEFAULT] = _sharedShaderUniforms[PlanetRenderer.BufferNames.EXEC_KEY_INDICES];

			this[2, READ_LIST, DEFAULT] = new StorageBufferUniform(READ_LIST.ToString(), this, RenderingDevice,
				CreateReadList()
			);

			this[3, WRITE_FULL_LIST, DEFAULT] = new StorageBufferUniform(WRITE_FULL_LIST.ToString(), this, RenderingDevice,
				[.. Utilities.ToBytes<Key>(TessellationData.MaximumKeys)]
			);

			this[4, WRITE_CULL_LIST, DEFAULT] = new StorageBufferUniform(WRITE_CULL_LIST.ToString(), this, RenderingDevice,
				[.. Utilities.ToBytes<Key>(TessellationData.MaximumKeys)]
			);

			this[5, TESSELLATION_DATA, DEFAULT] = _sharedShaderUniforms[PlanetRenderer.BufferNames.TESSELLATION_DATA];

			this[6, WORLD_DATA, DEFAULT] = _sharedShaderUniforms[PlanetRenderer.BufferNames.WORLD_DATA];
			
			this[7, RENDER_DATA, DEFAULT] = _sharedShaderUniforms[PlanetRenderer.BufferNames.RENDER_DATA];

			this[8, GLOBAL_KEYS_DATA, DEFAULT] = new StorageBufferUniform(GLOBAL_KEYS_DATA.ToString(), this, RenderingDevice,
				GetInitialGlobalKeyData()
			);

			this[0, MULTIMESH_BUFFER, MESH] = _triangleMultiMesh.BufferUniform;
		
			CreateUniformSets();
		}

#nullable enable
		protected override void InvokeInternal(object[]? pushConstants = null)
		{
			if (pushConstants == null)
				throw new("Push constants are required");
			Span<byte> pushConstantBytes = Utilities.CollectionToBytes(pushConstants);

			long computeList = RenderingDevice.ComputeListBegin();
			RenderingDevice.ComputeListBindComputePipeline(computeList, _pipeline);
            ComputeListBindUniformSets(computeList);
			RenderingDevice.ComputeListSetPushConstant(computeList, pushConstantBytes, (uint)pushConstantBytes.Length);
			RenderingDevice.ComputeListAddBarrier(computeList);
			RenderingDevice.ComputeListDispatchIndirect(computeList, _sharedShaderUniforms[PlanetRenderer.BufferNames.EXEC_DISPATCH_BUFFER].Rid, 0);
			RenderingDevice.ComputeListEnd();
		}
#nullable disable

		public override void UpdateUniforms()
		{
			this[READ_LIST].UpdateUniform(this[WRITE_FULL_LIST].GetByteData()[0]);
		}
		
		private static byte[] GetInitialGlobalKeyData()
		{
			return [.. Utilities.ToBytes([
				0u,
				uint.MaxValue,
				0u,
				.. new uint[32]
			])];
		}

		public void ResetGlobalKeyData()
		{
			GetUniform<StorageBufferUniform>(GLOBAL_KEYS_DATA).UpdateUniform(GetInitialGlobalKeyData());
		}

		public (int fullPrimCount, int culledPrimCount, int renderedPrimCount) GetPrimitiveCounts()
		{
			uint[] indices = GetUniform<StorageBufferUniform>(KEY_INDICES).GetData<uint>();
			uint[] primCounts = GetUniform<StorageBufferUniform>(ATOMIC_COUNTER).GetData<uint>();

			return ((int)primCounts[indices[0]], (int)primCounts[indices[0] + 3], (int)primCounts[indices[0] + 6]);
		}

		public (int MaxLod, int MinLod, int StableCount, int[] LodCount) GetGlobalKeyData()
		{
			uint[] data = GetUniform<StorageBufferUniform>(GLOBAL_KEYS_DATA).GetData<uint>();

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
				Key[] faceData = Key.GenerateFullFace((int)PlanetRenderer.STARTING_LOD, i);
				Array.Copy(faceData, 0, readList, i * faceData.Length, faceData.Length);
			}

			return [.. Utilities.ToBytes(readList)];
		}

        protected override void CleanupGPUInternal()
        {
            _triangleMultiMesh.BuffersChanged -= _onMeshChange;
			base.CleanupGPUInternal();

        }
    }
}