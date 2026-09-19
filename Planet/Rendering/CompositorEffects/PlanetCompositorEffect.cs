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

    public PlanetCompositorEffect(SparseVirtualTexture sparseVirtualTexture, MultiMeshRD triangleMultiMesh, Dictionary<PlanetRenderer.BufferNames, ShaderUniform> shaderedShaderUniforms)
    {
        PlanetRenderPass = new(sparseVirtualTexture, triangleMultiMesh, shaderedShaderUniforms);
        EffectCallbackType = EffectCallbackTypeEnum.PostSky;
    }

    public override void _RenderCallback(int effectCallbackType, RenderData renderData)
    {
        RenderSceneBuffersRD renderSceneBuffers = renderData.GetRenderSceneBuffers() as RenderSceneBuffersRD;
        RenderSceneData sceneData = renderData.GetRenderSceneData();

        
        Rid color = renderSceneBuffers.GetColorTexture();
        Rid depth = renderSceneBuffers.GetDepthTexture();

        PlanetRenderPass.SetFramebuffer([
            (color, "color"),
            (depth, "depth")
        ]);

        Transform3D cameraTransform = sceneData.GetCamTransform();
        Projection cameraProjection = sceneData.GetCamProjection();
        Projection viewProjectionMatrix = CustomCamera.GetViewProjectionMatrix(cameraTransform, cameraProjection);

        PlanetRenderPass.Invoke(Utilities.ToViewPushConstants(
            viewProjectionMatrix,
            cameraTransform.Origin,
            Mathf.Tan(Mathf.DegToRad(cameraProjection.GetFov()) / 2)
        ));
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

    public bool IsValidForProcessing()
    {
        return PlanetRenderPass?.IsValid() == true;
    }
}