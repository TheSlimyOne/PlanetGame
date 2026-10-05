using System;
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
    // public AtmospherePass AtmospherePass { get; private set; }
    public AtmosphereDispatcher AtmosphereDispatcher { get; private set; }
    private static AtmosphereData AtmosphereData => SaveManager.AtmosphereData;
    public DirectionalLight3D Sun;
    private static WorldData WorldData => SaveManager.WorldData;
    public bool Ready { get; private set; } = true;
    public bool Paused = false;

    public AtmosphereEffect(DirectionalLight3D sun, Dictionary<PlanetRenderer.BufferNames, ShaderUniform> sharedShaderUniforms)
    {
        Sun = sun;
        AtmosphereDispatcher = new(sharedShaderUniforms);

        EffectCallbackType = EffectCallbackTypeEnum.PostSky;
        // AccessResolvedDepth = true;

        DebugMenuController.Instance.AddSection("Atmosphere", 0, false, null, 400);
        DebugMenuController.Instance.AddSetValue("Atmosphere Scale", "Atmosphere", () => AtmosphereData.Scale, value =>
        {
            AtmosphereData.Scale = value;
            AtmosphereDispatcher.UpdateUniforms();
        },
        0.125f);
        
        DebugMenuController.Instance.AddSlider("Density Falloff", "Atmosphere", () => AtmosphereData.DensityFalloff, value =>
        {
            AtmosphereData.DensityFalloff = value;
            AtmosphereDispatcher.UpdateUniforms();
        }, 0, 100, 0.1f);

        DebugMenuController.Instance.AddSlider("Light Sampling Count", "Atmosphere", () => AtmosphereData.LightSamplingCount, value =>
        {
            AtmosphereData.LightSamplingCount = value;
            AtmosphereDispatcher.UpdateUniforms();
        }, 1, 64, 1);

        DebugMenuController.Instance.AddSlider("Optical Depth Sampling Count", "Atmosphere", () => AtmosphereData.OpticalDepthSamplingCount, value =>
        {
            AtmosphereData.OpticalDepthSamplingCount = value;
            AtmosphereDispatcher.UpdateUniforms();
        }, 1, 64, 1);

        DebugMenuController.Instance.AddSlider("Scattering Strength", "Atmosphere", () => AtmosphereData.ScatteringStrength, value =>
        {
            AtmosphereData.ScatteringStrength = value;
            AtmosphereDispatcher.UpdateUniforms();
        }, 1f, 25f, 0.025f);

        // DebugMenuController.Instance.AddLabel("Camera Right", null, () => { return });

    }

    public override void _RenderCallback(int effectCallbackType, Godot.RenderData renderData)
    {
        if (PlanetController.Quiting)
            return;

        RenderSceneBuffersRD renderSceneBuffers = renderData.GetRenderSceneBuffers() as RenderSceneBuffersRD;
        RenderSceneData sceneData = renderData.GetRenderSceneData();

        Vector2I size = renderSceneBuffers.GetInternalSize();

        Rid color = renderSceneBuffers.GetColorTexture();
        Rid depth = renderSceneBuffers.GetDepthTexture();

        // AtmospherePass.SetFramebuffer([
        //     (color, "color"),
        //     (depth, "depth"),
        // ], size);

        AtmosphereDispatcher.UpdateUniforms(color, depth);




        Transform3D cameraTransform = sceneData.GetCamTransform();
        Projection cameraProjection = sceneData.GetCamProjection();
        Projection viewProjectionMatrix = CustomCamera.GetViewProjectionMatrix(cameraTransform, cameraProjection);

        Basis basis = cameraTransform.Basis;

        Vector3 right = basis.X.Normalized();
        Vector3 up = basis.Y.Normalized();
        Vector3 forward = -basis.Z.Normalized();

        Vector2 halfExtents = cameraProjection.GetViewportHalfExtents();
        float near = cameraProjection.GetZNear();

        halfExtents = new Vector2(Mathf.Abs(halfExtents.X), Mathf.Abs(halfExtents.Y)) / near;

        Vector3 frustumTopLeft = (forward + up * halfExtents.Y + right * halfExtents.X).Normalized();
        Vector3 frustumTopRight = (forward + up * halfExtents.Y - right * halfExtents.X).Normalized();
        Vector3 frustumBottomLeft = (forward - up * halfExtents.Y + right * halfExtents.X).Normalized();
        Vector3 frustumBottomRight = (forward - up * halfExtents.Y - right * halfExtents.X).Normalized();

        // GD.Print(
        // $"""
        // ================================
        // vector((0, 0, 0), {frustumTopLeft})
        // vector((0, 0, 0), {frustumTopRight})
        // vector((0, 0, 0), {frustumBottomLeft})
        // vector((0, 0, 0), {frustumBottomRight})
        // """);

        AtmosphereDispatcher.Invoke([
            VectorUtils.ToVector4(cameraTransform.Origin, 1),
            VectorUtils.ToVector4(frustumTopLeft, 0),
            VectorUtils.ToVector4(frustumTopRight, 0),
            VectorUtils.ToVector4(frustumBottomLeft, 0),
            VectorUtils.ToVector4(frustumBottomRight, 0),
            VectorUtils.ToVector4(Sun.Basis.Z.Normalized(), 0)
        ]);

        // .. Utilities.ToBytesSingle(viewProjectionMatrix),
        // // .. Utilities.ToBytesSingle(VectorUtils.ToVector4(right, 1)),
        // // .. Utilities.ToBytesSingle(VectorUtils.ToVector4(up, 1)),
        // // .. Utilities.ToBytesSingle(VectorUtils.ToVector4(forward, 1)),
        // .. Utilities.ToBytesSingle(VectorUtils.ToVector4(cameraTransform.Origin, Mathf.Tan(Mathf.DegToRad(cameraProjection.GetFov()) / 2))),
        // .. Utilities.ToBytesSingle(VectorUtils.ToVector4(Sun.Rotation, 1)),
        // .. Utilities.ToBytesSingle(cameraProjection.GetZNear()),
        // .. Utilities.ToBytesSingle(cameraProjection.GetZFar()),
        // .. Utilities.ToBytesSingle(0),
        // .. Utilities.ToBytesSingle(0)

        // AtmosphereDispatcher.Invoke([
        //    viewProjectionMatrix,
        // //    VectorUtils.ToVector4(right, 1),
        // //    VectorUtils.ToVector4(up, 1),
        // //    VectorUtils.ToVector4(forward, 1),
        //    VectorUtils.ToVector4(cameraTransform.Origin, Mathf.Tan(Mathf.DegToRad(cameraProjection.GetFov()) / 2)),
        //    VectorUtils.ToVector4(Sun.Rotation, 1),
        //    cameraProjection.GetZNear(),
        //    cameraProjection.GetZFar(),
        //    0,
        //    0
        // ]);

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
        AtmosphereDispatcher.CreateUniforms();
    }

    public void CleanupGPUResources()
    {
        AtmosphereDispatcher?.CleanupGPU();
    }
}