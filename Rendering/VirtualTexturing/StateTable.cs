using System;
using System.Linq;
using Godot;
using PlanetGame.Shaders;

namespace PlanetGame.Rendering.VirtualTexturing
{
    public class StateTable : VirtualTextureTable
    {
        public Texture2DArrayRD Table
        {
            get => (Texture2DArrayRD)_storageTexture;
            protected set => _storageTexture = value;
        }

        public VTData VirtualTextureData { get; }

        // TODO need to recognize if there is border pixels 
        public StateTable(VTData virtualTextureData)
        {
            VirtualTextureData = virtualTextureData;

            uint gridSize = VirtualTextureData.GridSize;

            Format = RenderingDevice.DataFormat.R8G8B8A8Unorm;
            
            Table = new()
            {
                TextureRdRid = RenderingServer.GetRenderingDevice().TextureCreate(
                    new RDTextureFormat()
                    {
                        Format = Format,
                        Width = gridSize,
                        Height = gridSize,
                        ArrayLayers = VirtualTextureData.TotalMipLayers,
                        TextureType = RenderingDevice.TextureType.Type2DArray,
                        UsageBits = RenderingDevice.TextureUsageBits.StorageBit | RenderingDevice.TextureUsageBits.CanCopyFromBit | RenderingDevice.TextureUsageBits.CanUpdateBit | RenderingDevice.TextureUsageBits.SamplingBit | RenderingDevice.TextureUsageBits.CanCopyToBit
                    },
                    new RDTextureView()
                )
            };

            ClearStorageTexture();
        }

        //TODO not a fan of this one
        public override void ClearStorageTexture()
        {
            RenderingServer.GetRenderingDevice().TextureClear(GetRdRid(), new Color("00000000"), 0, 1, 0, VirtualTextureData.TotalMipLayers);
        }

        public override TextureRect CreateVisualization(string name = "")
        {
            string shaderCode = """
            shader_type canvas_item;
            render_mode unshaded;

            uniform ivec2 grid_size;
            uniform sampler2DArray state_table : repeat_disable, filter_nearest;

            void fragment() {
                vec2 grid_position = UV * vec2(grid_size);
                vec2 grid_cell_uv = fract(grid_position);
                ivec2 tile_position = ivec2(floor(grid_position));

                ivec2 tile_size = textureSize(state_table, 0).xy;

                int array_index = tile_position.y * grid_size.x + tile_position.x;

                int mip_index = tile_position.x;

                int mip_grid_size = max(tile_size.x >> mip_index, 1);
                int mip_step = max(tile_size.x / mip_grid_size, 1);

                ivec2 mip_position = ivec2(
                    clamp(grid_cell_uv, vec2(0.0), vec2(1.0 - 0.000001))
                    * float(mip_grid_size)
                );

                ivec2 state_position = mip_position * mip_step;

                vec4 color = texelFetch(
                    state_table,
                    ivec3(state_position, array_index),
                    0
                );
                
                // vec3 texture_coordinate = vec3(grid_cell_uv, float(array_index));
                // vec4 color = textureLod(state_table, texture_coordinate, 0.0);
                if (color.w != 0.0)
                    switch(tile_position.y)
                    {
                    
                        case 0:
                            COLOR = vec4(1.0, 0.0, 0.0, 1.0);
                            break;
                        case 1:
                            COLOR = vec4(0.0, 1.0, 0.0, 1.0);
                            break;
                        case 2:
                            COLOR = vec4(0.0, 0.0, 1.0, 1.0);
                            break;
                        case 3:
                            COLOR = vec4(1.0, 1.0, 0.0, 1.0);
                            break;
                        case 4:
                            COLOR = vec4(0.0, 1.0, 1.0, 1.0);
                            break;
                        case 5:
                            COLOR = vec4(1.0, 0.0, 1.0, 1.0);
                            break;
                        default:
                            COLOR = vec4(1.0, 1.0, 1.0, 1.0);
                            break;
                    }
                else
                {
                    bool is_white = ((tile_position.x + tile_position.y) % 2) == 0;


                    COLOR = is_white
                        ? vec4(0.6, 0.6, 0.6, 0.5)
                        : vec4(0.4, 0.4, 0.4, 0.5);
                }
                
            }
            """;

            Vector2I tileCount = new((int)Mathf.Sqrt(VirtualTextureData.TotalMipLayers), 6);
            
            Image image = Image.CreateEmpty(tileCount.X, tileCount.Y, false, Image.Format.Rgbaf);

            TextureRect texture = new()
            {
                Name = $"State Table {name}",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                Texture = ImageTexture.CreateFromImage(image),
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                Material = new ShaderMaterial() { Shader = new() { Code = shaderCode } }
            };

            ((ShaderMaterial)texture.Material).SetShaderParameter("grid_size", tileCount);
            ((ShaderMaterial)texture.Material).SetShaderParameter("state_table", Table);

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
            throw new NotSupportedException();
        }

        public override Color GetPixel(int x, int y, int z)
        {
            throw new NotImplementedException();
        }

        public override Rid GetRdRid() => Table.TextureRdRid;
    }
}