using System;
using Uniform;
using Godot;
using PlanetGame.Planet.Rendering.VirtualTexturing;
using System.Collections.Generic;
using PlanetGame.Planet.Rendering;
namespace PlanetGame.Shaders.Dispatchers
{
    public class ValidateCacheDispatcher : Dispatcher<ValidateCacheDispatcher.BufferNames>
    {
        private static ShaderProgramPaths _shaderPath = new() { Compute = ShaderPaths.VALIDATE_TILE_CACHE_COMPUTE };
        private readonly SparseVirtualTexture _sparseVirtualTexture;
        private readonly Dictionary<PlanetRenderer.BufferNames, ShaderUniform> _sharedShaderUniforms;

        public enum BufferNames
        {
            INDIRECTION_TABLE,
            RESIDENCY_TABLE,
            VIRTUAL_TEXTURE_DATA
        }

        public ValidateCacheDispatcher(SparseVirtualTexture sparseVirtualTexture, Dictionary<PlanetRenderer.BufferNames, ShaderUniform> sharedShaderUniforms) : base(_shaderPath)
        {
            _sharedShaderUniforms = sharedShaderUniforms;
            _sparseVirtualTexture = sparseVirtualTexture;
            SetupShader();
        }

        public override void CreateUniforms()
        {
            _shaderUniforms = [];
        
            _shaderUniforms[BufferNames.INDIRECTION_TABLE] = _sparseVirtualTexture.IndirectionTable;

            _shaderUniforms[BufferNames.RESIDENCY_TABLE] = _sparseVirtualTexture.ResidencyTable;

            _shaderUniforms[BufferNames.VIRTUAL_TEXTURE_DATA] = _sharedShaderUniforms[PlanetRenderer.BufferNames.VIRTUAL_TEXTURE_DATA];
            

            CreateUniformSet();
        }

#nullable enable
        protected override void InvokeInternal(object[]? pushConstants = null)
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
#nullable disable
    }
}