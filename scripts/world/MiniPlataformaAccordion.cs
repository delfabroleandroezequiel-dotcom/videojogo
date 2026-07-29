using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Reusable greybox row: set Count and get that many MiniPlataforma pieces marching sideways, each
// one stepping up/down from the previous by VerticalStep (alternating direction when Alternate is
// on, for the "crossed" zigzag staircase look) — same "just set the count" idea as SpikeAccordion
// and MiniCorridorAccordion, applied to a diagonal jump gap instead of a flat one.
// The horizontal spacing is NOT a free value: it's derived from JumpMode's physics-derived reach
// (GameConfig.JumpGap*, same source as MiniCorridorAccordion) shrunk by how much of that jump's
// height budget VerticalStep's ascending side spends, using an elliptical jump-envelope
// approximation (horizontal reach at height h ≈ hMax * sqrt(1 - (h/vMax)^2)) — a level-design
// heuristic, not an exact trajectory integration; the "descending" side of an alternated zigzag is
// always at least as reachable as flat ground, so it never reduces the shared step, only the
// ascending side does. JumpMode.Custom skips all of this and uses CustomHorizontalStep instead,
// for a hand-tuned gap once playtesting says the computed one needs adjusting.
// Rebuild wipes and regenerates every MiniPlataforma child from scratch on any change, so detach a
// piece into its own MiniPlataforma.tscn instance if one needs to be special.
// [Tool] so the row previews live in the editor, before ever pressing Play.
[Tool]
public partial class MiniPlataformaAccordion : Node2D
{
	private const string MiniPlataformaScenePath = "res://scenes/world/Rehusables/MiniPlataforma.tscn";

	private int _count = 5;

	[Export(PropertyHint.Range, "1,40,1")]
	public int Count
	{
		get => _count;
		set { _count = Mathf.Max(1, value); Rebuild(); }
	}

	private float _segmentLength = 96f;

	[Export]
	public float SegmentLength
	{
		get => _segmentLength;
		set { _segmentLength = value; Rebuild(); }
	}

	private float _thickness = 24f;

	[Export]
	public float Thickness
	{
		get => _thickness;
		set { _thickness = value; Rebuild(); }
	}

	private JumpGapMode _jumpMode = JumpGapMode.SingleJump;

	[Export]
	public JumpGapMode JumpMode
	{
		get => _jumpMode;
		set { _jumpMode = value; Rebuild(); }
	}

	private float _verticalStep = 60f;

	// Height stepped down (positive) or up (negative) from one platform to the next. With
	// Alternate on, this is the magnitude only — the sign flips every step.
	[Export]
	public float VerticalStep
	{
		get => _verticalStep;
		set { _verticalStep = value; Rebuild(); }
	}

	private bool _alternate = true;

	[Export]
	public bool Alternate
	{
		get => _alternate;
		set { _alternate = value; Rebuild(); }
	}

	private float _customHorizontalStep = 160f;

	// Only used when JumpMode is Custom — the computed SingleJump/DoubleJump step ignores this.
	[Export]
	public float CustomHorizontalStep
	{
		get => _customHorizontalStep;
		set { _customHorizontalStep = value; Rebuild(); }
	}

	private bool _oneWay = true;

	[Export]
	public bool OneWay
	{
		get => _oneWay;
		set { _oneWay = value; Rebuild(); }
	}

	[Export] public Color FillColor = new(0.5f, 0.5f, 0.55f, 0.6f);

	public override void _Ready() => Rebuild();

	private float ResolveHorizontalStep()
	{
		if (_jumpMode == JumpGapMode.Custom)
			return _customHorizontalStep;

		float hMax = _jumpMode == JumpGapMode.SingleJump
			? GameConfig.JumpGapHorizontalSingle
			: GameConfig.JumpGapHorizontalDouble;

		// Alternating means every step includes an ascending half of magnitude VerticalStep;
		// non-alternating only constrains the step when it's actually going up.
		float ascend = _alternate ? Mathf.Abs(_verticalStep) : Mathf.Max(0f, -_verticalStep);
		if (ascend <= 0f)
			return hMax;

		float vMax = _jumpMode == JumpGapMode.SingleJump
			? GameConfig.JumpGapVerticalSingle
			: GameConfig.JumpGapVerticalDouble;
		float t = Mathf.Clamp(ascend / vMax, 0f, 1f);
		return hMax * Mathf.Sqrt(Mathf.Max(0f, 1f - t * t));
	}

	private void Rebuild()
	{
		if (!IsInsideTree())
			return;

		foreach (Node child in GetChildren())
			child.Free();

		float horizontalStep = ResolveHorizontalStep();
		PackedScene miniPlataformaScene = GD.Load<PackedScene>(MiniPlataformaScenePath);

		Vector2 cursor = Vector2.Zero;
		float sign = 1f;
		for (int i = 0; i < _count; i++)
		{
			MiniPlataforma piece = miniPlataformaScene.Instantiate<MiniPlataforma>();
			piece.Name = $"MiniPlataforma{i + 1}";
			piece.Length = _segmentLength;
			piece.Thickness = _thickness;
			piece.OneWay = _oneWay;
			piece.FillColor = FillColor;
			piece.Position = cursor;
			AddChild(piece);
			piece.Owner = this;

			float vStep = _alternate ? _verticalStep * sign : _verticalStep;
			if (_alternate)
				sign = -sign;
			cursor += new Vector2(horizontalStep, vStep);
		}

		// Exit sits one jump-step past the last piece, so the next block's origin can just be
		// dropped on Exit's global position — same convention as CorridorBlock/MiniCorridorAccordion.
		var exit = new Marker2D { Name = "Exit", Position = cursor };
		AddChild(exit);
		exit.Owner = this;
	}
}
