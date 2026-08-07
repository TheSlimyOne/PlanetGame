using System.Threading.Tasks;
using PlanetGame.Shaders.Dispatchers;
using Godot;
using System.Linq;
using PlanetGame.Shaders.RenderPasses;
using PlanetGame.Rendering.Surface;
using Shaders;

namespace PlanetGame.Rendering.VirtualTexturing
{
    public class SparseVirtualTexture
    {
        public ResolveTileRequestDispatcher ResolveTileRequest { get; private set; }
        public ValidateCacheDispatcher ValidateTileCache { get; private set; }
        public SvtFeedbackRenderPass SvtFeedbackRenderPass { get; private set; }

        public IndirectionTable IndirectionTable { get; private set; }
        public TileCache AlbedoTileCache { get; private set; }
        public TileCache HeightTileCache { get; private set; }
        public ResidencyTable ResidencyTable { get; private set; }
        public StateTable StateTable { get; private set; }
        public VTData VirtualTextureData { get; private set; }

        public bool Ready { get; private set; } = true;
        public bool Paused = false;


        public SparseVirtualTexture(TerrainTessellator terrainTessellator, SaveManager.WorldSave worldSave, Mesh mesh)
        {
            VirtualTextureData = SaveManager.GetSVTData(worldSave);

            AlbedoTileCache = new(VirtualTextureData, worldSave.TilesAlbedo, Colors.Magenta, Image.Format.Rgba8);
            HeightTileCache = new(VirtualTextureData, worldSave.TilesHeightmap, Colors.Black, Image.Format.R8);

            IndirectionTable = new(VirtualTextureData);
            ResidencyTable = new(VirtualTextureData);
            StateTable = new(VirtualTextureData);

            Vector2I viewSize = new(1024, 512);
            SvtFeedbackRenderPass = new(terrainTessellator, this, viewSize, mesh);

            ResolveTileRequest = new(viewSize) { SparseVirtualTexture = this };

            ValidateTileCache = new() { SparseVirtualTexture = this };



            SvtFeedbackRenderPass.CreateUniforms();
            ResolveTileRequest.CreateUniforms();
            ValidateTileCache.CreateUniforms();
        }

        public bool IsValidForProcessing()
        {
            return ResolveTileRequest?.IsValid() == true && ValidateTileCache?.IsValid() == true && SvtFeedbackRenderPass?.IsValid() == true;
        }
        public async void CreateDebugWindow(Control container, CustomCamera camera)
        {
            if (!container.IsNodeReady())
                await container.ToSignal(container, Node.SignalName.Ready);

            await container.ToSignal(container, Control.SignalName.Resized);

            ScrollContainer scrollContainer = new()
            {
                Name = "SVTDebugTextures",
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

            TextureRect rect = new()
            {
                Texture = SvtFeedbackRenderPass.GetFrameBufferTexture(),
                Material = new ShaderMaterial
                {
                    Shader = new Shader
                    {
                        Code = """
                            shader_type canvas_item;
                            render_mode unshaded;

                            uniform int low_resolution_mip_count;
                            uniform int high_resolution_mip_count;
                            uniform int grid_size;

                            uint hash_uint(uint value)
                            {
                                value ^= value >> 16u;
                                value *= 0x7FEB352Du;
                                value ^= value >> 15u;
                                value *= 0x846CA68Bu;
                                value ^= value >> 16u;
                                return value;
                            }

                            vec3 hash_color(uvec3 value)
                            {
                                uint hashed =
                                    value.x * 0x9E3779B9u ^
                                    value.y * 0x85EBCA6Bu ^
                                    value.z * 0xC2B2AE35u;

                                hashed = hash_uint(hashed);

                                return vec3(
                                    float(hashed & 0xFFu),
                                    float((hashed >> 8u) & 0xFFu),
                                    float((hashed >> 16u) & 0xFFu)
                                ) / 255.0;
                            }

                            void fragment()
                            {
                                ivec2 texture_size = textureSize(TEXTURE, 0);
                                ivec2 pixel_coords = min(
                                    ivec2(UV * vec2(texture_size)),
                                    texture_size - ivec2(1)
                                );

                                uvec4 feedback = floatBitsToUint(
                                    texelFetch(TEXTURE, pixel_coords, 0)
                                );

                                uvec3 indirection_index = feedback.xyz;
                                bool is_requesting = feedback.w == 1u;

                                COLOR = is_requesting
                                    ? vec4(hash_color(indirection_index), 1.0)
                                    : vec4(0.0);
                            }
                            """
                    }
                }
            };

            ShaderMaterial material = (ShaderMaterial)rect.Material;
            material.SetShaderParameter(
                "low_resolution_mip_count",
                VirtualTextureData.LowResolutionMipCount
            );
            material.SetShaderParameter(
                "high_resolution_mip_count",
                VirtualTextureData.HighResolutionMipCount
            );
            material.SetShaderParameter(
                "grid_size",
                (int)VirtualTextureData.GridSize
            );
            boxContainer.AddChild(rect);

            TextureRect rect1 = new() { Texture = SvtFeedbackRenderPass.GetPickingTexture() };
            boxContainer.AddChild(rect1);



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
            (uint tileX, uint tileY, uint tileZ, uint slot)[] data =
                 [.. Util.Utilities.FromBytes<(uint, uint, uint, uint)>(bytes)];
            // Ready = true;
            // return;

            if (data.Length > 0)
            {
                await Parallel.ForEachAsync(data, new ParallelOptions { MaxDegreeOfParallelism = 4 }, (tileData, _) =>
                {
                    uint xCoord = tileData.tileX;
                    uint yCoord = tileData.tileY;
                    uint mipIndex = tileData.tileZ % VirtualTextureData.TotalSubdivisions;
                    uint normalId = (tileData.tileZ - mipIndex) / VirtualTextureData.TotalSubdivisions;
                    uint slot = tileData.slot;

                    int realMipIndex = (int)(mipIndex - VirtualTextureData.HighResolutionMipCount);


                    string tileName = $"{realMipIndex}_{normalId}_{xCoord}_{yCoord}";

                    // if (!AlbedoTileCache.TileExist(tileName) && realMipIndex < 0)
                    // {
                    //     Image tile = AlbedoTileCache.CreateTile(tileName);
                    //     AlbedoTileCache.InsertTile(tile, slot);
                    // }
                    // else
                    //     AlbedoTileCache.InsertTile(tileName, slot);

                    // if (!HeightTileCache.TileExist(tileName) && realMipIndex < 0)
                    // {
                    //     Image tile = HeightTileCache.CreateTile(tileName);
                    //     HeightTileCache.InsertTile(tile, slot);
                    // }
                    // else
                    //     HeightTileCache.InsertTile(tileName, slot);


                    AlbedoTileCache.InsertTile(tileName, slot);
                    HeightTileCache.InsertTile(tileName, slot);


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

            ResolveTileRequest.ResetTileSlotCounter();
        }

        public void Invoke()
        {
            if (!Ready || !IsValidForProcessing() || Paused)
                return;

            Ready = false;
            StateTable.ClearStorageTexture();


            SvtFeedbackRenderPass.Invoke();

            ResolveTileRequest.UpdateUniforms();
            ResolveTileRequest.Invoke();


            ResolveTileRequest.GetTextureIds(Callable.From<byte[]>(RequestTileSlot));




        }

        // public Vector3 GetMouseClickPosition(Vector2 mousePosition)
        // {
        //     // Vector3 mouse = SvtFeedbackRenderPass.GetMousePosition(mousePosition);

        //     return mouse;
        // }

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

            ResolveTileRequest.CleanupGPU();
            ValidateTileCache.CleanupGPU();
            SvtFeedbackRenderPass.CleanupGPU();

            ResolveTileRequest = default;
            ValidateTileCache = default;
            SvtFeedbackRenderPass = default;
        }
    }
}