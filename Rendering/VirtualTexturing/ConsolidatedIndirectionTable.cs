using System;
using System.Linq;
using Godot;
using PlanetGame.Shaders;
namespace PlanetGame.Rendering.VirtualTexturing
{
    public class ConsolidatedIndirectionTable : VirtualTextureTable
    {
        public Texture2DArrayRD Table
        {
            get => (Texture2DArrayRD)_storageTexture;
            protected set => _storageTexture = value;
        }

        public VTData VirtualTextureData { get; }
        public ConsolidatedIndirectionTable(VTData virtualTextureData)
        {
            VirtualTextureData = virtualTextureData;
            
            uint gridSize = VirtualTextureData.GridSize;

            Format = RenderingDevice.DataFormat.R32G32B32A32Sfloat;

            Table = new()
            {
                TextureRdRid = RenderingServer.GetRenderingDevice().TextureCreate(
                    new RDTextureFormat()
                    {
                        Width = gridSize,
                        Height = gridSize,
                        ArrayLayers = 6,
                        Format = Format,
                        TextureType = RenderingDevice.TextureType.Type2DArray,
                        UsageBits = RenderingDevice.TextureUsageBits.StorageBit | 
                                    RenderingDevice.TextureUsageBits.CanCopyFromBit | 
                                    RenderingDevice.TextureUsageBits.CanUpdateBit | 
                                    RenderingDevice.TextureUsageBits.SamplingBit | 
                                    RenderingDevice.TextureUsageBits.CanCopyToBit
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
            RenderingServer.GetRenderingDevice().TextureClear(GetRdRid(), new Color("00000000"), 0, 1, 0, 6);
        }

        public override TextureRect CreateVisualization(string name = "")
        {
            string shaderCode = """
            shader_type canvas_item;
            render_mode unshaded;

            uniform sampler2DArray image : repeat_disable, filter_nearest;

            vec3 mip_color(uint mip) {
                vec3 colors[12] = vec3[](
                    vec3(1.0, 0.0, 0.0),
                    vec3(0.0, 1.0, 0.0),
                    vec3(0.0, 0.0, 1.0),
                    vec3(1.0, 1.0, 0.0),
                    vec3(1.0, 0.0, 1.0),
                    vec3(0.0, 1.0, 1.0),
                    vec3(1.0, 0.5, 0.0),
                    vec3(0.5, 0.0, 1.0),
                    vec3(0.0, 0.5, 1.0),
                    vec3(0.5, 1.0, 0.0),
                    vec3(1.0, 0.0, 0.5),
                    vec3(0.7, 0.7, 0.7)
                );

                return colors[int(mip) % 12];
            }

            void fragment() {
                const int FACE_COUNT = 6;
                const float MARGIN = 0.03;

                float face_position = UV.y * float(FACE_COUNT);
                int face_index = min(int(floor(face_position)), FACE_COUNT - 1);

                float local_y = fract(face_position);

                if (local_y < MARGIN || local_y > 1.0 - MARGIN) {
                    COLOR = vec4(0.0, 0.0, 0.0, 1.0);
                    discard;
                }

                local_y = (local_y - MARGIN) / (1.0 - MARGIN * 2.0);

                vec2 face_uv = vec2(UV.x, local_y);

                ivec3 image_size = textureSize(image, 0);

                ivec2 pixel_coordinates = ivec2(face_uv * vec2(image_size.xy));
                pixel_coordinates = clamp(pixel_coordinates, ivec2(0), image_size.xy - 1);

                vec4 raw_value = texelFetch(image, ivec3(pixel_coordinates, face_index), 0);

                uvec4 indirection_data = floatBitsToUint(raw_value);

                uint mip = indirection_data.y;

                COLOR = vec4(mip_color(mip), 1.0);
            }
            """;

            Image image = Image.CreateEmpty(1, 6, false, Image.Format.Rgbaf);

            TextureRect texture = new()
            {
                Name = $"Consolidated Indirection Table {name}",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                Texture = ImageTexture.CreateFromImage(image),
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                Material = new ShaderMaterial() { Shader = new() { Code = shaderCode } }
            };

            ((ShaderMaterial)texture.Material).SetShaderParameter("image", Table);

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
            uint totalMipLayers = VirtualTextureData.TotalSubdivisions;
            string[] fallBackTiles = VirtualTextureData.FallBackTiles;
        
            uint gridSize = VirtualTextureData.GridSize;
            int size = (int)Mathf.Sqrt(TileCache.DEFAULT_TILE_SLOTS_COUNT);
            
            Image[] images = new Image[6];
            
            for (uint i = 0; i < fallBackTiles.Length; i++)
            {
                string[] tileData = fallBackTiles[i].Split('_');
                int realMipIndex = int.Parse(tileData[0]);

                int mipIndex = realMipIndex + (int)VirtualTextureData.HighResolutionMipCount;
                int normalId = int.Parse(tileData[1]);
                int tileX = int.Parse(tileData[2]);
                int tileY = int.Parse(tileData[3]);

                int lodSize = 1 << mipIndex;

                if(images[normalId] == null)
                    images[normalId] = Image.CreateEmpty((int)gridSize, (int)gridSize, false, FormatConverter.MatchDataFormat(Format));

                Color data = new(
                    BitConverter.UInt32BitsToSingle(i),
                    BitConverter.UInt32BitsToSingle((uint)mipIndex),
                    BitConverter.UInt32BitsToSingle(255),
                    BitConverter.UInt32BitsToSingle(255)
                );

                for (int j = 0; j < lodSize; j++)                   
                    for (int k = 0; k < lodSize; k++)
                        images[normalId].SetPixel(tileX + j, tileY + k, data);
            }
                    
            for(uint i = 0; i < images.Length; i++)
                if (images[i] != null)
                    RenderingServer.GetRenderingDevice().TextureUpdate(GetRdRid(), i, images[i].GetData());
        }

        public override Color GetPixel(int x, int y, int z)
        {
            throw new NotImplementedException();
        }

        public override Rid GetRdRid() => Table.TextureRdRid;
    }
}