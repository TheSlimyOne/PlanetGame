using System;
using Godot;
using System.Collections.Generic;
using Uniform;
using PlanetGame.Planet.Rendering;
using PlanetGame.Util;
using PlanetGame.Data;
using static PlanetGame.Shaders.RenderPasses.AtmospherePass.BufferNames;
using static PlanetGame.Shaders.RenderPasses.AtmospherePass.BufferSets;


namespace PlanetGame.Shaders.RenderPasses
{
    public partial class AtmospherePass : RenderPass<AtmospherePass.BufferNames, AtmospherePass.BufferSets>
    {
        private static ShaderProgramPaths _shaderPath = new() { Vertex = ShaderPaths.ATMOSPHERE_VERTEX, Fragment = ShaderPaths.ATMOSPHERE_FRAGMENT };
        public static AtmosphereData AtmosphereData => SaveManager.AtmosphereData;
        public bool IsWireframe;

        private readonly Dictionary<PlanetRenderer.BufferNames, ShaderUniform> _sharedShaderUniforms;
        // private Mesh _atmosphereMesh = new SphereMesh() { Radius = 0.5f, Height = 1, RadialSegments = 16, Rings = 8 };
        // private Mesh _atmosphereMesh = Data.Key.GetTriangleMesh(5); //new BoxMesh() { Size = new Vector3(100, 100, 100) };
        private Mesh _atmosphereMesh = new BoxMesh() { Size = Vector3.One };

        public enum BufferSets
        {
            DEFAULT = 0,
        }

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

            this[0, ATMOSPHERE_DATA, DEFAULT] = new StorageBufferUniform(ATMOSPHERE_DATA.ToString(), this, RenderingDevice,
                AtmosphereData.ToBytes()
            );

            this[1, WORLD_DATA, DEFAULT] = _sharedShaderUniforms[PlanetRenderer.BufferNames.WORLD_DATA];

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
            DrawListBindUniformSets(drawList);
            RenderingDevice.DrawListDraw(drawList, true, 1);
            RenderingDevice.DrawListEnd();
        }
#nullable disable

        public override void UpdateUniforms()
        {
            GetUniform<StorageBufferUniform>(ATMOSPHERE_DATA).UpdateUniform(AtmosphereData.ToBytes());
        }

        public void UpdatePipeline()
        {
            CreatePipeline();
        }

        protected override void CreatePipeline()
        {
            // FreePipeline();
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

        protected override void CleanupGPUInternal()
        {
            _atmosphereMesh = default;
            base.CleanupGPUInternal();
        }
    }
}