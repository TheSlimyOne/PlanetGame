using System;
using System.Linq;
using Godot;
using PlanetGame.ComputeShaders;

namespace PlanetGame.Rendering.VirtualTexturing
{
    public class IndirectionStateTable : VirtualTextureTable
    {
        public Texture2DArrayRD Table
        {
            get => (Texture2DArrayRD)StorageTexture;
            protected set => StorageTexture = value;
        }

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

            ClearStorageTexture();
            CreateVisualization();
        }

        //TODO not a fan of this one
        public override void ClearStorageTexture()
        {
            RenderingServer.GetRenderingDevice().TextureClear(Table.TextureRdRid, new Color("00000000"), 0, 1, 0, MipDepth * 6);
        }

        protected override void CreateVisualization()
        {
            Shader shader = GD.Load<Shader>(ShaderPaths.ARRAY_TEXTURE_VISUALIZER);
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
                    ((ShaderMaterial)rect.Material).SetShaderParameter("table", Table);

                    gridContainer.AddChild(rect);
                }
            }
            Visualization = gridContainer;
        }

        public override void CleanupGPU()
        {
            Visualization.GetChildren().OfType<ColorRect>()
                .ToList().ForEach(x =>
                {
                    ((ShaderMaterial)x.Material).SetShaderParameter("table", new PlaceholderTexture2D());
                    x.Material = null;
                    x.QueueFree();
                });
            Visualization.QueueFree();

            if (Table.TextureRdRid.IsValid)
                RenderingServer.GetRenderingDevice().FreeRid(Table.TextureRdRid);
        }
    }
}