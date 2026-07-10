using System.Collections.Generic;
using Godot;
using Metroidvania.Save;

namespace Metroidvania.Player;

public partial class PlayerAbilities : Node
{
	public const string DoubleJump = "double_jump";
	public const string Dash = "dash";
	public const string Sprint = "sprint";
	public const string WallClimb = "wall_climb";
	public const string RunThrust = "run_thrust";
	public const string Block = "block";
	public const string ChargedAttack = "charged_attack";
	public const string PoundAttack = "pound_attack";
	public const string UpAttack = "up_attack";

	[Export] public bool StartWithDoubleJump;
	[Export] public bool StartWithDash;
	[Export] public bool StartWithSprint;
	[Export] public bool StartWithWallClimb;
	[Export] public bool StartWithRunThrust;
	[Export] public bool StartWithBlock;
	[Export] public bool StartWithChargedAttack;
	[Export] public bool StartWithPoundAttack;
	[Export] public bool StartWithUpAttack;

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
		if (StartWithWallClimb)
			Unlock(WallClimb);
		if (StartWithRunThrust)
			Unlock(RunThrust);
		if (StartWithBlock)
			Unlock(Block);
		if (StartWithChargedAttack)
			Unlock(ChargedAttack);
		if (StartWithPoundAttack)
			Unlock(PoundAttack);
		if (StartWithUpAttack)
			Unlock(UpAttack);

		foreach (string abilityId in SaveManager.Instance.GetUnlockedAbilities())
			Unlock(abilityId);
	}

	public void Unlock(string abilityId)
	{
		if (_unlocked.Add(abilityId))
		{
			SaveManager.Instance.UnlockAbility(abilityId);
			EmitSignal(SignalName.AbilityUnlocked, abilityId);
		}
	}

	public bool Has(string abilityId) => _unlocked.Contains(abilityId);

	public IReadOnlyCollection<string> GetUnlocked() => _unlocked;
}
