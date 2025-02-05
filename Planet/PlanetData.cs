using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dispatcher;
using Godot;
using Godot.Collections;
using Shaders;
using Uniform;
namespace Planet
{
    [Tool]
    [GlobalClass]
    public partial class PlanetData : Resource
    {
        #region Planet Settings
        [ExportGroup("Planet Settings")]
        [Export(PropertyHint.Range, "1,8000")]
        public float Radius
        {
            get => _radius;
            set
            {
                if (_radius != Mathf.Clamp(value, 1, 8000))
                {
                    _radius = Mathf.Clamp(value, 1, 8000);
                    EmitChanged();

                }
            }
        }
        private float _radius;

        [Export]
        public float HeightScale
        {
            get => _heightScale;
            set
            {
                if (_heightScale != value)
                {
                    _heightScale = value;
                    EmitChanged();

                }
            }
        }
        private float _heightScale;
        #endregion

        #region  Planet Transforms
        public Transform3D Translation
        {
            get => _translation;
            set
            {
                if (_translation != value)
                {
                    _translation = value;
                    EmitChanged();
                }
            }
        }
        private Transform3D _translation = Transform3D.Identity;
        public void Translate(Vector3 offset)
        {
            _translation = _translation.Translated(offset);
        }

        public Transform3D Rotation
        {
            get => _rotation;
            set
            {
                if (_rotation != value)
                {
                    _rotation = value;
                    EmitChanged();
                }
            }
        }
        private Transform3D _rotation = Transform3D.Identity;
        public void Rotate(Vector3 axis, float angle)
        {
            _rotation = _rotation.Rotated(axis, angle).Orthonormalized();
        }
        public void Rotate(Vector3 axis, float angle, float weight)
        {
            _rotation = _rotation.InterpolateWith(_rotation.Rotated(axis, angle).Orthonormalized(), weight);
        }

        public Transform3D Scale
        {
            get => _scale;
            set
            {
                if (_scale != value)
                {
                    _scale = value;
                    EmitChanged();
                }
            }
        }
        private Transform3D _scale = Transform3D.Identity;
        public void Scaled(Vector3 scale)
        {
            _scale = _scale.Scaled(scale);
        }

        public Transform3D GetPlanetTransformMatrix()
        {
            return _translation * _rotation * _scale;
        }
        public Transform3D GetPlanetTRMatrix()
        {
            return _translation * _rotation;
        }
        public Vector3 TransformPoint(Vector3 point)
        {
            return GetPlanetTRMatrix().Inverse() * point;
        }

        #endregion

        #region LOD Settings
        [ExportGroup("LOD Settings")]
        [Export(PropertyHint.Range, "2,500,")]
        public int Resolution
        {
            get => _resolution;
            set
            {
                if (_resolution != Mathf.Clamp(value, 2, 500))
                {
                    _resolution = Mathf.Clamp(value, 2, 500);
                    EmitChanged();
                }
            }
        }
        private int _resolution = 3;

        [Export]
        public int MaximumNodes
        {
            get => _maximumNodes;
            set
            {
                if (_resolution != value)
                {
                    _maximumNodes = value;
                    EmitChanged();
                }
            }
        }
        private int _maximumNodes = 40000;

        [Export(PropertyHint.Range, "0, 31")]
        public int MaximumLOD
        {
            get => _maximumLOD;
            set
            {
                if (_maximumLOD != value)
                {
                    _maximumLOD = value;
                    EmitChanged();
                }
            }
        }
        private int _maximumLOD = 12;

        [Export(PropertyHint.Range, "1, 10")]
        public float SubFactor
        {
            get => _subFactor;
            set
            {
                if (_subFactor != Mathf.Clamp(value, 0, 10))
                {
                    _subFactor = Mathf.Clamp(value, 0, 10);
                    EmitChanged();
                }
            }
        }
        private float _subFactor = 1;

        public int CurrentLod { get; set; }

        [Export]
        public Vector2 MorphRange
        {
            get => _morphRange;
            set
            {
                if (_morphRange != value)
                {
                    _morphRange = value;
                    EmitChanged();
                }
            }
        }
        private Vector2 _morphRange = new(0, 0);

        [Export(PropertyHint.Range, "1, 256")]
        public int NodeSize
        {
            get => _nodeSize;
            set
            {
                if (_nodeSize != Mathf.Clamp(value, 1, 265))
                {
                    _nodeSize = Mathf.Clamp(value, 1, 265);
                    EmitChanged();
                }
            }
        }
        private int _nodeSize = 16;

        [Export(PropertyHint.Range, "0, 4")]
        public int StartingLod
        {
            get => _startingLod;
            set
            {
                if (_startingLod != Mathf.Clamp(value, 0, 4))
                {
                    _startingLod = Mathf.Clamp(value, 0, 4);
                    EmitChanged();
                }
            }
        }
        private int _startingLod = 1;
        #endregion

        #region Surface Settings
        [ExportGroup("Surface Settings")]

        [Export(PropertyHint.Range, "1, 100")]
        public int CenterSize
        {
            get => _centerSize;
            set
            {
                if (_centerSize != value)
                {
                    _centerSize = value;
                    EmitChanged();
                }
            }
        }
        private int _centerSize = 2;

        [Export(PropertyHint.Range, "0, 100")]
        public int BorderSize
        {
            get => _borderSize;
            set
            {
                if (_borderSize != value)
                {
                    _borderSize = value;
                    EmitChanged();
                }
            }
        }
        private int _borderSize = 1;

        [Export(PropertyHint.Range, "128, 8192")]
        public uint DesiredChunkSize
        {
            get => _desiredChunkSize;
            set
            {
                if (_desiredChunkSize != value)
                {
                    _desiredChunkSize = value;
                    EmitChanged();
                }
            }
        }
        private uint _desiredChunkSize = 512;

        [Export]
        public Texture2D AlbedoMap
        {
            get => _albedoMap;
            set
            {
                if (_albedoMap != value)
                {
                    _albedoMap = value;
                    EmitChanged();
                    SurfaceShader?.UpdateParameter("albedo_map");
                }
            }
        }
        private Texture2D _albedoMap = new PlaceholderTexture2D();

        [Export]
        public Texture2D HeightMap
        {
            get => _heightMap;
            set
            {
                if (_heightMap != value)
                {
                    _heightMap = value;
                    EmitChanged();
                }
            }
        }
        private Texture2D _heightMap = new PlaceholderTexture2D();

        [Export]
        public Texture2D NormalMap
        {
            get => _normalMap;
            set
            {
                if (_normalMap != value)
                {
                    _normalMap = value;
                    EmitChanged();
                }
            }
        }
        private Texture2D _normalMap = new PlaceholderTexture2D();

        [Export]
        public float NormalStrength
        {
            get => _normalStrength;
            set
            {
                if (_normalStrength != Mathf.Clamp(value, 0, 10))
                {
                    _normalStrength = Mathf.Clamp(value, 0, 10);
                    EmitChanged();
                }
            }
        }
        private float _normalStrength = 5;
        #endregion

        #region Virtual Texturing Settings

        [Export] public int[] TextureMapping { get; private set; }
        public SparseVirtualTexture SparseVirtualTexture { get; private set; }

        [Export(PropertyHint.File, "*.glsl")] private string cubeComputeShader;

        public void InitializeVirtualTextures(Viewport viewport, Node node)
        {
            Vector2I imageSize = new(8192, 4096);
            // ChunkManager chunkManager = new(imageSize, DesiredChunkSize, CenterSize, BorderSize);
            // chunkManager.QueueCubeMapGeneration("res://Assets/Images/test-image.png");
            // chunkManager.CreateCubeMaps();
            // chunkManager.ChunkDestination = "user://test/chunks/";
            // chunkManager.QueueChunkGeneration("user://test/cubemap/0.png", 0);
            // chunkManager.QueueChunkGeneration("user://test/cubemap/1.png", 1);
            // chunkManager.QueueChunkGeneration("user://test/cubemap/2.png", 2);
            // chunkManager.QueueChunkGeneration("user://test/cubemap/3.png", 3);
            // chunkManager.QueueChunkGeneration("user://test/cubemap/4.png", 4);
            // chunkManager.QueueChunkGeneration("user://test/cubemap/5.png", 5);
            // chunkManager.CreateChunks();

            SparseVirtualTexture = new(viewport, imageSize, DesiredChunkSize,
            [
                // Image.LoadFromFile("user://test/chunks/3-0-0-0.png"),
                // Image.LoadFromFile("user://test/chunks/3-1-0-0.png"),
                // Image.LoadFromFile("user://test/chunks/3-2-0-0.png"),
                // Image.LoadFromFile("user://test/chunks/3-3-0-0.png"),
                // Image.LoadFromFile("user://test/chunks/3-4-0-0.png"),
                // Image.LoadFromFile("user://test/chunks/3-5-0-0.png"),
            ]);
            SparseVirtualTexture.CreateDebugWindow(node);
            SparseVirtualTexture.Enabled = true;
        }


        #endregion

        #region Material Settings
        [ExportGroup("Material Settings")]

        [Export] public BindableShaderMaterial SurfaceShader { get; set; }

        [Export] public BindableShaderMaterial FramebufferShader { get; set; }

        public void UpdateShaderParameters()
        {
            SurfaceShader.UpdateAllParameters();
            FramebufferShader.UpdateAllParameters();
        }

        public void BindVertexShaderParameters(BindableShaderMaterial bindableShaderMaterial, CustomCamera main, CustomCamera helper)
        {
            bindableShaderMaterial.Bind("radius", () => Radius);
            bindableShaderMaterial.FrameDependentBind("height_scale", () => HeightScale);
            bindableShaderMaterial.Bind("resolution", () => Resolution);
            bindableShaderMaterial.Bind("maximum_lod", () => MaximumLOD);
            bindableShaderMaterial.FrameDependentBind("planet_transform_matrix", () => Utilities.ToProjection(GetPlanetTransformMatrix()));

            bindableShaderMaterial.Bind("height_map", () => HeightMap);

            bindableShaderMaterial.FrameDependentBind("is_cube", () => CubeMode);
            bindableShaderMaterial.FrameDependentBind("is_culling", () => Culling);

            bindableShaderMaterial.FrameDependentBind("is_morphing", () => Morphing);
            bindableShaderMaterial.FrameDependentBind("morph_range", () => MorphRange);

            bindableShaderMaterial.FrameDependentBind("camera_position", () => main.GlobalPosition);
            bindableShaderMaterial.FrameDependentBind("fovy", () => Mathf.Tan(helper.GetCameraFov(true) / 2));
            bindableShaderMaterial.Bind("sub_factor", () => SubFactor);
        }

        public void SurfaceShaderBindParameters(CustomCamera main, CustomCamera helper)
        {
            BindVertexShaderParameters(SurfaceShader, main, helper);
            SurfaceShader.Bind("albedo_map", () => AlbedoMap);
            SurfaceShader.Bind("is_texture_1D", () => AlbedoMap is GradientTexture1D);
            SurfaceShader.FrameDependentBind("normal_strength", () => NormalStrength);
            SurfaceShader.Bind("indirection_table", () => SparseVirtualTexture.IndirectionTable.Table);
            SurfaceShader.Bind("tile_cache", () => SparseVirtualTexture.TileCache.Cache);

            SurfaceShader.Bind("grid_size", () => SparseVirtualTexture.IndirectionTable.GridSize);
            SurfaceShader.Bind("total_texture_subdivisions", () => SparseVirtualTexture.IndirectionTable.MipDepth);
            SurfaceShader.FrameDependentBind("texture_mapping", () => TextureMapping);

            SurfaceShader.FrameDependentBind("morphing", () => Morphing);
        }

        public void FramebufferShaderBindParameters(CustomCamera main, CustomCamera helper)
        {
            BindVertexShaderParameters(FramebufferShader, main, helper);
            FramebufferShader.Bind("grid_size", () => SparseVirtualTexture.IndirectionTable.GridSize);
            FramebufferShader.Bind("total_texture_subdivisions", () => SparseVirtualTexture.IndirectionTable.MipDepth);
            FramebufferShader.FrameDependentBind("texture_mapping", () => TextureMapping);
        }

        #endregion

        #region Debug Settings
        [ExportGroup("Debug Settings")]

        [Export]
        public bool CubeMode
        {
            get => _cubeMode;
            set
            {
                if (_cubeMode != value)
                {
                    _cubeMode = value;
                    EmitChanged();
                }
            }
        }
        private bool _cubeMode = false;

        [Export]
        public bool Culling
        {
            get => _culling;
            set
            {
                if (_culling != value)
                {
                    _culling = value;
                    EmitChanged();
                }
            }
        }
        private bool _culling = true;

        [Export]
        public bool Morphing
        {
            get => _morphing;
            set
            {
                if (_morphing != value)
                {
                    _morphing = value;
                    EmitChanged();
                }
            }
        }
        private bool _morphing = true;

        [Export]
        public float Bias1
        {
            get => _bias1;
            set
            {
                if (_bias1 != value)
                {
                    _bias1 = value;
                    EmitChanged();
                }
            }
        }
        private float _bias1;

        [Export]
        public float Bias2
        {
            get => _bias2;
            set
            {
                if (_bias2 != value)
                {
                    _bias2 = value;
                    EmitChanged();
                }
            }
        }
        private float _bias2;

        [Export]
        public float Bias3
        {
            get => _bias3;
            set
            {
                if (_bias3 != value)
                {
                    _bias3 = value;
                    EmitChanged();
                }
            }
        }
        private float _bias3;

        #endregion

        public void ConnectChanged(Action action)
        {
            if (!IsConnected("changed", Callable.From(action)))
            {
                Changed += action;
            }
        }
        public void DisconnectChanged(Action action)
        {
            if (IsConnected("changed", Callable.From(action)))
            {
                Changed -= action;
            }
        }

        #region Generation

        public ArrayMesh TriangleMesh { get; private set; } = new();

        public void GenerateMesh()
        {
            Vector3[] vertices = new Vector3[_resolution * _resolution];
            Vector3[] normals = new Vector3[_resolution * _resolution];
            Vector2[] uvs = new Vector2[_resolution * _resolution];
            int[] triangles = new int[2 * _resolution * _resolution - 4];

            Vector3 normal = Vector3.Back;
            Vector3 axisA = new(normal.Y, normal.Z, normal.X);
            Vector3 axisB = normal.Cross(axisA).Abs();
            int triIndex = 0;
            int vertexIndex = 0;

            for (int x = 0; x < _resolution; x++)
            {
                for (int y = 0; y < _resolution; y++)
                {
                    int currentIndex = vertexIndex++;
                    Vector2 percentage = new Vector2(x, y) / (_resolution - 1) * 2 - Vector2.One;
                    vertices[currentIndex] = normal + percentage.X * axisA + percentage.Y * axisB;


                    normals[currentIndex] = normal;

                    if (triIndex < triangles.Length)
                    {
                        triangles[triIndex++] = currentIndex;
                        triangles[triIndex++] = currentIndex + _resolution;

                        if (y == _resolution - 1 && x < _resolution - 2)
                        {
                            triangles[triIndex++] = currentIndex + _resolution;
                            triangles[triIndex++] = currentIndex + 1;
                        }
                    }
                }
            }

            Godot.Collections.Array arrays = [];
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = vertices;
            arrays[(int)Mesh.ArrayType.Index] = triangles;
            arrays[(int)Mesh.ArrayType.Normal] = normals;
            arrays[(int)Mesh.ArrayType.TexUV] = uvs;

            Rid mesh = TriangleMesh.GetRid();
            RenderingServer.MeshClear(mesh);
            RenderingServer.MeshAddSurfaceFromArrays(mesh, RenderingServer.PrimitiveType.TriangleStrip, arrays);
        }
        #endregion
    }
}

