using System;
using System.Linq;
using Uniform;
using Godot;
using Godot.Collections;
using Planet;
namespace Dispatcher
{
    public class ValidateCacheDispatcher : ComputeShaderDispatcher<ValidateCacheDispatcher.BufferNames>
    {
    	public SparseVirtualTexture SparseVirtualTexture;

    	public enum BufferNames
    	{
			INDIRECTION_TABLE,
			RESIDENCY_TABLE,
			INDIRECTION_TABLE_DATA,
    	}

    	public ValidateCacheDispatcher(string shaderFilePath) : base(shaderFilePath)
    	{
    		SetupComputeShader();
    	}

    	public override void CreateUniforms()
    	{
    		_computeShaderUniforms = new System.Collections.Generic.Dictionary<Enum, ComputeShaderUniform>()
    		{
    			[BufferNames.INDIRECTION_TABLE] = new Texture2DUniform(this, (int)BufferNames.INDIRECTION_TABLE,
                    SparseVirtualTexture.IndirectionTable.Table.TextureRdRid, RenderingDevice.UniformType.Image
                ),

    			[BufferNames.RESIDENCY_TABLE] = new Texture2DUniform(this, (int)BufferNames.RESIDENCY_TABLE,
                    SparseVirtualTexture.ResidencyTable.Table.TextureRdRid, RenderingDevice.UniformType.Image
                ),

				[BufferNames.INDIRECTION_TABLE_DATA] = SparseVirtualTexture.ReadFramebuffer.GetUniform(ReadFramebufferDispatcher.BufferNames.INDIRECTION_TABLE_DATA),
            };

    		CreateUniformSet();
    	}

    	public override void Invoke()
    	{
            uint gridSize = SparseVirtualTexture.ResidencyTable.GridSize;
    		long computeList = _RenderingDevice.ComputeListBegin();
    		_RenderingDevice.ComputeListBindComputePipeline(computeList, _pipeline);
    		_RenderingDevice.ComputeListBindUniformSet(computeList, _uniformSet, 0);
			_RenderingDevice.ComputeListAddBarrier(computeList);
    		_RenderingDevice.ComputeListDispatch(computeList, gridSize, gridSize, 1);
    		_RenderingDevice.ComputeListEnd();
    	}

        public override void UpdateUniforms()
        {
            throw new NotImplementedException();
        }
    }

}