using Godot;

namespace Metroidvania.World;

// Reusable row of ArrowTrap pieces: set Count and get that many wall-mounted arrow traps laid
// out along Spacing, same idea as SpikeAccordion/CorridorBlock applied to ArrowTrap. Spacing is
// a vector (not a float) so the same piece lines traps down a vertical wall (default, shooting
// sideways) or across a horizontal one (shooting up/down) without a separate orientation setting.
// StaggerStep is the one knob that switches between the two modes the level design asked for:
// 0 fires every trap in the row together ("todas juntas"), >0 delays each next trap's first shot
// so the row fires like a wave ("accordion") — mirrors SpikeAccordion's StaggerStep exactly.
// Rebuild wipes and regenerates every ArrowTrap child from scratch on any change — detach a trap
// into its own ArrowTrap.tscn instance if one needs to be special.
// [Tool] so the row previews live in the editor, before ever pressing Play.
[Tool]
public partial class ArrowTrapAccordion : Node2D
{
	private const string ArrowTrapScenePath = "res://scenes/world/Rehusables/ArrowTrap.tscn";

	private int _count = 5;

	[Export(PropertyHint.Range, "1,40,1")]
	public int Count
	{
		get => _count;
		set
		{
			_count = Mathf.Max(1, value);
			Rebuild();
		}
	}

	private Vector2 _spacing = new(0f, 80f);

	[Export]
	public Vector2 Spacing
	{
		get => _spacing;
		set
		{
			_spacing = value;
			Rebuild();
		}
	}

	private float _staggerStep = 0.3f;

	[Export]
	public float StaggerStep
	{
		get => _staggerStep;
		set
		{
			_staggerStep = value;
			Rebuild();
		}
	}

	private Vector2 _direction = Vector2.Right;

	[Export]
	public Vector2 Direction
	{
		get => _direction;
		set
		{
			_direction = value;
			Rebuild();
		}
	}

	private float _fireInterval = 1.8f;

	[Export]
	public float FireInterval
	{
		get => _fireInterval;
		set
		{
			_fireInterval = value;
			Rebuild();
		}
	}

	private bool _initialized;

	public override void _Ready()
	{
		_initialized = true;
		Rebuild();
	}

	private void Rebuild()
	{
		// Property setters (Count etc.) call Rebuild() directly, and Godot also invokes those same
		// setters to restore this node's saved state while it's still being instantiated as part of
		// a parent scene (before _Ready runs) — Instantiate<ArrowTrap>() below can throw a spurious
		// InvalidCastException if it reenters scene instancing at that point. _initialized gates
		// Rebuild() to only actually run once, from _Ready(), after all restored values have landed.
		if (!_initialized || !IsInsideTree())
			return;

		foreach (Node child in GetChildren())
			child.Free();

		PackedScene arrowTrapScene = GD.Load<PackedScene>(ArrowTrapScenePath);
		for (int i = 0; i < _count; i++)
		{
			ArrowTrap trap = arrowTrapScene.Instantiate<ArrowTrap>();
			trap.Name = $"ArrowTrap{i + 1}";
			trap.Position = i * _spacing;
			trap.Direction = _direction;
			trap.FireInterval = _fireInterval;
			trap.StartOffset = i * _staggerStep;
			AddChild(trap);
			trap.Owner = this;
		}
	}
}
