using System;
using System.Linq;
using Godot;
using PlanetGame.Shaders;
namespace PlanetGame.Rendering.VirtualTexturing
{
    public class TileCache : VirtualTextureTable
    {
        private static VirtualTextureData VirtualTextureData => SaveManager.CurrentWorldSave.VirtualTextureData;

        public Texture2DArrayRD Cache
        {
            get => (Texture2DArrayRD)_storageTexture;
            protected set => _storageTexture = value;
        }
        public string TileDirectory { get; private set; }
        public Image.Format CacheFormat { get; private set; }
        public Image Placeholder { get; private set; }

        public const uint DEFAULT_TILE_SLOTS_COUNT = 256;

        public TileCache(string tileDirectory, Color placeholderColor, Image.Format format)
        {
            TileDirectory = tileDirectory;
            CacheFormat = format;
            Format = FormatConverter.MatchDataFormat(CacheFormat);

            int tileSize = (int)VirtualTextureData.TileSize;
            Placeholder = Image.CreateEmpty(tileSize, tileSize, false, CacheFormat);
            Placeholder.Fill(placeholderColor);

            Cache = new()
            {
                TextureRdRid = RenderingServer.GetRenderingDevice().TextureCreate(
                    new RDTextureFormat()
                    {
                        Width = (uint)tileSize,
                        Height = (uint)tileSize,
                        ArrayLayers = DEFAULT_TILE_SLOTS_COUNT,
                        Format = Format,
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
            RenderingServer.GetRenderingDevice().TextureClear(GetRdRid(), new Color("00000000"), 0, 1, 0, DEFAULT_TILE_SLOTS_COUNT);
        }

        public bool TileExist(string tileName)
        {
            return TileManager.TileExist(TileDirectory, tileName);
        }

        public Image GetTile(string tileName)
        {
            return TileManager.GetTile(TileDirectory, tileName, CacheFormat) ?? Placeholder;

        }

        public void InsertTile(string tileName, uint slot)
        {
            Image tile = TileManager.GetTile(TileDirectory, tileName, CacheFormat) ?? Placeholder;
            
            RenderingServer.CallOnRenderThread(Callable.From(() =>
            {
                RenderingServer.GetRenderingDevice().TextureUpdate(GetRdRid(), slot, tile.GetData());
            }));
        }

        public void InsertTile(Image tile, uint slot)
        {
            if (tile.GetFormat() != CacheFormat)
                tile.Convert(CacheFormat);

            RenderingServer.CallOnRenderThread(Callable.From(() =>
            {
                RenderingServer.GetRenderingDevice().TextureUpdate(GetRdRid(), slot, tile.GetData());
            }));
        }

        public Image CreateTile(string tileName)
        {
            GD.Print($"Creating: {tileName}");
            string[] tileData = tileName.Split('_');
            int mipIndex = int.Parse(tileData[0]);
            int normalId = int.Parse(tileData[1]);
            int tileX = int.Parse(tileData[2]);
            int tileY = int.Parse(tileData[3]);

            TileManager.TileGenerationParams parameters = new()
            {
                TileIndexX = tileX,
                TileIndexY = tileY,
                NormalId = normalId,
                MipIndex = mipIndex,
                SourceFormat = FormatConverter.MatchDataFormat(Format),
                TilesPerSide = 0,
                TileSize = (int)VirtualTextureData.TileSize,
                Padding = 0,
                Destination = TileDirectory
            };
            return TileManager.GenerateBlankTile(parameters);
        }

        public override TextureRect CreateVisualization(string name)
        {
            string shaderCode = """
            shader_type canvas_item;
            render_mode unshaded;

            uniform ivec2 grid_size;
            uniform sampler2DArray image : repeat_disable, source_color, filter_linear;

            void fragment() {
                vec2 grid_position = UV * vec2(grid_size);

                ivec2 cell_position = ivec2(floor(grid_position));
                vec2 tile_uv = fract(grid_position);

                int array_index = cell_position.y * grid_size.x + cell_position.x;

                vec3 texture_coordinate = vec3(tile_uv, float(array_index));

                vec4 color = textureLod(image, texture_coordinate, 0.0);

                if (color.w != 0.0)
                    COLOR = color;
                else
                    COLOR = vec4(0.0, 0.0, 0.0, 1.0);
            }
            """;

            Shader shader = new()
            {
                Code = shaderCode
            };

            int cacheSize = (int)Mathf.Sqrt(DEFAULT_TILE_SLOTS_COUNT);
            Vector2I tileCount = new(cacheSize, cacheSize);

            Image image = Image.CreateEmpty(tileCount.X, tileCount.Y, false, Image.Format.Rgbaf);

            TextureRect texture = new()
            {
                Name = $"Tile Cache {name}",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                Texture = ImageTexture.CreateFromImage(image),
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                Material = new ShaderMaterial()
                {
                    Shader = shader
                }
            };

            ((ShaderMaterial)texture.Material).SetShaderParameter("grid_size", tileCount);
            ((ShaderMaterial)texture.Material).SetShaderParameter("image", Cache);

            Visualization = texture;
            return texture;
        }

        public override void CleanupGPU()
        {
            if (GetRdRid().IsValid)
                RenderingServer.GetRenderingDevice().FreeRid(GetRdRid());
        }

        public override void SetFallbackSlots()
        {
            string[] fallBackTiles = VirtualTextureData.FallBackTiles;

            for (uint i = 0; i < fallBackTiles.Length; i++)
            {
                string tileName = fallBackTiles[i];

                string tilePath = $"{TileDirectory}/{tileName}.png";
                Image rootTile = FileAccess.FileExists(tilePath) ?
                    Image.LoadFromFile(tilePath) : Placeholder;

                if (rootTile.GetFormat() != CacheFormat)
                    rootTile.Convert(CacheFormat);

                RenderingServer.GetRenderingDevice().TextureUpdate(GetRdRid(), i, rootTile.GetData());
            }
        }

        public override Color GetPixel(int x, int y, int z)
        {
            throw new NotImplementedException();
        }

        public Image GetTile(uint slot)
        {
            byte[] data = RenderingServer.GetRenderingDevice().TextureGetData(GetRdRid(), slot);
            int tileSize = (int)VirtualTextureData.TileSize;
            return Image.CreateFromData(tileSize, tileSize, false, CacheFormat, data);
        }

        public override Rid GetRdRid() => Cache.TextureRdRid;
    }
}