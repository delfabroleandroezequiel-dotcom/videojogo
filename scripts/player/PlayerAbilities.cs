using System.Collections.Generic;
using Godot;
using Metroidvania.Save;

namespace Metroidvania.Player;

public partial class PlayerAbilities : Node
{
	public const string DoubleJump = "double_jump";
	public const string Dash = "dash";
	public const string Roll = "roll";
	public const string Spell = "spell";
	public const string Spell2 = "spell2";
	public const string Sprint = "sprint";
	public const string WallClimb = "wall_climb";
	public const string RunThrust = "run_thrust";
	public const string Block = "block";
	public const string ChargedAttack = "charged_attack";
	public const string PoundAttack = "pound_attack";
	public const string UpAttack = "up_attack";
	public const string LedgeGrab = "ledge_grab";
	// Maestro de Espadas: ComboFinisher unlocks the combo's 3rd hit, ComboFourth the 4th (needs the
	// 3rd), FlyingSword the thrown boomerang sword (G). PoundAttack (dive) and RunThrust (Ctrl lunge)
	// above belong to it too.
	public const string ComboFinisher = "combo_finisher";
	public const string ComboFourth = "combo_fourth";
	public const string FlyingSword = "flying_sword";
	public const string RopeSwing = "rope_swing";
	public const string Swim = "swim";
	public const string Bow = "bow";
	public const string Torch = "torch";
	// Elemental spells, parked 2026-10-13: they made the player too strong at range and didn't fit
	// the Shadow-centred kit. Code kept so a relic/consumable can grant them later.
	public const string TornadoSpell = "tornado_spell";
	public const string PoisonSpell = "poison_spell";
	public const string FireballSpell = "fireball_spell";
	public const string WindShield = "wind_shield";

	[Export] public bool StartWithDoubleJump;
	[Export] public bool StartWithDash;
	[Export] public bool StartWithRoll;
	[Export] public bool StartWithSpell;
	[Export] public bool StartWithSpell2;
	[Export] public bool StartWithSprint;
	[Export] public bool StartWithWallClimb;
	[Export] public bool StartWithRunThrust;
	[Export] public bool StartWithBlock;
	[Export] public bool StartWithChargedAttack;
	[Export] public bool StartWithPoundAttack;
	[Export] public bool StartWithUpAttack;
	[Export] public bool StartWithLedgeGrab;
	[Export] public bool StartWithComboFinisher;
	[Export] public bool StartWithRopeSwing;
	[Export] public bool StartWithSwim;
	[Export] public bool StartWithBow;
	[Export] public bool StartWithTorch;
	// Start unlocked for now, like everything above is in Player.tscn, until the skill tree grants
	// them — untick on the Abilities node to test them locked.
	[Export] public bool StartWithComboFourth = true;
	[Export] public bool StartWithFlyingSword = true;
	[Export] public bool StartWithTornadoSpell;
	[Export] public bool StartWithPoisonSpell;
	[Export] public bool StartWithFireballSpell;
	[Export] public bool StartWithWindShield;

	private readonly HashSet<string> _unlocked = new();

	[Signal] public delegate void AbilityUnlockedEventHandler(string abilityId);

	public override void _Ready()
	{
		if (StartWithDoubleJump)
			Unlock(DoubleJump);
		if (StartWithDash)
			Unlock(Dash);
		if (StartWithRoll)
			Unlock(Roll);
		if (StartWithSpell)
			Unlock(Spell);
		if (StartWithSpell2)
			Unlock(Spell2);
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
		if (StartWithLedgeGrab)
			Unlock(LedgeGrab);
		if (StartWithComboFinisher)
			Unlock(ComboFinisher);
		if (StartWithRopeSwing)
			Unlock(RopeSwing);
		if (StartWithSwim)
			Unlock(Swim);
		if (StartWithBow)
			Unlock(Bow);
		if (StartWithComboFourth)
			Unlock(ComboFourth);
		if (StartWithFlyingSword)
			Unlock(FlyingSword);
		if (StartWithTorch)
			Unlock(Torch);
		if (StartWithTornadoSpell)
			Unlock(TornadoSpell);
		if (StartWithPoisonSpell)
			Unlock(PoisonSpell);
		if (StartWithFireballSpell)
			Unlock(FireballSpell);
		if (StartWithWindShield)
			Unlock(WindShield);

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
