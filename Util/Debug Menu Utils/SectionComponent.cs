using Godot;

namespace PlanetGame.Util.DebugUIComponents;

public partial class SectionComponent : PanelContainer, IDebugComponent
{
	private const int DepthPadding = 20;
	private const int ContentIndent = 60;

	public string TechnicalName { get; set; }
	public bool IsTemplate { get; set; }

	private MarginContainer _sectionLabelMargin;
	private HBoxContainer _sectionLabelContainer;
	private VBoxContainer _contentContainer;
	private Label _label;
	private Control _contentIndent;

	private bool _isOpen;

	private void GetNodes()
	{
		_sectionLabelMargin = GetNode<MarginContainer>("%LabelMargin");
		_sectionLabelContainer = GetNode<HBoxContainer>("%LabelContainer");
		_contentContainer = GetNode<VBoxContainer>("%Content");
		_contentIndent = GetNode<Control>("%ContentIndent");
		_label = GetNode<Label>("%Label");

		_contentIndent.CustomMinimumSize = new Vector2(ContentIndent, 0);

		SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_sectionLabelMargin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_sectionLabelContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_label.SizeFlagsHorizontal = SizeFlags.ExpandFill;

		_sectionLabelMargin.MouseFilter = MouseFilterEnum.Stop;
		_sectionLabelContainer.MouseFilter = MouseFilterEnum.Ignore;
		_label.MouseFilter = MouseFilterEnum.Ignore;
	}

	public void Initialize(string name, int depth, bool isOpen, bool isTemplate = false)
	{
		GetNodes();

		TechnicalName = name.ToCamelCase();
		IsTemplate = isTemplate;
		_isOpen = isOpen;

		Name = $"{TechnicalName}SectionComponent";
		_label.Text = name;

		_sectionLabelMargin.AddThemeConstantOverride("margin_left", depth * DepthPadding);
		_contentContainer.Visible = _isOpen;
	}

	public override void _EnterTree()
	{
		GetNodes();

		if (!IsTemplate && _sectionLabelMargin != null)
			_sectionLabelMargin.GuiInput += OnSectionGuiInput;
	}

	public override void _ExitTree()
	{
		if (!IsTemplate && _sectionLabelMargin != null)
			_sectionLabelMargin.GuiInput -= OnSectionGuiInput;
	}

	public void AddContent(Control control)
	{
		_contentContainer.AddChild(control);
	}

	private void OnSectionGuiInput(InputEvent inputEvent)
	{
		if (inputEvent is not InputEventMouseButton mouseButton || mouseButton.ButtonIndex != MouseButton.Left || !mouseButton.Pressed) return;

		_isOpen = !_isOpen;
		_contentContainer.Visible = _isOpen;

		AcceptEvent();
	}
}