using System;
using Godot;
using Godot.Collections;
using Uniform;

namespace Shader;

public partial class ComputeCopy : ComputeShader<ComputeCopy.BufferNames>
{

    public ComputeCull ComputeCullShader { get; set; }

    public enum BufferNames
	{
		ATOMIC_COUNTER,
		INDICES,
		DISPATCH_BUFFER,
		GLOBAL_KEYS_DATA,
	}

    public ComputeCopy(string shaderFilePath, ref RenderingDevice rd) : base(shaderFilePath, ref rd)
    {
        SetupComputeShader();
    }

    public void SetComputeCullShader(ComputeCull computeCullShader)
    {
        ComputeCullShader = computeCullShader;
    }

    public override void CreateUniforms()
    {
        _computeShaderUniforms = new System.Collections.Generic.Dictionary<BufferNames, ComputeShaderUniform>()
		{
			[BufferNames.ATOMIC_COUNTER] = ComputeCullShader.GetUniform(ComputeCull.BufferNames.ATOMIC_COUNTER),
			
			[BufferNames.INDICES] = ComputeCullShader.GetUniform(ComputeCull.BufferNames.INDICES),

            [BufferNames.DISPATCH_BUFFER] = new StorageBufferUniform(_rd, (int)BufferNames.DISPATCH_BUFFER,
				Utilities.ToBytes<uint>(new uint[] { 1, 1, 1 }).ToArray(), 1
			),

			[BufferNames.GLOBAL_KEYS_DATA] = ComputeCullShader.GetUniform(ComputeCull.BufferNames.GLOBAL_KEYS_DATA),
		};

        CreateUniformSet(); 
    }

    public override void Ready()
    {
        long computeList = _rd.ComputeListBegin();
        _rd.ComputeListBindComputePipeline(computeList, _pipeline);
        _rd.ComputeListBindUniformSet(computeList, _uniformSet, 0);
        _rd.ComputeListDispatch(computeList, 1, 1, 1);
        _rd.ComputeListEnd();
    }

    public override void UpdateUniforms()
    {
        throw new NotImplementedException();
    }

    
}
