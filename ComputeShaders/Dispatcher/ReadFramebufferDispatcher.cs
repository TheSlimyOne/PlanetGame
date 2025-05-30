using System;
using Godot;
using Uniform;
using PlanetGame.Rendering.VirtualTexturing;
using PlanetGame.Util;

namespace PlanetGame.ComputeShaders.Dispatcher
{
    public partial class ReadFramebufferDispatcher : ComputeShaderDispatcher<ReadFramebufferDispatcher.BufferNames>
    {
        public Viewport Viewport { get; set; }
        public SparseVirtualTexture SparseVirtualTexture { get; set; }

        public enum BufferNames
        {
            FRAMEBUFFER,
            INDIRECTION_TABLE,
            INDIRECTION_STATE_TABLE,
            RESIDENCY_TABLE,
            VIRTUAL_TEXTURE_DATA,
            TEXTURE_ID_COUNTER,
            TILE_CACHE_COUNTER,
            TILE_IDS
        }

        public ReadFramebufferDispatcher() : base(ShaderPaths.READ_FRAME_BUFFER)
        {
            SetupComputeShader();
        }

        public override void CreateUniforms()
        {
            Vector2I size = ((SubViewport)Viewport).Size;
            Rid viewportTexture = RenderingServer.ViewportGetTexture(Viewport.GetViewportRid());

            _computeShaderUniforms = new System.Collections.Generic.Dictionary<Enum, ComputeShaderUniform>()
            {
                [BufferNames.FRAMEBUFFER] = new Texture2DUniform(this, (int)BufferNames.FRAMEBUFFER,
                    RenderingServer.TextureGetRdTexture(viewportTexture), RenderingDevice.UniformType.Image, perserved: true
                ),

                [BufferNames.INDIRECTION_TABLE] = new Texture2DUniform(this, (int)BufferNames.INDIRECTION_TABLE,
                    SparseVirtualTexture.IndirectionTable.Table.TextureRdRid, RenderingDevice.UniformType.Image, perserved: true
                ),

                [BufferNames.INDIRECTION_STATE_TABLE] = new Texture2DUniform(this, (int)BufferNames.INDIRECTION_STATE_TABLE,
                    SparseVirtualTexture.IndirectionStateTable.Table.TextureRdRid, RenderingDevice.UniformType.Image, perserved: true
                ),

                [BufferNames.RESIDENCY_TABLE] = new Texture2DUniform(this, (int)BufferNames.RESIDENCY_TABLE,
                    SparseVirtualTexture.ResidencyTable.Table.TextureRdRid, RenderingDevice.UniformType.Image, perserved: true
                ),

                [BufferNames.VIRTUAL_TEXTURE_DATA] = new StorageBufferUniform(this, _RenderingDevice, (int)BufferNames.VIRTUAL_TEXTURE_DATA,
                    Utilities.ToBytes(
                        [
                            SparseVirtualTexture.IndirectionTable.GridSize,
                            SparseVirtualTexture.IndirectionTable.MipDepth,
                            SparseVirtualTexture.IndirectionTable.RootTileAmount,
                            SparseVirtualTexture.TotalTileSlots
                        ]
                    ).ToArray()
                ),

                [BufferNames.TEXTURE_ID_COUNTER] = new StorageBufferUniform(this, _RenderingDevice, (int)BufferNames.TEXTURE_ID_COUNTER,
                    new byte[Utilities.SizeOf<uint>()]
                ),

                [BufferNames.TILE_CACHE_COUNTER] = new StorageBufferUniform(this, _RenderingDevice, (int)BufferNames.TILE_CACHE_COUNTER,
                    Utilities.ToBytesSingle(0).ToArray()
                ),

                [BufferNames.TILE_IDS] = new StorageBufferUniform(this, _RenderingDevice, (int)BufferNames.TILE_IDS,
                    new byte[Utilities.SizeOf<uint>() * size.X * size.Y]
                )
            };

            CreateUniformSet();
        }

        public override void Invoke()
        {
            Vector2I size = ((SubViewport)Viewport).Size;
            long computeList = _RenderingDevice.ComputeListBegin();
            _RenderingDevice.ComputeListBindComputePipeline(computeList, _pipeline);
            _RenderingDevice.ComputeListBindUniformSet(computeList, _uniformSet, 0);
            _RenderingDevice.ComputeListAddBarrier(computeList);
            _RenderingDevice.ComputeListDispatch(computeList, (uint)(size.X / 8 + 1), (uint)(size.Y / 8 + 1), 1);
            _RenderingDevice.ComputeListEnd();
        }

        public override void UpdateUniforms()
        {
            this[BufferNames.TEXTURE_ID_COUNTER].UpdateUniform(new byte[Utilities.SizeOf<int>()]);
            // GD.Print(GetUniform<StorageBufferUniform>(BufferNames.TILE_CACHE_COUNTER).GetData<uint>()[0]);
            // GetUniform<Texture2DUniform>(BufferNames.INDIRECTION_TABLE).ClearTexture(new Color(0, 0, 0, 0), layerCount: totalPages);
            // GetUniform<Texture2DUniform>(BufferNames.REQUEST_TABLE).ClearTexture(new Color(0, 0, 0, 0), layerCount: totalPages);
        }

        // public void UpdateIndirectionTableData(uint chunkPixelSize, uint gridSize, uint mipDepth, uint rootTileAmount)
        // {
        //     GetUniform<StorageBufferUniform>(BufferNames.INDIRECTION_TABLE_DATA).UpdateUniform(Utilities.ToBytes([chunkPixelSize, gridSize, mipDepth, rootTileAmount]).ToArray());
        // }

        public void GetTextureIds(Callable callback)
        {
            int amount = GetUniform<StorageBufferUniform>(BufferNames.TEXTURE_ID_COUNTER).GetData<int>()[0];
            if (amount == 0)
            {
                callback.Call(Array.Empty<byte>());
                return;
            }

            GetUniform<StorageBufferUniform>(BufferNames.TILE_IDS).GetDataAsync(callback, sizeBytes: (uint)amount * Utilities.SizeOf<uint>());
        }
    }
}
