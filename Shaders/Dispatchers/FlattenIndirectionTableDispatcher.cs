using System;
using Uniform;
using Godot;
using Godot.Collections;
using PlanetGame.Util;
using PlanetGame.Rendering.VirtualTexturing;

namespace PlanetGame.Shaders.Dispatchers
{
	public class FlattenIndirectionTableDispatcher : Dispatcher<FlattenIndirectionTableDispatcher.BufferNames>
	{
		public SparseVirtualTexture SparseVirtualTexture { get; set; }
		public enum BufferNames
		{
			INDIRECTION_TABLE,
			CONSOLIDATED_INDIRECTION_TABLE,
			VIRTUAL_TEXTURE_DATA
		}

		public FlattenIndirectionTableDispatcher() : base(new() { Compute = ShaderPaths.FLATTEN_INDIRECTION_TABLE })
		{
			SetupShader();
		}

		public override void CreateUniforms()
		{
			_computeShaderUniforms = new System.Collections.Generic.Dictionary<Enum, ShaderUniform>()
			{
				[BufferNames.INDIRECTION_TABLE] = new Texture2DUniform(this, (int)BufferNames.INDIRECTION_TABLE,
					SparseVirtualTexture.IndirectionTable.GetRdRid(), RenderingDevice.UniformType.Image, perserved: true
				),

				[BufferNames.CONSOLIDATED_INDIRECTION_TABLE] = new Texture2DUniform(this, (int)BufferNames.CONSOLIDATED_INDIRECTION_TABLE,
					SparseVirtualTexture.ConsolidatedIndirectionTable.GetRdRid(), RenderingDevice.UniformType.Image, perserved: true
				),

                [BufferNames.VIRTUAL_TEXTURE_DATA] = SparseVirtualTexture.ResolveTileRequest[ResolveTileRequestDispatcher.BufferNames.VIRTUAL_TEXTURE_DATA]
			};
			CreateUniformSet();
		}

#nullable enable
		public override void Invoke(byte[]? pushConstants = null)
		{
			uint gridSize = SparseVirtualTexture.VirtualTextureData.GridSize;
			uint groupCount = (gridSize + 7) / 8;
			
			long computeList = RenderingDevice.ComputeListBegin();
			RenderingDevice.ComputeListBindComputePipeline(computeList, _pipeline);
			RenderingDevice.ComputeListBindUniformSet(computeList, _uniformSet, 0);
			RenderingDevice.ComputeListAddBarrier(computeList);
			RenderingDevice.ComputeListDispatch(computeList, groupCount, groupCount, 6);
			RenderingDevice.ComputeListEnd();
		}
	}
}
