using System;
using System.Linq;
using Godot;
using PlanetGame.Shaders;

namespace PlanetGame.Rendering.VirtualTexturing
{
    public class ResidencyTable : VirtualTextureTable
    {
        public Texture2Drd Table
        {
            get => (Texture2Drd)StorageTexture;
            protected set => StorageTexture = value;
        }

        public VTData VirtualTextureData { get; }
        
        public ResidencyTable(VTData virtualTextureData)
        {
            VirtualTextureData = virtualTextureData;

            uint size = (uint)Mathf.Sqrt(TileCache.DEFAULT_TILE_SLOTS_COUNT);

            Format = RenderingDevice.DataFormat.R32G32B32A32Sfloat;

            Table = new()
            {
                TextureRdRid = RenderingServer.GetRenderingDevice().TextureCreate(
                    new RDTextureFormat()
                    {
                        Width = size,
                        Height = size,
                        Format = Format,
                        TextureType = RenderingDevice.TextureType.Type2D,
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
            RenderingServer.GetRenderingDevice().TextureClear(GetRdRid(), new Color("00000000"), 0, 1, 0, 1);
        }

        public override Control CreateVisualization(string name = "")
        {
            string shaderCode = """
            shader_type canvas_item;
            uniform uint total_resolution_mip_count;

            void fragment() {
                ivec2 tex_size = textureSize(TEXTURE, 0);
                ivec2 pixel_coords = ivec2(UV * vec2(tex_size));
                uvec4 tile_data = floatBitsToUint(texelFetch(TEXTURE, pixel_coords, 0));
             
                if (tile_data.w != 0u) {

                    ivec3 indirection_index = ivec3(uvec3(
                        tile_data.x,
                        tile_data.y,
                        tile_data.z
                    ));


                    uint mip_index = uint(indirection_index.z) % total_resolution_mip_count;
                    float lod_size = float(1u << mip_index);

                    float x = float(indirection_index.x) / lod_size;
                    float y = float(indirection_index.y) / lod_size;
                    // float z = float(mip_index / total_resolution_mip_count); 
                    float z = float(total_resolution_mip_count) - float(mip_index) + 1.0;

                    COLOR = vec4(x, y, float(mip_index + 1u) / float(total_resolution_mip_count), 1);

                    
                }
                else {
                    COLOR = vec4(0, 0, 0, 0);
                }
            }
            """;

            TextureRect textureRect = new()
            {
                Name = $"Residency Table {name}",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                Texture = Table,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                Material = new ShaderMaterial() { Shader = new() { Code = shaderCode } }
            };
            ((ShaderMaterial)textureRect.Material).SetShaderParameter("total_resolution_mip_count", VirtualTextureData.TotalSubdivisions);
            textureRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            return textureRect;
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
            int size = (int)Mathf.Sqrt(TileCache.DEFAULT_TILE_SLOTS_COUNT);

            Image image = Image.CreateEmpty(size, size, false, FormatConverter.MatchDataFormat(Format));

            for (int i = 0; i < fallBackTiles.Length; i++)
            {
                string[] tileData = fallBackTiles[i].Split('_');
                int realMipIndex = int.Parse(tileData[0]);

                int nonNegativeMipIndex = realMipIndex + (int)VirtualTextureData.HighResolutionMipCount;
                int normalId = int.Parse(tileData[1]);
                int tileX = int.Parse(tileData[2]);
                int tileY = int.Parse(tileData[3]);

                int tileLayer = (int)totalMipLayers * normalId + nonNegativeMipIndex;
                // int lodSize = 1 << nonNegativeMipIndex;

                // int gridSize = (int)Mathf.Pow(2, totalMipLayers - 1);
                // int lodGridSize = gridSize / (int)Mathf.Pow(2, nonNegativeMipIndex);

                Vector2I slotIndex = new(i % size, i / size);
                Vector3I indirectionIndex = new(
                    tileX, tileY, tileLayer
                );


                Color data = new(
                    BitConverter.UInt32BitsToSingle((uint)indirectionIndex.X),
                    BitConverter.UInt32BitsToSingle((uint)indirectionIndex.Y),
                    BitConverter.UInt32BitsToSingle((uint)indirectionIndex.Z),
                    BitConverter.UInt32BitsToSingle(255u)
                );

                // GD.PrintS(slotIndex, indirectionIndex);
                GD.PrintS(totalMipLayers, totalMipLayers - nonNegativeMipIndex + 1, nonNegativeMipIndex, realMipIndex);

                image.SetPixelv(slotIndex, data);
            }

            RenderingServer.GetRenderingDevice().TextureUpdate(GetRdRid(), 0, image.GetData());
        }

        public override Rid GetRdRid() => Table.TextureRdRid;

        public override Color GetPixel(int x, int y, int z)
        {
            throw new NotImplementedException();
        }

    }
}