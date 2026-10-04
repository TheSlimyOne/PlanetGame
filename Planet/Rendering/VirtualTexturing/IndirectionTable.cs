using System;
using Godot;
using PlanetGame.Data;
using Uniform;
namespace PlanetGame.Planet.Rendering.VirtualTexturing
{
    public class IndirectionTable : VirtualTextureTable
    {
        private static VirtualTextureData VirtualTextureData => SaveManager.VirtualTextureData;

        public Texture2DArrayRD Table
        {
            get => (Texture2DArrayRD)_storageTexture;
            protected set => _storageTexture = value;
        }

        public IndirectionTable(string name, Shaders.IGPUResource owner) : base(name, owner, RenderingDevice.UniformType.Image, perserved: false)
        {
            Size = VirtualTextureData.BaseGridSize;
            
            TextureFormat = new RDTextureFormat()
            {
                Width = Size,
                Height = Size,
                ArrayLayers = VirtualTextureData.TotalMipLayers,
                Format = RenderingDevice.DataFormat.R32G32B32A32Sfloat,
                TextureType = RenderingDevice.TextureType.Type2DArray,
                UsageBits = RenderingDevice.TextureUsageBits.StorageBit |
                                RenderingDevice.TextureUsageBits.CanCopyFromBit |
                                RenderingDevice.TextureUsageBits.CanUpdateBit |
                                RenderingDevice.TextureUsageBits.SamplingBit |
                                RenderingDevice.TextureUsageBits.CanCopyToBit
            };

            Rid = RenderingServer.GetRenderingDevice().TextureCreate(
                TextureFormat,
                new RDTextureView()
            );

            Table = new() { TextureRdRid = Rid };

            ClearStorageTexture();
            SetFallbackSlots();
        }

        public override void ClearStorageTexture() => ClearTexture(new Color("00000000"), 0, 1, 0, VirtualTextureData.TotalMipLayers);
        
        public override TextureRect CreateVisualization(string name = "")
        {
            string shaderCode = """
            shader_type canvas_item;
            render_mode unshaded;

            uniform ivec2 grid_size;
            uniform sampler2DArray image : repeat_disable, filter_nearest;

            void fragment() {
                vec2 grid_position = UV * vec2(grid_size);
                ivec2 tile_position = ivec2(floor(grid_position));

                ivec3 image_size = textureSize(image, 0);

                vec2 tile_uv = fract(grid_position);
                
                ivec2 pixel_coordinates = ivec2(tile_uv * vec2(image_size.xy));

                ivec3 texture_index = ivec3(pixel_coordinates, tile_position.y * grid_size.x + tile_position.x);

                vec4 raw_value = texelFetch(image, texture_index, 0);

                uvec4 indirection_data = floatBitsToUint(raw_value);

                float slot = float(indirection_data.x) / 255.0;
                
                COLOR = vec4(slot, float(indirection_data.b), 0.0, 1.0);
            }
            """;

            Vector2I tileCount = new((int)Mathf.Sqrt(VirtualTextureData.TotalMipLayers), 6);

            Image image = Image.CreateEmpty(tileCount.X, tileCount.Y, false, Image.Format.Rgbaf);

            TextureRect texture = new()
            {
                Name = $"Indirection Table {name}",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                Texture = ImageTexture.CreateFromImage(image),
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                Material = new ShaderMaterial() { Shader = new() { Code = shaderCode } }
            };

            ((ShaderMaterial)texture.Material).SetShaderParameter("grid_size", tileCount);
            ((ShaderMaterial)texture.Material).SetShaderParameter("image", Table);

            Visualization = texture;
            return texture;
        }

        public override void SetFallbackSlots()
        {
            uint totalMipLayers = VirtualTextureData.TotalMipLayersPerFace;
            string[] fallBackTiles = VirtualTextureData.FallBackTiles;
            uint gridSize = VirtualTextureData.BaseGridSize;
            int size = (int)Mathf.Sqrt(TileCache.DEFAULT_TILE_SLOTS_COUNT);

            Image[] images = new Image[VirtualTextureData.TotalMipLayers];

            for (uint i = 0; i < fallBackTiles.Length; i++)
            {
                string[] tileData = fallBackTiles[i].Split('_');

                int mipIndex = int.Parse(tileData[0]);
                int normalId = int.Parse(tileData[1]);
                int tileX = int.Parse(tileData[2]);
                int tileY = int.Parse(tileData[3]);

                int tileLayer = (int)totalMipLayers * normalId + mipIndex;

                int mipSize = (int)gridSize >> mipIndex;

                if (images[tileLayer] == null)
                    images[tileLayer] = Image.CreateEmpty((int)gridSize, (int)gridSize, false, FormatConverter.MatchDataFormat(TextureFormat.Format));

                Color data = new(
                    BitConverter.UInt32BitsToSingle(i),
                    BitConverter.UInt32BitsToSingle(0),
                    BitConverter.UInt32BitsToSingle(255),
                    BitConverter.UInt32BitsToSingle(255)
                );

                Vector2I slotIndex = new((int)i % size, (int)i / size);
                Vector3I indirectionIndex = new(tileX, tileY, tileLayer);


                for (int j = 0; j < mipSize; j++)
                    for (int k = 0; k < mipSize; k++)
                        images[tileLayer].SetPixel(tileX + j, tileY + k, data);
            }

            for (uint i = 0; i < images.Length; i++)
                if (images[i] != null)
                    RenderingServer.GetRenderingDevice().TextureUpdate(Rid, i, images[i].GetData());
        }

        // public uint GetSlot(Vector3I indirectionIndex) => BitConverter.SingleToUInt32Bits(GetPixel(indirectionIndex.X, indirectionIndex.Y, indirectionIndex.Z).R);
    }
}