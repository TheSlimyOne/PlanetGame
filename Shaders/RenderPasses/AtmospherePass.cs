using System;
using System.Collections.Generic;
using Godot;
using PlanetGame.Planet.Rendering;
using PlanetGame.Util;
using PlanetGame.Data;
using Uniform;

namespace PlanetGame.Shaders.RenderPasses
{
    public partial class AtmospherePass : RenderPass<AtmospherePass.BufferNames>
    {
        private static ShaderProgramPaths _shaderPath = new() { Vertex = ShaderPaths.ATMOSPHERE_VERTEX, Fragment = ShaderPaths.ATMOSPHERE_FRAGMENT };
        private static Data.RenderData RenderData => SaveManager.RenderData;
        private static AtmosphereData AtmosphereData => SaveManager.AtmosphereData;
        private static WorldData WorldData => SaveManager.WorldData;

        public Rid Color => _framebufferAttachments["color"];

        private readonly Dictionary<PlanetRenderer.BufferNames, ShaderUniform> _sharedShaderUniforms;

        public enum BufferNames
        {
            DEPTH_TEXTURE,
            ATMOSPHERE_DATA,
            WORLD_DATA,

        }

        public AtmospherePass(Dictionary<PlanetRenderer.BufferNames, ShaderUniform> shaderedShaderUniforms) : base(_shaderPath)
        {
            _sharedShaderUniforms = shaderedShaderUniforms;
            SetupShader(new QuadMesh());
        }

        public override void CreateUniforms()
        {
            _shaderUniforms = [];

            _shaderUniforms[BufferNames.DEPTH_TEXTURE] = new Texture2DUniform(this, RenderingDevice, (int)BufferNames.DEPTH_TEXTURE,
                RenderingDevice.UniformType.SamplerWithTexture,
                perserved: true
            );

            _shaderUniforms[BufferNames.ATMOSPHERE_DATA] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.ATMOSPHERE_DATA,
				AtmosphereData.ToBytes()
			);

            _shaderUniforms[BufferNames.WORLD_DATA] = _sharedShaderUniforms[PlanetRenderer.BufferNames.WORLD_DATA];
        }

#nullable enable
        public override void Invoke(byte[]? pushConstants = null)
        {
            long drawList = RenderingDevice.DrawListBegin(
                _framebuffer,
                RenderingDevice.DrawFlags.ClearAll,
                [Colors.Black]
            );

            RenderingDevice.DrawListBindRenderPipeline(drawList, _pipeline);

            if (pushConstants != null)
                RenderingDevice.DrawListSetPushConstant(drawList, pushConstants, (uint)pushConstants.Length);

            RenderingDevice.DrawListBindUniformSet(drawList, _uniformSet, 0);
            RenderingDevice.DrawListBindVertexArray(drawList, _geometry.VertexArray);
            RenderingDevice.DrawListBindIndexArray(drawList, _geometry.IndexArray);
            RenderingDevice.DrawListDraw(drawList, true, 1);

            RenderingDevice.DrawListEnd();
        }

        public void UpdatePipeline()
        {
            CreatePipeline();
        }

        protected override void CreatePipeline()
        {
            RDPipelineColorBlendState blendState = new();

            blendState.Attachments.Add(
                new RDPipelineColorBlendStateAttachment
                {
                    EnableBlend = true,

                    SrcColorBlendFactor = RenderingDevice.BlendFactor.SrcAlpha,
                    DstColorBlendFactor = RenderingDevice.BlendFactor.OneMinusSrcAlpha,
                    ColorBlendOp = RenderingDevice.BlendOperation.Add,

                    SrcAlphaBlendFactor = RenderingDevice.BlendFactor.One,
                    DstAlphaBlendFactor = RenderingDevice.BlendFactor.OneMinusSrcAlpha,
                    AlphaBlendOp = RenderingDevice.BlendOperation.Add
                }
            );

            _pipeline = RenderingDevice.RenderPipelineCreate(
                _shader,
                _framebufferFormat,
                _geometry.VertexFormat,
                RenderingDevice.RenderPrimitive.Triangles,
                new RDPipelineRasterizationState
                {
                    CullMode = RenderingDevice.PolygonCullMode.Disabled
                },
                new RDPipelineMultisampleState(),
                new RDPipelineDepthStencilState
                {
                    EnableDepthTest = false,
                    EnableDepthWrite = false
                },
                blendState
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

            CreateFramebufferFormat([colorAttachmentFormat]);
        }

        public override void UpdateUniforms()
        {
            _shaderUniforms[BufferNames.ATMOSPHERE_DATA].UpdateUniform(
				AtmosphereData.ToBytes()
			);
        }

        public override void CleanupGPU()
        {
            if (RenderingDevice == null)
                return;

            if (Color.IsValid)
                RenderingDevice.FreeRid(Color);

            base.CleanupGPU();
        }

        public void SetDepthUniform(Rid depth)
        {
            // _shaderUniforms[BufferNames.DEPTH_TEXTURE] = new
           
            Texture2DUniform uniform = GetUniform<Texture2DUniform>(BufferNames.DEPTH_TEXTURE);
            if (depth != uniform.Rid)
            {
                GetUniform<Texture2DUniform>(BufferNames.DEPTH_TEXTURE).SetRid(depth);
                CreateUniformSet();
            }
        }
    }
}