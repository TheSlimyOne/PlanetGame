using System;
using System.Collections.Generic;
using Godot;
using PlanetGame.Rendering.Surface;
using PlanetGame.Rendering.VirtualTexturing;
using PlanetGame.Shaders.Dispatchers;
using Uniform;

namespace PlanetGame.Shaders.RenderPasses;

public partial class PostProcessingPass : RenderPass<PostProcessingPass.BufferNames>
{
    public TerrainTessellator TerrainTessellator { get; }
    public SparseVirtualTexture SparseVirtualTexture { get; }

    private Rid _shadowTexture;

    public Rid ShadowTexture => _shadowTexture;
    public DirectionalLight3D Sun { get; private set; }

    public enum BufferNames
    {
        MULTIMESH_BUFFER,
        EXTERNAL_DATA,
        HEIGHT_MAP,
        INDIRECTION_TABLE,

    }

    public PostProcessingPass(
        TerrainTessellator terrainTessellator,
        SparseVirtualTexture sparseVirtualTexture,
        DirectionalLight3D sun,
        Vector2I viewSize,
        Mesh mesh
    ) : base(
        new()
        {
            Vertex = ShaderPaths.PLANET_TESSELLATION_VERTEX,
            Fragment = ShaderPaths.EMPTY_FRAGMENT
        },
        viewSize
    )
    {
        TerrainTessellator = terrainTessellator;
        SparseVirtualTexture = sparseVirtualTexture;

        Sun = sun;

        SetupShader(mesh);
    }

    public override void CreateUniforms()
    {
        _renderShaderUniforms = new Dictionary<Enum, ShaderUniform>
        {
            [BufferNames.MULTIMESH_BUFFER] =
                TerrainTessellator.ExecuteTessellationPass[
                    ExecuteTessellationPassDispatcher.BufferNames.MULTIMESH_BUFFER
                ],

            [BufferNames.EXTERNAL_DATA] =
                TerrainTessellator.ExecuteTessellationPass[
                    ExecuteTessellationPassDispatcher.BufferNames.EXTERNAL_DATA
                ],

            [BufferNames.HEIGHT_MAP] = new Texture2DUniform(
                this,
                (int)BufferNames.HEIGHT_MAP,
                SparseVirtualTexture.HeightTileCache.GetRdRid(),
                RenderingDevice.UniformType.SamplerWithTexture,
                true
            ),

            [BufferNames.INDIRECTION_TABLE] = new Texture2DUniform(
                this,
                (int)BufferNames.INDIRECTION_TABLE,
                SparseVirtualTexture.IndirectionTable.GetRdRid(),
                RenderingDevice.UniformType.SamplerWithTexture,
                true
            )
        };

        CreateUniformSet();
    }

#nullable enable
    public override void Invoke(byte[]? pushConstants = null)
    {
        long drawList = RenderingDevice.DrawListBegin(
            _framebuffer,
            RenderingDevice.DrawFlags.ClearDepth,
            [],
            1.0f,
            0
        );

        RenderingDevice.DrawListBindRenderPipeline(drawList, _pipeline);
        RenderingDevice.DrawListBindVertexArray(drawList, _geometry.VertexArray);
        RenderingDevice.DrawListBindIndexArray(drawList, _geometry.IndexArray);
        RenderingDevice.DrawListBindUniformSet(drawList, _uniformSet, 0);
        RenderingDevice.DrawListDrawIndirect(drawList, true, TerrainTessellator.PrepareTessellationPass[PrepareTessellationPassDispatcher.BufferNames.DRAW_DISPATCH_BUFFER].Rid);
        RenderingDevice.DrawListEnd();
    }

    protected override Rid CreatePipeline()
    {
        return RenderingDevice.RenderPipelineCreate(
            _shader,
            _framebufferFormat,
            _geometry.VertexFormat,
            RenderingDevice.RenderPrimitive.Triangles,
            new RDPipelineRasterizationState
            {
                CullMode = RenderingDevice.PolygonCullMode.Back,
                Wireframe = false,
                LineWidth = 1.0f
            },
            new RDPipelineMultisampleState(),
            new RDPipelineDepthStencilState
            {
                EnableDepthTest = true,
                EnableDepthWrite = true,
                DepthCompareOperator =
                    RenderingDevice.CompareOperator.Less
            },
            new RDPipelineColorBlendState()
        );
    }

    protected override Rid CreateFramebuffer()
    {
        RDTextureFormat textureFormat = new()
        {
            Width = (uint)ViewSize.X,
            Height = (uint)ViewSize.Y,
            Format = RenderingDevice.DataFormat.D32Sfloat,
            TextureType = RenderingDevice.TextureType.Type2D,
            Samples = RenderingDevice.TextureSamples.Samples1,
            UsageBits = RenderingDevice.TextureUsageBits.DepthStencilAttachmentBit |
                        RenderingDevice.TextureUsageBits.SamplingBit
        };

        _shadowTexture = RenderingDevice.TextureCreate(
            textureFormat,
            new RDTextureView()
        );

        RDAttachmentFormat depthAttachmentFormat = new()
        {
            Format = RenderingDevice.DataFormat.D32Sfloat,
            Samples = RenderingDevice.TextureSamples.Samples1,
            UsageFlags = (uint)(
                RenderingDevice.TextureUsageBits.DepthStencilAttachmentBit |
                RenderingDevice.TextureUsageBits.SamplingBit
            )
        };

        _framebufferFormat = RenderingDevice.FramebufferFormatCreate(
            [depthAttachmentFormat]
        );

        return RenderingDevice.FramebufferCreate(
            [_shadowTexture]
        );
    }

    public override void UpdateUniforms()
    {
    }

    public override void CleanupGPU()
    {
        if (RenderingDevice == null) return;

        if (_shadowTexture.IsValid)
            RenderingDevice.FreeRid(_shadowTexture);

        _shadowTexture = default;

        base.CleanupGPU();
    }
}