using System;
using Godot;

namespace PlanetGame.Util.DebugUIComponents
{
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

			MouseFilter = MouseFilterEnum.Stop;
			MouseDefaultCursorShape = CursorShape.PointingHand;
			_button.MouseFilter = MouseFilterEnum.Ignore;
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

		public override void _GuiInput(InputEvent @event)
		{
			if (@event is not InputEventMouseButton mouseEvent)
				return;

			if (mouseEvent.ButtonIndex != MouseButton.Left || !mouseEvent.Pressed)
				return;

			Activate();
			AcceptEvent();
		}

		private void Activate()
		{
			if (IsTemplate)
			{
				if (_buttonType != ButtonType.Toggle)
					return;

				_button.ButtonPressed = !_button.ButtonPressed;
				_button.Text = _button.ButtonPressed ? "ON" : "OFF";
				return;
			}

			_action?.Invoke();

			if (_buttonType != ButtonType.Toggle || _getState == null)
				return;

			bool state = _getState();

			_button.ButtonPressed = state;
			_button.Text = state ? "ON" : "OFF";
		}

		public override void _EnterTree()
		{
			GetNodes();
		}

		private void GetNodes()
		{
			_label ??= GetNode<Label>("%Label");
			_button ??= GetNode<Button>("%Button");
		}
	}
}