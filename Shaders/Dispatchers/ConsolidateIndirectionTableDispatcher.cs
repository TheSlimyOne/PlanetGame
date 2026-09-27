using System;
using Uniform;
using Godot;
using PlanetGame.Planet.Rendering.VirtualTexturing;
using PlanetGame.Data;

namespace PlanetGame.Shaders.Dispatchers
{
	public class ConsolidateIndirectionTableDispatcher : Dispatcher<ConsolidateIndirectionTableDispatcher.BufferNames>
	{
		private static ShaderProgramPaths _shaderPath = new() { Compute = ShaderPaths.CONSOLIDATE_INDIRECTION_TABLE_COMPUTE };
    	private static VirtualTextureData VirtualTextureData => SaveManager.VirtualTextureData;
		
		public enum BufferNames
		{
			INDIRECTION_TABLE,
			CONSOLIDATED_INDIRECTION_TABLE,
			VIRTUAL_TEXTURE_DATA
		}

		private readonly SparseVirtualTexture _sparseVirtualTexture;

		public ConsolidateIndirectionTableDispatcher(SparseVirtualTexture sparseVirtualTexture) : base(_shaderPath)
		{
			_sparseVirtualTexture = sparseVirtualTexture;
			SetupShader();
		}

		public override void CreateUniforms()
		{
			_shaderUniforms = new System.Collections.Generic.Dictionary<Enum, ShaderUniform>()
			{
				[BufferNames.INDIRECTION_TABLE] = new Texture2DUniform(this, RenderingDevice, RenderingDevice.UniformType.Image,
					_sparseVirtualTexture.IndirectionTable.GetRdRid(),  perserved: true
				),

				[BufferNames.CONSOLIDATED_INDIRECTION_TABLE] = new Texture2DUniform(this, RenderingDevice, RenderingDevice.UniformType.Image,
					_sparseVirtualTexture.ConsolidatedIndirectionTable.GetRdRid(), perserved: true
				),

                [BufferNames.VIRTUAL_TEXTURE_DATA] = _sparseVirtualTexture.ResolveTileRequest[ResolveTileRequestDispatcher.BufferNames.VIRTUAL_TEXTURE_DATA]
			};
			CreateUniformSet();
		}

#nullable enable
		protected override void InvokeInternal(object[]? pushConstants = null)
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
#nullable disable

		public override void CleanupGPU()
		{
			base.CleanupGPU();
		}
    }
}
