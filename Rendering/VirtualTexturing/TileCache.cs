using System;
using System.Linq;
using Godot;
using PlanetGame.ComputeShaders;
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
        public Image[] RootTiles { get; private set; }
        public Image.Format Format { get; private set; }
        public Image Placeholder { get; private set; }

        // TODO need to recognize if there is border pixels 
        public TileCache(uint tileSize, uint totalTileSlots, uint totalSubdivisions, string tileDirectory, Color placeholderColor, Image.Format format)
        {
            TileSize = tileSize;
            TotalSubdivisions = totalSubdivisions;
            GridSize = (uint)Mathf.Pow(2, totalSubdivisions - 1);
            TileDirectory = tileDirectory;
            Format = format;
            TotalTextureSlots = totalTileSlots;
            Placeholder = Image.CreateEmpty((int)TileSize, (int)TileSize, false, format);
            Placeholder.Fill(placeholderColor);
            RootTiles = GetPermanentTiles(Placeholder);

            GD.Print($"Creating Tile Cache of {TileSize} x {TileSize} x {TotalTextureSlots} slots");

            Cache = new()
            {
                TextureRdRid = RenderingServer.GetRenderingDevice().TextureCreate(
                    new RDTextureFormat()
                    {
                        Width = TileSize,
                        Height = TileSize,
                        ArrayLayers = totalTileSlots,
                        Format = FormatConverter.MatchDataFormat(Format),
                        TextureType = RenderingDevice.TextureType.Type2DArray,
                        UsageBits = RenderingDevice.TextureUsageBits.StorageBit | RenderingDevice.TextureUsageBits.CanCopyFromBit | RenderingDevice.TextureUsageBits.CanUpdateBit | RenderingDevice.TextureUsageBits.SamplingBit | RenderingDevice.TextureUsageBits.CanCopyToBit
                    },
                    new RDTextureView()
                )
            };

            ClearStorageTexture();
            CreateVisualization();
        }

        public Image[] GetPermanentTiles(Image placeholder)
        {
            Image[] images = new Image[6];
            for (int i = 0; i < 6; i++)
            {
                string tilePath = $"{TileDirectory}/{TotalSubdivisions - 1}-{i}-0-0.png";
                images[i] = FileAccess.FileExists(tilePath) ?
                    Image.LoadFromFile(tilePath) : placeholder;
            }
            return images;
        }

        //TODO not a fan of this one
        public override void ClearStorageTexture()
        {
            RenderingServer.GetRenderingDevice().TextureClear(Cache.TextureRdRid, new Color("00000000"), 0, 1, 0, TotalTextureSlots);
            for (uint i = 0; i < RootTiles.Length; i++)
            {
                if (RootTiles[i].GetFormat() != Format)
                    RootTiles[i].Convert(Format);

                RenderingServer.GetRenderingDevice().TextureUpdate(Cache.TextureRdRid, i, RootTiles[i].GetData());
            }
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
                RenderingServer.GetRenderingDevice().TextureUpdate(Cache.TextureRdRid, slot, tile.GetData());
            }));
        }

        protected override void CreateVisualization()
        {
            Shader shader = GD.Load<Shader>(ShaderPaths.ARRAY_TEXTURE_VISUALIZER);
            GridContainer gridContainer = new()
            {
                Columns = (int)GridSize,
                Name = "Tile Cache",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            gridContainer.SetAnchorsPreset(Control.LayoutPreset.FullRect);

            for (int index = 0; index < TotalTextureSlots; index++)
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
                ((ShaderMaterial)rect.Material).SetShaderParameter("table", Cache);

                gridContainer.AddChild(rect);
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
            if (Cache.TextureRdRid.IsValid)
                RenderingServer.GetRenderingDevice().FreeRid(Cache.TextureRdRid);
        }

    }
}