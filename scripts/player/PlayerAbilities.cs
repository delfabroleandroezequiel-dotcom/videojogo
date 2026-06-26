using System.Collections.Generic;
using Godot;

namespace Metroidvania.Player;

public partial class PlayerAbilities : Node
{
	public const string DoubleJump = "double_jump";
	public const string Dash = "dash";
	public const string Sprint = "sprint";

	[Export] public bool StartWithDoubleJump;
	[Export] public bool StartWithDash;
	[Export] public bool StartWithSprint;

	private readonly HashSet<string> _unlocked = new();

	[Signal] public delegate void AbilityUnlockedEventHandler(string abilityId);

	public override void _Ready()
	{
		if (StartWithDoubleJump)
			Unlock(DoubleJump);
		if (StartWithDash)
			Unlock(Dash);
		if (StartWithSprint)
			Unlock(Sprint);
	}

	public void Unlock(string abilityId)
	{
		if (_unlocked.Add(abilityId))
			EmitSignal(SignalName.AbilityUnlocked, abilityId);
	}

	public bool Has(string abilityId) => _unlocked.Contains(abilityId);

	public IReadOnlyCollection<string> GetUnlocked() => _unlocked;
}
