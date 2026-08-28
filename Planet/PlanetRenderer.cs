using System;
using System.Collections.Generic;
using Godot;
using PlanetGame.Rendering;
using PlanetGame.Rendering.Surface;
using PlanetGame.Rendering.VirtualTexturing;
using PlanetGame.Shaders;
using PlanetGame.Shaders.Dispatchers;
using PlanetGame.Util;
using PlanetGame.Util.DebugUIComponents;
using Shaders;
using Uniform;

namespace PlanetGame.Planet;

public class PlanetRenderer
{
    public TerrainTessellator TerrainTessellator { get; private set; }
    public SparseVirtualTexture SparseVirtualTexture { get; private set; }
    public BindableShaderMaterial SurfaceShader { get; set; }
  
    private readonly PlanetController _planetController;

    private readonly RenderingDevice _renderingDevice = RenderingServer.GetRenderingDevice();

    private static TessellationData TessellationData => SaveManager.CurrentWorldSave.TessellationData;
    private static VirtualTextureData VirtualTextureData => SaveManager.CurrentWorldSave.VirtualTextureData;

    public bool IsCulling = false;
    public bool IsMorphing = false;
    public bool IsCube = false;

    public enum BufferNames
    {
        EXTERNAL_DATA,

        EXEC_DISPATCH_BUFFER,
        EXEC_ATOMIC_COUNTER,
        EXEC_KEY_INDICES,

        DRAW_DISPATCH_BUFFER,
        VIRTUAL_TEXTURE_DATA,
    }

    private readonly Dictionary<BufferNames, ShaderUniform> _shaderedShaderUniforms;

    public PlanetRenderer(PlanetController planetController)
    {
        SurfaceShader = new()
        {
            Shader = GD.Load<Shader>(ShaderPaths.GD_PLANET_TESSELLATION_PATH)
        };

        _planetController = planetController;
        _shaderedShaderUniforms = [];

        Vector2I viewSize = new(1024, 512);

        TerrainTessellator = new(SurfaceShader.GetRid(), planetController.GetWorld3D().Scenario, _shaderedShaderUniforms);
        
        SparseVirtualTexture = new(TerrainTessellator.TriangleMultiMesh, viewSize, _shaderedShaderUniforms);

        CreateSharedBuffers();

        TerrainTessellator.CreateUniforms();
        SparseVirtualTexture.CreateUniforms();
        

        BindShaderParameters(SurfaceShader);
        BindDebugSettings();
    }

    public void Invoke(CustomCamera camera, float heightOffset, Transform3D planetTransform)
    {
        UpdateSharedUniforms(heightOffset, planetTransform);

        TerrainTessellator.Invoke(camera);
        SparseVirtualTexture.Invoke(camera);



        SurfaceShader.SetParameter("camera_position", camera.GlobalPosition);
        SurfaceShader.SetParameter("fovy", Mathf.Tan(camera.GetCameraFov(true) / 2));
        SurfaceShader.SetParameter("planet_transform_matrix", Utilities.ToProjection(planetTransform));


        SurfaceShader?.UpdateFrameDependentParameters();
    }

    public void ResetRenderer()
    {
        // TerrainTessellator.UpdateResolution();
        SparseVirtualTexture.ClearVirtualTexture();
    }

    private void UpdateSharedUniforms(float heightOffset, Transform3D planetTransform)
    {
        ((StorageBufferUniform)_shaderedShaderUniforms[BufferNames.EXTERNAL_DATA]).UpdateUniform(
            GetExternalData(heightOffset, planetTransform)
        );
    }

    private void CreateSharedBuffers()
    {
        CreateExecutionBuffers();
        CreateDrawBuffers();
        CreateDataBuffers();
    }

    private void CreateExecutionBuffers()
    {
        _shaderedShaderUniforms[BufferNames.EXEC_DISPATCH_BUFFER] = new StorageBufferUniform(
            null,
            _renderingDevice,
            -1,
            [.. Utilities.ToBytes<uint>([TessellationData.GetStartingPrimitiveCount / 64 + 1, 1, 1])],
            RenderingDevice.StorageBufferUsage.Indirect,
            perserve: true
        );

        _shaderedShaderUniforms[BufferNames.EXEC_ATOMIC_COUNTER] = new StorageBufferUniform(
            null,
            _renderingDevice,
            -1,
            GetExecAtomicCounterData(),
            perserve: true
        );

        _shaderedShaderUniforms[BufferNames.EXEC_KEY_INDICES] = new StorageBufferUniform(
            null,
            _renderingDevice,
            -1,
            [.. Utilities.ToBytes<uint>([0, 1, 2, TessellationData.MaximumKeys])],
            perserve: true
        );
    }

    private void CreateDrawBuffers()
    {
        _shaderedShaderUniforms[BufferNames.DRAW_DISPATCH_BUFFER] = new StorageBufferUniform(
            null,
            _renderingDevice,
            -1,
            [.. Utilities.ToBytes<uint>(5)],
            RenderingDevice.StorageBufferUsage.Indirect,
            perserve: true
        );
    }

    private void CreateDataBuffers()
    {
        _shaderedShaderUniforms[BufferNames.EXTERNAL_DATA] = new StorageBufferUniform(
            null,
            _renderingDevice,
            -1,
            GetExternalData(0, Transform3D.Identity),
            RenderingDevice.StorageBufferUsage.Indirect,
            perserve: true
        );

        _shaderedShaderUniforms[BufferNames.VIRTUAL_TEXTURE_DATA] = new StorageBufferUniform(
            null,
            _renderingDevice,
            -1,
            GetVirtualTextureData(),
            perserve: true
        );
    }

    private byte[] GetVirtualTextureData()
    {
        return
        [
            .. Utilities.ToBytes([
            VirtualTextureData.LowResolutionMipCount,
            VirtualTextureData.HighResolutionMipCount,

            VirtualTextureData.GridSize,
            (uint)VirtualTextureData.FallBackTiles.Length,

            TileCache.DEFAULT_TILE_SLOTS_COUNT,
            (uint)Mathf.Ceil(Mathf.Sqrt(TileCache.DEFAULT_TILE_SLOTS_COUNT)),

            ResolveTileRequestDispatcher.REQUEST_AMOUNT,
            0u
        ])];
    }

    public byte[] GetBitFlags()
    {
        return
        [
            .. Utilities.ToBytesSingle(Utilities.ToBitFlags([
                IsCulling,
                IsMorphing,
                IsCube,
        ]))];
    }

    private byte[] GetExternalData(float heightOffset, Transform3D planetTransform)
    {
        byte[] data =
        [
            .. Utilities.ToBytesSingle(VirtualTextureData.LowResolutionMipCount),
            .. Utilities.ToBytesSingle(VirtualTextureData.HighResolutionMipCount),
            .. Utilities.ToBytesSingle(TessellationData.Resolution),

            .. Utilities.ToBytesSingle(TessellationData.Radius),

            .. Utilities.ToBytesSingle(TessellationData.Radius * TessellationData.HeightScale),
            .. Utilities.ToBytesSingle(TessellationData.SubFactor),
            .. GetBitFlags(),
            .. Utilities.ToBytesSingle(TessellationData.MaximumLod),

            .. Utilities.ToBytesSingle(TessellationData.MinimumLod),
            .. Utilities.ToBytesSingle(heightOffset),
            .. Utilities.ToBytesSingle(0),
            .. Utilities.ToBytesSingle(0),

            .. Utilities.ToBytesSingle(Utilities.ToProjection(planetTransform)),

            .. Utilities.ToBytes<int>(VirtualTextureData.LodToMipMap),
        ];

        return data;
    }

    private byte[] GetExecAtomicCounterData()
    {
        uint[] primCounts = new uint[3 * 3];
        primCounts[0] = TessellationData.GetStartingPrimitiveCount;

        return [.. Utilities.ToBytes<uint>(primCounts)];
    }

    public void BindShaderParameters(BindableShaderMaterial bindableShaderMaterial)
    {
        bindableShaderMaterial.FrameDependentBind("radius", () => TessellationData.Radius);
        bindableShaderMaterial.FrameDependentBind("height_scale", () => TessellationData.Radius * TessellationData.HeightScale);
        bindableShaderMaterial.FrameDependentBind("resolution", () => TessellationData.Resolution);
        bindableShaderMaterial.FrameDependentBind("maximum_lod", () => TessellationData.MaximumLod);
        bindableShaderMaterial.FrameDependentBind("minimum_lod", () => TessellationData.MinimumLod);

        bindableShaderMaterial.FrameDependentBind("is_cube", () => IsCube);
        bindableShaderMaterial.FrameDependentBind("is_morphing", () => IsMorphing);

        bindableShaderMaterial.FrameDependentBind("sub_factor", () => TessellationData.SubFactor);
        bindableShaderMaterial.FrameDependentBind("current_lod", () => TerrainTessellator.MaxLod);

        bindableShaderMaterial.FrameDependentBind("lod_to_mip_map", () => VirtualTextureData.LodToMipMap);


        bindableShaderMaterial.Bind("height_map_tile_cache", () => SparseVirtualTexture.HeightTileCache.Cache);
        bindableShaderMaterial.Bind("terrain_indirection_table", () => SparseVirtualTexture.ConsolidatedIndirectionTable.Table);
        bindableShaderMaterial.Bind("albedo_tile_cache", () => SparseVirtualTexture.AlbedoTileCache.Cache);

        bindableShaderMaterial.Bind("low_resolution_mip_count", () => VirtualTextureData.LowResolutionMipCount);
        bindableShaderMaterial.Bind("high_resolution_mip_count", () => VirtualTextureData.HighResolutionMipCount);
        bindableShaderMaterial.Bind("total_tile_slots", () => TileCache.DEFAULT_TILE_SLOTS_COUNT);

        bindableShaderMaterial.UpdateAllParameters();
    }

    private void BindDebugSettings()
    {
        BindPlanetDebugSettings();
        BindRenderingDebugSettings();
    }

    private void BindPlanetDebugSettings()
    {
        DebugMenuController.Instance.AddSection("Planet", 0, false, null, 100);

        DebugMenuController.Instance.AddSlider("Radius", "Planet", () => TessellationData.Radius, value =>
        {
            TessellationData.Radius = value;
        }, 1.0f, 8000.0f, 1.0f);

        DebugMenuController.Instance.AddSlider("Height Scale", "Planet", () => TessellationData.HeightScale, value => TessellationData.HeightScale = value, 0.0f, 0.25f, 0.005f);
    }

    private void BindRenderingDebugSettings()
    {
        DebugMenuController.Instance.AddSection("Rendering", 0, false, null, 400);

        DebugMenuController.Instance.AddButton("Render Cube Mode", "Rendering", () => IsCube, () => IsCube = !IsCube);
        DebugMenuController.Instance.AddButton("Render Culling", "Rendering", () => IsCulling, () => IsCulling = !IsCulling);
        DebugMenuController.Instance.AddButton("Render Morphing", "Rendering", () => IsMorphing, () => IsMorphing = !IsMorphing);

        AddShaderToggle("Render Tile UVs", "render_tile_uvs");
        AddShaderToggle("Render Keys", "show_keys");
        AddShaderToggle("Render Indirection Age", "show_indirection_age");
        AddShaderToggle("Render Cached Tiles", "show_in_cache");
    }

    private void AddShaderToggle(string name, string parameter)
    {
        DebugMenuController.Instance.AddButton(name, "Rendering", () => SurfaceShader.GetParameter<bool>(parameter), () => SurfaceShader.SetParameter(parameter, !SurfaceShader.GetParameter<bool>(parameter)));
    }
}