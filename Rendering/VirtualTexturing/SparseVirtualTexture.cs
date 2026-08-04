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
        // public SvtDepthPrepass DepthPrepass { get; private set; }
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

            ResolveTileRequest = new()
            {
                SparseVirtualTexture = this
            };

            ValidateTileCache = new()
            {
                SparseVirtualTexture = this
            };

            Vector2I viewSize = new(1024, 512);



            SvtFeedbackRenderPass = new(terrainTessellator, this, viewSize, mesh);


            // RDTextureFormat depthFormat = new()
            // {
            //     Width = (uint)viewSize.X,
            //     Height = (uint)viewSize.Y,
            //     Depth = 1,
            //     ArrayLayers = 1,
            //     Mipmaps = 1,
            //     Format = RenderingDevice.DataFormat.D32Sfloat,
            //     TextureType = RenderingDevice.TextureType.Type2D,
            //     Samples = RenderingDevice.TextureSamples.Samples1,
            //     UsageBits =
            //         RenderingDevice.TextureUsageBits.DepthStencilAttachmentBit |
            //         RenderingDevice.TextureUsageBits.SamplingBit |
            //         RenderingDevice.TextureUsageBits.CanCopyFromBit

            // };


            // DepthPrepass = new(terrainTessellator, this, _depthTexture, viewSize, mesh);


            ResolveTileRequest.CreateUniforms();
            ValidateTileCache.CreateUniforms();
            // DepthPrepass.CreateUniforms();
            SvtFeedbackRenderPass.CreateUniforms();
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

            TextureRect rect = new() { Texture = SvtFeedbackRenderPass.GetFrameBufferTexture() };
            boxContainer.AddChild(rect);

            // shader = new BindableShaderMaterial()
            // {
            //     Shader = new Shader()
            //     {
            //         Code = """
            //             shader_type canvas_item;
            //             render_mode unshaded;

            //             uniform mat4 inverse_projection_matrix;
            //             uniform float depth_display_range = 200.0;

            //             void fragment()
            //             {
            //                 float depth = texture(TEXTURE, UV).r;



            //                 float linear_depth = 1.0 / (depth * inverse_projection_matrix[2].w + inverse_projection_matrix[3].w);
            //                 linear_depth = clamp(linear_depth / depth_display_range, 0, 1);


            //                 COLOR = vec4(
            //                     vec3(linear_depth),
            //                     1.0
            //                 );
            //             }
            //         """
            //     }
            // };

            // shader.FrameDependentBind("inverse_projection_matrix", () =>
            // {
            //     return camera.GetCameraProjection().Inverse();
            // });
            // shader.FrameDependentBind("depth_display_range", () =>
            // {
            //     return camera.DistanceFromTarget;
            // });
            TextureRect rect1 = new()
            {
                Texture = SvtFeedbackRenderPass.GetPickingTexture(),
            };
            boxContainer.AddChild(rect1);



            // TextureRect depthPreview = new()
            // {
            //     // Texture = new Texture2Drd() { TextureRdRid = _depthTexture },
            //     Material = new ShaderMaterial
            //     {
            //         Shader = new Shader
            //         {
            //             Code = """
            //                 shader_type canvas_item;
            //                 render_mode unshaded;

            //                 void fragment()
            //                 {
            //                     float depth = texture(TEXTURE, UV).r;
            //                     float visible_depth =
            //                         clamp((1.0 - depth) * 1000.0, 0.0, 1.0);

            //                     COLOR = vec4(vec3(visible_depth), 1.0);
            //                 }
            //             """
            //         }
            //     }
            // };

            // boxContainer.AddChild(depthPreview);





            foreach (TextureRect texture in boxContainer.GetChildren().Cast<TextureRect>())
            {
                // texture.ExpandMode = (TextureRect.ExpandModeEnum)1;
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

                    if (!AlbedoTileCache.TileExist(tileName) && realMipIndex < 0)
                    {
                        Image tile = AlbedoTileCache.CreateTile(tileName);
                        AlbedoTileCache.InsertTile(tile, slot);
                    }
                    else
                        AlbedoTileCache.InsertTile(tileName, slot);

                    if (!HeightTileCache.TileExist(tileName) && realMipIndex < 0)
                    {
                        Image tile = HeightTileCache.CreateTile(tileName);
                        HeightTileCache.InsertTile(tile, slot);
                    }
                    else
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

            // DepthPrepass.Invoke();

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