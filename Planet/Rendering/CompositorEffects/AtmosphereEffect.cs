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
    public DirectionalLight3D Sun;
    private static WorldData WorldData => SaveManager.WorldData;
    public bool Ready { get; private set; } = true;
    public bool Paused = false;

    public AtmosphereEffect(DirectionalLight3D sun, Dictionary<PlanetRenderer.BufferNames, ShaderUniform> sharedShaderUniforms)
    {
        // Sun = sun;
        // AtmospherePass = new(sharedShaderUniforms);

        // EffectCallbackType = EffectCallbackTypeEnum.PostSky;
        // AccessResolvedDepth = true;

        // DebugMenuController.Instance.AddSection("Atmosphere", 0, false, null, 400);
        // DebugMenuController.Instance.AddSlider("Atmosphere Radius", "Atmosphere", () => AtmosphereData.Radius - WorldData.Radius, value =>
        // {
        //     AtmosphereData.Radius = value + WorldData.Radius;
        //     AtmospherePass.UpdateUniforms();
        // }, 0, 200, 1);

        // DebugMenuController.Instance.AddSlider("Density Falloff", "Atmosphere", () => AtmosphereData.DensityFalloff, value =>
        // {
        //     AtmosphereData.DensityFalloff = value;
        //     AtmospherePass.UpdateUniforms();
        // }, 0, 10, 0.1f);

        // DebugMenuController.Instance.AddSlider("Light Sampling Count", "Atmosphere", () => AtmosphereData.LightSamplingCount, value =>
        // {
        //     AtmosphereData.LightSamplingCount = value;
        //     AtmospherePass.UpdateUniforms();
        // }, 1, 64, 1);

        // DebugMenuController.Instance.AddSlider("Optical Depth Sampling Count", "Atmosphere", () => AtmosphereData.OpticalDepthSamplingCount, value =>
        // {
        //     AtmosphereData.OpticalDepthSamplingCount = value;
        //     GD.Print(AtmosphereData.OpticalDepthSamplingCount);
        //     AtmospherePass.UpdateUniforms();
        // }, 1, 64, 1);
    }

    public override void _RenderCallback(int effectCallbackType, Godot.RenderData renderData)
    {
        // RenderSceneBuffersRD renderSceneBuffers = renderData.GetRenderSceneBuffers() as RenderSceneBuffersRD;
        // RenderSceneData sceneData = renderData.GetRenderSceneData();

        // Vector2I size = renderSceneBuffers.GetInternalSize();

        // Rid color = renderSceneBuffers.GetColorTexture();
        // Rid depth = renderSceneBuffers.GetDepthTexture();

        // AtmospherePass.SetFramebuffer([
        //     (color, "color"),
        //     (depth, "depth"),
        // ], size);

        // AtmospherePass.UpdateUniforms();

        // Transform3D cameraTransform = sceneData.GetCamTransform();
        // Projection cameraProjection = sceneData.GetCamProjection();
        // Projection viewProjectionMatrix = CustomCamera.GetViewProjectionMatrix(cameraTransform, cameraProjection);

        // Basis basis = cameraTransform.Basis;

        // Vector3 right = basis.X;
        // Vector3 up = basis.Y;
        // Vector3 forward = -basis.Z;

        // // .. Utilities.ToBytesSingle(viewProjectionMatrix),
        // // // .. Utilities.ToBytesSingle(VectorUtils.ToVector4(right, 1)),
        // // // .. Utilities.ToBytesSingle(VectorUtils.ToVector4(up, 1)),
        // // // .. Utilities.ToBytesSingle(VectorUtils.ToVector4(forward, 1)),
        // // .. Utilities.ToBytesSingle(VectorUtils.ToVector4(cameraTransform.Origin, Mathf.Tan(Mathf.DegToRad(cameraProjection.GetFov()) / 2))),
        // // .. Utilities.ToBytesSingle(VectorUtils.ToVector4(Sun.Rotation, 1)),
        // // .. Utilities.ToBytesSingle(cameraProjection.GetZNear()),
        // // .. Utilities.ToBytesSingle(cameraProjection.GetZFar()),
        // // .. Utilities.ToBytesSingle(0),
        // // .. Utilities.ToBytesSingle(0)

        // // AtmospherePass.Invoke(Utilities.CollectionToBytes([
        // //     .. Utilities.ToBytesSingle(viewProjectionMatrix),
        // //     .. Utilities.ToBytesSingle(VectorUtils.ToVector4(right, 1)),
        // //     .. Utilities.ToBytesSingle(VectorUtils.ToVector4(up, 1)),
        // //     .. Utilities.ToBytesSingle(VectorUtils.ToVector4(forward, 1)),
        // //     .. Utilities.ToBytesSingle(VectorUtils.ToVector4(cameraTransform.Origin, Mathf.Tan(Mathf.DegToRad(cameraProjection.GetFov()) / 2))),
        // //     .. Utilities.ToBytesSingle(VectorUtils.ToVector4(Sun.Rotation, 1)),
        // //     .. Utilities.ToBytesSingle(cameraProjection.GetZNear()),
        // //     .. Utilities.ToBytesSingle(cameraProjection.GetZFar()),
        // //     .. Utilities.ToBytesSingle(0),
        // //     .. Utilities.ToBytesSingle(0)
        // // ]));

        // AtmospherePass.Invoke([
        //     viewProjectionMatrix,
        //     cameraTransform.Origin,
        //     Mathf.Tan(Mathf.DegToRad(cameraProjection.GetFov()) / 2),
        //     VectorUtils.ToVector4(Sun.Rotation, 1),
        // ]);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete)
            CleanupGPUResources();
    }

    public void CreateUniforms()
    {
        // AtmospherePass.CreateUniforms();
    }

    public void CleanupGPUResources()
    {
        AtmospherePass?.CleanupGPU();
    }
}