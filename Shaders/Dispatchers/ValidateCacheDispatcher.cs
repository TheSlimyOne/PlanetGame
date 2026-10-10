using System;
using Uniform;
using Godot;
using PlanetGame.Planet.Rendering.VirtualTexturing;
using System.Collections.Generic;
using PlanetGame.Planet.Rendering;

using static PlanetGame.Shaders.Dispatchers.ValidateCacheDispatcher.BufferNames;
using static PlanetGame.Shaders.Dispatchers.ValidateCacheDispatcher.BufferSets;

namespace PlanetGame.Shaders.Dispatchers
{
    public class ValidateCacheDispatcher : Dispatcher<ValidateCacheDispatcher.BufferNames, ValidateCacheDispatcher.BufferSets>
    {
        private static ShaderProgramPaths _shaderPath = new() { Compute = ShaderPaths.VALIDATE_TILE_CACHE_COMPUTE };
        private readonly SparseVirtualTexture _sparseVirtualTexture;
        private readonly Dictionary<PlanetRenderer.BufferNames, ShaderUniform> _sharedShaderUniforms;

        public enum BufferSets
        {
            DEFAULT
        }
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
            this[0, INDIRECTION_TABLE, DEFAULT] = _sparseVirtualTexture.IndirectionTable;

            this[1, RESIDENCY_TABLE, DEFAULT] = _sparseVirtualTexture.ResidencyTable;

            this[2, VIRTUAL_TEXTURE_DATA, DEFAULT] = _sharedShaderUniforms[PlanetRenderer.BufferNames.VIRTUAL_TEXTURE_DATA];
            
            CreateUniformSets();
        }

#nullable enable
        protected override void InvokeInternal(object[]? pushConstants = null)
        {
            uint size = _sparseVirtualTexture.ResidencyTable.Size;

            uint x = (size + 31) / 32;
            uint y = (size + 31) / 32;

            long computeList = RenderingDevice.ComputeListBegin();
            RenderingDevice.ComputeListBindComputePipeline(computeList, _pipeline);
            ComputeListBindUniformSets(computeList);
            RenderingDevice.ComputeListAddBarrier(computeList);
            RenderingDevice.ComputeListDispatch(computeList, x, y, 1);
            RenderingDevice.ComputeListEnd();
        }
#nullable disable
    }
}