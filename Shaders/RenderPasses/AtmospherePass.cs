using System;
using Godot;
using System.Collections.Generic;
using Uniform;
using PlanetGame.Planet.Rendering;
using PlanetGame.Util;
using PlanetGame.Data;

namespace PlanetGame.Shaders.RenderPasses
{
    public partial class AtmospherePass : RenderPass<AtmospherePass.BufferNames>
    {
        private static ShaderProgramPaths _shaderPath = new() { Vertex = ShaderPaths.ATMOSPHERE_VERTEX, Fragment = ShaderPaths.ATMOSPHERE_FRAGMENT };
        public static AtmosphereData AtmosphereData => SaveManager.AtmosphereData;
        public bool IsWireframe;

        private readonly Dictionary<PlanetRenderer.BufferNames, ShaderUniform> _sharedShaderUniforms;
        // private Mesh _atmosphereMesh = new SphereMesh() { Radius = 0.5f, Height = 1, RadialSegments = 16, Rings = 8 };
        // private Mesh _atmosphereMesh = Data.Key.GetTriangleMesh(5); //new BoxMesh() { Size = new Vector3(100, 100, 100) };
        private Mesh _atmosphereMesh = new BoxMesh() { Size = Vector3.One };

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

            _shaderUniforms[BufferNames.ATMOSPHERE_DATA] = new StorageBufferUniform(BufferNames.ATMOSPHERE_DATA.ToString(), this, RenderingDevice,
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
                // drawFlags: RenderingDevice.DrawFlags.ClearColorAll | RenderingDevice.DrawFlags.ClearDepth,
                // clearColorValues:
                // [
                //     new Color(0, 0, 0, 0)
                // ],
                clearDepthValue: 0.0f,
                clearStencilValue: 0
            );

            RenderingDevice.DrawListBindRenderPipeline(drawList, _pipeline);
            RenderingDevice.DrawListBindVertexArray(drawList, _geometry.VertexArray);
            RenderingDevice.DrawListBindIndexArray(drawList, _geometry.IndexArray);
            RenderingDevice.DrawListSetPushConstant(drawList, pushConstantBytes, (uint)pushConstantBytes.Length);
            RenderingDevice.DrawListBindUniformSet(drawList, _uniformSet, 0);
            RenderingDevice.DrawListDraw(drawList, true, 1);
            RenderingDevice.DrawListEnd();
        }
#nullable disable

        public override void UpdateUniforms()
        {
            GetUniform<StorageBufferUniform>(BufferNames.ATMOSPHERE_DATA).UpdateUniform(AtmosphereData.ToBytes());
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
                    CullMode = RenderingDevice.PolygonCullMode.Disabled,
                    Wireframe = IsWireframe,
                    LineWidth = 1.0f
                },
                new RDPipelineMultisampleState(),
                new RDPipelineDepthStencilState()
                {
                    EnableDepthTest = true,
                    EnableDepthWrite = false,
                    DepthCompareOperator = RenderingDevice.CompareOperator.GreaterOrEqual
                },
                new()
                {
                    Attachments =
                    [
                        new RDPipelineColorBlendStateAttachment
                        {
                            EnableBlend = true,
                            SrcColorBlendFactor = RenderingDevice.BlendFactor.SrcAlpha,
                            DstColorBlendFactor = RenderingDevice.BlendFactor.OneMinusSrcAlpha,
                            SrcAlphaBlendFactor = RenderingDevice.BlendFactor.SrcAlpha,
                            DstAlphaBlendFactor = RenderingDevice.BlendFactor.OneMinusSrcAlpha
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