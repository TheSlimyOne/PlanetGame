using System;
using System.Collections.Generic;
using System.Globalization;
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

	public class SliderBinding<T>
	{
		public Func<T> GetState { get; }
		public Action<T> Action { get; }

		public T Min { get; }
		public T Max { get; }
		public T Step { get; }

		public Func<T, T> SubtractFunction { get; }
		public Func<T, T> AddFunction { get; }

		public SliderBinding(Func<T> getState, Action<T> action, T min, T max, T step, Func<T, T> subtractFunction = null, Func<T, T> addFunction = null)
		{
			GetState = getState;
			Action = action;

			Min = min;
			Max = max;
			Step = step;

			SubtractFunction = subtractFunction;
			AddFunction = addFunction;
		}
	}

	private class SliderEntry
	{
		public HBoxContainer Container;
		public HSlider Slider;
		public Button SubtractButton;
		public Button AddButton;
		public LineEdit ValueField;

		public Action<double> Action;
		public Func<double, double> SubtractFunction;
		public Func<double, double> AddFunction;
	}

	private class InternalBinding
	{
		public Func<double> GetState;
		public Action<double> Action;

		public double Min;
		public double Max;
		public double Step;

		public Func<double, double> SubtractFunction;
		public Func<double, double> AddFunction;
	}

	public string TechnicalName { get; set; }
	public bool IsTemplate { get; set; }

	private const float SliderWidth = 150.0f;

	private Label _label;
	private VBoxContainer _sliderRows;
	private HBoxContainer _sliderRowTemplate;

	private SliderType _sliderType;
	private readonly List<SliderEntry> _sliderEntries = new();

	public void GetNodes()
	{
		_label = GetNode<Label>("%Label");
		_sliderRows = GetNode<VBoxContainer>("%SliderRows");
		_sliderRowTemplate = _sliderRows.GetNode<HBoxContainer>("SliderRowTemplate");
	}

	public void Initialize(string name, SliderBinding<int>[] bindings, bool isTemplate = false)
	{
		Initialize(name, CreateBindings(bindings), SliderType.Int, isTemplate);
	}

	public void Initialize(string name, SliderBinding<uint>[] bindings, bool isTemplate = false)
	{
		Initialize(name, CreateBindings(bindings), SliderType.UInt, isTemplate);
	}

	public void Initialize(string name, SliderBinding<float>[] bindings, bool isTemplate = false)
	{
		Initialize(name, CreateBindings(bindings), SliderType.Float, isTemplate);
	}

	public void Initialize(string name, Func<int> getState, Action<int> action, int min, int max, int step, Func<int, int> subtractFunction = null, Func<int, int> addFunction = null, bool isTemplate = false)
	{
		Initialize(
			name,
			[
				new SliderBinding<int>(getState, action, min, max, step, subtractFunction, addFunction)
			],
			isTemplate
		);
	}

	public void Initialize(string name, Func<uint> getState, Action<uint> action, uint min, uint max, uint step, Func<uint, uint> subtractFunction = null, Func<uint, uint> addFunction = null, bool isTemplate = false)
	{
		Initialize(
			name,
			[
				new SliderBinding<uint>(getState, action, min, max, step, subtractFunction, addFunction)
			],
			isTemplate
		);
	}

	public void Initialize(string name, Func<float> getState, Action<float> action, float min, float max, float step, Func<float, float> subtractFunction = null, Func<float, float> addFunction = null, bool isTemplate = false)
	{
		Initialize(
			name,
			[
				new SliderBinding<float>(getState, action, min, max, step, subtractFunction, addFunction)
			],
			isTemplate
		);
	}

	public void InitializeTemplate(string name, int min, int max, int step)
	{
		InitializeTemplate(name, min, max, step, SliderType.Int);
	}

	public void InitializeTemplate(string name, uint min, uint max, uint step)
	{
		InitializeTemplate(name, min, max, step, SliderType.UInt);
	}

	public void InitializeTemplate(string name, float min, float max, float step)
	{
		InitializeTemplate(name, min, max, step, SliderType.Float);
	}

	private void Initialize(string name, InternalBinding[] bindings, SliderType sliderType, bool isTemplate)
	{
		GetNodes();

		TechnicalName = name.ToCamelCase();
		IsTemplate = isTemplate;
		_sliderType = sliderType;

		Name = $"{TechnicalName}SliderComponent";
		_label.Text = name;

		CreateSliderRows(bindings);
	}

	private void InitializeTemplate(string name, double min, double max, double step, SliderType sliderType)
	{
		GetNodes();

		TechnicalName = name.ToCamelCase();
		IsTemplate = true;
		_sliderType = sliderType;

		Name = $"{TechnicalName}SliderComponent";
		_label.Text = name;

		ConfigureTemplateRow(min, max, step);
	}

	private void CreateSliderRows(InternalBinding[] bindings)
	{
		_sliderEntries.Clear();

		if (bindings == null || bindings.Length == 0)
			return;

		for (int index = 0; index < bindings.Length; index++)
		{
			HBoxContainer row;

			if (index == 0)
			{
				row = _sliderRowTemplate;
			}
			else
			{
				row = (HBoxContainer)_sliderRowTemplate.Duplicate();
				_sliderRows.AddChild(row);
			}

			row.Name = $"SliderRow{index}";

			SliderEntry entry = CreateSliderEntry(row, bindings[index]);
			_sliderEntries.Add(entry);
		}
	}

	private SliderEntry CreateSliderEntry(HBoxContainer row, InternalBinding binding)
	{
		HSlider slider = row.GetNode<HSlider>("Slider");
		Button subtractButton = row.GetNode<Button>("SubtractButton");
		Button addButton = row.GetNode<Button>("AddButton");
		LineEdit valueField = row.GetNode<LineEdit>("ValueLabel");

		slider.CustomMinimumSize = new Vector2(SliderWidth, 0);
		slider.MinValue = binding.Min;
		slider.MaxValue = binding.Max;
		slider.Step = binding.Step;

		double state = binding.Min;

		if (binding.GetState != null)
			state = binding.GetState();

		slider.Value = Mathf.Clamp(state, binding.Min, binding.Max);
		valueField.Text = FormatValue(slider.Value);

		return new SliderEntry
		{
			Container = row,
			Slider = slider,
			SubtractButton = subtractButton,
			AddButton = addButton,
			ValueField = valueField,
			Action = binding.Action,
			SubtractFunction = binding.SubtractFunction,
			AddFunction = binding.AddFunction
		};
	}

	private void ConfigureTemplateRow(double min, double max, double step)
	{
		_sliderRowTemplate.Name = "SliderRow0";

		HSlider slider = _sliderRowTemplate.GetNode<HSlider>("Slider");
		LineEdit valueField = _sliderRowTemplate.GetNode<LineEdit>("ValueLabel");

		slider.CustomMinimumSize = new Vector2(SliderWidth, 0);
		slider.MinValue = min;
		slider.MaxValue = max;
		slider.Step = step;
		slider.Value = min;

		valueField.Text = FormatValue(min);
	}

	public override void _EnterTree()
	{
		if (IsTemplate) return;

		foreach (SliderEntry entry in _sliderEntries)
		{
			SliderEntry capturedEntry = entry;

			capturedEntry.Slider.ValueChanged += value => OnValueChanged(capturedEntry, value);
			capturedEntry.SubtractButton.Pressed += () => OnSubtractPressed(capturedEntry);
			capturedEntry.AddButton.Pressed += () => OnAddPressed(capturedEntry);
			capturedEntry.ValueField.TextSubmitted += _ => OnValueSubmitted(capturedEntry);
			capturedEntry.ValueField.FocusExited += () => OnValueSubmitted(capturedEntry);
		}
	}

	private void OnSubtractPressed(SliderEntry entry)
	{
		if (entry.SubtractFunction == null) return;

		SetValue(entry, entry.SubtractFunction(entry.Slider.Value));
	}

	private void OnAddPressed(SliderEntry entry)
	{
		if (entry.AddFunction == null) return;

		SetValue(entry, entry.AddFunction(entry.Slider.Value));
	}

	private void OnValueSubmitted(SliderEntry entry)
	{
		if (!TryParseValue(entry.ValueField.Text, out double value))
		{
			entry.ValueField.Text = FormatValue(entry.Slider.Value);
			return;
		}

		SetValue(entry, value);
		entry.ValueField.Text = FormatValue(entry.Slider.Value);
	}

	private void SetValue(SliderEntry entry, double value)
	{
		entry.Slider.Value = Mathf.Clamp(value, entry.Slider.MinValue, entry.Slider.MaxValue);
	}

	private void OnValueChanged(SliderEntry entry, double value)
	{
		entry.Action?.Invoke(value);
		entry.ValueField.Text = FormatValue(value);
	}

	private bool TryParseValue(string text, out double value)
	{
		if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
			return false;

		switch (_sliderType)
		{
			case SliderType.Int:
				value = (int)value;
				break;

			case SliderType.UInt:
				value = Math.Max((uint)Math.Max(value, 0), 0);
				break;

			case SliderType.Float:
				value = (float)value;
				break;
		}

		return true;
	}

	private string FormatValue(double value)
	{
		return _sliderType switch
		{
			SliderType.Int => ((int)value).ToString(),
			SliderType.UInt => ((uint)value).ToString(),
			SliderType.Float => ((float)value).ToString("0.##", CultureInfo.InvariantCulture),
			_ => value.ToString(CultureInfo.InvariantCulture)
		};
	}

	private static InternalBinding[] CreateBindings(SliderBinding<int>[] bindings)
	{
		if (bindings == null) return null;

		InternalBinding[] result = new InternalBinding[bindings.Length];

		for (int index = 0; index < bindings.Length; index++)
		{
			SliderBinding<int> binding = bindings[index];

			result[index] = new InternalBinding
			{
				GetState = binding.GetState != null ? () => binding.GetState() : null,
				Action = binding.Action != null ? value => binding.Action((int)value) : null,
				Min = binding.Min,
				Max = binding.Max,
				Step = binding.Step,
				SubtractFunction = binding.SubtractFunction != null
					? value => binding.SubtractFunction((int)value)
					: value => value - binding.Step,
				AddFunction = binding.AddFunction != null
					? value => binding.AddFunction((int)value)
					: value => value + binding.Step
			};
		}

		return result;
	}

	private static InternalBinding[] CreateBindings(SliderBinding<uint>[] bindings)
	{
		if (bindings == null) return null;

		InternalBinding[] result = new InternalBinding[bindings.Length];

		for (int index = 0; index < bindings.Length; index++)
		{
			SliderBinding<uint> binding = bindings[index];

			result[index] = new InternalBinding
			{
				GetState = binding.GetState != null ? () => binding.GetState() : null,
				Action = binding.Action != null ? value => binding.Action((uint)value) : null,
				Min = binding.Min,
				Max = binding.Max,
				Step = binding.Step,
				SubtractFunction = binding.SubtractFunction != null
					? value => binding.SubtractFunction((uint)value)
					: value => Math.Max(value - binding.Step, 0),
				AddFunction = binding.AddFunction != null
					? value => binding.AddFunction((uint)value)
					: value => value + binding.Step
			};
		}

		return result;
	}

	private static InternalBinding[] CreateBindings(SliderBinding<float>[] bindings)
	{
		if (bindings == null) return null;

		InternalBinding[] result = new InternalBinding[bindings.Length];

		for (int index = 0; index < bindings.Length; index++)
		{
			SliderBinding<float> binding = bindings[index];

			result[index] = new InternalBinding
			{
				GetState = binding.GetState != null ? () => binding.GetState() : null,
				Action = binding.Action != null ? value => binding.Action((float)value) : null,
				Min = binding.Min,
				Max = binding.Max,
				Step = binding.Step,
				SubtractFunction = binding.SubtractFunction != null
					? value => binding.SubtractFunction((float)value)
					: value => value - binding.Step,
				AddFunction = binding.AddFunction != null
					? value => binding.AddFunction((float)value)
					: value => value + binding.Step
			};
		}

		return result;
	}
}