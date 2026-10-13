using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Bandido pesado / Heavy Bandit (Bandits pack, 48x48 cells, faces LEFT — mirrored on the scene like
// Bandido3, its pack sibling). A tank: armoured from the front, never flinches, and charges like a
// bull. Sheet beats used:
//   attack (14fps): 0-3 sword raised overhead (held as the wind-up), 4-6 the big cleave sweeping
//     over and down the front, 7 recovery.
//   run 5-7: sword levelled forward like a lance — the charge.
//   recover: lying on the floor and getting up — the stun after charging into a wall.
//   combat_idle: the guard stance it advances in; jump: the leap of the leaping cleave.
// Moves:
//   Front armour — while it faces you, hits from the front lose FrontArmor damage (sparks fly);
//     from behind, or while it's down, they land in full. Wind-ups can't be interrupted.
//   Cleave — sometimes a double cleave; a slower charged cleave hits much harder.
//   Charge — from mid range it levels the sword and rushes; it shoves you back even through your
//     block. If it runs into a wall it's knocked flat (recover) and wide open; it stops at ledges.
//   Leaping cleave — hops in and cleaves on landing.
//   Punish — a whiffed swing or a dash ending in reach is answered with a quick cleave.
// Telegraphs: the body flashes white before every attack; bandit-red neon trails.
public partial class Bandido7 : Enemy
{
	[ExportGroup("Armour")]
	[Export] public int FrontArmor = 5;

	[ExportGroup("Cleave")]
	[Export] public float CleaveRange = 58f;
	[Export] public float AttackCooldown = 1.4f;
	[Export] public float CleaveTelegraph = 0.35f;
	[Export] public float ChargedTelegraph = 0.75f;
	[Export] public float ChargedChance = 0.25f;
	[Export] public float DoubleChance = 0.35f;
	[Export] public float CleaveDamageMultiplier = 1f;
	[Export] public float ChargedDamageMultiplier = 1.6f;
	[Export] public float CleaveKnockback = 260f;
	// Measured off attack frame 4 (scale 1.8 mirrored, sprite at 3.6,-4.8): the arc reaches ~47px
	// in front, from over the head down to the feet.
	[Export] public Vector2 CleaveHitboxCenter = new(28f, -8f);
	[Export] public Vector2 CleaveHitboxSize = new(54f, 66f);
	[Export] public Color TrailGlowColor = new(1f, 0.05f, 0.12f, 1f);
	[Export] public Color TrailCoreColor = new(1f, 0.6f, 0.62f, 1f);
	[Export] public Color FlashTint = new(2.2f, 2.2f, 2.2f, 1f);

	[ExportGroup("Charge")]
	[Export] public float ChargeMinDistance = 110f;
	[Export] public float ChargeMaxDistance = 300f;
	[Export] public float ChargeChance = 0.5f;
	[Export] public float ChargeCooldown = 4.5f;
	[Export] public float ChargeTelegraph = 0.5f;
	[Export] public float ChargeSpeed = 380f;
	[Export] public float ChargeMaxTime = 1.1f;
	[Export] public float ChargeDamageMultiplier = 1.2f;
	[Export] public float ChargeKnockback = 420f;
	[Export] public Vector2 ChargeHitboxCenter = new(26f, -6f);
	[Export] public Vector2 ChargeHitboxSize = new(38f, 34f);
	[Export] public float StunTime = 1.1f;
	[Export] public Color AfterimageColor = new(1f, 0.1f, 0.18f, 1f);

	[ExportGroup("Leap")]
	[Export] public float LeapMinDistance = 85f;
	[Export] public float LeapMaxDistance = 170f;
	[Export] public float LeapChance = 0.3f;
	[Export] public float LeapCooldown = 4f;
	[Export] public float LeapVelocityY = -340f;

	[Export] public float DecisionInterval = 0.4f;

	private const float AttackFps = 14f;

	private static readonly RandomNumberGenerator Rng = new();
	private readonly DashWatcher _dash = new();
	private readonly WhiffWatcher _whiff = new();

	private EnemySlashTrail _trail;
	private Tween _flashTween;
	private int _baseDefense;
	private bool _busy;
	private bool _stunned;
	private bool _hopping;
	private float _moveOverride;
	private float _decisionTimer;
	private float _attackCooldownTimer;
	private float _chargeCooldownTimer;
	private float _leapCooldownTimer;
	private float _afterimageTimer;

	static Bandido7()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_baseDefense = Stats.Defense;
		_attackCooldownTimer = Rng.RandfRange(0.4f, 0.9f);
		_chargeCooldownTimer = Rng.RandfRange(0.8f, 1.8f);
		_trail = new EnemySlashTrail
		{
			CoreWidth = 3.5f,
			GlowWidth = 11f,
			GlowColor = TrailGlowColor,
			CoreColor = TrailCoreColor,
			MinPointSpacing = 1.5f,
			SparkAmount = 14,
		};
		Visual.AddChild(_trail);
		Stats.HitTaken += _ => OnHit();
		Stats.HitTaken += _ => _whiff.NotifyHitTaken();
	}

	// Frontal hits bounce off the armour with sparks (the damage was already reduced by Defense).
	private void OnHit()
	{
		if (FrontArmourActive() && PlayerReads.Find(this) is { } player)
			ImpactEffect.SpawnAt(this, (GlobalPosition + player.GlobalPosition) * 0.5f + new Vector2(0f, -14f));
	}

	private bool PlayerInFront(Node2D player) => (player.GlobalPosition.X >= GlobalPosition.X) == FacingRight;

	private bool FrontArmourActive() => !_stunned && PlayerReads.Find(this) is { } p && PlayerInFront(p);

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;
		float dt = (float)delta;
		_decisionTimer -= dt;
		_attackCooldownTimer -= dt;
		_chargeCooldownTimer -= dt;
		_leapCooldownTimer -= dt;

		var player = PlayerReads.Find(this);
		_dash.Update(player, dt);
		_whiff.Update(player, dt);
		// Armour faces the player; down on the floor it's soft.
		Stats.Defense = _stunned ? Mathf.Max(0, _baseDefense - 2) : FrontArmourActive() ? _baseDefense + FrontArmor : _baseDefense;

		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval)
			return;
		if (_hopping && IsOnFloor() && Velocity.Y >= 0f)
			_hopping = false;
		if (player is null || !PlayerDetected || _busy || _hopping || !IsOnFloor())
			return;
		Think(player);
	}

	// ── Decisions ───────────────────────────────────────────────────────────────────────────

	private void Think(Metroidvania.Player.Player player)
	{
		float distanceX = player.GlobalPosition.X - GlobalPosition.X;
		float absDistance = Mathf.Abs(distanceX);
		float toward = Mathf.Sign(distanceX);

		// A whiff / dash ending in reach → quick cleave.
		if ((_whiff.JustWhiffed || _dash.SinceDashEnded < 0.1f) && absDistance <= CleaveRange + 10f
			&& _attackCooldownTimer <= AttackCooldown * 0.5f)
		{
			_whiff.Consume();
			_ = Cleave(0.15f, CleaveDamageMultiplier, allowDouble: false);
			return;
		}

		// The charge and leap have their own cooldowns: they only wait for the cleave recovery to be
		// half over, so it can rush you while you're still reeling from the knockback.
		if (_attackCooldownTimer > AttackCooldown * 0.5f)
			return;

		if (absDistance <= CleaveRange && _attackCooldownTimer <= 0f)
		{
			if (Rng.Randf() < ChargedChance)
				_ = Cleave(ChargedTelegraph, ChargedDamageMultiplier, allowDouble: false);
			else
				_ = Cleave(CleaveTelegraph, CleaveDamageMultiplier, allowDouble: true);
			return;
		}

		if (_decisionTimer > 0f)
			return;
		_decisionTimer = DecisionInterval * Rng.RandfRange(0.8f, 1.3f);

		bool turtling = PlayerReads.IsBlockingToward(player, GlobalPosition.X);
		if (absDistance >= ChargeMinDistance && absDistance <= ChargeMaxDistance && _chargeCooldownTimer <= 0f
			&& Rng.Randf() < ChargeChance + (turtling ? 0.3f : 0f))
		{
			_ = Charge(toward);
			return;
		}
		if (absDistance >= LeapMinDistance && absDistance <= LeapMaxDistance && _leapCooldownTimer <= 0f
			&& player.IsOnFloor() && Rng.Randf() < LeapChance)
			_ = LeapCleave(player);
	}

	// ── Movement ────────────────────────────────────────────────────────────────────────────

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_hopping)
			return currentVelocityX;
		if (_busy)
			return _moveOverride;
		float toward = Mathf.Sign(distanceX);
		if (Mathf.Abs(distanceX) > CleaveRange * 0.8f && CanStepTo(toward))
			return toward * MoveSpeed;
		return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
	}

	protected override bool DesiredFacingRight(Node2D player, float distanceX) => distanceX >= 0;

	protected override bool CanTurnToFacePlayer => !_busy;

	// ── Cleave ──────────────────────────────────────────────────────────────────────────────

	private async Task Cleave(float telegraph, float damageMultiplier, bool allowDouble)
	{
		if (!Begin())
			return;
		try
		{
			bool charged = damageMultiplier > CleaveDamageMultiplier + 0.01f;
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
				_trail.Play(new Vector2(6f, -10f), charged ? 60f : 54f, -130f, 80f, 0.09f);
				Strike(CleaveHitboxCenter, CleaveHitboxSize, damageMultiplier, CleaveKnockback * (charged ? 1.4f : 1f), shoveThroughBlock: charged);
				if (charged)
					Shake(3f, 0.18f);
				for (int f = 5; f <= 7; f++)
				{
					if (!await Wait(1f / AttackFps))
						return;
					Sprite.Frame = f;
				}
			}
			if (!await Wait(0.25f))
				return;
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * Rng.RandfRange(0.85f, 1.2f);
	}

	// ── Charge ──────────────────────────────────────────────────────────────────────────────

	private async Task Charge(float direction)
	{
		if (!Begin())
			return;
		_chargeCooldownTimer = ChargeCooldown;
		bool stunned = false;
		try
		{
			FaceTowards(GlobalPosition.X + direction);
			Sprite.Play("combat_idle");
			FlashBody(ChargeTelegraph);
			if (!await Wait(ChargeTelegraph))
				return;

			// Sword levelled: loop run frames 5-7 while rushing.
			Sprite.Play("run");
			Sprite.Pause();
			bool hit = false;
			float t = 0f;
			float frameTimer = 0f;
			int frame = 5;
			Sprite.Frame = frame;
			_moveOverride = direction * ChargeSpeed;
			while (t < ChargeMaxTime)
			{
				await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
				if (!IsInstanceValid(this) || IsQueuedForRemoval)
					return;
				float dt = (float)GetPhysicsProcessDeltaTime();
				t += dt;
				frameTimer += dt;
				if (frameTimer >= 1f / 12f)
				{
					frameTimer = 0f;
					frame = frame >= 7 ? 5 : frame + 1;
					Sprite.Frame = frame;
				}
				Afterimage(dt);

				if (!hit)
					hit = Strike(ChargeHitboxCenter, ChargeHitboxSize, ChargeDamageMultiplier, ChargeKnockback, shoveThroughBlock: true);

				// Into a wall → knocked flat. Ledge ahead → dig in and stop.
				if (TestMove(GlobalTransform, new Vector2(direction * 8f, 0f)))
				{
					stunned = true;
					break;
				}
				if (!HasGroundAhead(direction))
					break;
			}
			_moveOverride = 0f;

			if (stunned)
			{
				Shake(4f, 0.25f);
				ImpactEffect.SpawnAt(this, GlobalPosition + new Vector2(direction * 20f, -10f));
				_stunned = true;
				Sprite.Play("recover");
				Sprite.Pause();
				Sprite.Frame = 0;
				// Lies there, then the get-up frames play out.
				if (!await Wait(StunTime))
					return;
				Sprite.Play("recover");
				if (!await Wait(7f / 10f))
					return;
				_stunned = false;
			}
			else
			{
				// Skid to a stop.
				Sprite.Play("combat_idle");
				if (!await Wait(0.3f))
					return;
			}
		}
		finally
		{
			if (IsInstanceValid(this))
			{
				_moveOverride = 0f;
				_stunned = false;
			}
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * 0.6f;
	}

	private void Afterimage(float dt)
	{
		_afterimageTimer -= dt;
		if (_afterimageTimer > 0f)
			return;
		_afterimageTimer = 0.045f;
		Texture2D frame = Sprite?.SpriteFrames?.GetFrameTexture(Sprite.Animation, Sprite.Frame);
		if (frame is null || GetTree().CurrentScene is not Node scene)
			return;
		var ghost = new Sprite2D
		{
			Texture = frame,
			Centered = Sprite.Centered,
			Offset = Sprite.Offset,
			FlipH = Sprite.FlipH,
			Material = Metroidvania.Player.NeonSilhouette.CreateMaterial(AfterimageColor, 1.5f),
			Modulate = new Color(1f, 1f, 1f, 0.55f),
			ZIndex = ZIndex - 1,
		};
		scene.AddChild(ghost);
		ghost.GlobalTransform = Sprite.GlobalTransform;
		Tween fade = ghost.CreateTween();
		fade.TweenProperty(ghost, "modulate:a", 0f, 0.22f);
		fade.TweenCallback(Callable.From(ghost.QueueFree));
	}

	// ── Leaping cleave ──────────────────────────────────────────────────────────────────────

	private async Task LeapCleave(Metroidvania.Player.Player player)
	{
		float airTime = 2f * -LeapVelocityY / Mathf.Max(1f, Gravity);
		float dx = player.GlobalPosition.X - GlobalPosition.X;
		var launch = new Vector2(Mathf.Clamp(dx / airTime, -260f, 260f), LeapVelocityY);
		if (!LandsSafely(launch, 40f) || !Begin())
			return;
		_leapCooldownTimer = LeapCooldown;
		try
		{
			FlashBody(0.2f);
			Velocity = launch;
			_hopping = true;
			Sprite.Play("jump");
			float t = 0f;
			while (_hopping && t < airTime + 0.6f)
			{
				await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
				if (!IsInstanceValid(this) || IsQueuedForRemoval)
					return;
				t += (float)GetPhysicsProcessDeltaTime();
				// Raise the sword on the way down.
				if (Velocity.Y > 0f && Sprite.Animation != "attack")
				{
					Sprite.Play("attack");
					Sprite.Pause();
					Sprite.Frame = 3;
				}
			}
			FacePlayer();
			Sprite.Play("attack");
			Sprite.Pause();
			Sprite.Frame = 4;
			_trail.Play(new Vector2(6f, -10f), 56f, -130f, 80f, 0.08f);
			Strike(CleaveHitboxCenter, CleaveHitboxSize, CleaveDamageMultiplier * 1.2f, CleaveKnockback, shoveThroughBlock: false);
			Shake(2.5f, 0.15f);
			for (int f = 5; f <= 7; f++)
			{
				if (!await Wait(1f / AttackFps))
					return;
				Sprite.Frame = f;
			}
			if (!await Wait(0.3f))
				return;
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * 0.7f;
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
		FacePlayer();
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

	// One damage check; returns whether it connected. shoveThroughBlock: the knockback lands even
	// if the player blocks the damage (the charge / charged cleave bash them back regardless).
	private bool Strike(Vector2 center, Vector2 size, float damageMultiplier, float knockback, bool shoveThroughBlock)
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
			if (hit["collider"].AsGodotObject() is not Node2D body || !body.IsInGroup("player"))
				continue;
			if (body.GetNodeOrNull<Stats>("Stats") is not { } stats || stats.IsInvulnerable)
				continue;
			int before = stats.CurrentHealth;
			stats.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(Stats.AttackPower * damageMultiplier)));
			bool landed = stats.CurrentHealth < before;
			if ((landed || shoveThroughBlock) && body.HasMethod("ApplyKnockbackThroughBlock"))
				body.Call(shoveThroughBlock ? "ApplyKnockbackThroughBlock" : "ApplyKnockback", new Vector2(Mathf.Sign(body.GlobalPosition.X - GlobalPosition.X), -0.25f).Normalized(), knockback);
			if (landed)
				BloodEffect.SpawnAt(this, body.GlobalPosition);
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
		Stats.Defense = _baseDefense;
		ClearFlash();
		base.OnDefeated();
	}

	// ── Animation ───────────────────────────────────────────────────────────────────────────

	// Advances in its guard stance; runs only when far.
	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null || _busy)
			return;
		string anim = !IsOnFloor() ? "jump"
			: Mathf.Abs(velocity.X) > 5f ? "run"
			: "combat_idle";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	protected override bool ContactDamageEnabled => false;
}
