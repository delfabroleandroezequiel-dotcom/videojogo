using Godot;

namespace Metroidvania.World;

// Reusable greybox traversal piece: set Count and get that many Rope instances placed side by
// side, pre-staggered so they don't all swing in lockstep — same idea as SpikeAccordion, applied
// to Rope.tscn. Rebuild wipes and regenerates every Rope child from scratch on any change, so
// this is a "just set the count" piece, not one for hand-tuning individual ropes afterward —
// detach a rope into its own Rope.tscn instance if one needs to be special.
// [Tool] so the row previews live in the editor, before ever pressing Play.
[Tool]
public partial class RopeAccordion : Node2D
{
	private const string RopeScenePath = "res://scenes/world/Rope.tscn";

	private int _count = 3;

	[Export(PropertyHint.Range, "1,20,1")]
	public int Count
	{
		get => _count;
		set { _count = Mathf.Max(1, value); Rebuild(); }
	}

	private float _ropeLength = 200f;

	[Export]
	public float RopeLength
	{
		get => _ropeLength;
		set { _ropeLength = value; Rebuild(); }
	}

	// On: Spacing is derived from RopeLength (a jump distance that keeps a neighbor's rest
	// position within reach of a swing). Off: Spacing below is used as-is, tune it by hand.
	private bool _autoSpacing = true;

	[Export]
	public bool AutoSpacing
	{
		get => _autoSpacing;
		set { _autoSpacing = value; Rebuild(); }
	}

	// Fraction of RopeLength used as the gap between ropes when AutoSpacing is on. Under 1 keeps
	// a swung rope's reach overlapping its neighbor's resting position (an easy hop); pushing it
	// closer to (or past) 1 demands real swing amplitude/momentum to make the jump.
	private float _autoSpacingFactor = 0.85f;

	[Export]
	public float AutoSpacingFactor
	{
		get => _autoSpacingFactor;
		set { _autoSpacingFactor = value; Rebuild(); }
	}

	private float _spacing = 170f;

	[Export]
	public float Spacing
	{
		get => _spacing;
		set { _spacing = value; Rebuild(); }
	}

	private float _phaseStaggerStep = 0.6f;

	// Added to each next rope's PhaseOffset so the row's idle sway doesn't move in unison (matches
	// the hand-staggered PhaseOffset already used when placing ropes individually).
	[Export]
	public float PhaseStaggerStep
	{
		get => _phaseStaggerStep;
		set { _phaseStaggerStep = value; Rebuild(); }
	}

	public override void _Ready() => Rebuild();

	private void Rebuild()
	{
		if (!IsInsideTree())
			return;

		foreach (Node child in GetChildren())
			child.Free();

		float spacing = _autoSpacing ? _ropeLength * _autoSpacingFactor : _spacing;

		PackedScene ropeScene = GD.Load<PackedScene>(RopeScenePath);
		for (int i = 0; i < _count; i++)
		{
			Rope rope = ropeScene.Instantiate<Rope>();
			rope.Name = $"Rope{i + 1}";
			rope.Position = new Vector2(i * spacing, 0f);
			rope.Length = _ropeLength;
			rope.PhaseOffset = i * _phaseStaggerStep;
			AddChild(rope);
			rope.Owner = this;
		}
	}
}
