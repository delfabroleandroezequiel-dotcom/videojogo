using Godot;
using Metroidvania.Save;
using Metroidvania.Shared;

namespace Metroidvania.Player;

public partial class HealFlask : Node
{
	[Export] public int HealAmount = 40;

	public int MaxCharges { get; private set; }
	public int CurrentCharges { get; private set; }

	[Signal] public delegate void ChargesChangedEventHandler(int current, int max);

	public override void _Ready()
	{
		MaxCharges = SaveManager.Instance.GetMaxHealCharges();
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

	public void UnlockCharge()
	{
		if (!SaveManager.Instance.UnlockHealCharge())
			return;

		MaxCharges = SaveManager.Instance.GetMaxHealCharges();
		CurrentCharges = MaxCharges;
		EmitSignal(SignalName.ChargesChanged, CurrentCharges, MaxCharges);
	}
}
