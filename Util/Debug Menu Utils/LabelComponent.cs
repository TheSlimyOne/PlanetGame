using System;
using Godot;
using PlanetGame.Util.DebugUIComponents;

public partial class LabelComponent : PanelContainer, IDebugComponent
{
	public string TechnicalName { get; set; }
	public bool IsTemplate { get; set; } = true;

	private Label _label;
	private Label _value;

	private Func<string> _getValue;

	public override void _Ready()
	{
		_label = GetNode<Label>("%Label");
		_value = GetNode<Label>("%Value");
	}

	public void Initialize(string name, Func<string> getValue, bool isTemplate = false)
	{
		TechnicalName = name.ToCamelCase();
		IsTemplate = isTemplate;

		Name = $"{TechnicalName}LabelComponent";

		_getValue = getValue;

		_label ??= GetNode<Label>("%Label");
		_value ??= GetNode<Label>("%Value");

		_label.Text = name;
		_value.Text = getValue?.Invoke() ?? "Value";
	}

	public override void _Process(double delta)
	{
		if (_getValue == null)
			return;

		_value.Text = _getValue();
	}
}