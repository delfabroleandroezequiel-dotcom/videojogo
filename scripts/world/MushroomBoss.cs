using System.Threading.Tasks;
using Godot;
using Metroidvania.Player;
using Metroidvania.Save;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Mushroom boss — a fast, Malenia-inspired fight, being designed step by step with the user. So far:
//  * Lifesteal: every hit that connects heals it LifestealMultiplier × the hit's damage — also when
//    the player blocks it (the hitbox still connects), so dodging is the real defence.
//  * "Dance" (Waterfowl Dance): crouch tell with a green neon blink, leap, then DanceBursts lunges at
//    the player — each re-aimed, the spin_poison loop plus a BladeFlurry of neon cuts and a round
//    hitbox — and it lands vulnerable.
//  * "Scarlet flower" (Scarlet Aeonia): leaps very high, a big pink neon circle blooms around it at
//    the apex (the tell), it dives at where the player was and explodes on landing with a pink
//    shockwave that hits in an area.
//  * Heal punish (Malenia read): the player starts drinking a heal nearby → instant short leap that
//    lands on them and slams (green shockwave).
//  * Stone line: at range (or when the player heals out of leap range) stone stakes erupt one after
//    another toward the player, each with its own tell — jump or dash through them.
//  * Spin: player hugging it → short tell, one poison spin all around it.
//  * Sprouts: small spore mushrooms grow out of the ground around the player.
// Phase 1 (above PhaseTwoHealthRatio): spin, stone line, heal punish, sprouts. Phase 2: ONLY the dance
// and the scarlet flower, and it turns red (enraged tint + pulsing red aura, red shockwave on the switch).
// Its own class (boss-uniqueness rule); joins the "boss" group so the player's boss bar tracks it.
public partial class MushroomBoss : Enemy
{
	[ExportGroup("Lifesteal")]
	[Export] public float LifestealMultiplier = 1.5f;
	[Export] public Color HealTextColor = new(0.35f, 1f, 0.45f, 1f);

	[ExportGroup("Phases")]
	// Phase 1: spin, stone line, heal punish, sprouts. At or below this health ratio (phase 2) it switches
	// to only the dance and the scarlet flower.
	[Export] public float PhaseTwoHealthRatio = 0.5f;
	// Enraged look for phase 2: a red tint, a pulsing red aura + light, and a (harmless) red shockwave
	// when it switches.
	[Export] public Color RageTint = new(1.35f, 0.62f, 0.62f, 1f);
	[Export] public Color RageGlowColor = new(1f, 0.15f, 0.12f, 1f);
	[Export] public float RageTransitionPause = 0.7f;

	[ExportGroup("Phase 3 (Radahn)")]
	// Phase 3: "killing" it (health floor of 1 while this is on) makes it collapse and rise again,
	// healed, in a golden neon aura (Radahn reborn with Miquella's light). It opens with the Nuke.
	[Export] public bool PhaseThreeEnabled = true;
	// Testing: start the fight already in phase 3.
	[Export] public bool StartInPhaseThree;
	// Testing: use the phase 3 moves in a fixed order (0 slash, 1 dash, 2 meteor rain, 3 gravity
	// dive, 4 gravity meteor, 5 starcaller) instead of at random.
	[Export] public bool CyclePhaseThreeMoves;
	[Export] public float PhaseThreeHealthRatio = 1f;
	[Export] public Color GoldTint = new(1.3f, 1.15f, 0.72f, 1f);
	[Export] public Color GoldColor = new(1f, 0.78f, 0.22f, 1f);
	[Export] public float ReviveCollapseTime = 1.1f;
	[Export] public float ReviveRiseTime = 0.9f;
	[Export] public float GoldAuraScale = 1.12f;
	[Export] public float GoldAuraIntensity = 1.4f;

	[ExportGroup("Nuke")]
	[Export] public float NukeCooldown = 16f;
	[Export] public float NukeTellDuration = 0.35f;
	[Export] public float NukeRiseVelocity = -760f;
	// Hovering at the apex while it charges: the tell (screen darkens, light gathers).
	[Export] public float NukeChargeDuration = 1.8f;
	[Export] public float NukeDarken = 0.55f;
	// The blast: a golden ring expanding from it — dash through its edge as it passes you.
	[Export] public float NukeRadius = 560f;
	[Export] public float NukeExpandTime = 1.15f;
	[Export] public float NukeRingThickness = 46f;
	[Export] public float NukeDamageMultiplier = 2.2f;
	[Export] public float NukeKnockback = 380f;
	// Then light beams rain down one after another, each marked on the ground first; the first on the
	// player, the rest scattered around them.
	[Export] public int NukeBeamCount = 6;
	[Export] public float NukeBeamInterval = 0.22f;
	[Export] public float NukeBeamTell = 0.6f;
	[Export] public float NukeBeamActive = 0.2f;
	[Export] public float NukeBeamHalfWidth = 22f;
	[Export] public float NukeBeamSpread = 260f;
	[Export] public float NukeBeamDamageMultiplier = 1.3f;
	[Export] public float NukeRecovery = 1.4f;

	[ExportGroup("Spear rain")]
	// Floats up and rains golden light spears over the area around the player in waves, each spear's
	// landing spot marked on the ground first. Spears in a wave are spaced so there's always a gap,
	// and each wave is shifted, so the safe spots move — walk between waves.
	// Off: not one of Radahn's moves (base-game arrow rain mix-up) — kept for another boss.
	[Export] public bool SpearRainEnabled;
	[Export] public float SpearRainCooldown = 11f;
	[Export] public float SpearRainFirstDelay = 10f;
	[Export] public float SpearRainRiseVelocity = -600f;
	[Export] public int SpearWaves = 3;
	[Export] public float SpearWaveInterval = 1f;
	[Export] public float SpearSpacing = 95f;
	[Export] public float SpearAreaHalfWidth = 480f;
	[Export] public float SpearTell = 0.7f;
	[Export] public float SpearFallTime = 0.22f;
	[Export] public float SpearFallHeight = 520f;
	[Export] public float SpearMaxTiltDegrees = 12f;
	[Export] public float SpearHalfWidth = 16f;
	[Export] public float SpearLength = 70f;
	[Export] public float SpearDamageMultiplier = 1.1f;
	[Export] public float SpearRecovery = 1f;

	[ExportGroup("Lightspeed Slash")]
	// Leaps up; 3 golden light clones appear over the player one after another, each marking the
	// ground, and slam down; then Radahn himself crashes onto the player.
	[Export] public float LightSlashCooldown = 9f;
	[Export] public int LightSlashClones = 3;
	[Export] public float LightSlashCloneHeight = 280f;
	[Export] public float LightSlashCloneTell = 0.45f;
	[Export] public float LightSlashCloneInterval = 0.45f;
	[Export] public float LightSlashCloneFall = 0.12f;
	[Export] public float LightSlashCloneRadius = 55f;
	[Export] public float LightSlashCloneDamageMultiplier = 1.1f;
	[Export] public float LightSlashDiveSpeed = 950f;
	[Export] public float LightSlashCrashRadius = 90f;
	[Export] public float LightSlashCrashDamageMultiplier = 1.6f;
	[Export] public float LightSlashRecovery = 1.2f;

	[ExportGroup("Lightspeed Dash")]
	// Two light clones then Radahn himself cross through the player at light speed along the ground,
	// each path marked first. Radahn is invulnerable and passes through during his own dash.
	[Export] public float LightDashCooldown = 7f;
	[Export] public float LightDashTell = 0.5f;
	[Export] public int LightDashClones = 2;
	[Export] public float LightDashPathTell = 0.35f;
	[Export] public float LightDashInterval = 0.55f;
	[Export] public float LightDashSpeed = 1500f;
	[Export] public float LightDashOvershoot = 260f;
	[Export] public float LightDashHitHeight = 45f;
	[Export] public float LightDashDamageMultiplier = 1.2f;
	[Export] public float LightDashRecovery = 0.9f;

	[ExportGroup("Consort's Meteor Rain")]
	// Ground slam, rises out of sight, a golden target mark follows the player then locks, and he
	// crashes down on it as a light meteor.
	[Export] public float MeteorCooldown = 12f;
	[Export] public float MeteorSlamRadius = 80f;
	[Export] public float MeteorSlamDamageMultiplier = 1.2f;
	[Export] public float MeteorRiseTime = 0.6f;
	[Export] public float MeteorTrackTime = 1.2f;
	[Export] public float MeteorLockTime = 0.4f;
	[Export] public float MeteorDropHeight = 700f;
	[Export] public float MeteorFallSpeed = 1800f;
	[Export] public float MeteorBlastRadius = 150f;
	[Export] public float MeteorDamageMultiplier = 2f;
	[Export] public float MeteorRecovery = 1.5f;

	[ExportGroup("Gravity (Radahn)")]
	[Export] public Color GravityColor = new(0.62f, 0.35f, 1f, 1f);
	// Gravity Dive: leaps at the player and crashes down; a gravity wave rolls outward along the ground
	// both ways, flinging rocks — jump over the wave.
	[Export] public float GravityDiveCooldown = 8f;
	[Export] public float GravityDiveLeapHeight = -560f;
	[Export] public float GravityDiveImpactRadius = 70f;
	[Export] public float GravityWaveSpeed = 420f;
	[Export] public float GravityWaveDistance = 520f;
	[Export] public float GravityWaveHeight = 34f;
	[Export] public float GravityDiveDamageMultiplier = 1.4f;
	[Export] public float GravityDiveRecovery = 1f;
	// Gravity Meteor: slashes the ground, rocks rise and hang wrapped in gravity, then get flung at the
	// player one by one; each impact raises a light pillar (phase 3 empowerment).
	[Export] public float GravityMeteorCooldown = 10f;
	[Export] public int GravityMeteorRocks = 4;
	[Export] public float GravityMeteorHoverTime = 0.9f;
	[Export] public float GravityMeteorRockSpeed = 520f;
	[Export] public float GravityMeteorRockInterval = 0.3f;
	[Export] public float GravityMeteorRockRadius = 14f;
	[Export] public float GravityMeteorDamageMultiplier = 1f;
	[Export] public float GravityMeteorRecovery = 1.3f;
	[Export] public float GravityMeteorSkyVelocity = -760f;
	// After the meteors: rounds of diving — 2 light clones then Radahn himself, each round.
	[Export] public int GravityMeteorDiveRounds = 3;
	[Export] public int GravityMeteorDiveClones = 2;
	[Export] public float GravityMeteorCloneStagger = 0.2f;
	[Export] public float GravityMeteorCloneTell = 0.35f;
	[Export] public float GravityMeteorCloneDiveTime = 0.18f;
	[Export] public float GravityMeteorDiveSpeed = 1000f;
	[Export] public float GravityMeteorReleapVelocity = -620f;
	// Starcaller Cry: rises a little and opens a huge gravity field pulling the player in, then slams
	// with gravity spikes around it and a ring of light pillars (phase 3 empowerment).
	[Export] public float StarcallerCooldown = 14f;
	[Export] public float StarcallerPullDuration = 1.7f;
	[Export] public float StarcallerPullRadius = 420f;
	[Export] public float StarcallerPullSpeed = 150f;
	[Export] public float StarcallerSlamRadius = 110f;
	[Export] public float StarcallerDamageMultiplier = 1.8f;
	[Export] public int StarcallerPillars = 6;
	[Export] public float StarcallerPillarSpacing = 85f;
	[Export] public float StarcallerRecovery = 1.3f;

	[ExportGroup("Movement")]
	// Phase 2 keeps the player in the dance/flower band; phase 1 walks right up for the spin.
	[Export] public float EngageDistance = 170f;
	[Export] public float BackOffDistance = 130f;
	[Export] public float PhaseOneEngageDistance = 70f;

	[ExportGroup("Dance")]
	[Export] public float DanceCooldown = 5f;
	[Export] public float DanceMinRange = 50f;
	[Export] public float DanceMaxRange = 260f;
	[Export] public float DanceTellDuration = 0.5f;
	[Export] public Vector2 DanceLeap = new(220f, -520f);
	[Export] public int DanceBursts = 3;
	[Export] public float DanceBurstSpeed = 600f;
	[Export] public float DanceBurstDuration = 0.28f;
	// Burst period (duration + gap) must stay >= the player's dash cycle (DashDuration + i-frame tail
	// + DashCooldown = 0.78s) plus a reaction margin, or the dance can't be dodged: the player dashes mid-burst,
	// so the next one must arrive after the dash is back. 0.28 + 0.72 = 1.0s → one
	// precise dash per burst. The gap is the mushroom hanging in the air re-aiming — the read.
	[Export] public float DanceBurstGap = 0.72f;
	// Drift toward the player while hanging between bursts (≈ player run speed).
	[Export] public float DanceGapDriftSpeed = 210f;
	// ...but only down to this distance: each burst then starts from about the same range, so the
	// time to react (and to have the dash back) is consistent.
	[Export] public float DanceGapHoldDistance = 170f;
	// How fast (rad/s) each burst steers toward the player while it's flying — high enough that
	// running away doesn't escape it, finite so a well-timed dash through it makes it overshoot.
	[Export] public float DanceHomingTurnRate = 9f;
	// Damage radius of a burst — a bit inside the blades' visual reach (~85-100px), so a dash that
	// clearly goes through reads as a dodge.
	[Export] public float DanceHitRadius = 65f;
	// Final burst: reaches you wherever you are.
	[Export] public float DanceFinalBurstSpeed = 750f;
	[Export] public float DanceFinalBurstMaxTime = 2.5f;
	// A player dashing within this distance of a burst makes it stop homing and overshoot.
	[Export] public float DanceDodgeCommitDistance = 130f;
	[Export] public float DanceFirstHover = 0.15f;
	[Export] public float DanceRecovery = 0.9f;
	[Export] public Color DanceTint = new(0.55f, 3.2f, 0.75f, 1f);

	[ExportGroup("Scarlet flower")]
	[Export] public float FlowerCooldown = 8f;
	[Export] public float FlowerMinRange = 120f;
	[Export] public float FlowerTellDuration = 0.35f;
	[Export] public float FlowerLeapVelocity = -820f;
	[Export] public float FlowerBloomDuration = 0.7f;
	[Export] public float FlowerCircleRadius = 70f;
	[Export] public float FlowerDiveSpeed = 750f;
	[Export] public float FlowerExplosionRadius = 130f;
	[Export] public float FlowerExplosionActive = 0.15f;
	[Export] public float FlowerRecovery = 1.1f;
	[Export] public Color FlowerColor = new(1f, 0.3f, 0.75f, 1f);
	[Export] public float HurtDuration = 0.3f;

	[ExportGroup("Stone line")]
	// Ranged punish: from StoneMinRange out (or when the player heals beyond the heal-punish leap),
	// it casts and stone stakes erupt one after another from in front of it toward where the player
	// was — each with its own tell on the spot. Jump or dash through them.
	[Export] public float StoneCooldown = 6f;
	[Export] public float StoneMinRange = 230f;
	[Export] public float StoneMaxRange = 900f;
	[Export] public float StoneCastDelay = 0.45f;
	[Export] public int StoneCount = 7;
	[Export] public float StoneFirstOffset = 70f;
	[Export] public float StoneSpacing = 85f;
	[Export] public float StoneInterval = 0.12f;
	[Export] public float StoneTell = 0.3f;
	[Export] public float StoneActive = 0.3f;
	[Export] public float StoneLinger = 0.35f;
	[Export] public float StoneScale = 1.6f;
	[Export] public Vector2 StoneHitboxSize = new(70f, 95f);
	[Export] public float StoneRecovery = 0.5f;

	[ExportGroup("Spin")]
	// Close-range answer: player hugging it → short tell, one poison spin hitting all around it.
	[Export] public float SpinRange = 95f;
	[Export] public float SpinCooldown = 1.2f;
	[Export] public float SpinTell = 0.2f;
	[Export] public float SpinRadius = 75f;
	[Export] public float SpinActive = 0.33f;
	[Export] public float SpinRecovery = 0.35f;

	[ExportGroup("Sprouts")]
	// Summons small spore mushrooms (SproutScene, e.g. MushroomEnemy) out of the ground around the
	// player — a green neon mark on each spot first, then they grow in. Only once its health is at or
	// below SproutHealthThreshold (1 = from the start, 0.5 = only in the second half). They wither
	// when the boss dies.
	[Export] public PackedScene SproutScene;
	[Export] public float SproutHealthThreshold = 1f;
	[Export] public float SproutCooldown = 9f;
	[Export] public int SproutsPerCast = 2;
	[Export] public int MaxSproutsAlive = 3;
	[Export] public float SproutMinOffset = 70f;
	[Export] public float SproutMaxOffset = 140f;
	[Export] public float SproutCastDelay = 0.45f;
	[Export] public float SproutMarkDuration = 0.5f;
	[Export] public float SproutFeetOffset = 15f;
	[Export] public float SproutRecovery = 0.4f;

	[ExportGroup("Heal punish")]
	// Malenia-style read: the player starts drinking a heal within this range → it instantly
	// leaps onto them (short arc computed to land where they stand) and slams.
	[Export] public float HealPunishRange = 350f;
	[Export] public float HealPunishTell = 0.15f;
	[Export] public float HealPunishAirTime = 0.5f;
	[Export] public float HealPunishSlamRadius = 75f;
	[Export] public float HealPunishSlamActive = 0.12f;
	[Export] public float HealPunishRecovery = 0.6f;
	[Export] public Color HealPunishColor = new(0.3f, 1f, 0.45f, 1f);
	[Export] public float NeonLightScale = 1.8f;
	[Export] public float NeonLightEnergy = 1.6f;

	private Hitbox _danceHitbox;
	private Hitbox _flowerHitbox;
	private BladeFlurry _flurry;
	private Sprite2D _tellGlow;
	// Real light for the neon effects: additive lines alone get crushed by a dark map's CanvasModulate.
	private PointLight2D _neonLight;
	private Tween _tellTween;
	private readonly RandomNumberGenerator _rng = new();

	private bool _busy;
	private bool _airControl;
	private float _baseGravity;
	private float _danceCooldownTimer;
	private float _flowerCooldownTimer;
	private float _hurtTimer;
	private bool _nextIsFlower;
	private bool _playerWasHealing;
	private float _stoneCooldownTimer;
	private SpriteFrames _stakeFrames;
	private float _sproutCooldownTimer;
	private float _spinCooldownTimer;
	private Hitbox _spinHitbox;
	private bool _phaseTwoStarted;
	private bool _phaseThreeStarted;
	private float _nukeCooldownTimer;
	private float _spearRainCooldownTimer;
	private float _lightSlashCooldownTimer;
	private float _lightDashCooldownTimer;
	private float _meteorCooldownTimer;
	private float _gravityDiveCooldownTimer;
	private float _gravityMeteorCooldownTimer;
	private float _starcallerCooldownTimer;
	private int _lastPhaseThreeMove = -1;
	private Sprite2D _rageAura;
	private PointLight2D _rageLight;
	private Tween _ragePulse;
	private Sprite2D _goldAura;
	private readonly System.Collections.Generic.List<Enemy> _sprouts = new();
	private Hitbox _slamHitbox;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;

		AddToGroup("boss");
		_rng.Randomize();
		_baseGravity = Gravity;

		_danceHitbox = BuildHitbox("DanceHitbox", new CircleShape2D { Radius = DanceHitRadius });
		_flowerHitbox = BuildHitbox("FlowerHitbox", new CircleShape2D { Radius = FlowerExplosionRadius });
		_slamHitbox = BuildHitbox("SlamHitbox", new CircleShape2D { Radius = HealPunishSlamRadius });
		_spinHitbox = BuildHitbox("SpinHitbox", new CircleShape2D { Radius = SpinRadius });

		_flurry = new BladeFlurry();
		Visual.AddChild(_flurry);

		_tellGlow = new Sprite2D
		{
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			Modulate = new Color(0.3f, 1f, 0.4f, 0f),
			Scale = Vector2.One * 2.2f,
			ShowBehindParent = true,
		};
		Visual.AddChild(_tellGlow);

		_neonLight = new PointLight2D
		{
			Texture = GD.Load<Texture2D>("res://resources/lighting/PointLightGradient.tres"),
			TextureScale = NeonLightScale,
			Energy = 0f,
		};
		AddChild(_neonLight);

		Stats.HitTaken += _ => _hurtTimer = HurtDuration;
		_danceCooldownTimer = 1.5f;
		_stoneCooldownTimer = 2f;
		_sproutCooldownTimer = 1f;
		_stakeFrames = BuildStakeFrames();
		_flowerCooldownTimer = 3f;

		if (PhaseThreeEnabled)
			Stats.MinHealth = 1;
		if (PhaseThreeEnabled && StartInPhaseThree)
		{
			_phaseTwoStarted = true;
			_phaseThreeStarted = true;
			Stats.MinHealth = 0;
			ApplyGoldLook();
			_nukeCooldownTimer = 1.5f;
			_spearRainCooldownTimer = SpearRainFirstDelay;
		}
	}

	private Hitbox BuildHitbox(string name, Shape2D shape)
	{
		var hitbox = new Hitbox { Name = name, CollisionLayer = 0, CollisionMask = 2 };
		hitbox.AddChild(new CollisionShape2D { Name = "CollisionShape2D", Shape = shape, Disabled = true });
		AddChild(hitbox);
		hitbox.HitDealt += OnHitDealt;
		return hitbox;
	}

	// ── Boss plumbing ───────────────────────────────────────────────────────────────────────

	protected override bool IsDefeated() => SaveManager.Instance.IsBossDefeated(PersistenceId);

	protected override void OnDefeated()
	{
		SaveManager.Instance.MarkBossDefeated(PersistenceId);
		WitherSprouts();
		CallDeferred(MethodName.SpawnLoot);
		IsQueuedForRemoval = true;
		RemoveFromGroup("boss");
		Gravity = _baseGravity;
		SetDeferred(CollisionObject2D.PropertyName.CollisionLayer, 0);
		_danceHitbox.CallDeferred(Hitbox.MethodName.Deactivate);
		_flowerHitbox.CallDeferred(Hitbox.MethodName.Deactivate);
		_slamHitbox.CallDeferred(Hitbox.MethodName.Deactivate);
		_spinHitbox.CallDeferred(Hitbox.MethodName.Deactivate);
		Sprite?.Play("death_poison");
		if (Sprite is not null)
			Sprite.AnimationFinished += QueueFree;
		else
			QueueFree();
	}

	protected override bool ContactDamageEnabled => false;

	// ── Lifesteal ───────────────────────────────────────────────────────────────────────────

	// HitDealt fires whenever the hitbox connects — including a hit the player blocked — so it heals
	// the same either way (design decision: blocking doesn't stop the drain).
	private void OnHitDealt()
	{
		int heal = Mathf.Max(1, Mathf.RoundToInt(Stats.AttackPower * LifestealMultiplier));
		int before = Stats.CurrentHealth;
		Stats.Heal(heal);
		int healed = Stats.CurrentHealth - before;
		if (healed > 0)
			ShowHealText(healed);
	}

	private void ShowHealText(int amount)
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		var label = new Label
		{
			Text = $"+{amount}",
			Modulate = HealTextColor,
			Scale = Vector2.One * 1.4f,
		};
		scene.AddChild(label);
		label.GlobalPosition = GlobalPosition + new Vector2(-12f, -BodyBottom - 30f);
		Tween rise = label.CreateTween();
		rise.TweenProperty(label, "global_position:y", label.GlobalPosition.Y - 30f, 0.8f);
		rise.Parallel().TweenProperty(label, "modulate:a", 0f, 0.8f).SetEase(Tween.EaseType.In);
		rise.TweenCallback(Callable.From(label.QueueFree));

		Tween flash = CreateTween();
		Visual.Modulate = new Color(0.7f, 1.6f, 0.7f);
		flash.TweenProperty(Visual, "modulate", Colors.White, 0.3f);
	}

	// ── Test AI ─────────────────────────────────────────────────────────────────────────────

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;

		float dt = (float)delta;
		_danceCooldownTimer -= dt;
		_flowerCooldownTimer -= dt;
		_stoneCooldownTimer -= dt;
		_sproutCooldownTimer -= dt;
		_spinCooldownTimer -= dt;
		_nukeCooldownTimer -= dt;
		_spearRainCooldownTimer -= dt;
		_lightSlashCooldownTimer -= dt;
		_lightDashCooldownTimer -= dt;
		_meteorCooldownTimer -= dt;
		_gravityDiveCooldownTimer -= dt;
		_gravityMeteorCooldownTimer -= dt;
		_starcallerCooldownTimer -= dt;
		_hurtTimer -= dt;

		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval || _busy || !IsOnFloor())
			return;

		if (!_phaseTwoStarted && Stats.CurrentHealth <= Stats.MaxHealth * PhaseTwoHealthRatio)
		{
			_ = EnterPhaseTwo();
			return;
		}

		// "Killed" (held at 1 by the health floor) → reborn in gold.
		if (PhaseThreeEnabled && !_phaseThreeStarted && Stats.CurrentHealth <= 1)
		{
			_ = EnterPhaseThree();
			return;
		}

		if (!PlayerDetected)
			return;

		var player = PlayerReads.Find(this);
		if (player is null)
			return;

		float distance = Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X);

		if (_phaseThreeStarted && _nukeCooldownTimer <= 0f)
		{
			_ = Nuke(player);
			return;
		}
		if (_phaseThreeStarted && SpearRainEnabled && _spearRainCooldownTimer <= 0f)
		{
			_ = SpearRain(player);
			return;
		}
		if (_phaseThreeStarted)
		{
			// Whichever of the consort's moves are off cooldown, picked at random.
			var ready = new System.Collections.Generic.List<int>();
			if (_lightSlashCooldownTimer <= 0f)
				ready.Add(0);
			if (_lightDashCooldownTimer <= 0f)
				ready.Add(1);
			if (_meteorCooldownTimer <= 0f)
				ready.Add(2);
			if (_gravityDiveCooldownTimer <= 0f && distance <= 360f)
				ready.Add(3);
			if (_gravityMeteorCooldownTimer <= 0f)
				ready.Add(4);
			if (_starcallerCooldownTimer <= 0f && distance <= StarcallerPullRadius)
				ready.Add(5);
			if (ready.Count > 0)
			{
				int pick = ready[_rng.RandiRange(0, ready.Count - 1)];
				if (CyclePhaseThreeMoves)
				{
					// Testing: next move in order after the last one used (among the ready ones).
					pick = ready.Find(m => m > _lastPhaseThreeMove);
					if (!ready.Contains(pick) || pick <= _lastPhaseThreeMove)
						pick = ready[0];
				}
				_lastPhaseThreeMove = pick;
				if (CyclePhaseThreeMoves)
					GD.Print($"[MushroomBoss] phase 3 move {pick}");
				switch (pick)
				{
					case 0: _ = LightspeedSlash(player); break;
					case 1: _ = LightspeedDash(player); break;
					case 2: _ = ConsortMeteor(player); break;
					case 3: _ = GravityDive(player); break;
					case 4: _ = GravityMeteor(player); break;
					default: _ = StarcallerCry(player); break;
				}
				return;
			}
		}

		// Heal punish takes priority over everything else.
		// Phase 1 only: heal punish, spin, stone line, sprouts. Phase 2 only: dance, scarlet flower.
		bool phaseOne = !IsPhaseTwo;
		bool startedHealing = player.IsHealing && !_playerWasHealing;
		_playerWasHealing = player.IsHealing;
		if (phaseOne && startedHealing && distance <= HealPunishRange)
		{
			_ = HealPunish(player);
			return;
		}

		// Hugged → spin.
		if (phaseOne && distance <= SpinRange && _spinCooldownTimer <= 0f)
		{
			_ = Spin(player);
			return;
		}

		bool stoneReady = phaseOne && _stoneCooldownTimer <= 0f && distance >= StoneMinRange && distance <= StoneMaxRange;
		// Healing out of leap range doesn't get a pass either.
		if (stoneReady && (startedHealing || player.IsHealing))
		{
			_ = StoneLine(player);
			return;
		}
		// Phase 2 always opens with the flower (at any range); the dance only comes after it.
		// Phase 3 drops Malenia's moves entirely: Radahn's kit only.
		bool danceReady = IsPhaseTwo && !_phaseThreeStarted && !_openWithFlower && _danceCooldownTimer <= 0f && distance >= DanceMinRange && distance <= DanceMaxRange;
		bool flowerReady = IsPhaseTwo && !_phaseThreeStarted && _flowerCooldownTimer <= 0f && (_openWithFlower || distance >= FlowerMinRange);

		_sprouts.RemoveAll(s => !IsInstanceValid(s) || s.IsQueuedForDeletion());
		bool sproutReady = phaseOne && SproutScene is not null && _sproutCooldownTimer <= 0f && _sprouts.Count < MaxSproutsAlive
			&& Stats.CurrentHealth <= Stats.MaxHealth * SproutHealthThreshold;
		if (sproutReady && _rng.Randf() < 0.6f)
		{
			_ = Sprouts(player);
			return;
		}

		if (flowerReady && (_nextIsFlower || !danceReady))
			_ = ScarletFlower(player);
		else if (stoneReady && (!danceReady || _rng.Randf() < 0.5f))
			_ = StoneLine(player);
		else if (danceReady)
			_ = Dance();
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_busy || _airControl)
			return currentVelocityX;
		if (_hurtTimer > 0f)
			return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);

		// Keep the player inside its move ranges — walk in when far, back off when hugged (the spin
		// covers point-blank while it's off cooldown).
		float toward = Mathf.Sign(distanceX);
		float absDistance = Mathf.Abs(distanceX);
		float engage = IsPhaseTwo ? EngageDistance : PhaseOneEngageDistance;
		float backOff = IsPhaseTwo ? BackOffDistance : 0f;
		if (absDistance > engage && CanStepTo(toward))
			return toward * MoveSpeed;
		if (absDistance < backOff && CanStepTo(-toward))
			return -toward * MoveSpeed;
		return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
	}

	// Latched once the transition starts: lifesteal can heal it back above the threshold, but it stays enraged.
	private bool IsPhaseTwo => _phaseTwoStarted;

	protected override bool CanTurnToFacePlayer => !_busy;

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null || _busy)
			return;
		string anim = _hurtTimer > 0f ? "hurt" : Mathf.Abs(velocity.X) > 5f ? "walk" : "idle";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	private void FaceTowards(float x)
	{
		FacingRight = x >= GlobalPosition.X;
		Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);
	}

	private void Tell(Color tint, float duration)
	{
		_tellTween?.Kill();
		_tellTween = CreateTween();
		const int blinks = 3;
		float half = duration / blinks * 0.5f;
		Color glow = new(Mathf.Min(1f, tint.R), Mathf.Min(1f, tint.G), Mathf.Min(1f, tint.B), 0.9f);
		_tellGlow.Modulate = glow with { A = 0f };
		_neonLight.Color = glow;
		for (int i = 0; i < blinks; i++)
		{
			_tellTween.TweenProperty(Sprite, "self_modulate", tint, half);
			_tellTween.Parallel().TweenProperty(_tellGlow, "modulate:a", 0.9f, half);
			_tellTween.Parallel().TweenProperty(_neonLight, "energy", NeonLightEnergy, half);
			_tellTween.TweenProperty(Sprite, "self_modulate", Colors.White, half);
			_tellTween.Parallel().TweenProperty(_tellGlow, "modulate:a", 0f, half);
			_tellTween.Parallel().TweenProperty(_neonLight, "energy", 0f, half);
		}
	}

	private async Task<bool> Wait(float seconds)
	{
		float elapsed = 0f;
		do
		{
			// Out of the tree (scene change / respawn mid-move) → GetTree() is null; abort the move.
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
			// Out of the tree (scene change / respawn mid-move) → GetTree() is null; abort the move.
			if (!IsInsideTree())
				return false;
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			if (!IsInstanceValid(this) || IsQueuedForRemoval || !IsInsideTree())
				return false;
			elapsed += (float)GetPhysicsProcessDeltaTime();
		}
		return true;
	}

	private void SetNeonLight(Color color, float energy)
	{
		_tellTween?.Kill();
		_neonLight.Color = color;
		Tween tween = CreateTween();
		tween.TweenProperty(_neonLight, "energy", energy, 0.08f);
	}

	private void EndMove(float recovery)
	{
		Gravity = _baseGravity;
		_airControl = false;
		Sprite?.Play("idle");
		_ = FinishRecovery(recovery);
	}

	private async Task FinishRecovery(float recovery)
	{
		if (!await Wait(recovery))
			return;
		_busy = false;
	}

	// ── Dance ───────────────────────────────────────────────────────────────────────────────

	private async Task Dance()
	{
		_busy = true;
		_nextIsFlower = true;
		_danceCooldownTimer = DanceCooldown;
		var player = PlayerReads.Find(this);
		if (player is null) { _busy = false; return; }
		Velocity = Vector2.Zero;
		FaceTowards(player.GlobalPosition.X);

		// Tell: crouch pose (jump frame 2) + green blink.
		Sprite.Play("jump");
		Sprite.Pause();
		Sprite.Frame = 2;
		Tell(DanceTint, DanceTellDuration);
		if (!await Wait(DanceTellDuration))
			return;

		// Leap up and slightly toward the player.
		Sprite.Play("jump");
		Sprite.Frame = 3;
		_airControl = true;
		Velocity = new Vector2((FacingRight ? 1f : -1f) * DanceLeap.X, DanceLeap.Y);
		while (Velocity.Y < 0f)
		{
			if (!await Wait(0f))
				return;
		}

		Gravity = 0f;
		Velocity = Vector2.Zero;
		if (!await Wait(DanceFirstHover))
			return;

		for (int burst = 0; burst < DanceBursts; burst++)
		{
			player = PlayerReads.Find(this);
			Vector2 target = player is null ? GlobalPosition : player.GlobalPosition + new Vector2(0f, -6f);
			Vector2 direction = (target - GlobalPosition).Normalized();
			direction.Y = Mathf.Max(direction.Y, -0.3f);
			FaceTowards(target.X);

			// The last burst is Malenia's reach-anywhere one: faster, and it keeps going until it gets
			// to the player (capped by DanceFinalBurstMaxTime) instead of a fixed short lunge.
			bool finalBurst = burst == DanceBursts - 1;
			float speed = finalBurst ? DanceFinalBurstSpeed : DanceBurstSpeed;
			float maxDuration = finalBurst ? DanceFinalBurstMaxTime : DanceBurstDuration;

			Sprite.Play("spin_poison");
			Sprite.Frame = 3;
			Velocity = direction.Normalized() * speed;
			_flurry.Play();
			SetNeonLight(new Color(0.3f, 1f, 0.45f), NeonLightEnergy);
			// Pierces the player's post-hit i-frames (a caught player eats the whole dance) but not dash
			// i-frames — each burst has to be dashed on its own.
			_danceHitbox.Activate(Stats, ignorePostHitInvulnerability: true);

			// Homes in on the player for the whole burst (fast but finite turn rate): moving away
			// doesn't escape it — only a dash timed right as it reaches you (i-frames at the moment
			// the hitbox touches) makes it whiff and overshoot.
			float burstElapsed = 0f;
			bool dodged = false;
			bool reached = false;
			while (burstElapsed < maxDuration && !(finalBurst && reached && burstElapsed >= DanceBurstDuration))
			{
				if (!await Wait(0f))
					return;
				burstElapsed += (float)GetPhysicsProcessDeltaTime();
				player = PlayerReads.Find(this);
				if (player is null)
					continue;
				Vector2 toPlayer = player.GlobalPosition + new Vector2(0f, -6f) - GlobalPosition;
				// Dashed through it: stop steering, it overshoots for the rest of this burst.
				if (player.IsDashing && toPlayer.Length() < DanceDodgeCommitDistance)
					dodged = true;
				if (toPlayer.Length() <= DanceHitRadius)
					reached = true;
				if (dodged || toPlayer.Length() < 8f)
					continue;
				float maxTurn = DanceHomingTurnRate * (float)GetPhysicsProcessDeltaTime();
				float turn = Mathf.Clamp(Velocity.AngleTo(toPlayer), -maxTurn, maxTurn);
				Velocity = Velocity.Rotated(turn).Normalized() * speed;
				FaceTowards(player.GlobalPosition.X);
			}
			_danceHitbox.Deactivate();
			SetNeonLight(new Color(0.3f, 1f, 0.45f), 0f);

			// Between bursts it hangs in the air re-aiming, drifting after the player (down to
			// DanceGapHoldDistance) so simply running away doesn't open the gap — dodging still means a dash.
			Velocity = Vector2.Zero;
			if (burst < DanceBursts - 1)
			{
				float gapElapsed = 0f;
				while (gapElapsed < DanceBurstGap)
				{
					if (!await Wait(0f))
						return;
					gapElapsed += (float)GetPhysicsProcessDeltaTime();
					player = PlayerReads.Find(this);
					if (player is null)
						continue;
					Vector2 toPlayer = player.GlobalPosition - GlobalPosition;
					Velocity = toPlayer.Length() > DanceGapHoldDistance ? toPlayer.Normalized() * DanceGapDriftSpeed : Vector2.Zero;
					FaceTowards(player.GlobalPosition.X);
				}
				Velocity = Vector2.Zero;
			}
		}

		// Drop and land, then stay open for a moment.
		Gravity = _baseGravity;
		Sprite.Play("jump");
		Sprite.Frame = 7;
		if (!await WaitUntilLanded(2f))
			return;
		EndMove(DanceRecovery);
	}

	// ── Stone line ──────────────────────────────────────────────────────────────────────────

	// The stones are only drawn inside cast_stone frames 12-19, left of the mushroom (x 2-73 of the
	// 160px frame, measured): crop x0-64 (clear of the mushroom's body), y30-112, into their own clip.
	private SpriteFrames BuildStakeFrames()
	{
		var frames = new SpriteFrames();
		frames.RemoveAnimation("default");
		frames.AddAnimation("erupt");
		frames.SetAnimationLoop("erupt", false);
		frames.SetAnimationSpeed("erupt", 18);
		SpriteFrames source = Sprite?.SpriteFrames;
		if (source is null || !source.HasAnimation("cast_stone"))
			return frames;
		for (int i = 11; i < source.GetFrameCount("cast_stone"); i++)
		{
			frames.AddFrame("erupt", new AtlasTexture
			{
				Atlas = source.GetFrameTexture("cast_stone", i),
				Region = new Rect2(0f, 30f, 64f, 82f),
			});
		}
		return frames;
	}

	private async Task StoneLine(Metroidvania.Player.Player player)
	{
		_busy = true;
		_stoneCooldownTimer = StoneCooldown;
		Velocity = Vector2.Zero;
		FaceTowards(player.GlobalPosition.X);
		float direction = FacingRight ? 1f : -1f;

		Sprite.Play("cast");
		Tell(new Color(1.4f, 1.1f, 0.8f, 1f), StoneCastDelay);
		if (!await Wait(StoneCastDelay))
			return;

		float feetY = GlobalPosition.Y + BodyBottom;
		PhysicsDirectSpaceState2D space = GetWorld2D().DirectSpaceState;
		var exclude = new Godot.Collections.Array<Rid> { GetRid() };
		for (int i = 0; i < StoneCount; i++)
		{
			float x = GlobalPosition.X + direction * (StoneFirstOffset + i * StoneSpacing);

			// Stop at a wall between the previous stake and this one.
			var wallQuery = PhysicsRayQueryParameters2D.Create(new Vector2(x - direction * StoneSpacing, feetY - 20f),
				new Vector2(x, feetY - 20f), CollisionMask, exclude);
			if (space.IntersectRay(wallQuery).Count > 0)
				break;

			// Stakes only rise out of ground near its own floor level (none over a pit).
			var groundQuery = PhysicsRayQueryParameters2D.Create(new Vector2(x, feetY - 40f), new Vector2(x, feetY + 60f), CollisionMask, exclude);
			var ground = space.IntersectRay(groundQuery);
			if (ground.Count > 0 && GetTree().CurrentScene is Node scene)
			{
				var stake = new StoneStake();
				// The art faces left (stones drawn left of the mushroom), so flip it when the line runs right.
				stake.Setup(_stakeFrames, StoneScale, direction > 0f, Stats, StoneHitboxSize, StoneTell, StoneActive, StoneLinger, OnHitDealt);
				scene.AddChild(stake);
				stake.GlobalPosition = (Vector2)ground["position"];
			}

			if (!await Wait(StoneInterval))
				return;
		}

		EndMove(StoneRecovery);
	}

	// ── Phase 2 ─────────────────────────────────────────────────────────────────────────────

	private bool _openWithFlower;

	private async Task EnterPhaseTwo()
	{
		_phaseTwoStarted = true;
		_openWithFlower = true;
		_flowerCooldownTimer = 0f;
		_busy = true;
		Velocity = Vector2.Zero;
		Sprite.Play("hurt");

		SpawnShockwave(RageGlowColor, 150f);
		// Tint the mushroom sprite only (not Visual): the blades/tell glow hang off Visual and would dim,
		// and Enemy's hit flash drives Visual.Modulate.
		Sprite.Modulate = RageTint;

		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		var aura = new Sprite2D
		{
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			Material = additive,
			Modulate = RageGlowColor with { A = 0.5f },
			Scale = Vector2.One * 2.6f,
			ShowBehindParent = true,
		};
		Visual.AddChild(aura);
		var light = new PointLight2D
		{
			Texture = GD.Load<Texture2D>("res://resources/lighting/PointLightGradient.tres"),
			Color = RageGlowColor,
			Energy = 0.8f,
			TextureScale = 1.4f,
		};
		AddChild(light);
		Tween pulse = CreateTween().SetLoops();
		pulse.TweenProperty(aura, "modulate:a", 0.2f, 0.45f).SetTrans(Tween.TransitionType.Sine);
		pulse.Parallel().TweenProperty(light, "energy", 0.4f, 0.45f).SetTrans(Tween.TransitionType.Sine);
		pulse.TweenProperty(aura, "modulate:a", 0.5f, 0.45f).SetTrans(Tween.TransitionType.Sine);
		pulse.Parallel().TweenProperty(light, "energy", 0.8f, 0.45f).SetTrans(Tween.TransitionType.Sine);
		// Kept so phase 3 can swap the red rage look for the golden one.
		_rageAura = aura;
		_rageLight = light;
		_ragePulse = pulse;

		if (!await Wait(RageTransitionPause))
			return;
		EndMove(0f);
	}

	// ── Phase 3: golden rebirth ─────────────────────────────────────────────────────────────

	private bool IsPhaseThree => _phaseThreeStarted;

	// Collapses (death clip), a pillar of golden light comes down on it with a flash, and it rises
	// again healed and wrapped in the golden neon aura. Invulnerable throughout.
	private async Task EnterPhaseThree()
	{
		_phaseThreeStarted = true;
		_phaseTwoStarted = true;
		_busy = true;
		Stats.ExternalInvulnerable = true;
		Velocity = Vector2.Zero;
		_danceHitbox.Deactivate();
		_flowerHitbox.Deactivate();
		_slamHitbox.Deactivate();
		_spinHitbox.Deactivate();

		Sprite.Play("death_poison");
		if (!await Wait(ReviveCollapseTime))
			return;

		// Pillar of light descending onto it.
		if (GetTree().CurrentScene is Node scene)
		{
			var pillar = BuildLightColumn(GoldColor, 60f, 900f);
			scene.AddChild(pillar);
			pillar.GlobalPosition = new Vector2(GlobalPosition.X, GlobalPosition.Y + BodyBottom);
			Tween fade = pillar.CreateTween();
			fade.TweenProperty(pillar, "modulate:a", 0f, ReviveRiseTime + 0.4f).SetEase(Tween.EaseType.In);
			fade.TweenCallback(Callable.From(pillar.QueueFree));
		}
		SpawnShockwave(GoldColor, 170f);
		ScreenFlash(GoldColor with { A = 0.6f }, 0.5f);

		Stats.MinHealth = 0;
		Stats.SetCurrentHealth(Mathf.Max(1, Mathf.RoundToInt(Stats.MaxHealth * PhaseThreeHealthRatio)));
		ApplyGoldLook();
		Sprite.PlayBackwards("death_poison");
		if (!await Wait(ReviveRiseTime))
			return;

		Stats.ExternalInvulnerable = false;
		_nukeCooldownTimer = 0f;
		_spearRainCooldownTimer = SpearRainFirstDelay;
		EndMove(0.3f);
	}

	// Swaps the red rage look for gold: golden tint, a neon outline hugging its silhouette (updated
	// every frame in _Process), a soft golden glow, a light and golden sparks rising off it.
	private void ApplyGoldLook()
	{
		_ragePulse?.Kill();
		_rageAura?.QueueFree();
		_rageLight?.QueueFree();

		Sprite.Modulate = GoldTint;
		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		var soft = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres");

		_goldAura = new Sprite2D
		{
			Material = NeonSilhouette.CreateMaterial(GoldColor, GoldAuraIntensity),
			Modulate = new Color(1f, 1f, 1f, 0.8f),
		};
		Visual.AddChild(_goldAura);
		Visual.MoveChild(_goldAura, 0);

		var glow = new Sprite2D { Texture = soft, Material = additive, Modulate = GoldColor with { A = 0.3f }, Scale = Vector2.One * 2.8f, ShowBehindParent = true };
		Visual.AddChild(glow);
		var light = new PointLight2D
		{
			Texture = GD.Load<Texture2D>("res://resources/lighting/PointLightGradient.tres"),
			Color = GoldColor,
			Energy = 1f,
			TextureScale = 1.6f,
		};
		AddChild(light);

		var rise = new Curve();
		rise.AddPoint(new Vector2(0f, 1f));
		rise.AddPoint(new Vector2(1f, 0f));
		AddChild(new CpuParticles2D
		{
			Amount = 26,
			Lifetime = 1.2f,
			Texture = soft,
			Material = additive,
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
			EmissionRectExtents = new Vector2(34f, 30f),
			Direction = Vector2.Up,
			Spread = 15f,
			Gravity = new Vector2(0f, -40f),
			InitialVelocityMin = 20f,
			InitialVelocityMax = 50f,
			ScaleAmountMin = 0.06f,
			ScaleAmountMax = 0.12f,
			ScaleAmountCurve = rise,
			Color = GoldColor with { A = 0.9f },
			LocalCoords = false,
		});

		Tween pulse = CreateTween().SetLoops();
		pulse.TweenProperty(_goldAura, "modulate:a", 0.45f, 0.5f).SetTrans(Tween.TransitionType.Sine);
		pulse.Parallel().TweenProperty(glow, "modulate:a", 0.15f, 0.5f).SetTrans(Tween.TransitionType.Sine);
		pulse.Parallel().TweenProperty(light, "energy", 0.45f, 0.5f).SetTrans(Tween.TransitionType.Sine);
		pulse.TweenProperty(_goldAura, "modulate:a", 0.9f, 0.5f).SetTrans(Tween.TransitionType.Sine);
		pulse.Parallel().TweenProperty(glow, "modulate:a", 0.3f, 0.5f).SetTrans(Tween.TransitionType.Sine);
		pulse.Parallel().TweenProperty(light, "energy", 0.8f, 0.5f).SetTrans(Tween.TransitionType.Sine);
	}

	public override void _Process(double delta)
	{
		// The golden outline mirrors the current animation frame, slightly larger than the sprite.
		if (_goldAura is null || Sprite?.SpriteFrames is null)
			return;
		_goldAura.Texture = Sprite.SpriteFrames.GetFrameTexture(Sprite.Animation, Sprite.Frame);
		_goldAura.Position = Sprite.Position;
		_goldAura.Offset = Sprite.Offset;
		_goldAura.FlipH = Sprite.FlipH;
		_goldAura.Centered = Sprite.Centered;
		_goldAura.Scale = Sprite.Scale * GoldAuraScale;
	}

	// ── Nuke ────────────────────────────────────────────────────────────────────────────────

	// Radahn's light nuke, in 2D: leap high and hang there while light gathers and the screen darkens
	// (the tell), then a golden ring blasts outward across the arena — dash through its edge as it
	// passes — and light beams rain down one after another on marked spots (the first on the player).
	// Then it drops back down, open for a while.
	private async Task Nuke(Metroidvania.Player.Player player)
	{
		_busy = true;
		_nukeCooldownTimer = NukeCooldown;
		Velocity = Vector2.Zero;
		FaceTowards(player.GlobalPosition.X);
		float groundY = GlobalPosition.Y + BodyBottom;

		Sprite.Play("jump");
		Sprite.Pause();
		Sprite.Frame = 2;
		Tell(GoldColor * 2f, NukeTellDuration);
		if (!await Wait(NukeTellDuration))
			return;

		Sprite.Play("jump");
		Sprite.Frame = 3;
		_airControl = true;
		Velocity = new Vector2(0f, NukeRiseVelocity);
		while (Velocity.Y < 0f)
		{
			if (!await Wait(0f))
				return;
		}

		// Charge.
		Gravity = 0f;
		Velocity = Vector2.Zero;
		Sprite.Play("cast");
		ColorRect darkness = ScreenOverlay(new Color(0f, 0f, 0.02f, 0f));
		darkness?.CreateTween().TweenProperty(darkness, "color:a", NukeDarken, NukeChargeDuration);
		Node2D orb = BuildNukeOrb();
		AddChild(orb);
		Tween grow = orb.CreateTween();
		grow.TweenProperty(orb, "scale", Vector2.One * 1.4f, NukeChargeDuration).From(Vector2.One * 0.1f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
		SetNeonLight(GoldColor, NeonLightEnergy * 1.6f);
		if (!await Wait(NukeChargeDuration))
			return;

		// Blast.
		orb.QueueFree();
		SetNeonLight(GoldColor, 0f);
		ScreenFlash(new Color(1f, 0.93f, 0.7f, 0.85f), 0.45f);
		if (IsInstanceValid(darkness))
		{
			Tween clear = darkness.CreateTween();
			clear.TweenProperty(darkness, "color:a", 0f, 0.6f);
			clear.TweenCallback(Callable.From(() => darkness.GetParent()?.QueueFree()));
		}
		SpawnShockwave(GoldColor, 120f);
		if (!await ExpandNukeRing())
			return;

		// Beams.
		player = PlayerReads.Find(this);
		for (int i = 0; i < NukeBeamCount && player is not null; i++)
		{
			float x = i == 0 ? player.GlobalPosition.X : player.GlobalPosition.X + _rng.RandfRange(-NukeBeamSpread, NukeBeamSpread);
			SpawnLightBeam(new Vector2(x, groundY));
			if (!await Wait(NukeBeamInterval))
				return;
		}

		// Drop back down, open.
		Gravity = _baseGravity;
		Sprite.Play("jump");
		Sprite.Frame = 5;
		if (!await WaitUntilLanded(2.5f))
			return;
		SpawnShockwave(GoldColor, 70f);
		EndMove(NukeRecovery);
	}

	// Golden ring racing outward from the boss; hits the player once when its edge passes over them
	// (dash i-frames go through it).
	private async Task<bool> ExpandNukeRing()
	{
		if (GetTree().CurrentScene is not Node scene)
			return true;

		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		var ringNode = new Node2D();
		scene.AddChild(ringNode);
		ringNode.GlobalPosition = GlobalPosition;
		Line2D glow = Circle(1f, NukeRingThickness, GoldColor with { A = 0.55f }, additive);
		Line2D core = Circle(1f, 6f, new Color(1f, 0.97f, 0.85f, 1f), additive);
		ringNode.AddChild(glow);
		ringNode.AddChild(core);
		ringNode.AddChild(new PointLight2D
		{
			Texture = GD.Load<Texture2D>("res://resources/lighting/PointLightGradient.tres"),
			Color = GoldColor,
			Energy = 1.5f,
			TextureScale = NukeRadius / 60f,
		});

		bool hit = false;
		float elapsed = 0f;
		while (elapsed < NukeExpandTime)
		{
			float radius = Mathf.Max(1f, NukeRadius * elapsed / NukeExpandTime);
			// Circle() draws unit-radius points; widths are divided back out so the stroke stays constant.
			ringNode.Scale = Vector2.One * radius;
			glow.Width = NukeRingThickness / radius;
			core.Width = 6f / radius;

			var player = PlayerReads.Find(this);
			if (!hit && player is not null)
			{
				float d = player.GlobalPosition.DistanceTo(ringNode.GlobalPosition);
				if (Mathf.Abs(d - radius) <= NukeRingThickness * 0.5f)
				{
					hit = true;
					HitPlayerWithLight(player, NukeDamageMultiplier, ringNode.GlobalPosition, NukeKnockback);
				}
			}

			if (!await Wait(0f))
			{
				ringNode.QueueFree();
				return false;
			}
			elapsed += (float)GetPhysicsProcessDeltaTime();
		}

		Tween fade = ringNode.CreateTween();
		fade.TweenProperty(ringNode, "modulate:a", 0f, 0.25f);
		fade.TweenCallback(Callable.From(ringNode.QueueFree));
		return true;
	}

	// A marked spot on the ground (tell), then a column of light slams down on it.
	private void SpawnLightBeam(Vector2 groundPoint)
	{
		if (GetTree().CurrentScene is not Node scene)
			return;

		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		var mark = new Node2D();
		scene.AddChild(mark);
		mark.GlobalPosition = groundPoint;
		var markLine = new Line2D
		{
			Points = new[] { new Vector2(-NukeBeamHalfWidth, 0f), new Vector2(NukeBeamHalfWidth, 0f) },
			Width = 4f,
			Material = additive,
			DefaultColor = GoldColor,
		};
		mark.AddChild(markLine);
		mark.AddChild(new Sprite2D
		{
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			Material = additive,
			Modulate = GoldColor with { A = 0.6f },
			Scale = new Vector2(NukeBeamHalfWidth / 14f, 0.5f),
		});
		Tween blink = mark.CreateTween().SetLoops(3);
		blink.TweenProperty(mark, "modulate:a", 0.3f, NukeBeamTell / 6f);
		blink.TweenProperty(mark, "modulate:a", 1f, NukeBeamTell / 6f);

		GetTree().CreateTimer(NukeBeamTell).Timeout += () =>
		{
			if (!IsInstanceValid(mark))
				return;
			Node2D column = BuildLightColumn(GoldColor, NukeBeamHalfWidth * 2f, 700f);
			mark.AddChild(column);
			var player = IsInstanceValid(this) ? PlayerReads.Find(this) : null;
			if (player is not null && Mathf.Abs(player.GlobalPosition.X - groundPoint.X) <= NukeBeamHalfWidth + 10f
				&& player.GlobalPosition.Y > groundPoint.Y - 700f)
				HitPlayerWithLight(player, NukeBeamDamageMultiplier, groundPoint, 200f);
			Tween fade = mark.CreateTween();
			fade.TweenInterval(NukeBeamActive);
			fade.TweenProperty(mark, "modulate:a", 0f, 0.25f);
			fade.TweenCallback(Callable.From(mark.QueueFree));
		};
	}

	// ── Radahn helpers ──────────────────────────────────────────────────────────────────────

	// Hits the player if their center is within radius of `center`.
	private bool HitIfNear(Vector2 center, float radius, float multiplier, float knockback)
	{
		var player = PlayerReads.Find(this);
		if (player is null || player.GlobalPosition.DistanceTo(center) > radius)
			return false;
		HitPlayerWithLight(player, multiplier, center, knockback);
		return true;
	}

	// A golden neon copy of the mushroom (the frame of `animation`/`frame`), standing with its feet at
	// feetPoint — the light clones of Radahn's Lightspeed moves.
	private Sprite2D SpawnGoldClone(Vector2 feetPoint, string animation, int frame, bool faceRight)
	{
		if (GetTree().CurrentScene is not Node scene || Sprite?.SpriteFrames is null)
			return null;
		var clone = new Sprite2D
		{
			Texture = Sprite.SpriteFrames.GetFrameTexture(animation, frame),
			Offset = Sprite.Offset,
			Scale = Sprite.Scale,
			// Visual's scale flip isn't inherited here, so bake the facing into FlipH.
			FlipH = faceRight ? Sprite.FlipH : !Sprite.FlipH,
			Material = NeonSilhouette.CreateMaterial(GoldColor, 1.8f),
			Modulate = new Color(1f, 1f, 1f, 0f),
			ZIndex = ZIndex + 1,
		};
		scene.AddChild(clone);
		clone.GlobalPosition = feetPoint + new Vector2(0f, -BodyBottom) + Sprite.Position;
		return clone;
	}

	// Blinking ground mark (golden or gravity purple) under a coming hit.
	private Node2D SpawnGroundMark(Vector2 groundPoint, float halfWidth, Color color, float duration)
	{
		if (GetTree().CurrentScene is not Node scene)
			return null;
		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		var mark = new Node2D();
		scene.AddChild(mark);
		mark.GlobalPosition = groundPoint;
		mark.AddChild(new Line2D { Points = new[] { new Vector2(-halfWidth, 0f), new Vector2(halfWidth, 0f) }, Width = 4f, Material = additive, DefaultColor = color });
		mark.AddChild(new Sprite2D
		{
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			Material = additive,
			Modulate = color with { A = 0.55f },
			Scale = new Vector2(halfWidth / 14f, 0.5f),
		});
		Tween blink = mark.CreateTween().SetLoops(Mathf.Max(1, Mathf.RoundToInt(duration / 0.2f)));
		blink.TweenProperty(mark, "modulate:a", 0.3f, 0.1f);
		blink.TweenProperty(mark, "modulate:a", 1f, 0.1f);
		GetTree().CreateTimer(duration).Timeout += () => { if (IsInstanceValid(mark)) mark.QueueFree(); };
		return mark;
	}

	private void FlashTrail(Vector2 from, Vector2 to, Color color, float width, float duration)
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		var line = new Line2D { Points = new[] { from, to }, Width = width, Material = additive, DefaultColor = color };
		line.AddChild(new Line2D { Points = new[] { from, to }, Width = width * 0.3f, Material = additive, DefaultColor = new Color(1f, 0.97f, 0.85f, 1f) });
		scene.AddChild(line);
		Tween fade = line.CreateTween();
		fade.TweenProperty(line, "modulate:a", 0f, duration);
		fade.TweenCallback(Callable.From(line.QueueFree));
	}

	// Irregular rock chunk (procedural polygon) with a gravity glow around it.
	private Node2D BuildRock(float radius)
	{
		var rock = new Node2D();
		rock.AddChild(new Sprite2D
		{
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			Modulate = GravityColor with { A = 0.7f },
			Scale = Vector2.One * radius / 9f,
		});
		var points = new Vector2[7];
		for (int i = 0; i < points.Length; i++)
			points[i] = Vector2.Right.Rotated(Mathf.Tau * i / points.Length) * radius * _rng.RandfRange(0.7f, 1.1f);
		rock.AddChild(new Polygon2D { Polygon = points, Color = new Color(0.32f, 0.28f, 0.3f, 1f) });
		return rock;
	}

	// Leap up to hover height, or return false if interrupted.
	private async Task<bool> LeapAndHover(float velocity)
	{
		Sprite.Play("jump");
		Sprite.Frame = 3;
		_airControl = true;
		Velocity = new Vector2(0f, velocity);
		while (Velocity.Y < 0f)
		{
			if (!await Wait(0f))
				return false;
		}
		Gravity = 0f;
		Velocity = Vector2.Zero;
		return true;
	}

	// ── Lightspeed Slash ────────────────────────────────────────────────────────────────────

	private async Task LightspeedSlash(Metroidvania.Player.Player player)
	{
		_busy = true;
		_lightSlashCooldownTimer = LightSlashCooldown;
		Velocity = Vector2.Zero;
		FaceTowards(player.GlobalPosition.X);
		float groundY = GlobalPosition.Y + BodyBottom;

		Tell(GoldColor * 2f, 0.35f);
		if (!await Wait(0.35f) || !await LeapAndHover(FlowerLeapVelocity))
			return;
		Sprite.Play("cast");

		for (int i = 0; i < LightSlashClones; i++)
		{
			player = PlayerReads.Find(this);
			if (player is null)
				break;
			var landing = new Vector2(player.GlobalPosition.X, groundY);
			SpawnGroundMark(landing, LightSlashCloneRadius * 0.8f, GoldColor, LightSlashCloneTell);
			Sprite2D clone = SpawnGoldClone(landing + new Vector2(0f, -LightSlashCloneHeight), "spin", 1, player.GlobalPosition.X >= GlobalPosition.X);
			if (clone is not null)
			{
				Tween drop = clone.CreateTween();
				drop.TweenProperty(clone, "modulate:a", 0.9f, LightSlashCloneTell * 0.6f);
				drop.TweenInterval(LightSlashCloneTell * 0.4f);
				drop.TweenProperty(clone, "global_position:y", clone.GlobalPosition.Y + LightSlashCloneHeight, LightSlashCloneFall).SetEase(Tween.EaseType.In);
				drop.TweenCallback(Callable.From(() =>
				{
					SpawnShockwaveAt(landing, GoldColor, LightSlashCloneRadius);
					HitIfNear(landing + new Vector2(0f, -30f), LightSlashCloneRadius, LightSlashCloneDamageMultiplier, 180f);
				}));
				drop.TweenProperty(clone, "modulate:a", 0f, 0.2f);
				drop.TweenCallback(Callable.From(clone.QueueFree));
			}
			if (!await Wait(LightSlashCloneInterval))
				return;
		}

		if (!await Wait(LightSlashCloneTell))
			return;

		// Radahn himself crashes onto the player.
		player = PlayerReads.Find(this);
		Vector2 target = player?.GlobalPosition ?? GlobalPosition + Vector2.Down * 200f;
		FaceTowards(target.X);
		Velocity = (target - GlobalPosition).Normalized() * LightSlashDiveSpeed;
		Sprite.Play("spin");
		if (!await WaitUntilLanded(1.5f))
			return;
		Velocity = Vector2.Zero;
		SpawnShockwave(GoldColor, LightSlashCrashRadius);
		HitIfNear(GlobalPosition, LightSlashCrashRadius, LightSlashCrashDamageMultiplier, 320f);
		EndMove(LightSlashRecovery);
	}

	// ── Lightspeed Dash ─────────────────────────────────────────────────────────────────────

	private async Task LightspeedDash(Metroidvania.Player.Player player)
	{
		_busy = true;
		_lightDashCooldownTimer = LightDashCooldown;
		Velocity = Vector2.Zero;
		FaceTowards(player.GlobalPosition.X);
		Sprite.Play("jump");
		Sprite.Pause();
		Sprite.Frame = 1;
		Tell(GoldColor * 2f, LightDashTell);
		if (!await Wait(LightDashTell))
			return;

		// Clones first, then Radahn.
		for (int i = 0; i <= LightDashClones; i++)
		{
			player = PlayerReads.Find(this);
			if (player is null)
				break;
			bool self = i == LightDashClones;
			float dir = player.GlobalPosition.X >= GlobalPosition.X ? 1f : -1f;
			float y = GlobalPosition.Y;
			float startX = GlobalPosition.X;
			float endX = player.GlobalPosition.X + dir * LightDashOvershoot;

			// Path tell: a thin line along the dash lane.
			FlashTrail(new Vector2(startX, y), new Vector2(endX, y), GoldColor with { A = 0.5f }, 3f, LightDashPathTell);
			if (!await Wait(LightDashPathTell))
				return;

			if (self)
			{
				if (!await DashSelf(dir, endX))
					return;
			}
			else
			{
				DashClone(new Vector2(startX, y + BodyBottom), endX, dir);
				if (!await Wait(LightDashInterval))
					return;
			}
		}
		EndMove(LightDashRecovery);
	}

	// A light clone streaking along the ground from `feet` to endX, hitting the player as it passes.
	private void DashClone(Vector2 feet, float endX, float dir)
	{
		Sprite2D clone = SpawnGoldClone(feet, "spin", 2, dir > 0f);
		if (clone is null)
			return;
		clone.Modulate = Colors.White;
		float distance = Mathf.Abs(endX - feet.X);
		float duration = distance / LightDashSpeed;
		Vector2 start = clone.GlobalPosition;
		FlashTrail(start, new Vector2(endX, start.Y), GoldColor, 12f, 0.35f);
		bool hit = false;
		Tween move = clone.CreateTween();
		move.TweenMethod(Callable.From<float>(t =>
		{
			if (!IsInstanceValid(clone))
				return;
			clone.GlobalPosition = new Vector2(Mathf.Lerp(start.X, endX, t), start.Y);
			var p = PlayerReads.Find(this);
			if (!hit && p is not null && Mathf.Abs(p.GlobalPosition.X - clone.GlobalPosition.X) < 24f
				&& Mathf.Abs(p.GlobalPosition.Y - (feet.Y - BodyBottom)) < LightDashHitHeight)
			{
				hit = true;
				HitPlayerWithLight(p, LightDashDamageMultiplier, clone.GlobalPosition - new Vector2(dir * 30f, 0f), 260f);
			}
		}), 0f, 1f, duration);
		move.TweenProperty(clone, "modulate:a", 0f, 0.15f);
		move.TweenCallback(Callable.From(clone.QueueFree));
	}

	// Radahn's own light-speed dash: invulnerable, passes through the player, stops at ledges/walls.
	private async Task<bool> DashSelf(float dir, float endX)
	{
		Stats.ExternalInvulnerable = true;
		uint layer = CollisionLayer;
		CollisionLayer = 0;
		_airControl = true;
		Sprite.Play("spin");
		Vector2 start = GlobalPosition;
		bool hit = false;
		bool ok = true;
		while ((endX - GlobalPosition.X) * dir > 0f && CanStepTo(dir))
		{
			Velocity = new Vector2(dir * LightDashSpeed, 0f);
			var p = PlayerReads.Find(this);
			if (!hit && p is not null && Mathf.Abs(p.GlobalPosition.X - GlobalPosition.X) < 30f
				&& Mathf.Abs(p.GlobalPosition.Y - GlobalPosition.Y) < LightDashHitHeight)
			{
				hit = true;
				HitPlayerWithLight(p, LightDashDamageMultiplier, GlobalPosition - new Vector2(dir * 30f, 0f), 300f);
			}
			if (!await Wait(0f))
			{
				ok = false;
				break;
			}
		}
		Velocity = Vector2.Zero;
		FlashTrail(start, GlobalPosition, GoldColor, 16f, 0.4f);
		CollisionLayer = layer;
		Stats.ExternalInvulnerable = false;
		return ok;
	}

	// ── Consort's Meteor Rain ───────────────────────────────────────────────────────────────

	private async Task ConsortMeteor(Metroidvania.Player.Player player)
	{
		_busy = true;
		_meteorCooldownTimer = MeteorCooldown;
		Velocity = Vector2.Zero;
		FaceTowards(player.GlobalPosition.X);
		float groundY = GlobalPosition.Y + BodyBottom;

		// Ground slam.
		Sprite.Play("spin");
		Tell(GoldColor * 2f, 0.3f);
		if (!await Wait(0.3f))
			return;
		SpawnShockwave(GoldColor, MeteorSlamRadius);
		HitIfNear(GlobalPosition, MeteorSlamRadius, MeteorSlamDamageMultiplier, 250f);

		// Rise out of sight (invulnerable, no collision).
		Stats.ExternalInvulnerable = true;
		uint layer = CollisionLayer;
		CollisionLayer = 0;
		_airControl = true;
		Gravity = 0f;
		Sprite.Play("jump");
		Sprite.Frame = 3;
		Velocity = new Vector2(0f, -MeteorDropHeight / MeteorRiseTime);
		Tween vanish = CreateTween();
		vanish.TweenProperty(Visual, "modulate:a", 0f, MeteorRiseTime);
		if (!await Wait(MeteorRiseTime))
			return;
		Velocity = Vector2.Zero;

		// Target mark follows the player, then locks.
		var mark = SpawnGroundMark(new Vector2(player.GlobalPosition.X, groundY), 60f, GoldColor, MeteorTrackTime + MeteorLockTime);
		float tracked = 0f;
		while (tracked < MeteorTrackTime)
		{
			player = PlayerReads.Find(this);
			if (player is not null && IsInstanceValid(mark))
				mark.GlobalPosition = new Vector2(player.GlobalPosition.X, groundY);
			if (!await Wait(0f))
				return;
			tracked += (float)GetPhysicsProcessDeltaTime();
		}
		float lockX = IsInstanceValid(mark) ? mark.GlobalPosition.X : GlobalPosition.X;
		if (!await Wait(MeteorLockTime))
			return;

		// Crash.
		GlobalPosition = new Vector2(lockX, groundY - BodyBottom - MeteorDropHeight);
		Visual.Modulate = Visual.Modulate with { A = 1f };
		Sprite.Play("spin");
		Vector2 top = GlobalPosition;
		Velocity = new Vector2(0f, MeteorFallSpeed);
		Gravity = _baseGravity;
		CollisionLayer = layer;
		if (!await WaitUntilLanded(1.5f))
			return;
		Velocity = Vector2.Zero;
		Stats.ExternalInvulnerable = false;
		FlashTrail(top, GlobalPosition, GoldColor, 30f, 0.5f);
		ScreenFlash(GoldColor with { A = 0.45f }, 0.35f);
		SpawnShockwave(GoldColor, MeteorBlastRadius);
		if (GetTree().CurrentScene is Node scene)
		{
			Node2D pillar = BuildLightColumn(GoldColor, 70f, 900f);
			scene.AddChild(pillar);
			pillar.GlobalPosition = new Vector2(GlobalPosition.X, groundY);
			Tween fade = pillar.CreateTween();
			fade.TweenProperty(pillar, "modulate:a", 0f, 0.7f);
			fade.TweenCallback(Callable.From(pillar.QueueFree));
		}
		HitIfNear(GlobalPosition, MeteorBlastRadius, MeteorDamageMultiplier, 400f);
		EndMove(MeteorRecovery);
	}

	// ── Gravity Dive ────────────────────────────────────────────────────────────────────────

	private async Task GravityDive(Metroidvania.Player.Player player)
	{
		_busy = true;
		_gravityDiveCooldownTimer = GravityDiveCooldown;
		Velocity = Vector2.Zero;
		FaceTowards(player.GlobalPosition.X);
		float groundY = GlobalPosition.Y + BodyBottom;

		Sprite.Play("jump");
		Sprite.Pause();
		Sprite.Frame = 2;
		Tell(GravityColor * 2f, 0.4f);
		if (!await Wait(0.4f) || !await LeapAndHover(GravityDiveLeapHeight))
			return;

		// Dive onto the player.
		player = PlayerReads.Find(this);
		Vector2 target = player is not null ? new Vector2(player.GlobalPosition.X, groundY - BodyBottom) : GlobalPosition + Vector2.Down * 200f;
		FaceTowards(target.X);
		Velocity = (target - GlobalPosition).Normalized() * 900f;
		Sprite.Play("spin");
		if (!await WaitUntilLanded(1.5f))
			return;
		Velocity = Vector2.Zero;
		SpawnShockwave(GravityColor, GravityDiveImpactRadius);
		HitIfNear(GlobalPosition, GravityDiveImpactRadius, GravityDiveDamageMultiplier, 300f);

		// Gravity waves rolling outward along the ground, flinging rocks.
		SpawnGravityWave(new Vector2(GlobalPosition.X, groundY), 1f);
		SpawnGravityWave(new Vector2(GlobalPosition.X, groundY), -1f);
		EndMove(GravityDiveRecovery);
	}

	// A purple gravity front travelling along the ground: a low glowing band + rocks thrown up behind
	// it. Hits a grounded player as it passes (jump over it).
	private void SpawnGravityWave(Vector2 origin, float dir)
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		var front = new Node2D();
		scene.AddChild(front);
		front.GlobalPosition = origin;
		var fade = new Gradient();
		fade.SetColor(0, GravityColor with { A = 0.9f });
		fade.SetColor(1, GravityColor with { A = 0f });
		front.AddChild(new Line2D { Points = new[] { Vector2.Zero, new Vector2(0f, -GravityWaveHeight) }, Width = 26f, Gradient = fade, Material = additive });
		front.AddChild(new Line2D { Points = new[] { Vector2.Zero, new Vector2(0f, -GravityWaveHeight * 0.7f) }, Width = 6f, DefaultColor = new Color(0.9f, 0.8f, 1f, 1f), Material = additive });

		float duration = GravityWaveDistance / GravityWaveSpeed;
		bool hit = false;
		float rockTimer = 0f;
		float last = 0f;
		Tween move = front.CreateTween();
		move.TweenMethod(Callable.From<float>(t =>
		{
			if (!IsInstanceValid(front))
				return;
			float dt = (t - last) * duration;
			last = t;
			front.GlobalPosition = origin + new Vector2(dir * GravityWaveDistance * t, 0f);
			// Rocks flung up behind the front.
			rockTimer -= dt;
			if (rockTimer <= 0f)
			{
				rockTimer = 0.07f;
				FlingRock(front.GlobalPosition, dir);
			}
			var p = IsInstanceValid(this) ? PlayerReads.Find(this) : null;
			if (!hit && p is not null && Mathf.Abs(p.GlobalPosition.X - front.GlobalPosition.X) < 18f
				&& p.GlobalPosition.Y > origin.Y - GravityWaveHeight - 40f)
			{
				hit = true;
				HitPlayerWithLight(p, GravityDiveDamageMultiplier * 0.7f, front.GlobalPosition - new Vector2(dir * 20f, 0f), 220f);
			}
		}), 0f, 1f, duration);
		move.TweenProperty(front, "modulate:a", 0f, 0.2f);
		move.TweenCallback(Callable.From(front.QueueFree));
	}

	// Purely visual rock chunk arcing up and falling (debris of the gravity wave).
	private void FlingRock(Vector2 from, float dir)
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		Node2D rock = BuildRock(_rng.RandfRange(3f, 7f));
		scene.AddChild(rock);
		rock.GlobalPosition = from;
		Vector2 velocity = new(dir * _rng.RandfRange(-40f, 120f), _rng.RandfRange(-320f, -180f));
		float life = 0.8f;
		Tween arc = rock.CreateTween();
		arc.TweenMethod(Callable.From<float>(t =>
		{
			if (!IsInstanceValid(rock))
				return;
			float time = t * life;
			rock.GlobalPosition = from + velocity * time + new Vector2(0f, 0.5f * 900f * time * time);
			rock.Rotation = time * 8f * dir;
		}), 0f, 1f, life);
		arc.Parallel().TweenProperty(rock, "modulate:a", 0f, life).SetEase(Tween.EaseType.In);
		arc.TweenCallback(Callable.From(rock.QueueFree));
	}

	// ── Gravity Meteor ──────────────────────────────────────────────────────────────────────

	private async Task GravityMeteor(Metroidvania.Player.Player player)
	{
		_busy = true;
		_gravityMeteorCooldownTimer = GravityMeteorCooldown;
		Velocity = Vector2.Zero;
		FaceTowards(player.GlobalPosition.X);
		float groundY = GlobalPosition.Y + BodyBottom;

		// Slash the ground and go up into the sky; rocks rise from the ground and gather around it,
		// wrapped in gravity.
		Sprite.Play("cast_stone");
		Tell(GravityColor * 2f, 0.4f);
		if (!await Wait(0.4f) || GetTree().CurrentScene is not Node scene)
			return;
		var rocks = new System.Collections.Generic.List<Node2D>();
		for (int i = 0; i < GravityMeteorRocks; i++)
		{
			Node2D rock = BuildRock(GravityMeteorRockRadius);
			scene.AddChild(rock);
			float side = i % 2 == 0 ? -1f : 1f;
			rock.GlobalPosition = new Vector2(GlobalPosition.X + side * (30f + 22f * i), groundY);
			rocks.Add(rock);
		}
		if (!await LeapAndHover(GravityMeteorSkyVelocity))
			return;
		Sprite.Play("cast");
		for (int i = 0; i < rocks.Count; i++)
		{
			if (!IsInstanceValid(rocks[i]))
				continue;
			float angle = Mathf.Pi + Mathf.Pi * (i + 0.5f) / rocks.Count;
			Tween rise = rocks[i].CreateTween();
			rise.TweenProperty(rocks[i], "global_position", GlobalPosition + Vector2.Right.Rotated(angle) * 70f, GravityMeteorHoverTime * 0.8f)
				.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
		}
		if (!await Wait(GravityMeteorHoverTime))
			return;

		// Fling them at the player one by one; each impact raises a light pillar.
		foreach (Node2D rock in rocks)
		{
			player = PlayerReads.Find(this);
			if (!IsInstanceValid(rock))
				continue;
			Vector2 target = player?.GlobalPosition ?? rock.GlobalPosition + Vector2.Down * 100f;
			LaunchRock(rock, target, groundY);
			if (!await Wait(GravityMeteorRockInterval))
				return;
		}

		// Then the hard part: rounds of light-speed dives — clones first, then Radahn.
		for (int round = 0; round < GravityMeteorDiveRounds; round++)
		{
			for (int c = 0; c < GravityMeteorDiveClones; c++)
			{
				player = PlayerReads.Find(this);
				if (player is null)
					break;
				float side = c % 2 == 0 ? -1f : 1f;
				Vector2 landing = new(player.GlobalPosition.X, groundY);
				Vector2 skyFeet = landing + new Vector2(side * 220f, -320f);
				DiveClone(skyFeet, landing);
				if (!await Wait(GravityMeteorCloneStagger))
					return;
			}

			player = PlayerReads.Find(this);
			Vector2 target = player?.GlobalPosition ?? GlobalPosition + Vector2.Down * 200f;
			FaceTowards(target.X);
			Gravity = 0f;
			Velocity = (target - GlobalPosition).Normalized() * GravityMeteorDiveSpeed;
			Sprite.Play("spin");
			if (!await WaitUntilLanded(1.5f))
				return;
			Velocity = Vector2.Zero;
			SpawnShockwave(GoldColor, LightSlashCrashRadius * 0.8f);
			HitIfNear(GlobalPosition, LightSlashCrashRadius * 0.8f, LightSlashCrashDamageMultiplier * 0.8f, 280f);

			if (round < GravityMeteorDiveRounds - 1)
			{
				Gravity = _baseGravity;
				if (!await Wait(0.15f) || !await LeapAndHover(GravityMeteorReleapVelocity))
					return;
			}
		}
		EndMove(GravityMeteorRecovery);
	}

	// A light clone appearing in the sky (feet at skyFeet), marking its landing, then diving onto it.
	private void DiveClone(Vector2 skyFeet, Vector2 landing)
	{
		Sprite2D clone = SpawnGoldClone(skyFeet, "spin", 1, landing.X >= skyFeet.X);
		if (clone is null)
			return;
		SpawnGroundMark(landing, 40f, GoldColor, GravityMeteorCloneTell + GravityMeteorCloneDiveTime);
		Vector2 offset = clone.GlobalPosition - skyFeet;
		Tween dive = clone.CreateTween();
		dive.TweenProperty(clone, "modulate:a", 0.95f, GravityMeteorCloneTell);
		dive.TweenProperty(clone, "global_position", landing + offset, GravityMeteorCloneDiveTime).SetEase(Tween.EaseType.In);
		dive.TweenCallback(Callable.From(() =>
		{
			if (!IsInstanceValid(this))
				return;
			FlashTrail(skyFeet + offset, landing + offset, GoldColor, 14f, 0.3f);
			SpawnShockwaveAt(landing, GoldColor, 50f);
			HitIfNear(landing + new Vector2(0f, -30f), 50f, LightSlashCloneDamageMultiplier, 200f);
		}));
		dive.TweenProperty(clone, "modulate:a", 0f, 0.2f);
		dive.TweenCallback(Callable.From(clone.QueueFree));
	}

	private void LaunchRock(Node2D rock, Vector2 target, float groundY)
	{
		Vector2 start = rock.GlobalPosition;
		Vector2 direction = (target - start).Normalized();
		// Fly until it passes the target or hits the ground.
		float distance = Mathf.Min(900f, start.DistanceTo(target) + 60f);
		if (direction.Y > 0.05f)
			distance = Mathf.Min(distance, (groundY - start.Y) / direction.Y);
		float duration = distance / GravityMeteorRockSpeed;
		bool hit = false;
		Tween fly = rock.CreateTween();
		fly.TweenMethod(Callable.From<float>(t =>
		{
			if (!IsInstanceValid(rock))
				return;
			rock.GlobalPosition = start + direction * distance * t;
			rock.Rotation += 0.3f;
			var p = IsInstanceValid(this) ? PlayerReads.Find(this) : null;
			if (!hit && p is not null && p.GlobalPosition.DistanceTo(rock.GlobalPosition) < GravityMeteorRockRadius + 18f)
			{
				hit = true;
				HitPlayerWithLight(p, GravityMeteorDamageMultiplier, rock.GlobalPosition, 200f);
			}
		}), 0f, 1f, duration);
		fly.TweenCallback(Callable.From(() =>
		{
			if (!IsInstanceValid(this))
				return;
			Vector2 impact = new(rock.GlobalPosition.X, groundY);
			SpawnShockwaveAt(rock.GlobalPosition, GravityColor, 40f);
			// Phase 3: each impact raises a pillar of light a moment later.
			SpawnGroundMark(impact, 20f, GoldColor, 0.45f);
			GetTree().CreateTimer(0.45f).Timeout += () =>
			{
				if (!IsInstanceValid(this) || GetTree().CurrentScene is not Node s)
					return;
				Node2D pillar = BuildLightColumn(GoldColor, 36f, 600f);
				s.AddChild(pillar);
				pillar.GlobalPosition = impact;
				HitIfNear(impact + new Vector2(0f, -30f), 30f, 1f, 150f);
				Tween fade = pillar.CreateTween();
				fade.TweenProperty(pillar, "modulate:a", 0f, 0.5f);
				fade.TweenCallback(Callable.From(pillar.QueueFree));
			};
			rock.QueueFree();
		}));
	}

	// ── Starcaller Cry ──────────────────────────────────────────────────────────────────────

	private async Task StarcallerCry(Metroidvania.Player.Player player)
	{
		_busy = true;
		_starcallerCooldownTimer = StarcallerCooldown;
		Velocity = Vector2.Zero;
		FaceTowards(player.GlobalPosition.X);
		float groundY = GlobalPosition.Y + BodyBottom;

		// Rises a little and opens the gravity field.
		if (!await LeapAndHover(-380f))
			return;
		Sprite.Play("cast");
		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		var field = new Node2D();
		AddChild(field);
		var rings = new System.Collections.Generic.List<Line2D>();
		for (int i = 0; i < 4; i++)
		{
			Line2D ring = Circle(StarcallerPullRadius, 3f, GravityColor with { A = 0.6f }, additive);
			field.AddChild(ring);
			rings.Add(ring);
		}
		field.AddChild(new Sprite2D
		{
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			Material = additive,
			Modulate = GravityColor with { A = 0.7f },
			Scale = Vector2.One * 3f,
		});
		SetNeonLight(GravityColor, NeonLightEnergy);

		// Pull: rings collapse inward on a loop while the player is dragged toward the boss.
		float elapsed = 0f;
		while (elapsed < StarcallerPullDuration)
		{
			for (int i = 0; i < rings.Count; i++)
			{
				float phase = Mathf.PosMod(elapsed * 0.9f + i / (float)rings.Count, 1f);
				rings[i].Scale = Vector2.One * (1f - phase);
				rings[i].Modulate = GravityColor with { A = 0.7f * phase };
			}
			player = PlayerReads.Find(this);
			if (player is not null)
			{
				float dx = GlobalPosition.X - player.GlobalPosition.X;
				if (Mathf.Abs(dx) < StarcallerPullRadius && Mathf.Abs(dx) > 20f)
					player.MoveAndCollide(new Vector2(Mathf.Sign(dx) * StarcallerPullSpeed * (float)GetPhysicsProcessDeltaTime(), 0f));
			}
			if (!await Wait(0f))
			{
				field.QueueFree();
				return;
			}
			elapsed += (float)GetPhysicsProcessDeltaTime();
		}
		field.QueueFree();
		SetNeonLight(GravityColor, 0f);

		// Slam with gravity spikes, then a ring of light pillars bursting outward.
		Gravity = _baseGravity;
		Velocity = new Vector2(0f, 700f);
		Sprite.Play("spin");
		if (!await WaitUntilLanded(1.5f))
			return;
		Velocity = Vector2.Zero;
		SpawnShockwave(GravityColor, StarcallerSlamRadius);
		HitIfNear(GlobalPosition, StarcallerSlamRadius, StarcallerDamageMultiplier, 380f);
		for (int i = 1; i <= StarcallerPillars / 2; i++)
		{
			foreach (float side in new[] { -1f, 1f })
			{
				Vector2 spot = new(GlobalPosition.X + side * i * StarcallerPillarSpacing, groundY);
				float delay = 0.15f * i;
				SpawnGroundMark(spot, 18f, GoldColor, delay + 0.2f);
				GetTree().CreateTimer(delay + 0.2f).Timeout += () =>
				{
					if (!IsInstanceValid(this) || GetTree().CurrentScene is not Node s)
						return;
					Node2D pillar = BuildLightColumn(GoldColor, 34f, 600f);
					s.AddChild(pillar);
					pillar.GlobalPosition = spot;
					HitIfNear(spot + new Vector2(0f, -30f), 28f, 1f, 150f);
					Tween fade = pillar.CreateTween();
					fade.TweenProperty(pillar, "modulate:a", 0f, 0.45f);
					fade.TweenCallback(Callable.From(pillar.QueueFree));
				};
			}
		}
		EndMove(StarcallerRecovery);
	}

	// ── Spear rain ──────────────────────────────────────────────────────────────────────────

	private async Task SpearRain(Metroidvania.Player.Player player)
	{
		_busy = true;
		_spearRainCooldownTimer = SpearRainCooldown;
		// Keep the nuke from firing right after it.
		_nukeCooldownTimer = Mathf.Max(_nukeCooldownTimer, 4f);
		Velocity = Vector2.Zero;
		FaceTowards(player.GlobalPosition.X);
		float groundY = GlobalPosition.Y + BodyBottom;

		Sprite.Play("jump");
		Sprite.Pause();
		Sprite.Frame = 2;
		Tell(GoldColor * 2f, NukeTellDuration);
		if (!await Wait(NukeTellDuration))
			return;

		Sprite.Play("jump");
		Sprite.Frame = 3;
		_airControl = true;
		Velocity = new Vector2(0f, SpearRainRiseVelocity);
		while (Velocity.Y < 0f)
		{
			if (!await Wait(0f))
				return;
		}
		Gravity = 0f;
		Velocity = Vector2.Zero;
		Sprite.Play("cast");
		SetNeonLight(GoldColor, NeonLightEnergy);

		for (int wave = 0; wave < SpearWaves; wave++)
		{
			player = PlayerReads.Find(this);
			if (player is null)
				break;
			// Centered on the player, shifted by a fraction of the spacing each wave so gaps move.
			float center = player.GlobalPosition.X;
			float shift = SpearSpacing * (wave % 2 == 0 ? 0f : 0.5f) + _rng.RandfRange(-12f, 12f);
			for (float x = -SpearAreaHalfWidth; x <= SpearAreaHalfWidth; x += SpearSpacing)
				SpawnLightSpear(new Vector2(center + x + shift, groundY));
			if (!await Wait(SpearWaveInterval))
				return;
		}

		if (!await Wait(SpearTell + SpearFallTime))
			return;
		SetNeonLight(GoldColor, 0f);
		Gravity = _baseGravity;
		Sprite.Play("jump");
		Sprite.Frame = 5;
		if (!await WaitUntilLanded(2.5f))
			return;
		EndMove(SpearRecovery);
	}

	// Marks the landing spot, then a golden spear streaks down (slightly tilted) and bursts on it.
	private void SpawnLightSpear(Vector2 groundPoint)
	{
		if (GetTree().CurrentScene is not Node scene)
			return;

		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		var soft = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres");
		var root = new Node2D();
		scene.AddChild(root);
		root.GlobalPosition = groundPoint;

		// Ground mark.
		var mark = new Node2D();
		root.AddChild(mark);
		mark.AddChild(new Line2D
		{
			Points = new[] { new Vector2(-SpearHalfWidth, 0f), new Vector2(SpearHalfWidth, 0f) },
			Width = 3f,
			Material = additive,
			DefaultColor = GoldColor,
		});
		mark.AddChild(new Sprite2D { Texture = soft, Material = additive, Modulate = GoldColor with { A = 0.5f }, Scale = new Vector2(SpearHalfWidth / 16f, 0.35f) });
		Tween blink = mark.CreateTween().SetLoops(3);
		blink.TweenProperty(mark, "modulate:a", 0.3f, SpearTell / 6f);
		blink.TweenProperty(mark, "modulate:a", 1f, SpearTell / 6f);

		// The spear: glow + white-hot core, pointing along its fall direction.
		float tilt = Mathf.DegToRad(_rng.RandfRange(-SpearMaxTiltDegrees, SpearMaxTiltDegrees));
		Vector2 fallDirection = Vector2.Down.Rotated(tilt);
		var spear = new Node2D { Rotation = fallDirection.Angle(), Visible = false };
		root.AddChild(spear);
		var glowFade = new Gradient();
		glowFade.SetColor(0, GoldColor with { A = 0f });
		glowFade.SetColor(1, GoldColor);
		spear.AddChild(new Line2D { Points = new[] { new Vector2(-SpearLength, 0f), Vector2.Zero }, Width = 10f, Gradient = glowFade, Material = additive });
		var coreFade = new Gradient();
		coreFade.SetColor(0, new Color(1f, 0.97f, 0.85f, 0f));
		coreFade.SetColor(1, new Color(1f, 0.97f, 0.85f, 1f));
		spear.AddChild(new Line2D { Points = new[] { new Vector2(-SpearLength * 0.8f, 0f), Vector2.Zero }, Width = 3f, Gradient = coreFade, Material = additive });
		spear.AddChild(new Sprite2D { Texture = soft, Material = additive, Modulate = GoldColor, Scale = Vector2.One * 0.5f });
		Vector2 start = -fallDirection * SpearFallHeight;
		spear.Position = start;

		GetTree().CreateTimer(SpearTell).Timeout += () =>
		{
			if (!IsInstanceValid(root))
				return;
			spear.Visible = true;
			Tween fall = spear.CreateTween();
			fall.TweenProperty(spear, "position", Vector2.Zero, SpearFallTime).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
			fall.TweenCallback(Callable.From(() =>
			{
				if (!IsInstanceValid(root))
					return;
				mark.QueueFree();
				var burst = new Sprite2D { Texture = soft, Material = additive, Modulate = GoldColor, Scale = new Vector2(1.6f, 0.9f) };
				root.AddChild(burst);
				root.AddChild(new PointLight2D
				{
					Texture = GD.Load<Texture2D>("res://resources/lighting/PointLightGradient.tres"),
					Color = GoldColor,
					Energy = 1.2f,
					TextureScale = 0.5f,
				});
				var target = IsInstanceValid(this) ? PlayerReads.Find(this) : null;
				if (target is not null && Mathf.Abs(target.GlobalPosition.X - groundPoint.X) <= SpearHalfWidth + 10f
					&& Mathf.Abs(target.GlobalPosition.Y - (groundPoint.Y - 30f)) <= 60f)
					HitPlayerWithLight(target, SpearDamageMultiplier, groundPoint, 150f);
				Tween fade = root.CreateTween();
				fade.TweenInterval(0.15f);
				fade.TweenProperty(root, "modulate:a", 0f, 0.35f);
				fade.TweenCallback(Callable.From(root.QueueFree));
			}));
		};
	}

	// Vertical column of light standing on its origin (bottom), `height` tall.
	private static Node2D BuildLightColumn(Color color, float width, float height)
	{
		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		var column = new Node2D();
		var fadeUp = new Gradient();
		fadeUp.SetColor(0, color with { A = 0.9f });
		fadeUp.SetColor(1, color with { A = 0f });
		column.AddChild(new Line2D
		{
			Points = new[] { Vector2.Zero, new Vector2(0f, -height) },
			Width = width,
			Gradient = fadeUp,
			Material = additive,
		});
		var coreFade = new Gradient();
		coreFade.SetColor(0, new Color(1f, 0.98f, 0.9f, 1f));
		coreFade.SetColor(1, new Color(1f, 0.98f, 0.9f, 0f));
		column.AddChild(new Line2D
		{
			Points = new[] { Vector2.Zero, new Vector2(0f, -height) },
			Width = width * 0.3f,
			Gradient = coreFade,
			Material = additive,
		});
		column.AddChild(new PointLight2D
		{
			Texture = GD.Load<Texture2D>("res://resources/lighting/PointLightGradient.tres"),
			Color = color,
			Energy = 1.4f,
			TextureScale = 1.2f,
			Position = new Vector2(0f, -60f),
		});
		return column;
	}

	// Golden charge orb gathering around the boss: glow, pulsing ring, sparks converging inward.
	private Node2D BuildNukeOrb()
	{
		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		var soft = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres");
		var orb = new Node2D { Scale = Vector2.One * 0.1f };
		orb.AddChild(new Sprite2D { Texture = soft, Material = additive, Modulate = GoldColor with { A = 0.8f }, Scale = Vector2.One * 4f });
		orb.AddChild(new Sprite2D { Texture = soft, Material = additive, Modulate = new Color(1f, 0.97f, 0.85f, 1f), Scale = Vector2.One * 1.6f });
		Line2D ring = Circle(90f, 5f, GoldColor, additive);
		orb.AddChild(ring);
		Tween pulse = ring.CreateTween().SetLoops();
		pulse.TweenProperty(ring, "modulate:a", 0.3f, 0.12f);
		pulse.TweenProperty(ring, "modulate:a", 1f, 0.12f);
		AddChild(new CpuParticles2D
		{
			Name = "NukeGather",
			Amount = 40,
			Lifetime = 0.6f,
			OneShot = false,
			Texture = soft,
			Material = additive,
			EmissionShape = CpuParticles2D.EmissionShapeEnum.SphereSurface,
			EmissionSphereRadius = 170f,
			Gravity = Vector2.Zero,
			RadialAccelMin = -520f,
			RadialAccelMax = -420f,
			ScaleAmountMin = 0.08f,
			ScaleAmountMax = 0.14f,
			Color = GoldColor,
		});
		orb.TreeExiting += () => GetNodeOrNull("NukeGather")?.QueueFree();
		return orb;
	}

	private void HitPlayerWithLight(Metroidvania.Player.Player player, float multiplier, Vector2 from, float knockback)
	{
		Stats stats = player.GetNodeOrNull<Stats>("Stats");
		if (stats is null)
			return;
		int before = stats.CurrentHealth;
		stats.TakeDamage(Mathf.RoundToInt(Stats.AttackPower * multiplier));
		if (stats.CurrentHealth >= before)
			return; // dodged (dash i-frames) or blocked
		Vector2 direction = (player.GlobalPosition - from).Normalized();
		player.ApplyKnockback(direction == Vector2.Zero ? Vector2.Up : direction, knockback);
		OnHitDealt();
	}

	// Full-screen color layer above the world (removed by the caller / its own fade).
	private ColorRect ScreenOverlay(Color color)
	{
		if (GetTree().CurrentScene is not Node scene)
			return null;
		var layer = new CanvasLayer { Layer = 4 };
		var rect = new ColorRect { Color = color, MouseFilter = Control.MouseFilterEnum.Ignore };
		rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		layer.AddChild(rect);
		scene.AddChild(layer);
		return rect;
	}

	private void ScreenFlash(Color color, float duration)
	{
		ColorRect flash = ScreenOverlay(color);
		if (flash is null)
			return;
		Tween fade = flash.CreateTween();
		fade.TweenProperty(flash, "color:a", 0f, duration).SetEase(Tween.EaseType.Out);
		fade.TweenCallback(Callable.From(() => flash.GetParent()?.QueueFree()));
	}

	// ── Spin ────────────────────────────────────────────────────────────────────────────────

	private async Task Spin(Metroidvania.Player.Player player)
	{
		_busy = true;
		_spinCooldownTimer = SpinCooldown;
		Velocity = Vector2.Zero;
		FaceTowards(player.GlobalPosition.X);

		// Tell: the wind-up pose before the spin (spin_poison frames 0-2) + green blink.
		Sprite.Play("spin_poison");
		Sprite.Pause();
		Sprite.Frame = 1;
		Tell(new Color(0.55f, 3.2f, 0.75f, 1f), SpinTell);
		if (!await Wait(SpinTell))
			return;

		Sprite.Play("spin_poison");
		Sprite.Frame = 3;
		SetNeonLight(new Color(0.3f, 1f, 0.45f), NeonLightEnergy);
		_spinHitbox.Activate(Stats);
		if (!await Wait(SpinActive))
			return;
		_spinHitbox.Deactivate();
		SetNeonLight(new Color(0.3f, 1f, 0.45f), 0f);
		EndMove(SpinRecovery);
	}

	// ── Sprouts ─────────────────────────────────────────────────────────────────────────────

	private async Task Sprouts(Metroidvania.Player.Player player)
	{
		_busy = true;
		_sproutCooldownTimer = SproutCooldown;
		Velocity = Vector2.Zero;
		FaceTowards(player.GlobalPosition.X);

		Sprite.Play("cast");
		Tell(new Color(0.55f, 3.2f, 0.75f, 1f), SproutCastDelay);
		if (!await Wait(SproutCastDelay))
			return;

		player = PlayerReads.Find(this);
		if (player is null || GetTree().CurrentScene is not Node scene)
		{
			EndMove(SproutRecovery);
			return;
		}

		// Pick ground spots around the player (alternating sides), marked before anything grows.
		PhysicsDirectSpaceState2D space = GetWorld2D().DirectSpaceState;
		var exclude = new Godot.Collections.Array<Rid> { GetRid() };
		var spots = new System.Collections.Generic.List<Vector2>();
		int toSpawn = Mathf.Min(SproutsPerCast, MaxSproutsAlive - _sprouts.Count);
		for (int i = 0; i < toSpawn * 3 && spots.Count < toSpawn; i++)
		{
			float side = (i % 2 == 0) ? 1f : -1f;
			float x = player.GlobalPosition.X + side * _rng.RandfRange(SproutMinOffset, SproutMaxOffset);
			var query = PhysicsRayQueryParameters2D.Create(new Vector2(x, player.GlobalPosition.Y - 60f),
				new Vector2(x, player.GlobalPosition.Y + 120f), CollisionMask, exclude);
			var hit = space.IntersectRay(query);
			if (hit.Count > 0 && ((Vector2)hit["normal"]).Y < -0.7f)
				spots.Add((Vector2)hit["position"]);
		}

		var marks = new System.Collections.Generic.List<Node2D>();
		foreach (Vector2 spot in spots)
		{
			Node2D mark = BuildSproutMark();
			scene.AddChild(mark);
			mark.GlobalPosition = spot;
			marks.Add(mark);
		}
		if (!await Wait(SproutMarkDuration))
			return;

		foreach (Node2D mark in marks)
			if (IsInstanceValid(mark))
				mark.QueueFree();
		foreach (Vector2 spot in spots)
			SpawnSprout(scene, spot);

		EndMove(SproutRecovery);
	}

	private Node2D BuildSproutMark()
	{
		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		var mark = new Node2D();
		var glow = new Sprite2D
		{
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			Material = additive,
			Modulate = new Color(0.3f, 1f, 0.45f, 0.8f),
			Scale = new Vector2(1.6f, 0.5f),
		};
		var light = new PointLight2D
		{
			Texture = GD.Load<Texture2D>("res://resources/lighting/PointLightGradient.tres"),
			Color = new Color(0.3f, 1f, 0.45f),
			Energy = 1.2f,
			TextureScale = 0.6f,
		};
		mark.AddChild(glow);
		mark.AddChild(light);
		Tween pulse = mark.CreateTween().SetLoops();
		pulse.TweenProperty(glow, "modulate:a", 0.3f, 0.12f);
		pulse.TweenProperty(glow, "modulate:a", 0.8f, 0.12f);
		return mark;
	}

	private void SpawnSprout(Node scene, Vector2 groundPoint)
	{
		var sprout = SproutScene.Instantiate<Enemy>();
		// Unique id so a killed sprout doesn't mark some unrelated saved path as defeated.
		sprout.CustomPersistenceId = $"mushroom_boss_sprout_{GetInstanceId()}_{Time.GetTicksUsec()}";
		scene.AddChild(sprout);
		sprout.GlobalPosition = groundPoint - new Vector2(0f, SproutFeetOffset);
		_sprouts.Add(sprout);

		// Grow in out of the ground (its sprite, not Visual — Enemy drives Visual.Scale for facing).
		if (sprout.GetNodeOrNull<Node2D>("Visual/CharacterSprite") is Node2D sprite)
		{
			Vector2 fullScale = sprite.Scale;
			sprite.Scale = new Vector2(fullScale.X, 0.05f);
			Tween grow = sprite.CreateTween();
			grow.TweenProperty(sprite, "scale", fullScale, 0.3f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		}
	}

	private void WitherSprouts()
	{
		foreach (Enemy sprout in _sprouts)
		{
			if (!IsInstanceValid(sprout) || sprout.IsQueuedForDeletion())
				continue;
			Tween wither = sprout.CreateTween();
			wither.TweenProperty(sprout, "modulate:a", 0f, 0.6f);
			wither.TweenCallback(Callable.From(sprout.QueueFree));
		}
		_sprouts.Clear();
	}

	// ── Heal punish ─────────────────────────────────────────────────────────────────────────

	private async Task HealPunish(Metroidvania.Player.Player player)
	{
		_busy = true;
		Velocity = Vector2.Zero;
		FaceTowards(player.GlobalPosition.X);

		Sprite.Play("jump");
		Sprite.Pause();
		Sprite.Frame = 2;
		Tell(new Color(0.55f, 3.2f, 0.75f, 1f), HealPunishTell);
		if (!await Wait(HealPunishTell))
			return;

		// Ballistic arc that lands where the player is now: vy = -g·T/2, vx = dx/T.
		player = PlayerReads.Find(this);
		float targetX = player?.GlobalPosition.X ?? GlobalPosition.X;
		float airTime = Mathf.Max(0.2f, HealPunishAirTime);
		_airControl = true;
		Sprite.Play("jump");
		Sprite.Frame = 3;
		Velocity = new Vector2((targetX - GlobalPosition.X) / airTime, -Gravity * airTime * 0.5f);
		if (!await Wait(0.1f))
			return;
		if (!await WaitUntilLanded(airTime + 0.6f))
			return;

		Velocity = Vector2.Zero;
		SpawnShockwave(HealPunishColor, HealPunishSlamRadius);
		_slamHitbox.Activate(Stats);
		if (!await Wait(HealPunishSlamActive))
			return;
		_slamHitbox.Deactivate();
		EndMove(HealPunishRecovery);
	}

	// ── Scarlet flower ──────────────────────────────────────────────────────────────────────

	private async Task ScarletFlower(Metroidvania.Player.Player player)
	{
		_busy = true;
		_nextIsFlower = false;
		_openWithFlower = false;
		_flowerCooldownTimer = FlowerCooldown;
		Velocity = Vector2.Zero;
		FaceTowards(player.GlobalPosition.X);

		Sprite.Play("jump");
		Sprite.Pause();
		Sprite.Frame = 2;
		Tell(FlowerColor * 2.2f, FlowerTellDuration);
		if (!await Wait(FlowerTellDuration))
			return;

		// Leap straight up, very high.
		Sprite.Play("jump");
		Sprite.Frame = 3;
		_airControl = true;
		Velocity = new Vector2(0f, FlowerLeapVelocity);
		while (Velocity.Y < 0f)
		{
			if (!await Wait(0f))
				return;
		}

		// Apex: hang in the air while the pink neon circle blooms around it (the tell).
		Gravity = 0f;
		Velocity = Vector2.Zero;
		Sprite.Pause();
		Node2D bloom = BuildBloom();
		SetNeonLight(FlowerColor, NeonLightEnergy * 1.3f);
		AddChild(bloom);
		Tween grow = bloom.CreateTween();
		grow.TweenProperty(bloom, "scale", Vector2.One, FlowerBloomDuration).From(Vector2.One * 0.1f)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		if (!await Wait(FlowerBloomDuration))
			return;

		// Dive at where the player is now.
		player = PlayerReads.Find(this);
		Vector2 target = player?.GlobalPosition ?? GlobalPosition + Vector2.Down * 200f;
		FaceTowards(target.X);
		Velocity = (target - GlobalPosition).Normalized() * FlowerDiveSpeed;
		Sprite.Play("spin");
		if (!await WaitUntilLanded(1.5f))
			return;

		// Explosion.
		bloom.QueueFree();
		SetNeonLight(FlowerColor, 0f);
		Velocity = Vector2.Zero;
		SpawnShockwave(FlowerColor, FlowerExplosionRadius);
		_flowerHitbox.Activate(Stats);
		if (!await Wait(FlowerExplosionActive))
			return;
		_flowerHitbox.Deactivate();
		EndMove(FlowerRecovery);
	}

	// Pink neon ring + soft glow disc around the body.
	private Node2D BuildBloom()
	{
		var bloom = new Node2D { Scale = Vector2.One * 0.1f };
		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		bloom.AddChild(new Sprite2D
		{
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			Material = additive,
			Modulate = FlowerColor with { A = 0.7f },
			Scale = Vector2.One * (FlowerCircleRadius / 24f),
		});
		bloom.AddChild(Circle(FlowerCircleRadius, 10f, FlowerColor with { A = 0.8f }, additive));
		bloom.AddChild(Circle(FlowerCircleRadius, 3f, new Color(1f, 0.85f, 0.95f, 1f), additive));
		return bloom;
	}

	private static Line2D Circle(float radius, float width, Color color, Material material)
	{
		const int segments = 48;
		var points = new Vector2[segments];
		for (int i = 0; i < segments; i++)
			points[i] = Vector2.Right.Rotated(Mathf.Tau * i / segments) * radius;
		// Closed loop (not a repeated first point) so the seam has no stray joint spike.
		return new Line2D { Points = points, Closed = true, Width = width, Modulate = color, Material = material, DefaultColor = Colors.White };
	}

	private void SpawnShockwave(Color color, float radius) => SpawnShockwaveAt(GlobalPosition, color, radius);

	private void SpawnShockwaveAt(Vector2 position, Color color, float radius)
	{
		if (GetTree().CurrentScene is not Node scene)
			return;

		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		var blast = new Node2D();
		scene.AddChild(blast);
		blast.GlobalPosition = position;

		var flash = new Sprite2D
		{
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			Material = additive,
			Modulate = color,
			Scale = Vector2.One * (radius / 20f),
		};
		Line2D ring = Circle(radius, 12f, color, additive);
		Line2D core = Circle(radius, 4f, new Color(1f, 0.9f, 0.97f, 1f), additive);
		var light = new PointLight2D
		{
			Texture = GD.Load<Texture2D>("res://resources/lighting/PointLightGradient.tres"),
			Color = color,
			Energy = 2f,
			TextureScale = radius / 60f,
		};
		blast.AddChild(flash);
		blast.AddChild(ring);
		blast.AddChild(core);
		blast.AddChild(light);

		ring.Scale = core.Scale = Vector2.One * 0.2f;
		Tween tween = blast.CreateTween();
		tween.TweenProperty(ring, "scale", Vector2.One, 0.25f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		tween.Parallel().TweenProperty(core, "scale", Vector2.One, 0.25f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		tween.Parallel().TweenProperty(flash, "modulate:a", 0f, 0.4f);
		tween.Parallel().TweenProperty(light, "energy", 0f, 0.4f);
		tween.TweenProperty(ring, "modulate:a", 0f, 0.2f);
		tween.Parallel().TweenProperty(core, "modulate:a", 0f, 0.2f);
		tween.TweenCallback(Callable.From(blast.QueueFree));
	}
}
