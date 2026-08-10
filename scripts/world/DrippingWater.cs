using Godot;

namespace Metroidvania.World;

// Animated drip — a small drop that falls FallDistance then resets to the top on a loop, staggered
// per instance via StartOffset (same stagger idea SpikeAccordion/SpikeTrap use for their pieces).
// Draws a static wet mark at the source so it still reads as something in the editor before Play.
// Skips animating while in the editor (Engine.IsEditorHint()) so a scene full of these doesn't
// burn CPU/redraw while you're placing them — only the source mark previews there.
[Tool]
public partial class DrippingWater : Node2D
{
	private float _fallDistance = 40f;

	[Export(PropertyHint.Range, "4,400,1")]
	public float FallDistance
	{
		get => _fallDistance;
		set { _fallDistance = Mathf.Max(4f, value); QueueRedraw(); }
	}

	private float _dropInterval = 1.5f;

	[Export(PropertyHint.Range, "0.1,10,0.1")]
	public float DropInterval
	{
		get => _dropInterval;
		set => _dropInterval = Mathf.Max(0.05f, value);
	}

	// Seconds into the cycle this drop starts at — offset each instance so a row of drips doesn't
	// fall in lockstep.
	[Export]
	public float StartOffset { get; set; }

	private float _dropRadius = 2f;

	[Export(PropertyHint.Range, "0.5,10,0.1")]
	public float DropRadius
	{
		get => _dropRadius;
		set { _dropRadius = Mathf.Max(0.5f, value); QueueRedraw(); }
	}

	private Color _dropColor = new(0.55f, 0.68f, 0.7f, 0.8f);

	[Export]
	public Color DropColor
	{
		get => _dropColor;
		set { _dropColor = value; QueueRedraw(); }
	}

	private float _t;

	public override void _Ready()
	{
		_t = StartOffset;
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint())
			return;

		_t += (float)delta;
		if (_t >= _dropInterval)
			_t -= _dropInterval;

		QueueRedraw();
	}

	public override void _Draw()
	{
		// Small permanent wet mark at the source.
		DrawCircle(Vector2.Zero, _dropRadius * 0.8f, _dropColor * new Color(1f, 1f, 1f, 0.4f));

		if (Engine.IsEditorHint())
			return;

		float progress = Mathf.Clamp(_t / _dropInterval, 0f, 1f);
		float y = _fallDistance * progress;
		float fade = 1f - Mathf.Clamp((progress - 0.85f) / 0.15f, 0f, 1f);
		DrawCircle(new Vector2(0f, y), _dropRadius, _dropColor * new Color(1f, 1f, 1f, fade));
	}
}
