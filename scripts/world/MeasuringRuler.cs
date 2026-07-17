using Godot;

namespace Metroidvania.World;

// Level-design helper: a draggable/copyable ruler showing a fixed pixel distance (e.g. max
// jump range) so gaps can be checked by eye instead of playtesting every one. Toggle Visible
// per instance, or select-all + hide before a build.
// [Tool] so it draws directly in the editor's 2D viewport, without pressing Play.
[Tool]
public partial class MeasuringRuler : Node2D
{
	private float _length = 284f;

	[Export]
	public float Length
	{
		get => _length;
		set
		{
			_length = value;
			QueueRedraw();
			UpdateLabel();
		}
	}

	[Export] public Color LineColor = new(1f, 0.2f, 0.6f, 0.85f);

	private Label _label;

	public override void _Ready()
	{
		_label = GetNode<Label>("Label");
		UpdateLabel();

		// Editor-only gizmo: stay invisible once the game is actually running/playtested.
		if (!Engine.IsEditorHint())
			Visible = false;
	}

	public override void _Draw()
	{
		const float tickHeight = 10f;
		DrawLine(new Vector2(0, 0), new Vector2(_length, 0), LineColor, 2f);
		DrawLine(new Vector2(0, -tickHeight / 2), new Vector2(0, tickHeight / 2), LineColor, 2f);
		DrawLine(new Vector2(_length, -tickHeight / 2), new Vector2(_length, tickHeight / 2), LineColor, 2f);
	}

	private void UpdateLabel()
	{
		if (_label is not null)
		{
			_label.Text = $"{_length:0}px";
			_label.Position = new Vector2(_length / 2 - _label.Size.X / 2, -20);
		}
	}
}
