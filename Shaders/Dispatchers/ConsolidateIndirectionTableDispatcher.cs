using System;
using Uniform;
using Godot;
using PlanetGame.Planet.Rendering.VirtualTexturing;
using PlanetGame.Data;
using System.Collections.Generic;
using PlanetGame.Planet.Rendering;
using static PlanetGame.Shaders.Dispatchers.ConsolidateIndirectionTableDispatcher.BufferNames;
using static PlanetGame.Shaders.Dispatchers.ConsolidateIndirectionTableDispatcher.BufferSets;

namespace PlanetGame.Shaders.Dispatchers
{
	public class ConsolidateIndirectionTableDispatcher : Dispatcher<ConsolidateIndirectionTableDispatcher.BufferNames, ConsolidateIndirectionTableDispatcher.BufferSets>
	{
		private static ShaderProgramPaths _shaderPath = new() { Compute = ShaderPaths.CONSOLIDATE_INDIRECTION_TABLE_COMPUTE };
    	private static VirtualTextureData VirtualTextureData => SaveManager.VirtualTextureData;
		private readonly Dictionary<PlanetRenderer.BufferNames, ShaderUniform> _sharedShaderUniforms;

		public enum BufferSets
		{
			DEFAULT
		}
		
		public enum BufferNames
		{
			INDIRECTION_TABLE,
			CONSOLIDATED_INDIRECTION_TABLE,
			VIRTUAL_TEXTURE_DATA
		}

		private readonly SparseVirtualTexture _sparseVirtualTexture;

		public ConsolidateIndirectionTableDispatcher(SparseVirtualTexture sparseVirtualTexture, Dictionary<PlanetRenderer.BufferNames, ShaderUniform> sharedShaderUniforms) : base(_shaderPath)
		{
			_sharedShaderUniforms = sharedShaderUniforms;
			_sparseVirtualTexture = sparseVirtualTexture;
			SetupShader();
		}

		public override void CreateUniforms()
		{			
			this[0, INDIRECTION_TABLE, DEFAULT] = _sparseVirtualTexture.IndirectionTable;

			this[1, CONSOLIDATED_INDIRECTION_TABLE, DEFAULT] = _sparseVirtualTexture.ConsolidatedIndirectionTable;

			this[2, VIRTUAL_TEXTURE_DATA, DEFAULT] = _sharedShaderUniforms[PlanetRenderer.BufferNames.VIRTUAL_TEXTURE_DATA];
		
			CreateUniformSets();
		}

#nullable enable
		protected override void InvokeInternal(object[]? pushConstants = null)
		{
			uint gridSize = VirtualTextureData.BaseGridSize;
			uint groupCount = (gridSize + 7) / 8;
			
			long computeList = RenderingDevice.ComputeListBegin();
			RenderingDevice.ComputeListBindComputePipeline(computeList, _pipeline);
            ComputeListBindUniformSets(computeList);
			RenderingDevice.ComputeListAddBarrier(computeList);
			RenderingDevice.ComputeListDispatch(computeList, groupCount, groupCount, 6);
			RenderingDevice.ComputeListEnd();
		}
#nullable disable
    }
}
