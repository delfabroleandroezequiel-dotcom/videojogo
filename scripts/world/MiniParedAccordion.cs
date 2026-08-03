using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

public enum WallJumpGapMode
{
	Computed,
	Custom,
}

// Reusable greybox wall-jump ladder: set Count and get that many MiniPared pillars, alternating
// sides (left/right) every hop — that alternation is structural, not a toggle, since a wall-jump
// kick always pushes the player away from the wall they're on, so the next grab has to be the
// opposite side — while climbing by VerticalStep each hop, same "just set the count" idea as
// MiniCorridorAccordion/MiniPlataformaAccordion.
// The shaft width is NOT a free value: it's derived from GameConfig.WallJumpGap* (mirrors
// Player's WallJumpVelocityX/Y) shrunk by how much of that kick's height budget VerticalStep
// spends climbing, same elliptical jump-envelope approximation as MiniPlataformaAccordion — a
// level-design heuristic, not an exact trajectory integration. GapMode.Custom skips this and uses
// CustomShaftWidth instead, for a hand-tuned shaft once playtesting says the computed one needs
// adjusting (wall-jump timing is more player-dependent than a plain jump, so treat the computed
// width as a starting point to calibrate, not a guarantee).
// Rebuild wipes and regenerates every MiniPared child from scratch on any change, so detach a
// pillar into its own MiniPared.tscn instance if one needs to be special.
// [Tool] so the ladder previews live in the editor, before ever pressing Play.
[Tool]
public partial class MiniParedAccordion : Node2D
{
	private const string MiniParedScenePath = "res://scenes/world/Rehusables/MiniPared.tscn";

	private int _count = 5;

	[Export(PropertyHint.Range, "1,40,1")]
	public int Count
	{
		get => _count;
		set { _count = Mathf.Max(1, value); Rebuild(); }
	}

	private float _segmentLength = 160f;

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

	private float _verticalStep = -80f;

	// Height climbed (negative) or dropped (positive) from one pillar's grab to the next's, every
	// hop — a wall-jump ladder normally climbs, so this defaults negative.
	[Export]
	public float VerticalStep
	{
		get => _verticalStep;
		set { _verticalStep = value; Rebuild(); }
	}

	private WallJumpGapMode _gapMode = WallJumpGapMode.Computed;

	[Export]
	public WallJumpGapMode GapMode
	{
		get => _gapMode;
		set { _gapMode = value; Rebuild(); }
	}

	private float _customShaftWidth = 160f;

	// Only used when GapMode is Custom — the computed shaft width ignores this.
	[Export]
	public float CustomShaftWidth
	{
		get => _customShaftWidth;
		set { _customShaftWidth = value; Rebuild(); }
	}

	[Export] public Color FillColor = new(0.5f, 0.5f, 0.55f, 0.6f);

	private bool _initialized;

	public override void _Ready()
	{
		_initialized = true;
		Rebuild();
	}

	private float ResolveShaftWidth()
	{
		if (_gapMode == WallJumpGapMode.Custom)
			return _customShaftWidth;

		// Descending or flat hops don't eat into the kick's height budget, so the full shaft
		// width is always safely reachable; only climbing (negative VerticalStep) shrinks it.
		float ascend = Mathf.Max(0f, -_verticalStep);
		if (ascend <= 0f)
			return GameConfig.WallJumpGapHorizontal;

		float t = Mathf.Clamp(ascend / GameConfig.WallJumpGapVertical, 0f, 1f);
		return GameConfig.WallJumpGapHorizontal * Mathf.Sqrt(Mathf.Max(0f, 1f - t * t));
	}

	private void Rebuild()
	{
		// See RopeAccordion.Rebuild for why _initialized gates this — property setters restoring
		// this node's saved state before _Ready runs can otherwise reenter Instantiate<MiniPared>()
		// mid scene-instantiation and throw a spurious InvalidCastException.
		if (!_initialized || !IsInsideTree())
			return;

		foreach (Node child in GetChildren())
			child.Free();

		float shaftWidth = ResolveShaftWidth();
		PackedScene miniParedScene = GD.Load<PackedScene>(MiniParedScenePath);

		Vector2 cursor = Vector2.Zero;
		float sign = 1f;
		for (int i = 0; i < _count; i++)
		{
			MiniPared pillar = miniParedScene.Instantiate<MiniPared>();
			pillar.Name = $"MiniPared{i + 1}";
			pillar.Length = _segmentLength;
			pillar.Thickness = _thickness;
			pillar.FillColor = FillColor;
			pillar.Position = cursor;
			AddChild(pillar);
			pillar.Owner = this;

			// X ping-pongs between 0 and shaftWidth (a real shaft has two walls a fixed distance
			// apart); Y always advances by VerticalStep — the climb is one-directional, unlike
			// MiniPlataformaAccordion's optional zigzag.
			cursor += new Vector2(shaftWidth * sign, _verticalStep);
			sign = -sign;
		}

		// Exit sits one hop past the last pillar, so the next block's origin can just be dropped
		// on Exit's global position — same convention as the other Mini*Accordion pieces.
		var exit = new Marker2D { Name = "Exit", Position = cursor };
		AddChild(exit);
		exit.Owner = this;
	}
}
