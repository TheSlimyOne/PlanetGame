using System;
using System.Linq;
using Uniform;
using Godot;
using Godot.Collections;
using Planet;
namespace Dispatcher
{
    public class UpdateIndirectionTableDispatcher : ComputeShaderDispatcher<UpdateIndirectionTableDispatcher.BufferNames>
    {
    	public PlanetController PlanetController { get; set; }
		public RenderSurfaceDispatcher RenderSurfaceDispatcher { get; set; }

    	public enum BufferNames
    	{
			INDIRECTION_TEXTURE,
			EXTERNAL_DATA,

    	}

    	public UpdateIndirectionTableDispatcher(string shaderFilePath, ref RenderingDevice rd) : base(shaderFilePath, ref rd)
    	{
    		SetupComputeShader();
    	}

    	public override void CreateUniforms()
    	{
    		_computeShaderUniforms = new System.Collections.Generic.Dictionary<Enum, ComputeShaderUniform>()
    		{
    			
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