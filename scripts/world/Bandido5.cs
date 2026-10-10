using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// Salvaje/WildBandit — a berserker with claws. Its own class (it used to inherit
// BandidoCorpulento's artillery AI wholesale). The sheet's single attack is a crouch (frames 0-3),
// claws raised in the air (4-7), the slam into the floor (8), sprawled (9-12) and a get-up (13-15);
// everything below is built out of those frames:
//   - Leap slam, from almost any range (LeapMinDistance..LeapMaxDistance — no dead zone between
//     its in-place slam and the leap, which is what used to leave it standing there doing
//     nothing): crouch + body flash, then a real jump arc aimed where the player is GOING (leads
//     their run), steering toward them in the air, slamming on the actual landing — a big impact
//     plus earth spikes erupting to both sides. The arc height scales with distance and is capped
//     under the ceiling, so it still works in low caves; if it can't leap (no safe landing, ceiling
//     too low) it rushes in on foot instead of fizzling.
//   - In-place slam when the player is right there: claw streaks and a wide shockwave of earth
//     spikes marching forward (SlamTileCount tiles), each tile warned by a glowing crack just
//     before it erupts; stops at a pit. Enraged, it also sends a short wave backward.
//   - Anti-air: jumping over it (to dodge the shockwave) gets met by a straight-up claw leap.
//   - A raised shield barely slows it down (BerserkBlockEngageChance).
//   - Rage below RageHealthFraction: faster, shorter cooldowns and recovery, chains up to
//     MaxChainLeaps extra leaps, ember aura.
// All FX are hand-built in code (GroundSlamFx + EnemySlashTrail claws + neon afterimages).
public partial class Bandido5 : MeleeEnemy
{
	// Attack.png plays at 14fps (see WildBanditSpriteFrames.tres): the claws hit the floor on
	// frame 8, so the in-place slam's lead-in is 8/14 (set on the scene).
	[Export] public float WindupDuration = 8f / 14f;
	[Export] public float BerserkBlockEngageChance = 0.85f;

	// ── Shockwave ──
	[ExportGroup("Shockwave")]
	[Export] public float SlamTileSpacing = 52f;
	[Export] public int SlamTileCount = 3;
	[Export] public float SlamStrikeGap = 0.11f;
	[Export] public float SlamWarningLead = 0.12f;
	[Export] public float SlamActiveDuration = 0.22f;
	[Export] public float SpikeHoldDuration = 0.3f;
	[Export] public int SlamDamage = 15;
	[Export] public Vector2 SlamHitboxSize = new(48f, 34f);
	[Export] public int RageBackwardTiles = 2;
	[Export] public float SlamShake = 3f;

	// ── Telegraph ──
	// The body flashes during the crouch, same as Perrito1 (no colored glow).
	[ExportGroup("Telegraph")]
	[Export] public Color FlashTint = new(2.2f, 2.2f, 2.2f, 1f);
	[Export] public int FlashBlinks = 2;
	// Duration of frames 0-3 (the crouch) of the in-place slam.
	[Export] public float SlamFlashDuration = 4f / 14f;

	// ── Claws ──
	// Three parallel streaks raking down in front on the slam frame, swept out past the claws to
	// the slam's reach (trails go ahead of the weapon). Measured off Attack.png frame 8: the baked
	// claw smear runs from high-front down to the floor in front of the body.
	[ExportGroup("Claws")]
	[Export] public Vector2 ClawCenter = new(6f, -14f);
	[Export] public float ClawRadius = 50f;
	[Export] public float ClawSpacing = 8f;
	[Export] public float ClawStartAngle = -80f;
	[Export] public float ClawEndAngle = 40f;
	[Export] public float ClawDuration = 0.1f;
	[Export] public Color ClawGlowColor = new(1f, 0.2f, 0.05f, 1f);
	[Export] public Color ClawCoreColor = new(1f, 0.85f, 0.6f, 1f);

	// ── Leap ──
	[ExportGroup("Leap")]
	[Export] public float LeapMinDistance = 45f;
	[Export] public float LeapMaxDistance = 260f;
	[Export] public float LeapCooldown = 2.6f;
	[Export] public float LeapChance = 0.65f;
	[Export] public float DecisionInterval = 0.35f;
	// Held on the deepest crouch frame while flashing, before it jumps.
	[Export] public float LeapCrouchTime = 0.34f;
	[Export] public int LeapCrouchFrame = 2;
	// Arc apex height, from a short hop (close) to a full leap (far); capped under the ceiling.
	[Export] public float LeapApexMin = 45f;
	[Export] public float LeapApexMax = 105f;
	[Export] public float CeilingMargin = 12f;
	[Export] public float LeapMaxSpeedX = 380f;
	// How much of the player's current run it leads (0 = where they are, 1 = full prediction).
	[Export] public float LeapLead = 0.7f;
	// Mid-air homing toward the player's X.
	[Export] public float AirSteerAccel = 700f;
	[Export] public int LeapShockwaveTilesPerSide = 2;
	[Export] public float LeapImpactScale = 1.2f;
	[Export] public float LeapShake = 6f;
	// Sprawled on the floor after a slam — its punish window.
	[Export] public float RecoveryTime = 0.42f;

	// ── Anti-air ──
	[ExportGroup("Anti-air")]
	[Export] public float AntiAirRange = 95f;
	[Export] public float AntiAirMinHeight = 30f;
	[Export] public float AntiAirCooldown = 2.2f;
	[Export] public float AntiAirTelegraph = 0.14f;
	[Export] public float AntiAirVelocityY = -420f;
	[Export] public Vector2 AntiAirHitboxOffset = new(10f, -48f);

	// ── Charge / rage ──
	[ExportGroup("Charge & Rage")]
	[Export] public float ChargeDistance = 280f;
	[Export] public float ChargeSpeed = 150f;
	// Fallback when a leap isn't possible: a short sprint into the in-place slam.
	[Export] public float RushSpeed = 210f;
	[Export] public float RushMaxTime = 1.1f;
	[Export] public float RageHealthFraction = 0.5f;
	[Export] public float RageSpeedMultiplier = 1.5f;
	[Export] public float RageCooldownMultiplier = 0.6f;
	[Export] public float RageRecoveryTime = 0.22f;
	[Export] public float ChainLeapChance = 0.75f;
	[Export] public int MaxChainLeaps = 2;
	[Export] public Color RageTint = new(1f, 0.72f, 0.72f, 1f);

	// ── Afterimages ──
	[ExportGroup("Afterimages")]
	[Export] public float AfterimageInterval = 0.04f;
	[Export] public float AfterimageLifetime = 0.25f;
	[Export] public float AfterimageAlpha = 0.55f;
	[Export] public float AfterimageIntensity = 1.6f;
	[Export] public Color AfterimageColor = new(1f, 0.25f, 0.08f, 1f);

	// Attack.png frame layout (see the header).
	private const int FirstAirFrame = 4;
	private const int LastAirFrame = 7;
	private const int SlamFrame = 8;
	private const float SheetFps = 14f;

	private GroundSlamFx.Palette _palette = GroundSlamFx.Default;
	private readonly EnemySlashTrail[] _claws = new EnemySlashTrail[3];
	private GpuParticles2D _rageAura;
	private Tween _flashTween;

	private bool _airborneAttack;
	private bool _leapHoming;
	private float _leapCooldownTimer;
	private float _antiAirCooldownTimer;
	private float _decisionTimer;
	private float _afterimageTimer;
	private bool _enraged;
	private bool _chainPending;
	private int _chainCount;
	private bool _rushing;
	private float _rushTimer;
	private static readonly RandomNumberGenerator Rng = new();

	static Bandido5()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		BlockEngageChance = BerserkBlockEngageChance;
		_leapCooldownTimer = Rng.RandfRange(0.4f, 1.2f);

		for (int i = 0; i < _claws.Length; i++)
		{
			_claws[i] = new EnemySlashTrail
			{
				CoreWidth = 1.6f,
				GlowWidth = 5.5f,
				GlowColor = ClawGlowColor,
				CoreColor = ClawCoreColor,
				MinPointSpacing = 1.5f,
				SparkAmount = 6,
				SparkLifetime = 0.25,
			};
			Visual.AddChild(_claws[i]);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
		{
			FallAsCorpse((float)delta);
			return;
		}
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval)
			return;
		Think((float)delta);
		UpdateAfterimages((float)delta);
	}

	// Dead with the "death" animation (ExplodeOnDeath off): the base stops all physics, which would
	// leave a body killed mid-leap hanging in the air — so it drops to the floor first. The rage
	// aura stops smoldering on the corpse.
	private void FallAsCorpse(float dt)
	{
		if (_rageAura is not null && _rageAura.Emitting)
			_rageAura.Emitting = false;
		if (IsOnFloor())
			return;
		Velocity = new Vector2(0f, Velocity.Y + Gravity * dt);
		MoveAndSlide();
	}

	// ── Decisions ───────────────────────────────────────────────────────────────────────────────

	private void Think(float dt)
	{
		_leapCooldownTimer -= dt;
		_antiAirCooldownTimer -= dt;
		_decisionTimer -= dt;
		CheckRage();

		var player = PlayerRef;
		if (player is null || !PlayerDetected)
		{
			_rushing = false;
			return;
		}
		if (Attacking || Hopping || !IsOnFloor())
			return;

		float distanceX = player.GlobalPosition.X - GlobalPosition.X;
		float absDistance = Mathf.Abs(distanceX);

		if (_chainPending)
		{
			_chainPending = false;
			if (TryLeap(player, 0.45f))
				return;
		}

		// Jumping over it → claw leap straight up into them.
		float above = GlobalPosition.Y - player.GlobalPosition.Y;
		if (!player.IsOnFloor() && above >= AntiAirMinHeight && absDistance <= AntiAirRange && _antiAirCooldownTimer <= 0f)
		{
			TryAntiAir(player);
			return;
		}

		if (_rushing)
		{
			_rushTimer -= dt;
			if (absDistance <= AttackRange)
			{
				_rushing = false;
				TryAttackNow();
			}
			else if (_rushTimer <= 0f)
			{
				_rushing = false;
			}
			return;
		}

		bool leapRange = absDistance >= LeapMinDistance && absDistance <= LeapMaxDistance;

		// Punish: a whiffed swing or a dash ending in leap range.
		if ((Whiff.JustWhiffed || Dash.SinceDashEnded < 0.1f) && leapRange && _leapCooldownTimer <= 0f)
		{
			Whiff.Consume();
			TryLeap(player, 0.55f);
			return;
		}

		if (_decisionTimer <= 0f)
		{
			_decisionTimer = DecisionInterval;
			// Right on top of it, the in-place slam (MeleeEnemy's own range trigger) handles it.
			if (leapRange && absDistance > AttackRange && _leapCooldownTimer <= 0f && Rng.Randf() < LeapChance)
				TryLeap(player, 1f);
		}
	}

	private void StartRush()
	{
		_rushing = true;
		_rushTimer = RushMaxTime;
	}

	private void CheckRage()
	{
		if (_enraged || Stats is null || Stats.CurrentHealth <= 0 || Stats.CurrentHealth > Stats.MaxHealth * RageHealthFraction)
			return;

		_enraged = true;
		MoveSpeed *= RageSpeedMultiplier;
		ChargeSpeed *= RageSpeedMultiplier;
		RushSpeed *= RageSpeedMultiplier;
		AttackCooldown *= RageCooldownMultiplier;
		LeapCooldown *= RageCooldownMultiplier;
		AntiAirCooldown *= RageCooldownMultiplier;
		Visual.Modulate = RageTint;
		_palette.Hot = new Color(1f, 0.16f, 0.05f, 1f);
		_palette.Light = new Color(1f, 0.3f, 0.15f, 1f);

		// Roar: a burst at its feet, a rumble, and a smoldering ember aura from then on.
		if (TryFindGround(GlobalPosition.X, out Vector2 feet))
			GroundSlamFx.Impact(this, feet, 0.7f, _palette);
		ShakePlayerCamera(4f, 0.35f);
		StartRageAura();
	}

	private void StartRageAura()
	{
		var ramp = new Gradient();
		ramp.Offsets = new[] { 0f, 0.4f, 1f };
		ramp.Colors = new[] { new Color(1f, 0.8f, 0.4f, 0f), new Color(1f, 0.3f, 0.08f, 0.9f), new Color(0.6f, 0.05f, 0.02f, 0f) };
		_rageAura = new GpuParticles2D
		{
			Amount = 18,
			Lifetime = 0.9,
			LocalCoords = false,
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			ProcessMaterial = new ParticleProcessMaterial
			{
				Direction = new Vector3(0f, -1f, 0f),
				Spread = 25f,
				InitialVelocityMin = 25f,
				InitialVelocityMax = 70f,
				Gravity = new Vector3(0f, -30f, 0f),
				ScaleMin = 0.05f,
				ScaleMax = 0.12f,
				ColorRamp = new GradientTexture1D { Gradient = ramp },
				EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
				EmissionBoxExtents = new Vector3(BodyHalfWidth * 0.8f, 20f, 0f),
			},
			Position = new Vector2(0f, -6f),
		};
		AddChild(_rageAura);
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (Hopping)
			return currentVelocityX;
		if (Attacking || !CanChase)
			return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);

		float absDistance = Mathf.Abs(distanceX);
		float toward = Mathf.Sign(distanceX);
		if (_rushing)
			return CanStepTo(toward) ? toward * RushSpeed : 0f;
		// Keeping away from it → it charges instead of plodding over.
		if (absDistance > ChargeDistance)
			return CanStepTo(toward) ? toward * ChargeSpeed : 0f;
		return SpacingMoveX(distanceX, currentVelocityX, StopDistance, 0f);
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (_airborneAttack)
			return;
		base.UpdateAnimation(velocity);
	}

	// ── Telegraph / trails ──────────────────────────────────────────────────────────────────────

	private void FlashBody(float duration)
	{
		if (Sprite is null || duration <= 0f)
			return;
		_flashTween?.Kill();
		_flashTween = CreateTween();
		float half = duration / Mathf.Max(1, FlashBlinks) * 0.5f;
		for (int i = 0; i < FlashBlinks; i++)
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

	// Three parallel claw streaks; `upward` rakes up over the head (anti-air) instead of down in front.
	private void RakeClaws(bool upward)
	{
		for (int i = 0; i < _claws.Length; i++)
		{
			float radius = ClawRadius + (i - 1) * ClawSpacing;
			if (upward)
				_claws[i].Play(new Vector2(0f, -20f), radius, 30f - i * 6f, -150f + i * 6f, ClawDuration);
			else
				_claws[i].Play(ClawCenter, radius, ClawStartAngle, ClawEndAngle, ClawDuration);
		}
	}

	private void UpdateAfterimages(float dt)
	{
		if (!_leapHoming)
		{
			_afterimageTimer = 0f;
			return;
		}
		_afterimageTimer -= dt;
		if (_afterimageTimer > 0f)
			return;
		_afterimageTimer = AfterimageInterval;

		Texture2D frame = Sprite?.SpriteFrames?.GetFrameTexture(Sprite.Animation, Sprite.Frame);
		if (frame is null || GetTree().CurrentScene is not Node scene)
			return;
		var ghost = new Sprite2D
		{
			Texture = frame,
			Centered = Sprite.Centered,
			Offset = Sprite.Offset,
			FlipH = Sprite.FlipH,
			Material = Metroidvania.Player.NeonSilhouette.CreateMaterial(AfterimageColor, AfterimageIntensity),
			Modulate = new Color(1f, 1f, 1f, AfterimageAlpha),
			ZIndex = ZIndex - 1,
		};
		scene.AddChild(ghost);
		ghost.GlobalTransform = Sprite.GlobalTransform;
		Tween fade = ghost.CreateTween();
		fade.TweenProperty(ghost, "modulate:a", 0f, AfterimageLifetime).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
		fade.TweenCallback(Callable.From(ghost.QueueFree));
	}

	// ── In-place ground slam ────────────────────────────────────────────────────────────────────

	protected override async Task Attack()
	{
		Attacking = true;
		CanAttack = false;
		_rushing = false;
		// The wind-up here IS the animation's own lead-in (slam on frame 8), so it can't be
		// shortened without desyncing the impact from the art.
		WindupScale = 1f;
		Sprite.Play("attack");
		FlashBody(SlamFlashDuration);

		await ToSignal(GetTree().CreateTimer(WindupDuration), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;
		ClearFlash();

		float sign = FacingRight ? 1f : -1f;
		RakeClaws(upward: false);
		if (TryFindGround(GlobalPosition.X + sign * (AttackHitboxReach + 14f), out Vector2 impact))
			GroundSlamFx.Impact(this, impact, 0.85f, _palette);
		ShakePlayerCamera(SlamShake + 1.5f, 0.2f);
		RunShockwave(sign, SlamTileCount, 0f);
		if (_enraged)
			RunShockwave(-sign, RageBackwardTiles, SlamStrikeGap);
		await base.Attack();
	}

	// ── Leap slam ───────────────────────────────────────────────────────────────────────────────

	private bool TryLeap(Metroidvania.Player.Player player, float crouchScale)
	{
		if (Attacking || !CanAttack || !IsOnFloor() || !EnemyCombatCoordinator.TryAcquireAttackSlot())
			return false;
		HoldingAttackSlot = true;
		_ = Leap(player, crouchScale);
		return true;
	}

	// Launch velocity that lands on the player's predicted spot under an apex of `apex` px,
	// or null if it can't (target out of reach above the arc).
	private Vector2? SolveLeap(Metroidvania.Player.Player player, float apex)
	{
		float vy = -Mathf.Sqrt(2f * Gravity * apex);
		float dy = player.GlobalPosition.Y - GlobalPosition.Y;
		float discriminant = vy * vy + 2f * Gravity * dy;
		if (discriminant <= 0f)
			return null;
		float time = (-vy + Mathf.Sqrt(discriminant)) / Gravity;
		float lead = player.IsDashing ? 0f : player.Velocity.X * time * LeapLead;
		float dx = player.GlobalPosition.X + lead - GlobalPosition.X;
		return new Vector2(Mathf.Clamp(dx / time, -LeapMaxSpeedX, LeapMaxSpeedX), vy);
	}

	private async Task Leap(Metroidvania.Player.Player player, float crouchScale)
	{
		Attacking = true;
		CanAttack = false;
		_airborneAttack = true;
		_leapCooldownTimer = LeapCooldown;
		FacingRight = player.GlobalPosition.X >= GlobalPosition.X;
		Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);
		bool chain = false;
		bool fellBackToRush = false;

		Sprite.Play("attack");
		Sprite.Pause();
		Sprite.Frame = LeapCrouchFrame;
		float crouch = LeapCrouchTime * crouchScale;
		FlashBody(crouch);

		try
		{
			await ToSignal(GetTree().CreateTimer(crouch), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;
			ClearFlash();

			// Apex by distance, capped under whatever ceiling is overhead.
			float distance = IsInstanceValid(player) ? Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X) : 0f;
			float t = Mathf.Clamp((distance - LeapMinDistance) / Mathf.Max(1f, LeapMaxDistance - LeapMinDistance), 0f, 1f);
			float apex = Mathf.Min(Mathf.Lerp(LeapApexMin, LeapApexMax, t), HeadroomAbove() - CeilingMargin);
			Vector2? launch = apex >= 22f && IsInstanceValid(player) ? SolveLeap(player, apex) : null;

			// Can't leap here (low ceiling, no safe landing) → rush in on foot instead of fizzling.
			if (launch is null || !TryAttackHop(launch.Value, 80f))
				fellBackToRush = true;
			else
				chain = await LeapFlight(player, launch.Value);
		}
		finally
		{
			if (IsInstanceValid(this))
			{
				AttackHitbox.Deactivate();
				ClearFlash();
				Sprite.SpeedScale = 1f;
				_leapHoming = false;
				_airborneAttack = false;
				Attacking = false;
				// OnDefeated may already have released it (death mid-leap).
				if (HoldingAttackSlot)
				{
					EnemyCombatCoordinator.ReleaseAttackSlot();
					HoldingAttackSlot = false;
				}
			}
		}

		if (fellBackToRush)
		{
			// Didn't actually attack: free to strike again as soon as the rush arrives.
			CanAttack = true;
			StartRush();
			return;
		}

		// Enraged follow-up leaps.
		if (chain)
		{
			_chainCount++;
			await ToSignal(GetTree().CreateTimer(0.12f), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this))
				return;
			CanAttack = true;
			_chainPending = true;
			return;
		}
		_chainCount = 0;

		float jitter = 1f + Rng.RandfRange(-AttackCooldownJitter, AttackCooldownJitter);
		await ToSignal(GetTree().CreateTimer(AttackCooldown * jitter), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			CanAttack = true;
	}

	// The airborne part of a leap that actually launched: flight, landing slam, recovery.
	// Returns whether an enraged chain leap should follow. Kept separate so a failed launch in
	// Leap() never returns out of its try block past the CanAttack/cooldown bookkeeping.
	private async Task<bool> LeapFlight(Metroidvania.Player.Player player, Vector2 launch)
	{
		// Stretch the air frames (4-7) over the real airtime, then hold the last one until it lands.
		float airTime = 2f * -launch.Y / Mathf.Max(1f, Gravity);
		Sprite.SpeedScale = (LastAirFrame - FirstAirFrame + 1) / SheetFps / airTime;
		Sprite.Frame = FirstAirFrame;
		Sprite.Play();
		_leapHoming = true;

		float elapsed = 0f;
		while (Hopping && elapsed < airTime + 0.7f)
		{
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return false;
			float dt = (float)GetPhysicsProcessDeltaTime();
			elapsed += dt;
			if (Sprite.Frame >= LastAirFrame)
				Sprite.Pause();

			// Home toward the player's X while airborne (never off a ledge — LandsSafely
			// already vetted the original arc; this only nudges it).
			if (IsInstanceValid(player))
			{
				// Steer toward the speed that lands exactly on them in the remaining airtime
				// (not flat-out toward them, which overshot on long leaps).
				float toward = player.GlobalPosition.X - GlobalPosition.X;
				float remaining = Mathf.Max(0.12f, airTime - elapsed);
				float desired = Mathf.Clamp(toward / remaining, -LeapMaxSpeedX, LeapMaxSpeedX);
				float vx = Mathf.MoveToward(Velocity.X, desired, AirSteerAccel * dt);
				Velocity = new Vector2(vx, Velocity.Y);
			}
		}
		_leapHoming = false;

		// Landed: slam frame, claws, big impact, spikes out to both sides.
		Sprite.SpeedScale = 1f;
		Sprite.Frame = SlamFrame;
		Sprite.Play();
		RakeClaws(upward: false);
		if (TryFindGround(GlobalPosition.X, out Vector2 landing))
			GroundSlamFx.Impact(this, landing, LeapImpactScale, _palette);
		ShakePlayerCamera(LeapShake, 0.32f);
		int tiles = LeapShockwaveTilesPerSide + (_enraged ? 1 : 0);
		RunShockwave(1f, tiles, SlamStrikeGap * 0.6f, shakePerTile: false);
		RunShockwave(-1f, tiles, SlamStrikeGap * 0.6f, shakePerTile: false);

		AttackHitbox.Position = new Vector2(FacingRight ? AttackHitboxReach : -AttackHitboxReach, 0);
		AttackHitbox.Activate(Stats);
		await ToSignal(GetTree().CreateTimer(AttackDuration), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return false;
		AttackHitbox.Deactivate();

		// Sprawled on the floor — the opening to punish it (shorter when enraged).
		float recovery = Mathf.Max(0f, (_enraged ? RageRecoveryTime : RecoveryTime) - AttackDuration);
		await ToSignal(GetTree().CreateTimer(recovery), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return false;

		return _enraged && _chainCount < MaxChainLeaps && Rng.Randf() < ChainLeapChance;
	}

	// ── Anti-air claw ───────────────────────────────────────────────────────────────────────────

	private void TryAntiAir(Metroidvania.Player.Player player)
	{
		if (Attacking || !CanAttack || !IsOnFloor() || HeadroomAbove() < 60f || !EnemyCombatCoordinator.TryAcquireAttackSlot())
			return;
		HoldingAttackSlot = true;
		_ = AntiAir(player);
	}

	private async Task AntiAir(Metroidvania.Player.Player player)
	{
		Attacking = true;
		CanAttack = false;
		_airborneAttack = true;
		_antiAirCooldownTimer = AntiAirCooldown;
		FacingRight = player.GlobalPosition.X >= GlobalPosition.X;
		Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);

		Sprite.Play("attack");
		Sprite.Pause();
		Sprite.Frame = LeapCrouchFrame;
		FlashBody(AntiAirTelegraph);

		try
		{
			await ToSignal(GetTree().CreateTimer(AntiAirTelegraph), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;
			ClearFlash();

			float apex = Mathf.Min(-AntiAirVelocityY * -AntiAirVelocityY / (2f * Gravity), HeadroomAbove() - CeilingMargin);
			float vy = -Mathf.Sqrt(2f * Gravity * Mathf.Max(20f, apex));
			float dx = IsInstanceValid(player) ? player.GlobalPosition.X - GlobalPosition.X : 0f;
			float airTime = 2f * -vy / Gravity;
			// No room to jump → just skip it; the bookkeeping below still runs.
			if (TryAttackHop(new Vector2(Mathf.Clamp(dx / airTime, -160f, 160f), vy), 80f))
				await AntiAirFlight(airTime);
		}
		finally
		{
			if (IsInstanceValid(this))
			{
				AttackHitbox.Deactivate();
				ClearFlash();
				_leapHoming = false;
				_airborneAttack = false;
				Attacking = false;
				if (HoldingAttackSlot)
				{
					EnemyCombatCoordinator.ReleaseAttackSlot();
					HoldingAttackSlot = false;
				}
			}
		}

		float jitter = 1f + Rng.RandfRange(-AttackCooldownJitter, AttackCooldownJitter);
		await ToSignal(GetTree().CreateTimer(AttackCooldown * 0.6f * jitter), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			CanAttack = true;
	}

	// The rise and fall of an anti-air claw that actually launched.
	private async Task AntiAirFlight(float airTime)
	{
		// Claws up (frame 4) through the rise, hitbox over its head.
		Sprite.Frame = FirstAirFrame;
		RakeClaws(upward: true);
		AttackHitbox.Position = new Vector2(FacingRight ? AntiAirHitboxOffset.X : -AntiAirHitboxOffset.X, AntiAirHitboxOffset.Y);
		AttackHitbox.Activate(Stats);
		_leapHoming = true;

		float elapsed = 0f;
		while (Hopping && elapsed < airTime + 0.5f)
		{
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;
			elapsed += (float)GetPhysicsProcessDeltaTime();
			// Claws only hurt on the way up.
			if (Velocity.Y >= 0f)
			{
				AttackHitbox.Deactivate();
				Sprite.Frame = LastAirFrame;
			}
		}
		_leapHoming = false;

		if (TryFindGround(GlobalPosition.X, out Vector2 landing))
			GroundSlamFx.Impact(this, landing, 0.55f, _palette);
		ShakePlayerCamera(2.5f, 0.15f);
	}

	// ── Shockwave ───────────────────────────────────────────────────────────────────────────────

	// Earth spikes marching away along the floor, each tile warned by a glowing crack just before
	// it erupts; stops at the first spot with no ground (pit/ledge).
	private async void RunShockwave(float sign, int tiles, float firstDelay, bool shakePerTile = true)
	{
		float originX = GlobalPosition.X;
		float delay = firstDelay;
		for (int i = 1; i <= tiles; i++)
		{
			if (!TryFindGround(originX + sign * SlamTileSpacing * i, out Vector2 ground))
				return;
			GroundSlamFx.WarningCrack(this, ground, SlamHitboxSize.X, SlamWarningLead + delay, _palette);

			await ToSignal(GetTree().CreateTimer(SlamWarningLead + delay), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;
			delay = SlamStrikeGap - SlamWarningLead > 0f ? SlamStrikeGap - SlamWarningLead : 0f;
			StrikeTile(ground, shakePerTile);
		}
	}

	private async void StrikeTile(Vector2 ground, bool shake)
	{
		GroundSlamFx.EarthSpikes(this, ground, SlamHitboxSize, SpikeHoldDuration, _palette);
		if (shake)
			ShakePlayerCamera(SlamShake, 0.12f);

		Hazard hazard = Hazard.CreateArea((Node2D)GetTree().CurrentScene, instantKill: false, damage: SlamDamage, knockbackForce: 260f);
		hazard.GlobalPosition = ground - new Vector2(0f, SlamHitboxSize.Y * 0.5f);
		hazard.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = SlamHitboxSize } });

		await ToSignal(GetTree().CreateTimer(SlamActiveDuration), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(hazard))
			hazard.QueueFree();
	}

	// ── World queries ───────────────────────────────────────────────────────────────────────────

	private uint WorldMask => 1u | Metroidvania.Shared.PhysicsLayers.ClimbableWalls | Metroidvania.Shared.PhysicsLayers.OneWayPlatforms;

	// Floor surface under x, searched from a bit above its own body down past its feet.
	private bool TryFindGround(float x, out Vector2 point)
	{
		point = default;
		var from = new Vector2(x, GlobalPosition.Y - 10f);
		var to = new Vector2(x, GlobalPosition.Y + BodyBottom + 60f);
		var query = PhysicsRayQueryParameters2D.Create(from, to, WorldMask, new Godot.Collections.Array<Rid> { GetRid() });
		var hit = GetWorld2D().DirectSpaceState.IntersectRay(query);
		if (hit.Count == 0)
			return false;
		point = (Vector2)hit["position"];
		return true;
	}

	// Free space above the top of its body (solid ceilings only — one-way platforms are jumped through).
	private float HeadroomAbove()
	{
		float top = GlobalPosition.Y - BodyBottom;
		var from = new Vector2(GlobalPosition.X, top);
		var to = from + Vector2.Up * 400f;
		var query = PhysicsRayQueryParameters2D.Create(from, to, 1u | Metroidvania.Shared.PhysicsLayers.ClimbableWalls,
			new Godot.Collections.Array<Rid> { GetRid() });
		var hit = GetWorld2D().DirectSpaceState.IntersectRay(query);
		return hit.Count == 0 ? 400f : from.Y - ((Vector2)hit["position"]).Y;
	}

	private void ShakePlayerCamera(float strength, float duration)
	{
		if (PlayerReads.Find(this) is { } player && player.GlobalPosition.DistanceTo(GlobalPosition) < 700f)
			player.ShakeCamera(strength, duration);
	}
}
