using Godot;
using System;
using System.Collections.Generic;

namespace PlanetGame.Util.DebugUIComponents;

public partial class DebugMenuController : PanelContainer
{
	public static DebugMenuController Instance { get; private set; }

	private static readonly PackedScene SectionScene = GD.Load<PackedScene>("res://Util/Debug Menu Utils/Scenes/section_component.tscn");
	private static readonly PackedScene ButtonScene = GD.Load<PackedScene>("res://Util/Debug Menu Utils/Scenes/button_component.tscn");
	private static readonly PackedScene SliderScene = GD.Load<PackedScene>("res://Util/Debug Menu Utils/Scenes/slider_component.tscn");
	private static readonly PackedScene TextureScene = GD.Load<PackedScene>("res://Util/Debug Menu Utils/Scenes/texture_component.tscn");

	private readonly Dictionary<string, SectionComponent> sectionComponents = [];
	private readonly List<string> sectionNames = [];

	private VBoxContainer _debugMenuContent;
	private VBoxContainer _sectionContent;
	private ScrollContainer _contentScroll;
	private Button _debugMenuButton;

	private float _expandedAnchorLeft;
	private float _expandedAnchorRight;
	private float _expandedAnchorTop;
	private float _expandedAnchorBottom;

	private float _expandedOffsetLeft;
	private float _expandedOffsetRight;
	private float _expandedOffsetTop;
	private float _expandedOffsetBottom;

	private Vector2 _expandedMinimumSize;

	private void GetNodes()
	{
		_debugMenuContent = GetNode<VBoxContainer>("%DebugMenuContent");
		_sectionContent = GetNode<VBoxContainer>("%Content");
		_contentScroll = GetNode<ScrollContainer>("%ContentScroll");
		_debugMenuButton = GetNode<Button>("%DebugMenuButton");
	}

	public override void _EnterTree()
	{
		Instance = this;

		GetNodes();
		_debugMenuButton.Pressed += ToggleDebugMenu;
	}

	public override void _Ready()
	{
		_expandedAnchorLeft = AnchorLeft;
		_expandedAnchorRight = AnchorRight;
		_expandedAnchorTop = AnchorTop;
		_expandedAnchorBottom = AnchorBottom;

		_expandedOffsetLeft = OffsetLeft;
		_expandedOffsetRight = OffsetRight;
		_expandedOffsetTop = OffsetTop;
		_expandedOffsetBottom = OffsetBottom;

		_expandedMinimumSize = CustomMinimumSize;
	}

	public override void _ExitTree()
	{
		if (_debugMenuButton != null)
			_debugMenuButton.Pressed -= ToggleDebugMenu;

		if (Instance == this)
			Instance = null;
	}

	public override void _Notification(int what)
	{
		if (what != NotificationPredelete) return;

		Clear();
	}

	private void ToggleDebugMenu()
	{
		bool isOpen = !_contentScroll.Visible;

		_contentScroll.Visible = isOpen;
		_debugMenuButton.Text = isOpen ? "HIDE" : "SHOW";

		if (isOpen)
		{
			CustomMinimumSize = _expandedMinimumSize;

			AnchorLeft = _expandedAnchorLeft;
			AnchorRight = _expandedAnchorRight;
			AnchorTop = _expandedAnchorTop;
			AnchorBottom = _expandedAnchorBottom;

			OffsetLeft = _expandedOffsetLeft;
			OffsetRight = _expandedOffsetRight;
			OffsetTop = _expandedOffsetTop;
			OffsetBottom = _expandedOffsetBottom;

			return;
		}

		CustomMinimumSize = Vector2.Zero;
		ResetSize();

		Vector2 minimumSize = GetCombinedMinimumSize();

		AnchorLeft = 1.0f;
		AnchorRight = 1.0f;
		AnchorTop = 0.0f;
		AnchorBottom = 0.0f;

		OffsetLeft = -minimumSize.X;
		OffsetRight = 0.0f;
		OffsetTop = 0.0f;
		OffsetBottom = minimumSize.Y;
	}

	public void Clear()
	{
		foreach (SectionComponent sectionComponent in sectionComponents.Values)
		{
			if (IsInstanceValid(sectionComponent)) sectionComponent.QueueFree();
		}

		sectionComponents.Clear();
		sectionNames.Clear();
	}

	#region Sections

	public void AddSection(string name, int depth, bool isOpen = false)
	{
		AddSection(name, depth, isOpen, false);
	}

	public void AddSectionTemplate(string name, int depth, bool isOpen = false)
	{
		AddSection(name, depth, isOpen, true);
	}

	private void AddSection(string name, int depth, bool isOpen, bool isTemplate)
	{
		if (sectionNames.Contains(name)) throw new InvalidOperationException($"Debug menu already contains section '{name}'.");

		SectionComponent sectionComponent = SectionScene.Instantiate<SectionComponent>();

		sectionComponent.Initialize(name, depth, isOpen, isTemplate);
		_sectionContent.AddChild(sectionComponent);

		sectionNames.Add(name);
		sectionComponents[name] = sectionComponent;
	}

	#endregion

	#region Buttons

	public void AddButton(string name, string section, Func<bool> getState, Action action)
	{
		ButtonComponent buttonComponent = ButtonScene.Instantiate<ButtonComponent>();

		buttonComponent.Initialize(name, getState, action, ButtonComponent.ButtonType.Toggle);
		GetSection(section).AddContent(buttonComponent);
	}

	public void AddActionButton(string name, string section, Action action)
	{
		ButtonComponent buttonComponent = ButtonScene.Instantiate<ButtonComponent>();

		buttonComponent.Initialize(name, null, action, ButtonComponent.ButtonType.Action);
		GetSection(section).AddContent(buttonComponent);
	}

	public void AddButtonTemplate(string name, string section)
	{
		ButtonComponent buttonComponent = ButtonScene.Instantiate<ButtonComponent>();

		buttonComponent.Initialize(name, null, null, ButtonComponent.ButtonType.Toggle, true);
		GetSection(section).AddContent(buttonComponent);
	}

	public void AddActionButtonTemplate(string name, string section)
	{
		ButtonComponent buttonComponent = ButtonScene.Instantiate<ButtonComponent>();

		buttonComponent.Initialize(name, null, null, ButtonComponent.ButtonType.Action, true);
		GetSection(section).AddContent(buttonComponent);
	}

	#endregion

	#region Sliders

	public void AddSlider(string name, string section, Func<int> getState, Action<int> action, int min, int max, int step)
	{
		SliderComponent sliderComponent = SliderScene.Instantiate<SliderComponent>();

		sliderComponent.Initialize(name, getState, action, min, max, step);
		GetSection(section).AddContent(sliderComponent);
	}

	public void AddSlider(string name, string section, Func<uint> getState, Action<uint> action, uint min, uint max, uint step)
	{
		SliderComponent sliderComponent = SliderScene.Instantiate<SliderComponent>();

		sliderComponent.Initialize(name, getState, action, min, max, step);
		GetSection(section).AddContent(sliderComponent);
	}

	public void AddSlider(string name, string section, Func<float> getState, Action<float> action, float min, float max, float step)
	{
		SliderComponent sliderComponent = SliderScene.Instantiate<SliderComponent>();

		sliderComponent.Initialize(name, getState, action, min, max, step);
		GetSection(section).AddContent(sliderComponent);
	}

	public void AddSliderTemplate(string name, string section, int min, int max, int step)
	{
		SliderComponent sliderComponent = SliderScene.Instantiate<SliderComponent>();

		sliderComponent.Initialize(name, null, null, min, max, step, true);
		GetSection(section).AddContent(sliderComponent);
	}

	public void AddSliderTemplate(string name, string section, uint min, uint max, uint step)
	{
		SliderComponent sliderComponent = SliderScene.Instantiate<SliderComponent>();

		sliderComponent.Initialize(name, null, null, min, max, step, true);
		GetSection(section).AddContent(sliderComponent);
	}

	public void AddSliderTemplate(string name, string section, float min, float max, float step)
	{
		SliderComponent sliderComponent = SliderScene.Instantiate<SliderComponent>();

		sliderComponent.Initialize(name, null, null, min, max, step, true);
		GetSection(section).AddContent(sliderComponent);
	}

	#endregion

	#region Textures

	public void AddTexture(string name, string section, TextureRect textureRect)
	{
		TextureComponent textureComponent = TextureScene.Instantiate<TextureComponent>();

		textureComponent.Initialize(name, textureRect);
		GetSection(section).AddContent(textureComponent);
	}

	public void AddTextureTemplate(string name, string section)
	{
		TextureComponent textureComponent = TextureScene.Instantiate<TextureComponent>();

		textureComponent.Initialize(name, null, true);
		GetSection(section).AddContent(textureComponent);
	}

	#endregion

	private SectionComponent GetSection(string section)
	{
		if (!sectionComponents.TryGetValue(section, out SectionComponent sectionComponent)) throw new InvalidOperationException($"Debug section '{section}' does not exist.");

		return sectionComponent;
	}
}