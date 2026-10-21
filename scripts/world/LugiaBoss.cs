using System.Threading.Tasks;
using Godot;
using Metroidvania.Player;
using Metroidvania.Save;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Midir (built on Lugia, the wyvern from shinobuGaen's Pixel Abyss pack) for the "recreé a Midir"
// video, its Midir moves co-designed with the user step by step. So far:
//  * Asleep until the player comes into sight, then gets up and roars (player held, camera framed).
//  * Claw combo: player close → two swipes, sometimes ending in a short spit of fire.
//  * Fire breath: mid range → a churning stream of fire along the ground (burns in ticks).
//  * Savage rush: charges across the whole arena breathing fire, pulling up to swipe — jump it.
//  * Air assault: flies to one end and crosses to the other raking the floor with fire from above.
// Still to design: dark beam, fire under the body, tail whip, grab-bite, phase 2.
// The sheet faces left natively — the scene's CharacterSprite has flip_h on, so Visual.Scale.X = 1
// faces right like every other enemy. Hit timings are frame indexes inside each animation. The sheet
// is the fire-free 208px one recoloured black (Midir_SinFuego-Sheet.png); the breath is drawn in code.
// Its own class (boss-uniqueness rule); joins the "boss" group so the player's boss bar tracks it.
public partial class LugiaBoss : Enemy
{
	[ExportGroup("Movement")]
	[Export] public float EngageDistance = 140f;
	[Export] public float HurtDuration = 0.25f;

	[ExportGroup("Claw combo")]
	// Two claw swipes (rear up, rake down in front — attack_1 frames 5-6 are the strike) then a short
	// spit of fire. Each swipe leaves a neon trail along the claw's arc and a small gust of wind.
	[Export] public float ClawRange = 190f;
	[Export] public float ClawCooldown = 2.2f;
	[Export] public int ClawHitFrameFrom = 5;
	[Export] public int ClawHitFrameTo = 6;
	[Export] public float ClawReach = 140f;
	[Export] public float ClawRadius = 75f;
	// The second swipe skips part of the rear-up and plays faster — it comes quicker than the first.
	[Export] public int ClawSecondStartFrame = 3;
	[Export] public float ClawSecondSpeed = 1.35f;
	// Small step into each strike.
	[Export] public float ClawLungeSpeed = 180f;
	// Trail arc (Visual space, facing right; 0° = front, -90° = up), traced over the strike frames.
	[Export] public Vector2 ClawArcCenter = new(90f, -10f);
	[Export] public float ClawArcRadius = 125f;
	[Export] public float ClawArcStart = -75f;
	[Export] public float ClawArcEnd = 60f;
	[Export] public int ClawTrailFrame = 4;
	[Export] public float ClawTrailDuration = 0.18f;
	[Export] public Color ClawTrailGlow = new(0.55f, 0.6f, 1f, 1f);
	[Export] public Color ClawTrailCore = new(0.95f, 0.97f, 1f, 1f);
	// Gust off each swipe (same streaks as the roar, smaller).
	[Export] public int ClawGustStreaks = 10;
	[Export] public float ClawGustReach = 260f;
	[Export] public float ClawSpitDuration = 0.4f;
	// The spit is short-range: its burn area hugs the mouth (the long breath's cone starts further out).
	[Export] public Vector2 SpitAreaCenter = new(230f, -25f);
	[Export] public Vector2 SpitAreaSize = new(360f, 185f);
	[Export] public float ClawSpitGatherSpeed = 1.8f;
	// Variant: the same two swipes without the spit. Chance the combo ends in the spit.
	[Export] public float ClawSpitChance = 0.5f;
	[Export] public float ClawRecovery = 0.5f;
	// Third variant: three swipes (each a bit faster) and a roar that blows the player back.
	[Export] public float ClawTripleChance = 0.3f;
	[Export] public float ClawThirdSpeed = 1.5f;
	[Export] public float ClawRoarDuration = 1.2f;
	[Export] public float ClawTripleRecovery = 0.8f;

	[ExportGroup("Fire breath")]
	[Export] public float FireMinRange = 150f;
	[Export] public float FireMaxRange = 540f;
	[Export] public float FireCooldown = 4.5f;
	// The breath is our own procedural fire (the sheet's drawn flames were dropped): the animation
	// gathers on frames 0..BreathFrame-1 (a glow builds in the mouth), then holds BreathFrame (mouth
	// open) for BreathDuration while particles stream out, burning whoever stands in the cone.
	[Export] public int BreathFrame = 5;
	[Export] public float BreathDuration = 1.1f;
	// Mouth while breathing, in Visual space facing right (measured off the 208px sheet ×3 scale).
	[Export] public Vector2 GroundMouth = new(144f, 21f);
	[Export] public Vector2 FlyMouth = new(120f, -9f);
	// Degrees below horizontal the stream points.
	[Export] public float GroundBreathAngle = 9f;
	[Export] public float FlyBreathAngle = 12f;
	[Export] public float BreathSpeed = 1500f;
	[Export] public float BreathLifetime = 0.8f;
	[Export] public int BreathAmount = 600;
	// Chaos knobs: how hard the flames churn, how far the stream thrashes (degrees), embers spat out.
	[Export] public float BreathTurbulence = 6f;
	[Export] public float BreathWobble = 6f;
	[Export] public int BreathEmberAmount = 70;
	// Flames left burning where the stream hits the floor.
	[Export] public float GroundFireLinger = 1.6f;
	[Export] public float GroundFireHalfWidth = 110f;
	[Export] public Color BreathCoreColor = new(1f, 0.97f, 0.8f, 1f);
	[Export] public Color BreathFlameColor = new(1f, 0.62f, 0.15f, 1f);
	[Export] public Color BreathEmberColor = new(0.85f, 0.18f, 0.05f, 1f);
	[Export] public float BreathLightEnergy = 1.6f;
	// Damage: many small burn ticks while the player stays in the cone, instead of one hit.
	[Export] public int BreathTickDamage = 3;
	[Export] public float BreathTickInterval = 0.15f;
	// Player body half-size, roughly — the cone check is against their center.
	[Export] public float BreathPlayerMargin = 18f;
	// How long the player keeps the red burn tint after the last tick.
	[Export] public float BurnTintLinger = 0.6f;

	[ExportGroup("Phase 2")]
	// At PhaseTwoHealthRatio it channels darkness and explodes (a strong unparryable blast around it,
	// humanities flung everywhere), then fights on with the same moves plus the humanity orbs.
	[Export] public float PhaseTwoHealthRatio = 0.5f;
	[Export] public float PhaseChargeTime = 1.4f;
	[Export] public float PhaseBlastRadius = 380f;
	[Export] public float PhaseBlastDamageMultiplier = 2.2f;
	[Export] public int PhaseScatterOrbs = 12;
	// Humanity orbs: rise around it, hang, then home in on the player one after another.
	[Export] public int OrbCount = 6;
	[Export] public float OrbCooldown = 9f;
	[Export] public float OrbChance = 0.4f;
	[Export] public float OrbMinRange = 180f;
	[Export] public float OrbLaunchInterval = 0.25f;
	[Export] public float OrbDamageMultiplier = 0.8f;
	[Export] public float OrbRecovery = 1f;
	// Near death: the focused beam — DesperationSweeps sweeps in a row, then it collapses, dying,
	// wide open for DesperationDownTime.
	[Export] public float DesperationHealthRatio = 0.15f;
	[Export] public int DesperationSweeps = 6;
	[Export] public float DesperationSweepTime = 0.55f;
	[Export] public float DesperationDownTime = 4f;

	[ExportGroup("Showcase (video)")]
	// For filming: ignores distances/cooldowns and plays every move in a fixed order, looping, with
	// ShowcasePause between them. Claw moves wait (up to ShowcaseApproachTime) for it to walk into
	// claw range first. Punish reactions (under-fire) only come as their own step.
	[Export] public bool ShowcaseMode;
	[Export] public float ShowcasePause = 1.2f;
	[Export] public float ShowcaseApproachTime = 3f;

	[ExportGroup("Body chaos")]
	// Midir must always look unstable, whatever it's doing: the sprite jitters, dark afterimages
	// shudder out of sync behind it, smoke and embers keep coming off its body. All of it grows as it
	// loses health and while it's mid-attack; asleep it's still (contrast for the wake-up).
	// Jitter, in sheet pixels (the sprite is drawn ×3).
	[Export] public float JitterAmount = 0.7f;
	[Export] public float JitterInterval = 0.05f;
	[Export] public int GhostCount = 2;
	[Export] public float GhostSpread = 13f;
	[Export] public Color GhostColor = new(0.6f, 0.35f, 1f, 0.6f);
	[Export] public int SmokeAmount = 34;
	[Export] public Color SmokeColor = new(0.6f, 0.55f, 0.75f, 0.5f);
	[Export] public int BodyEmberAmount = 20;
	// Jitter/ghost spread multiplier at 0 health (1 at full).
	[Export] public float ChaosAtLowHealth = 2.2f;
	[Export] public float ChaosWhileAttacking = 1.5f;

	[ExportGroup("Crystal sheen")]
	// Faint cool light on the body so the silver-lavender scales read in dark (CanvasModulate) arenas —
	// with HDR 2D the modulate crushes the sprite's darks much harder than the bright backdrop.
	[Export] public Color SheenColor = new(0.7f, 0.75f, 1f, 1f);
	[Export] public float SheenEnergy = 0.9f;
	[Export] public float SheenScale = 3.2f;
	// Cone of fire in front of the mouth (facing right), relative to the body origin. Measured off a render: the
	// dense flames end ~410px past the mouth (~555px from the origin) — keep the far edge there so you
	// only burn where you can see fire.
	[Export] public Vector2 FireAreaCenter = new(350f, -25f);
	[Export] public Vector2 FireAreaSize = new(400f, 185f);
	[Export] public float FireRecovery = 0.6f;

	[ExportGroup("Dark beam")]
	// Midir's hook move: a normal fire breath that turns violet, sucks back into the mouth and becomes
	// a beam — first raking along the floor toward the player (jump it), then arcing up from the far
	// end through the air. Violet marks left along its path burst a moment later. One strong hit,
	// can't be parried (a dash still dodges). Then it's exhausted, head down: the big punish window.
	// Only from BeamHealthRatio of its health down; the first breath after that is always the beam.
	[Export] public float BeamHealthRatio = 0.55f;
	[Export] public float BeamChance = 0.35f;
	[Export] public float BeamCooldown = 10f;
	[Export] public float BeamFireLead = 0.8f;
	[Export] public float BeamChargeTime = 0.45f;
	[Export] public float BeamReach = 720f;
	[Export] public float BeamSweepGroundTime = 0.5f;
	[Export] public float BeamSweepAirTime = 0.6f;
	// Highest the second sweep climbs (degrees above horizontal).
	[Export] public float BeamAirAngle = 35f;
	[Export] public float BeamHitHalfWidth = 22f;
	[Export] public float BeamDamageMultiplier = 2.5f;
	[Export] public float BeamKnockback = 380f;
	[Export] public float BeamMarkSpacing = 75f;
	[Export] public float BeamBurstDelay = 0.5f;
	[Export] public float BeamBurstRadius = 55f;
	[Export] public float BeamBurstDamageMultiplier = 1.2f;
	[Export] public float BeamExhaustTime = 1.5f;
	// Chaos while it fires: violent shake, wind streaks blown down the beam, water and grit kicked up where it
	// meets the floor, the roar's dark flash and ceiling dust.
	[Export] public float BeamShakeStrength = 22f;
	[Export] public int BeamWindPerFrame = 2;
	[Export] public float BeamWindReach = 320f;
	[Export] public Color BeamWindColor = new(0.8f, 0.7f, 1f, 1f);
	[Export] public Color BeamGlowColor = new(0.62f, 0.3f, 1f, 1f);
	[Export] public Color BeamCoreColor = new(0.97f, 0.93f, 1f, 1f);

	[ExportGroup("Savage rush")]
	// Midir's savage rush: a short snarl, then it charges at the player and keeps going across the
	// arena — breathing fire in stretches as it runs and pulling up for a claw swipe after each, ending
	// after RushMaxSwipes (fire, swipe, fire, swipe) or at a wall. Being run over hurts; the body hits only up to RushHitHeight above the floor,
	// so a well-timed jump (or a double jump) clears it, and a dash goes through.
	[Export] public float RushCooldown = 9f;
	[Export] public float RushFirstDelay = 4f;
	[Export] public float RushMinRange = 240f;
	[Export] public float RushChance = 0.6f;
	[Export] public float RushTellDuration = 0.6f;
	[Export] public float RushSpeed = 520f;
	[Export] public float RushRunSpeedScale = 1.7f;
	// One stretch of running with fire, then a swipe; repeated until it reaches the far side.
	[Export] public float RushFireStretch = 0.7f;
	[Export] public int RushMaxSwipes = 2;
	[Export] public float RushMaxTime = 4.5f;
	[Export] public float RushHitWidth = 120f;
	[Export] public float RushHitHeight = 55f;
	[Export] public float RushRecovery = 0.9f;
	// The air assault and the rush are its two "big" moves: the sweep may flow straight into a rush
	// (RushAfterSweepChance — a good back-to-back), but after either, both are locked for
	// BigMoveLockout so it doesn't loop them while the player is far away (it walks in instead).
	[Export] public float RushAfterSweepChance = 0.6f;
	[Export] public float BigMoveLockout = 12f;
	// Big moves only from this far out, rolled every BigMoveRollInterval (chances FlightChance / RushChance).
	[Export] public float BigMoveMinRange = 300f;
	[Export] public float BigMoveRollInterval = 1.2f;
	[Export] public float FlightChance = 0.45f;

	[ExportGroup("Air assault")]
	[Export] public float FlightCooldown = 11f;
	[Export] public float FlightFirstDelay = 6f;
	[Export] public float FlySpeed = 320f;
	[Export] public float TakeOffHeight = 170f;
	// Midir's air assault: flies up to one end of the arena, then crosses to the other raking the
	// floor with fire from above. The stream angles forward-down, so the spot right under and behind
	// it is safe — run at it and pass underneath (or dash through the fire line).
	// Its feet this high above the player's feet while sweeping.
	[Export] public float SweepHeight = 250f;
	// Starts this far out on its own side of the player, then crosses up to SweepTravel.
	[Export] public float SweepStartOffset = 520f;
	[Export] public float SweepTravel = 1150f;
	[Export] public float SweepSpeed = 380f;
	// Degrees below horizontal (forward-down).
	[Export] public float SweepAngle = 55f;
	// Shorter-lived stream so it ends around the floor instead of drawing through it.
	[Export] public float SweepBreathLifetime = 0.32f;
	// Burn zone around where the stream meets the floor.
	[Export] public float SweepBurnHalfWidth = 90f;
	[Export] public float SweepBurnHeight = 130f;
	// Flames left along its path (visual only).
	[Export] public float SweepPatchSpacing = 110f;
	[Export] public float SweepPatchLinger = 1.4f;
	[Export] public float FlightMaxLegTime = 2.5f;
	[Export] public float FlightRecovery = 0.8f;

	[ExportGroup("Under fire")]
	// Punished hard (UnderFireHits hits within UnderFireWindow) → it rises a little and pours fire
	// straight down; a carpet of flames spreads both ways along the floor and burns for a while, so
	// the player is forced to back off. Can cut its own recovery short (that's when it's punished).
	[Export] public int UnderFireHits = 4;
	[Export] public float UnderFireWindow = 3f;
	[Export] public float UnderFireCooldown = 8f;
	[Export] public float UnderFireRise = 110f;
	[Export] public float UnderFireDuration = 1.6f;
	// The carpet grows to this half-width (each side of its body) over UnderFireSpreadTime.
	[Export] public float UnderFireRadius = 260f;
	[Export] public float UnderFireSpreadTime = 0.45f;
	[Export] public float UnderFireBurnHeight = 140f;
	[Export] public float UnderFirePatchInterval = 0.08f;
	[Export] public float UnderFireLinger = 0.9f;
	[Export] public float UnderFireRecovery = 0.9f;

	[ExportGroup("Wake & roar")]
	// Midir's entrance: asleep (lying, breathing) until the player comes into its sight
	// (DetectionRange), then the player is held in place while it gets up and roars; the boss bar
	// and the fight start after the roar.
	[Export] public bool StartAsleep = true;
	// Its sight while asleep — shorter than DetectionRange so the player gets to see it lying there
	// (the camera shows ~460px each side) before it notices them.
	[Export] public float WakeRange = 400f;
	// Intro close-up: camera centred on it (plus this offset) at IntroZoom × the map's zoom.
	[Export] public float IntroZoom = 1.35f;
	[Export] public Vector2 IntroCameraOffset = new(0f, -20f);
	// Once awake it's a boss fight: it tracks the player anywhere in the arena (the profile's DetectionRange
	// is too short — after a rush to the far wall it would lose the player and stand idle).
	[Export] public float AwakeDetectionRange = 3000f;
	[Export] public float SleepBreathAmount = 0.025f;
	[Export] public float SleepBreathPeriod = 2.6f;
	// The lying-down frames (sheet 76-77) dip 7px below the standing feet line; lift them by that × scale.
	[Export] public float SleepPoseLift = 21f;
	[Export] public float WakeDelay = 0.6f;
	[Export] public float RoarDuration = 1.8f;
	// The roar's gust: wind streaks blown out of the mouth in a fan toward the player.
	[Export] public float RoarStreakInterval = 0.012f;
	[Export] public float RoarStreakLength = 190f;
	[Export] public float RoarWindReach = 650f;
	[Export] public float RoarWindSpread = 22f;
	[Export] public Color RoarWindColor = new(0.85f, 0.9f, 1f, 1f);
	[Export] public Color RoarSprayColor = new(0.65f, 0.9f, 1f, 1f);
	[Export] public float RoarShakeStrength = 16f;
	[Export] public float RoarDarken = 0.4f;
	// Horizontal shove on the player when the roar hits (no hop).
	[Export] public float RoarPushSpeed = 420f;
	// In a fight the roar shoves the player away (no hop, no damage) if within this range.
	[Export] public float RoarShoveRange = 320f;
	[Export] public float RoarShoveDuration = 0.35f;
	[Export] public int RoarDustAmount = 90;
	// Plug the roar sound in here once there is one.
	[Export] public AudioStream RoarSound;

	private Hitbox _clawHitbox;
	private EnemySlashTrail _clawTrail;
	private GpuParticles2D _breath;
	private GpuParticles2D _embers;
	private Sprite2D _mouthGlow;
	// Real light for the fire: additive particles alone get dimmed by the map's CanvasModulate.
	private PointLight2D _breathLight;
	private PointLight2D _sheenLight;
	private Tween _breathTween;
	private readonly RandomNumberGenerator _rng = new();

	private bool _busy;
	private bool _flying;
	private float _baseGravity;
	private float _hurtTimer;
	private float _clawCooldownTimer;
	private float _fireCooldownTimer;
	private float _flightCooldownTimer;
	private float _rushCooldownTimer;
	private bool _chainRush;
	private bool _phaseTwo;
	private bool _desperationDone;
	private float _orbCooldownTimer;
	private float _bigMoveRollTimer;
	private float _underFireCooldownTimer;
	private float _beamCooldownTimer;
	private bool _beamUnlockedUsed;
	private bool _recovering;
	private int _moveSerial;
	private readonly System.Collections.Generic.Queue<ulong> _recentHits = new();
	private Hitbox _rushHitbox;
	private bool _asleep;
	private bool _waking;
	private Vector2 _spriteBaseScale;
	private Vector2 _spriteBasePosition;
	private float _sleepTime;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;

		// Asleep it isn't in the "boss" group yet: that's what makes the player's boss bar (and boss
		// zoom) kick in, and those belong after the roar.
		if (StartAsleep && Sprite is not null)
		{
			_asleep = true;
			_busy = true;
			_spriteBaseScale = Sprite.Scale;
			_spriteBasePosition = Sprite.Position;
			Sprite.Play("sleep");
		}
		else
		{
			AddToGroup("boss");
			DetectionRange = AwakeDetectionRange;
		}
		_rng.Randomize();
		_baseGravity = Gravity;

		_clawHitbox = BuildHitbox("ClawHitbox", new CircleShape2D { Radius = ClawRadius });
		_rushHitbox = BuildHitbox("RushHitbox", new RectangleShape2D { Size = new Vector2(RushHitWidth, RushHitHeight) });
		BuildBreath();
		BuildBodyChaos();
		_clawTrail = new EnemySlashTrail
		{
			CoreWidth = 6f,
			GlowWidth = 22f,
			GlowColor = ClawTrailGlow,
			CoreColor = ClawTrailCore,
			SparkAmount = 10,
			SparkLifetime = 0.3,
		};
		Visual.AddChild(_clawTrail);

		Stats.HitTaken += _ => _hurtTimer = HurtDuration;
		Stats.HitTaken += _ => CountPunishHit();
		_fireCooldownTimer = 1.5f;
		_flightCooldownTimer = FlightFirstDelay;
		_rushCooldownTimer = RushFirstDelay;
	}

	private Hitbox BuildHitbox(string name, Shape2D shape)
	{
		var hitbox = new Hitbox { Name = name, CollisionLayer = 0, CollisionMask = 2 };
		hitbox.AddChild(new CollisionShape2D { Name = "CollisionShape2D", Shape = shape, Disabled = true });
		AddChild(hitbox);
		return hitbox;
	}

	// ── Boss plumbing ───────────────────────────────────────────────────────────────────────

	protected override bool IsDefeated() => SaveManager.Instance.IsBossDefeated(PersistenceId);

	protected override void OnDefeated()
	{
		SaveManager.Instance.MarkBossDefeated(PersistenceId);
		// Killed mid wake-up: the roar coroutine aborts, so let the player go here.
		if (_waking)
			PlayerReads.Find(this)?.HoldForCutscene(false);
		CallDeferred(MethodName.SpawnLoot);
		IsQueuedForRemoval = true;
		RemoveFromGroup("boss");
		SetDeferred(CollisionObject2D.PropertyName.CollisionLayer, 0);
		_clawHitbox.CallDeferred(Hitbox.MethodName.Deactivate);
		StopBreath();
		_rushHitbox.CallDeferred(Hitbox.MethodName.Deactivate);
		_ = DieAndFall();
	}

	// Shot down mid-flight → falls to the ground first, then plays the death.
	private async Task DieAndFall()
	{
		Gravity = _baseGravity;
		Sprite?.Play(_flying ? "hurt_flying" : "death");
		while (_flying && IsInsideTree() && !IsOnFloor())
		{
			Velocity = new Vector2(0f, Velocity.Y + Gravity * (float)GetPhysicsProcessDeltaTime());
			MoveAndSlide();
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			if (!IsInstanceValid(this))
				return;
		}
		if (Sprite is null)
		{
			QueueFree();
			return;
		}
		Sprite.Play("death");
		Sprite.AnimationFinished += QueueFree;
	}

	protected override bool ContactDamageEnabled => false;

	protected override bool CanTurnToFacePlayer => !_busy;

	// ── AI ──────────────────────────────────────────────────────────────────────────────────

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;

		float dt = (float)delta;
		_hurtTimer -= dt;
		_clawCooldownTimer -= dt;
		_fireCooldownTimer -= dt;
		_flightCooldownTimer -= dt;
		_rushCooldownTimer -= dt;
		_underFireCooldownTimer -= dt;
		_beamCooldownTimer -= dt;
		_bigMoveRollTimer -= dt;
		_orbCooldownTimer -= dt;

		base._PhysicsProcess(delta);
		if (_asleep)
		{
			if (!_waking)
				UpdateSleepBreathing(dt);
			var sleeper = PlayerReads.Find(this);
			if (sleeper is not null && !_waking && !IsQueuedForRemoval
				&& Mathf.Abs(sleeper.GlobalPosition.X - GlobalPosition.X) <= WakeRange)
				_ = WakeAndRoar();
			return;
		}
		// Being punished hard → rises and pours fire under itself, even out of its own recovery.
		// Phase change and the near-death beam take over as soon as it's free (or recovering).
		if (!ShowcaseMode && !IsQueuedForRemoval && IsOnFloor() && (!_busy || _recovering))
		{
			float hp = (float)Stats.CurrentHealth / Mathf.Max(1, Stats.MaxHealth);
			if (!_phaseTwo && hp <= PhaseTwoHealthRatio)
			{
				_ = PhaseTransition();
				return;
			}
			if (_phaseTwo && !_desperationDone && hp <= DesperationHealthRatio)
			{
				_ = DesperationBeam();
				return;
			}
		}
		if (!ShowcaseMode && !IsQueuedForRemoval && IsOnFloor() && (!_busy || _recovering) && PunishedHard())
		{
			_ = UnderFire();
			return;
		}
		if (IsQueuedForRemoval || _busy || !IsOnFloor() || !PlayerDetected)
			return;

		var player = PlayerReads.Find(this);
		if (player is null)
			return;

		float distance = Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X);

		if (ShowcaseMode)
		{
			RunShowcaseStep(player, distance);
			return;
		}

		if (_chainRush)
		{
			_chainRush = false;
			_ = SavageRush(player);
			return;
		}
		// Close → claws. Mid range → fire (or the beam). Far → one of the big moves, rolled now and
		// then (not every frame) so it also just walks in sometimes.
		if (distance <= ClawRange && _clawCooldownTimer <= 0f)
		{
			if (_rng.Randf() < ClawTripleChance)
				_ = ClawTripleRoar();
			else
				_ = ClawCombo(_rng.Randf() < ClawSpitChance);
			return;
		}
		if (_phaseTwo && distance >= OrbMinRange && _orbCooldownTimer <= 0f && _bigMoveRollTimer <= 0f)
		{
			_bigMoveRollTimer = BigMoveRollInterval;
			if (_rng.Randf() < OrbChance)
			{
				_ = HumanityOrbs(player);
				return;
			}
		}
		if (distance >= FireMinRange && distance <= FireMaxRange && _fireCooldownTimer <= 0f)
		{
			_ = ShouldBeam() ? DarkBeam() : FireBreath();
			return;
		}
		if (distance < BigMoveMinRange || _bigMoveRollTimer > 0f)
			return;
		_bigMoveRollTimer = BigMoveRollInterval;
		if (_flightCooldownTimer <= 0f && _rng.Randf() < FlightChance)
			_ = AirAssault(player);
		else if (_rushCooldownTimer <= 0f && _rng.Randf() < RushChance)
			_ = SavageRush(player);
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_busy)
			return currentVelocityX;
		if (_hurtTimer > 0f)
			return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);

		float toward = Mathf.Sign(distanceX);
		if (Mathf.Abs(distanceX) > EngageDistance && CanStepTo(toward))
			return toward * MoveSpeed;
		return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null || _busy)
			return;
		string anim = _hurtTimer > 0f ? "hurt_floor" : Mathf.Abs(velocity.X) > 5f ? "run" : "idle";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	// ── Moves ───────────────────────────────────────────────────────────────────────────────

	private async Task ClawCombo(bool withSpit)
	{
		_busy = true;
		_clawCooldownTimer = ClawCooldown;
		Velocity = new Vector2(0f, Velocity.Y);
		FaceTowardsPlayer();
		if (!await ClawSwipe(0, 1f))
			return;
		FaceTowardsPlayer();
		if (!await ClawSwipe(ClawSecondStartFrame, ClawSecondSpeed))
			return;
		if (!withSpit)
		{
			EndMove(ClawRecovery);
			return;
		}
		FaceTowardsPlayer();
		if (!await Breathe("fire_attack", GroundMouth, GroundBreathAngle, SpitAreaCenter, SpitAreaSize, ClawSpitDuration, ClawSpitGatherSpeed))
			return;
		EndMove(ClawRecovery);
	}

	// One swipe: rear up, rake down. Trail starts on ClawTrailFrame; on the strike frames the hitbox
	// is live, it steps in and a gust blows off the claw.
	private async Task<bool> ClawSwipe(int startFrame, float speedScale)
	{
		if (Sprite is null)
			return false;
		float dir = FacingRight ? 1f : -1f;
		PlaceInFront(_clawHitbox, new Vector2(ClawReach, 0f));
		Sprite.SpeedScale = speedScale;
		Sprite.Play("attack_1");
		Sprite.Frame = startFrame;
		bool trailed = false, struck = false, active = false;
		bool ok = true;
		while (Sprite.Animation == "attack_1" && Sprite.IsPlaying())
		{
			int frame = Sprite.Frame;
			if (!trailed && frame >= ClawTrailFrame)
			{
				trailed = true;
				_clawTrail.Play(ClawArcCenter, ClawArcRadius, ClawArcStart, ClawArcEnd, ClawTrailDuration / speedScale);
			}
			bool live = frame >= ClawHitFrameFrom && frame <= ClawHitFrameTo;
			if (live && !active)
			{
				// Pierces the post-hit i-frames so both swipes of the combo can land (a dash still dodges).
				_clawHitbox.Activate(Stats, ignorePostHitInvulnerability: true);
				// Step in only if the player is beyond the claw's reach — never walk on top of them.
				var target = PlayerReads.Find(this);
				if (target is not null && Mathf.Abs(target.GlobalPosition.X - GlobalPosition.X) > ClawReach)
					Velocity = new Vector2(dir * ClawLungeSpeed, Velocity.Y);
				if (!struck)
				{
					struck = true;
					Vector2 claw = GlobalPosition + new Vector2(dir * ClawReach, 10f);
					for (int i = 0; i < ClawGustStreaks; i++)
						SpawnWindStreak(claw, dir, 0.7f, ClawGustReach, RoarStreakLength * 0.6f, RoarWindSpread * 1.4f);
				}
			}
			else if (!live && active)
			{
				_clawHitbox.Deactivate();
				Velocity = new Vector2(0f, Velocity.Y);
			}
			active = live;
			if (!await Wait(0f))
			{
				ok = false;
				break;
			}
		}
		_clawHitbox.Deactivate();
		Velocity = new Vector2(0f, Velocity.Y);
		if (Sprite is not null)
			Sprite.SpeedScale = 1f;
		return ok;
	}

	// Three swipes, then the roar from its entrance (shorter) — the last swipe and the roar shove the
	// player back out so the punish window afterwards has to be walked back into.
	private async Task ClawTripleRoar()
	{
		_busy = true;
		_clawCooldownTimer = ClawCooldown;
		Velocity = new Vector2(0f, Velocity.Y);
		FaceTowardsPlayer();
		if (!await ClawSwipe(0, 1f))
			return;
		FaceTowardsPlayer();
		if (!await ClawSwipe(ClawSecondStartFrame, ClawSecondSpeed))
			return;
		FaceTowardsPlayer();
		if (!await ClawSwipe(ClawSecondStartFrame, ClawThirdSpeed))
			return;
		FaceTowardsPlayer();
		if (!await Roar(PlayerReads.Find(this), held: false, ClawRoarDuration))
			return;
		EndMove(ClawTripleRecovery);
	}

	private void FaceTowardsPlayer()
	{
		var player = PlayerReads.Find(this);
		if (player is not null)
			FaceTowards(player.GlobalPosition.X);
	}

	private async Task FireBreath()
	{
		_busy = true;
		_fireCooldownTimer = FireCooldown;
		Velocity = new Vector2(0f, Velocity.Y);
		if (!await Breathe("fire_attack", GroundMouth, GroundBreathAngle, FireAreaCenter, FireAreaSize, BreathDuration))
			return;
		EndMove(FireRecovery);
	}

	// ── Dark beam ───────────────────────────────────────────────────────────────────────────

	private bool ShouldBeam()
	{
		if (Stats.CurrentHealth > Stats.MaxHealth * BeamHealthRatio || _beamCooldownTimer > 0f)
			return false;
		if (!_beamUnlockedUsed)
			return true;
		return _rng.Randf() < BeamChance;
	}

	private async Task DarkBeam()
	{
		_busy = true;
		_beamUnlockedUsed = true;
		_beamCooldownTimer = BeamCooldown;
		_fireCooldownTimer = FireCooldown;
		Velocity = new Vector2(0f, Velocity.Y);
		FaceTowardsPlayer();
		float dir = FacingRight ? 1f : -1f;

		// 1. Starts as an ordinary breath: gather, then the fire stream for BeamFireLead.
		if (Sprite is null)
			return;
		_breath.Position = GroundMouth;
		_breath.RotationDegrees = GroundBreathAngle;
		_embers.Position = GroundMouth;
		_embers.RotationDegrees = GroundBreathAngle;
		_mouthGlow.Position = GroundMouth;
		_breathLight.Position = GroundMouth + new Vector2(140f, 0f);
		Sprite.Play("fire_attack");
		_breathTween?.Kill();
		_breathTween = CreateTween();
		_breathTween.TweenProperty(_mouthGlow, "modulate:a", 0.9f, 0.5f);
		while (Sprite.Animation == "fire_attack" && Sprite.IsPlaying() && Sprite.Frame < BreathFrame)
		{
			if (!await Wait(0f))
				return;
		}
		Sprite.Pause();
		_breath.Emitting = true;
		_embers.Emitting = true;
		float lead = 0f;
		float tickTimer = 0f;
		while (lead < BeamFireLead)
		{
			float dt = (float)GetPhysicsProcessDeltaTime();
			_breathLight.Energy = BreathLightEnergy * _rng.RandfRange(0.55f, 1.15f);
			tickTimer -= dt;
			if (tickTimer <= 0f && BurnPlayerIfInCone(FireAreaCenter, FireAreaSize))
				tickTimer = BreathTickInterval;
			if (!await Wait(0f))
			{
				StopBreath();
				return;
			}
			lead += dt;
		}

		// 2. The tell: the fire dies back, violet light sucks into the mouth, the ground trembles.
		_breath.Emitting = false;
		_embers.Emitting = false;
		GpuParticles2D charge = SpawnBeamCharge();
		_breathTween?.Kill();
		_breathTween = CreateTween();
		_breathTween.TweenProperty(_mouthGlow, "modulate", BeamGlowColor with { A = 1f }, BeamChargeTime);
		_breathTween.Parallel().TweenProperty(_mouthGlow, "scale", Vector2.One * 1.1f, BeamChargeTime);
		_breathTween.Parallel().TweenProperty(_breathLight, "color", BeamGlowColor, BeamChargeTime);
		_breathTween.Parallel().TweenProperty(_breathLight, "energy", BreathLightEnergy * 1.4f, BeamChargeTime);
		PlayerReads.Find(this)?.ShakeCamera(4f, BeamChargeTime);
		if (!await Wait(BeamChargeTime))
		{
			charge?.QueueFree();
			EndBeamLook();
			return;
		}
		if (charge is not null)
			charge.Emitting = false;

		// 3. The beam.
		Vector2 mouth = GlobalPosition + new Vector2(dir * GroundMouth.X, GroundMouth.Y);
		float floorY = FloorBelow(mouth) ?? GlobalPosition.Y + BodyBottom;
		var beam = new DarkBeamVisual(BeamGlowColor, BeamCoreColor);
		GetTree().CurrentScene.AddChild(beam);
		RoarDarkenFlash();
		RoarDust();
		GpuParticles2D spray = SpawnRoarSpray(dir);

		// Sweep 1: along the floor, from just in front of it out to BeamReach.
		bool ok = await SweepBeam(beam, mouth, dir, BeamSweepGroundTime, t =>
			new Vector2(mouth.X + dir * Mathf.Lerp(90f, BeamReach, t), floorY), markGround: true, spray);
		if (spray is not null)
			spray.Emitting = false;
		// Sweep 2: from the far floor up through the air.
		if (ok)
		{
			Vector2 farFloor = new(mouth.X + dir * BeamReach, floorY);
			float startAngle = (farFloor - mouth).Angle();
			float endAngle = new Vector2(dir, 0f).Rotated(-dir * Mathf.DegToRad(BeamAirAngle)).Angle();
			ok = await SweepBeam(beam, mouth, dir, BeamSweepAirTime, t =>
				mouth + Vector2.Right.Rotated(Mathf.LerpAngle(startAngle, endAngle, t)) * BeamReach, markGround: false, null);
		}
		beam.FadeOut();
		EndBeamLook();
		if (!ok)
			return;

		// 4. Exhausted: mouth closes, head stays down — the big opening.
		Sprite.Play("fire_attack");
		while (Sprite.Animation == "fire_attack" && Sprite.IsPlaying())
		{
			if (!await Wait(0f))
				return;
		}
		EndMove(BeamExhaustTime);
	}

	// Drives the beam's end point over `time` (t: 0→1), hitting the player once if the beam touches
	// them, and dropping burst marks along the way (on the floor, or along the arc in the air).
	private async Task<bool> SweepBeam(DarkBeamVisual beam, Vector2 mouth, float dir, float time,
		System.Func<float, Vector2> endAt, bool markGround, GpuParticles2D spray)
	{
		bool hit = false;
		float elapsed = 0f;
		float shakeTimer = 0f;
		var shaken = PlayerReads.Find(this);
		Vector2 lastMark = endAt(0f);
		SpawnBeamMark(lastMark);
		while (elapsed < time)
		{
			float t = Mathf.Clamp(elapsed / time, 0f, 1f);
			Vector2 end = endAt(t);
			beam.Set(mouth, end);
			// Chaos: violent shake (re-armed so it never settles), wind blown down the beam, spray at the hit.
			shakeTimer -= (float)GetPhysicsProcessDeltaTime();
			if (shakeTimer <= 0f)
			{
				shaken?.ShakeCamera(BeamShakeStrength, 0.3f);
				shakeTimer = 0.2f;
			}
			Vector2 along = end - mouth;
			Vector2 normal = new Vector2(-along.Y, along.X).Normalized();
			for (int i = 0; i < BeamWindPerFrame; i++)
			{
				Vector2 origin = mouth + along * _rng.RandfRange(0.05f, 0.85f) + normal * _rng.RandfRange(-40f, 40f);
				SpawnWindStreak(origin, along, 1f, BeamWindReach, RoarStreakLength, 10f, BeamWindColor);
			}
			if (spray is not null)
				spray.GlobalPosition = end;
			_breathLight.Energy = BreathLightEnergy * _rng.RandfRange(1f, 1.6f);
			if (end.DistanceTo(lastMark) >= BeamMarkSpacing)
			{
				lastMark = end;
				SpawnBeamMark(end);
			}
			if (!hit && PlayerReads.Find(this) is { } player
				&& DistanceToSegment(player.GlobalPosition, mouth, end) <= BeamHitHalfWidth + BreathPlayerMargin)
				hit = HitUnparryable(player, BeamDamageMultiplier, end, BeamKnockback);
			if (!await Wait(0f))
				return false;
			elapsed += (float)GetPhysicsProcessDeltaTime();
		}
		return true;
	}

	private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
	{
		Vector2 ab = b - a;
		float t = Mathf.Clamp((p - a).Dot(ab) / Mathf.Max(0.0001f, ab.LengthSquared()), 0f, 1f);
		return p.DistanceTo(a + ab * t);
	}

	// A violet mark where the beam passed; it bursts BeamBurstDelay later.
	private void SpawnBeamMark(Vector2 at)
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		var dot = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres");
		var mark = new Sprite2D { Texture = dot, Material = additive, Modulate = BeamGlowColor with { A = 0.9f }, Scale = Vector2.One * 0.5f, ZIndex = 4 };
		scene.AddChild(mark);
		mark.GlobalPosition = at;
		Tween tween = mark.CreateTween();
		// Swells and flickers while it's about to go.
		tween.TweenProperty(mark, "scale", Vector2.One * 0.9f, BeamBurstDelay).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
		tween.TweenCallback(Callable.From(() =>
		{
			BeamBurst(at);
			mark.QueueFree();
		}));
	}

	private void BeamBurst(Vector2 at)
	{
		if (GetTree()?.CurrentScene is not Node scene)
			return;
		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		var dot = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres");
		var flash = new Sprite2D { Texture = dot, Material = additive, Modulate = BeamCoreColor, Scale = Vector2.One * 0.4f, ZIndex = 5 };
		scene.AddChild(flash);
		flash.GlobalPosition = at;
		var ring = new Line2D { Width = 6f, DefaultColor = BeamGlowColor, Closed = true, Material = additive, ZIndex = 5 };
		for (int i = 0; i < 24; i++)
			ring.AddPoint(Vector2.Right.Rotated(Mathf.Tau * i / 24f) * BeamBurstRadius);
		scene.AddChild(ring);
		ring.GlobalPosition = at;
		Tween tween = flash.CreateTween();
		tween.TweenProperty(flash, "scale", Vector2.One * (BeamBurstRadius / 32f), 0.18f);
		tween.Parallel().TweenProperty(flash, "modulate:a", 0f, 0.3f);
		tween.Parallel().TweenProperty(ring, "scale", Vector2.One * 1.3f, 0.3f).From(Vector2.One * 0.3f);
		tween.Parallel().TweenProperty(ring, "modulate:a", 0f, 0.3f);
		tween.TweenCallback(Callable.From(() => { flash.QueueFree(); ring.QueueFree(); }));
		var light = new PointLight2D
		{
			Texture = GD.Load<Texture2D>("res://resources/lighting/PointLightGradient.tres"),
			TextureScale = 1.2f,
			Color = BeamGlowColor,
			Energy = 1.5f,
		};
		flash.AddChild(light);
		if (PlayerReads.Find(this) is { } player && player.GlobalPosition.DistanceTo(at) <= BeamBurstRadius + BreathPlayerMargin)
			HitUnparryable(player, BeamBurstDamageMultiplier, at, BeamKnockback * 0.6f);
	}

	// A strong hit that a parry can't stop (the player's parry lives in IncomingHitInterceptor —
	// switched off just for this hit, same trick as AssassinBlade). Post-hit and dash i-frames still
	// count. True when it landed.
	private bool HitUnparryable(Metroidvania.Player.Player player, float multiplier, Vector2 from, float knockback)
	{
		Stats stats = player.GetNodeOrNull<Stats>("Stats");
		if (stats is null || stats.IsInvulnerable)
			return false;
		System.Func<bool> interceptor = stats.IncomingHitInterceptor;
		stats.IncomingHitInterceptor = null;
		int before = stats.CurrentHealth;
		stats.TakeDamage(Mathf.RoundToInt(Stats.AttackPower * multiplier));
		stats.IncomingHitInterceptor = interceptor;
		if (stats.CurrentHealth >= before)
			return false;
		Vector2 away = (player.GlobalPosition - from).Normalized();
		player.ApplyKnockbackThroughBlock(away == Vector2.Zero ? Vector2.Up : away, knockback);
		return true;
	}

	// Violet motes drawn into the mouth while the beam charges.
	private GpuParticles2D SpawnBeamCharge()
	{
		var ramp = new Gradient
		{
			Offsets = new[] { 0f, 0.3f, 1f },
			Colors = new[] { BeamGlowColor with { A = 0f }, BeamGlowColor, BeamCoreColor },
		};
		var charge = new GpuParticles2D
		{
			Amount = 60,
			Lifetime = 0.4f,
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			Position = GroundMouth,
			ProcessMaterial = new ParticleProcessMaterial
			{
				EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.SphereSurface,
				EmissionSphereRadius = 90f,
				Gravity = Vector3.Zero,
				RadialAccelMin = -700f,
				RadialAccelMax = -500f,
				ScaleMin = 0.05f,
				ScaleMax = 0.12f,
				ColorRamp = new GradientTexture1D { Gradient = ramp },
			},
		};
		Visual.AddChild(charge);
		charge.Emitting = true;
		charge.Finished += charge.QueueFree;
		return charge;
	}

	// Mouth glow and breath light back to the fire colours, off.
	private void EndBeamLook()
	{
		StopBreath();
		_mouthGlow.Modulate = BreathFlameColor with { A = 0f };
		_mouthGlow.Scale = Vector2.One * 0.6f;
		_breathLight.Color = BreathFlameColor;
	}

	private float? FloorBelow(Vector2 from)
	{
		var query = PhysicsRayQueryParameters2D.Create(from, from + Vector2.Down * 800f, 1, new Godot.Collections.Array<Rid> { GetRid() });
		var hit = GetWorld2D().DirectSpaceState.IntersectRay(query);
		return hit.Count > 0 ? ((Vector2)hit["position"]).Y : null;
	}

	private async Task SavageRush(Metroidvania.Player.Player player)
	{
		_busy = true;
		_rushCooldownTimer = Mathf.Max(RushCooldown, BigMoveLockout);
		_flightCooldownTimer = Mathf.Max(_flightCooldownTimer, BigMoveLockout);
		Velocity = new Vector2(0f, Velocity.Y);
		FaceTowards(player.GlobalPosition.X);
		float dir = FacingRight ? 1f : -1f;

		// Tell: a snarl — crouched, mouth glowing, the ground shaking a little.
		Sprite?.Play("idle_run");
		_mouthGlow.Position = GroundMouth;
		_breathTween?.Kill();
		_breathTween = CreateTween();
		_breathTween.TweenProperty(_mouthGlow, "modulate:a", 0.9f, RushTellDuration);
		player.ShakeCamera(5f, RushTellDuration);
		if (!await Wait(RushTellDuration))
			return;
		// The glow sits where the mouth is in the breath pose — running, the head is elsewhere and it
		// would float on the chest. Snuff it as it sets off.
		_breathTween?.Kill();
		_mouthGlow.Modulate = BreathFlameColor with { A = 0f };

		// Body hitbox: low, in front of the chest — jumpable.
		_rushHitbox.Position = new Vector2(dir * RushHitWidth * 0.25f, BodyBottom - RushHitHeight * 0.5f);

		float elapsed = 0f;
		int swipes = 0;
		bool reachedEnd = false;
		while (!reachedEnd && elapsed < RushMaxTime)
		{
			// A stretch of running (no fire while running — it has no animation for it; the fire comes at
			// the end, a short spit with the proper breath pose).
			if (Sprite is not null)
			{
				Sprite.SpeedScale = RushRunSpeedScale;
				Sprite.Play("run");
			}
			_rushHitbox.Activate(Stats);
			float stretch = 0f;
			while (stretch < RushFireStretch)
			{
				if (!CanStepTo(dir))
				{
					reachedEnd = true;
					break;
				}
				float dt = (float)GetPhysicsProcessDeltaTime();
				Velocity = new Vector2(dir * RushSpeed, Velocity.Y);
				if (!await Wait(0f))
				{
					EndRush();
					return;
				}
				stretch += dt;
				elapsed += dt;
			}
			_rushHitbox.Deactivate();
			if (Sprite is not null)
				Sprite.SpeedScale = 1f;
			if (reachedEnd || swipes >= RushMaxSwipes)
				break;

			// Pulls up for a claw swipe, then carries on.
			swipes++;
			if (!await ClawSwipe(ClawSecondStartFrame, ClawSecondSpeed))
			{
				EndRush();
				return;
			}
			// Run, swipe, run, swipe — then the closing spit (it used to keep charging to the far wall).
			if (swipes >= RushMaxSwipes)
				break;
			elapsed += 0.5f;
		}

		EndRush();
		Velocity = new Vector2(0f, Velocity.Y);
		// Last word: a short spit of fire with the breath animation.
		FaceTowardsPlayer();
		if (!await Breathe("fire_attack", GroundMouth, GroundBreathAngle, SpitAreaCenter, SpitAreaSize, ClawSpitDuration, ClawSpitGatherSpeed))
			return;
		// Spent: the punish window.
		EndMove(RushRecovery);
	}

	private void EndRush()
	{
		_rushHitbox?.Deactivate();
		if (Sprite is not null)
			Sprite.SpeedScale = 1f;
		StopBreath();
	}

	private async Task AirAssault(Metroidvania.Player.Player player)
	{
		_busy = true;
		_flightCooldownTimer = Mathf.Max(FlightCooldown, BigMoveLockout);
		_rushCooldownTimer = Mathf.Max(_rushCooldownTimer, BigMoveLockout);
		Velocity = Vector2.Zero;

		// Take off.
		Sprite?.Play("floor_fly");
		if (!await Wait(0.2f))
			return;
		_flying = true;
		Gravity = 0f;
		Sprite?.Play("fly_idle");
		if (!await FlyTo(GlobalPosition + new Vector2(0f, -TakeOffHeight), FlightMaxLegTime))
			return;

		// Out to the end of the arena on its own side, up high.
		float side = Mathf.Sign(GlobalPosition.X - player.GlobalPosition.X);
		if (side == 0f)
			side = 1f;
		float sweepY = player.GlobalPosition.Y + PlayerFeetOffset(player) - SweepHeight - BodyBottom;
		float startX = ClampToOpenAir(new Vector2(GlobalPosition.X, sweepY), player.GlobalPosition.X + side * SweepStartOffset);
		Sprite?.Play("flying");
		if (!await FlyTo(new Vector2(startX, sweepY), FlightMaxLegTime))
			return;

		// Turn to face across the arena.
		float dir = -side;
		if (FacingRight != dir > 0f)
		{
			Sprite?.Play("flying_turn");
			FaceTowards(GlobalPosition.X + dir);
			if (!await Wait(0.2f))
				return;
		}
		Velocity = Vector2.Zero;
		if (!await AerialSweep(dir))
			return;

		// Land: drop straight down.
		Sprite?.Play("fly_idle");
		Gravity = _baseGravity;
		if (!await WaitUntilLanded(3f))
			return;
		_flying = false;
		Sprite?.Play("land");
		if (!await Wait(0.2f))
			return;
		// Sometimes it lands straight into a rush.
		_chainRush = _rng.Randf() < RushAfterSweepChance;
		EndMove(_chainRush ? 0.15f : FlightRecovery);
	}

	// ── Phase 2 ─────────────────────────────────────────────────────────────────────────────

	// Channels darkness (violet motes sucked in, the ground shaking harder and harder), then it all
	// goes off: a blast around it that can't be parried, the screen flashes, humanities fly everywhere.
	private async Task PhaseTransition()
	{
		_busy = true;
		_recovering = false;
		_moveSerial++;
		_phaseTwo = true;
		_orbCooldownTimer = 3f;
		Velocity = new Vector2(0f, Velocity.Y);
		var player = PlayerReads.Find(this);

		Sprite?.Play("idle_run");
		GpuParticles2D charge = SpawnBeamCharge();
		if (charge is not null)
		{
			charge.Position = new Vector2(0f, -30f);
			charge.Amount = 120;
		}
		_mouthGlow.Position = new Vector2(0f, -30f);
		_breathTween?.Kill();
		_breathTween = CreateTween();
		_breathTween.TweenProperty(_mouthGlow, "modulate", BeamGlowColor with { A = 1f }, PhaseChargeTime);
		_breathTween.Parallel().TweenProperty(_mouthGlow, "scale", Vector2.One * 3.5f, PhaseChargeTime);
		_breathTween.Parallel().TweenProperty(_breathLight, "color", BeamGlowColor, 0.2f);
		_breathTween.Parallel().TweenProperty(_breathLight, "energy", BreathLightEnergy * 2.5f, PhaseChargeTime);
		float t = 0f;
		while (t < PhaseChargeTime)
		{
			player?.ShakeCamera(Mathf.Lerp(3f, 14f, t / PhaseChargeTime), 0.15f);
			if (!await Wait(0.1f))
			{
				charge?.QueueFree();
				EndBeamLook();
				return;
			}
			t += 0.1f;
		}
		if (charge is not null)
			charge.Emitting = false;

		// The blast.
		Vector2 center = GlobalPosition + new Vector2(0f, -20f);
		player?.ShakeCamera(28f, 1.2f);
		ScreenFlash(BeamCoreColor, 0.5f);
		RoarDust();
		SpawnShockRing(center, PhaseBlastRadius);
		for (int i = 0; i < 10; i++)
			SpawnWindStreak(center, Vector2.Right.Rotated(Mathf.Tau * i / 10f), 1f, PhaseBlastRadius * 1.3f, RoarStreakLength, 15f, BeamWindColor);
		if (player is not null && player.GlobalPosition.DistanceTo(center) <= PhaseBlastRadius)
			HitUnparryable(player, PhaseBlastDamageMultiplier, center, BeamKnockback * 1.3f);
		ScatterHumanities(center, player);
		EndBeamLook();
		// Spent from the blast for a moment.
		EndMove(1.2f);
	}

	// Humanities flung out of the blast in every direction, which then turn on the player.
	private void ScatterHumanities(Vector2 center, Metroidvania.Player.Player player)
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		for (int i = 0; i < PhaseScatterOrbs; i++)
		{
			// Blown out every which way, they slow down, then come for the player.
			var orb = new HumanityOrb
			{
				LaunchVelocity = Vector2.Right.Rotated(_rng.RandfRange(0f, Mathf.Tau)) * _rng.RandfRange(350f, 750f),
				HangTime = _rng.RandfRange(0.6f, 1.2f),
				Target = player,
				OnHitPlayer = at => { if (player is not null) HitUnparryable(player, OrbDamageMultiplier, at, BeamKnockback * 0.5f); },
			};
			scene.AddChild(orb);
			orb.GlobalPosition = center;
		}
	}

	// Humanities rise out of the ground around it and come for the player one after another.
	private async Task HumanityOrbs(Metroidvania.Player.Player player)
	{
		_busy = true;
		_orbCooldownTimer = OrbCooldown;
		Velocity = new Vector2(0f, Velocity.Y);
		FaceTowards(player.GlobalPosition.X);
		Sprite?.Play("idle_run");
		float floorY = FloorBelow(GlobalPosition) ?? GlobalPosition.Y + BodyBottom;
		if (GetTree().CurrentScene is not Node scene)
			return;
		var orbs = new System.Collections.Generic.List<HumanityOrb>();
		for (int i = 0; i < OrbCount; i++)
		{
			float x = GlobalPosition.X + _rng.RandfRange(-260f, 260f);
			var orb = new HumanityOrb
			{
				Target = player,
				// Each one waits a little longer before it goes, so they come one after another.
				HangTime = 0.4f + i * OrbLaunchInterval,
				OnHitPlayer = at => HitUnparryable(player, OrbDamageMultiplier, at, BeamKnockback * 0.5f),
			};
			scene.AddChild(orb);
			orb.GlobalPosition = new Vector2(x, floorY - 10f);
			orbs.Add(orb);
			player.ShakeCamera(3f, 0.1f);
			if (!await Wait(0.08f))
				return;
		}
		EndMove(OrbRecovery);
	}

	// The focused beam near death: DesperationSweeps sweeps in a row (floor, then air, alternating,
	// turning to face the player each time), then it collapses, dying, wide open.
	private async Task DesperationBeam()
	{
		_busy = true;
		_recovering = false;
		_moveSerial++;
		_desperationDone = true;
		Velocity = new Vector2(0f, Velocity.Y);
		var player = PlayerReads.Find(this);
		if (Sprite is null)
			return;

		// Charge: longer and more violent than the normal beam's.
		FaceTowardsPlayer();
		Sprite.Play("fire_attack");
		while (Sprite.Animation == "fire_attack" && Sprite.IsPlaying() && Sprite.Frame < BreathFrame)
		{
			if (!await Wait(0f))
				return;
		}
		Sprite.Pause();
		_mouthGlow.Position = GroundMouth;
		GpuParticles2D charge = SpawnBeamCharge();
		_breathTween?.Kill();
		_breathTween = CreateTween();
		_breathTween.TweenProperty(_mouthGlow, "modulate", BeamGlowColor with { A = 1f }, BeamChargeTime * 2f);
		_breathTween.Parallel().TweenProperty(_breathLight, "color", BeamGlowColor, 0.2f);
		player?.ShakeCamera(10f, BeamChargeTime * 2f);
		if (!await Wait(BeamChargeTime * 2f))
		{
			charge?.QueueFree();
			EndBeamLook();
			return;
		}
		if (charge is not null)
			charge.Emitting = false;
		RoarDarkenFlash();
		RoarDust();

		for (int i = 0; i < DesperationSweeps; i++)
		{
			FaceTowardsPlayer();
			float dir = FacingRight ? 1f : -1f;
			Vector2 mouth = GlobalPosition + new Vector2(dir * GroundMouth.X, GroundMouth.Y);
			float floorY = FloorBelow(mouth) ?? GlobalPosition.Y + BodyBottom;
			var beam = new DarkBeamVisual(BeamGlowColor, BeamCoreColor);
			GetTree().CurrentScene.AddChild(beam);
			bool ok;
			if (i % 2 == 0)
			{
				// Along the floor, out to its reach.
				ok = await SweepBeam(beam, mouth, dir, DesperationSweepTime, t =>
					new Vector2(mouth.X + dir * Mathf.Lerp(90f, BeamReach, t), floorY), markGround: true, null);
			}
			else
			{
				// Back from high in the air down onto the floor near it.
				float high = new Vector2(dir, 0f).Rotated(-dir * Mathf.DegToRad(BeamAirAngle)).Angle();
				float low = (new Vector2(mouth.X + dir * 120f, floorY) - mouth).Angle();
				ok = await SweepBeam(beam, mouth, dir, DesperationSweepTime, t =>
					mouth + Vector2.Right.Rotated(Mathf.LerpAngle(high, low, t)) * BeamReach, markGround: false, null);
			}
			beam.FadeOut();
			if (!ok)
			{
				EndBeamLook();
				return;
			}
			if (!await Wait(0.12f))
			{
				EndBeamLook();
				return;
			}
		}
		EndBeamLook();

		// Dying: it collapses (the death frames up to lying down), breathes heavily, then hauls
		// itself back up.
		Sprite.Play("death");
		while (Sprite.Animation == "death" && Sprite.Frame < 3)
		{
			if (!await Wait(0f))
				return;
		}
		Sprite.Play("sleep");
		Sprite.Position = _spriteBasePosition - new Vector2(0f, SleepPoseLift);
		float down = 0f;
		while (down < DesperationDownTime)
		{
			float rise = 0.04f * Mathf.Sin(down * Mathf.Tau / 0.9f);
			Sprite.Scale = new Vector2(_spriteBaseScale.X, _spriteBaseScale.Y * (1f + rise));
			if (!await Wait(0f))
				return;
			down += (float)GetPhysicsProcessDeltaTime();
		}
		Sprite.Scale = _spriteBaseScale;
		Sprite.Play("wake");
		while (Sprite.Animation == "wake" && Sprite.IsPlaying())
		{
			Sprite.Position = _spriteBasePosition - new Vector2(0f, Sprite.Frame <= 1 ? SleepPoseLift : 0f);
			if (!await Wait(0f))
				return;
		}
		Sprite.Position = _spriteBasePosition;
		EndMove(0.4f);
	}

	private void ScreenFlash(Color color, float duration)
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		var layer = new CanvasLayer { Layer = 50 };
		var flash = new ColorRect { Color = color with { A = 0.85f }, MouseFilter = Control.MouseFilterEnum.Ignore };
		flash.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		layer.AddChild(flash);
		scene.AddChild(layer);
		Tween tween = flash.CreateTween();
		tween.TweenProperty(flash, "color:a", 0f, duration);
		tween.TweenCallback(Callable.From(layer.QueueFree));
	}

	// A violet shockwave ring racing out to `radius`.
	private void SpawnShockRing(Vector2 at, float radius)
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		var ring = new Line2D
		{
			Width = 14f,
			DefaultColor = BeamGlowColor,
			Closed = true,
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			ZIndex = 6,
		};
		for (int i = 0; i < 48; i++)
			ring.AddPoint(Vector2.Right.Rotated(Mathf.Tau * i / 48f) * 40f);
		scene.AddChild(ring);
		ring.GlobalPosition = at;
		Tween tween = ring.CreateTween();
		tween.TweenProperty(ring, "scale", Vector2.One * (radius / 40f), 0.45f).From(Vector2.One * 0.2f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		tween.Parallel().TweenProperty(ring, "modulate:a", 0f, 0.6f);
		tween.Parallel().TweenProperty(ring, "width", 3f, 0.6f);
		tween.TweenCallback(Callable.From(ring.QueueFree));
	}

	// ── Showcase (video) ────────────────────────────────────────────────────────────────────

	private int _showcaseStep;
	private float _showcaseWait;
	private float _showcaseApproach;

	// Order of the reel: close-range claws, the breath, the big moves, the punish reaction, the beam.
	private static readonly string[] ShowcaseOrder =
	{
		"claws", "claws_spit", "claws_roar", "fire", "rush", "air", "rush", "under_fire", "beam", "phase", "orbs", "desperation",
	};

	private void RunShowcaseStep(Metroidvania.Player.Player player, float distance)
	{
		float dt = (float)GetPhysicsProcessDeltaTime();
		if (_showcaseWait > 0f)
		{
			_showcaseWait -= dt;
			return;
		}
		string step = ShowcaseOrder[_showcaseStep % ShowcaseOrder.Length];
		bool needsClose = step.StartsWith("claws");
		if (needsClose && distance > ClawRange && _showcaseApproach < ShowcaseApproachTime)
		{
			// Let the normal movement walk it in first.
			_showcaseApproach += dt;
			return;
		}
		_showcaseApproach = 0f;
		_showcaseStep++;
		_showcaseWait = ShowcasePause;
		GD.Print($"[Midir showcase] {step}");
		switch (step)
		{
			case "claws": _ = ClawCombo(false); break;
			case "claws_spit": _ = ClawCombo(true); break;
			case "claws_roar": _ = ClawTripleRoar(); break;
			case "fire": _ = FireBreath(); break;
			case "rush": _ = SavageRush(player); break;
			case "air": _ = AirAssault(player); break;
			case "under_fire": _ = UnderFire(); break;
			case "phase": _ = PhaseTransition(); break;
			case "orbs": _ = HumanityOrbs(player); break;
			case "desperation": _ = DesperationBeam(); break;
			default: _ = DarkBeam(); break;
		}
	}

	// ── Body chaos ──────────────────────────────────────────────────────────────────────────

	private readonly System.Collections.Generic.List<Sprite2D> _ghosts = new();
	private readonly System.Collections.Generic.List<Vector2> _ghostOffsets = new();
	private GpuParticles2D _bodySmoke;
	private GpuParticles2D _bodyEmbers;
	private float _jitterTimer;

	private void BuildBodyChaos()
	{
		if (Sprite is null)
			return;
		for (int i = 0; i < GhostCount; i++)
		{
			var ghost = new Sprite2D
			{
				Name = $"Fantasma{i}",
				Modulate = GhostColor,
				Scale = Sprite.Scale,
				// Additive: a dark tint would vanish against the dark body and cave.
				Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
				ShowBehindParent = true,
				Visible = false,
			};
			Visual.AddChild(ghost);
			Visual.MoveChild(ghost, 0);
			_ghosts.Add(ghost);
			_ghostOffsets.Add(Vector2.Zero);
		}

		var dot = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres");
		var grow = new Curve { MaxValue = 3f };
		grow.AddPoint(new Vector2(0f, 0.6f));
		grow.AddPoint(new Vector2(1f, 2.2f));
		var smokeRamp = new Gradient
		{
			Offsets = new[] { 0f, 0.25f, 1f },
			Colors = new[] { SmokeColor with { A = 0f }, SmokeColor, SmokeColor with { A = 0f } },
		};
		// Off the back and wings, drifting up and churning.
		_bodySmoke = new GpuParticles2D
		{
			Name = "HumoCuerpo",
			Amount = SmokeAmount,
			Lifetime = 1.8f,
			Randomness = 0.5f,
			LocalCoords = false,
			Texture = dot,
			Position = new Vector2(10f, -30f),
			ProcessMaterial = new ParticleProcessMaterial
			{
				EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
				EmissionBoxExtents = new Vector3(70f, 35f, 1f),
				Direction = new Vector3(0f, -1f, 0f),
				Spread = 35f,
				InitialVelocityMin = 15f,
				InitialVelocityMax = 45f,
				Gravity = new Vector3(0f, -25f, 0f),
				ScaleMin = 0.35f,
				ScaleMax = 0.7f,
				ScaleCurve = new CurveTexture { Curve = grow },
				ColorRamp = new GradientTexture1D { Gradient = smokeRamp },
				TurbulenceEnabled = true,
				TurbulenceNoiseStrength = 4f,
				TurbulenceNoiseScale = 2f,
				TurbulenceInfluenceMin = 0.1f,
				TurbulenceInfluenceMax = 0.3f,
			},
		};
		Visual.AddChild(_bodySmoke);
		Visual.MoveChild(_bodySmoke, 0);

		var emberRamp = new Gradient
		{
			Offsets = new[] { 0f, 0.5f, 1f },
			Colors = new[] { new Color(1f, 0.8f, 0.5f, 1f), new Color(0.75f, 0.35f, 1f, 1f), new Color(0.5f, 0.2f, 0.9f, 0f) },
		};
		_bodyEmbers = new GpuParticles2D
		{
			Name = "BrasasCuerpo",
			Amount = BodyEmberAmount,
			Lifetime = 1.4f,
			Randomness = 0.6f,
			LocalCoords = false,
			Texture = dot,
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			Position = new Vector2(10f, -30f),
			ProcessMaterial = new ParticleProcessMaterial
			{
				EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
				EmissionBoxExtents = new Vector3(70f, 35f, 1f),
				Direction = new Vector3(0f, -1f, 0f),
				Spread = 50f,
				InitialVelocityMin = 25f,
				InitialVelocityMax = 80f,
				Gravity = new Vector3(0f, -20f, 0f),
				ScaleMin = 0.03f,
				ScaleMax = 0.07f,
				ColorRamp = new GradientTexture1D { Gradient = emberRamp },
				TurbulenceEnabled = true,
				TurbulenceNoiseStrength = 6f,
				TurbulenceInfluenceMin = 0.2f,
				TurbulenceInfluenceMax = 0.5f,
			},
		};
		Visual.AddChild(_bodyEmbers);
		SetBodyChaosActive(!_asleep);
	}

	private void SetBodyChaosActive(bool active)
	{
		if (_bodySmoke is not null)
			_bodySmoke.Emitting = active;
		if (_bodyEmbers is not null)
			_bodyEmbers.Emitting = active;
		foreach (Sprite2D ghost in _ghosts)
			ghost.Visible = active;
		if (!active && Sprite is not null)
			Sprite.Offset = Vector2.Zero;
	}

	public override void _Process(double delta)
	{
		if (Sprite is null || (_asleep && !_waking) || IsQueuedForRemoval)
			return;
		float health = Stats is null ? 1f : (float)Stats.CurrentHealth / Mathf.Max(1, Stats.MaxHealth);
		float chaos = Mathf.Lerp(ChaosAtLowHealth, 1f, health) * (_busy ? ChaosWhileAttacking : 1f);

		// Jitter: a fresh random nudge every JitterInterval (not every frame — that just blurs).
		_jitterTimer -= (float)delta;
		if (_jitterTimer <= 0f)
		{
			_jitterTimer = JitterInterval;
			Sprite.Offset = new Vector2(_rng.RandfRange(-1f, 1f), _rng.RandfRange(-1f, 1f)) * JitterAmount * chaos;
			for (int i = 0; i < _ghostOffsets.Count; i++)
				_ghostOffsets[i] = new Vector2(_rng.RandfRange(-1f, 1f), _rng.RandfRange(-0.6f, 0.6f)) * GhostSpread * chaos;
		}

		// Afterimages: the current frame, shoved out of sync behind the body.
		Texture2D frame = Sprite.SpriteFrames?.GetFrameTexture(Sprite.Animation, Sprite.Frame);
		for (int i = 0; i < _ghosts.Count; i++)
		{
			Sprite2D ghost = _ghosts[i];
			ghost.Texture = frame;
			ghost.FlipH = Sprite.FlipH;
			ghost.Scale = Sprite.Scale;
			ghost.Position = Sprite.Position + _ghostOffsets[i];
		}
	}

	// ── Under fire ──────────────────────────────────────────────────────────────────────────

	private void CountPunishHit()
	{
		ulong now = Time.GetTicksMsec();
		_recentHits.Enqueue(now);
		while (_recentHits.Count > 0 && now - _recentHits.Peek() > (ulong)(UnderFireWindow * 1000f))
			_recentHits.Dequeue();
	}

	private bool PunishedHard()
	{
		if (_underFireCooldownTimer > 0f || _asleep)
			return false;
		ulong now = Time.GetTicksMsec();
		while (_recentHits.Count > 0 && now - _recentHits.Peek() > (ulong)(UnderFireWindow * 1000f))
			_recentHits.Dequeue();
		return _recentHits.Count >= UnderFireHits;
	}

	private async Task UnderFire()
	{
		_busy = true;
		_recovering = false;
		_moveSerial++; // a pending recovery must not hand control back mid-move
		_underFireCooldownTimer = UnderFireCooldown;
		_recentHits.Clear();
		Velocity = Vector2.Zero;

		// Floor under it, before it lifts off.
		var down = PhysicsRayQueryParameters2D.Create(GlobalPosition, GlobalPosition + Vector2.Down * 600f, 1, new Godot.Collections.Array<Rid> { GetRid() });
		var floorHit = GetWorld2D().DirectSpaceState.IntersectRay(down);
		float floorY = floorHit.Count > 0 ? ((Vector2)floorHit["position"]).Y : GlobalPosition.Y + BodyBottom;
		float centerX = GlobalPosition.X;

		// Tell: wings beat, it lifts, the mouth lights up.
		Sprite?.Play("floor_fly");
		_mouthGlow.Position = FlyMouth;
		_breathTween?.Kill();
		_breathTween = CreateTween();
		_breathTween.TweenProperty(_mouthGlow, "modulate:a", 0.9f, 0.3f);
		if (!await Wait(0.15f))
			return;
		_flying = true;
		Gravity = 0f;
		Sprite?.Play("fly_idle");
		if (!await FlyTo(GlobalPosition + new Vector2(0f, -UnderFireRise), 0.8f))
			return;

		// Mouth-open pose of the flying breath (head forward and down), held.
		if (Sprite is not null)
		{
			Sprite.SpeedScale = 1.8f;
			Sprite.Play("fire_attack_flying");
			while (Sprite.Animation == "fire_attack_flying" && Sprite.IsPlaying() && Sprite.Frame < BreathFrame)
			{
				if (!await Wait(0f))
					return;
			}
			Sprite.SpeedScale = 1f;
			Sprite.Pause();
		}
		// Pours fire straight down; the carpet spreads out both ways along the floor.
		_breath.Lifetime = SweepBreathLifetime;
		_breath.Position = FlyMouth;
		_breath.RotationDegrees = 90f;
		_embers.Position = FlyMouth;
		_embers.RotationDegrees = 90f;
		_breath.Emitting = true;
		_embers.Emitting = true;
		var light = SpawnFlamesAt(new Vector2(centerX, floorY), 60f);
		float elapsed = 0f;
		float patchTimer = 0f;
		float tickTimer = 0f;
		bool ok = true;
		while (elapsed < UnderFireDuration)
		{
			float dt = (float)GetPhysicsProcessDeltaTime();
			float radius = UnderFireRadius * Mathf.Min(1f, elapsed / UnderFireSpreadTime);
			_breathLight.Energy = BreathLightEnergy * _rng.RandfRange(0.55f, 1.15f);
			_breath.RotationDegrees = 90f + BreathWobble * Mathf.Sin(elapsed * 11f);

			patchTimer -= dt;
			if (patchTimer <= 0f)
			{
				patchTimer = UnderFirePatchInterval;
				foreach (float sideX in new[] { -1f, 1f })
				{
					float x = centerX + sideX * radius * _rng.RandfRange(0.6f, 1f);
					GpuParticles2D patch = SpawnFlamesAt(new Vector2(x, floorY), 45f);
					if (patch is not null)
						LingerThenStop(patch, UnderFireLinger);
				}
			}
			tickTimer -= dt;
			if (tickTimer <= 0f && BurnPlayerInRect(new Rect2(centerX - radius, floorY - UnderFireBurnHeight, radius * 2f, UnderFireBurnHeight)))
				tickTimer = BreathTickInterval;

			if (!await Wait(0f))
			{
				ok = false;
				break;
			}
			elapsed += dt;
		}
		StopBreath();
		_breath.Lifetime = BreathLifetime;
		if (light is not null)
			LingerThenStop(light, UnderFireLinger);
		if (!ok)
			return;

		// Down again, spent.
		Sprite?.Play("fly_idle");
		Gravity = _baseGravity;
		if (!await WaitUntilLanded(2f))
			return;
		_flying = false;
		Sprite?.Play("land");
		if (!await Wait(0.2f))
			return;
		EndMove(UnderFireRecovery);
	}

	// Open mouth held, gliding across at SweepSpeed while the stream rakes the floor ahead-below.
	private async Task<bool> AerialSweep(float dir)
	{
		if (Sprite is null)
			return false;
		// Mouth-open pose of the flying breath, with the usual gather glow.
		_mouthGlow.Position = FlyMouth;
		_breathTween?.Kill();
		_breathTween = CreateTween();
		_breathTween.TweenProperty(_mouthGlow, "modulate:a", 0.9f, 0.35f);
		Sprite.SpeedScale = 1.6f;
		Sprite.Play("fire_attack_flying");
		while (Sprite.Animation == "fire_attack_flying" && Sprite.IsPlaying() && Sprite.Frame < BreathFrame)
		{
			if (!await Wait(0f))
				return false;
		}
		Sprite.SpeedScale = 1f;
		Sprite.Pause();

		_breath.Lifetime = SweepBreathLifetime;
		_breath.Position = FlyMouth;
		_embers.Position = FlyMouth;
		_breath.Emitting = true;
		_embers.Emitting = true;
		float travelled = 0f;
		float sincePatch = SweepPatchSpacing;
		float tickTimer = 0f;
		float elapsed = 0f;
		bool ok = true;
		while (travelled < SweepTravel && CanFly(dir))
		{
			float dt = (float)GetPhysicsProcessDeltaTime();
			float wobble = BreathWobble * (Mathf.Sin(elapsed * 13f) * 0.6f + Mathf.Sin(elapsed * 7.3f + 1.7f) * 0.4f);
			_breath.RotationDegrees = SweepAngle + wobble;
			_embers.RotationDegrees = SweepAngle + wobble;
			_breathLight.Position = FlyMouth + new Vector2(140f, 0f).Rotated(Mathf.DegToRad(SweepAngle));
			_breathLight.Energy = BreathLightEnergy * _rng.RandfRange(0.55f, 1.15f);
			Velocity = new Vector2(dir * SweepSpeed, 0f);

			if (sincePatch >= SweepPatchSpacing)
			{
				sincePatch = 0f;
				GpuParticles2D patch = SpawnGroundFire(FlyMouth, SweepAngle);
				if (patch is not null)
					LingerThenStop(patch, SweepPatchLinger);
			}
			tickTimer -= dt;
			if (tickTimer <= 0f && BreathFloorHit(FlyMouth, SweepAngle) is Vector2 hit
				&& BurnPlayerInRect(new Rect2(hit.X - SweepBurnHalfWidth, hit.Y - SweepBurnHeight, SweepBurnHalfWidth * 2f, SweepBurnHeight)))
				tickTimer = BreathTickInterval;

			if (!await Wait(0f))
			{
				ok = false;
				break;
			}
			float step = SweepSpeed * dt;
			travelled += step;
			sincePatch += step;
			elapsed += dt;
		}
		Velocity = Vector2.Zero;
		StopBreath();
		_breath.Lifetime = BreathLifetime;
		return ok;
	}

	// Where a breath from `mouth` (Visual space) at `angleDegrees` meets the floor, if it does.
	private Vector2? BreathFloorHit(Vector2 mouth, float angleDegrees)
	{
		float dir = FacingRight ? 1f : -1f;
		Vector2 from = GlobalPosition + new Vector2(dir * mouth.X, mouth.Y);
		Vector2 along = new Vector2(dir, 0f).Rotated(dir * Mathf.DegToRad(angleDegrees));
		var query = PhysicsRayQueryParameters2D.Create(from, from + along * 900f, 1, new Godot.Collections.Array<Rid> { GetRid() });
		var hit = GetWorld2D().DirectSpaceState.IntersectRay(query);
		return hit.Count > 0 ? (Vector2)hit["position"] : null;
	}

	// Same burn tick as the breath cone, for an arbitrary world rect.
	private bool BurnPlayerInRect(Rect2 area)
	{
		var player = PlayerReads.Find(this);
		Stats stats = player?.GetNodeOrNull<Stats>("Stats");
		if (stats is null || stats.ExternalInvulnerable)
			return false;
		if (!area.Grow(BreathPlayerMargin).HasPoint(player.GlobalPosition))
			return false;
		stats.TakeDamage(BreathTickDamage, element: DamageElement.Fire, ignoreInvulnerability: true, armInvulnerability: false);
		player.ApplyBurn(BurnTintLinger);
		return true;
	}

	// Nothing solid just ahead in the air.
	private bool CanFly(float dir) => !TestMove(GlobalTransform, new Vector2(dir * (BodyHalfWidth + 20f), 0f));

	// targetX, pulled back short of any wall between here and there.
	private float ClampToOpenAir(Vector2 from, float targetX)
	{
		var query = PhysicsRayQueryParameters2D.Create(from, new Vector2(targetX, from.Y), 1, new Godot.Collections.Array<Rid> { GetRid() });
		var hit = GetWorld2D().DirectSpaceState.IntersectRay(query);
		if (hit.Count == 0)
			return targetX;
		float wallX = ((Vector2)hit["position"]).X;
		return wallX - Mathf.Sign(targetX - from.X) * (BodyHalfWidth + 40f);
	}

	// ── Wake & roar ─────────────────────────────────────────────────────────────────────────

	// Slow chest rise/fall while lying down, feet kept on the ground.
	private void UpdateSleepBreathing(float dt)
	{
		_sleepTime += dt;
		float rise = SleepBreathAmount * Mathf.Sin(_sleepTime * Mathf.Tau / SleepBreathPeriod);
		Sprite.Scale = new Vector2(_spriteBaseScale.X, _spriteBaseScale.Y * (1f + rise));
		// The sheet's feet sit 23px below the frame center; keep them planted while scaling.
		Sprite.Position = _spriteBasePosition - new Vector2(0f, SleepPoseLift + 23f * _spriteBaseScale.Y * rise);
	}

	private async Task WakeAndRoar()
	{
		_waking = true;
		SetBodyChaosActive(true);
		Sprite.Scale = _spriteBaseScale;
		Sprite.Position = _spriteBasePosition - new Vector2(0f, SleepPoseLift);
		var player = PlayerReads.Find(this);
		player?.HoldForCutscene(true);
		// Close-up centred on it, so the cinema bars don't cut it off.
		player?.FocusCameraDuringCutscene(GlobalPosition + IntroCameraOffset, 1f, IntroZoom);
		if (player is not null)
			FaceTowards(player.GlobalPosition.X);

		if (!await Wait(WakeDelay))
			return;
		// Gets up: the death animation played backwards.
		Sprite.Play("wake");
		while (Sprite.Animation == "wake" && Sprite.IsPlaying())
		{
			// The two lying frames sit lower than the standing ones.
			Sprite.Position = _spriteBasePosition - new Vector2(0f, Sprite.Frame <= 1 ? SleepPoseLift : 0f);
			if (!await Wait(0f))
				return;
		}

		Sprite.Position = _spriteBasePosition;
		if (!await Roar(player, held: true, RoarDuration))
			return;

		player?.HoldForCutscene(false);
		_asleep = false;
		_waking = false;
		AddToGroup("boss");
		DetectionRange = AwakeDetectionRange;
		_clawCooldownTimer = 0.5f;
		_fireCooldownTimer = 2f;
		_flightCooldownTimer = FlightFirstDelay;
		_rushCooldownTimer = RushFirstDelay;
		EndMove(0.3f);
	}

	// The roar: mouth wide open (the breath's open-mouth frame) held for `duration` — shake, dark
	// flash, ceiling dust, a gust of wind streaks and water spray blown at the player. `held`: the
	// player is in the wake-up cutscene (shoved via CutscenePush); in a fight they're shoved off it
	// with a plain horizontal push instead, if they're within RoarShoveRange.
	private async Task<bool> Roar(Metroidvania.Player.Player player, bool held, float duration)
	{
		if (Sprite is null)
			return false;
		Sprite.Play("fire_attack");
		while (Sprite.Animation == "fire_attack" && Sprite.IsPlaying() && Sprite.Frame < BreathFrame)
		{
			if (!await Wait(0f))
				return false;
		}
		Sprite.Pause();
		Vector2 mouth = GlobalPosition + new Vector2(FacingRight ? GroundMouth.X : -GroundMouth.X, GroundMouth.Y);
		if (RoarSound is not null)
		{
			var voice = new AudioStreamPlayer2D { Stream = RoarSound };
			AddChild(voice);
			voice.Finished += voice.QueueFree;
			voice.Play();
		}
		if (player is not null)
		{
			player.ShakeCamera(RoarShakeStrength, duration);
			float away = Mathf.Sign(player.GlobalPosition.X - GlobalPosition.X);
			if (held)
				player.CutscenePush(away * RoarPushSpeed);
			else if (Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X) <= RoarShoveRange)
				player.Shove(away * RoarPushSpeed, RoarShoveDuration);
		}
		RoarDarkenFlash();
		RoarDust();
		float dir = FacingRight ? 1f : -1f;
		GpuParticles2D spray = SpawnRoarSpray(dir);
		float roarElapsed = 0f;
		float streakTimer = 0f;
		while (roarElapsed < duration)
		{
			float dt = (float)GetPhysicsProcessDeltaTime();
			streakTimer -= dt;
			// The gust is strongest at the start and dies down.
			float strength = 1f - roarElapsed / duration;
			if (streakTimer <= 0f)
			{
				SpawnWindStreak(mouth, dir, strength, RoarWindReach, RoarStreakLength, RoarWindSpread);
				streakTimer = RoarStreakInterval / Mathf.Max(0.35f, strength);
			}
			if (!await Wait(0f))
			{
				spray?.QueueFree();
				return false;
			}
			roarElapsed += dt;
		}
		if (spray is not null)
			spray.Emitting = false;

		Sprite.Play("fire_attack");
		while (Sprite.Animation == "fire_attack" && Sprite.IsPlaying())
		{
			if (!await Wait(0f))
				return false;
		}
		return true;
	}

	// One gust streak: a thin, slightly bent line blown out of the mouth in a fan toward `dir`,
	// shooting forward and thinning out.
	private void SpawnWindStreak(Vector2 mouth, float dir, float strength, float reach, float streakLength, float spread) =>
		SpawnWindStreak(mouth, new Vector2(dir, 0f), strength, reach, streakLength, spread, RoarWindColor);

	// Same streak blown along any direction (e.g. down the dark beam), in `color`.
	private void SpawnWindStreak(Vector2 mouth, Vector2 baseDirection, float strength, float reach, float streakLength, float spread, Color color)
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		float facing = baseDirection.X >= 0f ? 1f : -1f;
		float angle = Mathf.DegToRad(_rng.RandfRange(-spread, spread * 0.6f));
		Vector2 forward = baseDirection.Normalized().Rotated(facing * angle);
		float length = _rng.RandfRange(streakLength * 0.5f, streakLength) * (0.6f + 0.4f * strength);
		float bend = _rng.RandfRange(-12f, 12f);
		var streak = new Line2D
		{
			Width = _rng.RandfRange(2.5f, 6f),
			DefaultColor = color with { A = color.A * (0.5f + 0.5f * strength) },
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			ZIndex = 5,
			BeginCapMode = Line2D.LineCapMode.Round,
			EndCapMode = Line2D.LineCapMode.Round,
			WidthCurve = new Curve(),
		};
		streak.WidthCurve.AddPoint(new Vector2(0f, 0.2f));
		streak.WidthCurve.AddPoint(new Vector2(0.5f, 1f));
		streak.WidthCurve.AddPoint(new Vector2(1f, 0.2f));
		Vector2 side = new Vector2(-forward.Y, forward.X);
		streak.AddPoint(Vector2.Zero);
		streak.AddPoint(forward * length * 0.5f + side * bend);
		streak.AddPoint(forward * length);
		scene.AddChild(streak);
		Vector2 start = mouth + forward * _rng.RandfRange(10f, 60f) + side * _rng.RandfRange(-25f, 25f);
		streak.GlobalPosition = start;
		float travel = reach * (0.5f + 0.5f * strength) * _rng.RandfRange(0.7f, 1f);
		const float life = 0.42f;
		Tween tween = streak.CreateTween();
		tween.TweenProperty(streak, "global_position", start + forward * travel, life)
			.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
		tween.Parallel().TweenProperty(streak, "modulate:a", 0f, life).From(1f).SetEase(Tween.EaseType.In);
		tween.TweenCallback(Callable.From(streak.QueueFree));
	}

	// Water and grit kicked up off the ground in front of it, blown away from the dragon.
	private GpuParticles2D SpawnRoarSpray(float dir)
	{
		if (GetTree().CurrentScene is not Node scene)
			return null;
		var ramp = new Gradient
		{
			Offsets = new[] { 0f, 0.15f, 1f },
			Colors = new[] { RoarSprayColor with { A = 0f }, RoarSprayColor, RoarSprayColor with { A = 0f } },
		};
		var process = new ParticleProcessMaterial
		{
			EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
			EmissionBoxExtents = new Vector3(140f, 4f, 1f),
			Direction = new Vector3(dir, -0.35f, 0f),
			Spread = 18f,
			InitialVelocityMin = 280f,
			InitialVelocityMax = 620f,
			Gravity = new Vector3(0f, 420f, 0f),
			DampingMin = 60f,
			DampingMax = 140f,
			ScaleMin = 0.08f,
			ScaleMax = 0.22f,
			ColorRamp = new GradientTexture1D { Gradient = ramp },
		};
		var spray = new GpuParticles2D
		{
			Amount = 200,
			Lifetime = 0.9f,
			ProcessMaterial = process,
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			ZIndex = 4,
		};
		scene.AddChild(spray);
		// Along the ground just in front of the head.
		spray.GlobalPosition = GlobalPosition + new Vector2(dir * (GroundMouth.X + 140f), BodyBottom - 4f);
		spray.Emitting = true;
		spray.Finished += spray.QueueFree;
		// Not one-shot: emits for the roar, then Emitting=false lets the last drops finish.
		var cleanup = GetTree().CreateTimer(RoarDuration + 2f);
		cleanup.Timeout += () => { if (IsInstanceValid(spray)) spray.QueueFree(); };
		return spray;
	}

	// Screen dips dark for a moment as the roar hits.
	private void RoarDarkenFlash()
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		var layer = new CanvasLayer { Layer = 50 };
		var shade = new ColorRect { Color = new Color(0f, 0f, 0f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
		shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		layer.AddChild(shade);
		scene.AddChild(layer);
		Tween tween = shade.CreateTween();
		tween.TweenProperty(shade, "color:a", RoarDarken, 0.12f);
		tween.TweenProperty(shade, "color:a", 0f, RoarDuration * 0.8f);
		tween.TweenCallback(Callable.From(layer.QueueFree));
	}

	// Dust shaken loose from the cave ceiling, across the top of the view.
	private void RoarDust()
	{
		if (GetTree().CurrentScene is not Node scene || GetViewport().GetCamera2D() is not Camera2D camera)
			return;
		Vector2 view = GetViewportRect().Size / camera.Zoom;
		Vector2 top = camera.GetScreenCenterPosition() - new Vector2(0f, view.Y * 0.5f);
		var ramp = new Gradient
		{
			Offsets = new[] { 0f, 0.1f, 0.8f, 1f },
			Colors = new[] { new Color(0.7f, 0.72f, 0.8f, 0f), new Color(0.7f, 0.72f, 0.8f, 0.7f), new Color(0.55f, 0.57f, 0.65f, 0.5f), new Color(0.55f, 0.57f, 0.65f, 0f) },
		};
		var process = new ParticleProcessMaterial
		{
			EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
			EmissionBoxExtents = new Vector3(view.X * 0.5f, 10f, 1f),
			Direction = new Vector3(0f, 1f, 0f),
			Spread = 12f,
			InitialVelocityMin = 20f,
			InitialVelocityMax = 80f,
			Gravity = new Vector3(0f, 260f, 0f),
			ScaleMin = 0.06f,
			ScaleMax = 0.16f,
			ColorRamp = new GradientTexture1D { Gradient = ramp },
		};
		var dust = new GpuParticles2D
		{
			Amount = RoarDustAmount,
			Lifetime = 2.2f,
			OneShot = true,
			Explosiveness = 0.5f,
			ProcessMaterial = process,
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			ZIndex = 4,
		};
		scene.AddChild(dust);
		dust.GlobalPosition = top;
		dust.Emitting = true;
		dust.Finished += dust.QueueFree;
	}

	// ── Helpers ─────────────────────────────────────────────────────────────────────────────

	private void BuildBreath()
	{
		var ramp = new Gradient
		{
			Offsets = new[] { 0f, 0.05f, 0.55f, 0.85f, 1f },
			Colors = new[]
			{
				BreathCoreColor with { A = 0.7f }, BreathFlameColor with { A = 0.85f }, BreathFlameColor with { A = 0.7f },
				BreathEmberColor with { A = 0.6f }, BreathEmberColor with { A = 0f },
			},
		};
		// Chaos, not a tidy cone (Midir's fire churns): turbulence, uneven speeds and sizes, blobs
		// that billow up as they travel, flames rising off the stream.
		var grow = new Curve { MaxValue = 2f };
		grow.AddPoint(new Vector2(0f, 0.25f));
		grow.AddPoint(new Vector2(0.35f, 0.8f));
		grow.AddPoint(new Vector2(1f, 1.3f));
		var process = new ParticleProcessMaterial
		{
			Direction = new Vector3(1f, 0f, 0f),
			Spread = 16f,
			InitialVelocityMin = BreathSpeed * 0.7f,
			InitialVelocityMax = BreathSpeed * 1.05f,
			Gravity = new Vector3(0f, -160f, 0f),
			DampingMin = 100f,
			DampingMax = 220f,
			AngleMin = 0f,
			AngleMax = 360f,
			AngularVelocityMin = -200f,
			AngularVelocityMax = 200f,
			ScaleMin = 0.22f,
			ScaleMax = 0.75f,
			ScaleCurve = new CurveTexture { Curve = grow },
			ColorRamp = new GradientTexture1D { Gradient = ramp },
			TurbulenceEnabled = true,
			TurbulenceNoiseStrength = BreathTurbulence,
			TurbulenceNoiseScale = 3.5f,
			TurbulenceNoiseSpeed = new Vector3(0.8f, 0.6f, 0f),
			TurbulenceNoiseSpeedRandom = 0.5f,
			TurbulenceInfluenceMin = 0.01f,
			TurbulenceInfluenceMax = 0.05f,
		};
		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		var dot = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres");
		_breath = new GpuParticles2D
		{
			Name = "FuegoAliento",
			Amount = BreathAmount,
			Lifetime = BreathLifetime,
			Randomness = 0.6f,
			Emitting = false,
			LocalCoords = false,
			ProcessMaterial = process,
			Texture = dot,
			Material = additive,
		};
		Visual.AddChild(_breath);

		// Embers spat out of the stream every which way, falling as they cool.
		var emberRamp = new Gradient
		{
			Offsets = new[] { 0f, 0.3f, 0.7f, 1f },
			Colors = new[] { new Color(1f, 0.95f, 0.6f, 1f), new Color(1f, 0.6f, 0.15f, 1f), BreathEmberColor, BreathEmberColor with { A = 0f } },
		};
		_embers = new GpuParticles2D
		{
			Name = "BrasasAliento",
			Amount = BreathEmberAmount,
			Lifetime = 1.1f,
			Randomness = 0.7f,
			Emitting = false,
			LocalCoords = false,
			Texture = dot,
			Material = additive,
			ProcessMaterial = new ParticleProcessMaterial
			{
				Direction = new Vector3(1f, -0.2f, 0f),
				Spread = 38f,
				InitialVelocityMin = BreathSpeed * 0.35f,
				InitialVelocityMax = BreathSpeed * 0.9f,
				Gravity = new Vector3(0f, 320f, 0f),
				DampingMin = 40f,
				DampingMax = 120f,
				ScaleMin = 0.04f,
				ScaleMax = 0.11f,
				ColorRamp = new GradientTexture1D { Gradient = emberRamp },
				TurbulenceEnabled = true,
				TurbulenceNoiseStrength = BreathTurbulence * 1.5f,
				TurbulenceNoiseScale = 2f,
				TurbulenceInfluenceMin = 0.15f,
				TurbulenceInfluenceMax = 0.45f,
			},
		};
		Visual.AddChild(_embers);

		_mouthGlow = new Sprite2D
		{
			Texture = dot,
			Material = additive,
			Modulate = BreathFlameColor with { A = 0f },
			Scale = Vector2.One * 0.6f,
		};
		Visual.AddChild(_mouthGlow);

		_breathLight = new PointLight2D
		{
			Texture = GD.Load<Texture2D>("res://resources/lighting/PointLightGradient.tres"),
			TextureScale = 2.2f,
			Color = BreathFlameColor,
			Energy = 0f,
		};
		Visual.AddChild(_breathLight);

		// Lights only the dragon (cull mask bit 2), not the backdrop around it.
		const int SheenMaskBit = 1 << 1;
		if (Sprite is not null)
			Sprite.LightMask |= SheenMaskBit;
		_sheenLight = new PointLight2D
		{
			Name = "BrilloCristal",
			Texture = GD.Load<Texture2D>("res://resources/lighting/PointLightGradient.tres"),
			TextureScale = SheenScale,
			Color = SheenColor,
			Energy = SheenEnergy,
			RangeItemCullMask = SheenMaskBit,
			Position = new Vector2(0f, -20f),
		};
		Visual.AddChild(_sheenLight);
	}

	// Gather (mouth glow builds while the animation winds up), then hold the open-mouth frame and
	// stream fire for BreathDuration, burning the player in the cone (`areaCenter`/`areaSize`, facing
	// right) in small ticks, then let the animation finish.
	private async Task<bool> Breathe(string animation, Vector2 mouth, float angleDegrees, Vector2 areaCenter, Vector2 areaSize,
		float duration, float gatherSpeedScale = 1f)
	{
		if (Sprite is null)
			return false;
		_breath.Position = mouth;
		_breath.RotationDegrees = angleDegrees;
		_mouthGlow.Position = mouth;
		_breathLight.Position = mouth + new Vector2(140f, 0f).Rotated(Mathf.DegToRad(angleDegrees));

		Sprite.SpeedScale = gatherSpeedScale;
		Sprite.Play(animation);
		float gatherTime = BreathFrame / Mathf.Max(1f, (float)Sprite.SpriteFrames.GetAnimationSpeed(animation) * gatherSpeedScale);
		_breathTween?.Kill();
		_breathTween = CreateTween();
		_breathTween.TweenProperty(_mouthGlow, "modulate:a", 0.9f, gatherTime);
		_breathTween.Parallel().TweenProperty(_breathLight, "energy", BreathLightEnergy * 0.4f, gatherTime);
		while (Sprite.Animation == animation && Sprite.IsPlaying() && Sprite.Frame < BreathFrame)
		{
			if (!await Wait(0f))
			{
				StopBreath();
				return false;
			}
		}

		Sprite.SpeedScale = 1f;
		Sprite.Pause();
		_breathTween?.Kill();
		_embers.Position = mouth;
		_breath.Emitting = true;
		_embers.Emitting = true;
		GpuParticles2D groundFire = SpawnGroundFire(mouth, angleDegrees);
		// Brief lead so the burning starts once the stream has actually reached out.
		bool ok = await Wait(0.08f);
		float elapsed = 0f;
		float tickTimer = 0f;
		while (ok && elapsed < duration)
		{
			float dt = (float)GetPhysicsProcessDeltaTime();
			// The head thrashes: the stream wobbles irregularly instead of holding a clean line.
			float wobble = BreathWobble * (Mathf.Sin(elapsed * 13f) * 0.6f + Mathf.Sin(elapsed * 7.3f + 1.7f) * 0.4f);
			_breath.RotationDegrees = angleDegrees + wobble;
			_embers.RotationDegrees = angleDegrees + wobble;
			// Erratic flicker, not a steady pulse.
			_breathLight.Energy = BreathLightEnergy * _rng.RandfRange(0.55f, 1.15f);
			tickTimer -= dt;
			if (tickTimer <= 0f && BurnPlayerIfInCone(areaCenter, areaSize))
				tickTimer = BreathTickInterval;
			ok = await Wait(0f);
			elapsed += dt;
		}
		StopBreath();
		if (groundFire is not null && IsInstanceValid(groundFire))
			LingerThenStop(groundFire, GroundFireLinger);
		if (!ok)
			return false;

		Sprite.Play(animation);
		while (Sprite.Animation == animation && Sprite.IsPlaying())
		{
			if (!await Wait(0f))
				return false;
		}
		return true;
	}

	// One burn tick on the player if they're inside the cone. Like GroundFire, ticks neither respect
	// nor arm the post-hit i-frames (each is small; standing in it is what hurts) — but a dash/roll
	// (ExternalInvulnerable) still passes through. True when a tick landed.
	private bool BurnPlayerIfInCone(Vector2 areaCenter, Vector2 areaSize)
	{
		var player = PlayerReads.Find(this);
		Stats stats = player?.GetNodeOrNull<Stats>("Stats");
		if (stats is null || stats.ExternalInvulnerable)
			return false;
		Vector2 center = GlobalPosition + new Vector2(FacingRight ? areaCenter.X : -areaCenter.X, areaCenter.Y);
		var cone = new Rect2(center - areaSize * 0.5f, areaSize).Grow(BreathPlayerMargin);
		if (!cone.HasPoint(player.GlobalPosition))
			return false;
		stats.TakeDamage(BreathTickDamage, element: DamageElement.Fire, ignoreInvulnerability: true, armInvulnerability: false);
		player.ApplyBurn(BurnTintLinger);
		return true;
	}

	// Where the stream meets the floor, flames keep licking up for a while after the breath (visual
	// only for now — whether it should burn is a design call).
	private GpuParticles2D SpawnGroundFire(Vector2 mouth, float angleDegrees)
	{
		if (GetTree().CurrentScene is not Node scene)
			return null;
		float dir = FacingRight ? 1f : -1f;
		Vector2 from = GlobalPosition + new Vector2(dir * mouth.X, mouth.Y);
		Vector2 along = new Vector2(dir, 0f).Rotated(dir * Mathf.DegToRad(angleDegrees));
		var query = PhysicsRayQueryParameters2D.Create(from, from + along * 900f, 1, new Godot.Collections.Array<Rid> { GetRid() });
		var hit = GetWorld2D().DirectSpaceState.IntersectRay(query);
		Vector2 at;
		if (hit.Count > 0)
		{
			at = (Vector2)hit["position"];
		}
		else
		{
			// Shallow angle (ground breath): no hit inside the reach — use the floor under the far end.
			Vector2 end = from + along * 450f;
			var down = PhysicsRayQueryParameters2D.Create(end, end + Vector2.Down * 600f, 1, new Godot.Collections.Array<Rid> { GetRid() });
			var floor = GetWorld2D().DirectSpaceState.IntersectRay(down);
			if (floor.Count == 0)
				return null;
			at = (Vector2)floor["position"];
		}
		return SpawnFlamesAt(at);
	}

	// A patch of flames licking up off the floor at `at` (world), with its own light. Emitting until
	// stopped (LingerThenStop).
	private GpuParticles2D SpawnFlamesAt(Vector2 at, float halfWidth = -1f)
	{
		if (GetTree().CurrentScene is not Node scene)
			return null;
		if (halfWidth < 0f)
			halfWidth = GroundFireHalfWidth;
		var ramp = new Gradient
		{
			Offsets = new[] { 0f, 0.2f, 0.6f, 1f },
			Colors = new[] { BreathCoreColor with { A = 0f }, BreathFlameColor, BreathEmberColor with { A = 0.6f }, BreathEmberColor with { A = 0f } },
		};
		var grow = new Curve { MaxValue = 2f };
		grow.AddPoint(new Vector2(0f, 0.4f));
		grow.AddPoint(new Vector2(1f, 1.4f));
		var flames = new GpuParticles2D
		{
			Amount = 80,
			Lifetime = 0.75f,
			Randomness = 0.6f,
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			ZIndex = 3,
			ProcessMaterial = new ParticleProcessMaterial
			{
				EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
				EmissionBoxExtents = new Vector3(halfWidth, 4f, 1f),
				Direction = new Vector3(0f, -1f, 0f),
				Spread = 25f,
				InitialVelocityMin = 40f,
				InitialVelocityMax = 160f,
				Gravity = new Vector3(0f, -90f, 0f),
				ScaleMin = 0.18f,
				ScaleMax = 0.55f,
				ScaleCurve = new CurveTexture { Curve = grow },
				ColorRamp = new GradientTexture1D { Gradient = ramp },
				TurbulenceEnabled = true,
				TurbulenceNoiseStrength = BreathTurbulence,
				TurbulenceNoiseScale = 2.5f,
				TurbulenceInfluenceMin = 0.1f,
				TurbulenceInfluenceMax = 0.35f,
			},
		};
		var glow = new PointLight2D
		{
			Texture = GD.Load<Texture2D>("res://resources/lighting/PointLightGradient.tres"),
			TextureScale = 1.6f,
			Color = BreathFlameColor,
			Energy = BreathLightEnergy * 0.45f,
			Position = new Vector2(0f, -30f),
		};
		flames.AddChild(glow);
		scene.AddChild(flames);
		flames.GlobalPosition = at;
		flames.Emitting = true;
		return flames;
	}

	private void LingerThenStop(GpuParticles2D flames, float linger)
	{
		var light = flames.GetChildOrNull<PointLight2D>(0);
		Tween tween = flames.CreateTween();
		tween.TweenInterval(linger);
		tween.TweenCallback(Callable.From(() => flames.Emitting = false));
		if (light is not null)
			tween.Parallel().TweenProperty(light, "energy", 0f, 0.6f).SetDelay(linger);
		tween.TweenInterval(flames.Lifetime + 0.2f);
		tween.TweenCallback(Callable.From(flames.QueueFree));
	}

	private void StopBreath()
	{
		if (_breath is null)
			return;
		_breath.Emitting = false;
		_embers.Emitting = false;
		_breathTween?.Kill();
		_breathTween = CreateTween();
		_breathTween.TweenProperty(_mouthGlow, "modulate:a", 0f, 0.2f);
		_breathTween.Parallel().TweenProperty(_breathLight, "energy", 0f, 0.3f);
	}

	// Moves at FlySpeed toward `target` (gravity is off while flying), facing the way it goes.
	private async Task<bool> FlyTo(Vector2 target, float timeout)
	{
		float elapsed = 0f;
		while (GlobalPosition.DistanceTo(target) > 12f && elapsed < timeout)
		{
			Vector2 toTarget = target - GlobalPosition;
			Velocity = toTarget.Normalized() * Mathf.Min(FlySpeed, toTarget.Length() / 0.1f);
			if (Mathf.Abs(toTarget.X) > 8f)
				FaceTowards(target.X);
			if (!await Wait((float)GetPhysicsProcessDeltaTime()))
				return false;
			elapsed += (float)GetPhysicsProcessDeltaTime();
		}
		Velocity = Vector2.Zero;
		return true;
	}

	private void PlaceInFront(Hitbox hitbox, Vector2 offset) =>
		hitbox.Position = new Vector2(FacingRight ? offset.X : -offset.X, offset.Y);

	private static float PlayerFeetOffset(Metroidvania.Player.Player player) =>
		player.GetNodeOrNull<CollisionShape2D>("CollisionShape2D")?.Shape is RectangleShape2D rect ? rect.Size.Y * 0.5f : 30f;

	private void FaceTowards(float x)
	{
		FacingRight = x >= GlobalPosition.X;
		Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);
	}

	private void EndMove(float recovery)
	{
		Gravity = _baseGravity;
		Sprite?.Play("idle");
		_recovering = true;
		_ = FinishRecovery(recovery, ++_moveSerial);
	}

	// `serial` guards against a move that cut this recovery short (the under-fire punish reaction):
	// only the latest recovery may hand control back.
	private async Task FinishRecovery(float recovery, int serial)
	{
		if (!await Wait(recovery) || serial != _moveSerial)
			return;
		_recovering = false;
		_busy = false;
	}

	// Waits at least one physics frame (so Wait(0) is "next frame").
	private async Task<bool> Wait(float seconds)
	{
		float elapsed = 0f;
		do
		{
			if (!IsInsideTree())
				return false;
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			if (!IsInstanceValid(this) || IsQueuedForRemoval || !IsInsideTree())
				return false;
			elapsed += (float)GetPhysicsProcessDeltaTime();
		} while (elapsed < seconds);
		return true;
	}

	private async Task<bool> WaitUntilLanded(float timeout)
	{
		float elapsed = 0f;
		while (!IsOnFloor() && elapsed < timeout)
		{
			if (!IsInsideTree())
				return false;
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			if (!IsInstanceValid(this) || IsQueuedForRemoval || !IsInsideTree())
				return false;
			elapsed += (float)GetPhysicsProcessDeltaTime();
		}
		return true;
	}
}
