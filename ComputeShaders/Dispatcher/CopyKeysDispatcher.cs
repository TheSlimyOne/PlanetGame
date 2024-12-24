using System;
using Godot;
using Godot.Collections;
using Uniform;

namespace Dispatcher
{
    public partial class CopyKeysDispatcher : ComputeShaderDispatcher<CopyKeysDispatcher.BufferNames>
    {
        public PlanetController PlanetController { get; set; }
        public RenderSurfaceDispatcher RenderSurfaceDispatcher { get; set; }

        public enum BufferNames
        {
            ATOMIC_COUNTER,
            INDICES,
            DISPATCH_BUFFER,
            GLOBAL_KEYS_DATA,
            MULTIMESH_COMMAND_BUFFER
        }

        public CopyKeysDispatcher(string shaderFilePath, ref RenderingDevice rd) : base(shaderFilePath, ref rd)
        {
            SetupComputeShader();
        }

        public void SetComputeCullShader(RenderSurfaceDispatcher computeCullShader)
        {
            RenderSurfaceDispatcher = computeCullShader;
        }

        public override void CreateUniforms()
        {
            _computeShaderUniforms = new System.Collections.Generic.Dictionary<Enum, ComputeShaderUniform>()
            {
                [BufferNames.ATOMIC_COUNTER] = RenderSurfaceDispatcher.GetUniform(RenderSurfaceDispatcher.BufferNames.ATOMIC_COUNTER),

                [BufferNames.INDICES] = RenderSurfaceDispatcher.GetUniform(RenderSurfaceDispatcher.BufferNames.INDICES),

                [BufferNames.DISPATCH_BUFFER] = new StorageBufferUniform(this, _rd, (int)BufferNames.DISPATCH_BUFFER,
                    Utilities.ToBytes<uint>(new uint[] { 6 * (uint)Mathf.Pow(4, PlanetController.PlanetData.StartingLod + 1) / 32 + 1, 1, 1 }).ToArray(), indirect: 1
                ),

                [BufferNames.GLOBAL_KEYS_DATA] = RenderSurfaceDispatcher.GetUniform(RenderSurfaceDispatcher.BufferNames.GLOBAL_KEYS_DATA),

                [BufferNames.MULTIMESH_COMMAND_BUFFER] = new MultimeshUniform(
                    this,
                    RenderSurfaceDispatcher.GetUniform<MultimeshUniform>(RenderSurfaceDispatcher.BufferNames.MULTIMESH_BUFFER).Parameters,
                    (int)BufferNames.MULTIMESH_COMMAND_BUFFER,
                    true),
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
