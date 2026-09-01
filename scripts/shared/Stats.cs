using Godot;

namespace Metroidvania.Shared;

public partial class Stats : Node
{
	[Export] public int MaxHealth = 100;
	[Export] public int AttackPower = 10;
	[Export] public int Defense = 0;
	[Export] public int MaxStamina = 100;
	[Export] public int StaminaRegenPerSecond = 20;
	// Souls-style pause: real stamina recovery in Dark Souls/Elden Ring never starts the instant you
	// stop spending, there's a beat (~0.7s in DS3, measured) where you're still "winded" first. Ours
	// used to regen every frame with no pause at all, which is the single biggest reason it didn't
	// feel like theirs — you could roll, wait one frame, roll again with barely any real cost.
	[Export] public float StaminaRegenDelay = 0.7f;
	// Elden Ring specifically punishes fully emptying the bar (not just spending some of it) with a
	// longer, harsher recovery pause than a partial spend gets — used instead of StaminaRegenDelay
	// only when a spend brings CurrentStamina to exactly 0.
	[Export] public float StaminaExhaustedRegenDelay = 1.5f;
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

	// Set by a controller while it wants regen slowed without fully pausing it — Dark Souls reduces
	// (not stops) stamina recovery by 80% while guarding, so the player sets this to 0.2f while
	// blocking and back to 1f otherwise. Left at 1f (full rate) by anything that doesn't care.
	public float RegenRateMultiplier { get; set; } = 1f;

	private float _staminaAccumulator;
	private float _staminaRegenDelayTimer;
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

		if (_staminaRegenDelayTimer > 0f)
		{
			_staminaRegenDelayTimer -= (float)delta;
			return;
		}

		if (CurrentStamina < MaxStamina)
		{
			_staminaAccumulator += StaminaRegenPerSecond * RegenRateMultiplier * (float)delta;
			int wholeUnits = (int)_staminaAccumulator;
			if (wholeUnits > 0)
			{
				_staminaAccumulator -= wholeUnits;
				CurrentStamina = Mathf.Min(MaxStamina, CurrentStamina + wholeUnits);
				EmitSignal(SignalName.StaminaChanged, CurrentStamina, MaxStamina);
			}
		}
	}

	// Restarts the recovery pause — call after any successful spend. Emptying the bar completely
	// gets the longer Elden-Ring-style pause instead of the normal one.
	private void ArmStaminaRegenDelay()
	{
		_staminaRegenDelayTimer = CurrentStamina <= 0 ? StaminaExhaustedRegenDelay : StaminaRegenDelay;
		_staminaAccumulator = 0f;
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
		ArmStaminaRegenDelay();
		EmitSignal(SignalName.StaminaChanged, CurrentStamina, MaxStamina);
		return true;
	}

	public void SpendStaminaClamped(int amount)
	{
		int actual = Mathf.Min(amount, CurrentStamina);
		if (actual <= 0)
			return;

		CurrentStamina -= actual;
		ArmStaminaRegenDelay();
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
