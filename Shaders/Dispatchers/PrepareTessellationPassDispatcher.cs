using System;
using Godot;
using Uniform;
using PlanetGame.Util;

namespace PlanetGame.Shaders.Dispatchers
{
    public partial class PrepareTessellationPassDispatcher : Dispatcher<PrepareTessellationPassDispatcher.BufferNames>
    {
        public ExecuteTessellationPassDispatcher ExecuteTessellationPass { get; set; }
        public MultiMeshRD PlanetMultimesh { get; set; }

        public enum BufferNames
        {
            ATOMIC_COUNTER,
            INDICES,
            EXEC_DISPATCH_BUFFER,
            DRAW_DISPATCH_BUFFER,
            MULTIMESH_COMMAND_BUFFER,
            MESH_DATA
        }

        public PrepareTessellationPassDispatcher() : base(new() { Compute = ShaderPaths.PREPARE_TESSELLATION_PASS })
        {
            SetupShader();
        }

        public override void CreateUniforms()
        {
            (Vector3[] vertices, int[] indices, Vector3[] _, Vector2[] __) meshData = PlanetMultimesh.GetMeshData();
            _computeShaderUniforms = new System.Collections.Generic.Dictionary<Enum, ShaderUniform>()
            {
                [BufferNames.ATOMIC_COUNTER] = ExecuteTessellationPass[ExecuteTessellationPassDispatcher.BufferNames.ATOMIC_COUNTER],

                [BufferNames.INDICES] = ExecuteTessellationPass[ExecuteTessellationPassDispatcher.BufferNames.KEY_INDICES],

                [BufferNames.EXEC_DISPATCH_BUFFER] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.EXEC_DISPATCH_BUFFER,
                    [.. Utilities.ToBytes<uint>([(uint)ExecuteTessellationPass.GetPrimitiveCounts().fullPrimCount / 64 + 1, 1, 1])], RenderingDevice.StorageBufferUsage.Indirect
                ),

                [BufferNames.DRAW_DISPATCH_BUFFER] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.DRAW_DISPATCH_BUFFER,
                    [.. Utilities.ToBytes<uint>(new uint[5])], RenderingDevice.StorageBufferUsage.Indirect
                ),

                [BufferNames.MULTIMESH_COMMAND_BUFFER] = new StorageBufferUniform(this, RenderingDevice,
                    (int)BufferNames.MULTIMESH_COMMAND_BUFFER,
                    PlanetMultimesh.CommandBuffer,
                    perserve: true
                ),

                [BufferNames.MESH_DATA] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.MESH_DATA,
                    [.. Utilities.ToBytes([(uint)meshData.vertices.Length, (uint)meshData.indices.Length])],
                    perserve: true
                )
            };

            CreateUniformSet();
        }

        #nullable enable
        public override void Invoke(byte[]? pushConstants = null)
        {
            long computeList = RenderingDevice.ComputeListBegin();
            RenderingDevice.ComputeListBindComputePipeline(computeList, _pipeline);
            RenderingDevice.ComputeListBindUniformSet(computeList, _uniformSet, 0);
            RenderingDevice.ComputeListAddBarrier(computeList);
            RenderingDevice.ComputeListDispatch(computeList, 1, 1, 1);
            RenderingDevice.ComputeListEnd();
        }
    }
}
