using Godot;
using PlanetGame.Data;

public partial class BrushOptions : PanelContainer
{
	[Signal]
	public delegate void SizeChangedEventHandler(float size);

	[Signal]
	public delegate void HardnessChangedEventHandler(float hardness);

	[Signal]
	public delegate void OpacityChangedEventHandler(float opacity);

	private static TessellationData TessellationData => SaveManager.TessellationData;
	private static VirtualTextureData VirtualTextureData => SaveManager.VirtualTextureData;

	private LineEdit SizeInput => GetNode<LineEdit>("%SizeInput");
	private HSlider SizeSlider => GetNode<HSlider>("%SizeSlider");
	private LineEdit HardnessInput => GetNode<LineEdit>("%HardnessInput");
	private HSlider HardnessSlider => GetNode<HSlider>("%HardnessSlider");
	private LineEdit OpacityInput => GetNode<LineEdit>("%OpacityInput");
	private HSlider OpacitySlider => GetNode<HSlider>("%OpacitySlider");
	public GridContainer BrushSelection => GetNode<GridContainer>("%BrushSelection");

	public override void _Ready()
	{
		SizeSlider.ValueChanged += OnSizeSliderChanged;
		HardnessSlider.ValueChanged += OnHardnessSliderChanged;
		OpacitySlider.ValueChanged += OnOpacitySliderChanged;

		SizeInput.TextSubmitted += OnSizeSubmitted;
		HardnessInput.TextSubmitted += OnHardnessSubmitted;
		OpacityInput.TextSubmitted += OnOpacitySubmitted;

		SetupRanges();

		SetSize((float)SizeSlider.Value, false);
		SetHardness((float)HardnessSlider.Value, false);
		SetOpacity((float)OpacitySlider.Value, false);
	}

	private void OnSizeSliderChanged(double value)
	{
		SetSize((float)value);
	}

	private void OnHardnessSliderChanged(double value)
	{
		SetHardness((float)value);
	}

	private void OnOpacitySliderChanged(double value)
	{
		SetOpacity((float)value);
	}

	private void SetupRanges()
	{
		SizeSlider.MinValue = TessellationData.Radius * 0.0001f;
		SizeSlider.MaxValue = TessellationData.Radius;
		SizeSlider.Step = 1f;

		HardnessSlider.MinValue = 0;
		HardnessSlider.MaxValue = 100;
		HardnessSlider.Step = 1;

		OpacitySlider.MinValue = 0;
		OpacitySlider.MaxValue = 100;
		OpacitySlider.Step = 1;
	}

	private void OnSizeSubmitted(string text)
	{
		if (TryParseValue(text, out float value))
			SetSize(value);
		else
			SizeInput.Text = $"{SizeSlider.Value:0.##}";
	}

	private void OnHardnessSubmitted(string text)
	{
		if (TryParseValue(text, out float value))
			SetHardness(value);
		else
			HardnessInput.Text = $"{HardnessSlider.Value:0}%";
	}

	private void OnOpacitySubmitted(string text)
	{
		if (TryParseValue(text, out float value))
			SetOpacity(value);
		else
			OpacityInput.Text = $"{OpacitySlider.Value:0}%";
	}

	public void SetSize(float value, bool emitSignal = true)
	{
		value = Mathf.Clamp(value, (float)SizeSlider.MinValue, (float)SizeSlider.MaxValue);

		SizeSlider.SetValueNoSignal(value);
		SizeInput.Text = $"{value:0.##}";

		if (emitSignal)
			EmitSignal(SignalName.SizeChanged, value);
	}

	public void SetHardness(float value, bool emitSignal = true)
	{
		value = Mathf.Clamp(value, (float)HardnessSlider.MinValue, (float)HardnessSlider.MaxValue);

		HardnessSlider.SetValueNoSignal(value);
		HardnessInput.Text = $"{value:0}%";

		if (emitSignal)
			EmitSignal(SignalName.HardnessChanged, value);
	}

	public void SetOpacity(float value, bool emitSignal = true)
	{
		value = Mathf.Clamp(value, (float)OpacitySlider.MinValue, (float)OpacitySlider.MaxValue);

		OpacitySlider.SetValueNoSignal(value);
		OpacityInput.Text = $"{value:0}%";

		if (emitSignal)
			EmitSignal(SignalName.OpacityChanged, value);
	}

	private static bool TryParseValue(string text, out float value)
	{
		text = text
			.Replace("%", "")
			.Trim();

		return float.TryParse(text, out value);
	}
}
