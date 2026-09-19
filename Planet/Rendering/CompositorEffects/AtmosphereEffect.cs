using System.Collections.Generic;
using Godot;
using PlanetGame.Data;
using PlanetGame.Planet.Rendering;
using PlanetGame.Shaders.RenderPasses;
using PlanetGame.Util;
using PlanetGame.Util.DebugUIComponents;
using Uniform;

[Tool]
public partial class AtmosphereEffect : CompositorEffect
{
    public AtmospherePass AtmospherePass { get; private set; }
    private static AtmosphereData AtmosphereData => SaveManager.AtmosphereData;
    private static WorldData WorldData => SaveManager.WorldData;
    public bool Ready { get; private set; } = true;
    public bool Paused = false;

    public AtmosphereEffect(Dictionary<PlanetRenderer.BufferNames, ShaderUniform> shaderedShaderUniforms)
    {
        AtmospherePass = new(shaderedShaderUniforms);

        EffectCallbackType = EffectCallbackTypeEnum.PostSky;
        AccessResolvedDepth = true;

        DebugMenuController.Instance.AddSection("Atmosphere", 0, false, null, 400);
        DebugMenuController.Instance.AddSlider("Atmosphere Radius", "Atmosphere", () => AtmosphereData.Radius, value =>
        {
            AtmosphereData.Radius = value;
            AtmospherePass.UpdateUniforms();
        }, 0, 200, 1);
    }

    public override void _RenderCallback(int effectCallbackType, Godot.RenderData renderData)
    {
        RenderSceneBuffersRD renderSceneBuffers = renderData.GetRenderSceneBuffers() as RenderSceneBuffersRD;
        RenderSceneData sceneData = renderData.GetRenderSceneData();


        Rid color = renderSceneBuffers.GetColorTexture();
        Rid depth = renderSceneBuffers.GetDepthTexture();

        AtmospherePass.SetFramebuffer([
            (color, "color")
        ]);

        Transform3D cameraTransform = sceneData.GetCamTransform();
        Projection cameraProjection = sceneData.GetCamProjection();
        Projection viewProjectionMatrix = CustomCamera.GetViewProjectionMatrix(cameraTransform, cameraProjection);

        AtmospherePass.SetDepthUniform(depth);

        AtmospherePass.Invoke(Utilities.ToViewPushConstants(
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
        AtmospherePass.CreateUniforms();
    }

    public void CleanupGPUResources()
    {
        AtmospherePass?.CleanupGPU();
    }

    public bool IsValidForProcessing()
    {
        return AtmospherePass?.IsValid() == true;
    }
}