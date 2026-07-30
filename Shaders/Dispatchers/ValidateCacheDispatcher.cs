using System;
using Uniform;
using Godot;
using PlanetGame.Rendering.VirtualTexturing;
namespace PlanetGame.Shaders.Dispatchers
{
    public class ValidateCacheDispatcher : Dispatcher<ValidateCacheDispatcher.BufferNames>
    {
        public SparseVirtualTexture SparseVirtualTexture { private get; set; }

        public enum BufferNames
        {
            INDIRECTION_TABLE,
            RESIDENCY_TABLE,
            VIRTUAL_TEXTURE_DATA
        }

        public ValidateCacheDispatcher() : base(new() { Compute = ShaderPaths.VALIDATE_TILE_CACHE })
        {
            SetupShader();
        }

        public override void CreateUniforms()
        {
            _computeShaderUniforms = new System.Collections.Generic.Dictionary<Enum, ShaderUniform>()
            {
                [BufferNames.INDIRECTION_TABLE] = SparseVirtualTexture.ReadFramebuffer[ResolveTileTextureDispatcher.BufferNames.INDIRECTION_TABLE],

                [BufferNames.RESIDENCY_TABLE] = SparseVirtualTexture.ReadFramebuffer[ResolveTileTextureDispatcher.BufferNames.RESIDENCY_TABLE],

                [BufferNames.VIRTUAL_TEXTURE_DATA] = SparseVirtualTexture.ReadFramebuffer[ResolveTileTextureDispatcher.BufferNames.VIRTUAL_TEXTURE_DATA]
            };

            CreateUniformSet();
        }

        #nullable enable
        public override void Invoke(byte[]? pushConstants = null)
        {
            uint x = (SparseVirtualTexture.ResidencyTable.GridSize + 32) / 32;
            uint y = (SparseVirtualTexture.ResidencyTable.GridSize + 32) / 32;

            long computeList = RenderingDevice.ComputeListBegin();
            RenderingDevice.ComputeListBindComputePipeline(computeList, _pipeline);
            RenderingDevice.ComputeListBindUniformSet(computeList, _uniformSet, 0);
            RenderingDevice.ComputeListAddBarrier(computeList);
            RenderingDevice.ComputeListDispatch(computeList, x, y, 1);
            RenderingDevice.ComputeListEnd();
        }

        public override void UpdateUniforms()
        {
            throw new NotImplementedException();
        }
    }

}