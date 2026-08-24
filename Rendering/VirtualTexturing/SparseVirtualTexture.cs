using System.Threading.Tasks;
using PlanetGame.Shaders.Dispatchers;
using Godot;
using System.Linq;
using PlanetGame.Shaders.RenderPasses;
using PlanetGame.Rendering.Surface;
using Shaders;
using PlanetGame.Util;

namespace PlanetGame.Rendering.VirtualTexturing
{
    public class SparseVirtualTexture
    {
        public PlanetController PlanetController { get; private set; }
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
        public VTData VirtualTextureData { get; private set; }

        public bool Ready { get; private set; } = true;
        public bool Paused = false;

        
        public SparseVirtualTexture(PlanetController planetController, Vector2I viewSize)
        {
            PlanetController = planetController;
            SaveManager.WorldSave worldSave = SaveManager.GetCurrentSave();
            VirtualTextureData = worldSave.GetSVTData();

            AlbedoTileCache = new(VirtualTextureData, worldSave.TilesAlbedo, Colors.Magenta, Image.Format.Rgba8);
            HeightTileCache = new(VirtualTextureData, worldSave.TilesHeightmap, Colors.Black, Image.Format.R8);

            IndirectionTable = new(VirtualTextureData);
            ConsolidatedIndirectionTable = new(VirtualTextureData);
            ResidencyTable = new(VirtualTextureData);
            StateTable = new(VirtualTextureData);
            
            SvtFeedbackRenderPass = new(PlanetController, viewSize);

            ResolveTileRequest = new(viewSize) { SparseVirtualTexture = this };

            ValidateTileCache = new() { SparseVirtualTexture = this };

            FlattenIndirectionTableDispatcher = new() { SparseVirtualTexture = this };
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

        public void Invoke()
        {
            if (!Ready || !IsValidForProcessing() || Paused)
                return;

            Ready = false;
            StateTable.ClearStorageTexture();


            SvtFeedbackRenderPass.Invoke(
                Utilities.ToViewPushConstants(
                    PlanetController.MainCamera.GetViewProjectionMatrix(),
                    PlanetController.MainCamera.GlobalPosition,
                    Mathf.Tan(PlanetController.MainCamera.GetCameraFov(true) / 2)
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

            ResolveTileRequest = default;
            ValidateTileCache = default;
            SvtFeedbackRenderPass = default;
        }
    }
}