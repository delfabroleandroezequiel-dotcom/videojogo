using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Diablilla / DemonKin (DuskBorne, 128x128 cells, faces right). A small horned demon girl dragging a
// hammer bigger than she is. Quick and pushy: she closes in fast and every swing comes in a pair.
// Sheet beats used (all one attack strip, 12 frames):
//   0-4 the hammer hauled back (the wind-up), 5-6 brought over her head, 7-8 buried in the floor in
//   front of her, 9-10 a low sweep that spins round behind her, 11 back to guard.
//   walk / idle / hurt (frame 0 is a white hit-flash) / death (burns away; last frame is empty).
// Moves:
//   Hammer slam — frames 0-8: wind-up, overhead, impact with a short ground burst in front.
//   Sweep — frames 9-10, always straight after the slam: a low spin that hits BOTH sides, so
//     rolling behind her to dodge the slam gets you caught. Then a short recovery (the opening).
//   Leap slam — from mid range she jumps in (frames 0-1 crouch, 2-4 in the air) and the slam +
//     sweep land where she lands.
// Telegraphs: green body blinks on the wind-up / crouch; red neon trail on the slam, pale violet
// on the sweep.
public partial class DemonKin : Enemy
{
	[ExportGroup("Hammer slam")]
	[Export] public float SlamTrigger = 52f;
	[Export] public float SlamWindup = 0.45f;
	[Export] public float SlamDamageMultiplier = 1.3f;
	[Export] public float AttackCooldown = 1.3f;
	// Frames 6-8: the hammer comes down ~25-45px in front, from head height to the floor.
	[Export] public Vector2 SlamHitboxCenter = new(28f, 4f);
	[Export] public Vector2 SlamHitboxSize = new(42f, 40f);
	// Where the hammer head meets the floor (frame 7), for the ground burst.
	[Export] public float ImpactOffsetX = 36f;
	[Export] public float ImpactScale = 0.45f;

	[ExportGroup("Sweep")]
	[Export] public float SweepDamageMultiplier = 0.8f;
	// Frames 9-10: a low ring ~30px either side of her, at ankle height.
	[Export] public Vector2 SweepHitboxCenter = new(0f, 13f);
	[Export] public Vector2 SweepHitboxSize = new(66f, 18f);
	[Export] public float RecoveryTime = 0.4f;

	[ExportGroup("Leap slam")]
	[Export] public float LeapMinDistance = 110f;
	[Export] public float LeapMaxDistance = 240f;
	[Export] public float LeapMaxHeightDiff = 40f;
	[Export] public float LeapCooldown = 3.5f;
	[Export] public float LeapCrouch = 0.35f;
	[Export] public float LeapApex = 48f;
	[Export] public float LeapMaxSpeedX = 280f;
	// Lands this far short of the player, so the slam reaches them.
	[Export] public float LeapLandShort = 26f;

	[ExportGroup("Look")]
	[Export] public Color FlashTint = new(1.4f, 2.6f, 1.5f, 1f);
	[Export] public Color SlamTrailColor = new(1f, 0.08f, 0.15f, 1f);
	[Export] public Color SweepTrailColor = new(0.75f, 0.55f, 1f, 1f);

	private static readonly RandomNumberGenerator Rng = new();
	private static readonly GroundSlamFx.Palette ImpactPalette = new()
	{
		Hot = new Color(1f, 0.15f, 0.2f, 1f),
		Core = new Color(1f, 0.85f, 0.8f, 1f),
		Rock = new Color(0.35f, 0.28f, 0.26f, 1f),
		Dust = new Color(0.55f, 0.45f, 0.42f, 0.6f),
		Light = new Color(1f, 0.25f, 0.25f, 1f),
	};

	private EnemySlashTrail _slamTrail;
	private EnemySlashTrail _sweepTrail;
	private Tween _flashTween;
	private bool _busy;
	private float _moveOverride;
	private float _attackCooldownTimer;
	private float _leapCooldownTimer;

	static DemonKin()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_attackCooldownTimer = Rng.RandfRange(0.3f, 0.7f);
		_leapCooldownTimer = Rng.RandfRange(0.5f, 1.5f);
		_slamTrail = new EnemySlashTrail
		{
			CoreWidth = 3f,
			GlowWidth = 10f,
			GlowColor = SlamTrailColor,
			CoreColor = new Color(1f, 0.7f, 0.7f, 1f),
			SparkAmount = 12,
		};
		_sweepTrail = new EnemySlashTrail
		{
			CoreWidth = 2f,
			GlowWidth = 7f,
			GlowColor = SweepTrailColor,
			CoreColor = new Color(0.95f, 0.9f, 1f, 1f),
			SparkAmount = 8,
		};
		Visual.AddChild(_slamTrail);
		Visual.AddChild(_sweepTrail);
		Stats.HitTaken += _ => OnHit();
	}

	private void OnHit()
	{
		if (!_busy)
			_ = Flinch();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;
		float dt = (float)delta;
		_attackCooldownTimer -= dt;
		_leapCooldownTimer -= dt;
		var player = PlayerReads.Find(this);
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval || player is null || !PlayerDetected || _busy || !IsOnFloor())
			return;

		float absDx = Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X);
		float absDy = Mathf.Abs(player.GlobalPosition.Y - GlobalPosition.Y);
		if (absDx <= SlamTrigger && absDy <= LeapMaxHeightDiff && _attackCooldownTimer <= 0f)
			_ = Slam(player, leap: false);
		else if (absDx >= LeapMinDistance && absDx <= LeapMaxDistance && absDy <= LeapMaxHeightDiff
			&& _leapCooldownTimer <= 0f && _attackCooldownTimer <= 0f)
			_ = Slam(player, leap: true);
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_busy)
			return _moveOverride == 0f || CanStepTo(Mathf.Sign(_moveOverride)) || !IsOnFloor() ? _moveOverride : 0f;
		return base.ComputeMoveX(player, distanceX, currentVelocityX, delta);
	}

	protected override bool CanTurnToFacePlayer => !_busy;

	// ── Slam + sweep (optionally leapt into) ─────────────────────────────────────────────────

	private async Task Slam(Metroidvania.Player.Player player, bool leap)
	{
		if (!Begin())
			return;
		if (leap)
			_leapCooldownTimer = LeapCooldown * Rng.RandfRange(0.85f, 1.2f);
		try
		{
			Sprite.Play("attack");
			Sprite.Pause();

			if (leap)
			{
				if (!await LeapIn(player))
					return;
			}
			else
			{
				FlashBody(SlamWindup);
				for (int f = 0; f <= 4; f++)
				{
					Sprite.Frame = f;
					if (!await Wait(SlamWindup / 5f))
						return;
				}
			}
			ClearFlash();

			// Overhead and down.
			Sfx.PlayAt(this, "Combat/Sword", "Sword Attack");
			_slamTrail.Play(new Vector2(4f, -2f), 36f, -150f, 35f, 0.11f);
			Sprite.Frame = 5;
			if (!await Wait(0.05f))
				return;
			Sprite.Frame = 6;
			bool hit = Strike(SlamHitboxCenter, SlamHitboxSize, SlamDamageMultiplier, 240f);
			if (!await Wait(0.05f))
				return;
			Sprite.Frame = 7;
			if (!hit)
				hit = Strike(SlamHitboxCenter, SlamHitboxSize, SlamDamageMultiplier, 240f);
			float forward = FacingRight ? 1f : -1f;
			Vector2 ground = GlobalPosition + new Vector2(forward * ImpactOffsetX, BodyBottom);
			GroundSlamFx.Impact(this, ground, ImpactScale, ImpactPalette);
			if (!await Wait(0.1f))
				return;
			Sprite.Frame = 8;
			if (!await Wait(0.1f))
				return;

			// The sweep round behind her.
			Sfx.PlayAt(this, "Combat/Sword", "Sword Attack");
			_sweepTrail.Play(new Vector2(0f, 12f), 34f, 10f, 330f, 0.14f, 7f);
			Sprite.Frame = 9;
			bool swept = Strike(SweepHitboxCenter, SweepHitboxSize, SweepDamageMultiplier, 200f);
			if (!await Wait(0.07f))
				return;
			Sprite.Frame = 10;
			if (!swept)
				Strike(SweepHitboxCenter, SweepHitboxSize, SweepDamageMultiplier, 200f);
			if (!await Wait(0.08f))
				return;

			// Recovery — the opening.
			Sprite.Frame = 11;
			if (!await Wait(RecoveryTime))
				return;
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * Rng.RandfRange(0.85f, 1.25f);
	}

	// Crouch (frames 0-1, flashing), jump to just short of the player (frames 2-4 spread over the
	// flight), back on the floor. Returns false if she died on the way. If the arc wouldn't land on
	// ground she just winds up in place instead.
	private async Task<bool> LeapIn(Metroidvania.Player.Player player)
	{
		FlashBody(LeapCrouch);
		Sprite.Frame = 0;
		if (!await Wait(LeapCrouch * 0.5f))
			return false;
		Sprite.Frame = 1;
		if (!await Wait(LeapCrouch * 0.5f))
			return false;

		float vy = -Mathf.Sqrt(2f * Gravity * LeapApex);
		float airTime = 2f * -vy / Mathf.Max(1f, Gravity);
		float target = IsInstanceValid(player)
			? player.GlobalPosition.X - Mathf.Sign(player.GlobalPosition.X - GlobalPosition.X) * LeapLandShort
			: GlobalPosition.X;
		float vx = Mathf.Clamp((target - GlobalPosition.X) / airTime, -LeapMaxSpeedX, LeapMaxSpeedX);
		var launch = new Vector2(vx, vy);
		if (!LandsSafely(launch, 60f))
		{
			for (int f = 2; f <= 4; f++)
			{
				Sprite.Frame = f;
				if (!await Wait(SlamWindup / 5f))
					return false;
			}
			return true;
		}

		_moveOverride = vx;
		Velocity = launch;
		float elapsed = 0f;
		while (elapsed < airTime + 0.6f)
		{
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return false;
			elapsed += (float)GetPhysicsProcessDeltaTime();
			Sprite.Frame = 2 + Mathf.Min(2, (int)(elapsed / airTime * 3f));
			if (elapsed > 0.1f && IsOnFloor())
				break;
		}
		_moveOverride = 0f;
		GroundSlamFx.Chip(this, GlobalPosition + new Vector2(0f, BodyBottom), 0.4f, ImpactPalette);
		return true;
	}

	private bool Strike(Vector2 center, Vector2 size, float damageMultiplier, float knockback)
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
				float away = Mathf.Sign(player.GlobalPosition.X - GlobalPosition.X);
				player.ApplyKnockback(new Vector2(away == 0f ? 1f : away, -0.35f).Normalized(), knockback);
				BloodEffect.SpawnAt(this, player.GlobalPosition);
			}
			return true;
		}
		return false;
	}

	// ── Flinch ──────────────────────────────────────────────────────────────────────────────

	private async Task Flinch()
	{
		_busy = true;
		try
		{
			Sprite.Play("hurt");
			await ToSignal(GetTree().CreateTimer(0.25f), SceneTreeTimer.SignalName.Timeout);
		}
		finally
		{
			if (IsInstanceValid(this))
				_busy = false;
		}
	}

	// ── Shared ──────────────────────────────────────────────────────────────────────────────

	private bool Begin()
	{
		if (!EnemyCombatCoordinator.TryAcquireAttackSlot())
		{
			_attackCooldownTimer = 0.3f;
			return false;
		}
		HoldingAttackSlot = true;
		_busy = true;
		if (PlayerReads.Find(this) is { } player)
		{
			FacingRight = player.GlobalPosition.X >= GlobalPosition.X;
			Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);
		}
		return true;
	}

	private void Finish()
	{
		if (!IsInstanceValid(this))
			return;
		_busy = false;
		_moveOverride = 0f;
		ClearFlash();
		if (HoldingAttackSlot)
		{
			EnemyCombatCoordinator.ReleaseAttackSlot();
			HoldingAttackSlot = false;
		}
	}

	private async Task<bool> Wait(float seconds)
	{
		await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
		return IsInstanceValid(this) && !IsQueuedForRemoval;
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
