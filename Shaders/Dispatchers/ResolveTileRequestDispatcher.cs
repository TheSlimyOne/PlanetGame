using System;
using Godot;
using Uniform;
using PlanetGame.Rendering.VirtualTexturing;
using PlanetGame.Util;

namespace PlanetGame.Shaders.Dispatchers
{
    public partial class ResolveTileRequestDispatcher : Dispatcher<ResolveTileRequestDispatcher.BufferNames>
    {
        public SparseVirtualTexture SparseVirtualTexture { get; set; }
        public const uint REQUEST_AMOUNT = 256;
        public enum BufferNames
        {
            INDIRECTION_TABLE,
            STATE_TABLE,
            RESIDENCY_TABLE,
            VIRTUAL_TEXTURE_DATA,
            TILE_SLOT_COUNTER,
            REQUEST_BUFFER_COUNTER,
            REQUEST_BUFFER
        }

        public ResolveTileRequestDispatcher() : base(new() { Compute = ShaderPaths.RESOLVE_TILE_REQUEST_PASS })
        {
            SetupShader();
        }

        public override void CreateUniforms()
        {
            _computeShaderUniforms = new System.Collections.Generic.Dictionary<Enum, ShaderUniform>()
            {

                [BufferNames.INDIRECTION_TABLE] = new Texture2DUniform(this, (int)BufferNames.INDIRECTION_TABLE,
                    SparseVirtualTexture.IndirectionTable.GetRdRid(), RenderingDevice.UniformType.Image, perserved: true
                ),

                [BufferNames.STATE_TABLE] = new Texture2DUniform(this, (int)BufferNames.STATE_TABLE,
                    SparseVirtualTexture.StateTable.GetRdRid(), RenderingDevice.UniformType.Image, perserved: true
                ),

                [BufferNames.RESIDENCY_TABLE] = new Texture2DUniform(this, (int)BufferNames.RESIDENCY_TABLE,
                    SparseVirtualTexture.ResidencyTable.GetRdRid(), RenderingDevice.UniformType.Image, perserved: true
                ),

                [BufferNames.VIRTUAL_TEXTURE_DATA] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.VIRTUAL_TEXTURE_DATA,
                    Utilities.ToBytes(
                        [
                            SparseVirtualTexture.VirtualTextureData.LowResolutionMipCount,
                            SparseVirtualTexture.VirtualTextureData.HighResolutionMipCount,

                            SparseVirtualTexture.VirtualTextureData.GridSize,
                            (uint)SparseVirtualTexture.VirtualTextureData.FallBackTiles.Length,
                            
                            TileCache.DEFAULT_TILE_SLOTS_COUNT,
                            (uint)Mathf.Sqrt(TileCache.DEFAULT_TILE_SLOTS_COUNT),

                            REQUEST_AMOUNT,  
                            0u
                        ]
                    ).ToArray()
                ),

                [BufferNames.TILE_SLOT_COUNTER] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.TILE_SLOT_COUNTER, [.. Utilities.ToBytesSingle<uint>(0)]),

                [BufferNames.REQUEST_BUFFER_COUNTER] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.REQUEST_BUFFER_COUNTER, [.. Utilities.ToBytesSingle<uint>(0)]),

                [BufferNames.REQUEST_BUFFER] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.REQUEST_BUFFER,
                    new byte[Utilities.SizeOf<Vector4I>() * REQUEST_AMOUNT]
                )
            };

            CreateUniformSet();
        }

        #nullable enable
        public override void Invoke(byte[]? pushConstants = null)
        {            
            uint gridSize = SparseVirtualTexture.VirtualTextureData.GridSize;
            uint x = (gridSize + 32) / 32;
            uint y = (gridSize + 32) / 32;
            uint z = SparseVirtualTexture.VirtualTextureData.TotalMipLayers;

            long computeList = RenderingDevice.ComputeListBegin();
            RenderingDevice.ComputeListBindComputePipeline(computeList, _pipeline);
            RenderingDevice.ComputeListBindUniformSet(computeList, _uniformSet, 0);
            RenderingDevice.ComputeListAddBarrier(computeList);
            RenderingDevice.ComputeListDispatch(computeList, x, y, z);
            RenderingDevice.ComputeListEnd();
        }
    
    
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

        internal void ResetTileSlotCounter()
        {
            GetUniform<StorageBufferUniform>(BufferNames.TILE_SLOT_COUNTER).UpdateUniform(
                [.. Utilities.ToBytesSingle(0)]
            );
        }

        // public Color GetPixelAt(Vector2I coordinates) => Viewport.GetTexture().GetImage().GetPixelv(coordinates);

    }
}
