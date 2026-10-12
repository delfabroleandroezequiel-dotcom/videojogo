using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Esqueleto guerrero / Skeleton Warrior (Undead pack, 110x80 cells, faces right). Sword-and-board
// grunt of the undead: patient behind its shield, and it doesn't stay dead the first time.
// Sheet beats used:
//   attack (8fps): 0-2 settle, 3 sword raised overhead (held as the wind-up), 4 the big cleave
//     sweeping down the front, 5 the low follow-through, 6-7 recovery.
//   block: 1 shield up, 2-4 the shield flashing (held while guarding), 5 lowered.
//   death / revival: collapsing into a bone pile / reassembling from it.
// Moves:
//   Cleave — sometimes chained into a second cleave.
//   Shield guard — a swing starting in front of it raises the shield: frontal hits and
//     projectiles are blocked (sparks). Each block cracks the guard; GuardBreakBlocks blocks in a
//     row break it and leave it reeling, open. A block is answered by a quick counter-cleave.
//   Shield bash — from mid range it rushes behind the raised shield and shoves you back, through
//     your block.
//   Reassemble — the first "death" only collapses it into a bone pile. Hit the pile before it
//     reassembles (ReassembleDelay) and it shatters for good; otherwise it stands back up with
//     ReviveHealthRatio of its health, faster and angrier. Only once.
//   Punish — a whiffed swing or a dash ending in reach is answered with a quick cleave.
// Telegraphs: the body flashes white before every attack; spectral-cyan neon trails.
public partial class SkeletonWarrior : Enemy
{
	[ExportGroup("Cleave")]
	[Export] public float CleaveRange = 52f;
	[Export] public float AttackCooldown = 1.5f;
	[Export] public float CleaveTelegraph = 0.4f;
	[Export] public float DoubleChance = 0.3f;
	[Export] public float CleaveDamageMultiplier = 1f;
	[Export] public float CleaveKnockback = 220f;
	// Measured off attack frame 4 (scale 1.5, sprite at 0,-5): the arc reaches ~56px in front, from
	// above the head down to the feet.
	[Export] public Vector2 CleaveHitboxCenter = new(32f, -6f);
	[Export] public Vector2 CleaveHitboxSize = new(48f, 70f);
	[Export] public Color TrailGlowColor = new(0.25f, 0.8f, 1f, 1f);
	[Export] public Color TrailCoreColor = new(0.85f, 0.97f, 1f, 1f);
	[Export] public Color FlashTint = new(2.2f, 2.2f, 2.2f, 1f);

	[ExportGroup("Shield")]
	[Export] public float GuardRange = 110f;
	[Export] public float GuardChance = 0.6f;
	[Export] public float GuardDuration = 1.1f;
	[Export] public float GuardCooldown = 2.2f;
	[Export] public int GuardBreakBlocks = 3;
	[Export] public float GuardBrokenTime = 1f;
	[Export] public float CounterTelegraph = 0.15f;

	[ExportGroup("Shield bash")]
	[Export] public float BashMinDistance = 90f;
	[Export] public float BashMaxDistance = 240f;
	[Export] public float BashChance = 0.35f;
	[Export] public float BashCooldown = 5f;
	[Export] public float BashTelegraph = 0.45f;
	[Export] public float BashSpeed = 300f;
	[Export] public float BashMaxTime = 0.7f;
	[Export] public float BashDamageMultiplier = 0.7f;
	[Export] public float BashKnockback = 380f;
	[Export] public Vector2 BashHitboxCenter = new(20f, -6f);
	[Export] public Vector2 BashHitboxSize = new(30f, 50f);

	[ExportGroup("Reassemble")]
	[Export] public float ReassembleDelay = 2.5f;
	[Export] public float ReviveHealthRatio = 0.5f;
	[Export] public float RevivedSpeedMultiplier = 1.25f;
	[Export] public float RevivedCooldownMultiplier = 0.75f;

	[Export] public float DecisionInterval = 0.4f;

	// Raised by a Necromancer: climbs out of the floor on spawn (revival played), never reassembles,
	// drops nothing and leaves no "defeated" mark in the save. Set before adding it to the tree.
	public bool Summoned;
	public const string SummonPersistenceId = "necromancer_summon";

	private const float AttackFps = 8f;

	private static readonly RandomNumberGenerator Rng = new();
	private readonly DashWatcher _dash = new();
	private readonly WhiffWatcher _whiff = new();

	private EnemySlashTrail _trail;
	private Tween _flashTween;
	private bool _busy;
	private bool _guarding;
	private bool _blocked;
	private int _guardCracks;
	private bool _collapsed;
	private bool _revived;
	private bool _shatter;
	private float _moveOverride;
	private float _decisionTimer;
	private float _attackCooldownTimer;
	private float _guardCooldownTimer;
	private float _bashCooldownTimer;
	private float _guardRecoverTimer;

	static SkeletonWarrior()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_attackCooldownTimer = Rng.RandfRange(0.4f, 0.9f);
		_bashCooldownTimer = Rng.RandfRange(1f, 2f);
		_trail = new EnemySlashTrail
		{
			CoreWidth = 2.5f,
			GlowWidth = 9f,
			GlowColor = TrailGlowColor,
			CoreColor = TrailCoreColor,
			MinPointSpacing = 1.5f,
			SparkAmount = 12,
		};
		Visual.AddChild(_trail);
		// The first lethal hit only collapses it (see Collapse); Died fires only after that.
		Stats.MinHealth = Summoned ? 0 : 1;
		_revived = Summoned;
		Stats.IncomingHitInterceptor = TryBlock;
		Stats.HealthChanged += (current, max) => OnHealthChanged(current);
		Stats.HitTaken += _ => _whiff.NotifyHitTaken();
		if (Summoned)
		{
			LootTable = System.Array.Empty<LootEntry>();
			DropsShadowEssence = false;
			_ = RiseFromGround();
		}
	}

	// Summoned: climbs out of the floor before it can act.
	private async Task RiseFromGround()
	{
		_busy = true;
		Sprite.Play("revival");
		await ToSignal(GetTree().CreateTimer(10f / 7.1f), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;
		_busy = false;
		_attackCooldownTimer = 0.5f;
	}

	// Summons share one fixed persistence id; it must never make the next summon count as dead.
	protected override bool IsDefeated() => !Summoned && base.IsDefeated();

	private bool PlayerInFront(Node2D player) => (player.GlobalPosition.X >= GlobalPosition.X) == FacingRight;

	// While the shield is up, frontal hits and projectiles bounce off it; from behind they land.
	private bool TryBlock()
	{
		if (!_guarding || PlayerReads.Find(this) is not { } player || !PlayerInFront(player))
			return false;
		_blocked = true;
		_guardCracks++;
		ImpactEffect.SpawnAt(this, GlobalPosition + new Vector2(FacingRight ? 18f : -18f, -12f));
		Sfx.PlayAt(this, "Combat/Sword", "Sword Blocked");
		return true;
	}

	private void OnHealthChanged(int current)
	{
		if (_collapsed)
		{
			// Struck while it's a bone pile → it shatters for good.
			_shatter = true;
			return;
		}
		if (current <= 1 && !_revived && Stats.MinHealth > 0)
			_ = Collapse();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;
		float dt = (float)delta;
		_decisionTimer -= dt;
		_attackCooldownTimer -= dt;
		_guardCooldownTimer -= dt;
		_bashCooldownTimer -= dt;
		_guardRecoverTimer -= dt;
		if (_guardRecoverTimer <= 0f)
			_guardCracks = 0;

		var player = PlayerReads.Find(this);
		_dash.Update(player, dt);
		_whiff.Update(player, dt);

		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval || player is null || !PlayerDetected || _busy || !IsOnFloor())
			return;
		Think(player);
	}

	// ── Decisions ───────────────────────────────────────────────────────────────────────────

	private void Think(Metroidvania.Player.Player player)
	{
		float distanceX = player.GlobalPosition.X - GlobalPosition.X;
		float absDistance = Mathf.Abs(distanceX);

		// A swing starting in front of it → shield up.
		if (_whiff.PlayerStartedSwing && absDistance <= GuardRange && PlayerInFront(player)
			&& _guardCooldownTimer <= 0f && Rng.Randf() < GuardChance)
		{
			_ = Guard();
			return;
		}

		// A whiff / dash ending in reach → quick cleave.
		if ((_whiff.JustWhiffed || _dash.SinceDashEnded < 0.1f) && absDistance <= CleaveRange + 12f
			&& _attackCooldownTimer <= AttackCooldown * 0.5f)
		{
			_whiff.Consume();
			_ = Cleave(0.15f, allowDouble: false);
			return;
		}

		if (absDistance <= CleaveRange && _attackCooldownTimer <= 0f)
		{
			_ = Cleave(CleaveTelegraph, allowDouble: true);
			return;
		}

		if (_decisionTimer > 0f || _attackCooldownTimer > AttackCooldown * 0.5f)
			return;
		_decisionTimer = DecisionInterval * Rng.RandfRange(0.8f, 1.3f);

		// Turtling players get bashed more often.
		bool turtling = PlayerReads.IsBlockingToward(player, GlobalPosition.X);
		if (absDistance >= BashMinDistance && absDistance <= BashMaxDistance && _bashCooldownTimer <= 0f
			&& Rng.Randf() < BashChance + (turtling ? 0.35f : 0f))
			_ = ShieldBash(Mathf.Sign(distanceX));
	}

	// ── Movement ────────────────────────────────────────────────────────────────────────────

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_busy)
			return _moveOverride;
		float toward = Mathf.Sign(distanceX);
		if (Mathf.Abs(distanceX) > CleaveRange * 0.8f && CanStepTo(toward))
			return toward * MoveSpeed;
		return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
	}

	protected override bool CanTurnToFacePlayer => !_busy;

	// ── Cleave ──────────────────────────────────────────────────────────────────────────────

	private async Task Cleave(float telegraph, bool allowDouble)
	{
		if (!Begin())
			return;
		try
		{
			int swings = allowDouble && Rng.Randf() < DoubleChance ? 2 : 1;
			for (int swing = 0; swing < swings; swing++)
			{
				FacePlayer();
				Sprite.Play("attack");
				Sprite.Pause();
				Sprite.Frame = 3;
				float hold = swing == 0 ? telegraph : 0.2f;
				FlashBody(hold);
				if (!await Wait(hold))
					return;

				Sprite.Frame = 4;
				_trail.Play(new Vector2(4f, -12f), 50f, -120f, 75f, 0.1f);
				Strike(CleaveHitboxCenter, CleaveHitboxSize, CleaveDamageMultiplier, CleaveKnockback, shoveThroughBlock: false);
				for (int f = 5; f <= 7; f++)
				{
					if (!await Wait(1f / AttackFps))
						return;
					Sprite.Frame = f;
				}
			}
			if (!await Wait(0.2f))
				return;
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * CooldownScale() * Rng.RandfRange(0.85f, 1.2f);
	}

	// ── Shield guard ────────────────────────────────────────────────────────────────────────

	private async Task Guard()
	{
		_busy = true;
		_guarding = true;
		_blocked = false;
		_guardCooldownTimer = GuardCooldown;
		FacePlayer();
		bool broken = false;
		try
		{
			Sprite.Play("block");
			Sprite.Pause();
			Sprite.Frame = 1;
			float t = 0f;
			int frame = 1;
			float frameTimer = 0f;
			while (t < GuardDuration)
			{
				await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
				if (!IsInstanceValid(this) || IsQueuedForRemoval || _collapsed)
					return;
				float dt = (float)GetPhysicsProcessDeltaTime();
				t += dt;
				frameTimer += dt;
				// Shield flash (frames 2-4) looping while it holds the guard.
				if (frameTimer >= 0.12f)
				{
					frameTimer = 0f;
					frame = frame >= 4 ? 2 : frame + 1;
					Sprite.Frame = frame;
				}
				if (_guardCracks >= GuardBreakBlocks)
				{
					broken = true;
					break;
				}
				if (_blocked)
					break;
			}
			_guarding = false;
			_guardRecoverTimer = 2f;

			if (broken)
			{
				// Guard broken: shield knocked aside, reeling and open.
				_guardCracks = 0;
				Shake(2f, 0.12f);
				ImpactEffect.SpawnAt(this, GlobalPosition + new Vector2(FacingRight ? 16f : -16f, -14f));
				Sprite.Play("hit");
				if (!await Wait(GuardBrokenTime))
					return;
			}
		}
		finally
		{
			if (IsInstanceValid(this))
			{
				_guarding = false;
				_busy = false;
			}
		}

		// A clean block → quick counter-cleave.
		if (_blocked && !broken)
		{
			_attackCooldownTimer = 0f;
			_ = Cleave(CounterTelegraph, allowDouble: false);
		}
	}

	// ── Shield bash ─────────────────────────────────────────────────────────────────────────

	private async Task ShieldBash(float direction)
	{
		if (!Begin())
			return;
		_bashCooldownTimer = BashCooldown;
		try
		{
			FaceTowards(GlobalPosition.X + direction);
			Sprite.Play("block");
			Sprite.Pause();
			Sprite.Frame = 1;
			FlashBody(BashTelegraph);
			if (!await Wait(BashTelegraph))
				return;

			// Rush behind the raised shield; the shield flash plays as it rams.
			Sprite.Frame = 3;
			_moveOverride = direction * BashSpeed * SpeedScale();
			float t = 0f;
			bool hit = false;
			while (t < BashMaxTime && !hit)
			{
				await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
				if (!IsInstanceValid(this) || IsQueuedForRemoval || _collapsed)
					return;
				t += (float)GetPhysicsProcessDeltaTime();
				hit = Strike(BashHitboxCenter, BashHitboxSize, BashDamageMultiplier, BashKnockback, shoveThroughBlock: true);
				if (TestMove(GlobalTransform, new Vector2(direction * 6f, 0f)) || !HasGroundAhead(direction))
					break;
			}
			_moveOverride = 0f;
			if (hit)
				Shake(2.5f, 0.15f);
			Sprite.Frame = 5;
			if (!await Wait(0.35f))
				return;
		}
		finally
		{
			if (IsInstanceValid(this))
				_moveOverride = 0f;
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * 0.5f;
	}

	// ── Reassemble ──────────────────────────────────────────────────────────────────────────

	// The first lethal hit collapses it into a bone pile instead of killing it. Struck again while
	// down → it shatters for good; left alone → it reassembles with part of its health.
	private async Task Collapse()
	{
		_collapsed = true;
		_shatter = false;
		_busy = true;
		_guarding = false;
		_moveOverride = 0f;
		ClearFlash();
		if (HoldingAttackSlot)
		{
			EnemyCombatCoordinator.ReleaseAttackSlot();
			HoldingAttackSlot = false;
		}
		// Harmless and unblockable on the floor; hits still register (that's how it gets finished).
		Sprite.Play("death");
		float t = 0f;
		while (t < ReassembleDelay)
		{
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;
			t += (float)GetPhysicsProcessDeltaTime();
			if (_shatter)
			{
				Stats.MinHealth = 0;
				ImpactEffect.SpawnAt(this, GlobalPosition + new Vector2(0f, 20f));
				Shake(2f, 0.12f);
				Stats.Kill();
				return;
			}
			// Bones rattle just before it gets up, as a warning.
			if (ReassembleDelay - t < 0.5f)
				Visual.Position = new Vector2(Mathf.Sin(t * 70f) * 1.5f, 0f);
		}
		Visual.Position = Vector2.Zero;

		Sprite.Play("revival");
		FlashBody(0.8f);
		await ToSignal(GetTree().CreateTimer(10f / 7.1f), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;
		_revived = true;
		_collapsed = false;
		Stats.MinHealth = 0;
		Stats.SetCurrentHealth(Mathf.Max(1, Mathf.RoundToInt(Stats.MaxHealth * ReviveHealthRatio)));
		MoveSpeed *= RevivedSpeedMultiplier;
		_busy = false;
		_attackCooldownTimer = 0.3f;
	}

	private float SpeedScale() => _revived ? RevivedSpeedMultiplier : 1f;
	private float CooldownScale() => _revived ? RevivedCooldownMultiplier : 1f;

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
		FacePlayer();
		return true;
	}

	private void Finish()
	{
		if (!IsInstanceValid(this))
			return;
		if (!_collapsed)
			_busy = false;
		ClearFlash();
		if (HoldingAttackSlot)
		{
			EnemyCombatCoordinator.ReleaseAttackSlot();
			HoldingAttackSlot = false;
		}
	}

	// One damage check; returns whether it connected. shoveThroughBlock: the knockback lands even if
	// the player blocks the damage.
	private bool Strike(Vector2 center, Vector2 size, float damageMultiplier, float knockback, bool shoveThroughBlock)
	{
		if (_collapsed)
			return false;
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
			if (hit["collider"].AsGodotObject() is not Node2D body || !body.IsInGroup("player"))
				continue;
			if (body.GetNodeOrNull<Stats>("Stats") is not { } stats || stats.IsInvulnerable)
				continue;
			int before = stats.CurrentHealth;
			stats.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(Stats.AttackPower * damageMultiplier)));
			bool landed = stats.CurrentHealth < before;
			if ((landed || shoveThroughBlock) && body.HasMethod("ApplyKnockback"))
				body.Call("ApplyKnockback", new Vector2(Mathf.Sign(body.GlobalPosition.X - GlobalPosition.X), -0.25f).Normalized(), knockback);
			if (landed)
				BloodEffect.SpawnAt(this, body.GlobalPosition);
			return true;
		}
		return false;
	}

	// False once it's gone or collapsed: an attack interrupted by the collapse just stops there
	// instead of driving frames over the death animation.
	private async Task<bool> Wait(float seconds)
	{
		await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
		return IsInstanceValid(this) && !IsQueuedForRemoval && !_collapsed;
	}

	private void Shake(float strength, float duration)
	{
		if (PlayerReads.Find(this) is { } player && player.GlobalPosition.DistanceTo(GlobalPosition) < 700f)
			player.ShakeCamera(strength, duration);
	}

	private void FacePlayer()
	{
		if (PlayerReads.Find(this) is { } player)
			FaceTowards(player.GlobalPosition.X);
	}

	private void FaceTowards(float targetX)
	{
		FacingRight = targetX >= GlobalPosition.X;
		Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);
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
		bool alreadyDown = _collapsed;
		ClearFlash();
		Visual.Position = Vector2.Zero;
		base.OnDefeated();
		// Finished off as a bone pile: stay a pile instead of replaying the fall from standing.
		if (alreadyDown && Sprite?.Animation == "death")
			Sprite.Frame = Sprite.SpriteFrames.GetFrameCount("death") - 1;
	}

	// ── Animation ───────────────────────────────────────────────────────────────────────────

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null || _busy)
			return;
		string anim = Mathf.Abs(velocity.X) > 5f ? "walk" : "idle";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	protected override bool ContactDamageEnabled => !_collapsed;
}
