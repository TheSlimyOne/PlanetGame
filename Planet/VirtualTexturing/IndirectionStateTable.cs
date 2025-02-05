using System;
using System.Linq;
using Dispatcher;
using Godot;
using Godot.Collections;
using Planet;
using Uniform;

public class IndirectionStateTable : IVirtualTextureDebuggable
{
    public Texture2DArrayRD Table { get; private set; }

    public uint GridSize { get; private set; }
    public uint MipDepth { get; private set; }
    public uint RootTileAmount { get; private set; }

    // TODO need to recognize if there is border pixels 
    public IndirectionStateTable(uint gridSize, uint mipDepth, uint rootTileAmount)
    {
        GridSize = gridSize;
        RootTileAmount = rootTileAmount;
        MipDepth = mipDepth;

        Table = new()
        {
            TextureRdRid = RenderingServer.GetRenderingDevice().TextureCreate(
                new RDTextureFormat()
                {
                    Format = RenderingDevice.DataFormat.R8G8B8A8Unorm,
                    Width = GridSize,
                    Height = GridSize,
                    ArrayLayers = MipDepth * 6,
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
        RenderingServer.GetRenderingDevice().TextureClear(Table.TextureRdRid, new Color("00000000"), 0, 1, 0, MipDepth * 6);
    }

    public Control GetVisualization()
    {
        Shader shader = GD.Load<Shader>("res://Assets/Shaders/array_texture_visualizer.gdshader");
        GridContainer gridContainer = new()
        {
            Columns = (int)MipDepth,
            Name = "Indirection Table",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        gridContainer.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        for (uint pageIndex = 0; pageIndex < 6; pageIndex++)
        {
            for (uint mipIndex = 0; mipIndex < MipDepth; mipIndex++)
            {
                uint index = MipDepth * pageIndex + mipIndex;
                ColorRect rect = new()
                {
                    SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand,
                    SizeFlagsVertical = Control.SizeFlags.Fill | Control.SizeFlags.Expand,
                    // StretchMode = TextureRect.StretchModeEnum.KeepAspect,
                    TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                    Material = new ShaderMaterial() { Shader = shader }
                };

                ((ShaderMaterial)rect.Material).SetShaderParameter("index", index);
                ((ShaderMaterial)rect.Material).SetShaderParameter("indirection_table", Table);
                
                gridContainer.AddChild(rect);
            }
        }
        return gridContainer;
    }
}