using System;
using Godot;

namespace PlanetGame.Util.DebugUIComponents;

public partial class SliderComponent : PanelContainer, IDebugComponent
{
	public enum SliderType
	{
		Int,
		UInt,
		Float
	}

	public string TechnicalName { get; set; }
	public bool IsTemplate { get; set; }
    private const float SliderWidth = 150.0f;

	private Label _label;
	private HSlider _slider;
	private Label _valueLabel;

	private SliderType _sliderType;
	private Action<double> _action;

	public void GetNodes()
	{
		_label = GetNode<Label>("%Label");
		_slider = GetNode<HSlider>("%Slider");
		_valueLabel = GetNode<Label>("%ValueLabel");

        _slider.CustomMinimumSize = new Vector2(SliderWidth, 0);
	}

	public void Initialize(string name, Func<int> getState, Action<int> action, int min, int max, int step, bool isTemplate = false)
	{
		Initialize(name, getState != null ? () => getState() : null, action != null ? value => action((int)value) : null, min, max, step, SliderType.Int, isTemplate);
	}

	public void Initialize(string name, Func<uint> getState, Action<uint> action, uint min, uint max, uint step, bool isTemplate = false)
	{
		Initialize(name, getState != null ? () => getState() : null, action != null ? value => action((uint)value) : null, min, max, step, SliderType.UInt, isTemplate);
	}

	public void Initialize(string name, Func<float> getState, Action<float> action, float min, float max, float step, bool isTemplate = false)
	{
		Initialize(name, getState != null ? () => getState() : null, action != null ? value => action((float)value) : null, min, max, step, SliderType.Float, isTemplate);
	}

	private void Initialize(string name, Func<double> getState, Action<double> action, double min, double max, double step, SliderType sliderType, bool isTemplate)
	{
        GetNodes();

		TechnicalName = name.ToCamelCase();
		IsTemplate = isTemplate;
		_sliderType = sliderType;
		_action = action;

		Name = $"{TechnicalName}SliderComponent";
		_label.Text = name;

		double state = min;
		if (getState != null) state = getState();

		_slider.MinValue = min;
		_slider.MaxValue = max;
		_slider.Step = step;
		_slider.Value = state;

		_valueLabel.Text = FormatValue(state);
	}

	public override void _EnterTree()
	{
        GetNodes();
		if (!IsTemplate && _action != null && _slider != null) _slider.ValueChanged += OnValueChanged;
	}

	public override void _ExitTree()
	{
		if (!IsTemplate && _action != null && _slider != null) _slider.ValueChanged -= OnValueChanged;
	}

	private void OnValueChanged(double value)
	{
		if (_action != null) _action(value);
		_valueLabel.Text = FormatValue(value);
	}

	private string FormatValue(double value)
	{
		return _sliderType switch
		{
			SliderType.Int => ((int)value).ToString(),
			SliderType.UInt => ((uint)value).ToString(),
			SliderType.Float => ((float)value).ToString("0.##"),
			_ => value.ToString()
		};
	}
}