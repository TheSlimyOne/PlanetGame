using System;
using System.Linq;
using Dispatcher;
using Godot;
using Godot.Collections;
using Planet;
using Uniform;

public class TileCache : IVirtualTextureDebuggable
{
    public Texture2DArrayRD Cache { get; set; }
    public uint ChunkPixelSize { get; private set; }
    public uint GridSize { get; private set; }
    public Image[] RootTiles { get; private set; }
    public Image.Format Format { get; private set; }

    // TODO need to recognize if there is border pixels 
    public TileCache(uint chunkPixelSize, uint gridSize, Image.Format format, Image[] rootTiles = null)
    {
        ChunkPixelSize = chunkPixelSize;
        GridSize = gridSize;
        Format = format;
        RootTiles = rootTiles;
        GD.Print($"Creating Tile Cache of {ChunkPixelSize} x {ChunkPixelSize} x {GridSize * GridSize} slots");

        Cache = new()
        {
            TextureRdRid = RenderingServer.GetRenderingDevice().TextureCreate(
                new RDTextureFormat()
                {
                    Width = ChunkPixelSize,
                    Height = ChunkPixelSize,
                    ArrayLayers = GridSize * GridSize,
                    Format = FormatConverter.MatchDataFormat(Format),
                    TextureType = RenderingDevice.TextureType.Type2DArray,
                    UsageBits = RenderingDevice.TextureUsageBits.StorageBit | RenderingDevice.TextureUsageBits.CanCopyFromBit | RenderingDevice.TextureUsageBits.CanUpdateBit | RenderingDevice.TextureUsageBits.SamplingBit | RenderingDevice.TextureUsageBits.CanCopyToBit
                },
                new RDTextureView()
            )
        };

        ClearCache();
    }

    //TODO not a fan of this one
    public void ClearCache()
    {
        RenderingServer.GetRenderingDevice().TextureClear(Cache.TextureRdRid, new Color("00000000"), 0, 1, 0, GridSize * GridSize);
        for (uint i = 0; i < RootTiles.Length; i++)
        {
            if (RootTiles[i].GetFormat() != Format)
                RootTiles[i].Convert(Format);
            
            RenderingServer.GetRenderingDevice().TextureUpdate(Cache.TextureRdRid, i, RootTiles[i].GetData());
        }
    }

    public void InsertTile(Image tile, uint slot)
    {
        tile.Convert(Format);
        RenderingServer.CallOnRenderThread(Callable.From(() =>
        {
            RenderingServer.GetRenderingDevice().TextureUpdate(Cache.TextureRdRid, slot, tile.GetData());
        }));
    }

    public Control GetVisualization()
    {
        GridContainer gridContainer = new() 
        {
            Columns = (int)GridSize,
            Name = "Tile Cache",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        gridContainer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        Shader shader = GD.Load<Shader>("res://Assets/Shaders/array_texture_visualizer.gdshader");

        for (int index = 0; index < GridSize * GridSize; index++)
        {
            ColorRect rect = new()
            {
                SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand,
                SizeFlagsVertical = Control.SizeFlags.Fill | Control.SizeFlags.Expand,
                // StretchMode = TextureRect.StretchModeEnum.KeepAspect,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                Material = new ShaderMaterial() { Shader = shader }
            };

            ((ShaderMaterial)rect.Material).SetShaderParameter("index", index);
            ((ShaderMaterial)rect.Material).SetShaderParameter("indirection_table", Cache);

            gridContainer.AddChild(rect);
        }

        return gridContainer;
    }
}