using System;
using Godot;
using Planet;
using Uniform;
using PlanetGame.Util;

namespace PlanetGame.ComputeShaders.Dispatcher
{
    public partial class CopyKeysDispatcher : ComputeShaderDispatcher<CopyKeysDispatcher.BufferNames>
    {
        public PlanetData PlanetData { get; set; }
        public RenderSurfaceDispatcher RenderSurfaceDispatcher { get; set; }
        public MultiMeshRD PlanetMultiMesh { get; set; }


        public enum BufferNames
        {
            ATOMIC_COUNTER,
            INDICES,
            DISPATCH_BUFFER,
            GLOBAL_KEYS_DATA,
            MULTIMESH_COMMAND_BUFFER
        }

        public CopyKeysDispatcher() : base(ShaderPaths.COPY_KEYS_PATH)
        {
            SetupComputeShader();
        }

        public override void CreateUniforms()
        {
            _computeShaderUniforms = new System.Collections.Generic.Dictionary<Enum, ComputeShaderUniform>()
            {
                [BufferNames.ATOMIC_COUNTER] = RenderSurfaceDispatcher[RenderSurfaceDispatcher.BufferNames.ATOMIC_COUNTER],

                [BufferNames.INDICES] = RenderSurfaceDispatcher[RenderSurfaceDispatcher.BufferNames.INDICES],

                [BufferNames.DISPATCH_BUFFER] = new StorageBufferUniform(this, _RenderingDevice, (int)BufferNames.DISPATCH_BUFFER,
                    Utilities.ToBytes<uint>([6 * (uint)Mathf.Pow(4, PlanetData.StartingLod + 1) / 32 + 1, 1, 1]).ToArray(), RenderingDevice.StorageBufferUsage.Indirect
                ),

                [BufferNames.GLOBAL_KEYS_DATA] = RenderSurfaceDispatcher[RenderSurfaceDispatcher.BufferNames.GLOBAL_KEYS_DATA],


                [BufferNames.MULTIMESH_COMMAND_BUFFER] = new StorageBufferUniform(this, _RenderingDevice,
                    (int)BufferNames.MULTIMESH_COMMAND_BUFFER,
                    PlanetMultiMesh.CommandBuffer,
                    perserve: true
                )
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
