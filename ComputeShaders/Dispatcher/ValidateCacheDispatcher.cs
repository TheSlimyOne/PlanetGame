using System;
using Uniform;
using Godot;
using PlanetGame.Rendering.VirtualTexturing;
namespace PlanetGame.ComputeShaders.Dispatcher
{
    public class ValidateCacheDispatcher : ComputeShaderDispatcher<ValidateCacheDispatcher.BufferNames>
    {
        public SparseVirtualTexture SparseVirtualTexture { private get; set; }

        public enum BufferNames
        {
            INDIRECTION_TABLE,
            RESIDENCY_TABLE,
            INDIRECTION_TABLE_DATA,
        }

        public ValidateCacheDispatcher() : base(ShaderPaths.VALIDATE_TILE_CACHE)
        {
            SetupComputeShader();
        }

        public override void CreateUniforms()
        {
            _computeShaderUniforms = new System.Collections.Generic.Dictionary<Enum, ComputeShaderUniform>()
            {
                [BufferNames.INDIRECTION_TABLE] = SparseVirtualTexture.ReadFramebuffer[ReadFramebufferDispatcher.BufferNames.INDIRECTION_TABLE],

                [BufferNames.RESIDENCY_TABLE] = SparseVirtualTexture.ReadFramebuffer[ReadFramebufferDispatcher.BufferNames.RESIDENCY_TABLE],

                [BufferNames.INDIRECTION_TABLE_DATA] = SparseVirtualTexture.ReadFramebuffer[ReadFramebufferDispatcher.BufferNames.INDIRECTION_TABLE_DATA],
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