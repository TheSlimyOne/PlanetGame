using System;
using Godot;

namespace PlanetGame.Rendering.VirtualTexturing
{
    public class TileCache : VirtualTextureTable
    {
        private static VirtualTextureData VirtualTextureData => SaveManager.CurrentWorldSave.VirtualTextureData;

        public const uint DEFAULT_TILE_SLOTS_COUNT = 256;

        private readonly Tile[] _tiles = new Tile[DEFAULT_TILE_SLOTS_COUNT];

        public Texture2DArrayRD Cache
        {
            get => (Texture2DArrayRD)_storageTexture;
            protected set => _storageTexture = value;
        }
        public readonly string TileDirectory;
        public readonly string BaseDirectory;
        public readonly Image.Format CacheFormat;
        public readonly Image Placeholder;


        public TileCache(string tileDirectory, string baseDirectory, Color placeholderColor, Image.Format format)
        {
            TileDirectory = tileDirectory;
            CacheFormat = format;
            Format = FormatConverter.MatchDataFormat(CacheFormat);
            BaseDirectory = baseDirectory;

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

        public bool TileImageExist(string tileName)
        {
            return TileManager.TileImageExist(TileDirectory, tileName);
        }

        public Image GetTileImage(string tileName)
        {
            return TileManager.GetTileImage(TileDirectory, tileName, CacheFormat) ?? Placeholder;
        }

        public Tile GetTile(string tileName)
        {
            if (!VirtualTextureData.IsValidTileName(tileName))
            {
                GD.PrintErr("Tile is not valid to insert");
                return null;
            }

            string[] tileData = tileName.Split('_');
            int realMipIndex = int.Parse(tileData[0]);
            Tile.TileMipType tileType = realMipIndex >= 0 ? Tile.TileMipType.Base : Tile.TileMipType.Detail;
            
            Tile tile;
            if (!TileImageExist(tileName))
            {
                tile = new(tileName, RequestTile(tileName, tileType), null, this, tileType);
                tile.Image.Convert(CacheFormat);
            }
            else
            {
                tile = new(tileName, null, this, tileType);
                tile.Image.Convert(CacheFormat);
            }

            return tile;
        }


        public bool InsertTile(string tileName, uint slot)
        {
            Tile tile = GetTile(tileName);

            tile.Slot = slot;

            _tiles[slot] = tile;

            RenderingServer.CallOnRenderThread(Callable.From(() =>
            {
                RenderingServer.GetRenderingDevice().TextureUpdate(GetRdRid(), slot, tile.Image.GetData());
            }));

            return true;
        }

        public Image RequestTile(string tileName, Tile.TileMipType tileType)
        {
            GD.Print($"Requesting: {tileName}");
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
                TileSize = (int)VirtualTextureData.TileSize,
                Source = Image.LoadFromFile(BaseDirectory),
                Destination = TileDirectory,
                Padding = 0, // going to need a variable for this in the future
            };

            switch (tileType)
            {
                case Tile.TileMipType.Base:
                    parameters.TilesPerSide = (int)Mathf.Pow(2, VirtualTextureData.LowResolutionMipCount - 1 - mipIndex);

                    return TileManager.GenerateTile(parameters);
                case Tile.TileMipType.Detail:
                    return TileManager.GenerateBlankTile(parameters);
                default:
                    return Placeholder;
            }
        }

        // public Image InsertTile(string tileName, uint slot)
        // {
        //     Image imageTile = TileManager.GetTile(TileDirectory, tileName, CacheFormat) ?? Placeholder;

        //     // _tiles[slot] = Tiel;

        //     RenderingServer.CallOnRenderThread(Callable.From(() =>
        //     {
        //         RenderingServer.GetRenderingDevice().TextureUpdate(GetRdRid(), slot, imageTile.GetData());
        //     }));

        //     return imageTile;
        // }

        // public void InsertTile(Image tile, uint slot)
        // {
        //     if (tile.GetFormat() != CacheFormat)
        //         tile.Convert(CacheFormat);

        //     RenderingServer.CallOnRenderThread(Callable.From(() =>
        //     {
        //         RenderingServer.GetRenderingDevice().TextureUpdate(GetRdRid(), slot, tile.GetData());
        //     }));
        // }

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