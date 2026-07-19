using System;
using System.Linq;
using Godot;
using PlanetGame.ComputeShaders;

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
        public uint TotalSubdivisions { get; private set; }

        public ResidencyTable(uint totalSubdivisions)
        {
            TotalSubdivisions = totalSubdivisions;
            GridSize = (uint)Mathf.Pow(2, TotalSubdivisions - 1);

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
            SetFallbackSlots();
            CreateVisualization();
        }

        //TODO not a fan of this one
        public override void ClearStorageTexture()
        {
            RenderingServer.GetRenderingDevice().TextureClear(Table.TextureRdRid, new Color("00000000"), 0, 1, 0, 1);
        }

        protected override void CreateVisualization()
        {
            Shader shader = GD.Load<Shader>(ShaderPaths.RESIDENCY_TABLE_SHADER);
            TextureRect textureRect = new()
            {
                Name = "Residency Table",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                Texture = Table,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                Material = new ShaderMaterial() { Shader = shader }
            };
            ((ShaderMaterial)textureRect.Material).SetShaderParameter("total_mips", (int)TotalSubdivisions);
            textureRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            Visualization = textureRect;
        }

        public override void CleanupGPU()
        {
            Visualization.QueueFree();

            if (Table.TextureRdRid.IsValid)
                RenderingServer.GetRenderingDevice().FreeRid(Table.TextureRdRid);
        }

        public override void SetFallbackSlots()
        {
            Image image = Image.CreateFromData((int)GridSize, (int)GridSize, false, Image.Format.Rgbaf, RenderingServer.GetRenderingDevice().TextureGetData(Table.TextureRdRid, 0));
            for (int i = 0; i < 6; i++)
            {
                image.SetPixel(i, 0, TileManager.EncodeTilePath(1, 1, i, (int)TotalSubdivisions - 1, (int)TotalSubdivisions));
            }
            RenderingServer.GetRenderingDevice().TextureUpdate(Table.TextureRdRid, 0, image.GetData());
        }

        public override Color GetPixel(int x, int y, int z = 0)
        {
            Image image = Image.CreateFromData((int)GridSize, (int)GridSize, false, Image.Format.Rgbaf, RenderingServer.GetRenderingDevice().TextureGetData(Table.TextureRdRid, 0));
            return image.GetPixel(x, y);
        }
    }
}