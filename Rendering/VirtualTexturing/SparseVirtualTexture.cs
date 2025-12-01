using System;
using System.Threading;
using System.Threading.Tasks;
using PlanetGame.ComputeShaders.Dispatcher;
using Godot;

namespace PlanetGame.Rendering.VirtualTexturing
{
    public class SparseVirtualTexture
    {
        public ReadFramebufferDispatcher ReadFramebuffer { get; private set; }
        public ValidateCacheDispatcher ValidateTileCache { get; private set; }

        public Viewport Viewport { get; private set; }
        public Window DebugWindow { get; private set; }

        public IndirectionTable IndirectionTable { get; private set; }
        public TileCache AlbedoTileCache { get; private set; }
        public TileCache HeightTileCache { get; private set; }
        public ResidencyTable ResidencyTable { get; private set; }
        public StateTable StateTable { get; private set; }
        
        public uint TilePadding { get; private set; }
        public uint TileSize { get; private set; }

        public bool Ready { get; private set; } = true;
        public bool Paused = false;

        public SparseVirtualTexture(SaveManager.WorldSave worldSave, Viewport viewport)
        {
            Viewport = viewport;

            TileSize = worldSave.TileSize;
            TilePadding = worldSave.TilePadding;
            uint totalSubdivisions = worldSave.TotalLods;

            AlbedoTileCache = new(TileSize, TilePadding, totalSubdivisions, worldSave.TilesAlbedo, Colors.Magenta, Image.Format.Rgba8);
            HeightTileCache = new(TileSize, TilePadding, totalSubdivisions, worldSave.TilesHeightmap, Colors.Black, Image.Format.R8);

            IndirectionTable = new(totalSubdivisions);
            ResidencyTable = new(totalSubdivisions);
            StateTable = new(totalSubdivisions);

            ReadFramebuffer = new()
            {
                SparseVirtualTexture = this,
                Viewport = Viewport
            };

            ValidateTileCache = new()
            {
                SparseVirtualTexture = this
            };

            ReadFramebuffer.CreateUniforms();
            ValidateTileCache.CreateUniforms();
        }

        public bool IsValidForProcessing()
        {
            return ReadFramebuffer != null && ValidateTileCache != null;
        }

        public void CreateDebugWindow(Node sceneReference)
        {
            DebugWindow = GD.Load<PackedScene>("res://Scenes/window.tscn").Instantiate<Window>();
            DebugWindow.Title = "Debug Window";
            sceneReference.AddChild(DebugWindow);
            Control node = DebugWindow.GetChild<Control>(0);
            node.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            node.AddChild(IndirectionTable.Visualization);

            node.AddChild(AlbedoTileCache.Visualization);
            node.AddChild(HeightTileCache.Visualization);

            node.AddChild(ResidencyTable.Visualization);
            node.AddChild(StateTable.Visualization);
        }

        public async void RequestTileSlot(byte[] bytes)
        {
            uint[] data = [.. Util.Utilities.FromBytes<uint>(bytes)];

            if (data.Length > 0)
            {
                // GD.Print($"Tile amount: {data.Length}");

                // (Image, uint)[] tileArray = new (Image, uint)[data.Length];
                await Parallel.ForEachAsync(data, new ParallelOptions { MaxDegreeOfParallelism = 4 }, (tileData, _) =>
                {
                    uint x_coord = tileData & 0xF;
                    uint y_coord = (tileData >> 4) & 0xF;
                    uint mipIndex = (tileData >> 8) & 0xF;
                    uint normalId = (tileData >> 12) & 0xF;
                    uint tileSlot = (tileData >> 16) & 0xFF;
                    string tilePath = $"{mipIndex}-{normalId}-{x_coord}-{y_coord}.png";

                    // // GD.Print("Requesting: ", tilePath);

                    HeightTileCache.InsertTile(tilePath, tileSlot);
                    AlbedoTileCache.InsertTile(tilePath, tileSlot);

                    // GD.PrintS((tileData >> 24) & 0xFF, (tileData >> 16) & 0xFF, (tileData >> 8) & 0xFF);

                    return new ValueTask();
                });

                ValidateTileCache.Invoke();
            }
            Ready = true;
        }

        public void ClearVirtualTexture()
        {
            IndirectionTable.ClearStorageTexture();
            IndirectionTable.SetFallbackSlots();

            AlbedoTileCache.ClearStorageTexture();
            AlbedoTileCache.SetFallbackSlots();

            HeightTileCache.ClearStorageTexture();
            HeightTileCache.SetFallbackSlots();

            ResidencyTable.ClearStorageTexture();
            ResidencyTable.SetFallbackSlots();
        }

        public void Invoke()
        {
            if (!Ready || !IsValidForProcessing() || Paused)
                return;

            Ready = false;
            StateTable.ClearStorageTexture();
            ReadFramebuffer.UpdateUniforms();
            ReadFramebuffer.Invoke();
            ReadFramebuffer.GetTextureIds(Callable.From<byte[]>(RequestTileSlot));
        }

        public void CleanupGPUResources()
        {
            IndirectionTable.CleanupGPU();
            IndirectionTable = null;

            AlbedoTileCache.CleanupGPU();
            AlbedoTileCache = null;

            HeightTileCache.CleanupGPU();
            HeightTileCache = null;

            ResidencyTable.CleanupGPU();
            ResidencyTable = null;

            StateTable.CleanupGPU();
            StateTable = null;

            ReadFramebuffer.CleanupGPU();
            ValidateTileCache.CleanupGPU();

            ReadFramebuffer = null;
            ValidateTileCache = null;
        }
    }
}