using Godot;

namespace Metroidvania.Shared;

public partial class Stats : Node
{
	[Export] public int MaxHealth = 100;
	[Export] public int AttackPower = 10;
	[Export] public int Defense = 0;
	[Export] public int MaxStamina = 100;
	[Export] public int StaminaRegenPerSecond = 20;
	[Export] public float InvulnerabilityDuration = 0.15f;

	public int CurrentHealth { get; private set; }
	public int CurrentStamina { get; private set; }
	public bool IsInvulnerable => _invulnerableTimer > 0f;

	private float _staminaAccumulator;
	private float _invulnerableTimer;

	[Signal] public delegate void HealthChangedEventHandler(int current, int max);
	[Signal] public delegate void StaminaChangedEventHandler(int current, int max);
	[Signal] public delegate void DiedEventHandler();
	[Signal] public delegate void HitTakenEventHandler();

	public override void _Ready()
	{
		CurrentHealth = MaxHealth;
		CurrentStamina = MaxStamina;
	}

	public override void _Process(double delta)
	{
		if (_invulnerableTimer > 0f)
			_invulnerableTimer -= (float)delta;

		if (CurrentStamina < MaxStamina)
		{
			_staminaAccumulator += StaminaRegenPerSecond * (float)delta;
			int wholeUnits = (int)_staminaAccumulator;
			if (wholeUnits > 0)
			{
				_staminaAccumulator -= wholeUnits;
				CurrentStamina = Mathf.Min(MaxStamina, CurrentStamina + wholeUnits);
				EmitSignal(SignalName.StaminaChanged, CurrentStamina, MaxStamina);
			}
		}
	}

	public void TakeDamage(int incomingAttack)
	{
		if (IsInvulnerable)
			return;

		int damage = Mathf.Max(1, incomingAttack - Defense);
		CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
		_invulnerableTimer = InvulnerabilityDuration;
		EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
		EmitSignal(SignalName.HitTaken);

		if (CurrentHealth <= 0)
			EmitSignal(SignalName.Died);
	}

	public void Kill()
	{
		if (CurrentHealth <= 0)
			return;

		CurrentHealth = 0;
		EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
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

	public void ApplyBonus(int health, int stamina, int attack, int defense)
	{
		MaxHealth += health;
		MaxStamina += stamina;
		AttackPower += attack;
		Defense += defense;

		CurrentHealth = Mathf.Clamp(CurrentHealth + health, 0, MaxHealth);
		CurrentStamina = Mathf.Clamp(CurrentStamina + stamina, 0, MaxStamina);

		EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
		EmitSignal(SignalName.StaminaChanged, CurrentStamina, MaxStamina);
	}
}
