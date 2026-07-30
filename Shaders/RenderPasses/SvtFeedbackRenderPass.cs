using System;
using System.Threading;
using System.Threading.Tasks;
using PlanetGame.Shaders.Dispatchers;
using Godot;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using Uniform;
using PlanetGame.Util;
using PlanetGame.Rendering.VirtualTexturing;
using PlanetGame.Rendering.Surface;

namespace PlanetGame.Shaders.RenderPasses
{
    public partial class SvtFeedbackRenderPass : RenderPass<SvtFeedbackRenderPass.BufferNames>
    {

        public TerrainTessellator TerrainTessellator { get; set; }
        public SparseVirtualTexture SparseVirtualTexture { get; set; }

        private Rid _depthTexture;

        public SvtFeedbackRenderPass(TerrainTessellator terrainTessellator, SparseVirtualTexture sparseVirtualTexture, Vector2I viewSize, Mesh arrayMesh) : base(new()
        {
            Vertex = ShaderPaths.PLANET_TESSELLATION_VERTEX,
            Fragment = ShaderPaths.PLANET_TESSELLATION_REQUEST_FRAGMENT
        }, viewSize)
        {
            TerrainTessellator = terrainTessellator;
            SparseVirtualTexture = sparseVirtualTexture;
            SetupShader(arrayMesh);
        }

        public enum BufferNames
        {
            MULTIMESH_BUFFER,
            EXTERNAL_DATA,
            HEIGHT_MAP,
            INDIRECTION_TABLE,
            STATE_TABLE
        }

        public override void CreateUniforms()
        {

            _renderShaderUniforms = new Dictionary<Enum, ShaderUniform>()
            {
                [BufferNames.MULTIMESH_BUFFER] = TerrainTessellator.ExecuteTessellationPass[ExecuteTessellationPassDispatcher.BufferNames.MULTIMESH_BUFFER],

                [BufferNames.EXTERNAL_DATA] = TerrainTessellator.ExecuteTessellationPass[ExecuteTessellationPassDispatcher.BufferNames.EXTERNAL_DATA],

                [BufferNames.HEIGHT_MAP] = new Texture2DUniform(this, (int)BufferNames.HEIGHT_MAP, SparseVirtualTexture.HeightTileCache.GetTableRid(), RenderingDevice.UniformType.SamplerWithTexture, true),

                [BufferNames.INDIRECTION_TABLE] = new Texture2DUniform(this, (int)BufferNames.INDIRECTION_TABLE, SparseVirtualTexture.IndirectionTable.GetTableRid(), RenderingDevice.UniformType.SamplerWithTexture, true),

                [BufferNames.STATE_TABLE] = new Texture2DUniform(this, (int)BufferNames.STATE_TABLE, SparseVirtualTexture.StateTable.GetTableRid(), RenderingDevice.UniformType.Image, true)
            };
            CreateUniformSet();
        }

        public override void Invoke()
        {
            long drawList = RenderingDevice.DrawListBegin(
                _framebuffer,
                RenderingDevice.DrawFlags.ClearColorAll |
                RenderingDevice.DrawFlags.ClearDepth,
                [new Color(1, 1, 1, 1)],
                1.0f,
                1
            );

            RenderingDevice.DrawListBindRenderPipeline(drawList, _pipeline);
            RenderingDevice.DrawListBindVertexArray(drawList, _geometry.VertexArray);
            RenderingDevice.DrawListBindIndexArray(drawList, _geometry.IndexArray);
            RenderingDevice.DrawListBindUniformSet(drawList, _uniformSet, 0);
            RenderingDevice.DrawListDrawIndirect(drawList, true, TerrainTessellator.PrepareTessellationPass[PrepareTessellationPassDispatcher.BufferNames.DRAW_DISPATCH_BUFFER].Rid);
            RenderingDevice.DrawListEnd();
        }

    
        public Texture2Drd GetFrameBufferTexture() => new() {TextureRdRid = _framebufferTexture};

        public override void CleanupGPU()
        {
            if (RenderingDevice == null)
                return;

            if (_depthTexture.IsValid)
                RenderingDevice.FreeRid(_depthTexture);
            // if (VertexArray.IsValid)
            //     RenderingDevice.FreeRid(VertexArray);

            // if (IndexArray.IsValid)
            //     RenderingDevice.FreeRid(IndexArray);



            // if (VertexBuffer.IsValid)
            //     RenderingDevice.FreeRid(VertexBuffer);

            // if (NormalBuffer.IsValid)
            //     RenderingDevice.FreeRid(NormalBuffer);

            // if (IndexBuffer.IsValid)
            //     RenderingDevice.FreeRid(IndexBuffer);

            // VertexArray = default;
            // IndexArray = default;
            // _framebuffer = default;
            // _framebufferTexture = default;
            // _depthTexture = default;
            // _framebufferFormat = 0;
            // VertexBuffer = default;
            // NormalBuffer = default;
            // IndexBuffer = default;
            // VertexFormat = 0;
            base.CleanupGPU();
        }

        protected override Rid CreatePipeline()
        {
            return RenderingDevice.RenderPipelineCreate(
                _shader,
                _framebufferFormat,
                _geometry.VertexFormat,
                RenderingDevice.RenderPrimitive.Triangles,
                new()
                {
                    CullMode = RenderingDevice.PolygonCullMode.Back,
                    Wireframe = false,
                    LineWidth = 1.0f
                },
                new RDPipelineMultisampleState(),
                new()
                {
                    EnableDepthTest = true,
                    EnableDepthWrite = true,
                    DepthCompareOperator = RenderingDevice.CompareOperator.Less
                },
                new()
                {
                    Attachments =
                    [
                        new RDPipelineColorBlendStateAttachment()
                    ]
                }
            );
        }

        protected override Rid CreateFramebuffer()
        {
            RDTextureFormat textureFormat = new()
            {
                TextureType = RenderingDevice.TextureType.Type2D,
                Width = (uint)ViewSize.X,
                Height = (uint)ViewSize.Y,
                Depth = 1,
                ArrayLayers = 1,
                Mipmaps = 1,
                Format = RenderingDevice.DataFormat.R32G32B32A32Sfloat,
                Samples = RenderingDevice.TextureSamples.Samples1,
                UsageBits = RenderingDevice.TextureUsageBits.ColorAttachmentBit |
                            RenderingDevice.TextureUsageBits.CanCopyFromBit |
                            RenderingDevice.TextureUsageBits.SamplingBit
            };

            RDTextureFormat depthFormat = new()
            {
                Width = (uint)ViewSize.X,
                Height = (uint)ViewSize.Y,
                Format = RenderingDevice.DataFormat.D32Sfloat,
                TextureType = RenderingDevice.TextureType.Type2D,
                Samples = RenderingDevice.TextureSamples.Samples1,
                UsageBits =
                    RenderingDevice.TextureUsageBits.DepthStencilAttachmentBit
            };


            _framebufferTexture = RenderingDevice.TextureCreate(
                textureFormat,
                new()
            );

            _depthTexture = RenderingDevice.TextureCreate(
                depthFormat,
                new()
            );

            RDAttachmentFormat colorAttachmentFormat = new()
            {
                Format = textureFormat.Format,
                Samples = RenderingDevice.TextureSamples.Samples1,
                UsageFlags = (uint)(
                    RenderingDevice.TextureUsageBits.ColorAttachmentBit |
                    RenderingDevice.TextureUsageBits.CanCopyFromBit |
                    RenderingDevice.TextureUsageBits.SamplingBit
                )
            };

            RDAttachmentFormat depthAttachmentFormat = new()
            {
                Format = depthFormat.Format,
                Samples = RenderingDevice.TextureSamples.Samples1,
                UsageFlags = (uint)RenderingDevice.TextureUsageBits.DepthStencilAttachmentBit
            };


            _framebufferFormat = RenderingDevice.FramebufferFormatCreate([colorAttachmentFormat, depthAttachmentFormat]);

            return RenderingDevice.FramebufferCreate(
                [_framebufferTexture, _depthTexture]
            );
        }

        public override void UpdateUniforms()
        {
            throw new NotImplementedException();
        }
    }
}