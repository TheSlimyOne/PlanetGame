using System;
using Godot;
using Godot.Collections;
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
                    SetRenderSurfaceMaterialParameters();
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
                    SetRenderSurfaceMaterialParameters();
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
                    GenerateMesh();
                    SetRenderSurfaceMaterialParameters();
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
                    SetRenderSurfaceMaterialParameters();
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
                    SetRenderSurfaceMaterialParameters();
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
                    SetRenderSurfaceMaterialParameters();
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
                    SetRenderSurfaceMaterialParameters();
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
                    SetRenderSurfaceMaterialParameters();
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
                    SetRenderSurfaceMaterialParameters();
                }
            }
        }
        private int _borderSize = 1;

        [Export(PropertyHint.Range, "128, 8192")]
        public int DesiredChunkSize
        {
            get => _desiredChunkSize;
            set
            {
                if (_desiredChunkSize != value)
                {
                    _desiredChunkSize = value;
                    EmitChanged();
                    SetRenderSurfaceMaterialParameters();
                }
            }
        }
        private int _desiredChunkSize = 512;

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
                    SetRenderSurfaceMaterialParameters();
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
                    SetRenderSurfaceMaterialParameters();
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
                    SetRenderSurfaceMaterialParameters();
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
                    SetRenderSurfaceMaterialParameters();
                }
            }
        }
        private float _normalStrength = 5;
        #endregion

        #region Virtual Texturing Settings

        public IndirectionTable IndirectionTable { get; private set; }
        public ChunkedClipmap ChunkedClipmap { get; private set; }
        public TileCache TileCache { get; private set; }

        public void InitializeVirtualTextures()
        {
            var rd = RenderingServer.GetRenderingDevice();
            var framebuffer = rd.FramebufferCreateEmpty(DisplayServer.WindowGetSize(), RenderingDevice.TextureSamples.Samples1);

            rd.FreeRid(framebuffer);

            ChunkedClipmap = new(DesiredChunkSize, CenterSize, BorderSize, "res://Assets/Images/test-image.png");
            int gridSize = ChunkedClipmap.ImageSize.Y / DesiredChunkSize;

            GD.Print(ChunkedClipmap.TotalSubdivisions);
            IndirectionTable = new(RenderingServer.GetRenderingDevice(), gridSize, ChunkedClipmap.TotalSubdivisions);
            TileCache = new(IndirectionTable, DesiredChunkSize, ChunkedClipmap);

            RenderSurface.SetShaderParameter("indirection_table", IndirectionTable.ToTexture2DArray());
            RenderSurface.SetShaderParameter("grid_size", gridSize);
            RenderSurface.SetShaderParameter("total_texture_subdivisions", ChunkedClipmap.TotalSubdivisions);
            RenderSurface.SetShaderParameter("tile_cache", TileCache.GetTexture());
        }

        #endregion

        #region Material Settings
        [ExportGroup("Material Settings")]
        [Export]
        public ShaderMaterial RenderSurface
        {
            get => _renderSurface;
            set
            {
                if (_renderSurface != value)
                {
                    _renderSurface = value;
                    EmitChanged();
                    SetRenderSurfaceMaterialParameters();
                }
            }
        }
        private ShaderMaterial _renderSurface;

        [Export] public Shader[] Shaders { get; set; }
        
        public void SetRenderSurfaceMaterialParameters()
        {

            if (_renderSurface != null)
            {
                _renderSurface.SetShaderParameter("radius", _radius);
                _renderSurface.SetShaderParameter("albedo_map", _albedoMap);
                _renderSurface.SetShaderParameter("is_texture_1D", _albedoMap is GradientTexture1D);
                _renderSurface.SetShaderParameter("height_map", _heightMap);
                _renderSurface.SetShaderParameter("normal_map", _normalMap);
                _renderSurface.SetShaderParameter("height_scale", _heightScale);
                _renderSurface.SetShaderParameter("is_colorize_lod", _colorizeLod);
                _renderSurface.SetShaderParameter("is_cube", _cubeMode);
                _renderSurface.SetShaderParameter("is_culling", _culling);
                _renderSurface.SetShaderParameter("resolution", _resolution);
                _renderSurface.SetShaderParameter("normal_strength", _normalStrength);
                _renderSurface.SetShaderParameter("is_morphing", _morphing);
                _renderSurface.SetShaderParameter("sub_factor", _subFactor);
                _renderSurface.SetShaderParameter("morph_range", _morphRange);
                _renderSurface.SetShaderParameter("border_size", _borderSize);
                _renderSurface.SetShaderParameter("center_size", _centerSize);
                _renderSurface.SetShaderParameter("desired_chunk_size", _desiredChunkSize);
                _renderSurface.SetShaderParameter("maximum_lod", _maximumLOD);

            }
        }

        ShaderMaterial[] ShaderMaterials; 
        public void PopulateShaderParameters()
        {

        

            // ShaderMaterials = new ShaderMaterial[Shaders.Length];
            // for (int i = 0; i < Shaders.Length; i++)
            // {
            //     Shader shader = Shaders[i];
            //     ShaderMaterials[i] = new ShaderMaterial() { Shader = shader };
            //     foreach (Dictionary parameter in shader.GetShaderUniformList())
            //     {
            //         string variableName =  Utilities.ToCamelCase((StringName)parameter["name"]);

            //         GD.PrintS(parameter["name"], variableName);
            //         // var properity = GetType().GetProperty(variableName);
            //         // // string snakeScale = Utilities.ToSnakeCase
            //         // ShaderMaterials[i].SetShaderParameter(variableName, (Variant)properity.GetValue(this));
            //     }
            // }


        }

        #endregion

        #region Debug Settings
        [ExportGroup("Debug Settings")]
        [Export]
        public bool ColorizeLod
        {
            get => _colorizeLod;
            set
            {
                if (_colorizeLod != value)
                {
                    _colorizeLod = value;
                    EmitChanged();
                    SetRenderSurfaceMaterialParameters();
                }
            }
        }
        private bool _colorizeLod = false;

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
                    SetRenderSurfaceMaterialParameters();
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
                    SetRenderSurfaceMaterialParameters();
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
                    SetRenderSurfaceMaterialParameters();
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
                    SetRenderSurfaceMaterialParameters();
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
                    SetRenderSurfaceMaterialParameters();
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
                    SetRenderSurfaceMaterialParameters();
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
                    uvs[currentIndex] = new Vector2(x, y);
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

            Godot.Collections.Array arrays = new();
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

