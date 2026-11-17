using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Metroidvania.Player;
using Metroidvania.Save;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Artemisa, la Hoja Carmesí — the user's Malenia-inspired valkyrie (Elementals Light Valkyrie pack,
// recoloured to Malenia's palette: scarlet hair, crimson cape, antique-gold armour; the winged
// elemental form gets scarlet wings). Built 2026-11-13. What's here:
//  * Entrance: kneeling in the middle of the arena (couch_idle) until the player walks in. A code-built
//    cinematic: wide shot, the player walks a few steps, close-up, her first line, she rises
//    (idle_to_couch backwards) with a scarlet petal gust, medium shot, her name line, then the name
//    on screen, music and the fight. Seen once per save; after that a 2 s version without control.
//  * Malenia's kit (2026-11-13, verified against eldenring.wiki.gg): slash chain (the pack combos),
//    Rapid Slashes (3 quick + delayed 4th), leaping Thrust (gap closer + heal punish), shield bash
//    (her Kick; goes through a parry), Waterfowl Dance (3 homing scarlet bursts) and lifesteal on
//    damage that gets through. Same moves in phase 2 with the winged e_* sprites.
//  * Brain, not a script: Decide() scores every available move against what the player is doing
//    (distance, airborne, parry open/spent, whiffed swing, roll/dash exit, flask, pressure) and what
//    she has learned about how he defends (_parryHabit / _evadeHabit), with a repeat penalty. Tells
//    hold while his parry is open (Malenia's delays) and never launch into dodge i-frames.
//  * Phase 2 at 50%: a short scene ("Florece conmigo..."), then she transforms (35-frame transform,
//    invulnerable) while scarlet petals gather — the player gets the whole transform to get away —
//    and blooms: an unparryable scarlet burst around her (dash i-frames dodge). Then e_* animations.
//  * Death: back to human form (back2human), her last line, the death animation.
// Hit windows are frame indexes measured from the sheet's horizontal extent (2026-11-13). The pack
// faces right natively. Its own class (boss-uniqueness rule); joins "boss" once the fight starts.
public partial class ArtemisaBoss : Enemy
{
	[ExportGroup("Entrance")]
	[Export] public float EntranceRange = 520f;
	[Export] public string EntranceCinematicId = "cine_artemisa_entrada";
	// Where the player stops walking in, measured from her (on the player's side).
	[Export] public float PlayerStopDistance = 300f;
	[Export] public string SpeakerKey = "NPC_ARTEMISA_NAME";
	[Export] public string[] EntranceLinesA = { "CIN_ARTEMISA_1", "CIN_ARTEMISA_1B" };
	// Long silences: she's talking to the dead, not to Delfrad (yet).
	[Export] public float[] EntrancePausesA = { 1.0f, 1.6f };
	[Export] public string[] EntranceLinesB = { "CIN_ARTEMISA_2" };
	[Export] public string[] PhaseLines = { "CIN_ARTEMISA_FASE" };
	[Export] public string[] DeathLines = { "CIN_ARTEMISA_MUERTE", "CIN_ARTEMISA_MUERTE_2" };
	[Export] public AudioStream BossMusic;
	// Phase 2 track (same melody, faster). Empty = replay BossMusic slightly sped up.
	[Export] public AudioStream BossMusicFase2;
	[Export] public float MusicVolumeDb = -6f;
	[Export] public float AwakeDetectionRange = 3000f;
	// Optional: the arena's CanvasModulate, dimmed to this colour as the player walks in.
	[Export] public NodePath ArenaModulatePath;
	// White = the hall keeps its light when she notices him (trying it without the dim, 2026-11-16;
	// the previous reddish dusk was (0.78, 0.66, 0.68)).
	[Export] public Color ArenaDimColor = new(1f, 1f, 1f, 1f);
	[Export] public float ArenaDimSeconds = 2.5f;
	// The petal rain over the arena: centred on her, this wide, starting this high above her.
	[Export] public float PetalRainWidth = 1800f;
	[Export] public float PetalRainHeight = 420f;
	// Her own light (she's the only warm thing in the hall).
	[Export] public Color HaloColor = new(1f, 0.55f, 0.4f, 1f);
	[Export] public float HaloEnergy = 1.1f;

	[ExportGroup("Armour")]
	// Real pack sounds: chain-mail clinks while she moves and rises, the spear ringing as she stands.
	[Export] public float ArmourStepInterval = 0.3f;
	// Extra loudness for the armour/spear sounds while she rises — her imposing moment.
	[Export] public float RiseVolumeDb = 6f;
	[Export] public Color GleamColor = new(1f, 0.85f, 0.45f, 1f);
	// Where the glint flashes, in Visual space facing right (helmet / pauldron).
	[Export] public Vector2 GleamOffset = new(-4f, -66f);

	// Prints every move she picks, blocks and parries to the output (for tuning).
	[Export] public bool DebugAI;

	[ExportGroup("Showcase (filming)")]
	// For filming: the brain is switched off and she plays every move in a fixed order, one after
	// another, walking in first when a move needs to be close. Phase one's reel, then (after
	// ShowcaseLapsBeforePhaseTwo laps) the transformation, then phase two's reel on a loop.
	// Stand still with F5 god mode; when she raises the shield, swing at her to film the parry.
	[Export] public bool ShowcaseMode;
	[Export] public float ShowcasePause = 1.4f;
	// 0 = she stays in phase one (the transformation then only comes from damage, as normal).
	[Export] public int ShowcaseLapsBeforePhaseTwo = 1;
	// How long she walks toward the player for a close move before throwing it anyway.
	[Export] public float ShowcaseWalkInTimeout = 3f;

	[ExportGroup("Movement")]
	[Export] public float EngageDistance = 120f;
	[Export] public float PhaseTwoSpeedScale = 1.25f;
	// Phase two's openings: extra seconds she stays spent after each big move (dance, phantoms,
	// flying slash, plunge; Aeonia waits out its own bloom first, then 1.5× this). That's when
	// you hit her.
	[Export] public float PhaseTwoOpening = 0.7f;

	[ExportGroup("Parry stagger")]
	// Parries in a row (within ParryStaggerWindow of each other) that break her stance, in either
	// phase: whatever she's doing stops and she's left reeling, wide open.
	[Export] public int ParriesToStagger = 3;
	[Export] public float ParryStaggerWindow = 4f;
	[Export] public float StaggerDuration = 2.2f;
	[Export] public float HurtDuration = 0.2f;

	[ExportGroup("Spear (Malenia's slash chain)")]
	[Export] public float AttackRange = 210f;
	// Time between moves. Malenia barely breathes: short on purpose.
	[Export] public float AttackCooldown = 0.9f;
	[Export] public float AttackRecovery = 0.45f;
	// The hit area runs from the body out to the spear's measured reach (frame px × sprite scale).
	[Export] public float SpriteScale = 1.6f;
	// Phase two's body size relative to phase one (the goddess of rot is slighter).
	[Export] public float PhaseTwoSize = 0.8f;
	[Export] public float HitHeight = 70f;

	private float SizeFactor => _phaseTwo ? PhaseTwoSize : 1f;
	private float CurrentScale => SpriteScale * SizeFactor;
	[Export] public float LungeSpeed = 160f;
	// Chance of a battle cry (Voice/<VoiceCategory>/Ataque N) at the start of a combo.
	[Export(PropertyHint.Range, "0,1")] public float AttackVoiceChance = 0.5f;

	[ExportGroup("Malenia: lifesteal")]
	// Heals this × the damage that actually got through (a parried hit heals nothing).
	[Export] public float LifestealMultiplier = 1f;
	[Export] public Color HealTextColor = new(1f, 0.45f, 0.5f, 1f);

	[ExportGroup("Malenia: rapid slashes (3 quick + delayed 4th)")]
	[Export] public float RapidTell = 0.35f;
	// The pause before the 4th stab: the one that catches a panic roll.
	[Export] public float RapidDelay = 0.5f;

	[ExportGroup("Malenia: leaping thrust")]
	[Export] public float ThrustMinRange = 260f;
	[Export] public float ThrustCooldown = 4.5f;
	[Export] public float ThrustTell = 0.45f;
	[Export] public float ThrustSpeed = 950f;
	[Export] public float ThrustMaxDistance = 560f;
	// Spear reach while lunging (px from her centre).
	[Export] public float ThrustReach = 112f;
	// Drinking a flask in front of her is answered at once (Malenia's heal punish).
	[Export] public float HealPunishRange = 700f;
	[Export] public float HealPunishTell = 0.15f;

	[ExportGroup("Malenia: shield bash (her kick)")]
	[Export] public float BashRange = 95f;
	[Export] public float BashCooldown = 3f;
	[Export] public float BashTell = 0.2f;
	[Export] public float BashKnockback = 560f;
	[Export] public float BashStepSpeed = 260f;

	[ExportGroup("Malenia: Waterfowl Dance")]
	[Export] public float DanceCooldown = 11f;
	[Export] public float DanceFirstDelay = 7f;
	[Export] public float DanceMinRange = 60f;
	[Export] public float DanceMaxRange = 340f;
	[Export] public float DanceTell = 0.6f;
	[Export] public Vector2 DanceLeap = new(160f, -560f);
	[Export] public int DanceBursts = 3;
	// Phase two's dance plays her slashing combo (e_3_atk) on a loop during each burst: these
	// striking frames, at this speed.
	[Export] public int DanceComboFirst = 3;
	[Export] public int DanceComboLast = 15;
	[Export] public float DanceComboFps = 28f;
	[Export] public float DanceBurstSpeed = 600f;
	[Export] public float DanceBurstDuration = 0.28f;
	// Burst + gap = 1.0 s: one precise dash per burst (the player's dash cycle is 0.78 s).
	[Export] public float DanceBurstGap = 0.72f;
	[Export] public float DanceGapDriftSpeed = 210f;
	[Export] public float DanceGapHoldDistance = 170f;
	[Export] public float DanceHomingTurnRate = 9f;
	[Export] public float DanceHitRadius = 70f;
	[Export] public float DanceFinalBurstSpeed = 760f;
	[Export] public float DanceFinalBurstMaxTime = 2.5f;
	[Export] public float DanceDodgeCommitDistance = 130f;
	[Export] public float DanceRecovery = 1.0f;

	[ExportGroup("Malenia: shield guard + parry")]
	// She raises the shield when he swings at her from the front (more often the more he spams).
	[Export] public float GuardRange = 170f;
	[Export] public float GuardMaxTime = 0.9f;
	[Export] public float GuardCooldown = 2.5f;
	// A hit landing this soon after the shield goes up is parried, not just blocked: he's thrown off
	// balance and she ripostes.
	[Export] public float GuardParryWindow = 0.22f;
	[Export] public float ParryStunTime = 0.6f;
	[Export] public float ParryKnockback = 300f;
	[Export] public float RiposteDamageMultiplier = 1.6f;

	[ExportGroup("Malenia: grab (impale and slam)")]
	[Export] public float GrabCooldown = 9f;
	[Export] public float GrabMinRange = 50f;
	[Export] public float GrabMaxRange = 230f;
	[Export] public float GrabTell = 0.55f;
	[Export] public float GrabLungeSpeed = 800f;
	[Export] public float GrabLungeTime = 0.3f;
	[Export] public float GrabReach = 95f;
	[Export] public int GrabTicks = 3;
	[Export] public float GrabTickMultiplier = 0.5f;
	[Export] public float GrabSlamMultiplier = 2.2f;

	[ExportGroup("Phase 2: Goddess of Rot")]
	// Scarlet rot: the colour of her phase-2 magic (warmer, more orange than her spear neon).
	[Export] public Color RotColor = new(1f, 0.32f, 0.16f, 1f);
	// Scarlet Aeonia: rise in tendrils, dive, a giant flower blooms where she lands, then rot.
	[Export] public float AeoniaCooldown = 18f;
	[Export] public float AeoniaRiseHeight = 300f;
	[Export] public float AeoniaHover = 1.0f;
	[Export] public float AeoniaDiveSpeed = 1100f;
	[Export] public float AeoniaImpactRadius = 90f;
	[Export] public float AeoniaBloomDelay = 1.1f;
	// After the flower blooms she's spent for this long — slowed, heavy breathing: hit her now.
	[Export] public float AeoniaOpening = 2.4f;
	[Export] public float AeoniaBloomRadius = 210f;
	[Export] public float AeoniaBloomMultiplier = 1.6f;
	[Export] public float RotCloudDuration = 4.5f;
	// Scarlet Phantoms: hovering over a rot pool, she sends phantoms of herself through him.
	[Export] public float PhantomsCooldown = 14f;
	[Export] public int PhantomCount = 4;
	[Export] public float PhantomTell = 0.5f;
	[Export] public float PhantomInterval = 0.6f;
	[Export] public float PhantomLungeLength = 440f;
	[Export] public float PhantomMultiplier = 1f;
	// Flying Slash: leap, sweeping arc in the air, then a diving thrust.
	[Export] public float FlyingSlashCooldown = 6f;
	[Export] public float FlyingSlashMinRange = 150f;
	[Export] public float FlyingSlashMaxRange = 450f;
	// Scarlet Plunge: high leap over him, spear driven into the ground, rot bursts forward.
	[Export] public float PlungeCooldown = 8f;
	[Export] public float PlungeMaxRange = 480f;
	[Export] public int PlungeBursts = 3;

	[ExportGroup("Phase 2: scarlet bloom")]
	[Export] public float PhaseTwoHealthRatio = 0.5f;
	// Testing/filming: as soon as she's free after rising, she transforms (the full scarlet-bloom
	// scene plays) and the whole fight is phase two.
	[Export] public bool StartInPhaseTwo;
	// Phase two never stands still: the sprite trembles a little all the time (px)…
	[Export] public float PhaseTwoTremble = 1.3f;
	// …and hard while she gathers power (Aeonia, the phantoms, the transformation).
	[Export] public float ChargeTremble = 4.5f;
	[Export] public float BloomRadius = 360f;
	[Export] public float BloomDamageMultiplier = 2f;
	[Export] public float BloomKnockback = 520f;
	[Export] public Color ScarletGlow = new(1f, 0.18f, 0.22f, 1f);
	[Export] public Color ScarletCore = new(1f, 0.78f, 0.7f, 1f);

	// One spear attack: animation and the frame windows where the hitbox is live, with the reach (in
	// sheet pixels from the frame centre) at that moment.
	private readonly record struct HitWindow(int From, int To, float Reach);

	private static readonly Dictionary<string, HitWindow[]> Moves = new()
	{
		["1_atk"] = new[] { new HitWindow(3, 6, 104) },
		["2_atk"] = new[] { new HitWindow(3, 6, 104), new HitWindow(12, 15, 142) },
		["3_atk"] = new[]
		{
			new HitWindow(3, 6, 104), new HitWindow(12, 15, 142),
			// The closing flurry: five quick stabs.
			new HitWindow(19, 20, 131), new HitWindow(21, 22, 141), new HitWindow(23, 24, 128),
			new HitWindow(25, 26, 122), new HitWindow(27, 28, 123),
		},
		["e_1_atk"] = new[] { new HitWindow(3, 8, 134) },
		["e_2_atk"] = new[] { new HitWindow(3, 8, 134), new HitWindow(9, 10, 127) },
		["e_3_atk"] = new[] { new HitWindow(3, 8, 134), new HitWindow(9, 10, 127), new HitWindow(12, 15, 141) },
	};

	private Hitbox _spear;
	private RectangleShape2D _spearShape;
	private readonly RandomNumberGenerator _rng = new();
	private MusicCrossfader _music;
	private bool _kneeling = true;
	private bool _entering;
	private bool _busy;
	private bool _phaseTwo;
	private bool _phaseStarted;
	private float _baseMoveSpeed;
	private float _hurtTimer;
	private float _attackCooldownTimer;
	private float _armourStepTimer;
	private PointLight2D _halo;
	private GpuParticles2D _petalRain;
	private Hitbox _danceHitbox;
	private float _baseGravity;
	private EnemySlashTrail _trail;
	private BladeFlurry _flurry;
	private PointLight2D _moveLight;
	private readonly WhiffWatcher _whiff = new();
	private float _thrustCooldownTimer;
	private float _bashCooldownTimer;
	private float _danceCooldownTimer;
	private bool _playerWasHealing;
	private int _playerHealthSeen = -1;
	private readonly DashWatcher _dash = new();
	// What she has learned about how this player defends (decays slowly): parries vs rolls/dashes
	// seen during her attacks. Tilts her choices — delays and the bash for a parrier, the delayed 4th
	// stab and exit punishes for a roller.
	private float _parryHabit;
	private float _evadeHabit;
	private string _lastMove = "";
	private string _secondLastMove = "";
	private float _sinceHitByPlayer = 99f;
	private int _hitsTakenRecently;
	private float _farTimer;
	// How much he swings at her (feeds how readily she raises the shield).
	private float _attackHabit;
	private float _guardCooldownTimer;
	private float _grabCooldownTimer;
	private bool _guarding;
	private float _guardTimer;
	private int _guardBlocks;
	private bool _parriedThisGuard;
	private bool _guardRolledThisSwing;
	private bool _guardGoesForParry;
	private float _aeoniaCooldownTimer;
	private float _phantomsCooldownTimer = 6f;
	private float _flyingCooldownTimer = 2f;
	private float _plungeCooldownTimer = 4f;
	// Phase 2 opens with Aeonia, like Malenia's.
	private bool _aeoniaNext;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_rng.Randomize();
		_baseMoveSpeed = MoveSpeed;
		if (Sprite is not null)
			_spriteBasePosition = Sprite.Position;
		_spearShape = new RectangleShape2D { Size = new Vector2(100f, HitHeight) };
		_spear = new Hitbox { Name = "LanzaHitbox", CollisionLayer = 0, CollisionMask = 2 };
		_spear.AddChild(new CollisionShape2D { Name = "CollisionShape2D", Shape = _spearShape, Disabled = true });
		AddChild(_spear);
		_spear.HitDealt += OnHitDealt;
		_danceHitbox = BuildHitbox("DanzaHitbox", new CircleShape2D { Radius = DanceHitRadius }, new Vector2(0f, -20f));
		_baseGravity = Gravity;

		// Her neon: scarlet glow, pale-rose core (the scarlet-rot colour), under Visual so it flips.
		_trail = new EnemySlashTrail
		{
			Name = "EstelaLanza",
			GlowColor = ScarletGlow,
			CoreColor = new Color(1f, 0.85f, 0.8f, 1f),
			GlowWidth = 11f,
			CoreWidth = 3f,
			SparkColor = new Color(1f, 0.6f, 0.45f, 1f),
		};
		Visual.AddChild(_trail);
		_flurry = new BladeFlurry
		{
			Name = "DanzaFlurry",
			GlowColor = ScarletGlow with { A = 0.95f },
			CoreColor = new Color(1f, 0.88f, 0.82f, 1f),
			GlowWidth = 13f,
			CoreWidth = 3.5f,
			Position = new Vector2(0f, -20f),
		};
		Visual.AddChild(_flurry);
		_moveLight = new PointLight2D
		{
			Name = "LuzMovimiento",
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			TextureScale = 2.6f,
			Color = ScarletGlow,
			Energy = 0f,
			Position = new Vector2(0f, -30f),
		};
		AddChild(_moveLight);
		_danceCooldownTimer = DanceFirstDelay;
		_thrustCooldownTimer = 2f;
		_grabCooldownTimer = 5f;
		Stats.IncomingHitInterceptor = InterceptHit;
		Stats.HitTaken += _ => _hurtTimer = HurtDuration;
		Stats.HitTaken += _ =>
		{
			_whiff.NotifyHitTaken();
			_sinceHitByPlayer = 0f;
			_hitsTakenRecently++;
		};
		_busy = true;
		// Untouchable while kneeling: the fight starts with the scene, not with a free hit.
		Stats.ExternalInvulnerable = true;
		Sprite?.Play("couch_idle");
	}

	// ── Boss plumbing ───────────────────────────────────────────────────────────────────────

	protected override bool IsDefeated() => SaveManager.Instance.IsBossDefeated(PersistenceId);

	protected override void OnDefeated()
	{
		SaveManager.Instance.MarkBossDefeated(PersistenceId);
		IsQueuedForRemoval = true;
		RemoveFromGroup("boss");
		SetDeferred(CollisionObject2D.PropertyName.CollisionLayer, 0);
		_spear.CallDeferred(Hitbox.MethodName.Deactivate);
		CallDeferred(MethodName.SpawnLoot);
		_music?.Stop(3f);
		_ = DeathScene();
	}

	protected override bool ContactDamageEnabled => false;
	protected override bool CanTurnToFacePlayer => !_busy;
	protected override bool CanChase => !_kneeling;

	// ── Entrance ────────────────────────────────────────────────────────────────────────────

	private async Task Entrance(Metroidvania.Player.Player player)
	{
		_entering = true;
		FaceTowards(player.GlobalPosition.X);
		bool seen = !string.IsNullOrEmpty(EntranceCinematicId) && SaveManager.Instance.IsCutscenePlayed(EntranceCinematicId);
		if (seen)
		{
			// Coming back after dying: no lines, no hold — she just rises and the fight is on.
			await Rise();
			StartFight(showName: false);
			return;
		}

		float side = Mathf.Sign(player.GlobalPosition.X - GlobalPosition.X);
		if (side == 0f)
			side = -1f;
		var scene = new Cinematic { Name = "EntradaArtemisa", PlayOnce = true, PersistenceId = EntranceCinematicId };
		// The threshold: no door, no jolt — the light goes cold and red,
		// petals start falling and her own light finds her. The camera stays on Delfrad for this —
		// it's his moment of "I'm in".
		scene.Steps.Add(new StepCode { Action = () => { SealArena(); return Task.CompletedTask; } });
		scene.Steps.Add(new StepWait { Seconds = 1.1f });
		// Wide shot: the hall and the kneeling figure under the petals, before anything is said.
		scene.Steps.Add(new StepCamera { Target = new NodePath(".."), Weight = 0.5f, Zoom = 0.85f, WaitSeconds = 1.6f });
		scene.Steps.Add(new StepMove { TargetX = GlobalPosition.X + side * PlayerStopDistance, Speed = 70f });
		scene.Steps.Add(new StepWait { Seconds = 0.8f });
		// Close-up on her, still kneeling: the first lines are said to the floor — to the dead —
		// not to Delfrad.
		scene.Steps.Add(new StepCamera { Target = new NodePath(".."), Weight = 1f, Zoom = 1.7f, WaitSeconds = 1.0f });
		scene.Steps.Add(new StepSay { SpeakerKeys = new[] { SpeakerKey }, LineKeys = EntranceLinesA, ExtraPauses = EntrancePausesA });
		scene.Steps.Add(new StepCode { Action = Rise });
		// Medium shot with both of them: now she's talking to him.
		scene.Steps.Add(new StepCamera { Target = new NodePath(".."), Weight = 0.5f, Zoom = 1.2f, WaitSeconds = 0.7f });
		scene.Steps.Add(new StepSay { SpeakerKeys = new[] { SpeakerKey }, LineKeys = EntranceLinesB, ExtraPauses = new[] { 1.0f } });
		scene.Steps.Add(new StepCode { Action = () => { StartFight(showName: true); return Task.CompletedTask; } });
		scene.Steps.Add(new StepCamera { WaitSeconds = 0.4f });
		AddChild(scene);
		await scene.Play();
		if (IsInstanceValid(scene))
			scene.QueueFree();
		// Skipped: StepCode.Skip fires StartFight without waiting for Rise — make sure she's standing.
		if (IsInstanceValid(this) && _kneeling)
		{
			_kneeling = false;
			Sprite?.Play("idle");
		}
	}

	// The arena takes the player in: dim red light, petal rain, her halo. Silent on purpose — the
	// first loud metal of the scene is her armour when she rises.
	private void SealArena()
	{
		if (ArenaModulatePath is not null && !ArenaModulatePath.IsEmpty
			&& GetNodeOrNull<CanvasModulate>(ArenaModulatePath) is { } modulate)
			modulate.CreateTween().TweenProperty(modulate, "color", ArenaDimColor, ArenaDimSeconds)
				.SetTrans(Tween.TransitionType.Sine);
		StartPetalRain();
		_halo ??= new PointLight2D
		{
			Name = "Halo",
			Color = HaloColor,
			Energy = 0f,
			TextureScale = 3.5f,
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			Position = new Vector2(0f, -45f),
		};
		if (_halo.GetParent() is null)
			AddChild(_halo);
		_halo.CreateTween().TweenProperty(_halo, "energy", HaloEnergy, ArenaDimSeconds);
	}

	// Gets up off her knee (the kneel-down played backwards). The armour sells it: chain-mail
	// clinking on each frame, then the plates settle with a heavy clank, the spear rings, a gold
	// glint runs off the helmet, dust and petals blow off the floor.
	private async Task Rise()
	{
		if (Sprite is null || !_kneeling)
			return;
		if (_halo is null)
			SealArena();
		Sprite.PlayBackwards("idle_to_couch");
		int lastFrame = -1;
		while (Sprite.Animation == "idle_to_couch" && Sprite.IsPlaying())
		{
			if (Sprite.Frame != lastFrame)
			{
				lastFrame = Sprite.Frame;
				Sfx.PlayAt(this, "Footsteps/Stone", "Stone Chain Walk", 2000f, RiseVolumeDb);
			}
			if (!await Wait(0f))
				return;
		}
		_kneeling = false;
		Sprite.Play("idle");
		Sfx.PlayAt(this, "Footsteps/Stone", "Stone Chain Land", 2000f, RiseVolumeDb);
		PlayerReads.Find(this)?.ShakeCamera(6f, 0.3f);
		SpawnDust(GlobalPosition + new Vector2(0f, 40f));
		SpawnPetals(GlobalPosition + new Vector2(0f, -20f), 40, 260f, 1.2f, inward: false);
		if (!await Wait(0.35f))
			return;
		Sfx.PlayAt(this, "Combat/Sword", "Sword Unsheath", 2000f, RiseVolumeDb);
		ArmourGleam();
		await Wait(0.6f);
	}

	// The armour catching the light: the whole sprite flares gold and the light swells at the helmet.
	private void ArmourGleam()
	{
		if (Sprite is null)
			return;
		Tween flare = Sprite.CreateTween();
		flare.TweenProperty(Sprite, "self_modulate", new Color(1.6f, 1.35f, 0.9f, 1f), 0.08f);
		flare.TweenProperty(Sprite, "self_modulate", Colors.White, 0.45f);
		Glint(GleamOffset, GleamColor, 1f);
	}

	// The tell of every attack: she lights up. No shape drawn — her whole body flares (HDR), a
	// strong light washes over her and the floor around her, and a soft additive glow swells at
	// `at` (Visual space, facing right) with a tall pillar of light through her, like the lit
	// doorways (LevelTransition's LightBeam).
	private void Glint(Vector2 at, Color color, float size)
	{
		if (Sprite is not null)
		{
			Color hot = new(1f + color.R * 1.4f, 1f + color.G * 1.4f, 1f + color.B * 1.4f, 1f);
			Tween flare = Sprite.CreateTween();
			flare.TweenProperty(Sprite, "self_modulate", hot, 0.06f);
			flare.TweenProperty(Sprite, "self_modulate", Colors.White, 0.4f).SetEase(Tween.EaseType.Out);
		}

		var gradient = GD.Load<Texture2D>("res://resources/lighting/PointLightGradient.tres");
		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		var glow = new Node2D { Name = "Destello", Position = at, ZIndex = 8 };
		var halo = new Sprite2D
		{
			Texture = gradient,
			Material = additive,
			Modulate = color with { A = 0.8f },
			Scale = Vector2.One * 0.45f * size,
		};
		glow.AddChild(halo);
		var beam = new Sprite2D
		{
			Texture = gradient,
			Material = additive,
			Modulate = color.Lerp(Colors.White, 0.35f) with { A = 0.6f },
			Position = new Vector2(-at.X, -40f - at.Y),
			Scale = new Vector2(0.3f * size, 2.6f),
		};
		glow.AddChild(beam);
		var light = new PointLight2D
		{
			Color = color,
			Energy = 3.2f * Mathf.Min(size, 1.6f),
			TextureScale = 2.4f * size,
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
		};
		glow.AddChild(light);
		Visual.AddChild(glow);
		glow.Modulate = Colors.Transparent;
		Tween tween = glow.CreateTween();
		tween.TweenProperty(glow, "modulate:a", 1f, 0.06f);
		tween.Parallel().TweenProperty(halo, "scale", Vector2.One * 0.8f * size, 0.12f).SetEase(Tween.EaseType.Out);
		tween.TweenInterval(0.08f);
		tween.TweenProperty(glow, "modulate:a", 0f, 0.4f).SetEase(Tween.EaseType.In);
		tween.Parallel().TweenProperty(light, "energy", 0f, 0.4f);
		tween.TweenCallback(Callable.From(glow.QueueFree));
	}

	// Grey dust puffing out to both sides from her feet.
	private void SpawnDust(Vector2 at)
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		var gradient = new Gradient();
		gradient.SetColor(0, new Color(0.75f, 0.7f, 0.68f, 0.55f));
		gradient.SetColor(1, new Color(0.75f, 0.7f, 0.68f, 0f));
		var dust = new GpuParticles2D
		{
			Amount = 26,
			Lifetime = 0.9f,
			OneShot = true,
			Explosiveness = 0.95f,
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			ProcessMaterial = new ParticleProcessMaterial
			{
				Direction = new Vector3(0f, -0.3f, 0f),
				Spread = 90f,
				InitialVelocityMin = 60f,
				InitialVelocityMax = 160f,
				DampingMin = 120f,
				DampingMax = 200f,
				Gravity = new Vector3(0f, -10f, 0f),
				ScaleMin = 0.5f,
				ScaleMax = 1.1f,
				ColorRamp = new GradientTexture1D { Gradient = gradient },
			},
			ZIndex = 5,
			Emitting = true,
		};
		scene.AddChild(dust);
		dust.GlobalPosition = at;
		dust.Finished += dust.QueueFree;
	}

	// Slow scarlet petals drifting down over the whole hall, for the rest of the fight.
	private void StartPetalRain()
	{
		if (_petalRain is not null || GetTree().CurrentScene is not Node scene)
			return;
		var gradient = new Gradient();
		gradient.SetColor(0, ScarletGlow with { A = 0f });
		gradient.AddPoint(0.15f, ScarletGlow with { A = 0.85f });
		gradient.AddPoint(0.85f, ScarletCore with { A = 0.6f });
		gradient.SetColor(gradient.GetPointCount() - 1, ScarletCore with { A = 0f });
		_petalRain = new GpuParticles2D
		{
			Name = "LluviaPetalos",
			Amount = 70,
			Lifetime = 7f,
			Preprocess = 0f,
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			ProcessMaterial = new ParticleProcessMaterial
			{
				EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
				EmissionBoxExtents = new Vector3(PetalRainWidth * 0.5f, 10f, 0f),
				Direction = new Vector3(0.3f, 1f, 0f),
				Spread = 25f,
				InitialVelocityMin = 25f,
				InitialVelocityMax = 55f,
				Gravity = new Vector3(6f, 14f, 0f),
				AngularVelocityMin = -90f,
				AngularVelocityMax = 90f,
				ScaleMin = 0.12f,
				ScaleMax = 0.28f,
				TurbulenceEnabled = true,
				TurbulenceNoiseStrength = 2f,
				TurbulenceNoiseScale = 4f,
				TurbulenceInfluenceMin = 0.02f,
				TurbulenceInfluenceMax = 0.06f,
				ColorRamp = new GradientTexture1D { Gradient = gradient },
			},
			ZIndex = 4,
			Emitting = true,
		};
		scene.AddChild(_petalRain);
		_petalRain.GlobalPosition = GlobalPosition + new Vector2(0f, -PetalRainHeight);
	}

	private void StartFight(bool showName)
	{
		if (IsInGroup("boss"))
			return;
		_kneeling = false;
		Stats.ExternalInvulnerable = false;
		AddToGroup("boss");
		DetectionRange = AwakeDetectionRange;
		if (showName && !string.IsNullOrEmpty(BossNameKey))
			Metroidvania.UI.ZoneTitle.Instance?.Show(TranslationServer.Translate(BossNameKey));
		if (BossMusic is not null && _music is null)
		{
			_music = new MusicCrossfader { Name = "MusicaJefe", VolumeDb = MusicVolumeDb };
			AddChild(_music);
			_music.Play(BossMusic, 0.4f);
		}
		_attackCooldownTimer = 0.6f;
		_busy = false;
	}

	// ── AI ──────────────────────────────────────────────────────────────────────────────────

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;
		float dt = (float)delta;
		_hurtTimer -= dt;
		_attackCooldownTimer -= dt;
		_thrustCooldownTimer -= dt;
		_bashCooldownTimer -= dt;
		_danceCooldownTimer -= dt;
		_guardCooldownTimer -= dt;
		_grabCooldownTimer -= dt;
		if (_phaseTwo)
		{
			_aeoniaCooldownTimer -= dt;
			_phantomsCooldownTimer -= dt;
			_flyingCooldownTimer -= dt;
			_plungeCooldownTimer -= dt;
		}
		_attackHabit = Mathf.Max(0f, _attackHabit - dt * 0.05f);
		_sinceHitByPlayer += dt;
		if (_sinceHitByPlayer > 2.5f)
			_hitsTakenRecently = 0;
		// What she learned fades if he stops doing it.
		_parryHabit = Mathf.Max(0f, _parryHabit - dt * 0.03f);
		_evadeHabit = Mathf.Max(0f, _evadeHabit - dt * 0.03f);
		base._PhysicsProcess(delta);
		UpdateTremble(dt);
		// Armour clinking under every stride.
		_armourStepTimer -= dt;
		if (!_kneeling && IsOnFloor() && Mathf.Abs(Velocity.X) > 5f && _armourStepTimer <= 0f)
		{
			_armourStepTimer = ArmourStepInterval;
			Sfx.PlayAt(this, "Footsteps/Stone", "Stone Chain Run");
		}

		var player = PlayerReads.Find(this);
		if (player is null)
			return;
		_dash.Update(player, dt);
		_whiff.Update(player, dt);
		if (_whiff.PlayerStartedSwing && Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X) < GuardRange + 60f)
			_attackHabit = Mathf.Min(_attackHabit + 0.4f, 4f);
		// Lifesteal reads how much of a hit actually got through against this.
		if (player.GetNodeOrNull<Stats>("Stats") is { } playerStats)
			_playerHealthSeen = playerStats.CurrentHealth;
		if (_kneeling)
		{
			if (!_entering && Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X) <= EntranceRange)
				_ = Entrance(player);
			return;
		}
		if (!_phaseStarted && !_busy && IsOnFloor()
			&& (StartInPhaseTwo || (float)Stats.CurrentHealth / Mathf.Max(1, Stats.MaxHealth) <= PhaseTwoHealthRatio))
		{
			_ = PhaseTransition();
			return;
		}
		if (_busy || !IsOnFloor() || !PlayerDetected)
			return;
		Decide(player, dt);
	}

	// ── Brain ───────────────────────────────────────────────────────────────────────────────
	// Not a script: every free frame she scores what she could do against what the player is doing
	// right now (distance, airborne, parry open or spent, a whiffed swing, the end of a roll/dash, a
	// flask, how hard he's pressing her) plus what she has learned about how he defends, and never
	// leans on the same move twice running.

	private static readonly string[] ShowcaseReelOne =
		{ "slash1", "slash2", "slash3", "rapid", "thrust", "bash", "guard", "grab", "dance" };
	private static readonly string[] ShowcaseReelTwo =
		{ "aeonia", "slash1", "rapid", "phantoms", "flying", "plunge", "dance", "grab", "thrust" };
	private int _abortSerial;
	private int _staggerParries;
	private ulong _lastStaggerParryMsec;
	private bool _staggered;

	// The player reports every parry he lands on her (Player.ReportMeleeParry). Three in a row
	// break her stance.
	public override void RegisterParried(int weight = 1)
	{
		if (ParriesToStagger <= 0 || _staggered || _kneeling || IsQueuedForRemoval
			|| (_phaseStarted && !_phaseTwo) || Stats.ExternalInvulnerable)
			return;
		ulong now = Time.GetTicksMsec();
		if (now - _lastStaggerParryMsec > (ulong)(ParryStaggerWindow * 1000f))
			_staggerParries = 0;
		_lastStaggerParryMsec = now;
		_staggerParries += Mathf.Max(1, weight);
		_parryHabit = Mathf.Min(_parryHabit + 0.3f, 4f);
		if (DebugAI)
			GD.Print($"[Artemisa] parried {_staggerParries}/{ParriesToStagger}");
		if (_staggerParries < ParriesToStagger)
			return;
		_staggerParries = 0;
		// Parries arrive from inside a hit callback: stagger on the next frame.
		Callable.From(() => { _ = Stagger(); }).CallDeferred();
	}

	// Stance broken: every move in flight is cut, she reels back and stays dazed — no shield, no
	// counter — for StaggerDuration. No stars: she lights up and the hall jolts.
	private async Task Stagger()
	{
		if (_staggered || !IsInstanceValid(this) || IsQueuedForRemoval)
			return;
		_staggered = true;
		_abortSerial++;
		_busy = true;
		_guarding = false;
		_spear.Deactivate();
		_danceHitbox.Deactivate();
		Gravity = _baseGravity;
		SetMoveLight(ScarletGlow, 0f);
		foreach (Node child in GetChildren())
			if (child is Tendriles tendrils)
				tendrils.Fade();
		if (Sprite is not null)
			Sprite.Rotation = 0f;
		float dir = FacingRight ? 1f : -1f;
		Velocity = new Vector2(-dir * 260f, Velocity.Y);
		if (DebugAI)
			GD.Print("[Artemisa] STAGGER");
		Sfx.PlayAt(this, "Combat/Sword", "Sword Parry", 2000f, 4f);
		Sfx.PlayIfExistsAt(this, VoiceCategory, "Golpeado", 2000f);
		var player = PlayerReads.Find(this);
		player?.ShakeCamera(14f, 0.35f);
		ScreenFlash(new Color(1f, 0.9f, 0.75f, 0.3f), 0.1f);
		Glint(new Vector2(0f, -40f), GleamColor, 1.8f);
		SpawnPetals(GlobalPosition + new Vector2(0f, -40f), 40, 140f, 0.6f, inward: false);
		Tremble(ChargeTremble, 0.5f);

		string reel = _phaseTwo ? "e_take_hit" : "take_hit";
		Sprite?.Play(reel);
		float elapsed = 0f;
		while (elapsed < StaggerDuration)
		{
			if (!await Wait(0f))
				break;
			elapsed += (float)GetPhysicsProcessDeltaTime();
			Velocity = new Vector2(Mathf.MoveToward(Velocity.X, 0f, 900f * (float)GetPhysicsProcessDeltaTime()), Velocity.Y);
			// Dazed: the reel's last frames, swaying, after it has played once.
			if (Sprite is not null && !Sprite.IsPlaying())
			{
				int frames = Sprite.SpriteFrames?.GetFrameCount(reel) ?? 1;
				ShowFrame(reel, Mathf.Max(0, frames - 1 - (int)(elapsed * 3f) % 2));
			}
		}
		_staggered = false;
		_attackCooldownTimer = 0.4f;
		if (IsInstanceValid(this) && !IsQueuedForRemoval)
			await Recover(0.1f);
	}

	private Vector2 _spriteBasePosition;
	private float _trembleTimer;
	private float _trembleBoost;
	private float _trembleBoostLeft;
	private float _rotDriftTimer;

	// Phase two is smaller: scale the sprite about the feet (the body's bottom edge, y = +42) so
	// she stays standing on the floor. The tremble re-centres on the new base position.
	private void ApplyPhaseTwoSize()
	{
		if (Sprite is null || Mathf.IsEqualApprox(PhaseTwoSize, 1f))
			return;
		const float feetY = 42f;
		_spriteBasePosition = new Vector2(_spriteBasePosition.X * PhaseTwoSize, feetY + (_spriteBasePosition.Y - feetY) * PhaseTwoSize);
		Sprite.Position = _spriteBasePosition;
		Sprite.CreateTween().TweenProperty(Sprite, "scale", Vector2.One * CurrentScale, 0.2f).SetEase(Tween.EaseType.Out);
	}

	private bool _tired;

	// Spent after a big move: the idle slowed right down, the body heaving with heavy breaths and
	// sagging a little, the shiver of phase two almost gone, a few petals dropping off her.
	// Returns false if cut short (a stagger, death).
	private async Task<bool> Exhausted(float seconds)
	{
		if (Sprite is null)
			return await Wait(seconds);
		_tired = true;
		if (DebugAI)
			GD.Print($"[Artemisa] exhausted {seconds:0.0}s");
		string idle = _phaseTwo ? "e_idle" : "idle";
		Sprite.Play(idle);
		Sprite.SpeedScale = 0.45f;
		float elapsed = 0f;
		float petalTimer = 0.3f;
		bool finished = true;
		while (elapsed < seconds)
		{
			if (!await Wait(0f))
			{
				finished = false;
				break;
			}
			float step = (float)GetPhysicsProcessDeltaTime();
			elapsed += step;
			// Breathing: a slow heave, deeper at the start, settling as she recovers.
			float depth = Mathf.Lerp(0.05f, 0.025f, elapsed / seconds);
			float breath = Mathf.Sin(elapsed * 4.2f);
			Sprite.Scale = new Vector2(CurrentScale * (1f - breath * depth * 0.3f), CurrentScale * (1f + breath * depth));
			petalTimer -= step;
			if (petalTimer <= 0f)
			{
				petalTimer = _rng.RandfRange(0.25f, 0.5f);
				SpawnPetals(GlobalPosition + new Vector2(_rng.RandfRange(-15f, 15f), -40f), 2, 20f, 0.9f, inward: false);
			}
		}
		if (IsInstanceValid(Sprite))
		{
			Sprite.SpeedScale = 1f;
			Sprite.Scale = Vector2.One * CurrentScale;
		}
		_tired = false;
		return finished;
	}

	// Extra tremble on top of phase two's constant one, for `seconds`.
	private void Tremble(float amplitude, float seconds)
	{
		_trembleBoost = amplitude;
		_trembleBoostLeft = seconds;
	}

	// Phase two's body: the sprite jitters (re-rolled every few frames, so it shivers rather than
	// slides) and rot drifts off her.
	private void UpdateTremble(float dt)
	{
		if (Sprite is null)
			return;
		_trembleBoostLeft -= dt;
		if (_trembleBoostLeft <= 0f)
			_trembleBoost = 0f;
		// Tired: the constant shiver nearly stops and she sags a couple of pixels.
		float amplitude = (_phaseTwo ? PhaseTwoTremble * (_tired ? 0.3f : 1f) : 0f) + _trembleBoost;
		Vector2 basePosition = _spriteBasePosition + (_tired ? new Vector2(0f, 2f) : Vector2.Zero);
		_trembleTimer -= dt;
		if (_trembleTimer <= 0f)
		{
			_trembleTimer = 0.04f;
			Sprite.Position = amplitude <= 0f
				? basePosition
				: basePosition + new Vector2(_rng.RandfRange(-amplitude, amplitude), _rng.RandfRange(-amplitude, amplitude) * 0.6f);
		}
		if (_phaseTwo && !IsQueuedForRemoval)
		{
			_rotDriftTimer -= dt;
			if (_rotDriftTimer <= 0f)
			{
				_rotDriftTimer = _rng.RandfRange(0.12f, 0.3f);
				SpawnPetals(GlobalPosition + new Vector2(_rng.RandfRange(-20f, 20f), _rng.RandfRange(-80f, -10f)), 2, 30f, 0.7f, inward: false);
			}
		}
	}

	private int _showcaseStep;
	private int _showcaseLaps;
	private float _showcaseWalkTimer;
	// Counts only while she's free (Decide doesn't run mid-move), so it's a real gap between moves.
	private float _showcasePauseLeft;

	// The fixed filming reel (ShowcaseMode). Returns after starting at most one move.
	private void ShowcaseDecide(Metroidvania.Player.Player player, float dt, float distance)
	{
		_showcasePauseLeft -= dt;
		if (_showcasePauseLeft > 0f)
			return;
		if (!_phaseStarted && ShowcaseLapsBeforePhaseTwo > 0 && _showcaseLaps >= ShowcaseLapsBeforePhaseTwo)
		{
			_showcaseStep = 0;
			_showcaseLaps = 0;
			_aeoniaNext = false; // the phase-two reel opens with Aeonia itself
			_ = PhaseTransition();
			return;
		}
		string[] reel = _phaseTwo ? ShowcaseReelTwo : ShowcaseReelOne;
		string move = reel[_showcaseStep % reel.Length];
		// Close moves: walk in first (ComputeMoveX closes the gap while the cooldown is spent).
		float reach = move switch
		{
			"slash1" or "slash2" or "slash3" or "rapid" => AttackRange,
			"bash" => BashRange + 20f,
			"guard" => GuardRange,
			"grab" => GrabMaxRange,
			"dance" => DanceMaxRange,
			"flying" => FlyingSlashMaxRange,
			"plunge" => PlungeMaxRange,
			_ => float.MaxValue,
		};
		// She never walks closer than EngageDistance, so a shorter reach would only wait out the timeout.
		reach = Mathf.Max(reach, EngageDistance + 10f);
		if (distance > reach && _showcaseWalkTimer < ShowcaseWalkInTimeout)
		{
			_showcaseWalkTimer += dt;
			return;
		}
		_showcaseWalkTimer = 0f;
		_showcaseStep++;
		if (_showcaseStep % reel.Length == 0)
			_showcaseLaps++;
		_showcasePauseLeft = ShowcasePause;
		if (DebugAI)
			GD.Print($"[Artemisa] showcase: {move}");
		FaceTowardsPlayer();
		string prefix = _phaseTwo ? "e_" : "";
		switch (move)
		{
			case "thrust": _ = Thrust(healPunish: false); break;
			case "dance": _ = WaterfowlDance(); break;
			case "bash": _ = ShieldBash(); break;
			case "guard": _ = Guard(); break;
			case "grab": _ = Grab(); break;
			case "aeonia": _ = ScarletAeonia(); break;
			case "phantoms": _ = ScarletPhantoms(); break;
			case "flying": _ = FlyingSlash(); break;
			case "plunge": _ = ScarletPlunge(); break;
			case "rapid": _ = RapidSlashes(); break;
			case "slash1": _ = SpearAttack(prefix + "1_atk", "slash1"); break;
			case "slash2": _ = SpearAttack(prefix + "2_atk", "slash2"); break;
			default: _ = SpearAttack(prefix + "3_atk", "slash3"); break;
		}
	}

	private void Decide(Metroidvania.Player.Player player, float dt)
	{
		float distance = Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X);
		if (ShowcaseMode)
		{
			ShowcaseDecide(player, dt, distance);
			return;
		}
		bool healingNow = player.IsHealing && !_playerWasHealing;
		_playerWasHealing = player.IsHealing;
		_farTimer = distance > ThrustMinRange ? _farTimer + dt : 0f;

		// Malenia answers a flask at once, from almost anywhere.
		if ((healingNow || player.IsHealing) && distance <= HealPunishRange)
		{
			_ = Thrust(healPunish: true);
			return;
		}

		// He swings at her from the front: a reaction, not a plan — she may get the shield up (and,
		// if his blade arrives right then, parry it). The more he spams, the readier the shield.
		if (!player.IsAttacking)
			_guardRolledThisSwing = false;
		else if (!_guardRolledThisSwing && distance < GuardRange && _guardCooldownTimer <= 0f)
		{
			_guardRolledThisSwing = true;
			bool inFront = (player.GlobalPosition.X >= GlobalPosition.X) == FacingRight;
			if (inFront && _rng.Randf() < 0.3f + _attackHabit * 0.12f)
			{
				_ = Guard();
				return;
			}
		}

		bool evading = player.IsDashing || player.IsRolling;
		// Never throw into i-frames: wait for the dodge to finish, then the exit gets punished.
		if (evading && distance < 260f)
			return;
		bool justEvaded = _dash.SinceDashEnded < 0.25f;
		bool whiffed = _whiff.JustWhiffed && distance < 190f;
		bool airborne = !player.IsOnFloor();
		bool parrySpent = player.IsParryRecovering && distance < AttackRange;

		// A whiffed swing or a spent parry is an opening: she takes it even mid-cooldown.
		bool opening = whiffed || parrySpent;
		if (_attackCooldownTimer > 0f && !(opening && _attackCooldownTimer < AttackCooldown * 0.7f))
			return;

		if (_phaseTwo && _aeoniaNext)
		{
			_aeoniaNext = false;
			_ = ScarletAeonia();
			return;
		}

		string best = null;
		float bestScore = float.MinValue;
		void Consider(string move, float score)
		{
			if (move == _lastMove)
				score -= 1.2f;
			else if (move == _secondLastMove)
				score -= 0.5f;
			score += _rng.Randf() * 0.8f;
			if (score > bestScore)
			{
				bestScore = score;
				best = move;
			}
		}

		if (_thrustCooldownTimer <= 0f && distance >= ThrustMinRange * 0.8f)
			Consider("thrust", 2f + (distance - ThrustMinRange) / 220f + _farTimer * 0.8f + (justEvaded ? 1f : 0f) - (airborne ? 1f : 0f));
		if (_danceCooldownTimer <= 0f && distance >= DanceMinRange && distance <= DanceMaxRange)
			Consider("dance", 2.1f + _hitsTakenRecently * 0.6f + (airborne ? 1f : 0f) + _evadeHabit * 0.2f);
		if (_phaseTwo)
		{
			// The goddess of rot's own moves: they favour distance and a player who keeps running.
			if (_aeoniaCooldownTimer <= 0f)
				Consider("aeonia", 2.2f + _farTimer * 0.5f + _hitsTakenRecently * 0.4f);
			if (_phantomsCooldownTimer <= 0f)
				Consider("phantoms", 2.0f + (distance > 300f ? 0.8f : 0f) + _evadeHabit * 0.3f);
			if (_flyingCooldownTimer <= 0f && distance >= FlyingSlashMinRange && distance <= FlyingSlashMaxRange)
				Consider("flying", 2.0f + (justEvaded ? 0.8f : 0f) + (airborne ? 0.6f : 0f));
			if (_plungeCooldownTimer <= 0f && distance <= PlungeMaxRange)
				Consider("plunge", 1.8f + _parryHabit * 0.4f + (distance > 200f ? 0.5f : 0f));
		}
		if (_grabCooldownTimer <= 0f && distance >= GrabMinRange && distance <= GrabMaxRange && !airborne)
			Consider("grab", 1.6f + _parryHabit * 0.6f + _attackHabit * 0.2f);
		if (!_phaseTwo && _bashCooldownTimer <= 0f && distance <= BashRange + 20f)
			Consider("bash", 1.8f + _parryHabit * 0.7f + _hitsTakenRecently * 0.5f + (parrySpent ? 1.5f : 0f));
		if (distance <= AttackRange)
		{
			Consider("rapid", 1.6f + _evadeHabit * 0.5f + (airborne ? -0.6f : 0f));
			Consider("slash1", 1.3f + (whiffed ? 1.4f : 0f) + (justEvaded ? 1f : 0f) + (parrySpent ? 1.2f : 0f));
			Consider("slash2", 1.4f + (airborne ? -0.4f : 0f));
			Consider("slash3", 1.0f + _parryHabit * 0.3f);
		}
		if (best is null)
			return;

		string prefix = _phaseTwo ? "e_" : "";
		switch (best)
		{
			case "thrust": _ = Thrust(healPunish: false); break;
			case "dance": _ = WaterfowlDance(); break;
			case "bash": _ = ShieldBash(); break;
			case "grab": _ = Grab(); break;
			case "aeonia": _ = ScarletAeonia(); break;
			case "phantoms": _ = ScarletPhantoms(); break;
			case "flying": _ = FlyingSlash(); break;
			case "plunge": _ = ScarletPlunge(); break;
			case "rapid": _ = RapidSlashes(); break;
			case "slash1": _ = SpearAttack(prefix + "1_atk", "slash1"); break;
			case "slash2": _ = SpearAttack(prefix + "2_atk", "slash2"); break;
			default: _ = SpearAttack(prefix + "3_atk", "slash3"); break;
		}
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_busy || _kneeling)
			return currentVelocityX;
		if (_hurtTimer > 0f)
			return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
		float toward = Mathf.Sign(distanceX);
		// While her next move recharges she waits just outside his reach instead of walking into his
		// sword: he has to come to her (or eat the thrust if he hangs back).
		float engage = _attackCooldownTimer > 0.25f ? 160f : EngageDistance;
		if (Mathf.Abs(distanceX) > engage && CanStepTo(toward))
			return toward * MoveSpeed;
		return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null || _busy || _kneeling)
			return;
		string anim = _hurtTimer > 0f ? "take_hit" : Mathf.Abs(velocity.X) > 5f ? "run" : "idle";
		if (_phaseTwo)
			anim = anim switch { "run" => "e_move", "take_hit" => "e_take_hit", _ => "e_idle" };
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	// ── Moves ───────────────────────────────────────────────────────────────────────────────

	// Malenia's slash chain: the pack's spear combos, played straight (fast, no tell — the quick
	// pressure between the big moves). Each stab can land; a dash still dodges.
	private async Task SpearAttack(string animation, string moveName)
	{
		if (Sprite is null || !Moves.TryGetValue(animation, out HitWindow[] windows) || !BeginMove(moveName))
			return;
		float dir = FacingRight ? 1f : -1f;
		Sprite.Play(animation);
		Cry();
		int active = -1;
		while (Sprite.Animation == animation && Sprite.IsPlaying())
		{
			int frame = Sprite.Frame;
			int live = System.Array.FindIndex(windows, w => frame >= w.From && frame <= w.To);
			if (live != active)
			{
				_spear.Deactivate();
				Velocity = new Vector2(0f, Velocity.Y);
				if (live >= 0)
				{
					float reach = windows[live].Reach * CurrentScale;
					SpearOn(reach);
					var target = PlayerReads.Find(this);
					if (target is not null && Mathf.Abs(target.GlobalPosition.X - GlobalPosition.X) > reach * 0.7f)
						Velocity = new Vector2(dir * LungeSpeed, Velocity.Y);
				}
				active = live;
			}
			if (!await Wait(0f))
				return;
		}
		await Recover(AttackRecovery);
	}

	// Rapid Slashes: she draws the spear back (tell, held longer while his parry is open), three quick
	// stabs, then the fourth after a pause — the one that catches a roll thrown on reflex.
	private readonly record struct Stab(int From, int To, float Reach);

	private async Task RapidSlashes()
	{
		if (Sprite is null || !BeginMove("rapid"))
			return;
		bool human = !_phaseTwo;
		string anim = human ? "3_atk" : "e_3_atk";
		Cry();
		if (!await PlayFrames(anim, human ? 15 : 0, human ? 18 : 2, human ? 14f : 12f))
			return;
		if (!await Tell(RapidTell, new Vector2(-30f, -40f), GleamColor, 0.5f))
			return;
		FaceTowardsPlayer();
		Stab[] quick = human
			? new[] { new Stab(19, 20, 131), new Stab(21, 22, 141), new Stab(23, 24, 128) }
			: new[] { new Stab(3, 5, 120), new Stab(6, 8, 134), new Stab(9, 10, 127) };
		if (!await PlayStabs(anim, quick, 20f))
			return;
		ShowFrame(anim, human ? 24 : 11);
		if (!await Tell(RapidDelay, new Vector2(40f, -14f), ScarletGlow, 0.35f))
			return;
		FaceTowardsPlayer();
		Stab last = human ? new Stab(25, 28, 123) : new Stab(12, 15, 141);
		if (!await PlayStabs(anim, new[] { last }, 14f))
			return;
		if (!await PlayFrames(anim, human ? 29 : 16, human ? 30 : 16, 12f))
			return;
		await Recover(AttackRecovery);
	}

	// Leaping thrust: the gap closer. Spear drawn back with a glint, then she crosses the arena in a
	// blink, spear first, trailing scarlet afterimages. Never launched into his dash i-frames: the
	// tell holds until the dodge ends, so it lands on the exit. As a heal punish the tell is short.
	private async Task Thrust(bool healPunish)
	{
		if (Sprite is null || !BeginMove("thrust"))
			return;
		_thrustCooldownTimer = ThrustCooldown;
		bool human = !_phaseTwo;
		string anim = human ? "dash" : "e_air_atk";
		Cry();
		if (!await PlayFrames(anim, 0, human ? 1 : 2, 12f))
			return;
		if (!await Tell(healPunish ? HealPunishTell : ThrustTell, new Vector2(-50f, -20f), GleamColor, healPunish ? 0.05f : 0.5f))
			return;

		var player = PlayerReads.Find(this);
		FaceTowardsPlayer();
		float dir = FacingRight ? 1f : -1f;
		float startX = GlobalPosition.X;
		float travel = player is null
			? ThrustMaxDistance
			: Mathf.Clamp(Mathf.Abs(player.GlobalPosition.X - startX) + 90f, 120f, ThrustMaxDistance);
		float reach = human ? ThrustReach : ThrustReach * 1.25f;
		float duration = travel / ThrustSpeed;
		SpearOn(reach, 60f, trail: false);
		_trail.PlayThrust(new Vector2(-40f, -12f), new Vector2(reach + 22f, -12f), 0.05f, duration);
		SetMoveLight(ScarletGlow, 1.8f);
		player?.ShakeCamera(3f, 0.15f);

		float elapsed = 0f;
		float frameTimer = 0f;
		int frame = human ? 2 : 3;
		while (Mathf.Abs(GlobalPosition.X - startX) < travel && elapsed < duration + 0.3f)
		{
			Velocity = new Vector2(dir * ThrustSpeed, Velocity.Y);
			frameTimer -= (float)GetPhysicsProcessDeltaTime();
			if (frameTimer <= 0f)
			{
				frameTimer = 1f / 14f;
				ShowFrame(anim, frame);
				frame = human ? (frame >= 7 ? 2 : frame + 1) : (frame >= 4 ? 3 : frame + 1);
			}
			Afterimage(ScarletGlow);
			if (!await Wait(0f))
				return;
			elapsed += (float)GetPhysicsProcessDeltaTime();
			if (IsOnWall())
				break;
		}
		_spear.Deactivate();
		SetMoveLight(ScarletGlow, 0f);
		Velocity = new Vector2(dir * ThrustSpeed * 0.25f, Velocity.Y);
		if (!await PlayFrames(anim, human ? 8 : 5, human ? 12 : 7, 14f))
			return;
		await Recover(healPunish ? 0.4f : 0.55f);
	}

	// Shield bash — her version of Malenia's kick: a short hop shoulder-first into the shield. It goes
	// through a parry (the answer to a player who parries everything), so its tell is scarlet.
	private async Task ShieldBash()
	{
		if (Sprite is null || !BeginMove("bash"))
			return;
		_bashCooldownTimer = BashCooldown;
		if (!await PlayFrames("air_atk", 0, 2, 14f))
			return;
		if (!await Tell(BashTell, new Vector2(30f, -30f), ScarletGlow, 0f))
			return;
		FaceTowardsPlayer();
		float dir = FacingRight ? 1f : -1f;
		Sfx.PlayAt(this, "Combat/Sword", "Sword Blocked", 2000f);
		_trail.Play(new Vector2(20f, -30f), 70f, -70f, 70f, 0.14f, 55f);
		bool landed = false;
		for (int f = 3; f <= 6; f++)
		{
			ShowFrame("air_atk", f);
			Velocity = new Vector2(f <= 5 ? dir * BashStepSpeed : 0f, Velocity.Y);
			float frameElapsed = 0f;
			while (frameElapsed < 1f / 14f)
			{
				var player = PlayerReads.Find(this);
				if (!landed && f <= 5 && player is not null)
				{
					Vector2 rel = player.GlobalPosition - GlobalPosition;
					if (rel.X * dir > -10f && rel.X * dir < 73f * CurrentScale && Mathf.Abs(rel.Y) < 60f
						&& HitUnparryable(player, 1f, GlobalPosition, BashKnockback))
					{
						landed = true;
						LifestealFrom(Mathf.RoundToInt(Stats.AttackPower));
						player.ShakeCamera(6f, 0.2f);
					}
				}
				if (!await Wait(0f))
					return;
				frameElapsed += (float)GetPhysicsProcessDeltaTime();
			}
		}
		await Recover(0.5f);
	}

	// Waterfowl Dance. Crouch with petals drawn in (tell), leap, hang in the air, then three homing
	// bursts of scarlet blades, the body itself turned spear-first along each one with afterimages;
	// the last burst chases him across the arena. One dash per burst, timed as it reaches him (burst
	// + gap = 1.0 s against his 0.78 s dash cycle). Lands open.
	private async Task WaterfowlDance()
	{
		if (Sprite is null || !BeginMove("dance"))
			return;
		_danceCooldownTimer = DanceCooldown;
		bool human = !_phaseTwo;
		var player = PlayerReads.Find(this);
		if (player is null)
		{
			await Recover(0.2f);
			return;
		}
		ShowFrame(human ? "jump" : "e_up_loop", 0);
		Sfx.PlayIfExistsAt(this, VoiceCategory, "Ataque", 2000f);
		SpawnPetals(GlobalPosition + new Vector2(0f, -30f), 40, 220f, DanceTell, inward: true);
		SetMoveLight(ScarletGlow, 1.4f);
		if (!await Tell(DanceTell, new Vector2(0f, -70f), ScarletGlow, 0.3f))
			return;

		float dir = FacingRight ? 1f : -1f;
		Velocity = new Vector2(dir * DanceLeap.X, DanceLeap.Y);
		Sfx.PlayAt(this, "Footsteps/Stone", "Stone Chain Jump", 2000f);
		int rise = 1;
		while (Velocity.Y < 0f)
		{
			ShowFrame(human ? "jump" : "e_up_loop", human ? Mathf.Min(rise / 3 + 1, 7) : (rise / 4) % 3);
			rise++;
			if (!await Wait(0f))
				return;
		}
		Gravity = 0f;
		Velocity = Vector2.Zero;
		if (!await Wait(0.15f))
			return;

		string lungeAnim = human ? "dash_loop" : "e_air_atk";
		for (int burst = 0; burst < DanceBursts; burst++)
		{
			player = PlayerReads.Find(this);
			Vector2 target = player is null ? GlobalPosition : player.GlobalPosition + new Vector2(0f, -10f);
			Vector2 direction = (target - GlobalPosition).Normalized();
			direction.Y = Mathf.Max(direction.Y, -0.3f);
			FaceTowards(target.X);
			bool finalBurst = burst == DanceBursts - 1;
			float speed = finalBurst ? DanceFinalBurstSpeed : DanceBurstSpeed;
			float maxDuration = finalBurst ? DanceFinalBurstMaxTime : DanceBurstDuration;

			// Phase two dances with her slashing combo (e_3_atk's striking frames, looped fast)
			// instead of a held pose rotated along the burst.
			ShowFrame(lungeAnim, human ? 0 : 4);
			if (!human)
				ShowFrame("e_3_atk", DanceComboFirst);
			Velocity = direction.Normalized() * speed;
			if (human)
				AimSpriteAlong(Velocity);
			_flurry.Play();
			SpawnPetals(GlobalPosition + new Vector2(0f, -20f), 26, 120f, 0.5f, inward: false);
			Sfx.PlayAt(this, "Combat/Sword", "Sword Attack", 2000f);
			SetMoveLight(ScarletGlow, 2f);
			_danceHitbox.Activate(Stats, ignorePostHitInvulnerability: true);

			float burstElapsed = 0f;
			bool dodged = false;
			bool reached = false;
			bool secondFlurry = false;
			while (burstElapsed < maxDuration && !(finalBurst && reached && burstElapsed >= DanceBurstDuration))
			{
				if (!await Wait(0f))
					return;
				float step = (float)GetPhysicsProcessDeltaTime();
				burstElapsed += step;
				if (!human)
				{
					int span = DanceComboLast - DanceComboFirst + 1;
					ShowFrame("e_3_atk", DanceComboFirst + (int)(burstElapsed * DanceComboFps) % span);
				}
				Afterimage(ScarletGlow);
				if (!secondFlurry && burstElapsed >= DanceBurstDuration * 0.5f)
				{
					// Second wave of blades mid-burst: the "tra-tra-tra" reads denser than the mushroom's.
					secondFlurry = true;
					_flurry.Play();
					Sfx.PlayAt(this, "Combat/Sword", "Sword Attack", 2000f);
				}
				player = PlayerReads.Find(this);
				if (player is null)
					continue;
				Vector2 toPlayer = player.GlobalPosition + new Vector2(0f, -10f) - GlobalPosition;
				if (player.IsDashing && toPlayer.Length() < DanceDodgeCommitDistance)
				{
					if (!dodged)
						_evadeHabit += 0.35f;
					dodged = true;
				}
				if (toPlayer.Length() <= DanceHitRadius)
					reached = true;
				if (dodged || toPlayer.Length() < 8f)
					continue;
				float maxTurn = DanceHomingTurnRate * step;
				float turn = Mathf.Clamp(Velocity.AngleTo(toPlayer), -maxTurn, maxTurn);
				Velocity = Velocity.Rotated(turn).Normalized() * speed;
				FaceTowards(player.GlobalPosition.X);
				if (human)
					AimSpriteAlong(Velocity);
			}
			_danceHitbox.Deactivate();
			SetMoveLight(ScarletGlow, 0.5f);
			Velocity = Vector2.Zero;
			Sprite.Rotation = 0f;
			ShowFrame(human ? "j_up_loop" : "e_up_loop", 0);

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

		Gravity = _baseGravity;
		SetMoveLight(ScarletGlow, 0f);
		ShowFrame(human ? "j_down_loop" : "e_down_loop", 0);
		if (!await WaitUntilLanded(2f))
			return;
		Sfx.PlayAt(this, "Footsteps/Stone", "Stone Chain Land", 2000f);
		SpawnDust(GlobalPosition + new Vector2(0f, 40f));
		await Recover(DanceRecovery + (_phaseTwo ? PhaseTwoOpening : 0f));
	}

	// ── Shield guard, parry and riposte ─────────────────────────────────────────────────────

	// The shield comes up against a swing from the front. Hits from the front are blocked (clang,
	// sparks, she gives a little ground); one landing in the first GuardParryWindow is parried — he
	// reels and she ripostes at once. Hits from behind go through: the answer is to get round her.
	private async Task Guard()
	{
		if (!BeginMove("guard"))
			return;
		_guardCooldownTimer = GuardCooldown;
		string anim = _phaseTwo ? "e_defend" : "defend";
		ShowFrame(anim, 1);
		Sfx.PlayAt(this, "Combat/Sword", "Sword Sheath", 2000f);
		_guarding = true;
		_guardTimer = 0f;
		_guardBlocks = 0;
		_parriedThisGuard = false;
		// Not every guard is a parry: she commits to reading his timing on some (more against a
		// player who spams), and on the rest she just blocks.
		_guardGoesForParry = _rng.Randf() < Mathf.Min(0.6f, 0.25f + _attackHabit * 0.09f);
		while (_guardTimer < GuardMaxTime && !_parriedThisGuard && _guardBlocks < 2)
		{
			if (!await Wait(0f))
			{
				_guarding = false;
				return;
			}
			_guardTimer += (float)GetPhysicsProcessDeltaTime();
			ShowFrame(anim, Mathf.Clamp(1 + (int)(_guardTimer * 10f), 1, 5));
		}
		_guarding = false;
		if (_parriedThisGuard)
		{
			await Riposte();
			return;
		}
		if (!await PlayFrames(anim, 6, 7, 14f))
			return;
		// Blocked twice and still standing: she answers straight away.
		_attackCooldownTimer = _guardBlocks > 0 ? 0.15f : AttackCooldown * 0.5f;
		await Recover(0.15f);
	}

	// Stats.IncomingHitInterceptor: true = the hit is stopped (no damage, no HitTaken).
	private bool InterceptHit()
	{
		if (!_guarding || Enemy.DealingContactDamage)
			return false;
		var player = PlayerReads.Find(this);
		if (player is null)
			return false;
		// The shield only covers the front.
		if ((player.GlobalPosition.X >= GlobalPosition.X) != FacingRight)
			return false;
		float dir = FacingRight ? 1f : -1f;
		Vector2 shield = GlobalPosition + new Vector2(dir * 30f, -30f);
		if (_guardGoesForParry && _guardTimer <= GuardParryWindow && !_parriedThisGuard)
		{
			_parriedThisGuard = true;
			_attackHabit = Mathf.Max(0f, _attackHabit - 0.5f);
			if (DebugAI)
				GD.Print("[Artemisa] PARRY");
			Sfx.PlayAt(this, "Combat/Sword", "Sword Parry", 2000f);
			Glint(new Vector2(30f, -30f), GleamColor, 2.2f);
			SpawnPetals(shield, 30, 120f, 0.5f, inward: false);
			player.Snare(ParryStunTime);
			player.ApplyKnockbackThroughBlock(new Vector2(dir, -0.25f).Normalized(), ParryKnockback);
			player.ShakeCamera(8f, 0.25f);
			ScreenFlash(GleamColor, 0.18f);
			return true;
		}
		_guardBlocks++;
		if (DebugAI)
			GD.Print("[Artemisa] BLOCK");
		Sfx.PlayAt(this, "Combat/Sword", "Sword Blocked", 2000f);
		Glint(new Vector2(30f, -30f), GleamColor, 1.2f);
		Velocity = new Vector2(-dir * 140f, Velocity.Y);
		player.ApplyKnockback(new Vector2(dir, 0f), 160f);
		return true;
	}

	// After a parry: one heavy, fast stab while he's off balance.
	private async Task Riposte()
	{
		_lastMove = "riposte";
		FaceTowardsPlayer();
		bool human = !_phaseTwo;
		string anim = human ? "1_atk" : "e_1_atk";
		Sfx.PlayIfExistsAt(this, VoiceCategory, "Ataque", 2000f);
		Glint(new Vector2(40f, -14f), ScarletGlow, 1.4f);
		if (!await PlayFrames(anim, human ? 1 : 1, 2, 18f))
			return;
		float dir = FacingRight ? 1f : -1f;
		float reach = (human ? 104f : 134f) * CurrentScale;
		var player = PlayerReads.Find(this);
		if (player is not null && Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X) > reach * 0.7f)
			Velocity = new Vector2(dir * LungeSpeed * 2f, Velocity.Y);
		SpearOn(reach, damageMultiplier: RiposteDamageMultiplier);
		if (!await PlayFrames(anim, 3, human ? 6 : 8, 16f))
			return;
		await Recover(0.45f);
	}

	// ── Grab: impale and slam ───────────────────────────────────────────────────────────────

	// Malenia's grab: a step back with the arm drawn (long scarlet tell — a grab can't be parried,
	// only dodged), then a lunge. Caught: she lifts him impaled on the spear, stabs three times
	// (each one drains him), then slams him into the floor. Missed: she's left wide open.
	private async Task Grab()
	{
		if (!BeginMove("grab"))
			return;
		_grabCooldownTimer = GrabCooldown;
		bool human = !_phaseTwo;
		string anim = human ? "dash" : "e_air_atk";
		float dir = FacingRight ? 1f : -1f;
		ShowFrame(anim, human ? 1 : 2);
		Sfx.PlayIfExistsAt(this, VoiceCategory, "Ataque", 2000f);
		Velocity = new Vector2(-dir * 220f, Velocity.Y);
		if (!await Wait(0.15f))
			return;
		Velocity = new Vector2(0f, Velocity.Y);
		if (!await Tell(GrabTell, new Vector2(-30f, -40f), ScarletGlow, 0.4f))
			return;

		FaceTowardsPlayer();
		dir = FacingRight ? 1f : -1f;
		ShowFrame(anim, human ? 2 : 3);
		_trail.PlayThrust(new Vector2(-30f, -12f), new Vector2(GrabReach + 10f, -12f), 0.04f, GrabLungeTime);
		SetMoveLight(ScarletGlow, 1.6f);
		Metroidvania.Player.Player caught = null;
		float elapsed = 0f;
		while (elapsed < GrabLungeTime)
		{
			Velocity = new Vector2(dir * GrabLungeSpeed, Velocity.Y);
			Afterimage(ScarletGlow);
			if (!await Wait(0f))
				return;
			elapsed += (float)GetPhysicsProcessDeltaTime();
			var player = PlayerReads.Find(this);
			if (player?.GetNodeOrNull<Stats>("Stats") is { } stats && !stats.IsInvulnerable)
			{
				Vector2 rel = player.GlobalPosition - GlobalPosition;
				if (rel.X * dir > -10f && rel.X * dir < GrabReach && Mathf.Abs(rel.Y) < 60f)
				{
					caught = player;
					break;
				}
			}
			if (IsOnWall())
				break;
		}
		Velocity = new Vector2(0f, Velocity.Y);
		SetMoveLight(ScarletGlow, 0f);
		if (caught is null)
		{
			// Whiffed: the long, open recovery is the reward for dodging it.
			if (!await PlayFrames(anim, human ? 8 : 5, human ? 12 : 7, 12f))
				return;
			await Recover(0.8f);
			return;
		}
		await ImpaleAndSlam(caught, human, dir);
	}

	private async Task ImpaleAndSlam(Metroidvania.Player.Player player, bool human, float dir)
	{
		player.Atrapado = true;
		player.Snare(6f);
		try
		{
			Sfx.PlayAt(this, "Combat/Sword", "Sword Impact Hit", 2000f);
			BloodEffect.SpawnAt(this, player.GlobalPosition);
			player.ShakeCamera(7f, 0.25f);
			string hold = human ? "projectile_atk" : "e_idle";
			// Held up on the spear, above and a little behind her head.
			Vector2 start = player.GlobalPosition;
			Vector2 held = GlobalPosition + new Vector2(-dir * 10f, -125f * SizeFactor);
			float tickEvery = 0.3f;
			float liftTime = 0.25f;
			float total = liftTime + tickEvery * GrabTicks + 0.15f;
			float elapsed = 0f;
			int ticks = 0;
			while (elapsed < total)
			{
				if (!IsInstanceValid(player) || !await Wait(0f))
					return;
				elapsed += (float)GetPhysicsProcessDeltaTime();
				ShowFrame(hold, human ? Mathf.Clamp(2 + (int)(elapsed * 10f), 2, 6) : (int)(elapsed * 8f) % 14);
				player.GlobalPosition = start.Lerp(held, Mathf.Clamp(elapsed / liftTime, 0f, 1f));
				player.Velocity = Vector2.Zero;
				if (ticks < GrabTicks && elapsed >= liftTime + tickEvery * (ticks + 1) - 0.1f)
				{
					ticks++;
					DamageHeld(player, GrabTickMultiplier);
					BloodEffect.SpawnAt(this, player.GlobalPosition);
					Sfx.PlayAt(this, "Combat/Sword", "Sword Impact Hit", 2000f);
					SpawnPetals(player.GlobalPosition, 12, 70f, 0.4f, inward: false);
					player.ShakeCamera(4f, 0.12f);
				}
			}

			// The throw: a short hitch higher up (she gathers him over her head), then hurled down in
			// front of her, accelerating into the floor — he's thrown, not set down.
			if (human)
				ShowFrame(hold, 8);
			Vector2 hitchFrom = player.GlobalPosition;
			Vector2 hitch = hitchFrom + new Vector2(-dir * 12f, -34f);
			float t = 0f;
			while (t < 0.1f)
			{
				if (!IsInstanceValid(player) || !await Wait(0f))
					return;
				t += (float)GetPhysicsProcessDeltaTime();
				player.GlobalPosition = hitchFrom.Lerp(hitch, Mathf.Ease(Mathf.Clamp(t / 0.1f, 0f, 1f), 0.4f));
				player.Velocity = Vector2.Zero;
			}
			Vector2 slamPoint = new(GlobalPosition.X + dir * 80f, GlobalPosition.Y);
			Vector2 from = player.GlobalPosition;
			float slam = 0f;
			while (slam < 0.09f)
			{
				if (!IsInstanceValid(player) || !await Wait(0f))
					return;
				slam += (float)GetPhysicsProcessDeltaTime();
				// Ease-in: slow off the top, fastest at the floor.
				float k = Mathf.Clamp(slam / 0.09f, 0f, 1f);
				player.GlobalPosition = from.Lerp(slamPoint, k * k);
				player.Velocity = Vector2.Zero;
			}
			if (human)
				ShowFrame(hold, 9);
			DamageHeld(player, GrabSlamMultiplier);
			BloodEffect.SpawnAt(this, player.GlobalPosition);
			BloodEffect.SpawnAt(this, player.GlobalPosition + new Vector2(dir * 14f, -6f));
			Sfx.PlayAt(this, "Footsteps/Stone", "Stone Chain Land", 2000f, RiseVolumeDb);
			Sfx.PlayAt(this, "Combat/Sword", "Sword Impact Hit", 2000f);
			Vector2 ground = slamPoint + new Vector2(0f, 36f);
			SlamImpact(player, ground, dir);
		}
		finally
		{
			if (IsInstanceValid(player))
			{
				player.Atrapado = false;
				player.ReleaseSnare();
				// Bounced off the floor and sent tumbling away, low and far.
				player.ApplyKnockbackThroughBlock(new Vector2(dir, -0.55f).Normalized(), 560f);
			}
		}
		await Recover(0.7f);
	}

	// Damage that ignores parry and i-frames (he's held on the spear), and drains into her.
	private void DamageHeld(Metroidvania.Player.Player player, float multiplier)
	{
		if (player.GetNodeOrNull<Stats>("Stats") is not { } stats)
			return;
		System.Func<bool> interceptor = stats.IncomingHitInterceptor;
		stats.IncomingHitInterceptor = null;
		int before = stats.CurrentHealth;
		stats.TakeDamage(Mathf.RoundToInt(Stats.AttackPower * multiplier), ignoreInvulnerability: true, armInvulnerability: false);
		stats.IncomingHitInterceptor = interceptor;
		LifestealFrom(before - stats.CurrentHealth);
	}

	// ── Phase 2: Goddess of Rot ─────────────────────────────────────────────────────────────

	// Scarlet Aeonia. She rises wreathed in scarlet tendrils and hangs there tracking him (the
	// tell), dives onto him, and where she lands a giant scarlet flower unfurls. The ring on the
	// ground shows the blast; after AeoniaBloomDelay it blooms — an unparryable explosion — and
	// leaves a rot cloud that poisons. Answer: run sideways out of the ring before it blooms.
	private async Task ScarletAeonia()
	{
		if (!BeginMove("aeonia"))
			return;
		_aeoniaCooldownTimer = AeoniaCooldown;
		Tremble(ChargeTremble, 0.45f + AeoniaHover + 1.4f);
		Sfx.PlayIfExistsAt(this, VoiceCategory, "Ataque", 2000f);
		Sfx.PlayAt(this, "Magic", "Firebuff", 2000f);
		var tendrils = new Tendriles { Color = RotColor, Position = new Vector2(0f, -40f) };
		AddChild(tendrils);
		SetMoveLight(RotColor, 1.8f);

		// Rise.
		Gravity = 0f;
		float rise = 0f;
		while (rise < 0.45f)
		{
			Velocity = new Vector2(0f, -AeoniaRiseHeight / 0.45f);
			ShowFrame("e_up_loop", (int)(rise * 10f) % 3);
			if (!await Wait(0f))
				return;
			rise += (float)GetPhysicsProcessDeltaTime();
		}

		// Hover, drifting over him, petals drawn in: the tell.
		float hover = 0f;
		float petalTimer = 0f;
		Sprite.Play("e_sp_atk_loop");
		while (hover < AeoniaHover)
		{
			var player = PlayerReads.Find(this);
			float toward = player is null ? 0f : Mathf.Clamp(player.GlobalPosition.X - GlobalPosition.X, -260f, 260f);
			Velocity = new Vector2(toward * 2.2f, 0f);
			if (player is not null)
				FaceTowards(player.GlobalPosition.X);
			petalTimer -= (float)GetPhysicsProcessDeltaTime();
			if (petalTimer <= 0f)
			{
				petalTimer = 0.2f;
				SpawnPetals(GlobalPosition + new Vector2(0f, -40f), 10, 150f, 0.5f, inward: true);
			}
			if (!await Wait(0f))
				return;
			hover += (float)GetPhysicsProcessDeltaTime();
		}

		// Dive.
		var target = PlayerReads.Find(this);
		Vector2 aim = target?.GlobalPosition ?? GlobalPosition + Vector2.Down * 300f;
		FaceTowards(aim.X);
		ShowFrame("e_down_loop", 0);
		Velocity = (aim - GlobalPosition).Normalized() * AeoniaDiveSpeed;
		if (Velocity.Y < 200f)
			Velocity = new Vector2(Velocity.X, 200f);
		float dive = 0f;
		while (!IsOnFloor() && dive < 1.4f)
		{
			Afterimage(RotColor);
			ShowFrame("e_down_loop", (int)(dive * 14f) % 3);
			if (!await Wait(0f))
				return;
			dive += (float)GetPhysicsProcessDeltaTime();
		}
		tendrils.Fade();
		Gravity = _baseGravity;
		Velocity = Vector2.Zero;
		SetMoveLight(RotColor, 0.6f);
		// Landing: a harder shudder that settles while the flower opens.
		Tremble(ChargeTremble * 1.4f, AeoniaBloomDelay);
		Vector2 ground = GlobalPosition + new Vector2(0f, 38f);
		HitPlayerAt(GlobalPosition + new Vector2(0f, -10f), AeoniaImpactRadius, 1.4f, false, 320f);
		Sfx.PlayAt(this, "Footsteps/Stone", "Stone Chain Land", 2000f, RiseVolumeDb);
		PlayerReads.Find(this)?.ShakeCamera(9f, 0.3f);
		SpawnDust(ground + new Vector2(0f, 2f));
		SpawnRing(GlobalPosition, AeoniaImpactRadius + 20f);

		// The flower.
		if (GetTree().CurrentScene is Node scene)
		{
			var flower = new FlorAeonia { Boss = this, Radius = AeoniaBloomRadius, Delay = AeoniaBloomDelay, Color = RotColor };
			scene.AddChild(flower);
			flower.GlobalPosition = ground;
		}
		ShowFrame("e_idle", 0);
		if (!await Exhausted(AeoniaBloomDelay + AeoniaOpening))
			return;
		await Recover(0.1f);
	}

	// Scarlet Phantoms. She rises over a pool of rot and, one after another, phantoms of herself
	// take shape beside him (the tell: they fade in and glint), then lunge through where he is and
	// vanish. After the last one she dives onto him herself.
	private async Task ScarletPhantoms()
	{
		if (!BeginMove("phantoms"))
			return;
		_phantomsCooldownTimer = PhantomsCooldown;
		Tremble(ChargeTremble * 0.7f, 0.4f + PhantomCount * PhantomInterval + PhantomTell + 0.6f);
		Sfx.PlayIfExistsAt(this, VoiceCategory, "Ataque", 2000f);
		SetMoveLight(RotColor, 1.5f);

		// A rot pool below her for the whole move: standing under her poisons.
		if (GetTree().CurrentScene is Node scene)
		{
			var pool = new NubePodrida { Boss = this, Radius = 120f, Duration = PhantomCount * PhantomInterval + 1.6f, Color = RotColor };
			scene.AddChild(pool);
			pool.GlobalPosition = GlobalPosition + new Vector2(0f, 38f);
		}

		Gravity = 0f;
		float rise = 0f;
		while (rise < 0.4f)
		{
			Velocity = new Vector2(0f, -220f / 0.4f);
			ShowFrame("e_sp_atk", Mathf.Clamp((int)(rise * 14f), 0, 5));
			if (!await Wait(0f))
				return;
			rise += (float)GetPhysicsProcessDeltaTime();
		}
		Velocity = Vector2.Zero;
		Sprite.Play("e_sp_atk_loop");

		float side = PlayerReads.Find(this) is { } first && first.GlobalPosition.X < GlobalPosition.X ? -1f : 1f;
		for (int i = 0; i < PhantomCount; i++)
		{
			_ = Phantom(side);
			side = -side;
			if (!await Wait(PhantomInterval))
				return;
		}
		if (!await Wait(PhantomTell + 0.35f))
			return;

		// The dive that closes it.
		var player = PlayerReads.Find(this);
		Vector2 aim = player?.GlobalPosition ?? GlobalPosition + Vector2.Down * 220f;
		FaceTowards(aim.X);
		Glint(new Vector2(0f, -40f), RotColor, 1.4f);
		if (!await Wait(0.25f))
			return;
		ShowFrame("e_down_loop", 0);
		Velocity = (aim - GlobalPosition).Normalized() * AeoniaDiveSpeed;
		if (Velocity.Y < 200f)
			Velocity = new Vector2(Velocity.X, 200f);
		float dive = 0f;
		while (!IsOnFloor() && dive < 1.2f)
		{
			Afterimage(RotColor);
			if (!await Wait(0f))
				return;
			dive += (float)GetPhysicsProcessDeltaTime();
		}
		Gravity = _baseGravity;
		Velocity = Vector2.Zero;
		HitPlayerAt(GlobalPosition + new Vector2(0f, -10f), 85f, 1.3f, false, 300f);
		Sfx.PlayAt(this, "Magic", "Spell Impact", 2000f);
		PlayerReads.Find(this)?.ShakeCamera(8f, 0.25f);
		SpawnRing(GlobalPosition, 100f);
		SpawnDust(GlobalPosition + new Vector2(0f, 40f));
		SetMoveLight(RotColor, 0f);
		await Recover(0.8f + PhaseTwoOpening);
	}

	// One phantom: it tears out of her own body (as Malenia's do), drifts off to one side of her
	// while it aims at him, then lunges all the way through where he stands and fades.
	private async Task Phantom(float side)
	{
		var player = PlayerReads.Find(this);
		if (player is null || GetTree().CurrentScene is not Node scene || Sprite?.SpriteFrames is null)
			return;
		Vector2 body = GlobalPosition + new Vector2(0f, -20f);
		// Where it settles beside her before lunging (a little uneven each time).
		Vector2 perch = body + new Vector2(side * _rng.RandfRange(55f, 85f), _rng.RandfRange(-35f, 5f));
		var ghost = new Sprite2D
		{
			Texture = Sprite.SpriteFrames.GetFrameTexture("e_air_atk", 3),
			Material = Metroidvania.Player.NeonSilhouette.CreateMaterial(RotColor, 1.6f),
			Modulate = new Color(1f, 1f, 1f, 0.9f),
			Scale = new Vector2(CurrentScale, CurrentScale),
			ZIndex = 6,
		};
		scene.AddChild(ghost);
		ghost.GlobalPosition = body;
		Sfx.PlayAt(this, "Magic", "Firebuff", 2000f);
		SpawnPetals(body, 14, 50f, 0.4f, inward: false);
		FlashBody(RotColor, 0.15f);

		// Tell: it peels away from her (leaving copies of itself on the way) and keeps re-aiming
		// at him until the lunge.
		float tell = 0f;
		float copyTimer = 0f;
		Vector2 direction = new(-side, 0f);
		while (tell < PhantomTell)
		{
			if (!IsInsideTree() || !await Wait(0f) || !IsInstanceValid(ghost))
			{
				if (IsInstanceValid(ghost))
					ghost.QueueFree();
				return;
			}
			float step = (float)GetPhysicsProcessDeltaTime();
			tell += step;
			float peel = Mathf.Ease(Mathf.Clamp(tell / (PhantomTell * 0.55f), 0f, 1f), 0.35f);
			ghost.GlobalPosition = body.Lerp(perch, peel);
			copyTimer -= step;
			if (peel < 1f && copyTimer <= 0f)
			{
				copyTimer = 0.03f;
				PhantomCopy(ghost, scene);
			}
			player = PlayerReads.Find(this);
			if (player is not null)
			{
				direction = (player.GlobalPosition + new Vector2(0f, -30f) - ghost.GlobalPosition).Normalized();
				float facing = direction.X >= 0f ? 1f : -1f;
				ghost.Scale = new Vector2(facing * CurrentScale, CurrentScale);
				ghost.Rotation = facing > 0f ? direction.Angle() : direction.Angle() - Mathf.Pi;
			}
		}
		Sfx.PlayAt(this, "Magic", "Wave Attack", 2000f);
		Vector2 start = ghost.GlobalPosition - new Vector2(0f, -20f);
		// Long enough to pass clean through him, wherever he is.
		float length = player is null ? PhantomLungeLength
			: Mathf.Max(PhantomLungeLength, start.DistanceTo(player.GlobalPosition) + 140f);
		Vector2 end = start + direction * length;
		float lungeTime = 0.14f * length / Mathf.Max(1f, PhantomLungeLength);
		// The trail is the phantom itself: neon copies of her silhouette left behind along the
		// lunge, each fading on its own, with rot petals shed along the way — no drawn line.
		bool hit = false;
		float lunge = 0f;
		copyTimer = 0f;
		while (lunge < lungeTime)
		{
			if (!IsInsideTree() || !await Wait(0f) || !IsInstanceValid(ghost))
			{
				if (IsInstanceValid(ghost))
					ghost.QueueFree();
				return;
			}
			float dt = (float)GetPhysicsProcessDeltaTime();
			lunge += dt;
			Vector2 at = start.Lerp(end, Mathf.Clamp(lunge / lungeTime, 0f, 1f));
			ghost.GlobalPosition = at + new Vector2(0f, -20f);
			copyTimer -= dt;
			if (copyTimer <= 0f)
			{
				copyTimer = 0.018f;
				PhantomCopy(ghost, scene);
				SpawnPetals(at + new Vector2(0f, -20f), 3, 26f, 0.5f, inward: false);
			}
			player = PlayerReads.Find(this);
			if (!hit && player is not null && DistanceToSegment(player.GlobalPosition + new Vector2(0f, -10f), start, at) < 42f)
				hit = HitPlayerAt(player.GlobalPosition, 1f, PhantomMultiplier, false, 260f);
		}
		Tween fade = ghost.CreateTween();
		fade.TweenProperty(ghost, "modulate:a", 0f, 0.25f);
		fade.TweenCallback(Callable.From(ghost.QueueFree));
	}

	// One fading neon copy of a phantom where it is right now, nudged a little off its path so
	// the trail frays instead of reading as a solid bar.
	private void PhantomCopy(Sprite2D phantom, Node scene)
	{
		var copy = new Sprite2D
		{
			Texture = phantom.Texture,
			Material = phantom.Material,
			Scale = phantom.Scale,
			Rotation = phantom.Rotation,
			Modulate = new Color(1f, 1f, 1f, _rng.RandfRange(0.35f, 0.6f)),
			ZIndex = phantom.ZIndex - 1,
		};
		scene.AddChild(copy);
		copy.GlobalPosition = phantom.GlobalPosition + new Vector2(_rng.RandfRange(-4f, 4f), _rng.RandfRange(-5f, 5f));
		Tween fade = copy.CreateTween();
		fade.TweenProperty(copy, "modulate:a", 0f, _rng.RandfRange(0.2f, 0.4f));
		fade.TweenCallback(Callable.From(copy.QueueFree));
	}

	// Flying Slash: a leap at him, a wide sweeping arc at the top, then a thrust diving down-forward.
	private async Task FlyingSlash()
	{
		if (!BeginMove("flying"))
			return;
		_flyingCooldownTimer = FlyingSlashCooldown;
		Cry();
		ShowFrame("e_up_loop", 0);
		if (!await Tell(0.3f, new Vector2(-30f, -50f), RotColor, 0.4f))
			return;
		var player = PlayerReads.Find(this);
		FaceTowardsPlayer();
		float dir = FacingRight ? 1f : -1f;
		float dx = player is null ? dir * 200f : player.GlobalPosition.X - dir * 80f - GlobalPosition.X;
		const float airTime = 0.5f;
		Velocity = new Vector2(dx / airTime, -Gravity * airTime * 0.5f);
		Sfx.PlayAt(this, "Footsteps/Stone", "Stone Chain Jump", 2000f);
		float t = 0f;
		while (Velocity.Y < -40f && t < 1f)
		{
			ShowFrame("e_up_loop", (int)(t * 12f) % 3);
			if (!await Wait(0f))
				return;
			t += (float)GetPhysicsProcessDeltaTime();
		}

		// The sweep: hang a beat, then a wide scarlet arc around her.
		Gravity = 0f;
		Velocity = Vector2.Zero;
		FaceTowardsPlayer();
		dir = FacingRight ? 1f : -1f;
		Sfx.PlayAt(this, "Combat/Sword", "Sword Attack", 2000f);
		_trail.Play(new Vector2(0f, -20f), 130f, -130f, 70f, 0.18f, 110f);
		for (int f = 12; f <= 15; f++)
		{
			ShowFrame("e_3_atk", f);
			HitPlayerAt(GlobalPosition + new Vector2(dir * 60f, -10f), 120f, 1.2f, false, 300f);
			if (!await Wait(1f / 20f))
				return;
		}

		// The diving thrust.
		player = PlayerReads.Find(this);
		Vector2 aim = player?.GlobalPosition ?? GlobalPosition + new Vector2(dir * 200f, 200f);
		FaceTowards(aim.X);
		dir = FacingRight ? 1f : -1f;
		Vector2 velocity = (aim - GlobalPosition).Normalized() * 900f;
		if (velocity.Y < 300f)
			velocity.Y = 300f;
		Velocity = velocity;
		AimSpriteAlong(velocity);
		ShowFrame("e_air_atk", 4);
		SpearOn(ThrustReach * 1.25f, 60f, trail: false);
		_trail.PlayThrust(new Vector2(-30f, -12f), new Vector2(ThrustReach * 1.25f + 20f, -12f), 0.04f, 0.4f);
		float dive = 0f;
		while (!IsOnFloor() && dive < 1f)
		{
			Afterimage(ScarletGlow);
			if (!await Wait(0f))
				return;
			dive += (float)GetPhysicsProcessDeltaTime();
		}
		_spear.Deactivate();
		Sprite.Rotation = 0f;
		Gravity = _baseGravity;
		Velocity = Vector2.Zero;
		SpawnDust(GlobalPosition + new Vector2(0f, 40f));
		Sfx.PlayAt(this, "Footsteps/Stone", "Stone Chain Land", 2000f);
		if (!await PlayFrames("e_air_atk", 5, 7, 14f))
			return;
		await Recover(0.6f + PhaseTwoOpening);
	}

	// Scarlet Plunge: a high leap that comes down over him, the spear driven into the floor, and
	// the rot bursting out along the ground in front of her, one burst after another.
	private async Task ScarletPlunge()
	{
		if (!BeginMove("plunge"))
			return;
		_plungeCooldownTimer = PlungeCooldown;
		Cry();
		ShowFrame("e_up_loop", 0);
		if (!await Tell(0.35f, new Vector2(0f, -60f), RotColor, 0.3f))
			return;
		var player = PlayerReads.Find(this);
		float targetX = player?.GlobalPosition.X ?? GlobalPosition.X;
		const float airTime = 0.6f;
		Velocity = new Vector2((targetX - GlobalPosition.X) / airTime, -720f);
		Sfx.PlayAt(this, "Footsteps/Stone", "Stone Chain Jump", 2000f);
		float t = 0f;
		while (Velocity.Y < -30f && t < 1.2f)
		{
			ShowFrame("e_up_loop", (int)(t * 12f) % 3);
			if (!await Wait(0f))
				return;
			t += (float)GetPhysicsProcessDeltaTime();
		}

		// Hang over him a beat, spear pointed down.
		Gravity = 0f;
		Velocity = Vector2.Zero;
		FaceTowardsPlayer();
		float dir = FacingRight ? 1f : -1f;
		ShowFrame("e_down_loop", 0);
		Glint(new Vector2(0f, 30f), RotColor, 1.6f);
		SetMoveLight(RotColor, 1.6f);
		if (!await Wait(0.28f))
			return;
		Velocity = new Vector2(0f, 1300f);
		float fall = 0f;
		while (!IsOnFloor() && fall < 1.2f)
		{
			Afterimage(RotColor);
			ShowFrame("e_down_loop", (int)(fall * 14f) % 3);
			if (!await Wait(0f))
				return;
			fall += (float)GetPhysicsProcessDeltaTime();
		}
		Gravity = _baseGravity;
		Velocity = Vector2.Zero;
		HitPlayerAt(GlobalPosition + new Vector2(0f, -10f), 70f, 1.4f, false, 320f);
		Sfx.PlayAt(this, "Magic", "Spell Impact", 2000f);
		PlayerReads.Find(this)?.ShakeCamera(10f, 0.3f);
		SpawnDust(GlobalPosition + new Vector2(0f, 40f));

		// The rot runs forward along the ground.
		for (int i = 1; i <= PlungeBursts; i++)
		{
			Vector2 at = GlobalPosition + new Vector2(dir * 75f * i, 10f);
			RotBurst(at, 72f);
			HitPlayerAt(at, 72f, 1f, true, 260f);
			if (!await Wait(0.09f))
				return;
		}
		if (GetTree().CurrentScene is Node scene)
		{
			var cloud = new NubePodrida { Boss = this, Radius = 120f, Duration = 2.5f, Color = RotColor };
			scene.AddChild(cloud);
			cloud.GlobalPosition = GlobalPosition + new Vector2(dir * 150f, 38f);
		}
		SetMoveLight(RotColor, 0f);
		await Recover(0.8f + PhaseTwoOpening);
	}

	// A burst of scarlet rot: flash, ring, embers.
	private void RotBurst(Vector2 at, float radius)
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		var flash = new Sprite2D
		{
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			Modulate = RotColor,
			Scale = Vector2.One * (radius / 18f),
			ZIndex = 6,
		};
		scene.AddChild(flash);
		flash.GlobalPosition = at;
		Tween tween = flash.CreateTween();
		tween.TweenProperty(flash, "modulate:a", 0f, 0.35f);
		tween.TweenCallback(Callable.From(flash.QueueFree));
		SpawnRing(at, radius);
		SpawnPetals(at, 24, radius * 1.2f, 0.6f, inward: false);
	}

	// Called by FlorAeonia when it blooms.
	private void BloomFlower(Vector2 at, float radius)
	{
		Sfx.PlayAt(this, "Magic", "Spell Impact", 3000f);
		ScreenFlash(RotColor, 0.35f);
		RotBurst(at + new Vector2(0f, -40f), radius * 0.6f);
		SpawnRing(at + new Vector2(0f, -20f), radius);
		SpawnPetals(at + new Vector2(0f, -40f), 120, radius * 1.3f, 1.4f, inward: false);
		PlayerReads.Find(this)?.ShakeCamera(12f, 0.4f);
		HitPlayerAt(at + new Vector2(0f, -30f), radius, AeoniaBloomMultiplier, true, 420f);
		if (GetTree().CurrentScene is Node scene)
		{
			var cloud = new NubePodrida { Boss = this, Radius = radius * 0.8f, Duration = RotCloudDuration, Color = RotColor };
			scene.AddChild(cloud);
			cloud.GlobalPosition = at;
		}
	}

	// A hit that doesn't use a hitbox: parryable ones go through his normal damage (so his parry
	// and i-frames work), unparryable ones skip the parry. Either way she drains what got through.
	private bool HitPlayerAt(Vector2 center, float radius, float multiplier, bool unparryable, float knockback)
	{
		var player = PlayerReads.Find(this);
		if (player?.GetNodeOrNull<Stats>("Stats") is not { } stats || player.GlobalPosition.DistanceTo(center) > radius + 30f)
			return false;
		int before = stats.CurrentHealth;
		if (unparryable)
		{
			HitUnparryable(player, multiplier, center, knockback);
		}
		else
		{
			if (stats.IsInvulnerable)
				return false;
			stats.TakeDamage(Mathf.RoundToInt(Stats.AttackPower * multiplier));
			if (stats.CurrentHealth < before)
			{
				Vector2 away = (player.GlobalPosition - center).Normalized();
				player.ApplyKnockback(away == Vector2.Zero ? Vector2.Up : away, knockback);
			}
		}
		int lost = before - stats.CurrentHealth;
		if (lost > 0)
		{
			BloodEffect.SpawnAt(this, player.GlobalPosition);
			LifestealFrom(lost);
		}
		return lost > 0;
	}

	private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
	{
		Vector2 ab = b - a;
		float t = ab.LengthSquared() < 0.001f ? 0f : Mathf.Clamp((p - a).Dot(ab) / ab.LengthSquared(), 0f, 1f);
		return p.DistanceTo(a + ab * t);
	}

	// Scarlet tendrils writhing around her while she charges Aeonia.
	private partial class Tendriles : Node2D
	{
		public Color Color;
		private readonly List<Line2D> _lines = new();
		private float _time;

		public override void _Ready()
		{
			ZIndex = 5;
			var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
			var taper = new Curve();
			taper.AddPoint(new Vector2(0f, 1f));
			taper.AddPoint(new Vector2(1f, 0.1f));
			for (int i = 0; i < 8; i++)
			{
				var line = new Line2D { Width = 6f, WidthCurve = taper, DefaultColor = Color, Material = additive, Modulate = new Color(1f, 1f, 1f, 0f) };
				AddChild(line);
				_lines.Add(line);
				line.CreateTween().TweenProperty(line, "modulate:a", 0.9f, 0.3f);
			}
		}

		public override void _Process(double delta)
		{
			_time += (float)delta;
			for (int i = 0; i < _lines.Count; i++)
			{
				float baseAngle = Mathf.Tau * i / _lines.Count + _time * 0.6f;
				var points = new Vector2[10];
				for (int k = 0; k < points.Length; k++)
				{
					float f = k / (float)(points.Length - 1);
					float angle = baseAngle + Mathf.Sin(_time * 5f + i * 1.7f + f * 4f) * 0.5f * f;
					points[k] = Vector2.Right.Rotated(angle) * Mathf.Lerp(18f, 95f, f);
				}
				_lines[i].Points = points;
			}
		}

		public void Fade()
		{
			Tween tween = CreateTween();
			tween.TweenProperty(this, "modulate:a", 0f, 0.3f);
			tween.TweenCallback(Callable.From(QueueFree));
		}
	}

	// The giant scarlet flower of Aeonia: a ring on the ground marks the blast, petals unfurl in a
	// fan, then it blooms (via the boss) and scatters.
	private partial class FlorAeonia : Node2D
	{
		public ArtemisaBoss Boss;
		public float Radius = 210f;
		public float Delay = 1.1f;
		public Color Color;
		private readonly List<Node2D> _petals = new();
		private ZonaRota _ring;
		private PointLight2D _light;
		private float _time;
		private bool _bloomed;

		public override void _Ready()
		{
			ZIndex = 4;
			var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
			// The danger zone on the ground: torn, crawling strands of rot (not a clean ellipse) with
			// rot boiling up out of it — still readable as "this patch of floor".
			_ring = new ZonaRota { Radius = Radius, Color = Color, Material = additive };
			AddChild(_ring);
			AddChild(RotEmbers(Radius, Color));
			_light = new PointLight2D
			{
				Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
				Color = Color,
				Energy = 0.3f,
				TextureScale = Radius / 40f,
				Position = new Vector2(0f, -40f),
			};
			AddChild(_light);
			// Nine petals in a fan, each a glowing teardrop with a pale core.
			for (int i = 0; i < 9; i++)
			{
				float angle = Mathf.Lerp(-170f, -10f, i / 8f);
				var petal = new Node2D { RotationDegrees = angle, Scale = Vector2.One * 0.05f };
				petal.AddChild(new Polygon2D { Polygon = Teardrop(Radius * 0.62f, Radius * 0.16f), Color = Color with { A = 0.85f }, Material = additive });
				petal.AddChild(new Polygon2D { Polygon = Teardrop(Radius * 0.45f, Radius * 0.07f), Color = new Color(1f, 0.85f, 0.75f, 0.9f), Material = additive });
				AddChild(petal);
				_petals.Add(petal);
				Tween open = petal.CreateTween();
				open.TweenInterval(i * 0.03f);
				open.TweenProperty(petal, "scale", Vector2.One, Delay * 0.75f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
			}
			Sfx.PlayAt(this, "Magic", "Firebuff", 2000f);
		}

		public override void _Process(double delta)
		{
			_time += (float)delta;
			if (_bloomed)
				return;
			// The ring and the light pulse faster as the bloom nears.
			float urgency = Mathf.Clamp(_time / Delay, 0f, 1f);
			float pulse = 0.5f + 0.5f * Mathf.Sin(_time * Mathf.Lerp(8f, 26f, urgency));
			_ring.Modulate = new Color(1f, 1f, 1f, 0.4f + 0.6f * pulse);
			_ring.Chaos = 0.12f + 0.35f * urgency;
			_light.Energy = 0.3f + 1.5f * urgency * pulse;
			// The petals shiver harder as it nears.
			foreach (Node2D petal in _petals)
				petal.Position = new Vector2((float)GD.RandRange(-1.0, 1.0), (float)GD.RandRange(-1.0, 1.0)) * (1f + 4f * urgency);
			if (_time < Delay)
				return;
			_bloomed = true;
			if (IsInstanceValid(Boss) && !Boss.IsQueuedForRemoval)
				Boss.BloomFlower(GlobalPosition, Radius);
			Tween scatter = CreateTween();
			foreach (Node2D petal in _petals)
			{
				scatter.Parallel().TweenProperty(petal, "scale", Vector2.One * 1.6f, 0.35f);
				scatter.Parallel().TweenProperty(petal, "modulate:a", 0f, 0.4f);
			}
			scatter.Parallel().TweenProperty(_ring, "modulate:a", 0f, 0.3f);
			scatter.Parallel().TweenProperty(_light, "energy", 0f, 0.4f);
			scatter.TweenCallback(Callable.From(QueueFree));
		}

		private static Vector2[] Teardrop(float length, float width)
		{
			var points = new Vector2[16];
			for (int i = 0; i < points.Length; i++)
			{
				float t = i / (float)(points.Length - 1);
				float a = t * Mathf.Tau;
				// Round at the base, pointed at the tip (along +X).
				float x = (1f - Mathf.Cos(a)) * 0.5f * length;
				float y = Mathf.Sin(a) * width * Mathf.Sin(a * 0.5f);
				points[i] = new Vector2(x, y);
			}
			return points;
		}
	}

	// Rot boiling up off a patch of floor: uneven embers rising and drifting.
	private static GpuParticles2D RotEmbers(float halfWidth, Color color)
	{
		var gradient = new Gradient();
		gradient.SetColor(0, color with { A = 0.9f });
		gradient.SetColor(1, color with { A = 0f });
		return new GpuParticles2D
		{
			Amount = 46,
			Lifetime = 0.9f,
			Randomness = 0.7f,
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			ProcessMaterial = new ParticleProcessMaterial
			{
				EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
				EmissionBoxExtents = new Vector3(halfWidth * 0.9f, 6f, 0f),
				Direction = new Vector3(0f, -1f, 0f),
				Spread = 25f,
				InitialVelocityMin = 40f,
				InitialVelocityMax = 150f,
				Gravity = new Vector3(0f, -30f, 0f),
				TurbulenceEnabled = true,
				TurbulenceNoiseStrength = 3f,
				TurbulenceInfluenceMin = 0.05f,
				TurbulenceInfluenceMax = 0.2f,
				ScaleMin = 0.15f,
				ScaleMax = 0.6f,
				ColorRamp = new GradientTexture1D { Gradient = gradient },
			},
			Position = new Vector2(0f, -4f),
			Emitting = true,
		};
	}

	// The ground patch of a rot attack, drawn as torn strands that re-tear every few frames:
	// gaps, kinks, uneven widths. Flattened (it's the floor seen from the side).
	private partial class ZonaRota : Node2D
	{
		public float Radius = 200f;
		public Color Color;
		// How much the radius wanders per point (fraction of Radius).
		public float Chaos = 0.12f;
		private float _redraw;

		public override void _Process(double delta)
		{
			_redraw -= (float)delta;
			if (_redraw > 0f)
				return;
			_redraw = 0.04f;
			QueueRedraw();
		}

		public override void _Draw()
		{
			for (int strand = 0; strand < 3; strand++)
			{
				var run = new List<Vector2>();
				float start = (float)GD.RandRange(0.0, Mathf.Tau);
				int steps = 40;
				for (int i = 0; i <= steps; i++)
				{
					// Random gaps: the strand tears.
					if (GD.Randf() < 0.2f)
					{
						DrawRun(run, strand);
						run.Clear();
						continue;
					}
					float a = start + Mathf.Tau * i / steps;
					float r = Radius * (1f + (float)GD.RandRange(-Chaos, Chaos)) - strand * (float)GD.RandRange(0.0, 18.0);
					run.Add(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.2f + (float)GD.RandRange(-3.0, 3.0)));
				}
				DrawRun(run, strand);
			}
		}

		private void DrawRun(List<Vector2> run, int strand)
		{
			if (run.Count < 2)
				return;
			float width = (float)GD.RandRange(1.5, 5.0) - strand * 0.6f;
			DrawPolyline(run.ToArray(), Color with { A = 0.9f - strand * 0.25f }, Mathf.Max(1f, width), true);
		}
	}

	// The bud of the phase change: fleshy scarlet petals push up out of the floor and close around
	// her, their edges never still (re-roughened every few frames); while she transforms inside it
	// swells and shudders harder and harder, glowing from within; then it tears open.
	private partial class CapulloRot : Node2D
	{
		public Color Color;
		public float Height = 230f;
		private readonly List<(Node2D Pivot, Polygon2D Shape, float Length, float Width, float Lean)> _petals = new();
		private Sprite2D _glow;
		private float _intensity = 0.15f;
		private float _redraw;
		private bool _burst;
		private readonly RandomNumberGenerator _rng = new();

		public override void _Ready()
		{
			_rng.Randomize();
			_glow = new Sprite2D
			{
				Texture = GD.Load<Texture2D>("res://resources/lighting/PointLightGradient.tres"),
				Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
				Modulate = Color with { A = 0f },
				Position = new Vector2(0f, -Height * 0.45f),
				Scale = new Vector2(0.5f, 0.8f),
				ZIndex = 1,
			};
			AddChild(_glow);
			AddChild(RotEmbers(60f, Color));
			int count = 9;
			for (int i = 0; i < count; i++)
			{
				float baseX = Mathf.Lerp(-48f, 48f, i / (float)(count - 1)) + _rng.RandfRange(-6f, 6f);
				// Alternate layers: some petals behind her, the rest in front (see-through).
				bool front = i % 2 == 0;
				float length = Height * _rng.RandfRange(0.82f, 1.05f);
				var pivot = new Node2D
				{
					Position = new Vector2(baseX, 0f),
					Scale = new Vector2(0.3f, 0f),
					ZIndex = front ? 2 : -1,
				};
				var shape = new Polygon2D { Modulate = new Color(1f, 1f, 1f, front ? 0.82f : 1f) };
				pivot.AddChild(shape);
				AddChild(pivot);
				_petals.Add((pivot, shape, length, length * _rng.RandfRange(0.3f, 0.4f), -baseX / 48f * 0.32f));
			}
			Reshape();
		}

		public void Grow(float seconds)
		{
			for (int i = 0; i < _petals.Count; i++)
			{
				Tween grow = _petals[i].Pivot.CreateTween();
				grow.TweenInterval(i * 0.05f);
				grow.TweenProperty(_petals[i].Pivot, "scale", Vector2.One, seconds).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
			}
			CreateTween().TweenProperty(_glow, "modulate:a", 0.35f, seconds);
		}

		// She transforms inside: it swells and the shudder builds.
		public void Charge()
		{
			CreateTween().TweenMethod(Callable.From<float>(v => _intensity = v), _intensity, 1f, 2.4f);
			CreateTween().TweenProperty(this, "scale", new Vector2(1.12f, 1.08f), 2.4f).SetEase(Tween.EaseType.In);
			CreateTween().TweenProperty(_glow, "modulate:a", 1f, 2.3f);
		}

		public void Burst()
		{
			_burst = true;
			Tween tween = CreateTween().SetParallel();
			foreach (var petal in _petals)
			{
				float side = petal.Pivot.Position.X >= 0f ? 1f : -1f;
				tween.TweenProperty(petal.Pivot, "rotation", petal.Lean + side * _rng.RandfRange(0.9f, 1.5f), 0.3f).SetEase(Tween.EaseType.Out);
				tween.TweenProperty(petal.Pivot, "position", petal.Pivot.Position + new Vector2(side * _rng.RandfRange(40f, 110f), _rng.RandfRange(-20f, 10f)), 0.35f);
				tween.TweenProperty(petal.Pivot, "scale", Vector2.One * 1.4f, 0.35f);
				tween.TweenProperty(petal.Pivot, "modulate:a", 0f, 0.45f);
			}
			tween.TweenProperty(_glow, "scale", new Vector2(2.2f, 2.2f), 0.2f);
			tween.TweenProperty(_glow, "modulate:a", 0f, 0.5f);
			tween.Chain().TweenCallback(Callable.From(QueueFree));
		}

		public override void _Process(double delta)
		{
			if (_burst)
				return;
			_redraw -= (float)delta;
			if (_redraw > 0f)
				return;
			_redraw = 0.05f;
			Reshape();
		}

		// Fresh rough edges and a fresh shiver for every petal, stronger as the intensity builds.
		private void Reshape()
		{
			var dark = new Color(0.22f, 0.02f, 0.04f, 1f);
			foreach (var petal in _petals)
			{
				petal.Pivot.Rotation = petal.Lean + _rng.RandfRange(-1f, 1f) * 0.06f * (0.3f + _intensity);
				const int steps = 9;
				var points = new Vector2[steps * 2];
				var colors = new Color[steps * 2];
				for (int k = 0; k < steps; k++)
				{
					float t = k / (float)(steps - 1);
					float half = petal.Width * 0.5f * Mathf.Pow(Mathf.Sin(Mathf.Pi * Mathf.Min(t * 1.05f, 1f)), 0.7f);
					float y = -petal.Length * t;
					float rough = petal.Width * 0.12f * (0.4f + _intensity);
					points[k] = new Vector2(-half - _rng.RandfRange(0f, rough), y + _rng.RandfRange(-3f, 3f));
					points[steps * 2 - 1 - k] = new Vector2(half + _rng.RandfRange(0f, rough), y + _rng.RandfRange(-3f, 3f));
					Color c = dark.Lerp(Color, Mathf.Pow(t, 0.7f));
					colors[k] = c;
					colors[steps * 2 - 1 - k] = c;
				}
				petal.Shape.Polygon = points;
				petal.Shape.VertexColors = colors;
			}
		}
	}

	// A burst wave of rot: torn, jittering strands racing outward and breaking up — the
	// replacement for a clean expanding ring.
	private partial class OndaRota : Node2D
	{
		public float Radius = 120f;
		public Color Color;
		public float Duration = 0.5f;
		private float _time;
		private float _redraw;

		public override void _Process(double delta)
		{
			_time += (float)delta;
			if (_time >= Duration)
			{
				QueueFree();
				return;
			}
			_redraw -= (float)delta;
			if (_redraw > 0f)
				return;
			_redraw = 0.035f;
			QueueRedraw();
		}

		public override void _Draw()
		{
			float p = Mathf.Clamp(_time / Duration, 0f, 1f);
			float eased = 1f - Mathf.Pow(1f - p, 3f);
			float r = Mathf.Lerp(Radius * 0.2f, Radius, eased);
			float fade = 1f - p;
			// Short shards flung along the wave front, each at its own radius and angle.
			int shards = 26;
			for (int i = 0; i < shards; i++)
			{
				if (GD.Randf() < 0.25f + 0.4f * p)
					continue;
				float a = Mathf.Tau * i / shards + (float)GD.RandRange(-0.12, 0.12);
				float rr = r * (1f + (float)GD.RandRange(-0.22, 0.22));
				float len = (float)GD.RandRange(0.18, 0.45) * Mathf.Tau / shards * rr;
				Vector2 tangent = Vector2.Right.Rotated(a + Mathf.Pi / 2f);
				Vector2 mid = Vector2.Right.Rotated(a) * rr;
				Vector2 kink = mid + Vector2.Right.Rotated(a) * (float)GD.RandRange(-6.0, 6.0);
				DrawPolyline(new[] { mid - tangent * len * 0.5f, kink, mid + tangent * len * 0.5f },
					Color with { A = fade }, (float)GD.RandRange(2.0, 8.0) * fade + 1f, true);
			}
		}
	}

	// A lingering cloud of scarlet rot on the ground: drifting embers and a glow; standing in it
	// poisons (one poison refresh per second, no i-frames).
	private partial class NubePodrida : Node2D
	{
		public ArtemisaBoss Boss;
		public float Radius = 150f;
		public float Duration = 4f;
		public Color Color;
		private float _time;
		private float _tick;
		private GpuParticles2D _embers;
		private Sprite2D _glow;

		public override void _Ready()
		{
			ZIndex = 3;
			var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
			_glow = new Sprite2D
			{
				Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
				Material = additive,
				Modulate = Color with { A = 0f },
				Scale = new Vector2(Radius / 20f, Radius / 55f),
				Position = new Vector2(0f, -10f),
			};
			AddChild(_glow);
			_glow.CreateTween().TweenProperty(_glow, "modulate:a", 0.55f, 0.4f);
			var gradient = new Gradient();
			gradient.SetColor(0, Color with { A = 0f });
			gradient.AddPoint(0.2f, Color with { A = 0.8f });
			gradient.SetColor(gradient.GetPointCount() - 1, new Color(0.3f, 0.05f, 0.05f, 0f));
			_embers = new GpuParticles2D
			{
				Amount = Mathf.RoundToInt(Radius * 0.35f),
				Lifetime = 1.6f,
				Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
				Material = additive,
				ProcessMaterial = new ParticleProcessMaterial
				{
					EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
					EmissionBoxExtents = new Vector3(Radius, 6f, 0f),
					Direction = new Vector3(0f, -1f, 0f),
					Spread = 25f,
					InitialVelocityMin = 15f,
					InitialVelocityMax = 45f,
					Gravity = new Vector3(0f, -10f, 0f),
					ScaleMin = 0.25f,
					ScaleMax = 0.6f,
					ColorRamp = new GradientTexture1D { Gradient = gradient },
				},
				Emitting = true,
			};
			AddChild(_embers);
			Sfx.PlayAt(this, "Magic", "Firespray", 1500f);
		}

		public override void _PhysicsProcess(double delta)
		{
			float dt = (float)delta;
			_time += dt;
			_tick -= dt;
			if (_time >= Duration)
			{
				SetPhysicsProcess(false);
				_embers.Emitting = false;
				Tween fade = CreateTween();
				fade.TweenProperty(_glow, "modulate:a", 0f, 0.6f);
				fade.TweenInterval(1.2f);
				fade.TweenCallback(Callable.From(QueueFree));
				return;
			}
			var player = PlayerReads.Find(this);
			if (player is null || _tick > 0f)
				return;
			Vector2 rel = player.GlobalPosition - GlobalPosition;
			float nx = rel.X / Radius;
			float ny = (rel.Y + 20f) / (Radius * 0.45f);
			if (nx * nx + ny * ny <= 1f)
			{
				_tick = 1f;
				player.ApplyPoison(3, 0.5f, 1.5f);
			}
		}
	}

	// ── Move plumbing ───────────────────────────────────────────────────────────────────────

	private Hitbox BuildHitbox(string name, Shape2D shape, Vector2 position)
	{
		var hitbox = new Hitbox { Name = name, CollisionLayer = 0, CollisionMask = 2, Position = position };
		hitbox.AddChild(new CollisionShape2D { Name = "CollisionShape2D", Shape = shape, Disabled = true });
		AddChild(hitbox);
		hitbox.HitDealt += OnHitDealt;
		return hitbox;
	}

	private bool BeginMove(string name)
	{
		if (Sprite is null)
			return false;
		_busy = true;
		_attackCooldownTimer = AttackCooldown;
		_secondLastMove = _lastMove;
		_lastMove = name;
		if (DebugAI)
			GD.Print($"[Artemisa] {name}  parryHabit={_parryHabit:0.0} evadeHabit={_evadeHabit:0.0} attackHabit={_attackHabit:0.0}");
		Velocity = new Vector2(0f, Velocity.Y);
		FaceTowardsPlayer();
		return true;
	}

	private async Task Recover(float recovery)
	{
		_spear.Deactivate();
		_danceHitbox.Deactivate();
		Gravity = _baseGravity;
		if (Sprite is not null)
		{
			Sprite.Rotation = 0f;
			Sprite.Play(_phaseTwo ? "e_idle" : "idle");
		}
		Velocity = new Vector2(0f, Velocity.Y);
		if (await Wait(recovery))
			_busy = false;
	}

	private void Cry()
	{
		// A battle cry on some moves only — every swing would wear it out.
		if (_rng.Randf() < AttackVoiceChance)
			Sfx.PlayIfExistsAt(this, VoiceCategory, "Ataque", 2000f);
	}

	private void ShowFrame(string anim, int frame)
	{
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
		Sprite.Pause();
		Sprite.Frame = frame;
	}

	private async Task<bool> PlayFrames(string anim, int from, int to, float fps)
	{
		for (int f = from; f <= to; f++)
		{
			ShowFrame(anim, f);
			if (!await Wait(1f / fps))
				return false;
		}
		return true;
	}

	private async Task<bool> PlayStabs(string anim, Stab[] stabs, float fps)
	{
		foreach (Stab stab in stabs)
		{
			float dir = FacingRight ? 1f : -1f;
			float reach = stab.Reach * CurrentScale;
			for (int f = stab.From; f <= stab.To; f++)
			{
				ShowFrame(anim, f);
				if (f == stab.From)
				{
					SpearOn(reach);
					var player = PlayerReads.Find(this);
					if (player is not null && Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X) > reach * 0.7f)
						Velocity = new Vector2(dir * LungeSpeed * 1.4f, Velocity.Y);
				}
				if (!await Wait(1f / fps))
					return false;
			}
			_spear.Deactivate();
			Velocity = new Vector2(0f, Velocity.Y);
		}
		return true;
	}

	// The spear hitbox out to `reach`, its scarlet thrust streak running a little past the hitbox's
	// far edge (the trail shows the real reach), the whoosh, and a note if he was dodging it.
	private void SpearOn(float reach, float height = -1f, bool trail = true, float damageMultiplier = 1f)
	{
		if (height <= 0f)
			height = HitHeight;
		float dir = FacingRight ? 1f : -1f;
		_spearShape.Size = new Vector2(reach, height);
		_spear.Position = new Vector2(dir * reach * 0.5f, -height * 0.5f);
		_spear.Activate(Stats, ignorePostHitInvulnerability: true, damageMultiplier: damageMultiplier);
		Sfx.PlayAt(this, "Combat/Sword", "Sword Attack", 2000f);
		if (trail)
			_trail.PlayThrust(new Vector2(24f, -12f), new Vector2(reach + 14f, -12f), 0.05f, 0.08f);
		var player = PlayerReads.Find(this);
		if (player is not null && (player.IsDashing || player.IsRolling)
			&& Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X) < reach + 120f)
			_evadeHabit = Mathf.Min(_evadeHabit + 0.3f, 4f);
	}

	// A tell: the body flares, a glint flashes at `glintAt` (Visual space, facing right). Then
	// Malenia's delay — while his parry is open (or he's mid-dodge) she keeps holding, up to
	// `maxDelay`, and lets go the moment it's spent. Seeing the parry teaches her he's a parrier.
	private async Task<bool> Tell(float seconds, Vector2 glintAt, Color color, float maxDelay)
	{
		FlashBody(color, seconds);
		Glint(glintAt, color, 1f);
		if (!await Wait(seconds))
			return false;
		float held = 0f;
		bool sawParry = false;
		var player = PlayerReads.Find(this);
		while (player is not null && held < maxDelay
			&& (PlayerReads.IsBlockingToward(player, GlobalPosition.X) || player.IsDashing || player.IsRolling))
		{
			if (PlayerReads.IsBlockingToward(player, GlobalPosition.X))
				sawParry = true;
			if (!await Wait(0f))
				return false;
			held += (float)GetPhysicsProcessDeltaTime();
			player = PlayerReads.Find(this);
		}
		if (sawParry)
			_parryHabit = Mathf.Min(_parryHabit + 0.6f, 4f);
		return true;
	}

	private void FlashBody(Color color, float duration)
	{
		if (Sprite is null)
			return;
		Color tint = new(1f + color.R * 0.8f, 1f + color.G * 0.5f, 1f + color.B * 0.5f, 1f);
		Tween tween = Sprite.CreateTween();
		tween.TweenProperty(Sprite, "self_modulate", tint, Mathf.Max(0.04f, duration * 0.35f));
		tween.TweenProperty(Sprite, "self_modulate", Colors.White, Mathf.Max(0.06f, duration * 0.65f));
	}

	private void SetMoveLight(Color color, float energy)
	{
		_moveLight.Color = color;
		_moveLight.CreateTween().TweenProperty(_moveLight, "energy", energy, 0.08f);
	}

	// The sprite turned spear-first along a direction (local to the facing flip).
	private void AimSpriteAlong(Vector2 velocity)
	{
		if (Sprite is null || velocity == Vector2.Zero)
			return;
		Sprite.Rotation = new Vector2(Mathf.Abs(velocity.X), velocity.Y).Angle();
	}

	private float _ghostTimer;

	// Scarlet neon afterimage of the current frame (same silhouette shader as the player's dash).
	private void Afterimage(Color color)
	{
		_ghostTimer -= (float)GetPhysicsProcessDeltaTime();
		if (_ghostTimer > 0f)
			return;
		_ghostTimer = 0.035f;
		Texture2D frame = Sprite?.SpriteFrames?.GetFrameTexture(Sprite.Animation, Sprite.Frame);
		if (frame is null || GetTree().CurrentScene is not Node scene)
			return;
		var ghost = new Sprite2D
		{
			Texture = frame,
			Centered = Sprite.Centered,
			Offset = Sprite.Offset,
			Material = Metroidvania.Player.NeonSilhouette.CreateMaterial(color, 1.5f),
			Modulate = new Color(1f, 1f, 1f, 0.55f),
			ZIndex = ZIndex - 1,
		};
		scene.AddChild(ghost);
		ghost.GlobalTransform = Sprite.GlobalTransform;
		Tween fade = ghost.CreateTween();
		fade.TweenProperty(ghost, "modulate:a", 0f, 0.25f);
		fade.TweenCallback(Callable.From(ghost.QueueFree));
	}

	private async Task<bool> WaitUntilLanded(float timeout)
	{
		float elapsed = 0f;
		while (!IsOnFloor() && elapsed < timeout)
		{
			if (!await Wait(0f))
				return false;
			elapsed += (float)GetPhysicsProcessDeltaTime();
		}
		return true;
	}

	// ── Lifesteal ───────────────────────────────────────────────────────────────────────────

	// Malenia's drain: she heals by what actually got through (the health he lost since last frame),
	// so a parried stab heals nothing.
	private void OnHitDealt()
	{
		var player = PlayerReads.Find(this);
		if (player?.GetNodeOrNull<Stats>("Stats") is not { } playerStats || _playerHealthSeen < 0)
			return;
		int lost = _playerHealthSeen - playerStats.CurrentHealth;
		_playerHealthSeen = playerStats.CurrentHealth;
		LifestealFrom(lost);
	}

	private void LifestealFrom(int damage)
	{
		if (damage <= 0 || IsQueuedForRemoval)
			return;
		int before = Stats.CurrentHealth;
		Stats.Heal(Mathf.Max(1, Mathf.RoundToInt(damage * LifestealMultiplier)));
		int healed = Stats.CurrentHealth - before;
		if (healed <= 0)
			return;
		if (GetTree().CurrentScene is Node scene)
		{
			var label = new Label { Text = $"+{healed}", Modulate = HealTextColor, Scale = Vector2.One * 1.4f };
			scene.AddChild(label);
			label.GlobalPosition = GlobalPosition + new Vector2(-12f, -110f);
			Tween rise = label.CreateTween();
			rise.TweenProperty(label, "global_position:y", label.GlobalPosition.Y - 30f, 0.8f);
			rise.Parallel().TweenProperty(label, "modulate:a", 0f, 0.8f).SetEase(Tween.EaseType.In);
			rise.TweenCallback(Callable.From(label.QueueFree));
		}
		SpawnPetals(GlobalPosition + new Vector2(0f, -40f), 14, 90f, 0.5f, inward: true);
	}

	// ── Phase 2: the scarlet bloom ──────────────────────────────────────────────────────────

	private async Task PhaseTransition()
	{
		_phaseStarted = true;
		_busy = true;
		_spear.Deactivate();
		Velocity = new Vector2(0f, Velocity.Y);
		Stats.ExternalInvulnerable = true;
		FaceTowardsPlayer();
		_music?.Stop(1.2f);

		// Beat 1 — she breaks: reels, drops to one knee as she was when he found her, and the hall
		// goes dark around her.
		// Forced by StartInPhaseTwo at full health she isn't hurt: no reel, she just kneels.
		bool hurt = !StartInPhaseTwo || Stats.CurrentHealth < Stats.MaxHealth;
		if (hurt)
		{
			Sfx.PlayIfExistsAt(this, VoiceCategory, "Golpeado", 2000f);
			Sprite?.Play("take_hit");
		}
		var arena = ArenaModulatePath is null || ArenaModulatePath.IsEmpty ? null : GetNodeOrNull<CanvasModulate>(ArenaModulatePath);
		Color arenaColour = arena?.Color ?? Colors.White;
		arena?.CreateTween().TweenProperty(arena, "color", arenaColour * new Color(0.45f, 0.4f, 0.42f, 1f), 1.4f);
		if (!await Wait(0.35f))
			return;
		Sprite?.Play("couch_idle");
		Sfx.PlayAt(this, "Footsteps/Stone", "Stone Chain Land", 2000f, RiseVolumeDb);
		SpawnDust(GlobalPosition + new Vector2(0f, 40f));

		// Beat 2 (held): a bud of scarlet rot pushes up out of the floor and closes around her while
		// she speaks from inside it.
		var bud = new CapulloRot { Color = RotColor, Height = 230f, Position = new Vector2(0f, 42f) };
		AddChild(bud);
		var player = PlayerReads.Find(this);
		var scene = new Cinematic { Name = "FaseArtemisa", PlayOnce = false };
		scene.Steps.Add(new StepCamera { Target = new NodePath(".."), Weight = 0.7f, Zoom = 1.5f, WaitSeconds = 0.5f });
		scene.Steps.Add(new StepCode { Action = async () =>
		{
			bud.Grow(1.6f);
			Tremble(ChargeTremble * 0.6f, 1.6f);
			for (int i = 0; i < 8; i++)
			{
				player?.ShakeCamera(3f + i * 1.2f, 0.15f);
				SpawnDust(GlobalPosition + new Vector2(_rng.RandfRange(-90f, 90f), 40f));
				if (!await Wait(0.2f))
					return;
			}
		} });
		scene.Steps.Add(new StepSay { SpeakerKeys = new[] { SpeakerKey }, LineKeys = PhaseLines, ExtraPauses = new[] { 0.4f } });
		scene.Steps.Add(new StepCamera { Target = new NodePath(".."), Weight = 0.5f, Zoom = 1.15f, WaitSeconds = 0.3f });
		scene.Steps.Add(new StepCamera { WaitSeconds = 0.1f });
		AddChild(scene);
		await scene.Play();
		if (IsInstanceValid(scene))
			scene.QueueFree();
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
		{
			if (IsInstanceValid(bud))
				bud.QueueFree();
			return;
		}

		// Beat 3 (player free — the warning): inside the bud she transforms. It swells, shudders
		// harder and harder, light builds, petals are sucked in. He has these ~2.5 s to get clear.
		var light = new PointLight2D
		{
			Color = ScarletGlow,
			Energy = 0f,
			TextureScale = 4f,
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			Position = new Vector2(0f, -50f),
		};
		AddChild(light);
		light.CreateTween().TweenProperty(light, "energy", 3.2f, 2.3f);
		Tremble(ChargeTremble, 2.8f);
		bud.Charge();
		Sprite?.Play("transform");
		float petalTimer = 0f;
		float rumbleTimer = 0f;
		float charging = 0f;
		while (Sprite is not null && Sprite.Animation == "transform" && Sprite.IsPlaying())
		{
			float step = (float)GetPhysicsProcessDeltaTime();
			charging += step;
			petalTimer -= step;
			if (petalTimer <= 0f)
			{
				petalTimer = 0.2f;
				SpawnPetals(GlobalPosition + new Vector2(0f, -50f), 16, BloomRadius, 0.6f, inward: true);
			}
			rumbleTimer -= step;
			if (rumbleTimer <= 0f)
			{
				rumbleTimer = _rng.RandfRange(0.08f, 0.16f);
				PlayerReads.Find(this)?.ShakeCamera(Mathf.Min(4f + charging * 5f, 15f), 0.12f);
			}
			if (!await Wait(0f))
			{
				if (IsInstanceValid(bud))
					bud.QueueFree();
				return;
			}
		}

		// Beat 4 — the bloom: the bud tears open, the blast, and the goddess hangs in the air on
		// scarlet wings for a breath before she comes down.
		bud.Burst();
		Bloom();
		if (_music is not null && BossMusicFase2 is not null)
			_music.Play(BossMusicFase2, 0.3f);
		else if (BossMusic is not null && _music is not null)
		{
			_music.PitchScale = 1.06f;
			_music.Play(BossMusic, 0.3f);
		}
		ScreenFlash(new Color(1f, 0.75f, 0.6f, 0.5f), 0.25f);
		PlayerReads.Find(this)?.ShakeCamera(22f, 0.5f);
		_phaseTwo = true;
		ApplyPhaseTwoSize();
		MoveSpeed = _baseMoveSpeed * PhaseTwoSpeedScale;
		light.CreateTween().TweenProperty(light, "energy", 0.6f, 0.8f);
		arena?.CreateTween().TweenProperty(arena, "color", arenaColour, 1.2f);
		Gravity = 0f;
		Sprite?.Play("e_sp_atk_loop");
		float hover = 0f;
		while (hover < 1.1f)
		{
			float step = (float)GetPhysicsProcessDeltaTime();
			hover += step;
			Velocity = new Vector2(0f, hover < 0.35f ? -170f : 0f);
			if (_rng.Randf() < 0.3f)
				SpawnPetals(GlobalPosition + new Vector2(_rng.RandfRange(-70f, 70f), _rng.RandfRange(-90f, -20f)), 3, 40f, 0.8f, inward: false);
			if (!await Wait(0f))
				return;
		}
		Gravity = _baseGravity;
		Velocity = Vector2.Zero;
		ShowFrame("e_down_loop", 0);
		await WaitUntilLanded(1.5f);
		Sfx.PlayAt(this, "Footsteps/Stone", "Stone Chain Land", 2000f, RiseVolumeDb);
		SpawnDust(GlobalPosition + new Vector2(0f, 40f));
		Stats.ExternalInvulnerable = false;
		Sprite?.Play("e_idle");
		_attackCooldownTimer = 0.8f;
		_aeoniaNext = true;
		if (await Wait(0.9f))
			_busy = false;
	}

	// The bloom: flash, a ring racing out, a storm of petals and an unparryable hit within the radius.
	private void Bloom()
	{
		Vector2 center = GlobalPosition + new Vector2(0f, -50f);
		ScreenFlash(ScarletGlow, 0.5f);
		SpawnRing(center, BloomRadius);
		SpawnPetals(center, 160, BloomRadius * 1.3f, 1.6f, inward: false);
		var player = PlayerReads.Find(this);
		if (player is null)
			return;
		player.ShakeCamera(14f, 0.5f);
		if (player.GlobalPosition.DistanceTo(center) <= BloomRadius)
			HitUnparryable(player, BloomDamageMultiplier, center, BloomKnockback);
	}

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

	// ── Death ───────────────────────────────────────────────────────────────────────────────

	// The wings fall away first (back to human form), then the last line, then she falls.
	private async Task DeathScene()
	{
		if (Sprite is null)
		{
			QueueFree();
			return;
		}
		Velocity = Vector2.Zero;
		if (_phaseTwo)
		{
			Sprite.Play("back2human");
			while (IsInstanceValid(this) && Sprite.Animation == "back2human" && Sprite.IsPlaying())
				await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			if (!IsInstanceValid(this))
				return;
		}
		Sprite.Play("couch_idle");
		var scene = new Cinematic { Name = "MuerteArtemisa", PlayOnce = false };
		scene.Steps.Add(new StepCamera { Target = new NodePath(".."), Weight = 0.8f, Zoom = 1.45f, WaitSeconds = 0.6f });
		scene.Steps.Add(new StepSay { SpeakerKeys = new[] { SpeakerKey }, LineKeys = DeathLines, ExtraPauses = new[] { 1.4f, 0.6f } });
		scene.Steps.Add(new StepCode { Action = Fall });
		scene.Steps.Add(new StepWait { Seconds = 2f });
		scene.Steps.Add(new StepCamera { WaitSeconds = 0.4f });
		AddChild(scene);
		await scene.Play();
		if (IsInstanceValid(this))
			QueueFree();
	}

	private async Task Fall()
	{
		if (Sprite is null || !IsInstanceValid(this))
			return;
		SpawnPetals(GlobalPosition + new Vector2(0f, -20f), 50, 200f, 1.8f, inward: false);
		Sfx.PlayAt(this, "Footsteps/Stone", "Stone Chain Land", 2000f);
		// The petals stop falling with her, and her light goes out.
		if (IsInstanceValid(_petalRain))
			_petalRain.Emitting = false;
		if (IsInstanceValid(_halo))
			_halo.CreateTween().TweenProperty(_halo, "energy", 0f, 2.5f);
		Sprite.Play("death");
		while (IsInstanceValid(this) && Sprite.Animation == "death" && Sprite.IsPlaying())
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	}

	// ── FX (procedural) ─────────────────────────────────────────────────────────────────────

	// Scarlet petals: a one-shot burst out from `at`, or drawn in toward it from `radius` away.
	private void SpawnPetals(Vector2 at, int amount, float radius, float lifetime, bool inward)
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		var gradient = new Gradient();
		gradient.SetColor(0, ScarletCore);
		gradient.SetColor(1, ScarletGlow with { A = 0f });
		var material = new ParticleProcessMaterial
		{
			Direction = new Vector3(0f, -1f, 0f),
			Spread = 180f,
			Gravity = new Vector3(0f, inward ? 0f : 60f, 0f),
			InitialVelocityMin = inward ? 0f : radius * 0.5f / lifetime,
			InitialVelocityMax = inward ? 0f : radius / lifetime,
			RadialAccelMin = inward ? -radius * 2.2f : 0f,
			RadialAccelMax = inward ? -radius * 1.6f : 0f,
			DampingMin = inward ? 0f : radius * 0.4f,
			DampingMax = inward ? 0f : radius * 0.7f,
			AngularVelocityMin = -300f,
			AngularVelocityMax = 300f,
			ScaleMin = 0.18f,
			ScaleMax = 0.45f,
			ColorRamp = new GradientTexture1D { Gradient = gradient },
		};
		if (inward)
		{
			material.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring;
			material.EmissionRingRadius = radius;
			material.EmissionRingInnerRadius = radius * 0.7f;
			material.EmissionRingHeight = 0f;
			material.EmissionRingAxis = new Vector3(0f, 0f, 1f);
		}
		var particles = new GpuParticles2D
		{
			Amount = Mathf.Max(1, amount),
			Lifetime = lifetime,
			OneShot = true,
			Explosiveness = inward ? 0.6f : 0.95f,
			ProcessMaterial = material,
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			ZIndex = 6,
			Emitting = true,
		};
		scene.AddChild(particles);
		particles.GlobalPosition = at;
		particles.Finished += particles.QueueFree;
	}

	private void ScreenFlash(Color color, float duration)
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		var layer = new CanvasLayer { Layer = 50 };
		var flash = new ColorRect { Color = color with { A = 0.7f }, MouseFilter = Control.MouseFilterEnum.Ignore };
		flash.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		layer.AddChild(flash);
		scene.AddChild(layer);
		Tween tween = flash.CreateTween();
		tween.TweenProperty(flash, "color:a", 0f, duration);
		tween.TweenCallback(Callable.From(layer.QueueFree));
	}

	private void SpawnRing(Vector2 at, float radius)
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		// No clean ring: a torn wave of shards plus embers flung out.
		var wave = new OndaRota
		{
			Radius = radius,
			Color = _phaseTwo ? RotColor : ScarletGlow,
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			ZIndex = 6,
		};
		scene.AddChild(wave);
		wave.GlobalPosition = at;
		SpawnPetals(at, 18, radius, 0.45f, inward: false);
	}

	// The grab's slam into the floor: nothing tidy. A hard jolt and a rumble that dies out
	// unevenly, the floor splitting in jagged cracks, chunks of stone thrown up and raining back,
	// dust puffs going off around the hit at slightly different moments, a brief flash.
	private void SlamImpact(Metroidvania.Player.Player player, Vector2 ground, float dir)
	{
		if (DebugAI)
			GD.Print("[Artemisa] grab slam");
		player.ShakeCamera(24f, 0.35f);
		ScreenFlash(new Color(1f, 0.85f, 0.8f, 0.35f), 0.09f);
		_ = Rumble(player, 0.9f);
		SpawnCracks(ground);
		SpawnDebris(ground, dir);
		SpawnDust(ground);
		_ = DelayedDust(ground);
		SpawnPetals(ground + new Vector2(0f, -20f), 40, 150f, 0.7f, inward: false);
	}

	// Aftershocks: random kicks at random intervals, fading — not one smooth shake.
	private async Task Rumble(Metroidvania.Player.Player player, float duration)
	{
		float elapsed = 0.25f;
		while (elapsed < duration)
		{
			float gap = (float)_rng.RandfRange(0.05f, 0.11f);
			if (!await Wait(gap) || !IsInstanceValid(player))
				return;
			elapsed += gap;
			float fade = 1f - elapsed / duration;
			player.ShakeCamera((float)_rng.RandfRange(6f, 14f) * fade, 0.1f);
		}
	}

	private async Task DelayedDust(Vector2 ground)
	{
		for (int i = 0; i < 3; i++)
		{
			if (!await Wait((float)_rng.RandfRange(0.04f, 0.12f)))
				return;
			SpawnDust(ground + new Vector2((float)_rng.RandfRange(-90f, 90f), (float)_rng.RandfRange(-6f, 2f)));
		}
	}

	// Jagged cracks running out along the floor both ways: random segment lengths and kinks,
	// uneven widths, a few short branches; they linger a moment and fade.
	private void SpawnCracks(Vector2 at)
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		var root = new Node2D { ZIndex = 4 };
		scene.AddChild(root);
		root.GlobalPosition = at;
		var dark = new Color(0.06f, 0.04f, 0.04f, 0.95f);
		var hot = new Color(1f, 0.45f, 0.35f, 0.55f);
		int count = _rng.RandiRange(4, 6);
		for (int c = 0; c < count; c++)
		{
			float side = c % 2 == 0 ? 1f : -1f;
			float length = (float)_rng.RandfRange(70f, 190f);
			var crack = new Line2D
			{
				Width = (float)_rng.RandfRange(2f, 5f),
				DefaultColor = dark,
				WidthCurve = new Curve(),
			};
			crack.WidthCurve.AddPoint(new Vector2(0f, 1f));
			crack.WidthCurve.AddPoint(new Vector2(1f, 0.15f));
			Vector2 p = new((float)_rng.RandfRange(-10f, 10f), 0f);
			crack.AddPoint(p);
			float run = 0f;
			while (run < length)
			{
				float step = (float)_rng.RandfRange(8f, 24f);
				run += step;
				p += new Vector2(side * step, (float)_rng.RandfRange(-5f, 4f));
				crack.AddPoint(p);
				// The odd short branch splitting off.
				if (_rng.Randf() < 0.18f)
				{
					var branch = new Line2D { Width = crack.Width * 0.5f, DefaultColor = dark };
					branch.AddPoint(p);
					branch.AddPoint(p + new Vector2(side * (float)_rng.RandfRange(8f, 22f), (float)_rng.RandfRange(-10f, 6f)));
					root.AddChild(branch);
				}
			}
			root.AddChild(crack);
			// A thin hot glow along the first stretch of each crack, gone fast.
			var glow = new Line2D
			{
				Width = crack.Width * 0.5f,
				DefaultColor = hot,
				Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			};
			for (int i = 0; i < Mathf.Min(4, crack.GetPointCount()); i++)
				glow.AddPoint(crack.GetPointPosition(i));
			root.AddChild(glow);
			glow.CreateTween().TweenProperty(glow, "modulate:a", 0f, 0.35f);
		}
		Tween tween = root.CreateTween();
		tween.TweenInterval(1.1f);
		tween.TweenProperty(root, "modulate:a", 0f, 0.7f);
		tween.TweenCallback(Callable.From(root.QueueFree));
	}

	// Stone chunks: uneven sizes, spinning, thrown up mostly away from her and falling back hard.
	private void SpawnDebris(Vector2 at, float dir)
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		var image = Image.CreateEmpty(3, 3, false, Image.Format.Rgba8);
		image.Fill(Colors.White);
		var chunk = ImageTexture.CreateFromImage(image);
		var gradient = new Gradient();
		gradient.SetColor(0, new Color(0.62f, 0.56f, 0.5f, 1f));
		gradient.SetColor(1, new Color(0.3f, 0.26f, 0.24f, 0f));
		for (int burst = 0; burst < 2; burst++)
		{
			var debris = new GpuParticles2D
			{
				Amount = burst == 0 ? 34 : 14,
				Lifetime = 1.1f,
				OneShot = true,
				Explosiveness = 0.9f,
				Randomness = 0.6f,
				Texture = chunk,
				ProcessMaterial = new ParticleProcessMaterial
				{
					// The big burst leans away from her; the second one sprays both ways.
					Direction = burst == 0 ? new Vector3(dir * 0.45f, -1f, 0f) : new Vector3(0f, -1f, 0f),
					Spread = burst == 0 ? 50f : 75f,
					InitialVelocityMin = 180f,
					InitialVelocityMax = burst == 0 ? 520f : 340f,
					Gravity = new Vector3(0f, 1300f, 0f),
					AngularVelocityMin = -720f,
					AngularVelocityMax = 720f,
					AngleMin = 0f,
					AngleMax = 360f,
					ScaleMin = 0.8f,
					ScaleMax = 3.4f,
					ColorRamp = new GradientTexture1D { Gradient = gradient },
					EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
					EmissionBoxExtents = new Vector3(40f, 4f, 0f),
				},
				ZIndex = 6,
				Emitting = true,
			};
			scene.AddChild(debris);
			debris.GlobalPosition = at + new Vector2(0f, -4f);
			debris.Finished += debris.QueueFree;
		}
	}

	// ── Helpers ─────────────────────────────────────────────────────────────────────────────

	private void FaceTowardsPlayer()
	{
		var player = PlayerReads.Find(this);
		if (player is not null)
			FaceTowards(player.GlobalPosition.X);
	}

	private void FaceTowards(float x)
	{
		FacingRight = x >= GlobalPosition.X;
		Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);
	}

	// Waits at least one physics frame (so Wait(0) is "next frame").
	private async Task<bool> Wait(float seconds)
	{
		// A stagger bumps the serial: every move waiting right now gives up at once.
		int serial = _abortSerial;
		float elapsed = 0f;
		do
		{
			if (!IsInsideTree())
				return false;
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			if (!IsInstanceValid(this) || IsQueuedForRemoval || !IsInsideTree() || serial != _abortSerial)
				return false;
			elapsed += (float)GetPhysicsProcessDeltaTime();
		} while (elapsed < seconds);
		return true;
	}
}
