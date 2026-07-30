using System;
using System.Linq;
using Godot;
using PlanetGame.Shaders;
namespace PlanetGame.Rendering.VirtualTexturing
{
    public class TileCache : VirtualTextureTable
    {
        public Texture2DArrayRD Cache
        {
            get => (Texture2DArrayRD)StorageTexture;
            protected set => StorageTexture = value;
        }
        public uint TileSize { get; private set; }
        public uint TotalSubdivisions { get; private set; }
        public uint GridSize { get; private set; }
        public uint TotalTextureSlots { get; private set; }
        public string TileDirectory { get; private set; }
        public Image.Format Format { get; private set; }
        public Image Placeholder { get; private set; }

        public const uint DEFAULT_TILE_SLOTS_COUNT = 256;

        public TileCache(uint tileSize, uint totalSubdivisions, uint totalTileSlots, string tileDirectory, Color placeholderColor, Image.Format format)
        {
            TileSize = tileSize;
            TotalSubdivisions = totalSubdivisions;
            TotalTextureSlots = totalTileSlots;
            GridSize = (uint)Mathf.Pow(2, totalSubdivisions - 1);
            TileDirectory = tileDirectory;
            Format = format;

            Placeholder = Image.CreateEmpty((int)TileSize, (int)TileSize, false, Format);
            Placeholder.Fill(placeholderColor);

            Cache = new()
            {
                TextureRdRid = RenderingServer.GetRenderingDevice().TextureCreate(
                    new RDTextureFormat()
                    {
                        Width = TileSize,
                        Height = TileSize,
                        ArrayLayers = TotalTextureSlots,
                        Format = FormatConverter.MatchDataFormat(Format),
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
            RenderingServer.GetRenderingDevice().TextureClear(GetTableRid(), new Color("00000000"), 0, 1, 0, TotalTextureSlots);
        }

        public void InsertTile(string tilePath, uint slot)
        {
            tilePath = $"{TileDirectory}/{tilePath}";
            Image tile = FileAccess.FileExists(tilePath) ? Image.LoadFromFile(tilePath) : null;
            tile ??= Placeholder;

            if (tile.GetFormat() != Format)
                tile.Convert(Format);

            RenderingServer.CallOnRenderThread(Callable.From(() =>
            {
                RenderingServer.GetRenderingDevice().TextureUpdate(GetTableRid(), slot, tile.GetData());
            }));
        }

        public override Control CreateVisualization(string name)
        {
            Shader shader = GD.Load<Shader>(ShaderPaths.TEXTURE_2D_ARRAY_SHADER);
            Vector2I tileCount = new((int)Mathf.Sqrt(TotalTextureSlots), (int)Mathf.Sqrt(TotalTextureSlots));

            Image image = Image.CreateEmpty(tileCount.X, tileCount.Y, false, Image.Format.Rgbaf);

            TextureRect texture = new()
            {
                Name = $"Tile Cache {name}",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                Texture = ImageTexture.CreateFromImage(image),
                Material = new ShaderMaterial() { Shader = shader },
            };

            ((ShaderMaterial)texture.Material).SetShaderParameter("grid_size", tileCount);
            ((ShaderMaterial)texture.Material).SetShaderParameter("image", Cache);

            return texture;
        }

        public override void CleanupGPU()
        {
            if (GetTableRid().IsValid)
                RenderingServer.GetRenderingDevice().FreeRid(GetTableRid());
        }

        public Image[] GetFallbackTiles()
        {
            Image[] tiles = new Image[6];
            for (uint i = 0; i < 6; i++)
            {
                string tilePath = $"{TileDirectory}/{TotalSubdivisions - 1}-{i}-0-0.png";
                Image rootTile = FileAccess.FileExists(tilePath) ?
                    Image.LoadFromFile(tilePath) : Placeholder;

                if (rootTile.GetFormat() != Format)
                    rootTile.Convert(Format);

                tiles[i] = rootTile;
            }

            return tiles;
        }

        public override void SetFallbackSlots()
        {
            for (uint i = 0; i < 6; i++)
            {
                string tilePath = $"{TileDirectory}/{TotalSubdivisions - 1}-{i}-0-0.png";
                Image rootTile = FileAccess.FileExists(tilePath) ?
                    Image.LoadFromFile(tilePath) : Placeholder;

                if (rootTile.GetFormat() != Format)
                    rootTile.Convert(Format);

                RenderingServer.GetRenderingDevice().TextureUpdate(GetTableRid(), i, rootTile.GetData());
            }
        }

        public override Color GetPixel(int x, int y, int z)
        {
            throw new NotImplementedException();
        }

        public Image GetTile(uint slot)
        {
            byte[] data = RenderingServer.GetRenderingDevice().TextureGetData(GetTableRid(), slot);
            return Image.CreateFromData((int)TileSize, (int)TileSize, false, Format, data);
        }

        public override Rid GetTableRid() => Cache.TextureRdRid;
    }
}