using System;
using Godot;
using Godot.Collections;
using Planet;
using Uniform;

namespace Dispatcher
{
    public partial class CopyKeysDispatcher : ComputeShaderDispatcher<CopyKeysDispatcher.BufferNames>
    {
        public PlanetData PlanetData { get; set; }
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

        public override void CreateUniforms()
        {
            _computeShaderUniforms = new System.Collections.Generic.Dictionary<Enum, ComputeShaderUniform>()
            {
                [BufferNames.ATOMIC_COUNTER] = RenderSurfaceDispatcher.GetUniform(RenderSurfaceDispatcher.BufferNames.ATOMIC_COUNTER),

                [BufferNames.INDICES] = RenderSurfaceDispatcher.GetUniform(RenderSurfaceDispatcher.BufferNames.INDICES),

                [BufferNames.DISPATCH_BUFFER] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.DISPATCH_BUFFER,
                    Utilities.ToBytes<uint>(new uint[] { 6 * (uint)Mathf.Pow(4, PlanetData.StartingLod + 1) / 32 + 1, 1, 1 }).ToArray(), indirect: 1
                ),

                [BufferNames.GLOBAL_KEYS_DATA] = RenderSurfaceDispatcher.GetUniform(RenderSurfaceDispatcher.BufferNames.GLOBAL_KEYS_DATA),

                [BufferNames.MULTIMESH_COMMAND_BUFFER] = new MultimeshUniform(this, 
                    (int)BufferNames.MULTIMESH_COMMAND_BUFFER,
                    RenderSurfaceDispatcher.GetUniform<MultimeshUniform>(RenderSurfaceDispatcher.BufferNames.MULTIMESH_BUFFER).Multimesh
                )
            };

            CreateUniformSet();
        }

        public override void Ready()
        {
            long computeList = RenderingDevice.ComputeListBegin();
            RenderingDevice.ComputeListBindComputePipeline(computeList, _pipeline);
            RenderingDevice.ComputeListBindUniformSet(computeList, _uniformSet, 0);
            RenderingDevice.ComputeListAddBarrier(computeList);
            RenderingDevice.ComputeListDispatch(computeList, 1, 1, 1);
            RenderingDevice.ComputeListEnd();
        }

        public override void UpdateUniforms()
        {
            throw new NotImplementedException();
        }
    }
}
