using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Escorpión / Scorpion (Desert enemy pack, 160x64 cells, faces LEFT — mirrored on the scene). A low,
// skittering hit-and-run critter of the desert: it darts in, stings, and scuttles back out.
// Sheet beats used:
//   attack (12fps): 0-6 the tail cocking back as it crouches (the natural wind-up), 7 the stinger
//     lashing far out in front (orange streak), 8-10 the tail arcing back over, 11-13 settle.
// Moves:
//   Sting — the tail lash; poisons on a hit that isn't blocked. It lunges forward a little during
//     the wind-up, so it reaches further than it looks.
//   Scuttle back — after every sting it skitters away, then circles back in.
//   Double sting — sometimes it re-cocks straight away and stings again.
//   Low profile — it's knee-high: it ignores you while you're airborne above it and waits for
//     you to land.
// Telegraphs: the body flashes white during the wind-up; poison-orange streak on the sting.
public partial class Scorpion : Enemy
{
	[ExportGroup("Sting")]
	[Export] public float StingRange = 52f;
	[Export] public float AttackCooldown = 1.1f;
	[Export] public float WindupSpeedScale = 1f;
	[Export] public float LungeSpeed = 120f;
	[Export] public float DoubleChance = 0.3f;
	[Export] public float StingDamageMultiplier = 1f;
	// Measured off attack frame 7 (scale 1.5 mirrored, sprite at -6.75,-31): the stinger lashes out
	// ~49px in front, at about knee height.
	[Export] public Vector2 StingHitboxCenter = new(32f, -6f);
	[Export] public Vector2 StingHitboxSize = new(38f, 26f);
	[Export] public int PoisonTickDamage = 2;
	[Export] public float PoisonTickInterval = 0.5f;
	[Export] public float PoisonDuration = 2f;

	[ExportGroup("Scuttle")]
	[Export] public float ScuttleSpeed = 170f;
	[Export] public float ScuttleTime = 0.45f;

	[ExportGroup("Look")]
	[Export] public Color StreakColor = new(1f, 0.6f, 0.1f, 1f);
	[Export] public Color FlashTint = new(2.2f, 2.2f, 2.2f, 1f);

	private const float AttackFps = 12f;

	private static readonly RandomNumberGenerator Rng = new();

	private EnemySlashTrail _trail;
	private Tween _flashTween;
	private bool _busy;
	private float _moveOverride;
	private float _attackCooldownTimer;

	static Scorpion()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_attackCooldownTimer = Rng.RandfRange(0.3f, 0.8f);
		_trail = new EnemySlashTrail
		{
			CoreWidth = 2f,
			GlowWidth = 7f,
			GlowColor = StreakColor,
			CoreColor = new Color(1f, 0.9f, 0.7f, 1f),
			MinPointSpacing = 1.5f,
			SparkAmount = 8,
		};
		Visual.AddChild(_trail);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;
		_attackCooldownTimer -= (float)delta;
		var player = PlayerReads.Find(this);
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval || player is null || !PlayerDetected || _busy || !IsOnFloor())
			return;

		float distanceX = player.GlobalPosition.X - GlobalPosition.X;
		// Knee-high: someone jumping over it is out of reach.
		bool reachable = player.GlobalPosition.Y > GlobalPosition.Y - 50f;
		if (reachable && Mathf.Abs(distanceX) <= StingRange && _attackCooldownTimer <= 0f)
			_ = Sting();
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_busy)
			return _moveOverride == 0f || CanStepTo(Mathf.Sign(_moveOverride)) ? _moveOverride : 0f;
		float toward = Mathf.Sign(distanceX);
		// Hangs back a little while its sting recharges, darts in when it's ready.
		float wanted = _attackCooldownTimer > 0.2f ? StingRange + 40f : StingRange * 0.7f;
		float absDistance = Mathf.Abs(distanceX);
		if (absDistance > wanted + 8f && CanStepTo(toward))
			return toward * MoveSpeed;
		if (absDistance < wanted - 20f && CanStepTo(-toward))
			return -toward * MoveSpeed * 0.7f;
		return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
	}

	protected override bool DesiredFacingRight(Node2D player, float distanceX) => distanceX >= 0;

	protected override bool CanTurnToFacePlayer => !_busy;

	// ── Sting ───────────────────────────────────────────────────────────────────────────────

	private async Task Sting()
	{
		if (!Begin())
			return;
		try
		{
			int stings = Rng.Randf() < DoubleChance ? 2 : 1;
			for (int s = 0; s < stings; s++)
			{
				FacePlayer();
				Sprite.Play("attack");
				Sprite.Pause();
				int start = s == 0 ? 0 : 3;
				FlashBody((7 - start) / AttackFps);
				for (int f = start; f <= 6; f++)
				{
					Sprite.Frame = f;
					// Creeps forward on the last wind-up frames.
					_moveOverride = f >= 4 ? (FacingRight ? 1f : -1f) * LungeSpeed : 0f;
					if (!await Wait(1f / AttackFps / WindupSpeedScale))
						return;
				}
				_moveOverride = 0f;
				Sprite.Frame = 7;
				_trail.PlayThrust(new Vector2(4f, -10f), new Vector2(46f, -8f), 0.05f, 0.08f);
				Strike();
				for (int f = 8; f <= 10; f++)
				{
					if (!await Wait(1f / AttackFps))
						return;
					Sprite.Frame = f;
				}
			}

			// Scuttle back out.
			Sprite.Play("walk");
			_moveOverride = (FacingRight ? -1f : 1f) * ScuttleSpeed;
			if (!await Wait(ScuttleTime))
				return;
			_moveOverride = 0f;
		}
		finally
		{
			if (IsInstanceValid(this))
				_moveOverride = 0f;
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * Rng.RandfRange(0.8f, 1.25f);
	}

	private void Strike()
	{
		Vector2 at = GlobalPosition + new Vector2(FacingRight ? StingHitboxCenter.X : -StingHitboxCenter.X, StingHitboxCenter.Y);
		var query = new PhysicsShapeQueryParameters2D
		{
			Shape = new RectangleShape2D { Size = StingHitboxSize },
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
			stats.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(Stats.AttackPower * StingDamageMultiplier)), element: DamageElement.Poison);
			if (stats.CurrentHealth < before)
			{
				player.ApplyPoison(PoisonTickDamage, PoisonTickInterval, PoisonDuration);
				player.ApplyKnockback(new Vector2(Mathf.Sign(player.GlobalPosition.X - GlobalPosition.X), -0.2f).Normalized(), 160f);
				BloodEffect.SpawnAt(this, player.GlobalPosition);
			}
			return;
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

	private async Task<bool> Wait(float seconds)
	{
		await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
		return IsInstanceValid(this) && !IsQueuedForRemoval;
	}

	private void FacePlayer()
	{
		if (PlayerReads.Find(this) is { } player)
		{
			FacingRight = player.GlobalPosition.X >= GlobalPosition.X;
			Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);
		}
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
