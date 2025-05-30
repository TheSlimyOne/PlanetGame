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
        public IndirectionStateTable IndirectionStateTable { get; private set; }
        
        public uint TotalTileSlots { get; private set; }

        public bool Ready { get; private set; } = true;

        public SparseVirtualTexture(SaveManager.WorldSave worldSave, Viewport viewport)
        {
            Viewport = viewport;
            TotalTileSlots = worldSave.TotalTileSlots;

            uint tileSize = worldSave.TileSize;
            uint totalSubdivisions = worldSave.TotalLods;

            AlbedoTileCache = new(tileSize, TotalTileSlots, totalSubdivisions, worldSave.TilesAlbedo, Colors.Magenta, Image.Format.Rgba8);
            HeightTileCache = new(tileSize, TotalTileSlots, totalSubdivisions, worldSave.TilesHeightmap, Colors.Black, Image.Format.R8);

            IndirectionTable = new(totalSubdivisions);
            ResidencyTable = new(totalSubdivisions);
            IndirectionStateTable = new(totalSubdivisions);

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
            // node.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            node.AddChild(IndirectionTable.Visualization);

            node.AddChild(AlbedoTileCache.Visualization);
            node.AddChild(HeightTileCache.Visualization);

            // node.AddChild(ResidencyTable.Visualization);
            node.AddChild(IndirectionStateTable.Visualization);
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

                    HeightTileCache.InsertTile(tilePath, tileSlot);
                    AlbedoTileCache.InsertTile(tilePath, tileSlot);

                    return new ValueTask();
                });

                ValidateTileCache.Invoke();
            }
            Ready = true;
        }

        public void Invoke()
        {
            if (!IsValidForProcessing() || !Ready)
                return;

            Ready = false;
            IndirectionStateTable.ClearStorageTexture();
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

            IndirectionStateTable.CleanupGPU();
            IndirectionStateTable = null;

            ReadFramebuffer.CleanupGPU();
            ValidateTileCache.CleanupGPU();

            ReadFramebuffer = null;
            ValidateTileCache = null;
        }
    }
}