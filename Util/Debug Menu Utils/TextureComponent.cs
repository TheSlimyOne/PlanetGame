using Godot;

namespace PlanetGame.Util.DebugUIComponents;

public partial class TextureComponent : PanelContainer, IDebugComponent
{
	public string TechnicalName { get; set; }
	public bool IsTemplate { get; set; }

	private Label _label;
	private Button _button;
	private TextureRect _textureRect;

	public void GetNodes()
	{
		_label = GetNode<Label>("%Label");
		_button = GetNode<Button>("%Button");
		_textureRect = GetNode<TextureRect>("%TextureRect");
	}

	public void Initialize(string name, TextureRect textureRect, bool isTemplate = false)
	{
		GetNodes();

		TechnicalName = name.ToCamelCase();
		IsTemplate = isTemplate;

		Name = $"{TechnicalName}TextureComponent";
		_label.Text = name;

		_textureRect.ExpandMode = TextureRect.ExpandModeEnum.FitHeightProportional;
		if (textureRect == null)
		{
			_textureRect.Texture = new PlaceholderTexture2D();
			return;
		}

		_textureRect.Texture = textureRect.Texture;
		_textureRect.Material = textureRect.Material;
		_textureRect.TextureFilter = textureRect.TextureFilter;
		_textureRect.StretchMode = textureRect.StretchMode;
	}

	public override void _EnterTree()
    {
        GetNodes();
        if (!IsTemplate) _button.Pressed += OnPressed;
    }

	public override void _ExitTree()
	{
		if (!IsTemplate && _button != null) _button.Pressed -= OnPressed;
	}

	private void OnPressed()
	{
		_textureRect.Visible = !_textureRect.Visible;
		_button.Text = _textureRect.Visible ? "HIDE" : "SHOW";
	}
}