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

        private readonly string BasePath;

        private readonly Image _placeholder;

        public bool Ready { get; private set; } = true;

        public SparseVirtualTexture(string basePath, Vector2I baseImageSize, Viewport viewport, int chunkPixelSize)
        {
            BasePath = basePath;
            if (baseImageSize.X != 2 * baseImageSize.Y) throw new ArgumentException("BaseImageSize must be 2:1");
            Viewport = viewport;

            int ratio = baseImageSize.Y / chunkPixelSize;
            uint totalSubdivisions = (uint)Math.Log2(ratio) + 1;
            uint gridSize = (uint)Mathf.Pow(2, totalSubdivisions - 1);

            Image[] rootTilesAlbedo = [
                Image.LoadFromFile($"{BasePath}/Tiles/Albedo Tiles/{totalSubdivisions - 1}-0-0-0.png"),
                Image.LoadFromFile($"{BasePath}/Tiles/Albedo Tiles/{totalSubdivisions - 1}-1-0-0.png"),
                Image.LoadFromFile($"{BasePath}/Tiles/Albedo Tiles/{totalSubdivisions - 1}-2-0-0.png"),
                Image.LoadFromFile($"{BasePath}/Tiles/Albedo Tiles/{totalSubdivisions - 1}-3-0-0.png"),
                Image.LoadFromFile($"{BasePath}/Tiles/Albedo Tiles/{totalSubdivisions - 1}-4-0-0.png"),
                Image.LoadFromFile($"{BasePath}/Tiles/Albedo Tiles/{totalSubdivisions - 1}-5-0-0.png"),
            ];

            Image[] rootTilesHeightMap = [
                Image.LoadFromFile($"{BasePath}/Tiles/Height Map Tiles/{totalSubdivisions - 1}-0-0-0.png"),
                Image.LoadFromFile($"{BasePath}/Tiles/Height Map Tiles/{totalSubdivisions - 1}-1-0-0.png"),
                Image.LoadFromFile($"{BasePath}/Tiles/Height Map Tiles/{totalSubdivisions - 1}-2-0-0.png"),
                Image.LoadFromFile($"{BasePath}/Tiles/Height Map Tiles/{totalSubdivisions - 1}-3-0-0.png"),
                Image.LoadFromFile($"{BasePath}/Tiles/Height Map Tiles/{totalSubdivisions - 1}-4-0-0.png"),
                Image.LoadFromFile($"{BasePath}/Tiles/Height Map Tiles/{totalSubdivisions - 1}-5-0-0.png"),
            ];
 
            AlbedoTileCache = new(chunkPixelSize, gridSize, Image.Format.Rgba8, rootTilesAlbedo);
            HeightTileCache = new(chunkPixelSize, gridSize, Image.Format.Rgbaf, rootTilesHeightMap);

            IndirectionTable = new(gridSize, totalSubdivisions, (uint)rootTilesAlbedo.Length);
            ResidencyTable = new(gridSize, (uint)rootTilesAlbedo.Length);
            IndirectionStateTable = new(gridSize, totalSubdivisions, (uint)rootTilesAlbedo.Length);

            _placeholder = Image.CreateEmpty(chunkPixelSize, chunkPixelSize, false, Image.Format.Rgbaf);
            _placeholder.Fill(Colors.Magenta);

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
            // GD.Print(data.Length);
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
                    string albedoPath = $"{BasePath}/Tiles/Albedo Tiles/{mipIndex}-{normalId}-{x_coord}-{y_coord}.png";
                    string heightPath = $"{BasePath}/Tiles/Height Map Tiles/{mipIndex}-{normalId}-{x_coord}-{y_coord}.png";
                    Image albedoTile = FileAccess.FileExists(albedoPath) ? Image.LoadFromFile(albedoPath) : _placeholder;
                    Image heightTile = FileAccess.FileExists(heightPath) ? Image.LoadFromFile(heightPath) : _placeholder;

                    // GD.PrintS(path, $"in slot: {tileSlot}", FileAccess.FileExists(path));
                    AlbedoTileCache.InsertTile(albedoTile, tileSlot);
                    HeightTileCache.InsertTile(heightTile, tileSlot);

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