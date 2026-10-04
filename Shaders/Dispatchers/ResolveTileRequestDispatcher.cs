using System;
using Godot;
using Uniform;
using PlanetGame.Planet.Rendering.VirtualTexturing;
using PlanetGame.Util;
using System.Collections.Generic;
using PlanetGame.Planet.Rendering;
using PlanetGame.Data;

namespace PlanetGame.Shaders.Dispatchers
{
    public partial class ResolveTileRequestDispatcher : Dispatcher<ResolveTileRequestDispatcher.BufferNames>
    {
        private static VirtualTextureData VirtualTextureData => SaveManager.VirtualTextureData;
        private static ShaderProgramPaths _shaderPath = new() { Compute = ShaderPaths.RESOLVE_TILE_REQUEST_COMPUTE };
        public const uint REQUEST_AMOUNT = 256;
        public enum BufferNames
        {
            INDIRECTION_TABLE,
            STATE_TABLE,
            RESIDENCY_TABLE,
            VIRTUAL_TEXTURE_DATA,
            TILE_REQUEST_DATA,
            TILE_SLOT_COUNTER,
            REQUEST_BUFFER_COUNTER,
            REQUEST_BUFFER
        }

        private SparseVirtualTexture _sparseVirtualTexture { get; set; }
        private readonly Dictionary<PlanetRenderer.BufferNames, ShaderUniform> _sharedShaderUniforms;


        public ResolveTileRequestDispatcher(SparseVirtualTexture sparseVirtualTexture, Dictionary<PlanetRenderer.BufferNames, ShaderUniform> sharedShaderUniforms) : base(_shaderPath)
        {
            _sharedShaderUniforms = sharedShaderUniforms;
            _sparseVirtualTexture = sparseVirtualTexture;
            SetupShader();
        }

        public override void CreateUniforms()
        {
            _shaderUniforms = [];

            _shaderUniforms[BufferNames.INDIRECTION_TABLE] = _sparseVirtualTexture.IndirectionTable;

            _shaderUniforms[BufferNames.STATE_TABLE] = _sparseVirtualTexture.StateTable;

            _shaderUniforms[BufferNames.RESIDENCY_TABLE] = _sparseVirtualTexture.ResidencyTable;

            _shaderUniforms[BufferNames.VIRTUAL_TEXTURE_DATA] = _sharedShaderUniforms[PlanetRenderer.BufferNames.VIRTUAL_TEXTURE_DATA];

            _shaderUniforms[BufferNames.TILE_REQUEST_DATA] = new StorageBufferUniform(BufferNames.TILE_REQUEST_DATA.ToString(), this, RenderingDevice,
                GetTileRequestData()
            );

            _shaderUniforms[BufferNames.TILE_SLOT_COUNTER] = new StorageBufferUniform(BufferNames.TILE_SLOT_COUNTER.ToString(), this, RenderingDevice,
                [.. Utilities.ToBytes<uint>(1)]
            );

            _shaderUniforms[BufferNames.REQUEST_BUFFER_COUNTER] = new StorageBufferUniform(BufferNames.REQUEST_BUFFER_COUNTER.ToString(), this, RenderingDevice,
                [.. Utilities.ToBytes<uint>(1)]
            );

            _shaderUniforms[BufferNames.REQUEST_BUFFER] = new StorageBufferUniform(BufferNames.REQUEST_BUFFER.ToString(), this, RenderingDevice,
                [.. Utilities.ToBytes<Vector4I>(REQUEST_AMOUNT)]
            );

            CreateUniformSet();
        }

#nullable enable
        protected override void InvokeInternal(object[]? pushConstants = null)
        {
            uint x = (VirtualTextureData.BaseGridSize + 31) / 32;
            uint y = (VirtualTextureData.BaseGridSize + 31) / 32;
            uint z = VirtualTextureData.TotalMipLayers;

            long computeList = RenderingDevice.ComputeListBegin();
            RenderingDevice.ComputeListBindComputePipeline(computeList, _pipeline);
            RenderingDevice.ComputeListBindUniformSet(computeList, _uniformSet, 0);
            RenderingDevice.ComputeListAddBarrier(computeList);
            RenderingDevice.ComputeListDispatch(computeList, x, y, z);
            RenderingDevice.ComputeListEnd();
        }
#nullable disable

        public override void UpdateUniforms()
        {
            // Updates the counter for the request buffer
            GetUniform<StorageBufferUniform>(BufferNames.REQUEST_BUFFER_COUNTER).UpdateUniform(
                [.. Utilities.ToBytesSingle(0)]
            );
        }

        // public void UpdateIndirectionTableData(uint chunkPixelSize, uint gridSize, uint mipDepth, uint rootTileAmount)
        // {
        //     GetUniform<StorageBufferUniform>(BufferNames.INDIRECTION_TABLE_DATA).UpdateUniform(Utilities.ToBytes([chunkPixelSize, gridSize, mipDepth, rootTileAmount]).ToArray());
        // }

        // public int GetCacheCounter()
        // {
        //     return GetUniform<StorageBufferUniform>(BufferNames.TILE_CACHE_COUNTER).GetData<int>()[0];
        // }

        public void GetTextureIds(Callable callback)
        {

            uint amount = GetUniform<StorageBufferUniform>(BufferNames.REQUEST_BUFFER_COUNTER).GetData<uint>()[0];

            amount = Math.Min(amount, REQUEST_AMOUNT);

            if (amount < 1)
            {
                callback.Call(Array.Empty<byte>());
                return;
            }

            GetUniform<StorageBufferUniform>(BufferNames.REQUEST_BUFFER).GetDataAsync(callback, sizeBytes: amount * Utilities.SizeOf<Vector4I>());
        }

        public void ResetTileSlotCounter()
        {
            GetUniform<StorageBufferUniform>(BufferNames.TILE_SLOT_COUNTER).UpdateUniform(
                [.. Utilities.ToBytesSingle(0)]
            );
        }

        private static byte[] GetTileRequestData()
        {
            return [.. Utilities.ToBytes([
                TileCache.DEFAULT_TILE_SLOTS_COUNT,
                (uint)Mathf.Ceil(Mathf.Sqrt(TileCache.DEFAULT_TILE_SLOTS_COUNT)),
                REQUEST_AMOUNT,
                0u
            ])];
        }
    }
}
