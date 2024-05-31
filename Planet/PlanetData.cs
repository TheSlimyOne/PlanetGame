using Godot;

[Tool]
[GlobalClass]
public partial class PlanetData : Resource
{

    #region Variables
    [ExportGroup("Planet Settings")]
    [Export(PropertyHint.Range, "1,1000")]
    public float Radius
    {
        get => _radius;
        set
        {   
            if (_radius != Mathf.Clamp(value, 1, 1000))
            {
                _radius = Mathf.Clamp(value, 1, 1000);
                EmitChanged();
            }
        }
    }
    private float _radius;

    [Export(PropertyHint.Range, "0,50")]
    public float HeightScale
    {
        get => _heightScale;
        set
        {
            if (_heightScale != Mathf.Clamp(value, 0, 50))
            {
                _heightScale = Mathf.Clamp(value, 0, 50);
                EmitChanged();
            }
        }
    }
    private float _heightScale;

    [ExportGroup("LOD Settings")]
    [Export(PropertyHint.Range, "2,100,")]
    public int Resolution
    {
        get => _resolution;
        set
        {
            if (_resolution != Mathf.Clamp(value, 2, 100))
            {
                _resolution = Mathf.Clamp(value, 2, 100);
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
    private float _subFactor;

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
    private Texture2D _albedoMap;

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
    private Texture2D _heightMap;

    [Export]
    public Curve HeightGradient
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
    private Curve _heightGradient = new Curve();

    [Export(PropertyHint.Range, "0, 10")]
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
    private float _normalStrength;

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

    public GradientTexture1D CreateHeightGradientTexture()
    {
        int samples = HeightGradient.BakeResolution;
        GradientTexture1D gradient = new() { Gradient = new() };

        Color[] colors = new Color[samples];
        float[] colorValues = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float percentage = (float)i / (samples - 1);
            colorValues[i] = percentage;
            float colorValue = HeightGradient.Sample(percentage);

            colors[i] = new Color(colorValue, colorValue, colorValue);
        }

        gradient.Gradient.Colors = colors;
        gradient.Gradient.Offsets = colorValues;

        return gradient;
    }

}
