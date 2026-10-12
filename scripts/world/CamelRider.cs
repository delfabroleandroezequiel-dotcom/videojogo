using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Jinete de camello / Camel Rider (Desert enemy pack, 224x119 cells, faces LEFT — mirrored on the
// scene). The desert's elite: a lancer on camelback with enormous reach in front, but a mount that
// turns slowly — its back is the opening, and it knows it.
// Sheet beats used:
//   front_attack (12fps): 1-5 the lance drawn back (the wind-up), 6 the camel lunging into a long,
//     low thrust (white streak), 7-10 the lance held out, 11-15 pulled back, 16-21 recovery.
//   back_attack (12fps): 2-5 the rider standing in the saddle, twirling the lance overhead, 6 a huge
//     sweep around the back, 7-13 coming down already turned the other way.
//   turn_right: the mount turning around (starts facing its front, ends facing the other way).
//   walk: the camel's stride (played fast for the charge).
// Moves:
//   Lance thrust — a long, low thrust out to ~120px. Jump it or stay out of reach.
//   Slow turn — it can't just flip: when you get behind it, the camel takes a moment to turn
//     around (turn_right) — that's your window.
//   Rear sweep — linger behind it and the rider stands up and sweeps the lance all the way
//     around, ending turned toward you.
//   Charge — from long range it gallops at you with the lance lowered, through to the other side,
//     then turns around. Stops at walls and ledges.
//   Heavy — never flinches, shoves you hard.
// Telegraphs: body flashes white before every attack.
public partial class CamelRider : Enemy
{
	[ExportGroup("Lance thrust")]
	[Export] public float ThrustRange = 115f;
	[Export] public float AttackCooldown = 1.8f;
	[Export] public float ThrustTelegraph = 0.35f;
	[Export] public float ThrustDamageMultiplier = 1.3f;
	[Export] public float ThrustKnockback = 320f;
	[Export] public float ThrustLunge = 30f;
	// Measured off front_attack frames 6-8 (scale 1.5 mirrored, sprite at 4.5,-43): the lance reaches
	// ~120px in front, low — from the rider's knee down to the floor.
	[Export] public Vector2 ThrustHitboxCenter = new(78f, 24f);
	[Export] public Vector2 ThrustHitboxSize = new(96f, 46f);

	[ExportGroup("Turning")]
	[Export] public float TurnDelay = 0.35f;
	[Export] public float RearSweepDelay = 0.6f;

	[ExportGroup("Rear sweep")]
	[Export] public float SweepDamageMultiplier = 1.15f;
	[Export] public float SweepKnockback = 340f;
	// back_attack frame 6: the arc reaches ~94px behind and ~36px in front, chest to knee height.
	[Export] public Vector2 SweepHitboxCenter = new(-30f, -6f);
	[Export] public Vector2 SweepHitboxSize = new(136f, 80f);

	[ExportGroup("Charge")]
	[Export] public float ChargeMinDistance = 200f;
	[Export] public float ChargeChance = 0.4f;
	[Export] public float ChargeCooldown = 6f;
	[Export] public float ChargeTelegraph = 0.55f;
	[Export] public float ChargeSpeed = 330f;
	[Export] public float ChargeMaxTime = 1.8f;
	[Export] public float ChargeOvershoot = 110f;
	[Export] public float ChargeDamageMultiplier = 1.1f;
	[Export] public float ChargeKnockback = 420f;

	[ExportGroup("Look")]
	[Export] public Color FlashTint = new(2.2f, 2.2f, 2.2f, 1f);
	[Export] public Color TrailGlowColor = new(1f, 0.8f, 0.45f, 1f);
	[Export] public float DecisionInterval = 0.4f;

	private const float Fps = 12f;

	private static readonly RandomNumberGenerator Rng = new();

	private EnemySlashTrail _trail;
	private Tween _flashTween;
	private bool _busy;
	private float _moveOverride;
	private float _behindTimer;
	private float _decisionTimer;
	private float _attackCooldownTimer;
	private float _chargeCooldownTimer;

	static CamelRider()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_attackCooldownTimer = Rng.RandfRange(0.5f, 1f);
		_chargeCooldownTimer = Rng.RandfRange(1.5f, 3f);
		_trail = new EnemySlashTrail
		{
			CoreWidth = 3f,
			GlowWidth = 10f,
			GlowColor = TrailGlowColor,
			CoreColor = new Color(1f, 0.97f, 0.9f, 1f),
			MinPointSpacing = 1.5f,
			SparkAmount = 12,
		};
		Visual.AddChild(_trail);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;
		float dt = (float)delta;
		_decisionTimer -= dt;
		_attackCooldownTimer -= dt;
		_chargeCooldownTimer -= dt;
		var player = PlayerReads.Find(this);
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval || player is null || !PlayerDetected || _busy || !IsOnFloor())
			return;

		float distanceX = player.GlobalPosition.X - GlobalPosition.X;
		float absDistance = Mathf.Abs(distanceX);
		bool behind = (distanceX >= 0) != FacingRight;

		if (behind)
		{
			_behindTimer += dt;
			// Close behind it for a while → the rear sweep; otherwise it turns around (slowly).
			if (absDistance < 110f && _behindTimer >= RearSweepDelay && _attackCooldownTimer <= AttackCooldown * 0.5f)
				_ = RearSweep();
			else if (_behindTimer >= TurnDelay && absDistance >= 110f)
				_ = TurnAround();
			return;
		}
		_behindTimer = 0f;

		if (absDistance <= ThrustRange && _attackCooldownTimer <= 0f)
		{
			_ = Thrust();
			return;
		}
		if (_decisionTimer > 0f || _attackCooldownTimer > AttackCooldown * 0.5f)
			return;
		_decisionTimer = DecisionInterval;
		if (absDistance >= ChargeMinDistance && _chargeCooldownTimer <= 0f && Rng.Randf() < ChargeChance)
			_ = Charge(Mathf.Sign(distanceX), player);
	}

	// Walks up to lance range; only ever walks forward (turning is its own action).
	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_busy)
			return _moveOverride == 0f || CanStepTo(Mathf.Sign(_moveOverride)) ? _moveOverride : 0f;
		float toward = Mathf.Sign(distanceX);
		bool ahead = (distanceX >= 0) == FacingRight;
		if (ahead && Mathf.Abs(distanceX) > ThrustRange * 0.85f && CanStepTo(toward))
			return toward * MoveSpeed;
		return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
	}

	// Never flips on its own: TurnAround / the rear sweep handle facing.
	protected override bool CanTurnToFacePlayer => false;

	// ── Turning ─────────────────────────────────────────────────────────────────────────────

	private async Task TurnAround()
	{
		_busy = true;
		_behindTimer = 0f;
		try
		{
			Sprite.Play("turn_right");
			if (!await Wait(6f / Fps))
				return;
			Flip();
			Sprite.Play("idle");
		}
		finally
		{
			if (IsInstanceValid(this))
				_busy = false;
		}
	}

	private void Flip()
	{
		FacingRight = !FacingRight;
		Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);
	}

	// ── Lance thrust ────────────────────────────────────────────────────────────────────────

	private async Task Thrust()
	{
		if (!Begin())
			return;
		try
		{
			Sprite.Play("front_attack");
			Sprite.Pause();
			for (int f = 1; f <= 4; f++)
			{
				Sprite.Frame = f;
				if (!await Wait(1f / Fps))
					return;
			}
			Sprite.Frame = 5;
			FlashBody(ThrustTelegraph);
			if (!await Wait(ThrustTelegraph))
				return;
			// The camel lunges into the thrust.
			Sprite.Frame = 6;
			_moveOverride = Forward() * ThrustLunge * Fps;
			_trail.PlayThrust(new Vector2(10f, 6f), new Vector2(ThrustHitboxCenter.X + ThrustHitboxSize.X * 0.5f, ThrustHitboxCenter.Y + 10f), 0.06f, 0.12f);
			if (!await Wait(1f / Fps))
				return;
			_moveOverride = 0f;
			bool hit = Strike(ThrustHitboxCenter, ThrustHitboxSize, ThrustDamageMultiplier, ThrustKnockback, Forward());
			for (int f = 7; f <= 21; f++)
			{
				Sprite.Frame = f;
				// The lance stays out (and dangerous) through 7-10.
				if (!hit && f <= 10)
					hit = Strike(ThrustHitboxCenter, ThrustHitboxSize, ThrustDamageMultiplier, ThrustKnockback, Forward());
				if (!await Wait(1f / Fps))
					return;
			}
		}
		finally
		{
			if (IsInstanceValid(this))
				_moveOverride = 0f;
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * Rng.RandfRange(0.85f, 1.2f);
	}

	// ── Rear sweep ──────────────────────────────────────────────────────────────────────────

	private async Task RearSweep()
	{
		if (!Begin(facePlayer: false))
			return;
		_behindTimer = 0f;
		try
		{
			Sprite.Play("back_attack");
			Sprite.Pause();
			for (int f = 1; f <= 4; f++)
			{
				Sprite.Frame = f;
				if (!await Wait(1f / Fps))
					return;
			}
			Sprite.Frame = 5;
			FlashBody(0.3f);
			if (!await Wait(0.3f))
				return;
			Sprite.Frame = 6;
			_trail.Play(new Vector2(-10f, -20f), 70f, 200f, 340f, 0.1f, 40f);
			Strike(SweepHitboxCenter, SweepHitboxSize, SweepDamageMultiplier, SweepKnockback, -Forward());
			for (int f = 7; f <= 13; f++)
			{
				if (!await Wait(1f / Fps))
					return;
				Sprite.Frame = f;
			}
			// It came down turned the other way.
			Flip();
			Sprite.Play("idle");
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * 0.8f;
	}

	// ── Charge ──────────────────────────────────────────────────────────────────────────────

	private async Task Charge(float direction, Metroidvania.Player.Player player)
	{
		if (!Begin())
			return;
		_chargeCooldownTimer = ChargeCooldown;
		try
		{
			// Lance lowered, pawing — the warning.
			Sprite.Play("front_attack");
			Sprite.Pause();
			Sprite.Frame = 9;
			FlashBody(ChargeTelegraph);
			if (!await Wait(ChargeTelegraph))
				return;

			Sprite.Play("walk");
			Sprite.SpeedScale = 2.5f;
			float stopX = player.GlobalPosition.X + direction * ChargeOvershoot;
			_moveOverride = direction * ChargeSpeed;
			bool hit = false;
			float t = 0f;
			while (t < ChargeMaxTime)
			{
				await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
				if (!IsInstanceValid(this) || IsQueuedForRemoval)
					return;
				t += (float)GetPhysicsProcessDeltaTime();
				if (!hit)
					hit = Strike(new Vector2(46f, 10f), new Vector2(50f, 70f), ChargeDamageMultiplier, ChargeKnockback, direction);
				if ((GlobalPosition.X - stopX) * direction >= 0f || TestMove(GlobalTransform, new Vector2(direction * 8f, 0f))
					|| !HasGroundAhead(direction))
					break;
			}
			_moveOverride = 0f;
			Sprite.SpeedScale = 1f;
			Sprite.Play("idle");
			Shake(1.5f, 0.12f);
			if (!await Wait(0.35f))
				return;
		}
		finally
		{
			if (IsInstanceValid(this))
			{
				_moveOverride = 0f;
				Sprite.SpeedScale = 1f;
			}
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * 0.5f;
	}

	// ── Shared ──────────────────────────────────────────────────────────────────────────────

	private float Forward() => FacingRight ? 1f : -1f;

	private bool Begin(bool facePlayer = true)
	{
		if (!EnemyCombatCoordinator.TryAcquireAttackSlot())
		{
			_attackCooldownTimer = 0.3f;
			return false;
		}
		HoldingAttackSlot = true;
		_busy = true;
		return true;
	}

	private void Finish()
	{
		if (!IsInstanceValid(this))
			return;
		_busy = false;
		ClearFlash();
		if (HoldingAttackSlot)
		{
			EnemyCombatCoordinator.ReleaseAttackSlot();
			HoldingAttackSlot = false;
		}
	}

	// One damage check; `push` is the direction the player gets shoved.
	private bool Strike(Vector2 center, Vector2 size, float damageMultiplier, float knockback, float push)
	{
		Vector2 at = GlobalPosition + new Vector2(FacingRight ? center.X : -center.X, center.Y);
		var query = new PhysicsShapeQueryParameters2D
		{
			Shape = new RectangleShape2D { Size = size },
			Transform = new Transform2D(0f, at),
			CollisionMask = 2,
			CollideWithBodies = true,
		};
		foreach (var hit in GetWorld2D().DirectSpaceState.IntersectShape(query, 8))
		{
			if (hit["collider"].AsGodotObject() is not Metroidvania.Player.Player player)
				continue;
			if (player.GetNodeOrNull<Stats>("Stats") is not { } stats || stats.IsInvulnerable)
				continue;
			int before = stats.CurrentHealth;
			stats.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(Stats.AttackPower * damageMultiplier)));
			if (stats.CurrentHealth < before)
			{
				player.ApplyKnockback(new Vector2(push, -0.35f).Normalized(), knockback);
				BloodEffect.SpawnAt(this, player.GlobalPosition);
			}
			return true;
		}
		return false;
	}

	private async Task<bool> Wait(float seconds)
	{
		await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
		return IsInstanceValid(this) && !IsQueuedForRemoval;
	}

	private void Shake(float strength, float duration)
	{
		if (PlayerReads.Find(this) is { } player && player.GlobalPosition.DistanceTo(GlobalPosition) < 700f)
			player.ShakeCamera(strength, duration);
	}

	private void FlashBody(float duration)
	{
		if (Sprite is null || duration <= 0f)
			return;
		_flashTween?.Kill();
		_flashTween = CreateTween();
		float half = duration / 4f;
		for (int i = 0; i < 2; i++)
		{
			_flashTween.TweenProperty(Sprite, "self_modulate", FlashTint, half);
			_flashTween.TweenProperty(Sprite, "self_modulate", Colors.White, half);
		}
	}

	private void ClearFlash()
	{
		_flashTween?.Kill();
		if (Sprite is not null)
			Sprite.SelfModulate = Colors.White;
	}

	protected override void OnDefeated()
	{
		ClearFlash();
		if (Sprite is not null)
			Sprite.SpeedScale = 1f;
		base.OnDefeated();
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null || _busy)
			return;
		string anim = Mathf.Abs(velocity.X) > 5f ? "walk" : "idle";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	protected override bool ContactDamageEnabled => false;
}
