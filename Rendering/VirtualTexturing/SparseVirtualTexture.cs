using System.Threading.Tasks;
using PlanetGame.Shaders.Dispatchers;
using Godot;
using System.Linq;
using PlanetGame.Shaders.RenderPasses;
using PlanetGame.Rendering.Surface;
using Shaders;
using PlanetGame.Util;
using PlanetGame.Planet;
using System.Collections.Generic;
using static PlanetGame.Planet.PlanetRenderer;
using Uniform;
using PlanetGame.Util.DebugUIComponents;
using System;

namespace PlanetGame.Rendering.VirtualTexturing
{
    public class SparseVirtualTexture
    {
        private static VirtualTextureData VirtualTextureData => SaveManager.CurrentWorldSave.VirtualTextureData;
        private static TessellationData TessellationData => SaveManager.CurrentWorldSave.TessellationData;

        public ResolveTileRequestDispatcher ResolveTileRequest { get; private set; }
        public ValidateCacheDispatcher ValidateTileCache { get; private set; }
        public SvtFeedbackRenderPass SvtFeedbackRenderPass { get; private set; }
        public FlattenIndirectionTableDispatcher FlattenIndirectionTableDispatcher { get; private set; }

        public IndirectionTable IndirectionTable { get; private set; }
        public ConsolidatedIndirectionTable ConsolidatedIndirectionTable { get; private set; }
        public TileCache AlbedoTileCache { get; private set; }
        public TileCache HeightTileCache { get; private set; }
        public ResidencyTable ResidencyTable { get; private set; }
        public StateTable StateTable { get; private set; }

        public bool Ready { get; private set; } = true;
        public bool Paused = false;

        private readonly Image[] _consolidatedIndirectionTexture = new Image[6];

        public SparseVirtualTexture(MultiMeshRD triangleMultiMesh, Vector2I viewSize, Dictionary<BufferNames, ShaderUniform> sharedUniforms)
        {
            SaveManager.WorldSave worldSave = SaveManager.CurrentWorldSave;

            AlbedoTileCache = new(worldSave.TilesAlbedo, Colors.Magenta, Image.Format.Rgba8);
            HeightTileCache = new(worldSave.TilesHeightmap, Colors.Black, Image.Format.R8);

            IndirectionTable = new();
            ConsolidatedIndirectionTable = new();
            ResidencyTable = new();
            StateTable = new();

            SvtFeedbackRenderPass = new(this, sharedUniforms, triangleMultiMesh, viewSize);
            ResolveTileRequest = new(this, sharedUniforms, viewSize);
            ValidateTileCache = new(this);
            FlattenIndirectionTableDispatcher = new(this);

            BindDebugSettings();
        }

        public void CreateUniforms()
        {
            SvtFeedbackRenderPass.CreateUniforms();
            ResolveTileRequest.CreateUniforms();
            ValidateTileCache.CreateUniforms();
            FlattenIndirectionTableDispatcher.CreateUniforms();
        }

        public bool IsValidForProcessing()
        {
            return ResolveTileRequest?.IsValid() == true && ValidateTileCache?.IsValid() == true && SvtFeedbackRenderPass?.IsValid() == true;
        }

        private const int SIMULATED_DISK_LATENCY_MS = 20;

        public async void RequestTileSlot(byte[] bytes)
        {
            (uint tileX, uint tileY, uint tileZ, uint slot)[] data =
                [.. Utilities.FromBytes<(uint, uint, uint, uint)>(bytes)];

            if (data.Length > 0)
            {
                await Parallel.ForEachAsync(data, new ParallelOptions { MaxDegreeOfParallelism = 4 }, async (tileData, _) =>
                {
                    // await Task.Delay(SIMULATED_DISK_LATENCY_MS, _);

                    uint xCoord = tileData.tileX;
                    uint yCoord = tileData.tileY;
                    uint mipIndex = tileData.tileZ % VirtualTextureData.TotalSubdivisions;
                    uint normalId = (tileData.tileZ - mipIndex) / VirtualTextureData.TotalSubdivisions;
                    uint slot = tileData.slot;

                    int realMipIndex = (int)(mipIndex - VirtualTextureData.HighResolutionMipCount);

                    string tileName = $"{realMipIndex}_{normalId}_{xCoord}_{yCoord}";

                    if (!AlbedoTileCache.TileExist(tileName) && realMipIndex < 0)
                    {
                        Image tile = AlbedoTileCache.CreateTile(tileName);
                        AlbedoTileCache.InsertTile(tile, slot);
                    }
                    else
                        AlbedoTileCache.InsertTile(tileName, slot);

                    if (!HeightTileCache.TileExist(tileName) && realMipIndex < 0)
                    {
                        Image tile = HeightTileCache.CreateTile(tileName);
                        HeightTileCache.InsertTile(tile, slot);
                    }
                    else
                        HeightTileCache.InsertTile(tileName, slot);
                });

                ValidateTileCache.Invoke();
                FlattenIndirectionTableDispatcher.Invoke();

                int gridSize = (int)VirtualTextureData.GridSize;
                RenderingDevice renderingDevice = RenderingServer.GetRenderingDevice();

                for (uint layer = 0; layer < 6; layer++)
                {
                    _consolidatedIndirectionTexture[layer] = Image.CreateFromData(
                        gridSize,
                        gridSize,
                        false,
                        FormatConverter.MatchDataFormat(ConsolidatedIndirectionTable.Format),
                        renderingDevice.TextureGetData(ConsolidatedIndirectionTable.GetRdRid(), layer)
                    );
                }
            }

            Ready = true;
        }

        public void ClearVirtualTexture()
        {
            IndirectionTable.ClearStorageTexture();
            IndirectionTable.SetFallbackSlots();

            ConsolidatedIndirectionTable.ClearStorageTexture();
            ConsolidatedIndirectionTable.SetFallbackSlots();

            AlbedoTileCache.ClearStorageTexture();
            AlbedoTileCache.SetFallbackSlots();

            HeightTileCache.ClearStorageTexture();
            HeightTileCache.SetFallbackSlots();

            ResidencyTable.ClearStorageTexture();
            ResidencyTable.SetFallbackSlots();

            ResolveTileRequest.ResetTileSlotCounter();
        }

        public void Invoke(CustomCamera camera)
        {
            if (!Ready || !IsValidForProcessing() || Paused)
                return;

            Ready = false;
            StateTable.ClearStorageTexture();


            SvtFeedbackRenderPass.Invoke(
                Utilities.ToViewPushConstants(
                    camera.GetViewProjectionMatrix(),
                    camera.GlobalPosition,
                    Mathf.Tan(camera.GetCameraFov(true) / 2)
                )
            );

            ResolveTileRequest.UpdateUniforms();
            ResolveTileRequest.Invoke();
            ResolveTileRequest.GetTextureIds(Callable.From<byte[]>(RequestTileSlot));
        }

        public Vector3 GetLocalMousePosition(Vector2 mousePosition, Vector2 screenSize)
        {
            return SvtFeedbackRenderPass.GetLocalMousePosition(mousePosition, screenSize);
        }

        public uint SampleConsolidatedIndirectionTexture(int normalId, Vector2 uv)
        {
            Image image = _consolidatedIndirectionTexture[normalId];

            int x = Mathf.Clamp((int)(uv.X * image.GetWidth()), 0, image.GetWidth() - 1);
            int y = Mathf.Clamp((int)(uv.Y * image.GetHeight()), 0, image.GetHeight() - 1);

            float value = image.GetPixel(x, y).G;

            return BitConverter.SingleToUInt32Bits(value);
        }

        public void CleanupGPUResources()
        {
            IndirectionTable.DeleteVisualization();
            IndirectionTable = default;

            ConsolidatedIndirectionTable.DeleteVisualization();
            ConsolidatedIndirectionTable = default;

            AlbedoTileCache.DeleteVisualization();
            AlbedoTileCache = default;

            HeightTileCache.DeleteVisualization();
            HeightTileCache = default;

            ResidencyTable.DeleteVisualization();
            ResidencyTable = default;

            StateTable.DeleteVisualization();
            StateTable = default;

            ResolveTileRequest.CleanupGPU();
            ValidateTileCache.CleanupGPU();
            SvtFeedbackRenderPass.CleanupGPU();
            FlattenIndirectionTableDispatcher.CleanupGPU();

            ResolveTileRequest = default;
            ValidateTileCache = default;
            SvtFeedbackRenderPass = default;
            FlattenIndirectionTableDispatcher = default;
        }

        private void BindDebugSettings()
        {
            DebugMenuController.Instance.AddSection("Virtual Texturing", 0, false, null, 300);

            DebugMenuController.Instance.AddButton("Enable Virtual Texturing", "Virtual Texturing", () => !Paused, () => Paused = !Paused);

            DebugMenuController.Instance.AddActionButton("Wipe Virtual Texture", "Virtual Texturing", ClearVirtualTexture);

            DebugMenuController.Instance.AddTexture("State Table", "Virtual Texturing", StateTable.CreateVisualization());

            DebugMenuController.Instance.AddTexture("Indirection Table", "Virtual Texturing", IndirectionTable.CreateVisualization());

            DebugMenuController.Instance.AddTexture("Residency Table", "Virtual Texturing", ResidencyTable.CreateVisualization());

            DebugMenuController.Instance.AddTexture("Albedo Tile Cache", "Virtual Texturing", AlbedoTileCache.CreateVisualization("Albedo"));

            DebugMenuController.Instance.AddTexture("Height Tile Cache", "Virtual Texturing", HeightTileCache.CreateVisualization("Height"));
            
            DebugMenuController.Instance.AddTexture("Flatten Indirection Table", "Virtual Texturing", ConsolidatedIndirectionTable.CreateVisualization());

            DebugMenuController.Instance.AddTexture("Picking Texture", "Virtual Texturing", new TextureRect
            {
                Texture = SvtFeedbackRenderPass.GetPickingTexture()
            });
        }
    }
}