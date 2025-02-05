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

    	public UpdateIndirectionTableDispatcher(string shaderFilePath, RenderingDevice rd) : base(shaderFilePath, rd)
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

    	public override void Invoke()
    	{
    		long computeList = _RenderingDevice.ComputeListBegin();
    		_RenderingDevice.ComputeListBindComputePipeline(computeList, _pipeline);
    		_RenderingDevice.ComputeListBindUniformSet(computeList, _uniformSet, 0);
			_RenderingDevice.ComputeListAddBarrier(computeList);
    		_RenderingDevice.ComputeListDispatch(computeList, 1, 1, 1);
    		_RenderingDevice.ComputeListEnd();
    	}

        public override void UpdateUniforms()
        {
            throw new NotImplementedException();
        }
    }

}