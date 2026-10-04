using System;
using Godot;
using System.Collections.Generic;
using Uniform;
using PlanetGame.Planet.Rendering;
using PlanetGame.Planet.Rendering.VirtualTexturing;
using PlanetGame.Util;

namespace PlanetGame.Shaders.RenderPasses
{
    public partial class PlanetRenderPass : RenderPass<PlanetRenderPass.BufferNames>
    {
        private static ShaderProgramPaths _shaderPath = new() { Vertex = ShaderPaths.PLANET_VERTEX, Fragment = ShaderPaths.PLANET_FRAGMENT };

        public bool IsWireframe;
        public Rid Picking => _framebufferAttachments["picking"];
        public Rid LinearDepth => _framebufferAttachments["linear_depth"];

        private Image _pickingImage = null;

        public Image GetPickingImage()
        {
            byte[] data = RenderingDevice.TextureGetData(Picking, 0);
            RDTextureFormat format = RenderingDevice.TextureGetFormat(Picking);

            return Image.CreateFromData((int)format.Width, (int)format.Height, false, Image.Format.Rgbaf, data);
        }

        private readonly Dictionary<PlanetRenderer.BufferNames, ShaderUniform> _sharedShaderUniforms;
        private readonly SparseVirtualTexture _sparseVirtualTexture;
        private MultiMeshRD _triangleMultiMesh;

        public enum BufferNames
        {
            MULTIMESH_BUFFER,
            VIRTUAL_TEXTURE_DATA,
            RENDER_DATA,
            TESSELLATION_DATA,
            WORLD_DATA,
            ALBEDO,
            HEIGHTMAP,
            CONSOLIDATED_INDIRECTION_TABLE,
            STATE_TABLE,
        }

        public PlanetRenderPass
        (
            SparseVirtualTexture sparseVirtualTexture,
            MultiMeshRD triangleMultiMesh,
            Dictionary<PlanetRenderer.BufferNames, ShaderUniform> sharedShaderUniforms) :
            base(RenderingServer.GetRenderingDevice(), _shaderPath
        )
        {
            _sharedShaderUniforms = sharedShaderUniforms;
            _sparseVirtualTexture = sparseVirtualTexture;
            _triangleMultiMesh = triangleMultiMesh;

            SetupShader(_triangleMultiMesh.Mesh);

            _framebufferAttachments["picking"] = CreatePickingTexture(Vector2I.One);
            _framebufferAttachments["linear_depth"] = CreateLinearDepthTexture(Vector2I.One);

            _triangleMultiMesh.BuffersChanged += CreateUniformSet;
        }

        public override void CreateUniforms()
        {
            _shaderUniforms = [];

            _shaderUniforms[BufferNames.MULTIMESH_BUFFER] = _triangleMultiMesh.BufferUniform;

            _shaderUniforms[BufferNames.VIRTUAL_TEXTURE_DATA] = _sharedShaderUniforms[PlanetRenderer.BufferNames.VIRTUAL_TEXTURE_DATA];

            _shaderUniforms[BufferNames.RENDER_DATA] = _sharedShaderUniforms[PlanetRenderer.BufferNames.RENDER_DATA];

            _shaderUniforms[BufferNames.TESSELLATION_DATA] = _sharedShaderUniforms[PlanetRenderer.BufferNames.TESSELLATION_DATA];

            _shaderUniforms[BufferNames.WORLD_DATA] = _sharedShaderUniforms[PlanetRenderer.BufferNames.WORLD_DATA];

            _shaderUniforms[BufferNames.ALBEDO] = _sparseVirtualTexture.GetTileCache(TileCache.TileCacheType.ALBEDO);

            _shaderUniforms[BufferNames.HEIGHTMAP] = _sparseVirtualTexture.GetTileCache(TileCache.TileCacheType.HEIGHTMAP);

            _shaderUniforms[BufferNames.CONSOLIDATED_INDIRECTION_TABLE] = _sparseVirtualTexture.ConsolidatedIndirectionTable;

            _shaderUniforms[BufferNames.STATE_TABLE] = _sparseVirtualTexture.StateTable;

            CreateUniformSet();
        }

#nullable enable
        protected override void InvokeInternal(object[]? pushConstants = null)
        {
            if (pushConstants == null)
                throw new("Push constants are required");
            Span<byte> pushConstantBytes = Utilities.CollectionToBytes(pushConstants);

            long drawList = RenderingDevice.DrawListBegin(
                framebuffer: _framebuffer,
                drawFlags: RenderingDevice.DrawFlags.ClearColor1 | RenderingDevice.DrawFlags.ClearColor2,
                clearColorValues:
                [
                    new Color(0, 0, 0, 0),
                    new Color(0, 0, 0, 0),
                    new Color(-1, 0, 0, 0),
                ]
                // clearDepthValue: 0.0f,
                // clearStencilValue: 0
            );

            RenderingDevice.DrawListBindRenderPipeline(drawList, _pipeline);
            RenderingDevice.DrawListBindVertexArray(drawList, _geometry.VertexArray);
            RenderingDevice.DrawListBindIndexArray(drawList, _geometry.IndexArray);
            RenderingDevice.DrawListSetPushConstant(drawList, pushConstantBytes, (uint)pushConstantBytes.Length);
            RenderingDevice.DrawListBindUniformSet(drawList, _uniformSet, 0);
            RenderingDevice.DrawListDrawIndirect(drawList, true, _sharedShaderUniforms[PlanetRenderer.BufferNames.DRAW_DISPATCH_BUFFER].Rid);
            RenderingDevice.DrawListEnd();

            _pickingImage = GetPickingImage();
        }
#nullable disable

        public void UpdateGeometry()
        {
            CreateGeometry(_triangleMultiMesh.Mesh);
        }

        public void UpdatePipeline()
        {
            CreatePipeline();
        }

        public Rid CreatePickingTexture(Vector2I size)
        {
            return RenderingDevice.TextureCreate(new()
            {
                Width = (uint)size.X,
                Height = (uint)size.Y,
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
            }, new());
        }

        public Rid CreateLinearDepthTexture(Vector2I size)
        {
            return RenderingDevice.TextureCreate(new()
            {
                Width = (uint)size.X,
                Height = (uint)size.Y,
                Depth = 1,
                ArrayLayers = 1,
                Mipmaps = 1,
                Format = RenderingDevice.DataFormat.R32Sfloat,
                TextureType = RenderingDevice.TextureType.Type2D,
                Samples = RenderingDevice.TextureSamples.Samples1,
                UsageBits = RenderingDevice.TextureUsageBits.ColorAttachmentBit |
                            RenderingDevice.TextureUsageBits.SamplingBit |
                            RenderingDevice.TextureUsageBits.StorageBit
            }, new());
        }

        protected override void CreatePipeline()
        {
            _pipeline = RenderingDevice.RenderPipelineCreate(
                _shader,
                _framebufferFormat,
                _geometry.VertexFormat,
                RenderingDevice.RenderPrimitive.Triangles,
                new()
                {
                    CullMode = RenderingDevice.PolygonCullMode.Back,
                    Wireframe = IsWireframe,
                    LineWidth = 1.0f
                },
                new RDPipelineMultisampleState(),
                new RDPipelineDepthStencilState()
                {
                    EnableDepthTest = true,
                    EnableDepthWrite = true,
                    DepthCompareOperator = RenderingDevice.CompareOperator.GreaterOrEqual
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
                        },
                        new RDPipelineColorBlendStateAttachment
                        {
                            EnableBlend = false
                        }
                    ]
                }
            );
        }

        public void SetFramebuffer(List<(Rid Texture, string Name)> attachmentData, Vector2I size)
        {
            RDTextureFormat pickingFormat = RenderingDevice.TextureGetFormat(Picking);
            Vector2I pickingSize = new((int)pickingFormat.Width, (int)pickingFormat.Height);

            if (size != pickingSize)
            {
                if (Picking.IsValid)
                    RenderingDevice.FreeRid(Picking);

                _framebufferAttachments["picking"] = CreatePickingTexture(size);
            }

            RDTextureFormat linearDepthFormat = RenderingDevice.TextureGetFormat(LinearDepth);
            Vector2I linearDepthSize = new((int)linearDepthFormat.Width, (int)linearDepthFormat.Height);

            if (size != linearDepthSize)
            {
                if (LinearDepth.IsValid)
                    RenderingDevice.FreeRid(LinearDepth);

                _framebufferAttachments["linear_depth"] = CreateLinearDepthTexture(size);
            }

            attachmentData.Insert(1, (Picking, "picking"));
            attachmentData.Insert(2, (LinearDepth, "linear_depth"));

            CreateFramebuffer(attachmentData);
        }

        protected override void SetFramebufferProperties()
        {
            RDTextureFormat colorFormat = new()
            {
                Format = RenderingDevice.DataFormat.R32G32B32A32Sfloat,
                TextureType = RenderingDevice.TextureType.Type2D,
                Samples = RenderingDevice.TextureSamples.Samples1,
                UsageBits = RenderingDevice.TextureUsageBits.ColorAttachmentBit |
                            RenderingDevice.TextureUsageBits.CanCopyFromBit |
                            RenderingDevice.TextureUsageBits.SamplingBit |
                            RenderingDevice.TextureUsageBits.StorageBit
            };

            RDTextureFormat pickingFormat = new()
            {
                Format = RenderingDevice.DataFormat.R32G32B32A32Sfloat,
                TextureType = RenderingDevice.TextureType.Type2D,
                Samples = RenderingDevice.TextureSamples.Samples1,
                UsageBits = RenderingDevice.TextureUsageBits.ColorAttachmentBit |
                            RenderingDevice.TextureUsageBits.CanCopyFromBit |
                            RenderingDevice.TextureUsageBits.SamplingBit |
                            RenderingDevice.TextureUsageBits.CpuReadBit
            };

            RDTextureFormat linearDepthFormat = new()
            {
                Format = RenderingDevice.DataFormat.R32Sfloat,
                TextureType = RenderingDevice.TextureType.Type2D,
                Samples = RenderingDevice.TextureSamples.Samples1,
                UsageBits = RenderingDevice.TextureUsageBits.ColorAttachmentBit |
                RenderingDevice.TextureUsageBits.SamplingBit |
                RenderingDevice.TextureUsageBits.StorageBit
            };

            RDTextureFormat depthFormat = new()
            {
                Format = RenderingDevice.DataFormat.D32Sfloat,
                TextureType = RenderingDevice.TextureType.Type2D,
                Samples = RenderingDevice.TextureSamples.Samples1,
                UsageBits = RenderingDevice.TextureUsageBits.DepthStencilAttachmentBit |
                            RenderingDevice.TextureUsageBits.SamplingBit |
                            RenderingDevice.TextureUsageBits.CanCopyFromBit
            };

            RDAttachmentFormat colorAttachmentFormat = new()
            {
                Format = colorFormat.Format,
                Samples = RenderingDevice.TextureSamples.Samples1,
                UsageFlags = (uint)(
                    RenderingDevice.TextureUsageBits.ColorAttachmentBit |
                    RenderingDevice.TextureUsageBits.CanCopyFromBit |
                    RenderingDevice.TextureUsageBits.SamplingBit
                )
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

            RDAttachmentFormat linearDepthAttachmentFormat = new()
            {
                Format = linearDepthFormat.Format,
                Samples = RenderingDevice.TextureSamples.Samples1,
                UsageFlags = (uint)(
                    RenderingDevice.TextureUsageBits.ColorAttachmentBit |
                    RenderingDevice.TextureUsageBits.SamplingBit |
                    RenderingDevice.TextureUsageBits.StorageBit
                )
            };

            RDAttachmentFormat depthAttachmentFormat = new()
            {
                Format = depthFormat.Format,
                Samples = RenderingDevice.TextureSamples.Samples1,
                UsageFlags = (uint)RenderingDevice.TextureUsageBits.DepthStencilAttachmentBit
            };

            CreateFramebufferFormat([
                colorAttachmentFormat,
                pickingAttachmentFormat,
                linearDepthAttachmentFormat,
                depthAttachmentFormat
            ]);
        }

        public override void UpdateUniforms()
        {
            ((Texture2DUniform)_sharedShaderUniforms[PlanetRenderer.BufferNames.LINEAR_DEPTH_TEXTURE]).SetRid(LinearDepth);
        }

        public override void CleanupGPU()
        {
            _triangleMultiMesh.BuffersChanged -= CreateUniformSet;

            if (RenderingDevice == null)
                return;

            if (Picking.IsValid)
                RenderingDevice.FreeRid(Picking);

            if (LinearDepth.IsValid)
                RenderingDevice.FreeRid(LinearDepth);

            base.CleanupGPU();
        }

        public Vector3 GetLocalMousePosition(Vector2 mousePosition, Vector2 screenSize)
        {
            Vector2 normalizedMousePosition = mousePosition / screenSize;

            if (_pickingImage == null)
                return Vector3.Inf;

            Vector2I pixelPosition = new(
                Mathf.Clamp((int)(normalizedMousePosition.X * _pickingImage.GetWidth()), 0, _pickingImage.GetWidth() - 1),
                Mathf.Clamp((int)(normalizedMousePosition.Y * _pickingImage.GetHeight()), 0, _pickingImage.GetHeight() - 1)
            );

            Color pickingData = _pickingImage.GetPixelv(pixelPosition);

            if (pickingData.A <= 0)
                return Vector3.Inf;

            return new(pickingData.R, pickingData.G, pickingData.B);
        }
    }
}