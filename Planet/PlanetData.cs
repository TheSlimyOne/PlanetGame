using System;
using Godot;
namespace Planet;
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

    [Export(PropertyHint.Range, "0, 1")]
    public float MorphFactor
    {
        get => _morphFactor;
        set
        {
            if (_morphFactor != Mathf.Clamp(value, 0, 1))
            {
                _morphFactor = Mathf.Clamp(value, 0, 1);
                EmitChanged();
            }
        }
    }
    private float _morphFactor = 1;
    #endregion

    #region Surface Settings
    [ExportGroup("Surface Settings")]
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
            }
        }
    }
    private ShaderMaterial _shaderMaterial;
    
    public void SetMaterialParameters()
	{
		_shaderMaterial.SetShaderParameter("position_list", GenerateTrianglePoints());
		_shaderMaterial.SetShaderParameter("radius", _radius);
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

    public Vector4[] GenerateTrianglePoints()
    {
        Vector4[] trianglePoints = new Vector4[6 * 5];
		Vector3[] normals = new Vector3[]
		{
			Vector3.Up,
			Vector3.Down,
			Vector3.Right,
			Vector3.Left,
			Vector3.Forward,
			Vector3.Back,
		};

		for (int i = 0; i < 6; i++)
		{
			Vector3 normal = normals[i];
			Vector3 axisA = new(normal.Y, normal.Z, normal.X);
			Vector3 axisB = normal.Cross(axisA);

			trianglePoints[5 * i + 0] = VectorUtils.toVector4(normal, 1);
			trianglePoints[5 * i + 1] = VectorUtils.toVector4(-axisA + axisB + normal, 1);
			trianglePoints[5 * i + 2] = VectorUtils.toVector4(-axisA - axisB + normal, 1);
			trianglePoints[5 * i + 3] = VectorUtils.toVector4(axisA + axisB + normal, 1);
			trianglePoints[5 * i + 4] = VectorUtils.toVector4(axisA - axisB + normal, 1);
		}
        return trianglePoints;
    }

    public MultiMesh GenerateMulitMesh()
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
                GD.Print(uvs[currentIndex]);
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
        string s = "[";
        for (int i = 0; i < triangles.Length; i+=3)
        {
            Vector2 A = VectorUtils.toVector2(vertices[triangles[i + 0]]);
            Vector2 B = VectorUtils.toVector2(vertices[triangles[i + 1]]);
            Vector2 C = VectorUtils.toVector2(vertices[triangles[i + 2]]);
            s += $"{A}, {B}, {C}, ";
        }
        s = s.Remove(s.Length - 2) + "]";
        GD.Print(s);

		ArrayMesh mesh = new();
		Godot.Collections.Array arrays = new();
		arrays.Resize((int)Mesh.ArrayType.Max);
		arrays[(int)Mesh.ArrayType.Vertex] = vertices;
		arrays[(int)Mesh.ArrayType.Index] = triangles;
		arrays[(int)Mesh.ArrayType.Normal] = normals;
		arrays[(int)Mesh.ArrayType.TexUV] = uvs;
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
		mesh.SurfaceSetMaterial(0, _shaderMaterial);

		return new MultiMesh
		{
			InstanceCount = 0,
			Mesh = mesh,
			TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
			UseCustomData = true,
			UseColors = true
		};
	}
}
