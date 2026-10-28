using Godot;

namespace Metroidvania.World;

// Middleman between a trigger (PressurePlate, Lever...) and the traps it sets off. Calling
// Activate() forwards TargetMethod to every target — Pulses times, PulseInterval apart, so one
// step on a plate can keep an ArrowTrap volleying for a while (an ambush) instead of firing once.
// DisarmWith points at the thing the trap is guarding (usually a ChargedWall/SecretWall): once
// that node is gone — broken now, or already broken in this save and removed on load — the relay
// stops forwarding for good, so a "contratiempo" only ever springs on the way in and never
// ambushes the player walking back through the opened passage.
public partial class TrapRelay : Node2D
{
	[Export] public NodePath[] Targets = System.Array.Empty<NodePath>();
	[Export] public string TargetMethod = "Activate";
	[Export] public NodePath DisarmWith;
	[Export] public int Pulses = 1;
	[Export] public float PulseInterval = 1f;

	private int _pulsesLeft;
	private float _timer;

	public bool IsDisarmed
	{
		get
		{
			if (DisarmWith is null || DisarmWith.IsEmpty)
				return false;
			Node guarded = GetNodeOrNull(DisarmWith);
			return guarded is null || !IsInstanceValid(guarded) || guarded.IsQueuedForDeletion() || !guarded.IsInsideTree();
		}
	}

	public void Activate()
	{
		if (_pulsesLeft > 0 || IsDisarmed)
			return;
		_pulsesLeft = Mathf.Max(1, Pulses);
		_timer = 0f;
	}

	public override void _Process(double delta)
	{
		if (_pulsesLeft <= 0)
			return;
		_timer -= (float)delta;
		if (_timer > 0f)
			return;
		// Breaking the guarded wall mid-ambush calls the rest of it off.
		if (IsDisarmed)
		{
			_pulsesLeft = 0;
			return;
		}
		_pulsesLeft--;
		_timer = PulseInterval;
		foreach (NodePath path in Targets)
		{
			if (GetNodeOrNull(path) is Node target && target.HasMethod(TargetMethod))
				target.Call(TargetMethod);
		}
	}
}
