using System.Collections.Generic;
using Godot;
using PlanetGame.Planet.Rendering;
using PlanetGame.Planet.Rendering.VirtualTexturing;
using PlanetGame.Shaders.RenderPasses;
using PlanetGame.Util;
using Uniform;

[Tool]
public partial class PlanetCompositorEffect : CompositorEffect
{
    public PlanetRenderPass PlanetRenderPass { get; private set; }
    public bool Ready { get; private set; } = true;
    public bool Paused = false;

    private readonly Dictionary<PlanetRenderer.BufferNames, ShaderUniform> _sharedShaderUniforms;

    public PlanetCompositorEffect(SparseVirtualTexture sparseVirtualTexture, MultiMeshRD triangleMultiMesh, Dictionary<PlanetRenderer.BufferNames, ShaderUniform> sharedShaderUniforms)
    {
        _sharedShaderUniforms = sharedShaderUniforms;
        PlanetRenderPass = new(sparseVirtualTexture, triangleMultiMesh, _sharedShaderUniforms);
        EffectCallbackType = EffectCallbackTypeEnum.PostSky;
    }

    public override void _RenderCallback(int effectCallbackType, RenderData renderData)
    {
        RenderSceneBuffersRD renderSceneBuffers = renderData.GetRenderSceneBuffers() as RenderSceneBuffersRD;
        RenderSceneData sceneData = renderData.GetRenderSceneData();
        Vector2I size = renderSceneBuffers.GetInternalSize();

        
        Rid color = renderSceneBuffers.GetColorTexture();
        Rid depth = renderSceneBuffers.GetDepthTexture();

        PlanetRenderPass.SetFramebuffer([
            (color, "color"),
            (depth, "depth"),
        ], size);

        PlanetRenderPass.UpdateUniforms();

        Transform3D cameraTransform = sceneData.GetCamTransform();
        Projection cameraProjection = sceneData.GetCamProjection();
        Projection viewProjectionMatrix = CustomCamera.GetViewProjectionMatrix(cameraTransform, cameraProjection);


        PlanetRenderPass.Invoke([
            viewProjectionMatrix,
            cameraTransform.Origin,
            Mathf.Tan(Mathf.DegToRad(cameraProjection.GetFov()) / 2)
        ]);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete)
            CleanupGPUResources();
    }

    public void CreateUniforms()
    {
        PlanetRenderPass.CreateUniforms();
    }

    public void CleanupGPUResources()
    {
        PlanetRenderPass?.CleanupGPU();
    }
}