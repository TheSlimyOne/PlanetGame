using System;
using System.Linq;
using Godot;
using PlanetGame.Shaders;
namespace PlanetGame.Rendering.VirtualTexturing
{
    public class IndirectionTable : VirtualTextureTable
    {
        public Texture2DArrayRD Table
        {
            get => (Texture2DArrayRD)StorageTexture;
            protected set => StorageTexture = value;
        }

        public uint GridSize { get; private set; }
        public uint MipDepth { get; private set; }
        public uint RootTileAmount { get; private set; }

        public IndirectionTable(uint totalSubdivisions)
        {
            //TODO make sure gridSize is a power of 2?
            GridSize = (uint)Mathf.Pow(2, totalSubdivisions - 1);
            MipDepth = totalSubdivisions;
            RootTileAmount = 6;

            Table = new()
            {
                TextureRdRid = RenderingServer.GetRenderingDevice().TextureCreate(
                    new RDTextureFormat()
                    {
                        Width = GridSize,
                        Height = GridSize,
                        ArrayLayers = MipDepth * 6,
                        Format = RenderingDevice.DataFormat.R32G32B32A32Sfloat,
                        TextureType = RenderingDevice.TextureType.Type2DArray,
                        UsageBits = RenderingDevice.TextureUsageBits.StorageBit | RenderingDevice.TextureUsageBits.CanCopyFromBit | RenderingDevice.TextureUsageBits.CanUpdateBit | RenderingDevice.TextureUsageBits.SamplingBit | RenderingDevice.TextureUsageBits.CanCopyToBit
                    },
                    new RDTextureView()
                )
            };

            ClearStorageTexture();
            SetFallbackSlots();
        }

        //TODO not a fan of this one
        public override void ClearStorageTexture()
        {
            RenderingServer.GetRenderingDevice().TextureClear(GetTableRid(), new Color("00000000"), 0, 1, 0, MipDepth * 6);
        }

        public override Control CreateVisualization(string name = "")
        {
            Shader shader = GD.Load<Shader>(ShaderPaths.INDIRECTION_TABLE_SHADER);
            Vector2I tileCount = new((int)Mathf.Sqrt(MipDepth * 6), 6);

            Image image = Image.CreateEmpty(tileCount.X, tileCount.Y, false, Image.Format.Rgbaf);

            TextureRect texture = new()
            {
                Name = $"Indirection Table {name}",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                Texture = ImageTexture.CreateFromImage(image),
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                Material = new ShaderMaterial() { Shader = shader },
            };

            ((ShaderMaterial)texture.Material).SetShaderParameter("grid_size", tileCount);
            ((ShaderMaterial)texture.Material).SetShaderParameter("image", Table);
            
            return texture;
        }

        public override void CleanupGPU()
        {
            if (GetTableRid().IsValid)
                RenderingServer.GetRenderingDevice().FreeRid(GetTableRid());
        }

        public override void SetFallbackSlots()
        {
            for (uint i = 0; i < RootTileAmount; i++)
            {
                uint tileLayer = MipDepth * i + (MipDepth - 1);
                Image image = Image.CreateEmpty((int)GridSize, (int)GridSize, false, Image.Format.Rgbaf);
                Color data = new()
                {
                    R = BitConverter.UInt32BitsToSingle(i),
                    G = BitConverter.UInt32BitsToSingle(0),
                    B = BitConverter.UInt32BitsToSingle(255),
                    A = BitConverter.UInt32BitsToSingle(255),
                };
                image.Fill(data);

                RenderingServer.GetRenderingDevice().TextureUpdate(GetTableRid(), tileLayer, image.GetData());
            }
        }

        public override Color GetPixel(int x, int y, int z)
        {
            byte[] data = RenderingServer.GetRenderingDevice().TextureGetData(GetTableRid(), (uint)z);
            Image image = Image.CreateFromData((int)GridSize, (int)GridSize, false, FormatConverter.MatchDataFormat(RenderingDevice.DataFormat.R32G32B32A32Sfloat), data);
            return image.GetPixel(x, y);
        }

        public uint GetSlot(Vector3I indirectionIndex) => BitConverter.SingleToUInt32Bits(GetPixel(indirectionIndex.X, indirectionIndex.Y, indirectionIndex.Z).R);
        
        public override Rid GetTableRid() => Table.TextureRdRid;
    }
}