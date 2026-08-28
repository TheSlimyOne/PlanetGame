using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;

namespace PlanetGame.Util.DebugUIComponents;

public partial class DistributionComponent : PanelContainer, IDebugComponent
{
	public enum DistributionType
	{
		Int,
		UInt,
		Float
	}

	public class DistributionBinding<T>
	{
		public string Name { get; }
		public Func<T> GetState { get; }

		public DistributionBinding(string name, Func<T> getState)
		{
			Name = name;
			GetState = getState;
		}
	}

	private class DistributionEntry
	{
		public HBoxContainer Container;
		public Label ValueName;
		public ProgressBar Slider;
		public Label ValueLabel;
		public Label ValueLabelPercentage;

		public Func<double> GetState;
	}

	private class InternalBinding
	{
		public string Name;
		public Func<double> GetState;
	}

	public string TechnicalName { get; set; }
	public bool IsTemplate { get; set; }

	private const float ValueNameWidth = 70.0f;
	private const float SliderWidth = 150.0f;
	private const float ValueLabelWidth = 60.0f;
	private const float PercentageLabelWidth = 55.0f;

	private const double UpdateInterval = 0.1;

	private Label _label;
	private VBoxContainer _distributionRows;
	private HBoxContainer _distributionRowTemplate;

	private DistributionType _distributionType;
	private readonly List<DistributionEntry> _distributionEntries = new();

	private double _updateTimer;

	public void GetNodes()
	{
		_label = GetNode<Label>("%Label");
		_distributionRows = GetNode<VBoxContainer>("%DistributionRows");
		_distributionRowTemplate = _distributionRows.GetNode<HBoxContainer>("DistributionRowTemplate");
	}

	public void Initialize(string name, DistributionBinding<int>[] bindings, bool isTemplate = false)
	{
		Initialize(name, CreateBindings(bindings), DistributionType.Int, isTemplate);
	}

	public void Initialize(string name, DistributionBinding<uint>[] bindings, bool isTemplate = false)
	{
		Initialize(name, CreateBindings(bindings), DistributionType.UInt, isTemplate);
	}

	public void Initialize(string name, DistributionBinding<float>[] bindings, bool isTemplate = false)
	{
		Initialize(name, CreateBindings(bindings), DistributionType.Float, isTemplate);
	}

	public void Initialize(string name, string valueName, Func<int> getState, bool isTemplate = false)
	{
		Initialize(
			name,
			[
				new DistributionBinding<int>(valueName, getState)
			],
			isTemplate
		);
	}

	public void Initialize(string name, string valueName, Func<uint> getState, bool isTemplate = false)
	{
		Initialize(
			name,
			[
				new DistributionBinding<uint>(valueName, getState)
			],
			isTemplate
		);
	}

	public void Initialize(string name, string valueName, Func<float> getState, bool isTemplate = false)
	{
		Initialize(
			name,
			[
				new DistributionBinding<float>(valueName, getState)
			],
			isTemplate
		);
	}

	public void InitializeTemplate(string name, int rowCount = 3)
	{
		InitializeTemplate(name, rowCount, DistributionType.Int);
	}

	public void InitializeTemplate(string name, uint rowCount)
	{
		InitializeTemplate(name, (int)rowCount, DistributionType.UInt);
	}

	public void InitializeTemplate(string name, float rowCount)
	{
		InitializeTemplate(name, (int)rowCount, DistributionType.Float);
	}

	private void Initialize(string name, InternalBinding[] bindings, DistributionType distributionType, bool isTemplate)
	{
		GetNodes();

		TechnicalName = name.ToCamelCase();
		IsTemplate = isTemplate;
		_distributionType = distributionType;

		Name = $"{TechnicalName}DistributionComponent";
		_label.Text = name;

		CreateDistributionRows(bindings);
		UpdateDistribution();
	}

	private void InitializeTemplate(string name, int rowCount, DistributionType distributionType)
	{
		GetNodes();

		TechnicalName = name.ToCamelCase();
		IsTemplate = true;
		_distributionType = distributionType;

		Name = $"{TechnicalName}DistributionComponent";
		_label.Text = name;

		CreateTemplateRows(rowCount);
	}

	private void CreateDistributionRows(InternalBinding[] bindings)
	{
		_distributionEntries.Clear();

		if (bindings == null || bindings.Length == 0)
			return;

		for (int index = 0; index < bindings.Length; index++)
		{
			HBoxContainer row;

			if (index == 0)
			{
				row = _distributionRowTemplate;
			}
			else
			{
				row = (HBoxContainer)_distributionRowTemplate.Duplicate();
				_distributionRows.AddChild(row);
			}

			row.Name = $"DistributionRow{index}";

			DistributionEntry entry = CreateDistributionEntry(row, bindings[index]);
			_distributionEntries.Add(entry);
		}
	}

	private DistributionEntry CreateDistributionEntry(HBoxContainer row, InternalBinding binding)
	{
		Label valueName = row.GetNode<Label>("ValueName");
		ProgressBar slider = row.GetNode<ProgressBar>("Slider");
		Label valueLabel = row.GetNode<Label>("ValueLabel");
		Label valueLabelPercentage = row.GetNode<Label>("ValueLabelPercentage");

		ConfigureRow(valueName, slider, valueLabel, valueLabelPercentage);

		slider.MinValue = 0;
		slider.MaxValue = 1;
		slider.Value = 0;

		valueName.Text = binding.Name;
		valueLabel.Text = FormatValue(0);
		valueLabelPercentage.Text = "0%";

		return new DistributionEntry
		{
			Container = row,
			ValueName = valueName,
			Slider = slider,
			ValueLabel = valueLabel,
			ValueLabelPercentage = valueLabelPercentage,
			GetState = binding.GetState
		};
	}

	private void ConfigureRow(Label valueName, ProgressBar slider, Label valueLabel, Label valueLabelPercentage)
	{
		valueName.CustomMinimumSize = new Vector2(ValueNameWidth, 0);
		slider.CustomMinimumSize = new Vector2(SliderWidth, 0);
		valueLabel.CustomMinimumSize = new Vector2(ValueLabelWidth, 0);
		valueLabelPercentage.CustomMinimumSize = new Vector2(PercentageLabelWidth, 0);

		valueName.HorizontalAlignment = HorizontalAlignment.Right;
		valueLabel.HorizontalAlignment = HorizontalAlignment.Right;
		valueLabelPercentage.HorizontalAlignment = HorizontalAlignment.Right;

		valueName.VerticalAlignment = VerticalAlignment.Center;
		valueLabel.VerticalAlignment = VerticalAlignment.Center;
		valueLabelPercentage.VerticalAlignment = VerticalAlignment.Center;
	}

	private void CreateTemplateRows(int rowCount)
	{
		rowCount = Math.Max(rowCount, 1);

		double total = rowCount * (rowCount + 1) / 2.0;

		for (int index = 0; index < rowCount; index++)
		{
			HBoxContainer row;

			if (index == 0)
			{
				row = _distributionRowTemplate;
			}
			else
			{
				row = (HBoxContainer)_distributionRowTemplate.Duplicate();
				_distributionRows.AddChild(row);
			}

			row.Name = $"DistributionRow{index}";

			Label valueName = row.GetNode<Label>("ValueName");
			ProgressBar slider = row.GetNode<ProgressBar>("Slider");
			Label valueLabel = row.GetNode<Label>("ValueLabel");
			Label valueLabelPercentage = row.GetNode<Label>("ValueLabelPercentage");

			ConfigureRow(valueName, slider, valueLabel, valueLabelPercentage);

			double value = rowCount - index;

			slider.MinValue = 0;
			slider.MaxValue = rowCount;
			slider.Value = value;

			valueName.Text = $"Value {index}";
			valueLabel.Text = FormatValue(value);
			valueLabelPercentage.Text = $"{value / total * 100.0:F1}%";
		}
	}

	public override void _Process(double delta)
	{
		if (IsTemplate) return;

		_updateTimer += delta;

		if (_updateTimer < UpdateInterval)
			return;

		_updateTimer = 0;

		UpdateDistribution();
	}

	private void UpdateDistribution()
	{
		if (_distributionEntries.Count == 0)
			return;

		double total = 0;
		double maximum = 0;

		double[] values = new double[_distributionEntries.Count];

		for (int index = 0; index < _distributionEntries.Count; index++)
		{
			double value = _distributionEntries[index].GetState?.Invoke() ?? 0;

			value = Math.Max(value, 0);

			values[index] = value;
			total += value;
			maximum = Math.Max(maximum, value);
		}

		maximum = Math.Max(maximum, 1);

		for (int index = 0; index < _distributionEntries.Count; index++)
		{
			DistributionEntry entry = _distributionEntries[index];
			double value = values[index];

			entry.Slider.MaxValue = maximum;
			entry.Slider.Value = value;

			entry.ValueLabel.Text = FormatValue(value);

			double percentage = total > 0
				? value / total * 100.0
				: 0;

			entry.ValueLabelPercentage.Text = $"{percentage:F1}%";
		}
	}

	private string FormatValue(double value)
	{
		return _distributionType switch
		{
			DistributionType.Int => ((int)value).ToString(),
			DistributionType.UInt => ((uint)value).ToString(),
			DistributionType.Float => ((float)value).ToString("0.##", CultureInfo.InvariantCulture),
			_ => value.ToString(CultureInfo.InvariantCulture)
		};
	}

	private static InternalBinding[] CreateBindings(DistributionBinding<int>[] bindings)
	{
		if (bindings == null) return null;

		InternalBinding[] result = new InternalBinding[bindings.Length];

		for (int index = 0; index < bindings.Length; index++)
		{
			DistributionBinding<int> binding = bindings[index];

			result[index] = new InternalBinding
			{
				Name = binding.Name,
				GetState = binding.GetState != null ? () => binding.GetState() : null
			};
		}

		return result;
	}

	private static InternalBinding[] CreateBindings(DistributionBinding<uint>[] bindings)
	{
		if (bindings == null) return null;

		InternalBinding[] result = new InternalBinding[bindings.Length];

		for (int index = 0; index < bindings.Length; index++)
		{
			DistributionBinding<uint> binding = bindings[index];

			result[index] = new InternalBinding
			{
				Name = binding.Name,
				GetState = binding.GetState != null ? () => binding.GetState() : null
			};
		}

		return result;
	}

	private static InternalBinding[] CreateBindings(DistributionBinding<float>[] bindings)
	{
		if (bindings == null) return null;

		InternalBinding[] result = new InternalBinding[bindings.Length];

		for (int index = 0; index < bindings.Length; index++)
		{
			DistributionBinding<float> binding = bindings[index];

			result[index] = new InternalBinding
			{
				Name = binding.Name,
				GetState = binding.GetState != null ? () => binding.GetState() : null
			};
		}

		return result;
	}
}