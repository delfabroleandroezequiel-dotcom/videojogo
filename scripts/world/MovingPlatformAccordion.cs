using Godot;

namespace Metroidvania.World;

// Reusable greybox row: set Count and get that many MovingPlatform instances laid out Spacing
// apart, all sharing the same patrol config (PointBOffset/Speed/AutoPatrol/WaitTimeAtPoints/
// JumpHeight/Switches/SwitchOffset) — same "just set the count" idea as SpikeAccordion, applied
// to MovingPlatform instead of SpikeTrap.
// StaggerStep feeds MovingPlatform's own StartOffset (i * StaggerStep per platform), so a row
// doesn't have to move in lockstep — set it to half a leg's travel time (_legDistance / Speed,
// i.e. roughly PointBOffset.Length() / Speed / 2 for a straight leg) to get neighbors in
// anti-phase: one approaching while the next pulls away ("se acercan y se alejan"). Left at 0 by
// default (synced) since that default depends on Speed/PointBOffset, which are free to change.
// Rebuild wipes and regenerates every MovingPlatform child from scratch on any change, so detach
// a platform into its own MovingPlatform.tscn instance if one needs to be special.
// [Tool] so the row's rest positions preview live in the editor — the platforms themselves only
// animate in Play, same as a lone MovingPlatform (its _Ready isn't [Tool]-gated).
[Tool]
public partial class MovingPlatformAccordion : Node2D
{
	private const string MovingPlatformScenePath = "res://scenes/world/MovingPlatform.tscn";

	private int _count = 4;

	[Export(PropertyHint.Range, "1,40,1")]
	public int Count
	{
		get => _count;
		set { _count = Mathf.Max(1, value); Rebuild(); }
	}

	private Vector2 _spacing = new(260f, 0f);

	[Export]
	public Vector2 Spacing
	{
		get => _spacing;
		set { _spacing = value; Rebuild(); }
	}

	private Vector2 _pointBOffset = new(200f, 0f);

	[Export]
	public Vector2 PointBOffset
	{
		get => _pointBOffset;
		set { _pointBOffset = value; Rebuild(); }
	}

	private float _speed = 80f;

	[Export]
	public float Speed
	{
		get => _speed;
		set { _speed = value; Rebuild(); }
	}

	private bool _autoPatrol = true;

	[Export]
	public bool AutoPatrol
	{
		get => _autoPatrol;
		set { _autoPatrol = value; Rebuild(); }
	}

	private float _waitTimeAtPoints;

	[Export]
	public float WaitTimeAtPoints
	{
		get => _waitTimeAtPoints;
		set { _waitTimeAtPoints = value; Rebuild(); }
	}

	private float _jumpHeight;

	[Export]
	public float JumpHeight
	{
		get => _jumpHeight;
		set { _jumpHeight = value; Rebuild(); }
	}

	private PlatformSwitches _switches = PlatformSwitches.None;

	[Export]
	public PlatformSwitches Switches
	{
		get => _switches;
		set { _switches = value; Rebuild(); }
	}

	private Vector2 _switchOffset = new(0f, -32f);

	[Export]
	public Vector2 SwitchOffset
	{
		get => _switchOffset;
		set { _switchOffset = value; Rebuild(); }
	}

	private float _staggerStep;

	[Export]
	public float StaggerStep
	{
		get => _staggerStep;
		set { _staggerStep = value; Rebuild(); }
	}

	public override void _Ready() => Rebuild();

	private void Rebuild()
	{
		if (!IsInsideTree())
			return;

		foreach (Node child in GetChildren())
			child.Free();

		PackedScene movingPlatformScene = GD.Load<PackedScene>(MovingPlatformScenePath);

		Vector2 cursor = Vector2.Zero;
		for (int i = 0; i < _count; i++)
		{
			MovingPlatform platform = movingPlatformScene.Instantiate<MovingPlatform>();
			platform.Name = $"MovingPlatform{i + 1}";
			platform.PointBOffset = _pointBOffset;
			platform.Speed = _speed;
			platform.AutoPatrol = _autoPatrol;
			platform.WaitTimeAtPoints = _waitTimeAtPoints;
			platform.JumpHeight = _jumpHeight;
			platform.Switches = _switches;
			platform.SwitchOffset = _switchOffset;
			platform.StartOffset = i * _staggerStep;
			platform.Position = cursor;
			AddChild(platform);
			platform.Owner = this;

			cursor += _spacing;
		}

		// Exit sits one spacing step past the last platform, so the next block's origin can just
		// be dropped on Exit's global position — same convention as the other Mini*Accordion pieces.
		var exit = new Marker2D { Name = "Exit", Position = cursor };
		AddChild(exit);
		exit.Owner = this;
	}
}
