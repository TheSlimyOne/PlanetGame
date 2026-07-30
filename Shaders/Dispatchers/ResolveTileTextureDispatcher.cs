using System;
using Godot;
using Uniform;
using PlanetGame.Rendering.VirtualTexturing;
using PlanetGame.Util;

namespace PlanetGame.Shaders.Dispatchers
{
    public partial class ResolveTileTextureDispatcher : Dispatcher<ResolveTileTextureDispatcher.BufferNames>
    {
        // public Viewport Viewport { get; set; }
        
        public SparseVirtualTexture SparseVirtualTexture { get; set; }
        public const uint REQUEST_AMOUNT = 256;
        public enum BufferNames
        {
            INDIRECTION_TABLE,
            STATE_TABLE,
            RESIDENCY_TABLE,
            VIRTUAL_TEXTURE_DATA,
            // REQUESTED_TILE_ID_COUNTER,
            // TILE_CACHE_COUNTER,
            REQUESTED_TILE_IDS
        }

        public ResolveTileTextureDispatcher() : base(new() { Compute = ShaderPaths.RESOLVE_TILE_TEXTURE_PASS })
        {
            SetupShader();
        }

        public override void CreateUniforms()
        {
            // Rid viewportTexture = RenderingServer.ViewportGetTexture(Viewport.GetViewportRid());
            _computeShaderUniforms = new System.Collections.Generic.Dictionary<Enum, ShaderUniform>()
            {

                [BufferNames.INDIRECTION_TABLE] = new Texture2DUniform(this, (int)BufferNames.INDIRECTION_TABLE,
                    SparseVirtualTexture.IndirectionTable.GetTableRid(), RenderingDevice.UniformType.Image, perserved: true
                ),

                [BufferNames.STATE_TABLE] = new Texture2DUniform(this, (int)BufferNames.STATE_TABLE,
                    SparseVirtualTexture.StateTable.GetTableRid(), RenderingDevice.UniformType.Image, perserved: true
                ),

                [BufferNames.RESIDENCY_TABLE] = new Texture2DUniform(this, (int)BufferNames.RESIDENCY_TABLE,
                    SparseVirtualTexture.ResidencyTable.GetTableRid(), RenderingDevice.UniformType.Image, perserved: true
                ),

                [BufferNames.VIRTUAL_TEXTURE_DATA] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.VIRTUAL_TEXTURE_DATA,
                    Utilities.ToBytes(
                        [
                            SparseVirtualTexture.IndirectionTable.GridSize,
                            SparseVirtualTexture.IndirectionTable.MipDepth,
                            SparseVirtualTexture.IndirectionTable.RootTileAmount,
                            TileCache.DEFAULT_TILE_SLOTS_COUNT,

                            REQUEST_AMOUNT, 
                            0u, 
                            0u,
                            0u
                        ]
                    ).ToArray()
                ),

                [BufferNames.REQUESTED_TILE_IDS] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.REQUESTED_TILE_IDS,
                    new byte[Utilities.SizeOf<Vector4I>() * REQUEST_AMOUNT]
                )
            };

            CreateUniformSet();
        }

        #nullable enable
        public override void Invoke(byte[]? pushConstants = null)
        {            
            uint x = (SparseVirtualTexture.StateTable.GridSize + 32) / 32;
            uint y = (SparseVirtualTexture.StateTable.GridSize + 32) / 32;
            uint z = SparseVirtualTexture.StateTable.MipDepth * 6;

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
            GetUniform<StorageBufferUniform>(BufferNames.VIRTUAL_TEXTURE_DATA).UpdateUniform(
                5 * sizeof(uint), sizeof(uint), [.. Utilities.ToBytesSingle(0)]
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
            
            uint amount = GetUniform<StorageBufferUniform>(BufferNames.VIRTUAL_TEXTURE_DATA).GetData<uint>(
                offsetBytes: 5 * sizeof(uint), sizeBytes: sizeof(uint))[0];

            amount = Math.Min(amount, REQUEST_AMOUNT);

            if (amount < 1)
            {
                callback.Call(Array.Empty<byte>());
                return;
            }

            GetUniform<StorageBufferUniform>(BufferNames.REQUESTED_TILE_IDS).GetDataAsync(callback, sizeBytes: amount * Utilities.SizeOf<Vector4I>());
        }

        // public Color GetPixelAt(Vector2I coordinates) => Viewport.GetTexture().GetImage().GetPixelv(coordinates);
    }
}
