using System;
using System.Linq;
using Godot;

namespace PlanetGame.Rendering.VirtualTexturing
{
    public class ResidencyTable : VirtualTextureTable
    {
        public Texture2Drd Table 
        { 
            get => (Texture2Drd)StorageTexture;
            protected set => StorageTexture = value;
        }

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
            ClearStorageTexture();
            CreateVisualization();
        }

        //TODO not a fan of this one
        public override void ClearStorageTexture()
        {
            RenderingServer.GetRenderingDevice().TextureClear(Table.TextureRdRid, new Color("00000000"), 0, 1, 0, 1);
        }

        protected override void CreateVisualization()
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
            Visualization = textureRect;
        }

        public override void CleanupGPU()
        {
            Visualization.QueueFree();

            if (Table.TextureRdRid.IsValid)
                RenderingServer.GetRenderingDevice().FreeRid(Table.TextureRdRid);
        }
    }
}