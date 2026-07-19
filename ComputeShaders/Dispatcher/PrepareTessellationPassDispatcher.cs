using System;
using Godot;
using Uniform;
using PlanetGame.Util;

namespace PlanetGame.ComputeShaders.Dispatcher
{
    public partial class PrepareTessellationPassDispatcher : ComputeShaderDispatcher<PrepareTessellationPassDispatcher.BufferNames>
    {
        public ExecuteTessellationPassDispatcher ExecuteTessellationPass { get; set; }
        public MultiMeshRD PlanetMultimesh { get; set; }

        public enum BufferNames
        {
            ATOMIC_COUNTER,
            INDICES,
            DISPATCH_BUFFER,
            GLOBAL_KEYS_DATA,
            MULTIMESH_COMMAND_BUFFER
        }

        public PrepareTessellationPassDispatcher() : base(ShaderPaths.PREPARE_TESSELLATION_PASS)
        {
            SetupComputeShader();
        }

        public override void CreateUniforms()
        {
            _computeShaderUniforms = new System.Collections.Generic.Dictionary<Enum, ComputeShaderUniform>()
            {
                [BufferNames.ATOMIC_COUNTER] = ExecuteTessellationPass[ExecuteTessellationPassDispatcher.BufferNames.ATOMIC_COUNTER],

                [BufferNames.INDICES] = ExecuteTessellationPass[ExecuteTessellationPassDispatcher.BufferNames.KEY_INDICES],

                [BufferNames.DISPATCH_BUFFER] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.DISPATCH_BUFFER,
                    Utilities.ToBytes<uint>([(uint)ExecuteTessellationPass.GetPrimitiveCounts().fullPrimCount / 64 + 1, 1, 1]).ToArray(), RenderingDevice.StorageBufferUsage.Indirect
                ),

                [BufferNames.GLOBAL_KEYS_DATA] = ExecuteTessellationPass[ExecuteTessellationPassDispatcher.BufferNames.GLOBAL_KEYS_DATA],


                [BufferNames.MULTIMESH_COMMAND_BUFFER] = new StorageBufferUniform(this, RenderingDevice,
                    (int)BufferNames.MULTIMESH_COMMAND_BUFFER,
                    PlanetMultimesh.CommandBuffer,
                    perserve: true
                )
            };

            CreateUniformSet();
        }

        public override void Invoke()
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
