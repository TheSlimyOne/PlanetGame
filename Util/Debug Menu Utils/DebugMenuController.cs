using Godot;
using System;
using System.Collections.Generic;

namespace PlanetGame.Util.DebugUIComponents
{
	public partial class DebugMenuController : PanelContainer, IDebugContainer
	{
		public static DebugMenuController Instance { get; private set; }

		private static readonly PackedScene SectionScene = ScenePaths.GetPackedScene(ScenePaths.DEBUG_SECTION);
		private static readonly PackedScene ButtonScene = ScenePaths.GetPackedScene(ScenePaths.DEBUG_BUTTON);
		private static readonly PackedScene SetValueScene = ScenePaths.GetPackedScene(ScenePaths.DEBUG_SET_VALUE);
		private static readonly PackedScene SliderScene = ScenePaths.GetPackedScene(ScenePaths.DEBUG_SLIDER);
		private static readonly PackedScene TextureScene = ScenePaths.GetPackedScene(ScenePaths.DEBUG_TEXTURE);
		private static readonly PackedScene DistributionScene = ScenePaths.GetPackedScene(ScenePaths.DEBUG_DISTRIBUTION);
		private static readonly PackedScene LabelScene = ScenePaths.GetPackedScene(ScenePaths.DEBUG_LABEL);


		private static readonly Texture2D VisibleIcon = GD.Load<Texture2D>("res://Assets/Icons/GuiVisibilityVisible.svg");
		private static readonly Texture2D HiddenIcon = GD.Load<Texture2D>("res://Assets/Icons/GuiVisibilityHidden.svg");

		private const float DebugMenuButtonSize = 32.0f;

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
			SetDefaultComponents();

			_debugMenuButton.Text = "";
			_debugMenuButton.CustomMinimumSize = new Vector2(DebugMenuButtonSize, DebugMenuButtonSize);
			_debugMenuButton.ExpandIcon = true;
			_debugMenuButton.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
			_debugMenuButton.SizeFlagsVertical = SizeFlags.ShrinkCenter;
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

			ToggleDebugMenu();
		}

		public override void _ExitTree()
		{
			_debugMenuButton?.Pressed -= ToggleDebugMenu;

			Clear();

			if (Instance == this)
				Instance = null;
		}

		public override void _Notification(int what)
		{
			if (what != NotificationPredelete) return;

			Clear();
		}

		private void SetDefaultComponents()
		{
			AddLabel("FPS", null, () => $"{(int)Engine.GetFramesPerSecond()}");

			Instance.AddActionButton("Quit", null, () =>
			{
				// CleanUpGPU();
				GetTree().ChangeSceneToFile(ScenePaths.MAIN);
				GD.Print("Quiting");
			}, 1000);
		}

		private bool _isDebugging = false;
		private string _lastMemoryOutput = "";


		public override void _Process(double delta)
		{
			if (_isDebugging)
			{
				Control hoveredControl = GetViewport().GuiGetHoveredControl();

				if (hoveredControl != null)
					GD.Print(hoveredControl.GetPath());
			}

			RenderingDevice rd = RenderingServer.GetRenderingDevice();

			ulong buffers = rd.GetMemoryUsage(RenderingDevice.MemoryType.Buffers);
			ulong textures = rd.GetMemoryUsage(RenderingDevice.MemoryType.Textures);
			ulong total = rd.GetMemoryUsage(RenderingDevice.MemoryType.Total);

			string s = "";

			s += $"Buffers: {buffers / 1024f / 1024f:F6} MB ";
			s += $"Textures: {textures / 1024f / 1024f:F6} MB ";
			// s += $"Total: {total / 1024f / 1024f:F6} MB ";

			if (s != _lastMemoryOutput)
			{
				_lastMemoryOutput = s;
				// GD.Print(s);
			}
		}


		private void ToggleDebugMenu()
		{
			SetDebugMenuOpen(!_contentScroll.Visible);
		}

		private void SetDebugMenuOpen(bool isOpen)
		{
			_contentScroll.Visible = isOpen;
			_debugMenuButton.Icon = isOpen ? VisibleIcon : HiddenIcon;

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
				if (IsInstanceValid(sectionComponent))
					sectionComponent.QueueFree();
			}

			sectionComponents.Clear();
			sectionNames.Clear();

			foreach (Node child in _sectionContent.GetChildren())
			{
				child.QueueFree();
			}

			SetDebugMenuOpen(false);
			SetDefaultComponents();
		}

		public void AddContent(Control control, int order = 0)
		{
			control.SetMeta("DebugOrder", order);

			_sectionContent.AddChild(control);
			SortContent(_sectionContent);
		}

		private static void SortContent(Container container)
		{
			List<Control> controls = [];

			foreach (Node child in container.GetChildren())
			{
				if (child is Control control)
					controls.Add(control);
			}

			controls.Sort((left, right) =>
			{
				int leftOrder = GetControlOrder(left);
				int rightOrder = GetControlOrder(right);

				return leftOrder.CompareTo(rightOrder);
			});

			for (int index = 0; index < controls.Count; index++)
				container.MoveChild(controls[index], index);
		}

		private static int GetControlOrder(Control control)
		{
			if (!control.HasMeta("DebugOrder"))
				return 0;

			return control.GetMeta("DebugOrder").AsInt32();
		}

		private IDebugContainer GetContainer(string section)
		{
			if (section == null)
				return this;

			if (!sectionComponents.TryGetValue(section, out SectionComponent sectionComponent))
				throw new InvalidOperationException($"Debug section '{section}' does not exist.");

			return sectionComponent;
		}

		#region Sections

		public void AddSection(string name, int depth, bool isOpen, string parentSection, int order = 0)
		{
			AddSection(name, depth, isOpen, parentSection, order, false);
		}

		public void AddSectionTemplate(string name, int depth, bool isOpen, string parentSection, int order = 0)
		{
			AddSection(name, depth, isOpen, parentSection, order, true);
		}

		private void AddSection(string name, int depth, bool isOpen, string parentSection, int order, bool isTemplate)
		{
			if (sectionNames.Contains(name))
				throw new InvalidOperationException($"Debug menu already contains section '{name}'.");

			SectionComponent sectionComponent = SectionScene.Instantiate<SectionComponent>();

			sectionComponent.Initialize(name, depth, isOpen, isTemplate);
			GetContainer(parentSection).AddContent(sectionComponent, order);

			sectionNames.Add(name);
			sectionComponents[name] = sectionComponent;
		}

		#endregion

		#region Buttons

		public void AddButton(string name, string section, Func<bool> getState, Action action, int order = 0)
		{
			ButtonComponent buttonComponent = ButtonScene.Instantiate<ButtonComponent>();

			buttonComponent.Initialize(name, getState, action, ButtonComponent.ButtonType.Toggle);
			GetContainer(section).AddContent(buttonComponent, order);
		}

		public void AddActionButton(string name, string section, Action action, int order = 0)
		{
			ButtonComponent buttonComponent = ButtonScene.Instantiate<ButtonComponent>();

			buttonComponent.Initialize(name, null, action, ButtonComponent.ButtonType.Action);
			GetContainer(section).AddContent(buttonComponent, order);
		}

		public void AddButtonTemplate(string name, string section, int order = 0)
		{
			ButtonComponent buttonComponent = ButtonScene.Instantiate<ButtonComponent>();

			buttonComponent.Initialize(name, null, null, ButtonComponent.ButtonType.Toggle, true);
			GetContainer(section).AddContent(buttonComponent, order);
		}

		public void AddActionButtonTemplate(string name, string section, int order = 0)
		{
			ButtonComponent buttonComponent = ButtonScene.Instantiate<ButtonComponent>();

			buttonComponent.Initialize(name, null, null, ButtonComponent.ButtonType.Action, true);
			GetContainer(section).AddContent(buttonComponent, order);
		}

		#endregion

		#region Sliders

		public void AddSlider(string name, string section, Func<int> getState, Action<int> action, int min, int max, int step, Func<int, int> subtractFunction = null, Func<int, int> addFunction = null, int order = 0)
		{
			SliderComponent sliderComponent = SliderScene.Instantiate<SliderComponent>();

			sliderComponent.Initialize(name, getState, action, min, max, step, subtractFunction, addFunction);
			GetContainer(section).AddContent(sliderComponent, order);
		}

		public void AddSlider(string name, string section, SliderComponent.SliderBinding<int>[] bindings, int order = 0)
		{
			SliderComponent sliderComponent = SliderScene.Instantiate<SliderComponent>();

			sliderComponent.Initialize(name, bindings);
			GetContainer(section).AddContent(sliderComponent, order);
		}

		public void AddSlider(string name, string section, Func<uint> getState, Action<uint> action, uint min, uint max, uint step, Func<uint, uint> subtractFunction = null, Func<uint, uint> addFunction = null, int order = 0)
		{
			SliderComponent sliderComponent = SliderScene.Instantiate<SliderComponent>();

			sliderComponent.Initialize(name, getState, action, min, max, step, subtractFunction, addFunction);
			GetContainer(section).AddContent(sliderComponent, order);
		}

		public void AddSlider(string name, string section, SliderComponent.SliderBinding<uint>[] bindings, int order = 0)
		{
			SliderComponent sliderComponent = SliderScene.Instantiate<SliderComponent>();

			sliderComponent.Initialize(name, bindings);
			GetContainer(section).AddContent(sliderComponent, order);
		}

		public void AddSlider(string name, string section, Func<float> getState, Action<float> action, float min, float max, float step, Func<float, float> subtractFunction = null, Func<float, float> addFunction = null, int order = 0)
		{
			SliderComponent sliderComponent = SliderScene.Instantiate<SliderComponent>();

			sliderComponent.Initialize(name, getState, action, min, max, step, subtractFunction, addFunction);
			GetContainer(section).AddContent(sliderComponent, order);
		}

		public void AddSlider(string name, string section, SliderComponent.SliderBinding<float>[] bindings, int order = 0)
		{
			SliderComponent sliderComponent = SliderScene.Instantiate<SliderComponent>();

			sliderComponent.Initialize(name, bindings);
			GetContainer(section).AddContent(sliderComponent, order);
		}

		public void AddSliderTemplate(string name, string section, int min, int max, int step, int order = 0)
		{
			SliderComponent sliderComponent = SliderScene.Instantiate<SliderComponent>();

			sliderComponent.InitializeTemplate(name, min, max, step);
			GetContainer(section).AddContent(sliderComponent, order);
		}

		public void AddSliderTemplate(string name, string section, uint min, uint max, uint step, int order = 0)
		{
			SliderComponent sliderComponent = SliderScene.Instantiate<SliderComponent>();

			sliderComponent.InitializeTemplate(name, min, max, step);
			GetContainer(section).AddContent(sliderComponent, order);
		}

		public void AddSliderTemplate(string name, string section, float min, float max, float step, int order = 0)
		{
			SliderComponent sliderComponent = SliderScene.Instantiate<SliderComponent>();

			sliderComponent.InitializeTemplate(name, min, max, step);
			GetContainer(section).AddContent(sliderComponent, order);
		}

		#endregion

		#region Set Values

		public void AddSetValue(string name, string section, Func<int> getState, Action<int> action, int step = 1, Func<int, int> subtractFunction = null, Func<int, int> addFunction = null, int order = 0)
		{
			SetValueComponent setValueComponent = SetValueScene.Instantiate<SetValueComponent>();
			setValueComponent.Initialize(name, getState, action, step, subtractFunction, addFunction);
			GetContainer(section).AddContent(setValueComponent, order);
		}

		public void AddSetValue(string name, string section, SetValueComponent.SetValueBinding<int>[] bindings, int order = 0)
		{
			SetValueComponent setValueComponent = SetValueScene.Instantiate<SetValueComponent>();
			setValueComponent.Initialize(name, bindings);
			GetContainer(section).AddContent(setValueComponent, order);
		}

		public void AddSetValue(string name, string section, Func<uint> getState, Action<uint> action, uint step = 1, Func<uint, uint> subtractFunction = null, Func<uint, uint> addFunction = null, int order = 0)
		{
			SetValueComponent setValueComponent = SetValueScene.Instantiate<SetValueComponent>();
			setValueComponent.Initialize(name, getState, action, step, subtractFunction, addFunction);
			GetContainer(section).AddContent(setValueComponent, order);
		}

		public void AddSetValue(string name, string section, SetValueComponent.SetValueBinding<uint>[] bindings, int order = 0)
		{
			SetValueComponent setValueComponent = SetValueScene.Instantiate<SetValueComponent>();
			setValueComponent.Initialize(name, bindings);
			GetContainer(section).AddContent(setValueComponent, order);
		}

		public void AddSetValue(string name, string section, Func<float> getState, Action<float> action, float step = 1.0f, Func<float, float> subtractFunction = null, Func<float, float> addFunction = null, int order = 0)
		{
			SetValueComponent setValueComponent = SetValueScene.Instantiate<SetValueComponent>();
			setValueComponent.Initialize(name, getState, action, step, subtractFunction, addFunction);
			GetContainer(section).AddContent(setValueComponent, order);
		}

		public void AddSetValue(string name, string section, SetValueComponent.SetValueBinding<float>[] bindings, int order = 0)
		{
			SetValueComponent setValueComponent = SetValueScene.Instantiate<SetValueComponent>();
			setValueComponent.Initialize(name, bindings);
			GetContainer(section).AddContent(setValueComponent, order);
		}

		public void AddSetValueTemplate(string name, string section, int step = 1, int order = 0)
		{
			SetValueComponent setValueComponent = SetValueScene.Instantiate<SetValueComponent>();
			setValueComponent.InitializeTemplate(name, step);
			GetContainer(section).AddContent(setValueComponent, order);
		}

		public void AddSetValueTemplate(string name, string section, uint step = 1, int order = 0)
		{
			SetValueComponent setValueComponent = SetValueScene.Instantiate<SetValueComponent>();
			setValueComponent.InitializeTemplate(name, step);
			GetContainer(section).AddContent(setValueComponent, order);
		}

		public void AddSetValueTemplate(string name, string section, float step = 1.0f, int order = 0)
		{
			SetValueComponent setValueComponent = SetValueScene.Instantiate<SetValueComponent>();
			setValueComponent.InitializeTemplate(name, step);
			GetContainer(section).AddContent(setValueComponent, order);
		}

		#endregion

		#region Textures

		public void AddTexture(string name, string section, TextureRect textureRect, bool isVisible, int order = 0)
		{
			TextureComponent textureComponent = TextureScene.Instantiate<TextureComponent>();

			textureComponent.Initialize(name, textureRect, isVisible);
			GetContainer(section).AddContent(textureComponent, order);
		}

		public void AddTexture(string name, string section, Func<Rid> getRid, bool isVisible, int order = 0)
		{
			TextureComponent textureComponent = TextureScene.Instantiate<TextureComponent>();

			textureComponent.Initialize(name, getRid, isVisible);
			GetContainer(section).AddContent(textureComponent, order);
		}

		public void AddTextureTemplate(string name, string section, int order = 0)
		{
			TextureComponent textureComponent = TextureScene.Instantiate<TextureComponent>();

			textureComponent.Initialize(name, null, true);
			GetContainer(section).AddContent(textureComponent, order);
		}

		#endregion

		#region Distributions

		public void AddDistribution(string name, string section, DistributionComponent.DistributionBinding<int>[] bindings)
		{
			DistributionComponent distributionComponent = DistributionScene.Instantiate<DistributionComponent>();

			distributionComponent.Initialize(name, bindings);
			GetContainer(section).AddContent(distributionComponent);
		}

		public void AddDistribution(string name, string section, DistributionComponent.DistributionBinding<uint>[] bindings)
		{
			DistributionComponent distributionComponent = DistributionScene.Instantiate<DistributionComponent>();

			distributionComponent.Initialize(name, bindings);
			GetContainer(section).AddContent(distributionComponent);
		}

		public void AddDistribution(string name, string section, DistributionComponent.DistributionBinding<float>[] bindings)
		{
			DistributionComponent distributionComponent = DistributionScene.Instantiate<DistributionComponent>();

			distributionComponent.Initialize(name, bindings);
			GetContainer(section).AddContent(distributionComponent);
		}

		public void AddDistribution(string name, string section, string valueName, Func<int> getState)
		{
			DistributionComponent distributionComponent = DistributionScene.Instantiate<DistributionComponent>();

			distributionComponent.Initialize(name, valueName, getState);
			GetContainer(section).AddContent(distributionComponent);
		}

		public void AddDistribution(string name, string section, string valueName, Func<uint> getState)
		{
			DistributionComponent distributionComponent = DistributionScene.Instantiate<DistributionComponent>();

			distributionComponent.Initialize(name, valueName, getState);
			GetContainer(section).AddContent(distributionComponent);
		}

		public void AddDistribution(string name, string section, string valueName, Func<float> getState)
		{
			DistributionComponent distributionComponent = DistributionScene.Instantiate<DistributionComponent>();

			distributionComponent.Initialize(name, valueName, getState);
			GetContainer(section).AddContent(distributionComponent);
		}

		#endregion

		#region Label
		public void AddLabel(string name, string section, Func<string> getValue)
		{
			LabelComponent labelComponent = LabelScene.Instantiate<LabelComponent>();

			labelComponent.Initialize(name, getValue);
			GetContainer(section).AddContent(labelComponent);
		}
		#endregion
	}

}