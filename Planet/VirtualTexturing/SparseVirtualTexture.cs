using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dispatcher;
using Godot;
using Planet;
using Uniform;

public class SparseVirtualTexture
{
    public ReadFramebufferDispatcher ReadFramebuffer;
    private Callable _executeReadFramebuffer;
    public ValidateCacheDispatcher ValidateTileCache;
    private Callable _executeValidateTileCache;
    public Window DebugWindow;

    public bool Enabled;

    public IndirectionTable IndirectionTable { get; private set; }
    public TileCache TileCache { get; private set; }
    public ResidencyTable ResidencyTable { get; private set; }
    public IndirectionStateTable IndirectionStateTable { get; private set; }

    private bool Executing = false;
    private readonly Image _placeholder;

    public SparseVirtualTexture(Viewport viewport, Vector2I baseImageSize, uint chunkPixelSize, Image[] rootTiles)
    {
        if (baseImageSize.X != 2 * baseImageSize.Y) throw new ArgumentException("BaseImageSize must be 2:1");

        int ratio = baseImageSize.Y / (int)chunkPixelSize;
        uint totalSubdivisions = (uint)Math.Log2(ratio) + 1;
        uint gridSize = (uint)Mathf.Pow(2, totalSubdivisions - 1);

        TileCache = new(chunkPixelSize, gridSize, Image.Format.Rgba8, rootTiles);
        IndirectionTable = new(gridSize, totalSubdivisions, (uint)rootTiles.Length);
        ResidencyTable = new(gridSize, (uint)rootTiles.Length);
        IndirectionStateTable = new(gridSize, totalSubdivisions, (uint)rootTiles.Length);

        ReadFramebuffer = new("res://ComputeShaders/GLSL/ReadFrameBuffer.glsl");
        ReadFramebuffer.SparseVirtualTexture = this;
        ReadFramebuffer.Viewport = viewport;

        ValidateTileCache = new("res://ComputeShaders/GLSL/ValidateTileCache.glsl");
        ValidateTileCache.SparseVirtualTexture = this;

        _executeReadFramebuffer = Callable.From(ReadFramebuffer.Invoke);
        _executeValidateTileCache = Callable.From(ValidateTileCache.Invoke);

        ReadFramebuffer.CreateUniforms();
        ValidateTileCache.CreateUniforms();

        _placeholder = Image.CreateEmpty((int)chunkPixelSize, (int)chunkPixelSize, false, Image.Format.Rgbaf);
        _placeholder.Fill(Colors.Magenta);
    }

    public void CreateDebugWindow(Node sceneReference)
    {
        DebugWindow = GD.Load<PackedScene>("res://Scenes/window.tscn").Instantiate<Window>();
        DebugWindow.Title = "Debug Window";
        sceneReference.AddChild(DebugWindow);
        Control node = DebugWindow.GetChild<Control>(0);
        // node.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        node.AddChild(IndirectionTable.GetVisualization());
        node.AddChild(TileCache.GetVisualization());
        node.AddChild(ResidencyTable.GetVisualization());
        node.AddChild(IndirectionStateTable.GetVisualization());
    }

    private bool Cleaning;
    public async void CleanUpCache()
    {
        Cleaning = true;
        while (Enabled)
        {
            await Task.Delay(1000);
            GD.Print("Cleaning");
            RenderingServer.CallOnRenderThread(_executeValidateTileCache);
        }
    }

    public void UpdateTextures()
    {
        // if (!Cleaning && Enabled) CleanUpCache();
        
        if (!Executing && Enabled)
        {
            Executing = true;
            IndirectionStateTable.ClearCache();
            ReadFramebuffer.UpdateUniforms();
            RenderingServer.CallOnRenderThread(_executeReadFramebuffer);
            ReadFramebuffer.GetTextureIds(Callable.From<byte[]>(RequestTileSlot));
            // Enabled = false;
        }
    }

    public async void RequestTileSlot(byte[] bytes)
    {
        uint[] data = Utilities.FromBytes<uint>(bytes).ToArray();

        if (data.Length > 0)
        {
            GD.Print($"Tile amount: {data.Length}");

            List<Task> tasks = [];
            for (uint i = 0; i < data.Length; i++)
            {
                uint index = i;
                tasks.Add(new Task(() =>
                {
                    uint tileData = data[index];

                    uint x_coord = tileData & 0xF;
                    uint y_coord = (tileData >> 4) & 0xF;
                    uint mipIndex = (tileData >> 8) & 0xF;
                    uint normalId = (tileData >> 12) & 0xF;
                    uint tileSlot = (tileData >> 16) & 0xFF;
                    string path = $"user://test/chunks/{mipIndex}-{normalId}-{x_coord}-{y_coord}.png";
                    GD.PrintS(path, $"in slot: {tileSlot}", FileAccess.FileExists(path));

                    Image image = FileAccess.FileExists(path) ? Image.LoadFromFile(path) : _placeholder;
                    TileCache.InsertTile(image, tileSlot);

                    // GD.Print((index + currentCacheCount) % (IndirectionTable.GridSize * IndirectionTable.GridSize));
                    // UpdateVirtualTexture.AddImage(image, index);

                }));
                tasks[(int)index].Start();
            }
            await Task.WhenAll(tasks);
        }

        Executing = false;
    }
}





