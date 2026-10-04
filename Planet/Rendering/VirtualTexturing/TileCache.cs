using System;
using Godot;
using PlanetGame.Data;

namespace PlanetGame.Planet.Rendering.VirtualTexturing
{
    public class TileCache : VirtualTextureTable
    {
        public enum TileCacheType
        {
            UNDEFINED,
            ALBEDO,
            HEIGHTMAP,
            LAYER,
            MAX
        }

        public readonly TileFile DataSource;

        public static string GetTileCacheTypeName(TileCacheType tileCacheType)
        {
            return tileCacheType switch
            {
                TileCacheType.ALBEDO => "Albedo",
                TileCacheType.HEIGHTMAP => "Heightmap",
                TileCacheType.LAYER => "Layer",
                _ => ""
            };
        }

        private static VirtualTextureData VirtualTextureData => SaveManager.VirtualTextureData;

        public const uint DEFAULT_TILE_SLOTS_COUNT = 512;

        // private readonly Tile[] _tiles = new Tile[DEFAULT_TILE_SLOTS_COUNT];

        public Texture2DArrayRD Cache
        {
            get => (Texture2DArrayRD)_storageTexture;
            protected set => _storageTexture = value;
        }

        public readonly Image Placeholder;

        public TileCache(string name, Shaders.IGPUResource owner, string dataSourcePath, Color placeholderColor, Image.Format format) : base(name, owner, RenderingDevice.UniformType.SamplerWithTexture, perserved: false)
        {
            DataSource = new(dataSourcePath);
            
            Size = DataSource.TileSize;
            
            TextureFormat = new RDTextureFormat()
            {
                Width = Size,
                Height = Size,
                ArrayLayers = DEFAULT_TILE_SLOTS_COUNT,
                Format = FormatConverter.MatchDataFormat(format),
                TextureType = RenderingDevice.TextureType.Type2DArray,
                UsageBits = RenderingDevice.TextureUsageBits.StorageBit | RenderingDevice.TextureUsageBits.CanCopyFromBit | RenderingDevice.TextureUsageBits.CanUpdateBit | RenderingDevice.TextureUsageBits.SamplingBit | RenderingDevice.TextureUsageBits.CanCopyToBit
            };

            Rid = RenderingServer.GetRenderingDevice().TextureCreate(
                TextureFormat,
                new RDTextureView()
            );

            Cache = new() { TextureRdRid = Rid };

            Placeholder = Image.CreateEmpty((int)Size, (int)Size, false, format);
            Placeholder.Fill(placeholderColor);

            ClearStorageTexture();
            SetFallbackSlots();
        }

        public override void ClearStorageTexture() => ClearTexture(new Color("00000000"), 0, 1, 0, DEFAULT_TILE_SLOTS_COUNT);


        public bool InsertTile(Tile tile, uint slot)
        {
            // _tiles[slot] = tile;
            byte[] imageData = DataSource.GetTileData(tile);

            RenderingServer.CallOnRenderThread(Callable.From(() =>
            {
                RenderingServer.GetRenderingDevice().TextureUpdate(
                    Rid, slot, imageData
                );
            }));

            return true;
        }

        public Image GetTileImage(uint mipIndex, uint normalId, uint xIndex, uint yIndex)
        {
            return DataSource.GetTileImage(mipIndex, normalId, xIndex, yIndex);
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

        public override void SetFallbackSlots()
        {
            string[] fallBackTiles = VirtualTextureData.FallBackTiles;

            for (uint slot = 0; slot < fallBackTiles.Length; slot++)
            {
                string tileName = fallBackTiles[slot];
                Tile tile = new(tileName);
                InsertTile(tile, slot);
            }
        }
    }
}