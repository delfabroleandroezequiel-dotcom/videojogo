using Godot;
using Metroidvania.Save;
using Metroidvania.Shared;

namespace Metroidvania.Player;

public partial class HealFlask : Node
{
	[Export] public int HealAmount = 40;

	// Test-scene-only escape hatch (e.g. SpiderBossArena_TestRoom) — 0 keeps the normal
	// progression-driven charge count from SaveManager untouched everywhere else in the game.
	[Export] public int DebugForceMaxCharges = 0;

	public int MaxCharges { get; private set; }
	public int CurrentCharges { get; private set; }

	[Signal] public delegate void ChargesChangedEventHandler(int current, int max);

	public override void _Ready()
	{
		MaxCharges = DebugForceMaxCharges > 0 ? DebugForceMaxCharges : SaveManager.Instance.GetMaxHealCharges();
		CurrentCharges = MaxCharges;
	}

	public bool TryUse(Stats stats)
	{
		if (CurrentCharges <= 0 || stats.CurrentHealth >= stats.MaxHealth)
			return false;

		CurrentCharges--;
		stats.Heal(HealAmount);
		EmitSignal(SignalName.ChargesChanged, CurrentCharges, MaxCharges);
		return true;
	}

	public void SetCurrentCharges(int value)
	{
		CurrentCharges = Mathf.Clamp(value, 0, MaxCharges);
		EmitSignal(SignalName.ChargesChanged, CurrentCharges, MaxCharges);
	}

	public void UnlockCharge()
	{
		if (!SaveManager.Instance.UnlockHealCharge())
			return;

		MaxCharges = SaveManager.Instance.GetMaxHealCharges();
		CurrentCharges = MaxCharges;
		EmitSignal(SignalName.ChargesChanged, CurrentCharges, MaxCharges);
	}
}
