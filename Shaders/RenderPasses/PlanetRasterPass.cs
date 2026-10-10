using System;
using Godot;
using System.Collections.Generic;
using Uniform;
using PlanetGame.Planet.Rendering;
using PlanetGame.Planet.Rendering.VirtualTexturing;
using PlanetGame.Util;

using static PlanetGame.Shaders.RenderPasses.PlanetRenderPass.BufferNames;
using static PlanetGame.Shaders.RenderPasses.PlanetRenderPass.BufferSets;

namespace PlanetGame.Shaders.RenderPasses
{
    public partial class PlanetRenderPass : RenderPass<PlanetRenderPass.BufferNames, PlanetRenderPass.BufferSets>
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

        private MultiMeshRD _triangleMultiMesh;
        private readonly Dictionary<PlanetRenderer.BufferNames, ShaderUniform> _sharedShaderUniforms;
        private Action _onMeshChange;
        private readonly SparseVirtualTexture _sparseVirtualTexture;


        public enum BufferSets
        {
            DEFAULT,
            MESH,
        }
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

            _onMeshChange = () => CreateUniformSet(MESH);
            _triangleMultiMesh.BuffersChanged += _onMeshChange;
        }

        public override void CreateUniforms()
        {

            this[0, VIRTUAL_TEXTURE_DATA, DEFAULT] = _sharedShaderUniforms[PlanetRenderer.BufferNames.VIRTUAL_TEXTURE_DATA];

            this[1, RENDER_DATA, DEFAULT] = _sharedShaderUniforms[PlanetRenderer.BufferNames.RENDER_DATA];

            this[2, TESSELLATION_DATA, DEFAULT] = _sharedShaderUniforms[PlanetRenderer.BufferNames.TESSELLATION_DATA];

            this[3, WORLD_DATA, DEFAULT] = _sharedShaderUniforms[PlanetRenderer.BufferNames.WORLD_DATA];

            this[4, ALBEDO, DEFAULT] = _sparseVirtualTexture.GetTileCache(TileCache.TileCacheType.ALBEDO);

            this[5, HEIGHTMAP, DEFAULT] = _sparseVirtualTexture.GetTileCache(TileCache.TileCacheType.HEIGHTMAP);

            this[6, CONSOLIDATED_INDIRECTION_TABLE, DEFAULT] = _sparseVirtualTexture.ConsolidatedIndirectionTable;

            this[7, STATE_TABLE, DEFAULT] = _sparseVirtualTexture.StateTable;

            this[0, MULTIMESH_BUFFER, MESH] = _triangleMultiMesh.BufferUniform;

            CreateUniformSets();
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
            DrawListBindUniformSets(drawList);
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
            FreePipeline();
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

        protected override void CleanupGPUInternal()
        {
            _triangleMultiMesh.BuffersChanged -= _onMeshChange;

            if (RenderingDevice == null)
                return;
            
            if (Picking.IsValid)
                RenderingDevice.FreeRid(Picking);

            if (LinearDepth.IsValid)
                RenderingDevice.FreeRid(LinearDepth);
                
            base.CleanupGPUInternal();
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