using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Guardia esqueleto / Skeleton Guard (Hero & Opponents pack, single 128x96 frames, faces right). An
// undead sentry behind a tower shield: a wall from the front, so the fight is about getting it to
// lower the shield — or getting behind it.
// Sheet beats used:
//   attack_a (12): 1 the axe drawn back, 2 a low sweep out front, 3-4 carried up into an arc,
//     5-7 reset and raised again, 8 swung down the front, 9 a low sweep right across both sides,
//     10-11 the axe trailing behind.
//   attack_b (7): 1-2 the axe hoisted high behind (the wind-up), 3 a huge overhead arc, 4-5 buried
//     in the floor in front, 6 recovery.
//   shield_block: the tower shield braced in front.
// Moves:
//   Shield wall — always guarding from the front unless it's mid-attack (the shield swings aside
//     while it chops). Frontal hits and projectiles clang off; from behind, or while it attacks, it
//     takes them in full.
//   Axe flurry — two sweeps; the second swings low across both sides (don't stand behind it either).
//   Heavy chop — a slow overhead that buries the axe in the floor and throws up a short shockwave
//     on both sides — jump it.
//   Shield bash — a player turtling against its shield gets shoved back hard.
// Telegraphs: body flashes white before every attack; pale-green neon trails.
public partial class SkeletonGuard : Enemy
{
	[ExportGroup("Axe flurry")]
	[Export] public float FlurryRange = 55f;
	[Export] public float AttackCooldown = 1.6f;
	[Export] public float FlurryTelegraph = 0.35f;
	[Export] public float FlurryDamageMultiplier = 1f;
	// Measured (scale 1, sprite at 17,-17): frames 2-3 reach ~49px in front, knee to chest; frame 9
	// sweeps ~45px to either side at knee height.
	[Export] public Vector2 Sweep1Center = new(30f, 6f);
	[Export] public Vector2 Sweep1Size = new(52f, 56f);
	[Export] public Vector2 Sweep2Center = new(0f, 14f);
	[Export] public Vector2 Sweep2Size = new(100f, 40f);

	[ExportGroup("Heavy chop")]
	[Export] public float ChopChance = 0.35f;
	[Export] public float ChopTelegraph = 0.6f;
	[Export] public float ChopDamageMultiplier = 1.5f;
	[Export] public Vector2 ChopCenter = new(30f, -4f);
	[Export] public Vector2 ChopSize = new(50f, 70f);
	[Export] public float ShockwaveRadius = 75f;
	[Export] public float ShockwaveDamageMultiplier = 0.6f;

	[ExportGroup("Shield")]
	[Export] public float BashTurtleTime = 0.6f;
	[Export] public float BashKnockback = 400f;
	[Export] public float BashCooldown = 3f;

	[ExportGroup("Look")]
	[Export] public Color TrailGlowColor = new(0.7f, 1f, 0.75f, 1f);
	[Export] public Color FlashTint = new(2.2f, 2.2f, 2.2f, 1f);

	private static readonly RandomNumberGenerator Rng = new();

	private EnemySlashTrail _trail;
	private Tween _flashTween;
	private bool _busy;
	private bool _shieldDown;
	private float _attackCooldownTimer;
	private float _bashCooldownTimer;
	private float _turtleTimer;

	static SkeletonGuard()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_attackCooldownTimer = Rng.RandfRange(0.5f, 1f);
		_trail = new EnemySlashTrail
		{
			CoreWidth = 2.5f,
			GlowWidth = 9f,
			GlowColor = TrailGlowColor,
			CoreColor = new Color(0.95f, 1f, 0.95f, 1f),
			MinPointSpacing = 1.5f,
			SparkAmount = 10,
		};
		Visual.AddChild(_trail);
		Stats.IncomingHitInterceptor = TryBlock;
		// The sheet names its death clip "dead"; Enemy's death handling looks for "death". Alias it
		// on this instance's own copy of the frames (never touch the shared resource).
		if (Sprite.SpriteFrames is { } frames && !frames.HasAnimation("death") && frames.HasAnimation("dead"))
		{
			var copy = (SpriteFrames)frames.Duplicate();
			copy.AddAnimation("death");
			copy.SetAnimationLoop("death", false);
			copy.SetAnimationSpeed("death", frames.GetAnimationSpeed("dead"));
			for (int i = 0; i < frames.GetFrameCount("dead"); i++)
				copy.AddFrame("death", frames.GetFrameTexture("dead", i));
			Sprite.SpriteFrames = copy;
		}
	}

	private bool PlayerInFront(Node2D player) => (player.GlobalPosition.X >= GlobalPosition.X) == FacingRight;

	// The shield wall: frontal hits clang off unless the shield is swung aside mid-attack.
	private bool TryBlock()
	{
		if (_shieldDown || PlayerReads.Find(this) is not { } player || !PlayerInFront(player))
			return false;
		ImpactEffect.SpawnAt(this, GlobalPosition + new Vector2(FacingRight ? 20f : -20f, -10f));
		Sfx.PlayAt(this, "Combat/Sword", "Sword Blocked");
		return true;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;
		float dt = (float)delta;
		_attackCooldownTimer -= dt;
		_bashCooldownTimer -= dt;
		var player = PlayerReads.Find(this);
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval || player is null || !PlayerDetected || _busy || !IsOnFloor())
			return;

		float absDistance = Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X);
		bool turtling = PlayerReads.IsBlockingToward(player, GlobalPosition.X) && absDistance <= FlurryRange + 10f;
		_turtleTimer = turtling ? _turtleTimer + dt : 0f;
		if (_turtleTimer >= BashTurtleTime && _bashCooldownTimer <= 0f)
		{
			_ = ShieldBash();
			return;
		}
		if (absDistance <= FlurryRange && _attackCooldownTimer <= 0f)
		{
			if (Rng.Randf() < ChopChance)
				_ = HeavyChop();
			else
				_ = AxeFlurry();
		}
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_busy)
			return 0f;
		float toward = Mathf.Sign(distanceX);
		if (Mathf.Abs(distanceX) > FlurryRange * 0.8f && CanStepTo(toward))
			return toward * MoveSpeed;
		return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
	}

	protected override bool CanTurnToFacePlayer => !_busy;

	// ── Axe flurry ──────────────────────────────────────────────────────────────────────────

	private async Task AxeFlurry()
	{
		if (!Begin())
			return;
		try
		{
			Sprite.Play("attack_a");
			Sprite.Pause();
			Sprite.Frame = 1;
			FlashBody(FlurryTelegraph);
			if (!await Wait(FlurryTelegraph))
				return;
			_shieldDown = true;
			Sprite.Frame = 2;
			_trail.Play(new Vector2(0f, 6f), 46f, 160f, 30f, 0.08f, 30f);
			bool hit = Strike(Sweep1Center, Sweep1Size, FlurryDamageMultiplier, 200f, centred: false);
			if (!await Frames(3, 7, 0.07f))
				return;
			if (!hit || Rng.Randf() < 0.5f)
			{
				Sprite.Frame = 8;
				if (!await Wait(0.07f))
					return;
				Sprite.Frame = 9;
				_trail.Play(new Vector2(0f, 14f), 48f, 20f, 160f, 0.08f, 20f);
				Strike(Sweep2Center, Sweep2Size, FlurryDamageMultiplier, 220f, centred: true);
				if (!await Frames(10, 11, 0.08f))
					return;
			}
		}
		finally
		{
			_shieldDown = false;
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * Rng.RandfRange(0.85f, 1.2f);
	}

	// ── Heavy chop ──────────────────────────────────────────────────────────────────────────

	private async Task HeavyChop()
	{
		if (!Begin())
			return;
		try
		{
			Sprite.Play("attack_b");
			Sprite.Pause();
			Sprite.Frame = 1;
			if (!await Wait(0.08f))
				return;
			_shieldDown = true;
			Sprite.Frame = 2;
			FlashBody(ChopTelegraph);
			if (!await Wait(ChopTelegraph))
				return;
			Sprite.Frame = 3;
			_trail.Play(new Vector2(-4f, -10f), 54f, -150f, 60f, 0.1f);
			Strike(ChopCenter, ChopSize, ChopDamageMultiplier, 280f, centred: false);
			if (!await Wait(0.07f))
				return;
			Sprite.Frame = 4;
			Shockwave();
			if (!await Frames(5, 6, 0.18f))
				return;
		}
		finally
		{
			_shieldDown = false;
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * 1.1f;
	}

	// A low shockwave around where the axe bites the floor: grounded players nearby get knocked up.
	private void Shockwave()
	{
		Shake(2.5f, 0.15f);
		Vector2 impact = GlobalPosition + new Vector2(FacingRight ? 34f : -34f, BodyBottom);
		ImpactEffect.SpawnAt(this, impact);
		if (PlayerReads.Find(this) is not { } player || !player.IsOnFloor())
			return;
		if (Mathf.Abs(player.GlobalPosition.X - impact.X) > ShockwaveRadius || Mathf.Abs(player.GlobalPosition.Y - GlobalPosition.Y) > 50f)
			return;
		var stats = player.GetNodeOrNull<Stats>("Stats");
		if (stats is null || stats.IsInvulnerable)
			return;
		int before = stats.CurrentHealth;
		stats.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(Stats.AttackPower * ShockwaveDamageMultiplier)));
		if (stats.CurrentHealth < before)
			player.ApplyKnockback(new Vector2(Mathf.Sign(player.GlobalPosition.X - impact.X) * 0.4f, -1f).Normalized(), 280f);
	}

	// ── Shield bash ─────────────────────────────────────────────────────────────────────────

	private async Task ShieldBash()
	{
		if (!Begin())
			return;
		_bashCooldownTimer = BashCooldown;
		_turtleTimer = 0f;
		try
		{
			Sprite.Play("shield_block");
			FlashBody(0.25f);
			if (!await Wait(0.25f))
				return;
			Visual.Position = new Vector2(FacingRight ? 8f : -8f, 0f);
			if (PlayerReads.Find(this) is { } player && Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X) <= FlurryRange + 20f)
			{
				// Shoves through the block; tiny chip damage only if it lands.
				player.GetNodeOrNull<Stats>("Stats")?.TakeDamage(Mathf.Max(1, Stats.AttackPower / 3));
				player.ApplyKnockback(new Vector2(FacingRight ? 1f : -1f, -0.3f).Normalized(), BashKnockback);
				Sfx.PlayAt(this, "Combat/Sword", "Sword Blocked");
			}
			if (!await Wait(0.2f))
				return;
			Visual.Position = Vector2.Zero;
		}
		finally
		{
			if (IsInstanceValid(this))
				Visual.Position = Vector2.Zero;
			Finish();
		}
		_attackCooldownTimer = Mathf.Max(_attackCooldownTimer, 0.5f);
	}

	// ── Shared ──────────────────────────────────────────────────────────────────────────────

	private async Task<bool> Frames(int from, int to, float each)
	{
		for (int f = from; f <= to; f++)
		{
			Sprite.Frame = f;
			if (!await Wait(each))
				return false;
		}
		return true;
	}

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

	private bool Strike(Vector2 center, Vector2 size, float damageMultiplier, float knockback, bool centred)
	{
		Vector2 at = GlobalPosition + new Vector2(FacingRight || centred ? center.X : -center.X, center.Y);
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
				player.ApplyKnockback(new Vector2(Mathf.Sign(player.GlobalPosition.X - GlobalPosition.X), -0.3f).Normalized(), knockback);
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
		_shieldDown = true;
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
