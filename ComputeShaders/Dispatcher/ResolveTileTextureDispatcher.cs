using System;
using Godot;
using Uniform;
using PlanetGame.Rendering.VirtualTexturing;
using PlanetGame.Util;

namespace PlanetGame.ComputeShaders.Dispatcher
{
    public partial class ResolveTileTextureDispatcher : ComputeShaderDispatcher<ResolveTileTextureDispatcher.BufferNames>
    {
        public Viewport Viewport { get; set; }
        public SparseVirtualTexture SparseVirtualTexture { get; set; }
        public const int REQUEST_AMOUNT = 256;

        public enum BufferNames
        {
            FRAMEBUFFER,
            INDIRECTION_TABLE,
            STATE_TABLE,
            RESIDENCY_TABLE,
            VIRTUAL_TEXTURE_DATA,
            REQUESTED_TILE_ID_COUNTER,
            TILE_CACHE_COUNTER,
            REQUESTED_TILE_IDS
        }

        public ResolveTileTextureDispatcher() : base(ShaderPaths.RESOLVE_TILE_TEXTURE_PASS)
        {
            SetupComputeShader();
        }

        public override void CreateUniforms()
        {
            Rid viewportTexture = RenderingServer.ViewportGetTexture(Viewport.GetViewportRid());
            _computeShaderUniforms = new System.Collections.Generic.Dictionary<Enum, ComputeShaderUniform>()
            {
                [BufferNames.FRAMEBUFFER] = new Texture2DUniform(this, (int)BufferNames.FRAMEBUFFER,
                    RenderingServer.TextureGetRdTexture(viewportTexture), RenderingDevice.UniformType.Image, perserved: true
                ),

                [BufferNames.INDIRECTION_TABLE] = new Texture2DUniform(this, (int)BufferNames.INDIRECTION_TABLE,
                    SparseVirtualTexture.IndirectionTable.Table.TextureRdRid, RenderingDevice.UniformType.Image, perserved: true
                ),

                [BufferNames.STATE_TABLE] = new Texture2DUniform(this, (int)BufferNames.STATE_TABLE,
                    SparseVirtualTexture.StateTable.Table.TextureRdRid, RenderingDevice.UniformType.Image, perserved: true
                ),

                [BufferNames.RESIDENCY_TABLE] = new Texture2DUniform(this, (int)BufferNames.RESIDENCY_TABLE,
                    SparseVirtualTexture.ResidencyTable.Table.TextureRdRid, RenderingDevice.UniformType.Image, perserved: true
                ),

                [BufferNames.VIRTUAL_TEXTURE_DATA] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.VIRTUAL_TEXTURE_DATA,
                    Utilities.ToBytes(
                        [
                            SparseVirtualTexture.IndirectionTable.GridSize,
                            SparseVirtualTexture.IndirectionTable.MipDepth,
                            SparseVirtualTexture.IndirectionTable.RootTileAmount,
                            TileCache.DEFAULT_TILE_SLOTS_COUNT
                        ]
                    ).ToArray()
                ),

                [BufferNames.REQUESTED_TILE_ID_COUNTER] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.REQUESTED_TILE_ID_COUNTER,
                    Utilities.ToBytesSingle(0).ToArray()
                ),

                [BufferNames.TILE_CACHE_COUNTER] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.TILE_CACHE_COUNTER,
                    Utilities.ToBytesSingle(0).ToArray()
                ),

                [BufferNames.REQUESTED_TILE_IDS] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.REQUESTED_TILE_IDS,
                    new byte[Utilities.SizeOf<uint>() * REQUEST_AMOUNT]
                )
            };

            CreateUniformSet();
        }

        public override void Invoke()
        {
            Vector2I size = ((SubViewport)Viewport).Size;
            long computeList = RenderingDevice.ComputeListBegin();
            RenderingDevice.ComputeListBindComputePipeline(computeList, _pipeline);
            RenderingDevice.ComputeListBindUniformSet(computeList, _uniformSet, 0);
            RenderingDevice.ComputeListAddBarrier(computeList);
            RenderingDevice.ComputeListDispatch(computeList, (uint)(size.X / 8 + 1), (uint)(size.Y / 8 + 1), 1);
            RenderingDevice.ComputeListEnd();
        }

        public override void UpdateUniforms()
        {
            this[BufferNames.REQUESTED_TILE_ID_COUNTER].UpdateUniform(Utilities.ToBytesSingle(0).ToArray());
        }

        // public void UpdateIndirectionTableData(uint chunkPixelSize, uint gridSize, uint mipDepth, uint rootTileAmount)
        // {
        //     GetUniform<StorageBufferUniform>(BufferNames.INDIRECTION_TABLE_DATA).UpdateUniform(Utilities.ToBytes([chunkPixelSize, gridSize, mipDepth, rootTileAmount]).ToArray());
        // }

        public int GetCacheCounter()
        {
            return GetUniform<StorageBufferUniform>(BufferNames.TILE_CACHE_COUNTER).GetData<int>()[0];
        }

        public void GetTextureIds(Callable callback)
        {
            int amount = GetUniform<StorageBufferUniform>(BufferNames.REQUESTED_TILE_ID_COUNTER).GetData<int>()[0];
            if (amount < 1)
            {
                callback.Call(Array.Empty<byte>());
                return;
            }
            GetUniform<StorageBufferUniform>(BufferNames.REQUESTED_TILE_IDS).GetDataAsync(callback, sizeBytes: (uint)amount * Utilities.SizeOf<uint>());
        }

        public Color GetPixelAt(Vector2I coordinates) => Viewport.GetTexture().GetImage().GetPixelv(coordinates);
    }
}
