using System;
using System.Linq;
using Uniform;
using Godot;
using Godot.Collections;
using Planet;
namespace Dispatcher
{
	public partial class UpdateCommandBufferDispatcher : ComputeShaderDispatcher<UpdateCommandBufferDispatcher.BufferNames>
	{
		public PlanetController PlanetController { get; set; }
		public RenderSurfaceDispatcher RenderSurfaceDispatcher { get; set; }
		public CopyKeysDispatcher CopyKeysDispatcher { get; set; }

		public enum BufferNames
		{
			MULTIMESH_COMMAND_BUFFER,
			ATOMIC_COUNTER,
			INDICES,
			DEBUG_DATA
		}

		public UpdateCommandBufferDispatcher(string shaderFilePath, ref RenderingDevice rd) : base(shaderFilePath, ref rd)
		{
			SetupComputeShader();
		}

		public override void CreateUniforms()
		{
			_computeShaderUniforms = new System.Collections.Generic.Dictionary<Enum, ComputeShaderUniform>()
			{
				[BufferNames.MULTIMESH_COMMAND_BUFFER] = new MultimeshUniform(
					this, 
					RenderSurfaceDispatcher.GetUniform<MultimeshUniform>(RenderSurfaceDispatcher.BufferNames.MULTIMESH_BUFFER).Parameters, 
					(int)BufferNames.MULTIMESH_COMMAND_BUFFER, 
					true),
				[BufferNames.ATOMIC_COUNTER] = CopyKeysDispatcher.GetUniform(CopyKeysDispatcher.BufferNames.ATOMIC_COUNTER),
				[BufferNames.INDICES] = CopyKeysDispatcher.GetUniform(CopyKeysDispatcher.BufferNames.INDICES),
				[BufferNames.DEBUG_DATA] = RenderSurfaceDispatcher.GetUniform(RenderSurfaceDispatcher.BufferNames.DEBUG_DATA),

			};

			CreateUniformSet();
		}

		public override void Ready()
		{
			long computeList = _rd.ComputeListBegin();
			_rd.ComputeListBindComputePipeline(computeList, _pipeline);
			_rd.ComputeListBindUniformSet(computeList, _uniformSet, 0);
			_rd.ComputeListAddBarrier(computeList);
			_rd.ComputeListDispatch(computeList, 1, 1, 1);
			_rd.ComputeListEnd();
		}

		public override void UpdateUniforms()
		{
			throw new NotImplementedException();
		}
	}

}