using System;
using Godot;

[Tool]
public partial class Reticle : Control
{

	private uint _slotCount = 5;
	float _innerRadius;
	float _outerRadius;
	float _separaterWidth;

	Action[] _actions = [];

	public event Action<int> ReticleOptionSelected;

	
	[Export]
	public float InnerRadius
	{
		get => _innerRadius;
		set
		{
			// _reticleInnerRadius = Mathf.Clamp(value, 0, _reticleOuterRadius);
			_innerRadius = Mathf.Max(0, value);
			QueueRedraw();
		}
	}

	[Export]
	public float OuterRadius
	{
		get => _outerRadius;
		set
		{
			_outerRadius = Mathf.Max(0, value);
			QueueRedraw();
		}
	}
	[Export]
	public float SeparaterWidth
	{
		get => _separaterWidth;
		set
		{
			_separaterWidth = Mathf.Max(0, value);
			QueueRedraw();
		}
	}

	[Export(PropertyHint.Range, "1,7,1")]
	public uint SlotCount
	{
		get => _slotCount;
		set
		{
			_slotCount = value;
			_actions = new Action[_slotCount];
			QueueRedraw();
		}
	}

	public bool IsActive = false;
	public Vector2 SpawnPoint = Vector2.Inf;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Pass;
		_actions = new Action[SlotCount];
	}

	public override void _Draw()
	{
		if (IsActive)
		{
			DrawReticle(SpawnPoint, GetLocalMousePosition());
		}
		else if (Engine.IsEditorHint())
		{
			DrawReticle(Size / 2f, Size / 2f);
		}
	}

	private int _selectedIndex = -1;

	public void SetAction(int index, Action action)
	{
		if (index >= SlotCount || index < 0)
			throw new ArgumentOutOfRangeException($"Index: {index} is out of range for Action Array Size: {SlotCount}");

		_actions[index] = action;
	}

	private void DrawRingSegment(Vector2 center, float innerRadius, float outerRadius, float startAngle, float endAngle, float separatorWidth, Color color, int resolution = 32)
	{
		float halfSeparator = separatorWidth / 2.0f;

		float innerOffset = Mathf.Asin(halfSeparator / innerRadius);
		float outerOffset = Mathf.Asin(halfSeparator / outerRadius);

		float innerStart = startAngle + innerOffset;
		float innerEnd = endAngle - innerOffset;

		float outerStart = startAngle + outerOffset;
		float outerEnd = endAngle - outerOffset;

		Vector2[] points = new Vector2[(resolution + 1) * 2];

		for (int i = 0; i <= resolution; i++)
		{
			float t = (float)i / resolution;
			float angle = Mathf.Lerp(outerStart, outerEnd, t);

			points[i] = center + Vector2.Right.Rotated(angle) * outerRadius;
		}

		for (int i = 0; i <= resolution; i++)
		{
			float t = (float)i / resolution;
			float angle = Mathf.Lerp(innerEnd, innerStart, t);

			points[resolution + 1 + i] = center + Vector2.Right.Rotated(angle) * innerRadius;
		}

		DrawColoredPolygon(points, color);
	}

	private void DrawReticle(Vector2 center, Vector2 mousePosition)
	{
		float angleStep = Mathf.Tau / SlotCount;
		float halfAngleStep = angleStep / 2;
		Vector2 ReticleStartingDirection = Vector2.Up.Rotated(halfAngleStep);

		Vector2 selection = GetLocalMousePosition() - SpawnPoint;
		float angle = ReticleStartingDirection.AngleTo(selection);
		if (angle < 0)
			angle += Mathf.Tau;

		int index = (int)(angle / angleStep);

		float startingAngle = -Mathf.Pi / 2.0f + halfAngleStep;

		for (int i = 0; i < SlotCount; i++)
		{
			Color color = Colors.LightSlateGray;
			if (i == index && selection.Length() > InnerRadius)
				color = Colors.Green;

			float startAngle = startingAngle + i * angleStep;
			float endAngle = startAngle + angleStep;

			DrawRingSegment(
				center,
				InnerRadius,
				OuterRadius,
				startAngle,
				endAngle,
				SeparaterWidth,
				color
			);
		}



		DrawLine(center, mousePosition, Colors.Red, antialiased: true);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Right)
		{
			IsActive = mouseButton.Pressed;

			if (IsActive)
			{
				SpawnPoint = GetLocalMousePosition();
				// Input.WarpMouse(GetGldobalRect().GetCenter());
			}
			else
			{
				Vector2 selection = GetLocalMousePosition() - SpawnPoint;

				float angleStep = Mathf.Tau / SlotCount;
				float halfAngleStep = angleStep / 2;
				Vector2 ReticleStartingDirection = Vector2.Up.Rotated(halfAngleStep);

				float angle = ReticleStartingDirection.AngleTo(selection);
				if (angle < 0)
					angle += Mathf.Tau;

				int index = (int)(angle / angleStep);

				if (selection.Length() < InnerRadius)
					return;

				GD.Print(index);
				ReticleOptionSelected?.Invoke(index);
			}

			QueueRedraw();
		}

		if (@event is InputEventMouseMotion && IsActive)
			QueueRedraw();
	}
}