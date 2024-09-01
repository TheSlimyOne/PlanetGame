using System;
using Godot;
using Godot.Collections;
using ComputeShaderClasses;

namespace BufferClasses;

public partial class ComputeCopyShader : ComputeShader<ComputeCopyShader.BufferNames>
{

    private ComputeCullShader _computeCullShader;

    public enum BufferNames
	{
		ATOMIC_COUNTER,
		INDICES,
		DISPATCH_BUFFER,
		GLOBAL_KEYS_DATA,
	}

    public ComputeCopyShader(string shaderFilePath, RenderingDevice rd = null) : base(shaderFilePath, rd)
    {
        SetupComputeShader();
    }

    public void SetComputeCullShader(ComputeCullShader computeCullShader)
    {
        _computeCullShader = computeCullShader;
    }

    public override void CreateUniforms()
    {
        _computeShaderUniforms = new System.Collections.Generic.Dictionary<BufferNames, ComputeShaderUniform>()
		{
			[BufferNames.ATOMIC_COUNTER] = _computeCullShader.GetUniform(ComputeCullShader.BufferNames.ATOMIC_COUNTER),
			
			[BufferNames.INDICES] = _computeCullShader.GetUniform(ComputeCullShader.BufferNames.INDICES),

            [BufferNames.DISPATCH_BUFFER] = new StorageBufferUniform(_rd, (int)BufferNames.DISPATCH_BUFFER,
				Utilities.ToBytes<uint>(new uint[] { 1, 1, 1 }).ToArray(), 1
			),

			[BufferNames.GLOBAL_KEYS_DATA] = _computeCullShader.GetUniform(ComputeCullShader.BufferNames.GLOBAL_KEYS_DATA),
		};

        foreach(ComputeShaderUniform computeShaderUniform in _computeShaderUniforms.Values)
        {
            _bindings.Add(computeShaderUniform.Uniform);
        }

        _uniformSet = _rd.UniformSetCreate(_bindings, _shader, 0);    
    }

    public override void Run()
    {
        long computeList = _rd.ComputeListBegin();
        _rd.ComputeListBindComputePipeline(computeList, _pipeline);
        _rd.ComputeListBindUniformSet(computeList, _uniformSet, 0);
        _rd.ComputeListDispatch(computeList, 1, 1, 1);
        _rd.ComputeListEnd();
        SubmitThenSync();
    }

    public override void UpdateUniforms()
    {
        throw new NotImplementedException();
    }

    
}
