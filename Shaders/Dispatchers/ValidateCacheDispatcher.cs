using System;
using Uniform;
using Godot;
using PlanetGame.Rendering.VirtualTexturing;
namespace PlanetGame.Shaders.Dispatchers
{
    public class ValidateCacheDispatcher : Dispatcher<ValidateCacheDispatcher.BufferNames>
    {
        private static ShaderProgramPaths _shaderPath = new() { Compute = ShaderPaths.VALIDATE_TILE_CACHE };
        private readonly SparseVirtualTexture _sparseVirtualTexture;

        public enum BufferNames
        {
            INDIRECTION_TABLE,
            RESIDENCY_TABLE,
            VIRTUAL_TEXTURE_DATA
        }

        public ValidateCacheDispatcher(SparseVirtualTexture sparseVirtualTexture) : base(_shaderPath)
        {
            _sparseVirtualTexture = sparseVirtualTexture;
            SetupShader();
        }

        public override void CreateUniforms()
        {
            _shaderUniforms = new System.Collections.Generic.Dictionary<Enum, ShaderUniform>()
            {
                [BufferNames.INDIRECTION_TABLE] = _sparseVirtualTexture.ResolveTileRequest[ResolveTileRequestDispatcher.BufferNames.INDIRECTION_TABLE],

                [BufferNames.RESIDENCY_TABLE] = _sparseVirtualTexture.ResolveTileRequest[ResolveTileRequestDispatcher.BufferNames.RESIDENCY_TABLE],

                [BufferNames.VIRTUAL_TEXTURE_DATA] = _sparseVirtualTexture.ResolveTileRequest[ResolveTileRequestDispatcher.BufferNames.VIRTUAL_TEXTURE_DATA]
            };

            CreateUniformSet();
        }

        #nullable enable
        public override void Invoke(byte[]? pushConstants = null)
        {
            uint size = _sparseVirtualTexture.ResidencyTable.Size;

            uint x = (size + 31) / 32;
            uint y = (size + 31) / 32;

            long computeList = RenderingDevice.ComputeListBegin();
            RenderingDevice.ComputeListBindComputePipeline(computeList, _pipeline);
            RenderingDevice.ComputeListBindUniformSet(computeList, _uniformSet, 0);
            RenderingDevice.ComputeListAddBarrier(computeList);
            RenderingDevice.ComputeListDispatch(computeList, x, y, 1);
            RenderingDevice.ComputeListEnd();
        }
    }
}