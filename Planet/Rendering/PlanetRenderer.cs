using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using PlanetGame.Data;
using PlanetGame.Planet.Rendering.Drawing;
using PlanetGame.Planet.Rendering.VirtualTexturing;
using PlanetGame.Rendering.Surface;
using PlanetGame.Shaders;
using PlanetGame.Shaders.Dispatchers;
using PlanetGame.Util;
using PlanetGame.Util.DebugUIComponents;
using Shaders;
using Uniform;

namespace PlanetGame.Planet.Rendering
{
    public class PlanetRenderer
    {
        public TerrainTessellator TerrainTessellator { get; private set; }
        public SparseVirtualTexture SparseVirtualTexture { get; private set; }
        
        public PlanetCompositorEffect PlanetCompositorEffect { get; private set; }
        public AtmosphereEffect AtmosphereEffect { get; private set; }

        public BindableShaderMaterial SurfaceShader { get; set; }
        private static TessellationData TessellationData => SaveManager.TessellationData;
        private static VirtualTextureData VirtualTextureData => SaveManager.VirtualTextureData;
        private static WorldData WorldData => SaveManager.WorldData;
        private static Data.RenderData RenderData => SaveManager.RenderData;

        private readonly RenderingDevice _renderingDevice = RenderingServer.GetRenderingDevice();
        private CustomCamera _viewCamera;
        
        public const uint STARTING_LOD = 2;
        public static uint GetStartingPrimitiveCount() => (uint)(6 * Mathf.Pow(4, STARTING_LOD + 1));

        public enum BufferNames
        {
            EXEC_DISPATCH_BUFFER,
            EXEC_ATOMIC_COUNTER,
            EXEC_KEY_INDICES,

            DRAW_DISPATCH_BUFFER,
            TESSELLATION_DATA,
            VIRTUAL_TEXTURE_DATA,
            RENDER_DATA,
            WORLD_DATA,
        }

        private readonly Dictionary<BufferNames, ShaderUniform> _sharedShaderUniforms;

        public PlanetRenderer(WorldEnvironment worldEnvironment, CustomCamera viewCamera)
        {
            _viewCamera = viewCamera;
            SurfaceShader = new()
            {
                Shader = GD.Load<Shader>(ShaderPaths.GD_PLANET_TESSELLATION_PATH)
            };

            _sharedShaderUniforms = [];

            Vector2I viewSize = new(1024, 512);

            // Creating Rendering Systems
            TerrainTessellator = new(SurfaceShader.GetRid(), _viewCamera.GetWorld3D().Scenario, _sharedShaderUniforms);
            SparseVirtualTexture = new(TerrainTessellator.TriangleMultiMesh, viewSize, _sharedShaderUniforms);

            // Creating Compositor Effects
            PlanetCompositorEffect = new(SparseVirtualTexture, TerrainTessellator.TriangleMultiMesh, _sharedShaderUniforms);
            AtmosphereEffect = new(_sharedShaderUniforms);

            CreateSharedBuffers();

            TerrainTessellator.CreateUniforms();
            SparseVirtualTexture.CreateUniforms();
            PlanetCompositorEffect.CreateUniforms();
            AtmosphereEffect.CreateUniforms();

            worldEnvironment.Compositor = new()
            {
                CompositorEffects = [PlanetCompositorEffect, AtmosphereEffect]
            };

            BindShaderParameters(SurfaceShader);
            BindDebugSettings();
        }

        public void Invoke()
        {
            UpdateSharedUniforms();

            TerrainTessellator.Invoke(_viewCamera);
            SparseVirtualTexture.Invoke(_viewCamera);

            SurfaceShader.SetParameter("camera_position", _viewCamera.GlobalPosition);
            SurfaceShader.SetParameter("fovy", Mathf.Tan(_viewCamera.GetCameraFov(true) / 2));
            SurfaceShader.SetParameter("planet_transform_matrix", Utilities.ToProjection(WorldData.GetPlanetTransform()));


            SurfaceShader?.UpdateFrameDependentParameters();
        }

        private double _heightUpdateTimer;
        private float _targetHeightOffset;
        private float _heightInterpolationSpeed = HEIGHT_INTERPOLATION_SPEED;

        private const double HEIGHT_UPDATE_INTERVAL = 0.5;
        private const float HEIGHT_INTERPOLATION_SPEED = 2;
        private const float HEIGHT_UNDERGROUND_INTERPOLATION_SPEED = 4;
        public void UpdateHeightOffset(PlanetQuery planetQuery, Vector3 position, float distanceFromSurface, double delta)
        {
            _heightUpdateTimer += delta;

            if (_heightUpdateTimer >= HEIGHT_UPDATE_INTERVAL)
            {
                _heightUpdateTimer = 0;

                float heightOffset = planetQuery.GetHeightAtPoint(position);

                if (!float.IsNaN(heightOffset))
                {
                    _targetHeightOffset = heightOffset;

                    _heightInterpolationSpeed = distanceFromSurface > heightOffset
                        ? HEIGHT_INTERPOLATION_SPEED
                        : HEIGHT_INTERPOLATION_SPEED * HEIGHT_UNDERGROUND_INTERPOLATION_SPEED;
                }
            }

            TessellationData.HeightOffset = Mathf.Lerp(
                TessellationData.HeightOffset,
                _targetHeightOffset,
                1.0f - Mathf.Exp(-_heightInterpolationSpeed * (float)delta)
            );
        }

        public async Task ProcessDrawCommand(DrawCommand drawCommand)
        {

            TileCache.TileCacheType type = drawCommand.Parameters.TargetTile;
            TileManager.DrawCommandOnTile(drawCommand, SparseVirtualTexture.GetTileCache(type), Callable.From(SparseVirtualTexture.ClearVirtualTexture));
        }

#region Buffer handling
        private void UpdateSharedUniforms()
        {
            ((StorageBufferUniform)_sharedShaderUniforms[BufferNames.TESSELLATION_DATA]).UpdateUniform(
                TessellationData.ToBytes()
            );
            ((StorageBufferUniform)_sharedShaderUniforms[BufferNames.RENDER_DATA]).UpdateUniform(
                RenderData.ToBytes()
            );
            ((StorageBufferUniform)_sharedShaderUniforms[BufferNames.WORLD_DATA]).UpdateUniform(
                WorldData.ToBytes()
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
            _sharedShaderUniforms[BufferNames.EXEC_DISPATCH_BUFFER] = new StorageBufferUniform(
                null,
                _renderingDevice,
                -1,
                [.. Utilities.ToBytes<uint>([GetStartingPrimitiveCount() / 64 + 1, 1, 1])],
                RenderingDevice.StorageBufferUsage.Indirect,
                perserve: true
            );

            _sharedShaderUniforms[BufferNames.EXEC_ATOMIC_COUNTER] = new StorageBufferUniform(
                null,
                _renderingDevice,
                -1,
                GetExecAtomicCounterData(),
                perserve: true
            );

            _sharedShaderUniforms[BufferNames.EXEC_KEY_INDICES] = new StorageBufferUniform(
                null,
                _renderingDevice,
                -1,
                [.. Utilities.ToBytes<uint>([0, 1, 2, TessellationData.MaximumKeys])],
                perserve: true
            );
        }

        private void CreateDrawBuffers()
        {
            _sharedShaderUniforms[BufferNames.DRAW_DISPATCH_BUFFER] = new StorageBufferUniform(
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
            _sharedShaderUniforms[BufferNames.TESSELLATION_DATA] = new StorageBufferUniform(
                null,
                _renderingDevice,
                -1,
                TessellationData.ToBytes(),
                perserve: true
            );

            _sharedShaderUniforms[BufferNames.VIRTUAL_TEXTURE_DATA] = new StorageBufferUniform(
                null,
                _renderingDevice,
                -1,
                VirtualTextureData.ToBytes(),
                perserve: true
            );

            _sharedShaderUniforms[BufferNames.RENDER_DATA] = new StorageBufferUniform(
                null,
                _renderingDevice,
                -1,
                RenderData.ToBytes(),
                perserve: true
            );

            _sharedShaderUniforms[BufferNames.WORLD_DATA] = new StorageBufferUniform(
                null,
                _renderingDevice,
                -1,
                WorldData.ToBytes(),
                perserve: true
            );
        }

        private byte[] GetExecAtomicCounterData()
        {
            uint[] primCounts = new uint[3 * 3];
            primCounts[0] = GetStartingPrimitiveCount();

            return [.. Utilities.ToBytes<uint>(primCounts)];
        }

#endregion
        public void BindShaderParameters(BindableShaderMaterial bindableShaderMaterial)
        {
            bindableShaderMaterial.FrameDependentBind("radius", () => WorldData.Radius);
            bindableShaderMaterial.FrameDependentBind("height_scale", () => WorldData.Radius * WorldData.HeightScale);
            bindableShaderMaterial.FrameDependentBind("resolution", () => TessellationData.Resolution);
            bindableShaderMaterial.FrameDependentBind("maximum_lod", () => TessellationData.MaximumLod);
            bindableShaderMaterial.FrameDependentBind("minimum_lod", () => TessellationData.MinimumLod);

            bindableShaderMaterial.FrameDependentBind("is_cube", () => RenderData.IsCube);
            bindableShaderMaterial.FrameDependentBind("is_morphing", () => RenderData.IsMorphing);

            bindableShaderMaterial.FrameDependentBind("sub_factor", () => TessellationData.SubFactor);
            bindableShaderMaterial.FrameDependentBind("current_lod", () => TerrainTessellator.MaxLod);

            bindableShaderMaterial.FrameDependentBind(
                "lod_to_mip_map",
                () => Array.ConvertAll(VirtualTextureData.LodToMipMap, x => (int)x)
            );

            bindableShaderMaterial.FrameDependentBind("mouse_position", () =>
            {
                Vector3 localPickedPosition = SparseVirtualTexture.GetLocalMousePosition(
                    _viewCamera.GetViewport().GetMousePosition(),
                    _viewCamera.GetViewport().GetVisibleRect().Size
                );

                return localPickedPosition.Normalized();
            });

            bindableShaderMaterial.Bind("heightmap_tile_cache", () => SparseVirtualTexture.GetTileCache(TileCache.TileCacheType.HEIGHTMAP).Cache);
            bindableShaderMaterial.Bind("albedo_tile_cache", () => SparseVirtualTexture.GetTileCache(TileCache.TileCacheType.ALBEDO).Cache);
            bindableShaderMaterial.Bind("terrain_indirection_table", () => SparseVirtualTexture.ConsolidatedIndirectionTable.Table);

            bindableShaderMaterial.Bind("low_resolution_mip_count", () => VirtualTextureData.LowResolutionMipCount);
            bindableShaderMaterial.Bind("high_resolution_mip_count", () => VirtualTextureData.HighResolutionMipCount);
            bindableShaderMaterial.Bind("total_tile_slots", () => TileCache.DEFAULT_TILE_SLOTS_COUNT);

            bindableShaderMaterial.UpdateAllParameters();
        }

#region Debug Settings
        private void BindDebugSettings()
        {
            BindPlanetDebugSettings();
            BindRenderingDebugSettings();
        }

        private void BindPlanetDebugSettings()
        {
            DebugMenuController.Instance.AddSection("Planet", 0, false, null, 100);

            DebugMenuController.Instance.AddSlider("Radius", "Planet", () => WorldData.Radius, value =>
            {
                WorldData.Radius = value;
            }, 1.0f, 8000.0f, 1.0f);

            DebugMenuController.Instance.AddSlider("Height Scale", "Planet", () => WorldData.HeightScale, value => WorldData.HeightScale = value, 0.0f, 0.25f, 0.005f);
        }

        private void BindRenderingDebugSettings()
        {
            DebugMenuController.Instance.AddSection("Rendering", 0, false, null, 400);
            DebugMenuController.Instance.AddButton("Render Wireframe", "Rendering", () => PlanetCompositorEffect.PlanetRenderPass.IsWireframe, () =>
            {
                PlanetCompositorEffect.PlanetRenderPass.IsWireframe = !PlanetCompositorEffect.PlanetRenderPass.IsWireframe;
                PlanetCompositorEffect.PlanetRenderPass.UpdatePipeline();

            });
            
            DebugMenuController.Instance.AddSection("Visiblity", 0, false, "Rendering", 400);
            
            DebugMenuController.Instance.AddButton("Instance Visiblity", "Visiblity", () => TerrainTessellator.PlanetInstanceVisible, () =>
            {
                TerrainTessellator.PlanetInstanceVisible = !TerrainTessellator.PlanetInstanceVisible;
            });


            DebugMenuController.Instance.AddButton("Compositor Visiblity", "Visiblity", () => PlanetCompositorEffect.Enabled, () =>
            {
                PlanetCompositorEffect.Enabled = !PlanetCompositorEffect.Enabled;
            });
            DebugMenuController.Instance.AddButton("Atmosphere Visiblity", "Visiblity", () => AtmosphereEffect.Enabled, () =>
            {
                AtmosphereEffect.Enabled = !AtmosphereEffect.Enabled;
            });

            DebugMenuController.Instance.AddButton("Render Cube Mode", "Rendering", () => RenderData.IsCube, () => RenderData.IsCube = !RenderData.IsCube);
            DebugMenuController.Instance.AddButton("Render Culling", "Rendering", () => RenderData.IsCulling, () => RenderData.IsCulling = !RenderData.IsCulling);
            DebugMenuController.Instance.AddButton("Render Morphing", "Rendering", () => RenderData.IsMorphing, () => RenderData.IsMorphing = !RenderData.IsMorphing);

            // DebugMenuController.Instance.AddTexture("Output Color", "Rendering", new TextureRect() { Texture = new Texture2Drd() { TextureRdRid = PlanetCompositorEffect.PlanetRenderPass.Depth } });

            AddShaderToggle("Render Keys", "show_keys");
            AddShaderToggle("Render Indirection Age", "show_indirection_age");
            AddShaderToggle("Render Cached Tiles", "show_in_cache");

        }

        private void AddShaderToggle(string name, string parameter)
        {
            DebugMenuController.Instance.AddButton(name, "Rendering", () => SurfaceShader.GetParameter<bool>(parameter), () => SurfaceShader.SetParameter(parameter, !SurfaceShader.GetParameter<bool>(parameter)));
        }
#endregion

        public void CleanupGPU()
        {
            TerrainTessellator.CleanupGPUResources();
            SparseVirtualTexture.CleanupGPUResources();
        }
    }
}