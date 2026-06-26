using Godot;

namespace Metroidvania.Shared;

public partial class Stats : Node
{
	[Export] public int MaxHealth = 100;
	[Export] public int AttackPower = 10;
	[Export] public int Defense = 0;
	[Export] public int MaxStamina = 100;
	[Export] public int StaminaRegenPerSecond = 20;

	public int CurrentHealth { get; private set; }
	public int CurrentStamina { get; private set; }

	[Signal] public delegate void HealthChangedEventHandler(int current, int max);
	[Signal] public delegate void StaminaChangedEventHandler(int current, int max);
	[Signal] public delegate void DiedEventHandler();

	public override void _Ready()
	{
		CurrentHealth = MaxHealth;
		CurrentStamina = MaxStamina;
	}

	public override void _Process(double delta)
	{
		if (CurrentStamina < MaxStamina)
		{
			CurrentStamina = Mathf.Min(MaxStamina, CurrentStamina + (int)(StaminaRegenPerSecond * delta));
			EmitSignal(SignalName.StaminaChanged, CurrentStamina, MaxStamina);
		}
	}

	public void TakeDamage(int incomingAttack)
	{
		int damage = Mathf.Max(1, incomingAttack - Defense);
		CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
		EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);

		if (CurrentHealth <= 0)
			EmitSignal(SignalName.Died);
	}

	public bool TrySpendStamina(int amount)
	{
		if (CurrentStamina < amount)
			return false;

		CurrentStamina -= amount;
		EmitSignal(SignalName.StaminaChanged, CurrentStamina, MaxStamina);
		return true;
	}
}
