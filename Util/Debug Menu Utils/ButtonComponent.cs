using System;
using Godot;

namespace PlanetGame.Util.DebugUIComponents;

public partial class ButtonComponent : PanelContainer, IDebugComponent
{
	public enum ButtonType
	{
		Toggle,
		Action
	}

	public string TechnicalName { get; set; }
	public bool IsTemplate { get; set; } = true;

	private Label _label;
	private Button _button;

	private Func<bool> _getState;
	private Action _action;
	private ButtonType _buttonType;

	public void Initialize(string name, Func<bool> getState, Action action, ButtonType buttonType, bool isTemplate = false)
	{
		GetNodes();

		TechnicalName = name.ToCamelCase();
		IsTemplate = isTemplate;
		_buttonType = buttonType;

		Name = $"{TechnicalName}ButtonComponent";
		_label.Text = name;

		_getState = getState;
		_action = action;

		_button.ToggleMode = _buttonType == ButtonType.Toggle;

		if (_buttonType == ButtonType.Toggle)
		{
			bool state = false;
			if (_getState != null) state = _getState();

			_button.ButtonPressed = state;
			_button.Text = state ? "ON" : "OFF";
		}
		else
		{
			_button.ButtonPressed = false;
			_button.Text = "EXECUTE";
		}
	}

	public override void _EnterTree()
	{
		GetNodes();

		if (IsTemplate)
		{
			_button.Pressed += OnTemplatePressed;
			return;
		}

		if (_action != null) _button.Pressed += OnPressed;
	}

	public override void _ExitTree()
	{
		if (_button == null) return;

		if (IsTemplate)
		{
			_button.Pressed -= OnTemplatePressed;
			return;
		}

		if (_action != null) _button.Pressed -= OnPressed;
	}

	private void GetNodes()
	{
		_label ??= GetNode<Label>("%Label");
		_button ??= GetNode<Button>("%Button");
	}

	private void OnPressed()
	{
		if (_action != null) _action();
		if (_buttonType != ButtonType.Toggle || _getState == null) return;

		bool state = _getState();

		_button.ButtonPressed = state;
		_button.Text = state ? "ON" : "OFF";
	}

	private void OnTemplatePressed()
	{
		if (_buttonType != ButtonType.Toggle) return;

		_button.Text = _button.ButtonPressed ? "ON" : "OFF";
	}
}