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

	// DamageElement.Normal means "no weakness" — every attacker's hits are typed (Normal by
	// default), so a Normal weakness would otherwise match every single hit.
	[Export] public DamageElement Weakness = DamageElement.Normal;
	[Export] public float WeaknessDamageMultiplier = 1.5f;

	public int CurrentHealth { get; private set; }
	public int CurrentStamina { get; private set; }
	public bool ExternalInvulnerable { get; set; }
	public bool IsInvulnerable => _invulnerableTimer > 0f || ExternalInvulnerable;

	// Lets a controller (e.g. the player's block/parry state) veto an incoming hit before
	// health/stamina are touched. Returning true fully negates the hit for this call.
	public System.Func<bool> IncomingHitInterceptor { get; set; }

	private float _staminaAccumulator;
	private float _invulnerableTimer;

	[Signal] public delegate void HealthChangedEventHandler(int current, int max);
	[Signal] public delegate void StaminaChangedEventHandler(int current, int max);
	[Signal] public delegate void DiedEventHandler();
	[Signal] public delegate void HitTakenEventHandler(bool isProjectile);

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

	// ignoreInvulnerability lets a deliberate multi-hit move (see Player.ThrustTripleHit) land
	// its own consecutive pulses on the same target — normal invulnerability still gets
	// (re-)armed by this call same as any hit, it's just not used to block THIS hit.
	public void TakeDamage(int incomingAttack, bool isProjectile = false, DamageElement element = DamageElement.Normal, bool ignoreInvulnerability = false)
	{
		if (IsInvulnerable && !ignoreInvulnerability)
			return;

		if (IncomingHitInterceptor != null && IncomingHitInterceptor())
			return;

		float multiplier = element != DamageElement.Normal && element == Weakness ? WeaknessDamageMultiplier : 1f;
		int damage = Mathf.Max(1, Mathf.RoundToInt(incomingAttack * multiplier) - Defense);
		CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
		_invulnerableTimer = InvulnerabilityDuration;
		EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
		EmitSignal(SignalName.HitTaken, isProjectile);

		if (CurrentHealth <= 0)
			EmitSignal(SignalName.Died);
	}

	public void Heal(int amount)
	{
		if (CurrentHealth <= 0 || CurrentHealth >= MaxHealth)
			return;

		CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
		EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
	}

	public void SetCurrentHealth(int value)
	{
		CurrentHealth = Mathf.Clamp(value, 0, MaxHealth);
		EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
	}

	// Call after changing MaxHealth/MaxStamina at runtime (e.g. applying an EnemyProfile)
	// so Current* doesn't keep pointing at whatever the old max used to be.
	public void ResetToFull()
	{
		CurrentHealth = MaxHealth;
		CurrentStamina = MaxStamina;
		EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
		EmitSignal(SignalName.StaminaChanged, CurrentStamina, MaxStamina);
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

	public void SpendStaminaClamped(int amount)
	{
		int actual = Mathf.Min(amount, CurrentStamina);
		if (actual <= 0)
			return;

		CurrentStamina -= actual;
		EmitSignal(SignalName.StaminaChanged, CurrentStamina, MaxStamina);
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
