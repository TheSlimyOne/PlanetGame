using System;
using Godot;
using System.Collections.Generic;
using Uniform;
using PlanetGame.Planet.Rendering;
using PlanetGame.Planet.Rendering.VirtualTexturing;

namespace PlanetGame.Shaders.RenderPasses
{
    public partial class PlanetRenderPass : RenderPass<PlanetRenderPass.BufferNames>
    {
        public bool IsWireframe;
        private static ShaderProgramPaths _shaderPath = new() { Vertex = ShaderPaths.PLANET_VERTEX, Fragment = ShaderPaths.PLANET_FRAGMENT };

        public Rid Color => _framebufferAttachments["color"];
        public Rid Depth => _framebufferAttachments["depth"];
        
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
            CONSOLIDATED_INDIRECTION_TABLE
        }



        public PlanetRenderPass(SparseVirtualTexture sparseVirtualTexture, MultiMeshRD triangleMultiMesh, Dictionary<PlanetRenderer.BufferNames, ShaderUniform> shaderedShaderUniforms) : base(_shaderPath)
        {
            _sharedShaderUniforms = shaderedShaderUniforms;
            _sparseVirtualTexture = sparseVirtualTexture;
            _triangleMultiMesh = triangleMultiMesh;

            SetupShader(_triangleMultiMesh.Mesh);

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

            _shaderUniforms[BufferNames.ALBEDO] = new Texture2DUniform(this, (int)BufferNames.ALBEDO, _sparseVirtualTexture.GetTileCache(TileCache.TileCacheType.ALBEDO).GetRdRid(), RenderingDevice.UniformType.SamplerWithTexture, true);

            _shaderUniforms[BufferNames.HEIGHTMAP] = new Texture2DUniform(this, (int)BufferNames.HEIGHTMAP, _sparseVirtualTexture.GetTileCache(TileCache.TileCacheType.HEIGHTMAP).GetRdRid(), RenderingDevice.UniformType.SamplerWithTexture, true);

            _shaderUniforms[BufferNames.CONSOLIDATED_INDIRECTION_TABLE] = new Texture2DUniform(this, (int)BufferNames.CONSOLIDATED_INDIRECTION_TABLE, _sparseVirtualTexture.ConsolidatedIndirectionTable.GetRdRid(), RenderingDevice.UniformType.SamplerWithTexture, true);

            CreateUniformSet();
        }

#nullable enable
        public override void Invoke(byte[]? pushConstants = null)
        {
            long drawList = RenderingDevice.DrawListBegin(
                framebuffer: _framebuffer,
                drawFlags: RenderingDevice.DrawFlags.ClearDepth,
                clearColorValues: [],
                clearDepthValue: 0.0f,
                clearStencilValue: 0
            );


            RenderingDevice.DrawListBindRenderPipeline(drawList, _pipeline);
            RenderingDevice.DrawListBindVertexArray(drawList, _geometry.VertexArray);
            RenderingDevice.DrawListBindIndexArray(drawList, _geometry.IndexArray);

            if (pushConstants != null)
                RenderingDevice.DrawListSetPushConstant(drawList, pushConstants, (uint)pushConstants.Length);

            RenderingDevice.DrawListBindUniformSet(drawList, _uniformSet, 0);
            RenderingDevice.DrawListDrawIndirect(drawList, true, _sharedShaderUniforms[PlanetRenderer.BufferNames.DRAW_DISPATCH_BUFFER].Rid);
            RenderingDevice.DrawListEnd();
        }


        public void UpdatePipeline()
        {
            CreatePipeline();
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
                    ]
                }
            );
        }

        public void SetFramebuffer(List<(Rid Texture, string Name)> attachmentData)
        {
            CreateFramebuffer(attachmentData);
        }

        protected override void SetFramebufferProperties()
        {
            RDTextureFormat textureFormat = new()
            {
                TextureType = RenderingDevice.TextureType.Type2D,
                Format = RenderingDevice.DataFormat.R32G32B32A32Sfloat,
                Samples = RenderingDevice.TextureSamples.Samples1,
                UsageBits = RenderingDevice.TextureUsageBits.ColorAttachmentBit |
                            RenderingDevice.TextureUsageBits.CanCopyFromBit |
                            RenderingDevice.TextureUsageBits.SamplingBit |
                            RenderingDevice.TextureUsageBits.StorageBit
            };

            RDTextureFormat depthFormat = new()
            {
                Format = RenderingDevice.DataFormat.D32Sfloat,
                TextureType = RenderingDevice.TextureType.Type2D,
                Samples = RenderingDevice.TextureSamples.Samples1,
                UsageBits =
                    RenderingDevice.TextureUsageBits.DepthStencilAttachmentBit |
                    RenderingDevice.TextureUsageBits.SamplingBit |
                    RenderingDevice.TextureUsageBits.CanCopyFromBit

            };

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

            CreateFramebufferFormat([colorAttachmentFormat, depthAttachmentFormat]);
        }

        public override void UpdateUniforms()
        {
            throw new NotImplementedException();
        }

        public override void CleanupGPU()
        {
            _triangleMultiMesh.BuffersChanged -= CreateUniformSet;

            if (RenderingDevice == null)
                return;

            if (Color.IsValid)
                RenderingDevice.FreeRid(Color);
            if (Depth.IsValid)
                RenderingDevice.FreeRid(Depth);
         
            base.CleanupGPU();
        }
    }
}