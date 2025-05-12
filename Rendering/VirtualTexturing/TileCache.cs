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
        public int ChunkPixelSize { get; private set; }
        public uint GridSize { get; private set; }
        public Image[] RootTiles { get; private set; }
        public Image.Format Format { get; private set; }

        // TODO need to recognize if there is border pixels 
        public TileCache(int chunkPixelSize, uint gridSize, Image.Format format, Image[] rootTiles = null)
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
                        Width = (uint)ChunkPixelSize,
                        Height = (uint)ChunkPixelSize,
                        ArrayLayers = GridSize * GridSize,
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

        //TODO not a fan of this one
        public override void ClearStorageTexture()
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