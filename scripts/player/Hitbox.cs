using Godot;
using Metroidvania.Shared;
using Metroidvania.World;

namespace Metroidvania.Player;

public partial class Hitbox : Area2D
{
	[Export] public float KnockbackForce = 250f;

	[Signal] public delegate void HitDealtEventHandler();

	private CollisionShape2D _shape;
	private Stats _attackerStats;
	private string _impactFramesPath;
	private DamageElement _element;
	private bool _ignoreTargetInvulnerability;
	private bool _ignorePostHitInvulnerability;
	private Color? _impactModulate;
	private System.Action<Vector2> _customImpactEffect;
	private ulong _suppressedUntilMsec;
	private float _damageMultiplier = 1f;

	// Raised last for every body this hitbox damages (after knockback), for owners that need to act
	// on the target itself — e.g. the charged draw launching what it hit.
	public event System.Action<Node2D> HitTarget;

	public override void _Ready()
	{
		_shape = GetNode<CollisionShape2D>("CollisionShape2D");
		BodyEntered += OnBodyEntered;
	}

	// ignoreTargetInvulnerability is for deliberate multi-hit moves (e.g. Player's triple-hit
	// thrust) whose own consecutive pulses would otherwise be swallowed by the i-frames their
	// first pulse just armed on the same target. Every other caller leaves this false and gets
	// today's normal single-hit-per-i-frame-window behavior. impactModulate lets a caller with its
	// own custom impactFramesPath also tint/darken that spark (e.g. SpiderBossArena) — left null,
	// the spark keeps whatever colors its source frames already have. customImpactEffect lets a
	// caller replace the sprite-based impact spark entirely with its own VFX (e.g. Player's combo
	// uses it for HitImpactBurst) without changing this generic default for every other caller.
	// ignorePostHitInvulnerability: pierce the target's short post-hit i-frames but still respect
	// ExternalInvulnerable (dash/roll i-frames) — for multi-hit combos meant to land in full on a
	// player who got caught, while a well-timed dash still dodges each hit (MushroomBoss's dance).
	// damageMultiplier scales the attacker's AttackPower for this activation only (the player's
	// charged draw); every other caller leaves it at 1.
	public void Activate(Stats attackerStats, string impactFramesPath = null, DamageElement element = DamageElement.Normal, bool ignoreTargetInvulnerability = false, Color? impactModulate = null, System.Action<Vector2> customImpactEffect = null, bool ignorePostHitInvulnerability = false, float damageMultiplier = 1f)
	{
		if (Time.GetTicksMsec() < _suppressedUntilMsec)
			return;
		_attackerStats = attackerStats;
		_damageMultiplier = damageMultiplier;
		_ignorePostHitInvulnerability = ignorePostHitInvulnerability;
		_impactFramesPath = impactFramesPath;
		_element = element;
		_ignoreTargetInvulnerability = ignoreTargetInvulnerability;
		_impactModulate = impactModulate;
		_customImpactEffect = customImpactEffect;
		_shape.Disabled = false;
	}

	public void Deactivate() => _shape.Disabled = true;

	// Kills the hitbox now and turns Activate into a no-op for `seconds` — for a swing that got
	// interrupted (MeleeEnemy flinch) while its attack coroutine is still running on its own timers.
	public void Suppress(float seconds)
	{
		// Deferred: Suppress runs from inside another hitbox's BodyEntered (a hit interrupting this
		// swing), where Godot refuses to toggle a shape directly.
		_shape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
		_suppressedUntilMsec = Time.GetTicksMsec() + (ulong)(seconds * 1000f);
	}

	// Poise damage each hit deals to an Enemy (see Enemy.TakePoiseDamage). Read at the moment of the
	// hit, so the owner can make it depend on which move is out (Player sets it per attack). Unset =
	// no poise damage, which is every enemy-owned hitbox.
	public System.Func<float> PoiseDamage;

	// Fired after HitDealt when this hit broke the target's poise.
	[Signal] public delegate void PoiseBrokenEventHandler();

	// Fired after HitDealt when this hit took the target to 0 HP (a multi-life boss held at
	// MinHealth never fires it). LastImpactPoint is where it landed.
	[Signal] public delegate void KilledTargetEventHandler();
	public Vector2 LastImpactPoint { get; private set; }

	public bool IsActive => _shape is { Disabled: false };

	private void OnBodyEntered(Node2D body)
	{
		Stats targetStats = body.GetNodeOrNull<Stats>("Stats");
		if (targetStats is null || targetStats == _attackerStats)
			return;
		if (_ignorePostHitInvulnerability && targetStats.ExternalInvulnerable)
			return;
		bool pierce = _ignoreTargetInvulnerability || _ignorePostHitInvulnerability;
		if (targetStats.IsInvulnerable && !pierce)
			return;

		targetStats.TakeDamage(Mathf.RoundToInt(_attackerStats.AttackPower * _damageMultiplier), element: _element, ignoreInvulnerability: pierce);
		Vector2 impactPoint = (GlobalPosition + body.GlobalPosition) / 2f;
		LastImpactPoint = impactPoint;
		BloodEffect.SpawnAt(this, body.GlobalPosition);
		if (_customImpactEffect is not null)
		{
			_customImpactEffect(impactPoint);
		}
		// No generic star spark on the player getting hit (user call: it read as clutter) — only on
		// what the player hits. A custom effect or impact sheet passed by the attacker still shows.
		else if (_impactFramesPath is not null || !body.IsInGroup("player"))
		{
			string framesPath = _impactFramesPath ?? "res://resources/sprites/HitSparkSpriteFrames.tres";
			string animation = _impactFramesPath is null ? "spark" : "impact";
			VfxSpawner.SpawnAt(this, impactPoint, framesPath, animation, new Vector2(0f, 10f), modulate: _impactModulate);
		}
		EmitSignal(SignalName.HitDealt);
		if (targetStats.CurrentHealth <= 0)
			EmitSignal(SignalName.KilledTarget);
		else if (PoiseDamage is not null && body is Enemy enemy && enemy.TakePoiseDamage(PoiseDamage()))
			EmitSignal(SignalName.PoiseBroken);

		if (body.HasMethod("ApplyKnockback"))
		{
			Vector2 knockDirection = (body.GlobalPosition - GlobalPosition);
			knockDirection = knockDirection == Vector2.Zero ? Vector2.Right : knockDirection.Normalized();
			body.Call("ApplyKnockback", knockDirection, KnockbackForce);
		}
		HitTarget?.Invoke(body);
	}
}
