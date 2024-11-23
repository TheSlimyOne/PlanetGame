using System;
using Godot;
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
                    SetMaterialParameters();
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
                    SetMaterialParameters();
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
                    GenerateMulitMesh();
                    SetMaterialParameters();
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
                    SetMaterialParameters();
                }
            }
        }
        private float _subFactor = 1;

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
                    SetMaterialParameters();
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
                    SetMaterialParameters();
                }
            }
        }
        private int _nodeSize = 16;
        #endregion

        #region Surface Settings
        [ExportGroup("Surface Settings")]
    
        [Export(PropertyHint.Range, "1, 100")]
        public int GridSize
        {
            get => _gridSize;
            set
            {
                if (_gridSize != value)
                {
                    _gridSize = value;
                    EmitChanged();
                    SetMaterialParameters();
                }
                // for (int i = 0; i <= _gridSize; i++)
                // {
                //     GD.Print(_radius / _gridSize * i);
                // }
                // GD.Print("====================");
            }
        }
        private int _gridSize = 5;


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
                    SetMaterialParameters();
                }
            }
        }
        private Texture2D _albedoMap = new PlaceholderTexture2D();

        [Export]
        public Texture2D HeightMap
        {
            get =>_heightMap;
            set
            {
                if (_heightMap != value)
                {
                    _heightMap = value;
                    EmitChanged();
                    SetMaterialParameters();
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
                    SetMaterialParameters();
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
                    SetMaterialParameters();
                }
            }
        }
        private float _normalStrength = 5;
        #endregion

        #region Material Settings
        [ExportGroup("Material Settings")]
        [Export]
        public ShaderMaterial ShaderMaterial
        {
            get => _shaderMaterial;
            set
            {
                if (_shaderMaterial != value)
                {
                    _shaderMaterial = value;
                    EmitChanged();
                    SetMaterialParameters();
                }
            }
        }
        private ShaderMaterial _shaderMaterial;

        public MultiMesh MultiMesh
        {
            get => _multiMesh;
            set
            {
                if (_multiMesh != value)
                {
                    _multiMesh = value;
                    EmitChanged();
                    SetMaterialParameters();
                }
            }
        }
        private MultiMesh _multiMesh = new() { Mesh = new PlaceholderMesh(), InstanceCount = 0, TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true };

        public NodeAtlas NodeAtlas
        {
            get => _nodeAtlas;
            set
            {
                if (_nodeAtlas != value)
                {
                    _nodeAtlas = value;
                    EmitChanged();
                    SetMaterialParameters();
                }
            }
        }
        private NodeAtlas _nodeAtlas;

        public void SetMaterialParameters()
        {
            if (_shaderMaterial != null)
            {
                _shaderMaterial.SetShaderParameter("radius", _radius);
                _shaderMaterial.SetShaderParameter("grid_size", _gridSize);
                _shaderMaterial.SetShaderParameter("position_list", GenerateTrianglePoints());
                _shaderMaterial.SetShaderParameter("albedo_map", _albedoMap);
                _shaderMaterial.SetShaderParameter("is_texture_1D", _albedoMap is GradientTexture1D);
                _shaderMaterial.SetShaderParameter("height_map", _heightMap);
                _shaderMaterial.SetShaderParameter("normal_map", _normalMap);
                _shaderMaterial.SetShaderParameter("height_scale", _heightScale);
                _shaderMaterial.SetShaderParameter("is_colorize_lod", _colorizeLod);
                _shaderMaterial.SetShaderParameter("is_cube", _cubeMode);
                _shaderMaterial.SetShaderParameter("is_culling", _culling);
                _shaderMaterial.SetShaderParameter("resolution", _resolution);
                _shaderMaterial.SetShaderParameter("normal_strength", _normalStrength);
                _shaderMaterial.SetShaderParameter("is_morphing", _morphing);
                _shaderMaterial.SetShaderParameter("sub_factor", _subFactor);
                _shaderMaterial.SetShaderParameter("morph_range", _morphRange);
                _shaderMaterial.SetShaderParameter("atlas_map", _nodeAtlas?.NodeAtlasImage);
            }
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
                    SetMaterialParameters();
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
                    SetMaterialParameters();
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
                    SetMaterialParameters();
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
                    SetMaterialParameters();
                }
            }
        }
        private bool _morphing = true;
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

        public static Vector4[] GenerateTrianglePoints()
        {
            Vector4[] trianglePoints = new Vector4[6 * 6];
            Vector3[] normals = new Vector3[]
            {
                Vector3.Up,
                Vector3.Right,
                Vector3.Back,
                Vector3.Down,
                Vector3.Left,
                Vector3.Forward,
            };

            for (int i = 0; i < 6; i++)
            {
                Vector3 normal = normals[i];
                Vector3 axisA = new(normal.Y, normal.Z, normal.X);
                Vector3 axisB = normal.Cross(axisA);
                // if (i < 3) {
                
                trianglePoints[5 * i + 0] = VectorUtils.toVector4(normal, 1);
                trianglePoints[5 * i + 1] = VectorUtils.toVector4(-axisA + axisB + normal, 1);
                trianglePoints[5 * i + 2] = VectorUtils.toVector4(-axisA - axisB + normal, 1);
                trianglePoints[5 * i + 3] = VectorUtils.toVector4(axisA + axisB + normal, 1);
                trianglePoints[5 * i + 4] = VectorUtils.toVector4(axisA - axisB + normal, 1);
                // }
            }
            return trianglePoints;
        }

        public void GenerateMulitMesh()
        {
            Vector3[] vertices = new Vector3[_resolution * (_resolution + 1) / 2];
            Vector3[] normals = new Vector3[_resolution * (_resolution + 1) / 2];
            Vector2[] uvs = new Vector2[_resolution * (_resolution + 1) / 2];
            int[] triangles = new int[(_resolution - 1) * (_resolution - 1) * 6 / 2];
        
            Vector3 normal = Vector3.Back;
            Vector3 axisA = new(normal.Y, normal.Z, normal.X);
            Vector3 axisB = normal.Cross(axisA).Abs();
            int triIndex = 0;
            int vertexIndex = 0;
            for (int y = 0; y < _resolution; y++)
            {
                for (int x = 0; x < _resolution - y; x++)
                {
                    int currentIndex = vertexIndex++;
                    Vector2 percentage = new Vector2(x, y) / (_resolution - 1);
                    vertices[currentIndex] = normal + (percentage.X * axisA + percentage.Y * axisB);
                    uvs[currentIndex] = new Vector2(x, y);
                    normals[currentIndex] = normal;

                    if (x != _resolution - y - 1)
                    {
                        if (x == _resolution - y - 2)
                        {
                            triangles[triIndex++] = currentIndex;
                            triangles[triIndex++] = currentIndex + 1;
                            triangles[triIndex++] = currentIndex + _resolution - y;
                        }
                        else
                        {
                            bool isXEven = x % 2 == 0;
                            bool isYEven = y % 2 == 0;

                            if ((isXEven && isYEven) || (!isXEven && !isYEven))
                            {
                                triangles[triIndex++] = currentIndex;
                                triangles[triIndex++] = currentIndex + _resolution - y + 1;
                                triangles[triIndex++] = currentIndex + _resolution - y;
                                triangles[triIndex++] = currentIndex;
                                triangles[triIndex++] = currentIndex + 1;
                                triangles[triIndex++] = currentIndex + _resolution - y + 1;
                            }
                            else
                            {
                                triangles[triIndex++] = currentIndex;
                                triangles[triIndex++] = currentIndex + 1;
                                triangles[triIndex++] = currentIndex + _resolution - y;
                                triangles[triIndex++] = currentIndex + 1;
                                triangles[triIndex++] = currentIndex + _resolution - y + 1;
                                triangles[triIndex++] = currentIndex + _resolution - y;
                            }
                        }
                    }
                }
            }
            // string s = "[";
            // for (int i = 0; i < triangles.Length; i+=3)
            // {
            //     Vector2 A = VectorUtils.toVector2(vertices[triangles[i + 0]]);
            //     Vector2 B = VectorUtils.toVector2(vertices[triangles[i + 1]]);
            //     Vector2 C = VectorUtils.toVector2(vertices[triangles[i + 2]]);
            //     s += $"{A}, {B}, {C}, ";
            // }
            // s = s.Remove(s.Length - 2) + "]";
            // GD.Print(s);

            ArrayMesh mesh = new();
            Godot.Collections.Array arrays = new();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = vertices;
            arrays[(int)Mesh.ArrayType.Index] = triangles;
            arrays[(int)Mesh.ArrayType.Normal] = normals;
            arrays[(int)Mesh.ArrayType.TexUV] = uvs;
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            mesh.SurfaceSetMaterial(0, _shaderMaterial);
            _multiMesh.Mesh = mesh;
        }

        public void InitNodeAtlas()
        {
            NodeAtlas = new(RenderingServer.GetRenderingDevice(), _gridSize);
        }

        #endregion
    }
}

