using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Beduino del cactus / Cactus Bedouin (Desert enemy pack, 96x64 cells, faces right). A desert
// brawler swinging a living cactus as a club — everything about it is prickly.
// Sheet beats used:
//   attack_melle (12fps): 2 club raised, 3-5 swung back behind it (the wind-up), 6 the big
//     horizontal swing (green arc over the top and out front), 7-9 spines flying off the club,
//     10-12 recovery.
//   attack_ranged (12fps): the cactus shaken at the hip with a kick (4-6) — the needle spray.
// Moves:
//   Cactus swing — wide, heavy club swing; the spines it sheds spray forward as a short-range fan.
//   Needle spray — from mid range it shakes the cactus and flings a fan of spines in arcs
//     (CactusNeedle). Keeps itself at that range when it can.
//   Thorny hide — hitting it up close pricks you back a little (with a burst of needles), at most
//     once every ThornInterval. Dash-attacks and projectiles get through clean.
//   Punish — a whiffed swing near it gets a quick swing back.
// Telegraphs: body flashes white before each attack; green neon trail on the swing.
public partial class CactusBedouin : Enemy
{
	[ExportGroup("Spacing")]
	[Export] public float PreferredMin = 90f;
	[Export] public float PreferredMax = 200f;

	[ExportGroup("Swing")]
	[Export] public float SwingRange = 50f;
	[Export] public float AttackCooldown = 1.4f;
	[Export] public float SwingTelegraph = 0.35f;
	[Export] public float SwingDamageMultiplier = 1.1f;
	[Export] public float SwingKnockback = 240f;
	// Measured off attack_melle frame 6 (scale 1.5, sprite at 3.75,-18): the arc reaches ~50px in
	// front, from over the head down to the waist.
	[Export] public Vector2 SwingHitboxCenter = new(28f, -14f);
	[Export] public Vector2 SwingHitboxSize = new(48f, 58f);
	[Export] public int SwingSpines = 3;

	[ExportGroup("Needle spray")]
	[Export] public float SprayCooldown = 3f;
	[Export] public float SprayChance = 0.55f;
	[Export] public int SprayCount = 5;
	[Export] public float SpraySpeed = 300f;
	[Export] public float SpraySpreadDegrees = 30f;
	[Export] public float NeedleDamageMultiplier = 0.6f;
	// The cactus at the hip during attack_ranged.
	[Export] public Vector2 SprayOrigin = new(20f, 4f);

	[ExportGroup("Thorny hide")]
	[Export] public float ThornDamageMultiplier = 0.25f;
	[Export] public float ThornInterval = 0.8f;
	[Export] public float ThornRange = 70f;

	[ExportGroup("Look")]
	[Export] public Color TrailGlowColor = new(0.45f, 0.95f, 0.3f, 1f);
	[Export] public Color FlashTint = new(2.2f, 2.2f, 2.2f, 1f);
	[Export] public float DecisionInterval = 0.35f;

	private const float AttackFps = 12f;

	private static readonly RandomNumberGenerator Rng = new();
	private readonly WhiffWatcher _whiff = new();

	private EnemySlashTrail _trail;
	private Tween _flashTween;
	private bool _busy;
	private float _decisionTimer;
	private float _attackCooldownTimer;
	private float _sprayCooldownTimer;
	private float _thornTimer;

	static CactusBedouin()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_attackCooldownTimer = Rng.RandfRange(0.4f, 0.9f);
		_sprayCooldownTimer = Rng.RandfRange(0.5f, 1.5f);
		_trail = new EnemySlashTrail
		{
			CoreWidth = 3f,
			GlowWidth = 10f,
			GlowColor = TrailGlowColor,
			CoreColor = new Color(0.9f, 1f, 0.8f, 1f),
			MinPointSpacing = 1.5f,
			SparkAmount = 10,
		};
		Visual.AddChild(_trail);
		Stats.HitTaken += isProjectile => OnHit(isProjectile);
		Stats.HitTaken += _ => _whiff.NotifyHitTaken();
	}

	// Thorny hide: a melee hit pricks the attacker back.
	private void OnHit(bool isProjectile)
	{
		if (isProjectile || _thornTimer > 0f || PlayerReads.Find(this) is not { } player || player.IsDashing)
			return;
		if (player.GlobalPosition.DistanceTo(GlobalPosition) > ThornRange)
			return;
		_thornTimer = ThornInterval;
		if (player.GetNodeOrNull<Stats>("Stats") is { } stats && !stats.IsInvulnerable)
			stats.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(Stats.AttackPower * ThornDamageMultiplier)));
		// A little cosmetic burst of needles off its body.
		for (int i = 0; i < 4; i++)
		{
			float angle = Mathf.DegToRad(Rng.RandfRange(-150f, -30f));
			SpawnNeedle(GlobalPosition + new Vector2(0f, -10f), Vector2.FromAngle(angle) * 160f, 0f);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;
		float dt = (float)delta;
		_decisionTimer -= dt;
		_attackCooldownTimer -= dt;
		_sprayCooldownTimer -= dt;
		_thornTimer -= dt;

		var player = PlayerReads.Find(this);
		_whiff.Update(player, dt);
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval || player is null || !PlayerDetected || _busy || !IsOnFloor())
			return;
		Think(player);
	}

	private void Think(Metroidvania.Player.Player player)
	{
		float distanceX = player.GlobalPosition.X - GlobalPosition.X;
		float absDistance = Mathf.Abs(distanceX);

		if (_whiff.JustWhiffed && absDistance <= SwingRange + 10f && _attackCooldownTimer <= AttackCooldown * 0.5f)
		{
			_whiff.Consume();
			_ = Swing(0.15f);
			return;
		}
		if (absDistance <= SwingRange && _attackCooldownTimer <= 0f)
		{
			_ = Swing(SwingTelegraph);
			return;
		}
		if (_decisionTimer > 0f || _attackCooldownTimer > AttackCooldown * 0.5f)
			return;
		_decisionTimer = DecisionInterval * Rng.RandfRange(0.8f, 1.3f);
		if (absDistance > SwingRange && absDistance <= 320f && _sprayCooldownTimer <= 0f && Rng.Randf() < SprayChance)
			_ = NeedleSpray();
	}

	// Hangs around spray range while the spray is coming back; otherwise walks in to swing.
	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_busy)
			return 0f;
		float toward = Mathf.Sign(distanceX);
		float absDistance = Mathf.Abs(distanceX);
		// Too close to bother spraying: walk in and swing.
		bool sprayReady = _sprayCooldownTimer <= 0.5f && absDistance > SwingRange + 25f;
		float stopAt = sprayReady ? PreferredMin : SwingRange * 0.8f;
		if (absDistance > (sprayReady ? PreferredMax : stopAt) && CanStepTo(toward))
			return toward * MoveSpeed;
		if (sprayReady && absDistance < PreferredMin - 30f && CanStepTo(-toward))
			return -toward * MoveSpeed * 0.8f;
		return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
	}

	protected override bool CanTurnToFacePlayer => !_busy;

	// ── Swing ───────────────────────────────────────────────────────────────────────────────

	private async Task Swing(float telegraph)
	{
		if (!Begin())
			return;
		try
		{
			Sprite.Play("attack_melle");
			Sprite.Pause();
			Sprite.Frame = 2;
			if (!await Wait(1f / AttackFps))
				return;
			Sprite.Frame = 4;
			FlashBody(telegraph);
			if (!await Wait(telegraph))
				return;
			Sprite.Frame = 5;
			if (!await Wait(1f / AttackFps))
				return;
			Sprite.Frame = 6;
			_trail.Play(new Vector2(0f, -14f), 48f, -165f, 20f, 0.09f, 34f);
			Strike();
			for (int f = 7; f <= 12; f++)
			{
				if (!await Wait(1f / AttackFps))
					return;
				Sprite.Frame = f;
				// The club sheds a short fan of spines as it follows through.
				if (f == 7)
					for (int i = 0; i < SwingSpines; i++)
					{
						float angle = Mathf.DegToRad(-35f + 25f * i);
						Vector2 dir = Vector2.FromAngle(angle);
						SpawnNeedle(GlobalPosition + new Vector2(FacingRight ? 26f : -26f, -12f),
							new Vector2(FacingRight ? dir.X : -dir.X, dir.Y) * 230f, NeedleDamageMultiplier);
					}
			}
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * Rng.RandfRange(0.85f, 1.2f);
	}

	private void Strike()
	{
		Vector2 at = GlobalPosition + new Vector2(FacingRight ? SwingHitboxCenter.X : -SwingHitboxCenter.X, SwingHitboxCenter.Y);
		var query = new PhysicsShapeQueryParameters2D
		{
			Shape = new RectangleShape2D { Size = SwingHitboxSize },
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
			stats.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(Stats.AttackPower * SwingDamageMultiplier)));
			if (stats.CurrentHealth < before)
			{
				player.ApplyKnockback(new Vector2(Mathf.Sign(player.GlobalPosition.X - GlobalPosition.X), -0.3f).Normalized(), SwingKnockback);
				BloodEffect.SpawnAt(this, player.GlobalPosition);
			}
			return;
		}
	}

	// ── Needle spray ────────────────────────────────────────────────────────────────────────

	private async Task NeedleSpray()
	{
		if (!Begin())
			return;
		_sprayCooldownTimer = SprayCooldown;
		try
		{
			Sprite.Play("attack_ranged");
			Sprite.Pause();
			FlashBody(0.35f);
			for (int f = 0; f <= 9; f++)
			{
				Sprite.Frame = f;
				if (f == 5)
					Spray();
				if (!await Wait(1f / AttackFps * (f < 4 ? 1.3f : 1f)))
					return;
			}
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * 0.7f;
	}

	private void Spray()
	{
		if (PlayerReads.Find(this) is not { } player)
			return;
		Vector2 origin = GlobalPosition + new Vector2(FacingRight ? SprayOrigin.X : -SprayOrigin.X, SprayOrigin.Y);
		// Aim the middle spine so its arc drops onto the player: a flat-ish lob, raised with distance.
		float dx = Mathf.Abs(player.GlobalPosition.X - origin.X);
		float lift = Mathf.Clamp(dx / 320f, 0.1f, 0.55f);
		Vector2 baseDir = new Vector2(FacingRight ? 1f : -1f, -lift).Normalized();
		for (int i = 0; i < SprayCount; i++)
		{
			float t = SprayCount == 1 ? 0.5f : (float)i / (SprayCount - 1);
			float spread = Mathf.DegToRad(Mathf.Lerp(-SpraySpreadDegrees, SpraySpreadDegrees, t) * 0.5f);
			Vector2 dir = baseDir.Rotated(FacingRight ? spread : -spread);
			SpawnNeedle(origin, dir * SpraySpeed * Rng.RandfRange(0.9f, 1.1f), NeedleDamageMultiplier);
		}
		Sfx.PlayAt(this, "Combat/Bow", "Bow Attack");
	}

	private void SpawnNeedle(Vector2 at, Vector2 velocity, float damageMultiplier)
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		var needle = new CactusNeedle { Velocity = velocity, ThrowerStats = Stats, DamageMultiplier = damageMultiplier };
		scene.AddChild(needle);
		needle.GlobalPosition = at;
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
