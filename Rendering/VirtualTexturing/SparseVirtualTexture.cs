using System.Threading.Tasks;
using PlanetGame.Shaders.Dispatchers;
using Godot;
using System.Linq;
using PlanetGame.Shaders.RenderPasses;
using PlanetGame.Rendering.Surface;

namespace PlanetGame.Rendering.VirtualTexturing
{
    public class SparseVirtualTexture
    {
        public ResolveTileTextureDispatcher ReadFramebuffer { get; private set; }
        public ValidateCacheDispatcher ValidateTileCache { get; private set; }
        public SvtFeedbackRenderPass SvtFeedbackRenderPass { get; private set; }
        public Viewport Viewport { get; private set; }
        public IndirectionTable IndirectionTable { get; private set; }
        public TileCache AlbedoTileCache { get; private set; }
        public TileCache HeightTileCache { get; private set; }
        public ResidencyTable ResidencyTable { get; private set; }
        public StateTable StateTable { get; private set; }

        public uint TileSize { get; private set; }

        public bool Ready { get; private set; } = true;
        public bool Paused = false;

        public SparseVirtualTexture(TerrainTessellator terrainTessellator, SaveManager.WorldSave worldSave, Viewport viewport, Mesh mesh)
        {
            Viewport = viewport;
            TileSize = worldSave.TileSize;
            uint totalSubdivisions = worldSave.TotalLods;

            AlbedoTileCache = new(TileSize, totalSubdivisions, TileCache.DEFAULT_TILE_SLOTS_COUNT, worldSave.TilesAlbedo, Colors.Magenta, Image.Format.Rgba8);
            HeightTileCache = new(TileSize, totalSubdivisions, TileCache.DEFAULT_TILE_SLOTS_COUNT, worldSave.TilesHeightmap, Colors.Black, Image.Format.R8);

            IndirectionTable = new(totalSubdivisions);
            ResidencyTable = new(totalSubdivisions);
            StateTable = new(totalSubdivisions);

            ReadFramebuffer = new()
            {
                SparseVirtualTexture = this,
                // Viewport = Viewport
            };

            ValidateTileCache = new()
            {
                SparseVirtualTexture = this
            };

            SvtFeedbackRenderPass = new(terrainTessellator, this, new Vector2I(1024, 512), mesh);

            ReadFramebuffer.CreateUniforms();
            ValidateTileCache.CreateUniforms();
            SvtFeedbackRenderPass.CreateUniforms();
        }

        public bool IsValidForProcessing()
        {
            return ReadFramebuffer?.IsValid() == true && ValidateTileCache?.IsValid() == true && SvtFeedbackRenderPass?.IsValid() == true;
        }

        public async void CreateDebugWindow(Control container)
        {
            if (!container.IsNodeReady())
                await container.ToSignal(container, Node.SignalName.Ready);

            await container.ToSignal(container, Control.SignalName.Resized);


            ScrollContainer scrollContainer = new()
            {
                Name = "SVTDebugTextures",
                // LayoutMode = 1,
                AnchorsPreset = (int)Control.LayoutPreset.FullRect,
                AnchorRight = 1.0f,
                AnchorBottom = 1.0f,
                OffsetRight = 0.0f,
                OffsetBottom = 0.0f,
                LayoutDirection = Control.LayoutDirectionEnum.Rtl
            };

            BoxContainer boxContainer = container.Size.X <= container.Size.Y ? new VBoxContainer() : new HBoxContainer();

            boxContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            boxContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            boxContainer.LayoutDirection = Control.LayoutDirectionEnum.Ltr;
            

            container.AddChild(scrollContainer);
            scrollContainer.AddChild(boxContainer);

           
            boxContainer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            boxContainer.AddChild(StateTable.CreateVisualization());
            boxContainer.AddChild(IndirectionTable.CreateVisualization());
            boxContainer.AddChild(ResidencyTable.CreateVisualization());

            boxContainer.AddChild(AlbedoTileCache.CreateVisualization("Albedo"));
            boxContainer.AddChild(HeightTileCache.CreateVisualization("Heightmap"));

            TextureRect rect = new() { Texture = SvtFeedbackRenderPass.GetFrameBufferTexture() };
            boxContainer.AddChild(rect);

            foreach (TextureRect texture in boxContainer.GetChildren().Cast<TextureRect>())
            {
                if (boxContainer is VBoxContainer)
                    texture.ExpandMode = TextureRect.ExpandModeEnum.FitHeightProportional;
                else
                    texture.ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional;

            }

        }

        public async void RequestTileSlot(byte[] bytes)
        {
            (uint a, uint b, uint c, uint d)[] data = [.. Util.Utilities.FromBytes<(uint a, uint b, uint c, uint d)>(bytes)];

            // GD.Print(data.Length);

            if (data.Length > 0)
            {
                await Parallel.ForEachAsync(data, new ParallelOptions { MaxDegreeOfParallelism = 4 }, (tileData, _) =>
                {
                    uint xCoord = tileData.a;
                    uint yCoord = tileData.b;
                    uint mipIndex =  tileData.c % IndirectionTable.MipDepth;
                    uint normalId = (tileData.c - mipIndex) / IndirectionTable.MipDepth;
                    uint slot = tileData.d;
                    // uint tileSlot = (tileData >> 16) & 0xFF;

                    string tilePath = $"{mipIndex}-{normalId}-{xCoord}-{yCoord}.png";

                    HeightTileCache.InsertTile(tilePath, slot);
                    AlbedoTileCache.InsertTile(tilePath, slot);

                    return new ValueTask();
                });

                ValidateTileCache.Invoke();
            }
            Ready = true;
            
        }

        public void ClearVirtualTexture()
        {
            IndirectionTable.ClearStorageTexture();
            IndirectionTable.SetFallbackSlots();

            AlbedoTileCache.ClearStorageTexture();
            AlbedoTileCache.SetFallbackSlots();

            HeightTileCache.ClearStorageTexture();
            HeightTileCache.SetFallbackSlots();

            ResidencyTable.ClearStorageTexture();
            ResidencyTable.SetFallbackSlots();
        }

        public void Invoke()
        {
            if (!Ready || !IsValidForProcessing() || Paused)
                return;

            Ready = false;
            StateTable.ClearStorageTexture();

            SvtFeedbackRenderPass.Invoke();

            ReadFramebuffer.UpdateUniforms();
            ReadFramebuffer.Invoke();
            ReadFramebuffer.GetTextureIds(Callable.From<byte[]>(RequestTileSlot));

        }

        public void CleanupGPUResources()
        {
            IndirectionTable.CleanupGPU();
            IndirectionTable = default;

            AlbedoTileCache.CleanupGPU();
            AlbedoTileCache = default;

            HeightTileCache.CleanupGPU();
            HeightTileCache = default;

            ResidencyTable.CleanupGPU();
            ResidencyTable = default;

            StateTable.CleanupGPU();
            StateTable = default;

            ReadFramebuffer.CleanupGPU();
            ValidateTileCache.CleanupGPU();
            SvtFeedbackRenderPass.CleanupGPU();

            ReadFramebuffer = default;
            ValidateTileCache = default;
            SvtFeedbackRenderPass = default;
        }
    }
}