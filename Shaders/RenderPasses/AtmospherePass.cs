using System;
using Godot;
using System.Collections.Generic;
using Uniform;
using PlanetGame.Planet.Rendering;
using PlanetGame.Planet.Rendering.VirtualTexturing;
using PlanetGame.Util;
using PlanetGame.Data;

namespace PlanetGame.Shaders.RenderPasses
{
    public partial class AtmospherePass : RenderPass<AtmospherePass.BufferNames>
    {
        public static AtmosphereData AtmosphereData => SaveManager.AtmosphereData;
        private static ShaderProgramPaths _shaderPath = new() { Vertex = ShaderPaths.ATMOSPHERE_VERTEX, Fragment = ShaderPaths.ATMOSPHERE_FRAGMENT };

        public bool IsWireframe;

        private readonly Dictionary<PlanetRenderer.BufferNames, ShaderUniform> _sharedShaderUniforms;
        private Mesh _atmosphereMesh = new SphereMesh() { Radius = AtmosphereData.Radius, Height = 2 * AtmosphereData.Radius };

        public enum BufferNames
        {

            ATMOSPHERE_DATA,
            WORLD_DATA,
        }

        public AtmospherePass(Dictionary<PlanetRenderer.BufferNames, ShaderUniform> sharedShaderUniforms) : base(RenderingServer.GetRenderingDevice(), _shaderPath)
        {
            _sharedShaderUniforms = sharedShaderUniforms;
            SetupShader(_atmosphereMesh);
        }

        public override void CreateUniforms()
        {
            _shaderUniforms = [];

            _shaderUniforms[BufferNames.ATMOSPHERE_DATA] = new StorageBufferUniform(this, RenderingDevice,
                AtmosphereData.ToBytes()
            );

            _shaderUniforms[BufferNames.WORLD_DATA] = _sharedShaderUniforms[PlanetRenderer.BufferNames.WORLD_DATA];

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
                drawFlags: RenderingDevice.DrawFlags.ClearColorAll | RenderingDevice.DrawFlags.ClearDepth,
                clearColorValues:
                [
                    new Color(0, 0, 0, 0),
                ],
                clearDepthValue: 0.0f,
                clearStencilValue: 0
            );

            RenderingDevice.DrawListBindRenderPipeline(drawList, _pipeline);
            RenderingDevice.DrawListBindVertexArray(drawList, _geometry.VertexArray);
            RenderingDevice.DrawListBindIndexArray(drawList, _geometry.IndexArray);
            RenderingDevice.DrawListSetPushConstant(drawList, pushConstantBytes, (uint)pushConstantBytes.Length);
            RenderingDevice.DrawListBindUniformSet(drawList, _uniformSet, 0);
            RenderingDevice.DrawListDrawIndirect(drawList, true, _sharedShaderUniforms[PlanetRenderer.BufferNames.DRAW_DISPATCH_BUFFER].Rid);
            RenderingDevice.DrawListEnd();
        }
#nullable disable

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
            CreateFramebuffer(attachmentData);
        }

        protected override void SetFramebufferProperties()
        {
            RDTextureFormat textureFormat = new()
            {
                Format = RenderingDevice.DataFormat.R32G32B32A32Sfloat,
                TextureType = RenderingDevice.TextureType.Type2D,
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
                UsageBits = RenderingDevice.TextureUsageBits.DepthStencilAttachmentBit |
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

            CreateFramebufferFormat([
                colorAttachmentFormat,
                depthAttachmentFormat
            ]);
        }

        public override void CleanupGPU()
        {
            _atmosphereMesh = default;
            base.CleanupGPU();
        }

        public override bool IsValid() => 
            RenderingDevice != null && 
            RenderingDevice.UniformSetIsValid(_uniformSet) && 
            _shader.IsValid && RenderingDevice.RenderPipelineIsValid(_pipeline);   
    }
}