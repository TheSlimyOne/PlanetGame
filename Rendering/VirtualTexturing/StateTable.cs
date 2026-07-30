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
            get => (Texture2DArrayRD)StorageTexture;
            protected set => StorageTexture = value;
        }

        public uint GridSize { get; private set; }
        public uint MipDepth { get; private set; }

        // TODO need to recognize if there is border pixels 
        public StateTable(uint totalSubdivisions)
        {
            GridSize = (uint)Mathf.Pow(2, totalSubdivisions - 1);
            MipDepth = totalSubdivisions;

            Table = new()
            {
                TextureRdRid = RenderingServer.GetRenderingDevice().TextureCreate(
                    new RDTextureFormat()
                    {
                        Format = RenderingDevice.DataFormat.R8G8B8A8Unorm,
                        Width = GridSize,
                        Height = GridSize,
                        ArrayLayers = MipDepth * 6,
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
            RenderingServer.GetRenderingDevice().TextureClear(GetTableRid(), new Color("00000000"), 0, 1, 0, MipDepth * 6);
        }

        public override Control CreateVisualization(string name = "")
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

                vec3 texture_coordinate = vec3(grid_cell_uv, float(array_index));

                vec4 color = textureLod(state_table, texture_coordinate, 0.0);
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
                    COLOR = vec4(0, 0, 0, 1);
            }
            """;

            Vector2I tileCount = new((int)Mathf.Sqrt(MipDepth * 6), 6);

            
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

            return texture;
        }

        public override void CleanupGPU()
        {
            if (GetTableRid().IsValid)
                RenderingServer.GetRenderingDevice().FreeRid(GetTableRid());
        }

        public override void SetFallbackSlots()
        {
            throw new NotSupportedException();
        }

        public override Color GetPixel(int x, int y, int z)
        {
            throw new NotImplementedException();
        }

        public override Rid GetTableRid() => Table.TextureRdRid;
    }
}