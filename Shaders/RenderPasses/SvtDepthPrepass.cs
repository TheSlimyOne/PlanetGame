using System;
using System.Collections.Generic;
using Godot;
using PlanetGame.Rendering.Surface;
using PlanetGame.Rendering.VirtualTexturing;
using PlanetGame.Shaders.Dispatchers;
using Uniform;

namespace PlanetGame.Shaders.RenderPasses;

public partial class SvtDepthPrepass : RenderPass<SvtDepthPrepass.BufferNames>
{
    public TerrainTessellator TerrainTessellator { get; }
    public SparseVirtualTexture SparseVirtualTexture { get; }

    private Rid _depthBuffer;

    public enum BufferNames
    {
        MULTIMESH_BUFFER,
        EXTERNAL_DATA,
        HEIGHT_MAP,
        INDIRECTION_TABLE,
        STATE_TABLE
    }

    public SvtDepthPrepass(
        TerrainTessellator terrainTessellator,
        SparseVirtualTexture sparseVirtualTexture,
        Rid depthBuffer,
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
        _depthBuffer = depthBuffer;

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
            ),

            [BufferNames.STATE_TABLE] = new Texture2DUniform(
                this,
                (int)BufferNames.STATE_TABLE,
                SparseVirtualTexture.StateTable.GetRdRid(),
                RenderingDevice.UniformType.Image,
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
        RenderingDevice.DrawListBindVertexArray(
            drawList,
            _geometry.VertexArray
        );
        RenderingDevice.DrawListBindIndexArray(
            drawList,
            _geometry.IndexArray
        );
        RenderingDevice.DrawListBindUniformSet(
            drawList,
            _uniformSet,
            0
        );

        RenderingDevice.DrawListDrawIndirect(
            drawList,
            true,
            TerrainTessellator.PrepareTessellationPass[
                PrepareTessellationPassDispatcher.BufferNames.DRAW_DISPATCH_BUFFER
            ].Rid
        );

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
        RDAttachmentFormat depthAttachmentFormat = new()
        {
            Format = RenderingDevice.DataFormat.D32Sfloat,
            Samples = RenderingDevice.TextureSamples.Samples1,
            UsageFlags = (uint)(
                RenderingDevice.TextureUsageBits.DepthStencilAttachmentBit |
                RenderingDevice.TextureUsageBits.SamplingBit |
                RenderingDevice.TextureUsageBits.CanCopyFromBit
            )
        };

        _framebufferFormat = RenderingDevice.FramebufferFormatCreate(
            [depthAttachmentFormat]
        );

        return RenderingDevice.FramebufferCreate(
            [_depthBuffer]
        );
    }

    public override void UpdateUniforms()
    {
    }

    public override void CleanupGPU()
    {
        if (RenderingDevice == null)
            return;

        _depthBuffer = default;

        base.CleanupGPU();
    }
}