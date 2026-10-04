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

    public DirectionalLight3D Sun;


    public PlanetCompositorEffect(DirectionalLight3D sun, SparseVirtualTexture sparseVirtualTexture, MultiMeshRD triangleMultiMesh, Dictionary<PlanetRenderer.BufferNames, ShaderUniform> sharedShaderUniforms)
    {
        Sun = sun;
        PlanetRenderPass = new(sparseVirtualTexture, triangleMultiMesh, sharedShaderUniforms);
        EffectCallbackType = EffectCallbackTypeEnum.PostSky;
    }

    public override void _RenderCallback(int effectCallbackType, RenderData renderData)
    {
        if (PlanetController.Quiting)
            return;
            
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


        Transform3D rotationOnlyView = new(cameraTransform.Basis.Inverse(), Vector3.Zero);

        PlanetRenderPass.Invoke([
            // cameraProjection * new Projection(rotationOnlyView),
            viewProjectionMatrix,
            cameraTransform.Origin,
            Mathf.Tan(Mathf.DegToRad(cameraProjection.GetFov()) / 2),
            VectorUtils.ToVector4(Sun.Basis.Z.Normalized(), 0)
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