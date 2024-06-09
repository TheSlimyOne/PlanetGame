using System;
using Godot;

[Tool]
[GlobalClass]
public partial class PlanetData : Resource
{

    #region Variables
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

    [ExportGroup("Gravity Settings")]
    [Export(PropertyHint.Range, "0, 1000")]
    public float GravityRadius
    {
        get => _gravityRadius;
        set
        {
            if (_gravityRadius != Mathf.Clamp(value, 0, 1000))
            {
                _gravityRadius = Mathf.Clamp(value, 0, 1000);
                EmitChanged();
            }
        }
    }
    private float _gravityRadius;

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
    public CurveTexture HeightGradient
    {
        get => _heightGradient;
        set
        {
            if (_heightGradient != value)
            {
                _heightGradient = value;
                EmitChanged();
            }
        }
    }
    private CurveTexture _heightGradient = new CurveTexture() { Curve = new Curve() };

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

    [ExportGroup("Debug Settings")]
    [Export]
    public bool DebugMode
    {
        get => _debugMode;
        set
        {
            if (_debugMode != value)
            {
                _debugMode = value;
                EmitChanged();
            }
        }
    }
    private bool _debugMode;

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
    private bool _cubeMode;
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

}
