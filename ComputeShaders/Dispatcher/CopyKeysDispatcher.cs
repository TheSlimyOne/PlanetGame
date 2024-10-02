using System;
using Godot;
using Godot.Collections;
using Uniform;

namespace Dispatcher
{
    public partial class CopyKeysDispatcher : ComputeShaderDispatcher<CopyKeysDispatcher.BufferNames>
    {

        public CalculateSurfaceDispatcher ComputeCullShader { get; set; }

        public enum BufferNames
    	{
    		ATOMIC_COUNTER,
    		INDICES,
    		DISPATCH_BUFFER,
    		GLOBAL_KEYS_DATA,
    	}

        public CopyKeysDispatcher(string shaderFilePath, ref RenderingDevice rd) : base(shaderFilePath, ref rd)
        {
            SetupComputeShader();
        }

        public void SetComputeCullShader(CalculateSurfaceDispatcher computeCullShader)
        {
            ComputeCullShader = computeCullShader;
        }

        public override void CreateUniforms()
        {
            _computeShaderUniforms = new System.Collections.Generic.Dictionary<Enum, ComputeShaderUniform>()
    		{
    			[BufferNames.ATOMIC_COUNTER] = ComputeCullShader.GetUniform(CalculateSurfaceDispatcher.BufferNames.ATOMIC_COUNTER),
			
    			[BufferNames.INDICES] = ComputeCullShader.GetUniform(CalculateSurfaceDispatcher.BufferNames.INDICES),

                [BufferNames.DISPATCH_BUFFER] = new StorageBufferUniform(this, _rd, (int)BufferNames.DISPATCH_BUFFER,
    				Utilities.ToBytes<uint>(new uint[] { 1, 1, 1 }).ToArray(), indirect: 1
    			),

    			[BufferNames.GLOBAL_KEYS_DATA] = ComputeCullShader.GetUniform(CalculateSurfaceDispatcher.BufferNames.GLOBAL_KEYS_DATA),
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
}
