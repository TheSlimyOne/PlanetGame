using System;
using System.Linq;
using Dispatcher;
using Godot;
using Godot.Collections;
using Planet;
using Uniform;

public class ResidencyTable : IVirtualTextureDebuggable
{
    public Texture2Drd Table { get; set; }
    public uint GridSize { get; private set; }
    public uint RootTileAmount { get; private set; }

    // TODO need to recognize if there is border pixels 
    public ResidencyTable(uint gridSize, uint rootTileAmount)
    {
        GridSize = gridSize;
        RootTileAmount = rootTileAmount;

        Table = new()
        {
            TextureRdRid = RenderingServer.GetRenderingDevice().TextureCreate(
                new RDTextureFormat()
                {
                    Width = GridSize,
                    Height = GridSize,
                    Format = RenderingDevice.DataFormat.R32G32B32A32Sfloat,
                    TextureType = RenderingDevice.TextureType.Type2D,
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
        RenderingServer.GetRenderingDevice().TextureClear(Table.TextureRdRid, new Color("00000000"), 0, 1, 0, 1);
    }

    public Control GetVisualization()
    {
        TextureRect textureRect = new()
        {
            Name = "Residency Table",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            Texture = Table,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest
        };
        
        textureRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        return textureRect;
    }
}