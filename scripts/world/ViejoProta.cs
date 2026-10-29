using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// El viejo prota — the old protagonist (FireWarrior sheets, 98x70 cells, faces right) back as a
// rival that fights the way a player does: footsies at sword range, a 3-hit combo it can stop
// early, guarding with a parry window, rolling through you, jump slashes, low sweeps, a fireball,
// and it drinks a potion when low. At half health it transforms:
// the sword ignites (FireWarriorFireSwordSpriteFrames — the same moveset with the burning blade),
// trails turn to fire, it gets faster and its combo finisher leaves fire on the floor.
// Frames used (from the sheets): attack1 f1 overhead cut, attack2 f1 big crescent, attack3 f1-3
// thrust, crouch_attack f1 low sweep to both sides, jump_attack f1 aerial cut, roll,
// block (parry flashes), spell f5 releases the fireball, transformation (12f), heal (4f).
// The sheet's shoot_bow clip is skipped (its frames are another character); slide and spell2 (flame jet) are unused.
// Every strike is telegraphed with a white body flash; neon trails reach out to each cut's reach.
public partial class ViejoProta : Enemy
{
	[ExportGroup("Spacing")]
	[Export] public float IdealDistance = 52f;
	[Export] public float RunDistance = 180f;
	[Export] public float RunSpeedMultiplier = 1.7f;

	[ExportGroup("Combo")]
	[Export] public float SwordReach = 62f;
	[Export] public float AttackCooldown = 1.1f;
	[Export] public float ComboTelegraph = 0.26f;
	[Export] public float CutKnockback = 230f;
	[Export] public float LowSweepChance = 0.2f;
	// Hit boxes measured off the sheets (scale 1.5, sprite at 1.5,-14).
	[Export] public Vector2 Cut1Center = new(24f, -10f);
	[Export] public Vector2 Cut1Size = new(50f, 58f);
	[Export] public Vector2 Cut2Center = new(24f, -16f);
	[Export] public Vector2 Cut2Size = new(56f, 68f);
	[Export] public Vector2 ThrustCenter = new(32f, 0f);
	[Export] public Vector2 ThrustSize = new(58f, 20f);
	[Export] public Vector2 SweepCenter = new(0f, 14f);
	[Export] public Vector2 SweepSize = new(122f, 24f);
	[Export] public Color SteelGlow = new(1f, 0.88f, 0.55f, 1f);
	[Export] public Color FlashTint = new(2.2f, 2.2f, 2.2f, 1f);

	[ExportGroup("Guard")]
	[Export] public float GuardChance = 0.4f;
	[Export] public float GuardRange = 90f;
	[Export] public float GuardDuration = 1f;
	[Export] public float ParryWindow = 0.22f;
	[Export] public float GuardCooldown = 2.5f;

	[ExportGroup("Evasion")]
	[Export] public float RollChance = 0.25f;
	[Export] public float RollSpeed = 300f;
	[Export] public float RollCooldown = 2.5f;
	[Export] public float PunishTelegraph = 0.12f;
	[Export] public float JumpSlashCooldown = 3.5f;
	[Export] public Vector2 JumpSlashHop = new(150f, -380f);

	[ExportGroup("Spells")]
	[Export] public float FireballCooldown = 4f;
	[Export] public float FireballMinDistance = 120f;
	[Export] public float FireballMaxDistance = 320f;
	[Export] public float FireballChance = 0.45f;
	[Export] public Vector2 FireballOffset = new(22f, -16f);
	[Export] public PackedScene GroundFireScene;

	[ExportGroup("Heal & transformation")]
	[Export] public float HealThreshold = 0.35f;
	[Export] public int MaxHeals = 2;
	[Export] public float HealFraction = 0.25f;
	[Export] public float TransformThreshold = 0.5f;
	[Export] public SpriteFrames FireSwordFrames;
	[Export] public float PhaseTwoSpeedMultiplier = 1.15f;
	[Export] public float PhaseTwoDamageMultiplier = 1.2f;
	[Export] public float DecisionInterval = 0.3f;
	[Export] public float HurtDuration = 0.25f;

	private static readonly RandomNumberGenerator Rng = new();
	private readonly DashWatcher _dash = new();
	private readonly WhiffWatcher _whiff = new();

	private EnemySlashTrail _steelTrail;
	private EnemySlashTrail _fireTrail;
	private Tween _flashTween;
	private bool _busy;
	private bool _uncancellable;
	private bool _cancel;
	private bool _guarding;
	private float _guardTime;
	private bool _parried;
	private bool _phaseTwo;
	private bool _transforming;
	private int _healsUsed;
	private float _moveOverride;
	private bool _hopping;
	private float _hurtTimer;
	private float _decisionTimer;
	private float _attackCooldownTimer;
	private float _guardCooldownTimer;
	private float _rollCooldownTimer;
	private float _jumpCooldownTimer;
	private float _fireballCooldownTimer;

	private EnemySlashTrail Trail => _phaseTwo ? _fireTrail : _steelTrail;
	private float DamageScale => _phaseTwo ? PhaseTwoDamageMultiplier : 1f;

	static ViejoProta()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_attackCooldownTimer = Rng.RandfRange(0.3f, 0.7f);
		_fireballCooldownTimer = Rng.RandfRange(0.2f, 0.8f);

		_steelTrail = new EnemySlashTrail
		{
			CoreWidth = 2.5f,
			GlowWidth = 8f,
			GlowColor = SteelGlow,
			CoreColor = new Color(1f, 1f, 0.95f, 1f),
			MinPointSpacing = 1.5f,
			SparkAmount = 10,
		};
		Visual.AddChild(_steelTrail);
		var ramp = new Gradient();
		ramp.Offsets = new[] { 0f, 0.5f, 1f };
		ramp.Colors = new[] { new Color(0.45f, 0.03f, 0f, 0f), new Color(1f, 0.25f, 0.02f, 0.8f), new Color(1f, 0.65f, 0.15f, 1f) };
		_fireTrail = new EnemySlashTrail
		{
			CoreWidth = 3.5f,
			GlowWidth = 11f,
			GlowColor = Colors.White,
			CoreColor = new Color(1f, 0.9f, 0.55f, 1f),
			GlowRamp = ramp,
			SparkProcess = GD.Load<ParticleProcessMaterial>("res://resources/particles/FlameEmberProcess.tres"),
			SparkColor = new Color(1f, 0.7f, 0.3f, 1f),
			SparkAmount = 20,
			SparkLifetime = 0.7,
		};
		Visual.AddChild(_fireTrail);

		Stats.IncomingHitInterceptor = TryGuard;
		Stats.HitTaken += _ => OnHurt();
		Stats.HitTaken += _ => _whiff.NotifyHitTaken();
	}

	private void OnHurt()
	{
		_hurtTimer = HurtDuration;
		if (_busy && !_uncancellable)
			_cancel = true;
	}

	private bool PlayerInFront(Node2D player) => (player.GlobalPosition.X >= GlobalPosition.X) == FacingRight;

	// Guarding: frontal hits are blocked; inside the parry window they're parried (riposte).
	private bool TryGuard()
	{
		if (!_guarding)
			return false;
		var player = PlayerReads.Find(this);
		if (player is null || !PlayerInFront(player))
			return false;
		_parried = _guardTime <= ParryWindow;
		Sprite.Play("block");
		ImpactEffect.SpawnAt(this, GlobalPosition + new Vector2(FacingRight ? 20f : -20f, -20f));
		Sfx.PlayAt(this, "Combat/Sword", "Sword Blocked");
		if (_parried)
			_guardTime = GuardDuration; // ends the guard: the riposte follows
		return true;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;
		float dt = (float)delta;
		_hurtTimer -= dt;
		_decisionTimer -= dt;
		_attackCooldownTimer -= dt;
		_guardCooldownTimer -= dt;
		_rollCooldownTimer -= dt;
		_jumpCooldownTimer -= dt;
		_fireballCooldownTimer -= dt;

		var player = PlayerReads.Find(this);
		_dash.Update(player, dt);
		_whiff.Update(player, dt);

		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval)
			return;
		if (_hopping && IsOnFloor() && Velocity.Y >= 0f)
			_hopping = false;

		if (player is null || !PlayerDetected || _busy || _hopping || !IsOnFloor())
			return;

		float health = Stats.CurrentHealth / (float)Mathf.Max(1, Stats.MaxHealth);
		if (!_phaseTwo && health <= TransformThreshold && FireSwordFrames is not null)
		{
			_ = Transform();
			return;
		}
		if (health <= HealThreshold && _healsUsed < MaxHeals && Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X) > 120f)
		{
			_ = Heal();
			return;
		}
		Think(player, dt);
	}

	// ── Decisions ───────────────────────────────────────────────────────────────────────────

	private void Think(Metroidvania.Player.Player player, float dt)
	{
		float distanceX = player.GlobalPosition.X - GlobalPosition.X;
		float absDistance = Mathf.Abs(distanceX);
		float toward = Mathf.Sign(distanceX);
		bool airborne = !player.IsOnFloor();

		// Reacting to a swing in front: guard (parry chance) or roll back out.
		if (_whiff.PlayerStartedSwing && absDistance <= GuardRange && PlayerInFront(player))
		{
			float roll = Rng.Randf();
			if (_guardCooldownTimer <= 0f && roll < GuardChance)
			{
				_ = Guard();
				return;
			}
			if (_rollCooldownTimer <= 0f && roll < GuardChance + RollChance)
			{
				_ = Roll(-toward, attackAfter: false);
				return;
			}
		}

		// A whiffed parry → roll through to their back during the recovery and hit from there.
		if (PlayerReads.WhiffedParryToward(player, GlobalPosition.X) && absDistance <= 100f && _rollCooldownTimer <= 0f)
		{
			_ = Roll(toward, attackAfter: true);
			return;
		}

		// A whiff / dash ending in reach → a quick 1-2 hit punish with a short wind-up.
		if ((_whiff.JustWhiffed || _dash.SinceDashEnded < 0.1f) && absDistance <= SwordReach + 15f
			&& _attackCooldownTimer <= AttackCooldown * 0.5f)
		{
			_whiff.Consume();
			_ = Combo(Rng.Randf() < 0.5f ? 1 : 2, PunishTelegraph, bypassBlock: false);
			return;
		}

		// The fireball has its own cooldown: they only wait for the melee recovery to be
		// half over, so he can cast while closing the gap after a combo knocked you back.
		if (_attackCooldownTimer > AttackCooldown * 0.5f)
			return;

		if (_attackCooldownTimer <= 0f)
		{
			// Jumping at it up close → jump slash.
			if (airborne && absDistance <= 90f && _jumpCooldownTimer <= 0f)
			{
				_ = JumpSlash(toward);
				return;
			}

			if (absDistance <= SwordReach)
			{
				if (Rng.Randf() < LowSweepChance)
					_ = LowSweep();
				else
				{
					float r = Rng.Randf();
					int hits = r < 0.3f ? 1 : r < 0.6f ? 2 : 3;
					_ = Combo(hits, ComboTelegraph, bypassBlock: false);
				}
				return;
			}
		}

		if (_decisionTimer > 0f)
			return;
		_decisionTimer = DecisionInterval * Rng.RandfRange(0.8f, 1.3f);

		if (absDistance >= FireballMinDistance && absDistance <= FireballMaxDistance && _fireballCooldownTimer <= 0f
			&& Rng.Randf() < FireballChance)
			_ = Fireball(player);
	}

	// ── Movement ────────────────────────────────────────────────────────────────────────────

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_hopping)
			return currentVelocityX;
		if (_busy)
			return _moveOverride != 0f ? (CanStepTo(Mathf.Sign(_moveOverride)) ? _moveOverride : 0f) : Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
		if (_hurtTimer > 0f)
			return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
		float absDistance = Mathf.Abs(distanceX);
		float toward = Mathf.Sign(distanceX);
		float speed = MoveSpeed * (_phaseTwo ? PhaseTwoSpeedMultiplier : 1f);
		if (absDistance > IdealDistance && CanStepTo(toward))
			return toward * speed * (absDistance > RunDistance ? RunSpeedMultiplier : 1f);
		return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
	}

	protected override bool DesiredFacingRight(Node2D player, float distanceX) => distanceX >= 0;

	protected override bool CanTurnToFacePlayer => !_busy;

	// ── Combo ───────────────────────────────────────────────────────────────────────────────

	private async Task Combo(int hits, float telegraph, bool bypassBlock)
	{
		if (!Begin())
			return;
		try
		{
			Sprite.Play("attack1");
			Sprite.Pause();
			Sprite.Frame = 0;
			FlashBody(telegraph);
			if (!await WaitChecked(telegraph))
				return;
			_uncancellable = true;

			// 1 — overhead cut.
			Sprite.Frame = 1;
			Trail.Play(new Vector2(4f, -12f), 50f, -120f, 50f, 0.08f);
			Strike(Cut1Center, Cut1Size, 1f, bypassBlock);
			if (!await Frames(2, 12f))
				return;
			if (hits >= 2)
			{
				// 2 — the big crescent.
				FacePlayer();
				Sprite.Play("attack2");
				Sprite.Pause();
				Sprite.Frame = 0;
				if (!await Frames(1, 12f))
					return;
				Sprite.Frame = 1;
				Trail.Play(new Vector2(2f, -14f), 56f, -140f, 70f, 0.08f);
				Strike(Cut2Center, Cut2Size, 1.1f, bypassBlock);
				if (!await Frames(2, 12f))
					return;
			}
			if (hits >= 3)
			{
				// 3 — the thrust finisher (phase two leaves fire where it lands).
				FacePlayer();
				Sprite.Play("attack3");
				Sprite.Pause();
				Sprite.Frame = 0;
				if (!await Frames(1, 12f))
					return;
				Sprite.Frame = 1;
				Trail.PlayThrust(new Vector2(10f, 0f), new Vector2(62f, 0f), 0.05f, 0.15f);
				Strike(ThrustCenter, ThrustSize, 1.25f, bypassBlock);
				if (_phaseTwo)
					DropFire(GlobalPosition.X + (FacingRight ? 50f : -50f));
				if (!await Frames(3, 12f))
					return;
			}
			if (!await Wait(0.2f))
				return;
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * Rng.RandfRange(0.85f, 1.2f);
	}

	private async Task LowSweep()
	{
		if (!Begin())
			return;
		try
		{
			Sprite.Play("crouch_attack");
			Sprite.Pause();
			Sprite.Frame = 0;
			FlashBody(ComboTelegraph);
			if (!await WaitChecked(ComboTelegraph))
				return;
			_uncancellable = true;
			Sprite.Frame = 1;
			Trail.Play(SweepCenter, 62f, 190f, -10f, 0.09f, 12f);
			Strike(SweepCenter, SweepSize, 1f, bypassBlock: false);
			if (!await Frames(3, 10f))
				return;
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown;
	}

	// Up and over with the aerial cut (anti-air, or to close in).
	private async Task JumpSlash(float toward)
	{
		var launch = new Vector2(toward * JumpSlashHop.X, JumpSlashHop.Y);
		if (!IsOnFloor() || !LandsSafely(launch, 40f) || !Begin())
			return;
		_jumpCooldownTimer = JumpSlashCooldown;
		try
		{
			FlashBody(0.15f);
			Velocity = launch;
			_hopping = true;
			Sprite.Play("jump");
			// Cut near the top of the arc.
			while (Velocity.Y < -60f)
			{
				await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
				if (!IsInstanceValid(this) || IsQueuedForRemoval)
					return;
			}
			_uncancellable = true;
			FacePlayer();
			Sprite.Play("jump_attack");
			Sprite.Pause();
			Sprite.Frame = 1;
			Trail.Play(new Vector2(4f, -12f), 50f, -120f, 60f, 0.08f);
			Strike(Cut1Center, Cut1Size, 1f, bypassBlock: false);
			if (!await Frames(3, 12f))
				return;
			float t = 0f;
			while (_hopping && t < 1.5f)
			{
				await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
				if (!IsInstanceValid(this) || IsQueuedForRemoval)
					return;
				t += (float)GetPhysicsProcessDeltaTime();
			}
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * 0.8f;
	}

	// ── Evasion ─────────────────────────────────────────────────────────────────────────────

	// Roll with i-frames: backward out of a swing, or forward through a turtling player to hit
	// their back (those hits ignore the block).
	private async Task Roll(float direction, bool attackAfter)
	{
		if (!Begin(slot: false))
			return;
		_rollCooldownTimer = RollCooldown;
		_uncancellable = true;
		FaceTowards(GlobalPosition.X + direction);
		Sprite.Play("roll");
		_moveOverride = direction * RollSpeed;
		Stats.ExternalInvulnerable = true;
		try
		{
			if (!await Wait(7f / 12f))
				return;
		}
		finally
		{
			if (IsInstanceValid(this))
			{
				Stats.ExternalInvulnerable = false;
				_moveOverride = 0f;
			}
			Finish(slot: false);
		}
		if (attackAfter)
		{
			FacePlayer();
			_attackCooldownTimer = 0f;
			_ = Combo(Rng.Randf() < 0.5f ? 1 : 2, 0.1f, bypassBlock: true);
		}
	}

	private async Task Guard()
	{
		if (!Begin(slot: false))
			return;
		_guardCooldownTimer = GuardCooldown;
		_uncancellable = true;
		_guarding = true;
		_parried = false;
		_guardTime = 0f;
		Sprite.Play("idle_block");
		try
		{
			while (_guardTime < GuardDuration)
			{
				await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
				if (!IsInstanceValid(this) || IsQueuedForRemoval)
					return;
				_guardTime += (float)GetPhysicsProcessDeltaTime();
			}
		}
		finally
		{
			if (IsInstanceValid(this))
				_guarding = false;
			Finish(slot: false);
		}
		if (_parried)
		{
			// Parried → instant thrust riposte.
			_attackCooldownTimer = 0f;
			_ = Riposte();
		}
	}

	private async Task Riposte()
	{
		if (!Begin())
			return;
		try
		{
			FacePlayer();
			_uncancellable = true;
			Sprite.Play("attack3");
			Sprite.Pause();
			Sprite.Frame = 1;
			Trail.PlayThrust(new Vector2(10f, 0f), new Vector2(62f, 0f), 0.04f, 0.15f);
			Strike(ThrustCenter, ThrustSize, 1.4f, bypassBlock: false);
			if (!await Frames(3, 12f))
				return;
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * 0.6f;
	}

	// ── Spells ──────────────────────────────────────────────────────────────────────────────

	private async Task Fireball(Metroidvania.Player.Player player)
	{
		if (!Begin())
			return;
		_fireballCooldownTimer = FireballCooldown;
		try
		{
			FacePlayer();
			Sprite.Play("spell");
			FlashBody(0.4f);
			// Fire gathers in the hand (frames 1-4), released on frame 5.
			if (!await WaitChecked(5f / 10f))
				return;
			_uncancellable = true;
			player = PlayerReads.Find(this);
			if (player is not null)
			{
				Vector2 origin = GlobalPosition + new Vector2(FacingRight ? FireballOffset.X : -FireballOffset.X, FireballOffset.Y);
				Vector2 aim = PlayerReads.PredictAimDirection(origin, player, 330f, 0.7f, 0.3f, new Vector2(0f, -8f));
				var ball = new ProtaFireball
				{
					Direction = aim,
					ThrowerStats = Stats,
					DamageMultiplier = 0.8f * DamageScale,
					GroundFireScene = _phaseTwo ? GroundFireScene : null,
				};
				GetTree().CurrentScene.AddChild(ball);
				ball.GlobalPosition = origin;
			}
			if (!await Wait(4f / 10f))
				return;
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * 0.8f;
	}

	private void DropFire(float x)
	{
		if (GroundFireScene is null)
			return;
		var query = PhysicsRayQueryParameters2D.Create(new Vector2(x, GlobalPosition.Y - 10f), new Vector2(x, GlobalPosition.Y + BodyBottom + 60f),
			1u | PhysicsLayers.ClimbableWalls | PhysicsLayers.OneWayPlatforms, new Godot.Collections.Array<Rid> { GetRid() });
		var hit = GetWorld2D().DirectSpaceState.IntersectRay(query);
		if (hit.Count == 0)
			return;
		Vector2 at = (Vector2)hit["position"];
		Node scene = GetTree().CurrentScene;
		PackedScene fireScene = GroundFireScene;
		Callable.From(() =>
		{
			var fire = fireScene.Instantiate<Node2D>();
			scene.AddChild(fire);
			fire.GlobalPosition = at;
		}).CallDeferred();
	}

	// ── Heal & transformation ───────────────────────────────────────────────────────────────

	// Drinks a potion (interruptible — pressure him while he drinks).
	private async Task Heal()
	{
		if (!Begin(slot: false))
			return;
		_healsUsed++;
		try
		{
			Sprite.Play("heal");
			HealMotes();
			if (!await WaitChecked(4f / 5f))
				return;
			Stats.Heal(Mathf.RoundToInt(Stats.MaxHealth * HealFraction));
		}
		finally
		{
			Finish(slot: false);
		}
	}

	private void HealMotes()
	{
		var ramp = new Gradient();
		ramp.SetColor(0, new Color(1f, 0.95f, 0.6f, 0.9f));
		ramp.SetColor(1, new Color(0.5f, 1f, 0.5f, 0f));
		var motes = new GpuParticles2D
		{
			Amount = 18,
			Lifetime = 0.8,
			OneShot = false,
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			ProcessMaterial = new ParticleProcessMaterial
			{
				Direction = new Vector3(0f, -1f, 0f),
				Spread = 20f,
				InitialVelocityMin = 30f,
				InitialVelocityMax = 60f,
				Gravity = Vector3.Zero,
				ScaleMin = 0.06f,
				ScaleMax = 0.12f,
				EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
				EmissionBoxExtents = new Vector3(14f, 20f, 0f),
				ColorRamp = new GradientTexture1D { Gradient = ramp },
			},
		};
		AddChild(motes);
		motes.Emitting = true;
		GetTree().CreateTimer(0.8).Timeout += () =>
		{
			if (IsInstanceValid(motes))
			{
				motes.Emitting = false;
				GetTree().CreateTimer(0.9).Timeout += () =>
				{
					if (IsInstanceValid(motes))
						motes.QueueFree();
				};
			}
		};
	}

	// Half health: the sword ignites (invulnerable while it happens), then the fire-sword moveset.
	private async Task Transform()
	{
		if (!Begin(slot: false))
			return;
		_transforming = true;
		_uncancellable = true;
		Stats.ExternalInvulnerable = true;
		try
		{
			Sprite.Play("transformation");
			var palette = new GroundSlamFx.Palette
			{
				Hot = new Color(1f, 0.4f, 0.06f, 1f),
				Core = new Color(1f, 0.92f, 0.7f, 1f),
				Rock = new Color(0.3f, 0.2f, 0.15f, 1f),
				Dust = new Color(0.35f, 0.25f, 0.2f, 0.5f),
				Light = new Color(1f, 0.5f, 0.2f, 1f),
			};
			for (int burst = 0; burst < 3; burst++)
			{
				if (!await Wait(0.45f))
					return;
				GroundSlamFx.Chip(this, GlobalPosition + new Vector2(0f, BodyBottom), 0.8f, palette);
			}
			if (!await Wait(0.2f))
				return;
			GroundSlamFx.Impact(this, GlobalPosition + new Vector2(0f, BodyBottom), 0.9f, palette);
			if (PlayerReads.Find(this) is { } player && player.GlobalPosition.DistanceTo(GlobalPosition) < 700f)
				player.ShakeCamera(4f, 0.3f);
			_phaseTwo = true;
			Sprite.SpriteFrames = FireSwordFrames;
			Sprite.Play("idle");
		}
		finally
		{
			if (IsInstanceValid(this))
			{
				Stats.ExternalInvulnerable = false;
				_transforming = false;
			}
			Finish(slot: false);
		}
	}

	// ── Shared ──────────────────────────────────────────────────────────────────────────────

	private bool Begin(bool slot = true)
	{
		if (slot)
		{
			if (!EnemyCombatCoordinator.TryAcquireAttackSlot())
			{
				_attackCooldownTimer = 0.25f;
				return false;
			}
			HoldingAttackSlot = true;
		}
		_busy = true;
		_cancel = false;
		FacePlayer();
		return true;
	}

	private void Finish(bool slot = true)
	{
		if (!IsInstanceValid(this))
			return;
		_busy = false;
		_cancel = false;
		_uncancellable = false;
		ClearFlash();
		if (slot && HoldingAttackSlot)
		{
			EnemyCombatCoordinator.ReleaseAttackSlot();
			HoldingAttackSlot = false;
		}
	}

	// One damage check; returns whether it hit. bypassBlock: hits from behind ignore the
	// player's (omnidirectional) block — used after rolling through a turtling player.
	private bool Strike(Vector2 center, Vector2 size, float damageMultiplier, bool bypassBlock)
	{
		foreach (var (body, stats) in PlayersIn(center, size))
		{
			int damage = Mathf.Max(1, Mathf.RoundToInt(Stats.AttackPower * damageMultiplier * DamageScale));
			var player = body as Metroidvania.Player.Player;
			bool fromBehind = player is not null
				&& (player.IsFacingRight ? GlobalPosition.X < player.GlobalPosition.X : GlobalPosition.X > player.GlobalPosition.X);
			if (bypassBlock && fromBehind && player.IsBlocking)
			{
				System.Func<bool> interceptor = stats.IncomingHitInterceptor;
				stats.IncomingHitInterceptor = null;
				stats.TakeDamage(damage, element: _phaseTwo ? DamageElement.Fire : DamageElement.Normal);
				stats.IncomingHitInterceptor = interceptor;
			}
			else
			{
				stats.TakeDamage(damage, element: _phaseTwo ? DamageElement.Fire : DamageElement.Normal);
			}
			if (body.HasMethod("ApplyKnockback"))
				body.Call("ApplyKnockback", new Vector2(Mathf.Sign(body.GlobalPosition.X - GlobalPosition.X), -0.2f).Normalized(), CutKnockback);
			BloodEffect.SpawnAt(this, body.GlobalPosition);
			return true;
		}
		return false;
	}

	private System.Collections.Generic.IEnumerable<(Node2D, Stats)> PlayersIn(Vector2 center, Vector2 size)
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
			yield return (body, stats);
			yield break;
		}
	}

	private async Task<bool> Frames(int count, float fps)
	{
		for (int i = 0; i < count; i++)
		{
			await ToSignal(GetTree().CreateTimer(1f / fps), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return false;
			int next = Sprite.Frame + 1;
			if (next < Sprite.SpriteFrames.GetFrameCount(Sprite.Animation))
				Sprite.Frame = next;
		}
		return true;
	}

	private async Task<bool> Wait(float seconds)
	{
		await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
		return IsInstanceValid(this) && !IsQueuedForRemoval;
	}

	// Like Wait, but a hit during it cancels the action (wind-ups only).
	private async Task<bool> WaitChecked(float seconds)
	{
		float t = 0f;
		while (t < seconds)
		{
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return false;
			if (_cancel)
			{
				_attackCooldownTimer = AttackCooldown * 0.5f;
				return false;
			}
			t += (float)GetPhysicsProcessDeltaTime();
		}
		return true;
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
		_guarding = false;
		Stats.ExternalInvulnerable = false;
		ClearFlash();
		base.OnDefeated();
	}

	// ── Animation ───────────────────────────────────────────────────────────────────────────

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null || _busy)
			return;
		string anim;
		if (!IsOnFloor())
			anim = velocity.Y < 0f ? "jump" : "fall";
		else if (_hurtTimer > 0f)
			anim = "hurt";
		else if (Mathf.Abs(velocity.X) > MoveSpeed * 1.1f)
			anim = "run";
		else if (Mathf.Abs(velocity.X) > 5f)
			anim = "walk";
		else
			anim = "idle";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	protected override bool ContactDamageEnabled => false;
}
