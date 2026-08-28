using System;
using Godot;
using Uniform;
using PlanetGame.Rendering.VirtualTexturing;
using PlanetGame.Util;
using PlanetGame.Planet;
using System.Collections.Generic;

namespace PlanetGame.Shaders.Dispatchers
{
    public partial class ResolveTileRequestDispatcher : Dispatcher<ResolveTileRequestDispatcher.BufferNames>
    {
        private static ShaderProgramPaths _shaderPath = new() { Compute = ShaderPaths.RESOLVE_TILE_REQUEST_PASS };
        public const uint REQUEST_AMOUNT = 256;
        public enum BufferNames
        {
            FEEDBACK_TEXTURE,
            INDIRECTION_TABLE,
            STATE_TABLE,
            RESIDENCY_TABLE,
            VIRTUAL_TEXTURE_DATA,
            TILE_SLOT_COUNTER,
            REQUEST_BUFFER_COUNTER,
            REQUEST_BUFFER
        }

        private Vector2I _viewSize;
        private SparseVirtualTexture _sparseVirtualTexture { get; set; }
        private readonly Dictionary<PlanetRenderer.BufferNames, ShaderUniform> _shaderedShaderUniforms;


        public ResolveTileRequestDispatcher(SparseVirtualTexture sparseVirtualTexture, Dictionary<PlanetRenderer.BufferNames, ShaderUniform> shaderedShaderUniforms, Vector2I viewSize) : base(_shaderPath)
        {
            _shaderedShaderUniforms = shaderedShaderUniforms;
            _sparseVirtualTexture = sparseVirtualTexture;
            _viewSize = viewSize;
            SetupShader();
        }

        public override void CreateUniforms()
        {
            _shaderUniforms = [];
            
            _shaderUniforms[BufferNames.FEEDBACK_TEXTURE] = new Texture2DUniform(this, (int)BufferNames.FEEDBACK_TEXTURE,
                _sparseVirtualTexture.SvtFeedbackRenderPass.GetFeedbackTextureRid(), RenderingDevice.UniformType.Image, perserved: true

            );
            _shaderUniforms[BufferNames.INDIRECTION_TABLE] = new Texture2DUniform(this, (int)BufferNames.INDIRECTION_TABLE,
                _sparseVirtualTexture.IndirectionTable.GetRdRid(), RenderingDevice.UniformType.Image, perserved: true
            );

            _shaderUniforms[BufferNames.STATE_TABLE] = new Texture2DUniform(this, (int)BufferNames.STATE_TABLE,
                _sparseVirtualTexture.StateTable.GetRdRid(), RenderingDevice.UniformType.Image, perserved: true
            );

            _shaderUniforms[BufferNames.RESIDENCY_TABLE] = new Texture2DUniform(this, (int)BufferNames.RESIDENCY_TABLE,
                _sparseVirtualTexture.ResidencyTable.GetRdRid(), RenderingDevice.UniformType.Image, perserved: true
            );

            _shaderUniforms[BufferNames.VIRTUAL_TEXTURE_DATA] = _shaderedShaderUniforms[PlanetRenderer.BufferNames.VIRTUAL_TEXTURE_DATA];

            _shaderUniforms[BufferNames.TILE_SLOT_COUNTER] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.TILE_SLOT_COUNTER, 
                [.. Utilities.ToBytes<uint>(1)]
            );

            _shaderUniforms[BufferNames.REQUEST_BUFFER_COUNTER] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.REQUEST_BUFFER_COUNTER,
                [.. Utilities.ToBytes<uint>(1)]
            );

            _shaderUniforms[BufferNames.REQUEST_BUFFER] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.REQUEST_BUFFER, 
                [.. Utilities.ToBytes<Vector4I>(REQUEST_AMOUNT)]
            );
        
            CreateUniformSet();
        }

        #nullable enable
        public override void Invoke(byte[]? pushConstants = null)
        {            
            uint x = (uint)((_viewSize.X + 31) / 32);
            uint y = (uint)((_viewSize.Y + 31) / 32);
            uint z = 1;

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

        public void ResetTileSlotCounter()
        {
            GetUniform<StorageBufferUniform>(BufferNames.TILE_SLOT_COUNTER).UpdateUniform(
                [.. Utilities.ToBytesSingle(0)]
            );
        }
    }
}
