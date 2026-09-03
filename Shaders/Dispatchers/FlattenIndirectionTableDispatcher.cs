using System;
using Uniform;
using Godot;
using PlanetGame.Rendering.VirtualTexturing;

namespace PlanetGame.Shaders.Dispatchers
{
	public class FlattenIndirectionTableDispatcher : Dispatcher<FlattenIndirectionTableDispatcher.BufferNames>
	{
		private static ShaderProgramPaths _shaderPath = new() { Compute = ShaderPaths.FLATTEN_INDIRECTION_TABLE };
    	private static VirtualTextureData VirtualTextureData => SaveManager.CurrentWorldSave.VirtualTextureData;
		
		public enum BufferNames
		{
			INDIRECTION_TABLE,
			CONSOLIDATED_INDIRECTION_TABLE,
			VIRTUAL_TEXTURE_DATA
		}

		private readonly SparseVirtualTexture _sparseVirtualTexture;

		public FlattenIndirectionTableDispatcher(SparseVirtualTexture sparseVirtualTexture) : base(_shaderPath)
		{
			_sparseVirtualTexture = sparseVirtualTexture;
			SetupShader();
		}

		public override void CreateUniforms()
		{
			_shaderUniforms = new System.Collections.Generic.Dictionary<Enum, ShaderUniform>()
			{
				[BufferNames.INDIRECTION_TABLE] = new Texture2DUniform(this, (int)BufferNames.INDIRECTION_TABLE,
					_sparseVirtualTexture.IndirectionTable.GetRdRid(), RenderingDevice.UniformType.Image, perserved: true
				),

				[BufferNames.CONSOLIDATED_INDIRECTION_TABLE] = new Texture2DUniform(this, (int)BufferNames.CONSOLIDATED_INDIRECTION_TABLE,
					_sparseVirtualTexture.ConsolidatedIndirectionTable.GetRdRid(), RenderingDevice.UniformType.Image, perserved: true
				),

                [BufferNames.VIRTUAL_TEXTURE_DATA] = _sparseVirtualTexture.ResolveTileRequest[ResolveTileRequestDispatcher.BufferNames.VIRTUAL_TEXTURE_DATA]
			};
			CreateUniformSet();
		}

#nullable enable
		public override void Invoke(byte[]? pushConstants = null)
		{
			uint gridSize = VirtualTextureData.BaseGridSize;
			uint groupCount = (gridSize + 7) / 8;
			
			long computeList = RenderingDevice.ComputeListBegin();
			RenderingDevice.ComputeListBindComputePipeline(computeList, _pipeline);
			RenderingDevice.ComputeListBindUniformSet(computeList, _uniformSet, 0);
			RenderingDevice.ComputeListAddBarrier(computeList);
			RenderingDevice.ComputeListDispatch(computeList, groupCount, groupCount, 6);
			RenderingDevice.ComputeListEnd();
		}

		public override void CleanupGPU()
		{
			base.CleanupGPU();
		}
    }
}
