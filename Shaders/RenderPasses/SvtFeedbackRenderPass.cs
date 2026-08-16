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
        private Rid _pickingTexture;

        private Image _pickingImage;

        public SvtFeedbackRenderPass(
            TerrainTessellator terrainTessellator,
            SparseVirtualTexture sparseVirtualTexture,
            Vector2I viewSize,
            Mesh arrayMesh
        ) : base(new()
        {
            Vertex = ShaderPaths.PLANET_TESSELLATION_VERTEX,
            Fragment = ShaderPaths.PLANET_TESSELLATION_REQUEST_FRAGMENT
        },
            viewSize
        )
        {
            TerrainTessellator = terrainTessellator;
            SparseVirtualTexture = sparseVirtualTexture;

            SetupShader(arrayMesh);
        }

        public enum BufferNames
        {
            MULTIMESH_BUFFER,
            EXTERNAL_DATA,
            GLOBAL_KEYS_DATA,
            HEIGHT_MAP,
            CONSOLIDATED_INDIRECTION_TABLE,
            STATE_TABLE
        }

        public override void CreateUniforms()
        {

            _renderShaderUniforms = new Dictionary<Enum, ShaderUniform>()
            {
                [BufferNames.MULTIMESH_BUFFER] = TerrainTessellator.ExecuteTessellationPass[ExecuteTessellationPassDispatcher.BufferNames.MULTIMESH_BUFFER],

                [BufferNames.EXTERNAL_DATA] = TerrainTessellator.ExecuteTessellationPass[ExecuteTessellationPassDispatcher.BufferNames.EXTERNAL_DATA],

                [BufferNames.GLOBAL_KEYS_DATA] = TerrainTessellator.ExecuteTessellationPass[ExecuteTessellationPassDispatcher.BufferNames.GLOBAL_KEYS_DATA],

                [BufferNames.HEIGHT_MAP] = new Texture2DUniform(this, (int)BufferNames.HEIGHT_MAP, SparseVirtualTexture.HeightTileCache.GetRdRid(), RenderingDevice.UniformType.SamplerWithTexture, true),

                [BufferNames.CONSOLIDATED_INDIRECTION_TABLE] = new Texture2DUniform(this, (int)BufferNames.CONSOLIDATED_INDIRECTION_TABLE, SparseVirtualTexture.ConsolidatedIndirectionTable.GetRdRid(), RenderingDevice.UniformType.SamplerWithTexture, true),

                [BufferNames.STATE_TABLE] = new Texture2DUniform(this, (int)BufferNames.STATE_TABLE, SparseVirtualTexture.StateTable.GetRdRid(), RenderingDevice.UniformType.Image, true)
            };
            CreateUniformSet();
        }

        public override void Invoke()
        {
            long drawList = RenderingDevice.DrawListBegin(
                framebuffer: _framebuffer,
                drawFlags: RenderingDevice.DrawFlags.ClearColorAll | RenderingDevice.DrawFlags.ClearDepth,
                clearColorValues: [
                    new Color(0, 0, 0, 0),
                    new Color(0, 0, 0, 0)
                ],
                clearDepthValue: 1.0f,
                clearStencilValue: 0
            );

            RenderingDevice.DrawListBindRenderPipeline(drawList, _pipeline);
            RenderingDevice.DrawListBindVertexArray(drawList, _geometry.VertexArray);
            RenderingDevice.DrawListBindIndexArray(drawList, _geometry.IndexArray);
            RenderingDevice.DrawListBindUniformSet(drawList, _uniformSet, 0);
            RenderingDevice.DrawListDrawIndirect(drawList, true, TerrainTessellator.PrepareTessellationPass[PrepareTessellationPassDispatcher.BufferNames.DRAW_DISPATCH_BUFFER].Rid);
            RenderingDevice.DrawListEnd();

            _pickingImage = GetPickingImage();
        }


        public Texture2Drd GetFrameBufferTexture() => new() { TextureRdRid = _framebufferTexture };
        public Rid GetFeedbackTextureRid() => _framebufferTexture;
        public Rid GetDepthTextureRid() => _depthTexture;
        public Texture2Drd GetPickingTexture() => new() { TextureRdRid = _pickingTexture };
        public Image GetPickingImage()
        {
            byte[] data = RenderingDevice.TextureGetData(_pickingTexture, 0);

            Image image = Image.CreateFromData(ViewSize.X, ViewSize.Y, false, Image.Format.Rgbaf, data);

            return image;
        }

        public override void CleanupGPU()
        {
            if (RenderingDevice == null)
                return;

            if (_depthTexture.IsValid)
                RenderingDevice.FreeRid(_depthTexture);
            if (_pickingTexture.IsValid)
                RenderingDevice.FreeRid(_pickingTexture);

            _depthTexture = default;
            _pickingTexture = default;
            _framebufferFormat = 0;

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
                        new RDPipelineColorBlendStateAttachment
                        {
                            EnableBlend = false
                        },
                        new RDPipelineColorBlendStateAttachment
                        {
                            EnableBlend = false
                        }
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
                            RenderingDevice.TextureUsageBits.SamplingBit |
                            RenderingDevice.TextureUsageBits.StorageBit
            };

            RDTextureFormat depthFormat = new()
            {
                Width = (uint)ViewSize.X,
                Height = (uint)ViewSize.Y,
                Depth = 1,
                ArrayLayers = 1,
                Mipmaps = 1,
                Format = RenderingDevice.DataFormat.D32Sfloat,
                TextureType = RenderingDevice.TextureType.Type2D,
                Samples = RenderingDevice.TextureSamples.Samples1,
                UsageBits =
                    RenderingDevice.TextureUsageBits.DepthStencilAttachmentBit |
                    RenderingDevice.TextureUsageBits.SamplingBit |
                    RenderingDevice.TextureUsageBits.CanCopyFromBit

            };

            RDTextureFormat pickingFormat = new()
            {
                Width = (uint)ViewSize.X,
                Height = (uint)ViewSize.Y,
                Depth = 1,
                ArrayLayers = 1,
                Mipmaps = 1,
                Format = RenderingDevice.DataFormat.R32G32B32A32Sfloat,
                TextureType = RenderingDevice.TextureType.Type2D,
                Samples = RenderingDevice.TextureSamples.Samples1,
                UsageBits = RenderingDevice.TextureUsageBits.ColorAttachmentBit |
                            RenderingDevice.TextureUsageBits.CanCopyFromBit |
                            RenderingDevice.TextureUsageBits.SamplingBit |
                            RenderingDevice.TextureUsageBits.CpuReadBit

            };


            _framebufferTexture = RenderingDevice.TextureCreate(
                textureFormat,
                new()
            );

            _depthTexture = RenderingDevice.TextureCreate(
                depthFormat,
                new()
            );

            _pickingTexture = RenderingDevice.TextureCreate(
                pickingFormat,
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

            RDAttachmentFormat pickingAttachmentFormat = new()
            {
                Format = pickingFormat.Format,
                Samples = RenderingDevice.TextureSamples.Samples1,
                UsageFlags = (uint)(
                    RenderingDevice.TextureUsageBits.ColorAttachmentBit |
                    RenderingDevice.TextureUsageBits.CanCopyFromBit |
                    RenderingDevice.TextureUsageBits.SamplingBit |
                    RenderingDevice.TextureUsageBits.CpuReadBit
                )
            };

            _framebufferFormat = RenderingDevice.FramebufferFormatCreate(
                [
                    colorAttachmentFormat,
                    pickingAttachmentFormat,
                    depthAttachmentFormat
                ]
            );

            return RenderingDevice.FramebufferCreate(
                [_framebufferTexture, _pickingTexture, _depthTexture]
            );
        }

        public override void UpdateUniforms()
        {
            throw new NotImplementedException();
        }

        public Vector3 GetLocalMousePosition(Vector2 mousePosition, Vector2 screenSize)
        {

            Vector2 normalizedMousePosition = mousePosition / screenSize;

            Vector2I pixelPosition = new(
                Mathf.Clamp(
                    (int)(normalizedMousePosition.X * _pickingImage.GetWidth()),
                    0,
                    _pickingImage.GetWidth() - 1
                ),
                Mathf.Clamp(
                    (int)(normalizedMousePosition.Y * _pickingImage.GetHeight()),
                    0,
                    _pickingImage.GetHeight() - 1
                )
            );

            Color pickingData = _pickingImage.GetPixelv(pixelPosition);

            // Return an invald value if not a valid pick
            if (pickingData.A == -1)
                return Vector3.Inf;

            return new(
                pickingData.R,
                pickingData.G,
                pickingData.B
            );
        }
    }
}