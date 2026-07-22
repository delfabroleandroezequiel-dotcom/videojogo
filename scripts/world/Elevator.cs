using Godot;

namespace Metroidvania.World;

public enum ElevatorSwitches
{
	None,
	AtBottom,
	AtTop,
	Both,
}

public partial class Elevator : AnimatableBody2D
{
	[Export] public float TravelDistance = 230f;
	[Export] public float Speed = 80f;

	// Opt-in (defaults to None): spawns Lever.tscn instances at runtime, already wired to
	// CallToBottom()/CallToTop() — no manual signal-connecting in the editor needed. A switch at
	// the bottom summons the car down, one at the top summons it up. Spawned as siblings
	// (children of this elevator's parent, not of the elevator itself) so they stay put at
	// their landing instead of riding along. Leave as None and wire a Lever by hand (via its
	// ElevatorPath, see Lever.cs) if a single lever should only ever call one direction, or if
	// it needs to sit somewhere these auto-placed defaults don't fit.
	[Export] public ElevatorSwitches Switches = ElevatorSwitches.None;

	// Local offset (relative to the bottom/top landing) so the lever sits beside the shaft
	// instead of inside it.
	[Export] public Vector2 SwitchOffset = new(40f, 0f);

	private Vector2 _bottomPosition;
	private Vector2 _topPosition;
	private Vector2 _target;

	public override void _Ready()
	{
		_bottomPosition = Position;
		_topPosition = Position - new Vector2(0, TravelDistance);
		_target = _bottomPosition;

		Area2D triggerZone = GetNode<Area2D>("TriggerZone");
		triggerZone.BodyEntered += OnTriggerEntered;

		if (Switches != ElevatorSwitches.None)
			SpawnSwitches();
	}

	private void SpawnSwitches()
	{
		Node parent = GetParent() ?? this;

		if (Switches is ElevatorSwitches.AtBottom or ElevatorSwitches.Both)
			LeverSpawner.SpawnAt(parent, _bottomPosition + SwitchOffset, CallToBottom);

		if (Switches is ElevatorSwitches.AtTop or ElevatorSwitches.Both)
			LeverSpawner.SpawnAt(parent, _topPosition + SwitchOffset, CallToTop);
	}

	private void OnTriggerEntered(Node2D body)
	{
		if (!body.IsInGroup("player"))
			return;

		_target = _target == _topPosition ? _bottomPosition : _topPosition;
	}

	public void CallToTop() => _target = _topPosition;
	public void CallToBottom() => _target = _bottomPosition;

	public override void _PhysicsProcess(double delta)
	{
		Position = Position.MoveToward(_target, Speed * (float)delta);
	}
}
