using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Metroidvania.Save;
using Metroidvania.Shared;
using Metroidvania.UI;
using Metroidvania.World;

namespace Metroidvania.Player;

public partial class Player : CharacterBody2D
{
	[Export] public float Speed = 200f;
	// Off: horizontal velocity only updates from input while grounded — a jump commits to whatever
	// velocity.X was the instant you left the floor, no steering mid-air. On (default) keeps the
	// current behavior (direction always drives velocity.X, grounded or not).
	[Export] public bool AirControl = true;
	[Export] public float JumpVelocity = -400f;
	[Export] public float Gravity = 900f;
	[Export] public float AttackHitboxDelay = 0.15f;
	[Export] public float AttackDuration = 0.27f;
	[Export] public float AttackHitboxReach = 50.6f;
	// Was -12 (well above the player's own body center) — shorter enemies (rats, slimes) whose
	// collision center sits much lower than the player's often ended up entirely below the
	// hitbox, reading as "the attack lands above the enemy." Closer to center is more forgiving
	// both ways instead of consistently biased upward.
	[Export] public float AttackHitboxYOffset = -4f;

	// The combo's third hit (the thrust) actually lands 3 separate hits, not one — the hitbox
	// re-activates 3 times, each landing its own hit-stop freeze pulse, so it reads as a
	// "thunk-thunk-thunk" flurry rather than a single stab. Each pulse briefly runs at normal
	// speed BEFORE freezing: Area2D overlap detection happens during _physics_process, and
	// dropping TimeScale immediately would starve that step of simulated delta, so the hit could
	// silently fail to register before the freeze even ends. The freeze timers explicitly ignore
	// Engine.TimeScale — otherwise a timer measuring the freeze's own duration would itself get
	// slowed down by the freeze it's producing.
	[Export] public float HitStopDetectWindow = 0.02f;
	[Export] public float HitStopFreezeScale = 0.02f;
	[Export] public float HitStopFreezeDuration = 0.07f;
	[Export] public float HitStopGapDuration = 0.03f;
	[Export] public float AttackAnimDuration = 0.62f;
	[Export] public float ComboResetWindow = 0.7f;
	// DS1 doesn't publish per-weapon stamina costs anywhere accessible (unlike DS3, where the
	// community datamined exact numbers). 15% is the midpoint of DS3's straight sword R1 cost
	// (~19.5 stamina out of a 91-160 bar depending on Endurance, i.e. ~12-21%) — the closest real
	// reference available, used deliberately as a stand-in since no DS1-specific figure exists.
	[Export] public int AttackStaminaCost = 15;
	[Export] public float CrouchWeaponTrailYOffset = 30f;
	[Export] public float HealAnimDuration = 0.8f;
	[Export] public float DashSpeed = 560f;
	[Export] public float DashDuration = 0.2f;
	[Export] public float DashIframeDuration = 0.28f;
	[Export] public float DashCooldown = 0.5f;
	// Neon afterimages left behind while dashing (see RunDashGhosts). Colours match the sword trails.
	[Export] public float DashGhostInterval = 0.035f;
	[Export] public float DashGhostLifetime = 0.22f;
	[Export] public float DashGhostAlpha = 0.85f;
	[Export] public float DashGhostIntensity = 1.8f;
	[Export] public Color DashGhostColor = new(0.15f, 0.65f, 1f, 1f);
	[Export] public Color DashGhostFireColor = new(1f, 0.55f, 0.2f, 1f);
	// Same afterimages, much fainter, while rising from a jump (see RunJumpGhosts). Same colours.
	[Export] public float JumpGhostInterval = 0.05f;
	[Export] public float JumpGhostLifetime = 0.16f;
	[Export] public float JumpGhostAlpha = 0.3f;
	[Export] public float JumpGhostIntensity = 1.1f;
	// Ground dust puffs ("jump_dust" on a ground takeoff, "land_dust" on touchdown); null = none.
	// Frames are expected with the dust's bottom edge at the frame's bottom (see SpawnDust).
	[Export] public SpriteFrames DustFrames;
	// Crouch beat on the floor before a ground jump lifts off (plays "jump_squat" when the sheet
	// has it; 0 or no clip = instant jump).
	[Export] public float JumpSquatDuration = 0.07f;
	// 25% of a 100 MaxStamina bar — matches Dark Souls 1's actual roll cost (community-measured: a
	// full bar gets you about 4 rolls, i.e. ~25% each). Dash is the one with i-frames (see
	// DashIframeDuration below), so it's the real analog to DS1's roll, not Roll itself (see its own
	// comment — Roll has no i-frames, it's a slower non-invincible alternative, a different move).
	[Export] public int DashStaminaCost = 25;
	// Slower than Dash but travels almost as far (~105px vs Dash's ~112px) and ground-only — a
	// slower, no-iframe alternative to Dash rather than a quicker one. RollDuration also has to
	// leave the "roll" clip (7 frames at speed 12) enough time to actually read as a roll instead
	// of getting cut after a frame or two.
	[Export] public float RollSpeed = 300f;
	[Export] public float RollDuration = 0.35f;
	[Export] public float RollCooldown = 0.35f;
	[Export] public int RollStaminaCost = 10;
	[Export] public float RunThrustSpeed = 420f;
	[Export] public float RunThrustDuration = 0.3f;
	[Export] public float RunThrustHitboxDelay = 0.05f;
	[Export] public float RunThrustCooldown = 0.6f;
	// A heavier committed attack than a normal swing — scaled up from AttackStaminaCost the same way
	// DS1's heavy/strong attacks cost noticeably more stamina than a light attack, not a documented
	// DS1 number itself.
	[Export] public int RunThrustStaminaCost = 23;
	[Export] public Vector2 RunThrustHitboxSize = new(105f, 44.1f);
	[Export] public float RunThrustSecondHitGap = 0.06f;
	// Brightness of the lunge's neon shockwave (see RunThrustEffects).
	[Export] public float RunThrustNeonIntensity = 1.5f;
	[Export] public float CrouchSpeedMultiplier = 0.5f;
	[Export] public float ParryWindowDuration = 0.2f;
	[Export] public float ParryBubbleFadeIn = 0.08f;
	[Export] public float ParryBubbleHold = 0.15f;
	[Export] public float ParryBubbleFadeOut = 0.3f;
	[Export] public float BlockStaminaCostFraction = 0.7f;
	// Dark Souls reduces (doesn't stop) stamina recovery by 80% while guard is up — same idea here.
	[Export] public float BlockStaminaRegenMultiplier = 0.2f;
	[Export] public SpriteFrames FireSpriteFrames;
	// Fire imbue look that keeps the same character: when set, entering fire swaps only the
	// sprite's material (e.g. a palette with a burning blade) and FireSpriteFrames is ignored.
	[Export] public Material FireMaterial;
	// Identity layer over the bought sheet (see CharacterFlair): cape, element-coloured eye glow and
	// element aura, placed with the per-frame neck/eye table measured on the sheet.
	[Export] public string BodyPointsPath = "res://resources/sprites/MainCharBodyPoints.json";
	[Export] public bool CapeEnabled = true;
	[Export] public bool EyeGlowEnabled = true;
	[Export] public bool ElementAuraEnabled = true;
	[Export] public Color CapeColor = new(0.141f, 0.149f, 0.18f);
	[Export] public Color CapeShadeColor = new(0.086f, 0.09f, 0.11f);
	[Export] public Color CapeOutlineColor = new(0.04f, 0.04f, 0.05f);
	// Poison/ice looks are built from FireMaterial at runtime with the blade's three swapped
	// colours (its last three palette entries) replaced by these, brightest first.
	[Export] public Color[] PoisonBladeColors = { new(0.914f, 1f, 0.69f), new(0.561f, 0.878f, 0.29f), new(0.243f, 0.541f, 0.173f) };
	[Export] public Color[] IceBladeColors = { new(0.95f, 0.99f, 1f), new(0.62f, 0.9f, 1f), new(0.29f, 0.56f, 0.82f) };
	// Neon tint of trails/afterimages per element (normal and fire use DashGhostColor/DashGhostFireColor).
	[Export] public Color PoisonElementTint = new(0.45f, 1f, 0.35f, 1f);
	[Export] public Color IceElementTint = new(0.62f, 0.93f, 1f, 1f);
	[Export] public Color LightningElementTint = new(1f, 0.9f, 0.2f, 1f);
	[Export] public Color[] LightningBladeColors = { new(1f, 1f, 0.85f), new(1f, 0.9f, 0.3f), new(0.85f, 0.6f, 0.1f) };
	// Fire/ice/lightning trails throw this many times more sparks than normal (see PrepareTrailSparks).
	[Export] public float ElementSparkMultiplier = 2.5f;
	// Lightning imbue: seconds between the little bolts crackling off a running trail.
	[Export] public float LightningBoltInterval = 0.045f;
	[Export] public float TransformationInDuration = 1.5f;
	[Export] public float TransformationOutDuration = 0.9f;
	[Export] public float ChargedAttackDuration = 2f;
	[Export] public Vector2 ChargedAttackHitboxSize = new(90f, 50f);
	[Export] public float ChargedAttackReach = 60f;
	// Blink tint shown once the charge is full (the only charge feedback).
	[Export] public Color ChargedReadyBlinkColor = new(1f, 0.25f, 0.25f, 1f);
	// Sword-tip path for the charged attack's neon trail, in the "charged_attack" sprite frame's
	// own pixel space (top-left origin). Measured per sprite sheet, so each skin scene sets its
	// own; empty = no trail.
	[Export] public Vector2[] ChargedTrailPath = System.Array.Empty<Vector2>();
	// How long the trail takes to draw along ChargedTrailPath — should match the frames where the
	// blade actually sweeps in the sheet.
	[Export] public float ChargedTrailDrawTime = 0.14f;
	// Same idea for the air combo (air_attack1..3): blade-tip paths in each clip's frame-pixel
	// space, plus when the swing starts and how long it takes. Empty path = falls back to the
	// matching ground hit's procedural trail.
	[Export] public Vector2[] AirAttack1TrailPath = System.Array.Empty<Vector2>();
	[Export] public Vector2[] AirAttack2TrailPath = System.Array.Empty<Vector2>();
	[Export] public Vector2[] AirAttack3TrailPath = System.Array.Empty<Vector2>();
	// x = delay before drawing, y = draw time (seconds), per air hit.
	[Export] public Vector2 AirAttack1TrailTiming = new(0f, 0.1f);
	[Export] public Vector2 AirAttack2TrailTiming = new(0f, 0.18f);
	[Export] public Vector2 AirAttack3TrailTiming = new(0.08f, 0.16f);
	// Same for the ground combo's third hit ("attack3" clip); timing counted from the attack's start.
	// Empty path = the procedural thrust trail.
	[Export] public Vector2[] Attack3TrailPath = System.Array.Empty<Vector2>();
	[Export] public Vector2 Attack3TrailTiming = new(0.1f, 0.08f);
	// The combo's 4th hit (rising slash, "attack4" clip) — measured on the MainCharacter pack.
	[Export] public Vector2[] Attack4TrailPath = { new(118, 58), new(123, 44), new(124, 28), new(110, 10), new(90, 2), new(74, 4) };
	[Export] public Vector2 Attack4TrailTiming = new(0.16f, 0.09f);
	// Down-attack (pound) trails, measured on the MainCharacter pack: the blade point the falling
	// line follows (in the "down_attack_loop" frame's pixel space), and the slam crescent drawn on
	// touchdown ("down_attack_landing" clip, path + delay/draw time).
	[Export] public Vector2 PoundTrailBladePixel = new(68f, 62f);
	[Export] public Vector2[] PoundLandingTrailPath = { new(100, 46), new(118, 52), new(132, 68), new(134, 82), new(120, 92), new(98, 95) };
	[Export] public Vector2 PoundLandingTrailTiming = new(0f, 0.06f);
	// How long the slam pose holds after bouncing off an enemy before normal air animations resume.
	[Export] public float PoundBounceSlamHold = 0.14f;
	// Opening the ground combo while moving faster than this fraction of Speed uses "run_attack"
	// (when the sheet has it) instead of attack1, with its own blade-tip trail.
	[Export] public float RunAttackSpeedThreshold = 0.3f;
	// Default measured on the MainCharacter pack's MainChar_RunAttack sheet (kept here rather than
	// in Player.tscn to avoid another on-disk scene edit while the editor has it open).
	[Export] public Vector2[] RunAttackTrailPath = { new(72, 14), new(98, 38), new(126, 54), new(143, 63), new(126, 70), new(96, 72), new(72, 67) };
	[Export] public Vector2 RunAttackTrailTiming = new(0.17f, 0.08f);
	// attack3's thrust shakes the sprite back and forth while the blade is out (reads as a burst
	// of stabs over its 3 damage pulses). Starts when the hitbox opens.
	[Export] public float ThrustVibrationAmplitude = 2.5f;
	[Export] public float ThrustVibrationDuration = 0.18f;
	[Export] public float ThrustVibrationInterval = 0.03f;
	// Attacking from a ladder ("ladder_attack" clip): when the hitbox opens (to match the sheet's
	// swing frame) and the blade-tip trail path/timing for that swing.
	[Export] public float LadderAttackHitDelay = 0.15f;
	[Export] public Vector2[] LadderAttackTrailPath = System.Array.Empty<Vector2>();
	[Export] public Vector2 LadderAttackTrailTiming = new(0.15f, 0.1f);
	// Same for the crouch attack ("crouch_attack" clip). Timing x is counted from when the hitbox
	// opens (AttackHitboxDelay into the attack). Empty path = the old pivot-traced arc.
	[Export] public Vector2[] CrouchTrailPath = System.Array.Empty<Vector2>();
	[Export] public Vector2 CrouchTrailTiming = new(0f, 0.08f);
	[Export] public float PoundSpeed = 500f;
	[Export] public float PoundBounceVelocity = -420f;
	[Export] public Vector2 PoundHitboxSize = new(50f, 70f);
	[Export] public float PoundHitboxReach = 40f;
	[Export] public Vector2 UpAttackHitboxSize = new(46f, 90f);
	[Export] public float UpAttackHitboxReach = 40f;
	[Export] public float UpAttackCooldown = 0.4f;
	// The up attack reuses air_attack1's trail path scaled out from this point (the body's centre,
	// in that clip's frame pixels) so the streak reaches as far as the up hitbox does.
	[Export] public Vector2 UpAttackTrailPivot = new(92f, 60f);
	[Export] public float UpAttackTrailReachScale = 1.35f;
	// Animation-only for now — no effect/projectile is spawned yet, this just gets the two existing
	// "spell"/"spell2" clips playable; actual spell effects get attached to these slots later.
	// Duration matches each clip's frame_count / speed (see FireWarriorSpriteFrames.tres).
	[Export] public float SpellAnimDuration = 0.9f;
	[Export] public float Spell2AnimDuration = 1.7f;
	// Spell 1 = shadow clone (see CastSpell / ShadowClone).
	[Export] public int ShadowCloneStaminaCost = 20;
	[Export] public float ShadowCloneCooldown = 1.2f;
	[Export] public float ShadowCloneSpawnDelay = 0.3f;
	[Export] public float ShadowCloneOffsetX = 34f;
	[Export] public float ShadowCloneAlpha = 0.8f;
	[Export] public float ShadowCloneIntensity = 1.6f;
	[Export] public float ShadowCloneFadeIn = 0.08f;
	[Export] public float ShadowCloneFadeOut = 0.2f;
	// Tornado spell (key 1, "spell_tornado"): a procedural Tornado that travels forward, hitting every
	// enemy inside it each tick for AttackPower × TornadoDamageMultiplier (it never moves them).
	[Export] public int TornadoStaminaCost = 30;
	[Export] public float TornadoCooldown = 3f;
	[Export] public float TornadoCastDelay = 0.35f;
	[Export] public float TornadoOffsetX = 40f;
	[Export] public float TornadoDamageMultiplier = 0.5f;
	// Variant tuning (see SpawnTornadoVariant). Double: second tornado this far beyond the first.
	[Export] public float DoubleTornadoGap = 70f;
	// Mini: three small airborne tornadoes fanned out at these angles (degrees, negative = upward),
	// launched from this height above the player's centre.
	[Export] public float[] MiniTornadoAngles = { -35f, -12f, 10f };
	[Export] public float MiniTornadoScale = 0.4f;
	[Export] public float MiniTornadoDamageMultiplier = 0.5f;
	[Export] public float MiniTornadoLifetime = 2f;
	[Export] public float MiniTornadoLaunchHeight = 10f;
	// Growing: starts this small and grows with each enemy it touches (see Tornado.GrowPerEnemy).
	[Export] public float GrowingTornadoStartScale = 0.4f;
	// Wind shield (key 7): a tornado wrapped around the player — fully invulnerable (no damage,
	// knockback or poison) and no sword while it lasts; it also grazes whatever touches it.
	[Export] public float WindShieldDuration = 2f;
	[Export] public int WindShieldStaminaCost = 40;
	[Export] public float WindShieldCooldown = 8f;
	[Export] public float WindShieldDamageMultiplier = 0.25f;
	// Hurricane super (key 8): a screen-wide tornado ticking fast over everything on screen
	// (bosses included). Needs this fraction of a full stamina bar and spends all of it.
	[Export] public float HurricaneDuration = 2.2f;
	[Export] public float HurricaneTickInterval = 0.1f;
	[Export] public float HurricaneDamageMultiplier = 0.35f;
	[Export] public float HurricaneCooldown = 30f;
	[Export] public float HurricaneStaminaFraction = 1f;
	// Poison spit (key 2, "spell_poison"): the spiders' lobbed poison glob, thrown forward — hits for
	// AttackPower on impact, poisons the enemy, and leaves a puddle that poisons enemies walking in.
	[Export] public PackedScene PoisonSpitScene = GD.Load<PackedScene>("res://scenes/world/Rehusables/PoisonSpit.tscn");
	[Export] public int PoisonSpitStaminaCost = 15;
	[Export] public float PoisonSpitCooldown = 0.8f;
	[Export] public float PoisonSpitCastDelay = 0.25f;
	[Export] public Vector2 PoisonSpitVelocity = new(320f, -170f);
	[Export] public float PoisonSpitSpawnYOffset = -8f;
	[Export] public int PoisonSpitTickDamage = 4;
	// Fireball (key 3, "spell_fireball"): a procedural Fireball fired straight ahead that bursts on
	// impact, burning everything in its blast for AttackPower × FireballDamageMultiplier.
	[Export] public int FireballStaminaCost = 20;
	[Export] public float FireballCooldown = 0.9f;
	[Export] public float FireballCastDelay = 0.25f;
	[Export] public float FireballDamageMultiplier = 1.4f;
	[Export] public float FireballSpawnYOffset = -12f;
	// Bastón summons: 4 cycles the selected slot, 5 summons it (one shadow attack on the nearest
	// enemy, costs 1 use + stamina), 6 discards the selected slot (asks which).
	[Export] public int ShadowSummonStaminaCost = 10;
	[Export] public float ShadowSummonCooldown = 0.6f;
	[Export] public float ShadowSummonDamageMultiplier = 1f;
	[Export] public float ShadowSummonSearchRadius = 380f;
	[Export] public float ShadowSummonReach = 50f;
	[Export] public float ShadowSummonIntensity = 1.6f;
	[Export] public Color ShadowSummonColor = new(0.55f, 0.35f, 1f, 1f);
	// Flying sword (see ThrowFlyingSword): release moment into the throw clip, how far it flies
	// before boomeranging back, and the height (from the player's centre) it travels/is caught at.
	[Export] public int FlyingSwordStaminaCost = 15;
	[Export] public float FlyingSwordReleaseDelay = 0.2f;
	[Export] public float FlyingSwordRange = 240f;
	[Export] public float FlyingSwordHeight = -14f;
	[Export] public float FlyingSwordDamageMultiplier = 0.8f;
	[Export] public float BowAnimDuration = 0.5f;
	[Export] public float BowReleaseDelay = 0.35f;
	[Export] public float ArrowSpeed = 420f;
	[Export] public float ArrowSpawnYOffset = -20f;
	[Export] public PackedScene ArrowProjectileScene;
	[Export] public float LookUpOffset = -80f;
	[Export] public float LookDownOffset = 60f;
	[Export] public float LookSmoothSpeed = 4f;
	[Export] public float BaseCameraOffsetY = -60f;
	public float ProfileCameraOffsetY { get; set; }
	public float ProfileZoom { get; set; } = 1.78f;

	// Read-only combat state for enemy AI to react to (see EnemyCombatCoordinator) — an enemy
	// that can tell whether the player is blocking, mid-swing, or dashing can make an actual
	// tactical choice instead of just swinging whenever it's in range.
	public bool IsBlocking => _isBlocking;
	public bool IsAttacking => _attacking;
	public bool IsDashing => _isDashing;
	// Drinking the heal flask (read by enemies that punish healing, e.g. MushroomBoss).
	public bool IsHealing => _healing;
	public bool IsFacingRight => _facingRight;
	[Export] public float BossZoomDistance = 500f;
	[Export] public float BossZoomInMultiplier = 0.75f;
	[Export] public float ZoomSmoothSpeed = 3f;
	// Keep a boss that goes high above the player in frame (camera shifts up + pulls back).
	[Export] public float BossFramingThreshold = 120f;
	[Export] public float BossFramingMargin = 90f;
	[Export] public float BossFramingMinZoom = 0.8f;
	private float _bossFramingOffsetY;
	[Export] public float HeadLookUpAngle = -25f;
	[Export] public float WeaponRestAngle = -20f;
	[Export] public float WeaponSwingStartAngle = -70f;
	[Export] public float WeaponSwingEndAngle = 60f;
	// How far from WeaponPivot's origin the traced blade tip sits. A screen-captured playtest
	// showed 62 reading as a detached blob (that was also before the width got fixed) — 52 with
	// today's much thinner line reads as a longer, further-out arc without that problem.
	[Export] public float SwordArcTrailRadius = 52f;
	// Minimum tip movement (px) before a new trail point is recorded — see RunSwordArcTrail's
	// hit-stop comment for why this matters.
	[Export] public float MinSwordArcPointSpacing = 1.5f;
	// attack2's own quick circular spin trail (see RunSwordSpinTrail) — shorter and a much
	// wider sweep than attack1's arc, on purpose, so it reads as its own move.
	[Export] public float SwordSpinDuration = 0.2f;
	[Export] public float SwordSpinDegrees = 280f;
	// Its own radius (bigger than SwordArcTrailRadius) instead of sharing attack1's — this arc
	// is meant to read as a bit more generous/dramatic than attack1's, not the same size.
	[Export] public float SwordSpinRadius = 56f;
	// attack3's own forward-piercing thrust trail (see RunSwordThrustTrail).
	[Export] public float SwordThrustReach = 70f;
	[Export] public float SwordThrustExtendDuration = 0.08f;
	[Export] public float SwordThrustHoldDuration = 0.1f;
	// Crouch attack's own trail (see RunCrouchArcTrail): unlike attack2's invented circle, this
	// actually swings _weaponPivot through this shallow angle range and traces THAT real motion,
	// same idea as attack1 — just a smaller, lower arc matching a quick crouched horizontal cut.
	// A video capture showed 42 leaving a visible gap between the character and the whole arc
	// (the arc only ever draws its 42-unit-out edge, never anything closer), reading as a
	// detached floating blob instead of a cut coming off the blade — much smaller fixes that.
	[Export] public float CrouchArcRadius = 24f;
	[Export] public float CrouchSwingStartAngle = -20f;
	[Export] public float CrouchSwingEndAngle = 55f;
	[Export] public float CrouchArcDuration = 0.16f;
	[Export] public float KnockbackDuration = 0.35f;
	// Pop straight up on every hit taken (regardless of the attack's own direction) so it reads as
	// a Hollow Knight-style launch-and-arc instead of a flat horizontal shove — gravity takes over
	// from here once applied, same as any other airborne velocity.
	[Export] public float KnockbackUpwardVelocity = 260f;
	[Export] public float FallDeathY = 700f;
	[Export] public float MaxAirborneTime = 15f;
	[Export] public float SprintSpeedMultiplier = 1.6f;
	[Export] public float WalkSwingAmplitudeDeg = 18f;
	[Export] public float RunSwingAmplitudeDeg = 30f;
	[Export] public float WalkCycleSpeed = 8f;
	[Export] public float JumpLegAngleDeg = -15f;
	[Export] public float JumpArmAngleDeg = 20f;
	[Export] public float ClimbSpeed = 130f;
	[Export] public float SwimSpeed = 100f;
	// How far the player's origin rests above the water surface once floating — negative dips
	// the origin below the surface line instead, submerging more of the body. -22 leaves roughly
	// the top ~10px of the 64px-tall collision box (head/neck area) above water.
	[Export] public float SwimFloatOffset = -22f;
	[Export] public float SwimFloatCorrectionSpeed = 250f;
	[Export] public float ClimbHorizontalSpeedMultiplier = 0.6f;
	[Export] public float LadderJumpLockoutDuration = 0.25f;
	[Export] public float InputBufferWindow = 0.15f;
	[Export] public float WallClimbSpeed = 110f;
	[Export] public float WallSlideSpeed = 60f;
	[Export] public float WallJumpVelocityX = 260f;
	[Export] public float WallJumpVelocityY = -380f;
	[Export] public float WallJumpLockoutDuration = 0.2f;
	[Export] public float LedgeGrabOffsetX = 16f;
	[Export] public float LedgeSurfaceProbeHeight = 70f;
	[Export] public float LedgeHangDropY = 26f;
	[Export] public float LedgeClimbForwardX = 26f;
	[Export] public float LedgeClimbDuration = 0.35f;
	// With a dedicated "ledge_climb" clip the body only travels up during these frames of it
	// (the sheet hangs/pulls in place first, then tucks over the edge); the climb ends when the
	// clip does, so LedgeClimbDuration only applies to sheets without that clip.
	[Export] public int LedgeClimbMoveStartFrame;
	[Export] public int LedgeClimbMoveEndFrame = 1;
	// Nudges only the sprite toward the wall while hanging (eased back out as the climb moves up),
	// for sheets whose hang pose sits back from the frame centre — the body itself stays
	// LedgeGrabOffsetX away so its collider never overlaps the wall.
	[Export] public float LedgeHangVisualShiftX;
	[Export] public float LedgeGrabLockoutDuration = 0.3f;
	[Export] public float LedgeAutoClimbDelay = 0.15f;
	[Export] public float DropThroughDuration = 0.3f;
	[Export] public float DropThroughLedgeLockoutDuration = 0.5f;

	private Hitbox _hitbox;
	private Stats _stats;
	private PlayerAbilities _abilities;
	private HealFlask _healFlask;
	private Label _healChargesLabel;
	private Label _goldLabel;
	private Node2D _visual;
	private Node2D _head;
	private Node2D _weaponPivot;
	private AnimatedSprite2D _weaponTrail;
	private AnimatedSprite2D _crystalEffect;
	private Material _weaponTrailBaseMaterial;
	private float _weaponTrailBaseX;
	private float _weaponTrailBaseY;
	private Line2D _swordArcGlow;
	private Line2D _swordArcCore;
	private GpuParticles2D _swordArcSparks;
	private readonly List<Vector2> _swordArcPoints = new();
	private int _swordArcRunId;
	private float _swordArcBaseCoreWidth;
	private float _swordArcBaseGlowWidth;
	private RectangleShape2D _hitboxShape;
	private Vector2 _hitboxBaseSize;
	private Node2D _legLeft;
	private Node2D _legRight;
	private Node2D _armLeft;
	private Node2D _armRight;
	private float _walkPhase;
	private float _lastStepSin;
	private bool _wasAirborne;
	private CollisionShape2D _standCollision;
	private CollisionShape2D _crouchCollision;
	private Camera2D _camera;
	private Vector2 _lookOffset;
	private float _shakeTimer;
	private float _shakeDuration;
	private float _shakeStrength;
	private AnimatedSprite2D _sprite;
	private HudBar _bossHealthBar;
	private Node2D _trackedBoss;
	private Enemy _trackedBossEnemy;
	private bool _bossEncounterActive;
	private PointLight2D _debugFlashlight;
	private PointLight2D _torchLight;
	private PointLight2D _swordFireLight;
	private bool _torchEquipped;
	private bool _equipTorchKeyReleased = true;
	private const int ComboHitCount = 4;

	// Combo length is unlocked hit by hit (Maestro de Espadas: ComboFinisher = 3rd, ComboFourth =
	// 4th), and capped to the attackN clips the sheet actually has.
	private int EffectiveComboHitCount
	{
		get
		{
			int available = 1;
			while (available < ComboHitCount && _sprite.SpriteFrames.HasAnimation($"attack{available + 1}"))
				available++;
			// Two hits are always there; ComboFinisher adds the 3rd, ComboFourth the 4th on top of it.
			int unlocked = 2;
			if (_abilities.Has(PlayerAbilities.ComboFinisher))
			{
				unlocked = 3;
				if (_abilities.Has(PlayerAbilities.ComboFourth))
					unlocked = 4;
			}
			return Mathf.Max(1, Mathf.Min(available, unlocked));
		}
	}

	private bool _facingRight = true;
	private bool _attacking;
	private bool _healing;
	private int _comboStep;
	// Separate counter for the airborne combo (air_attack1..3) so a jump doesn't eat the ground
	// combo's progress; reset on touchdown.
	private int _airComboStep;
	private int _jumpGhostRunId;
	private float _jumpSquatTimer;
	private bool _isLadderAttacking;
	private float _comboResetTimer;
	private string _currentAttackAnimation = "attack1";
	private int _jumpCount;
	private bool _isDoubleJumping;
	private bool _lowStaminaWarned;
	private const float LowStaminaRatio = 0.15f;
	private bool _infiniteJumpDebug;
	private bool _isDashing;
	private bool _canDash = true;
	private bool _crouching;
	private float _dashDirection;
	private bool _isRolling;
	private bool _canRoll = true;
	private float _rollDirection;
	private bool _isRunThrusting;
	private bool _canRunThrust = true;
	private float _runThrustDirection;
	private bool _canUpAttack = true;
	private bool _isShooting;
	// Which clip the shooting state plays (flying sword phases); null = the bow's "shoot_bow".
	private string _shootAnimation;
	private bool _swordOut;
	private bool _isCastingSpell;
	// Engine time (seconds) when spell 1 can be cast again — a timestamp, so it keeps counting down
	// through states whose physics branch returns early (ledge hang, pound...).
	private double _shadowCloneReadyAt;
	private double _tornadoReadyAt;
	private TornadoVariant _tornadoVariant;
	private bool _windShieldActive;
	private double _windShieldReadyAt;
	private double _hurricaneReadyAt;
	private Metroidvania.UI.TornadoVariantsHud _tornadoHud;
	private double _poisonSpitReadyAt;
	private double _fireballReadyAt;
	private double _summonReadyAt;
	private string _selectedSummon;
	private Metroidvania.UI.ShadowSlotsHud _summonHud;
	private GoldHud _goldHud;
	private bool _isCastingSpell2;
	private bool _isBlocking;
	private float _parryWindowTimer;
	private AnimatedSprite2D _parryFlash;
	private Sprite2D _parryBubble;
	private Tween _parryBubbleTween;
	private SpriteFrames _normalSpriteFrames;
	private Material _normalMaterial;
	private bool _isFireImbued;
	private bool _isTransforming;
	private bool _fireImbueKeyReleased = true;
	// The attack element (F) cycles Normal -> Fire -> Poison -> Ice -> Lightning -> Normal. Each one
	// recolors the blade (palette material) and every neon effect (ElementTint/ElementGhostColor);
	// _isFireImbued stays true only for fire (torch/sword-fire light/fire sprite trails).
	private DamageElement _currentElement = DamageElement.Normal;
	private ShaderMaterial _poisonMaterial;
	private ShaderMaterial _iceMaterial;
	private ShaderMaterial _lightningMaterial;
	private ParticleProcessMaterial _baseSparkProcess;
	private ParticleProcessMaterial _fireSparkProcess;
	private ParticleProcessMaterial _iceSparkProcess;
	private ParticleProcessMaterial _lightningSparkProcess;
	private float _baseSparkLifetime;
	private float _lightningBoltTimer;
	private bool _isCharging;
	private bool _isPounding;
	private bool _poundHitLanded;
	private float _knockbackTimer;
	private Vector2 _knockbackVelocity;
	private bool _isDead;
	private float _airborneTimer;
	private Ladder _ladder;
	private bool _isClimbing;
	private float _ladderGrabLockout;
	private Water _water;
	private bool _isSwimming;
	private Rope _ropeInZone;
	private Rope _rope;
	private bool _isSwinging;
	// The rope just jumped off of — refusing to re-grab specifically this one (rather than just
	// waiting out a timer) means it doesn't matter how slow the release velocity happens to be at
	// that point in the swing; you're only blocked from THIS rope, and only until you've actually
	// left its grab zone once, so aiming for a neighboring rope is never held up by it.
	private Rope _lastReleasedRope;
	private bool _isWallClimbing;
	private float _wallJumpLockout;
	private ShapeCast2D _ledgeWallCheck;
	private bool _isLedgeHanging;
	private bool _isLedgeClimbing;
	private float _ledgeClimbTimer;
	private float _ledgeGrabLockout;
	private float _ledgeHangTimer;
	private Vector2 _ledgeClimbStartPosition;
	private Vector2 _ledgeClimbEndPosition;
	private float _ledgeSurfaceY;
	private float _standHalfHeight;
	private float _attackBufferTimer;
	private float _dashBufferTimer;
	private float _rollBufferTimer;

	public override void _Ready()
	{
		_hitbox = GetNode<Hitbox>("AttackHitbox");
		_hitboxShape = GetNode<CollisionShape2D>("AttackHitbox/CollisionShape2D").Shape as RectangleShape2D;
		_hitboxBaseSize = _hitboxShape.Size;
		_stats = GetNode<Stats>("Stats");
		_abilities = GetNode<PlayerAbilities>("Abilities");
		_healFlask = GetNode<HealFlask>("HealFlask");
		_visual = GetNode<Node2D>("Visual");
		_head = GetNode<Node2D>("Visual/Head");
		_weaponPivot = GetNode<Node2D>("Visual/WeaponPivot");
		_weaponTrail = GetNode<AnimatedSprite2D>("Visual/WeaponTrail");
		_weaponTrailBaseMaterial = _weaponTrail.Material;
		_weaponTrail.AnimationFinished += () =>
		{
			_weaponTrail.Visible = false;
			_weaponTrail.Material = _weaponTrailBaseMaterial;
		};
		_swordArcGlow = GetNode<Line2D>("Visual/SwordArcGlow");
		_swordArcCore = GetNode<Line2D>("Visual/SwordArcCore");
		_swordArcSparks = GetNode<GpuParticles2D>("Visual/SwordArcSparks");
		BuildElementSparkProcesses();
		var swordArcWidthTaper = new Curve();
		swordArcWidthTaper.AddPoint(new Vector2(0f, 0.2f));
		swordArcWidthTaper.AddPoint(new Vector2(1f, 1f));
		_swordArcGlow.WidthCurve = swordArcWidthTaper;
		_swordArcCore.WidthCurve = swordArcWidthTaper;
		_swordArcBaseCoreWidth = _swordArcCore.Width;
		_swordArcBaseGlowWidth = _swordArcGlow.Width;
		_weaponTrailBaseX = _weaponTrail.Position.X;
		_weaponTrailBaseY = _weaponTrail.Position.Y;
		_crystalEffect = GetNode<AnimatedSprite2D>("Visual/CrystalEffect");
		// "fall" loops for the whole descent, so AnimationFinished fires once per loop — only
		// the one-shot "break" should actually hide the node when it finishes.
		_crystalEffect.AnimationFinished += () =>
		{
			if (_crystalEffect.Animation == "break")
				_crystalEffect.Visible = false;
		};
		_parryFlash = GetNode<AnimatedSprite2D>("Visual/ParryFlash");
		_parryFlash.AnimationFinished += () => _parryFlash.Visible = false;
		_parryBubble = GetNode<Sprite2D>("Visual/ParryBubble");
		_legLeft = GetNode<Node2D>("Visual/LegLeft");
		_legRight = GetNode<Node2D>("Visual/LegRight");
		_armLeft = GetNode<Node2D>("Visual/ArmLeft");
		_armRight = GetNode<Node2D>("Visual/ArmRight");
		_standCollision = GetNode<CollisionShape2D>("CollisionShape2D");
		_crouchCollision = GetNode<CollisionShape2D>("CrouchCollisionShape2D");
		_ledgeWallCheck = GetNode<ShapeCast2D>("LedgeWallCheck");
		_standHalfHeight = ((RectangleShape2D)_standCollision.Shape).Size.Y / 2f;
		_camera = GetNode<Camera2D>("Camera2D");
		_sprite = GetNode<AnimatedSprite2D>("Visual/CharacterSprite");
		var bodyPoints = CharacterFlair.LoadPoints(BodyPointsPath);
		if (bodyPoints.Count > 0)
		{
			var flair = new CharacterFlair
			{
				Name = "CharacterFlair",
				CapeEnabled = CapeEnabled,
				EyeGlowEnabled = EyeGlowEnabled,
				AuraEnabled = ElementAuraEnabled,
				CapeColor = CapeColor,
				CapeShadeColor = CapeShadeColor,
				CapeOutlineColor = CapeOutlineColor,
			};
			flair.Setup(_sprite, _visual, this, bodyPoints, () => _currentElement, ElementGhostColor);
			AddChild(flair);
		}
		_debugFlashlight = GetNode<PointLight2D>("DebugFlashlight");
		if (!OS.HasFeature("debug"))
		{
			_debugFlashlight.QueueFree();
			_debugFlashlight = null;
		}
		_torchLight = GetNode<PointLight2D>("Visual/TorchLight");
		_swordFireLight = GetNode<PointLight2D>("Visual/SwordFireLight");
		_normalSpriteFrames = _sprite.SpriteFrames;
		_normalMaterial = _sprite.Material;
		_poisonMaterial = BuildBladeVariant(PoisonBladeColors, 0.22f);
		_iceMaterial = BuildBladeVariant(IceBladeColors, 0.08f);
		_lightningMaterial = BuildBladeVariant(LightningBladeColors, 0.35f);
		_stats.Died += OnDied;
		_stats.IncomingHitInterceptor = TryBlockIncomingHit;

		if (SaveManager.Instance.SessionCurrentHealth.HasValue)
			_stats.SetCurrentHealth(SaveManager.Instance.SessionCurrentHealth.Value);
		if (SaveManager.Instance.SessionHealCharges.HasValue)
			_healFlask.SetCurrentCharges(SaveManager.Instance.SessionHealCharges.Value);

		_stats.HealthChanged += (current, max) => SaveManager.Instance.SessionCurrentHealth = current;
		_healFlask.ChargesChanged += (current, max) => SaveManager.Instance.SessionHealCharges = current;

		if (SaveManager.Instance.SessionElement.HasValue)
		{
			_currentElement = SaveManager.Instance.SessionElement.Value;
			// Restoring straight into the post-transform state, not replaying CycleElement's
			// transform-in animation — that's only meant to play once, at the moment the
			// player actually presses the button, not every time a scene loads.
			ApplyElementLook(_currentElement);
			_isFireImbued = _currentElement == DamageElement.Fire;
		}
		if (SaveManager.Instance.SessionTorchEquipped.HasValue)
			_torchEquipped = SaveManager.Instance.SessionTorchEquipped.Value;

		HudBar healthBar = GetNode<HudBar>("HUD/VBox/HealthBar");
		HudBar staminaBar = GetNode<HudBar>("HUD/VBox/StaminaBar");
		_bossHealthBar = GetNode<HudBar>("HUD/BossHealthBar");
		_healChargesLabel = GetNode<Label>("HUD/VBox/HealChargesLabel");
		_goldLabel = GetNode<Label>("HUD/VBox/GoldLabel");
		_stats.HealthChanged += (current, max) => healthBar.SetRatio((float)current / max);
		_stats.StaminaChanged += (current, max) => staminaBar.SetRatio((float)current / max);

		// Jekyria-styled health/stamina (emblem orb + ornate rails) at the top of the HUD; the plain
		// bars stay in the scene, hidden, so nothing else that references them breaks.
		var statusBars = new JekyriaStatusBars { Name = "JekyriaStatusBars" };
		Node hudBox = GetNode("HUD/VBox");
		hudBox.AddChild(statusBars);
		hudBox.MoveChild(statusBars, 0);
		healthBar.Visible = false;
		staminaBar.Visible = false;
		_stats.HealthChanged += (current, max) =>
		{
			statusBars.SetMaxHealth(max);
			statusBars.SetHealthRatio((float)current / max);
		};
		statusBars.SetMaxHealth(_stats.MaxHealth);
		_stats.StaminaChanged += (current, max) => statusBars.SetStaminaRatio((float)current / max);
		statusBars.SetHealthRatio((float)_stats.CurrentHealth / _stats.MaxHealth);
		statusBars.SetStaminaRatio((float)_stats.CurrentStamina / _stats.MaxStamina);
		// Edge-triggered on crossing INTO the low band (not every frame while under it) — plays
		// once per drop, resets once stamina climbs back above the threshold so it can fire again
		// next time it runs low.
		_stats.StaminaChanged += (current, max) =>
		{
			bool isLow = (float)current / max <= LowStaminaRatio;
			if (isLow && !_lowStaminaWarned)
				Sfx.Play(this, "Voice/Player", "EstaminaInsuficiente");
			_lowStaminaWarned = isLow;
		};
		healthBar.SetRatio((float)_stats.CurrentHealth / _stats.MaxHealth);
		staminaBar.SetRatio((float)_stats.CurrentStamina / _stats.MaxStamina);
		_stats.HitTaken += (isProjectile) => FlashHit(_visual, new Color(1f, 0.2f, 0.2f));
		_stats.HitTaken += (isProjectile) => Sfx.Play(this, "Voice/Player", "Damage Grunt (Male)");
		_hitbox.HitDealt += () => FlashHit(_visual, new Color(1f, 1f, 0.2f));
		_hitbox.HitDealt += () => Sfx.Play(this, "Combat/Sword", "Sword Impact Hit");
		_healFlask.ChargesChanged += (current, max) => UpdateHealChargesLabel(current, max);
		UpdateHealChargesLabel(_healFlask.CurrentCharges, _healFlask.MaxCharges);
		// Heal charges as the Jekyria red potion bar (one pip per charge) under the status bars; the old
		// "Curas: x/y" label stays in the scene, hidden.
		var potionBar = new JekyriaPotionBar { Name = "JekyriaPotionBar" };
		hudBox.AddChild(potionBar);
		hudBox.MoveChild(potionBar, 1);
		_healChargesLabel.Visible = false;
		_healFlask.ChargesChanged += (current, max) => potionBar.SetCharges(current, max);
		potionBar.SetCharges(_healFlask.CurrentCharges, _healFlask.MaxCharges);
		LocaleManager.Instance.LocaleChanged += _ => UpdateHealChargesLabel(_healFlask.CurrentCharges, _healFlask.MaxCharges);

		SaveManager.Instance.GoldChanged += UpdateGoldLabel;
		UpdateGoldLabel(SaveManager.Instance.Gold);
		// Gold now lives in the top-right corner (bag icon + Jekyria digits); the old text label stays
		// in the scene, hidden.
		_goldHud = new GoldHud { Name = "GoldHud" };
		GetNode("HUD").AddChild(_goldHud);
		_goldHud.SetGold(SaveManager.Instance.Gold);
		_goldLabel.Visible = false;
		LocaleManager.Instance.LocaleChanged += _ => UpdateGoldLabel(SaveManager.Instance.Gold);

		// Bastón mini HUD: 5 slot squares in the bottom-left corner of the HUD layer.
		_summonHud = new Metroidvania.UI.ShadowSlotsHud { Name = "ShadowSlotsHud" };
		GetNode("HUD").AddChild(_summonHud);
		SaveManager.Instance.ShadowUsesChanged += UpdateSummonLabel;
		LocaleManager.Instance.LocaleChanged += _ => UpdateSummonLabel();
		UpdateSummonLabel();

		// Tornado variant picker, just above the Bastón row.
		if (SaveManager.Instance.SessionTornadoVariant is int savedVariant
			&& savedVariant >= 0 && savedVariant < TornadoVariantNameKeys.Length)
			_tornadoVariant = (TornadoVariant)savedVariant;
		_tornadoHud = new Metroidvania.UI.TornadoVariantsHud { Name = "TornadoVariantsHud" };
		GetNode("HUD").AddChild(_tornadoHud);
		_tornadoHud.Build(TornadoVariantNameKeys);
		_tornadoHud.Refresh((int)_tornadoVariant);
		LocaleManager.Instance.LocaleChanged += _ =>
		{
			if (IsInstanceValid(_tornadoHud))
				_tornadoHud.Refresh((int)_tornadoVariant);
		};
	}

	public override void _ExitTree()
	{
		// SaveManager is an autoload that outlives this player.
		if (SaveManager.Instance is not null)
			SaveManager.Instance.ShadowUsesChanged -= UpdateSummonLabel;
	}

	// ── Bastón summons ──

	// Taking an enemy's shadow essence: +1 use. False when all slots are full with other types.
	public bool CollectShadowEssence(string enemyKey)
	{
		if (!SaveManager.Instance.AddShadowUse(enemyKey))
			return false;
		_selectedSummon ??= enemyKey;
		UpdateSummonLabel();
		return true;
	}

	// All slots full: ask which slot to discard to make room for enemyKey (or keep them all).
	public void OfferShadowSlotSwap(string enemyKey, System.Action<bool> onDone)
	{
		string title = string.Format(TranslationServer.Translate("UI_SHADOW_SWAP_TITLE"), Metroidvania.UI.ShadowNames.DisplayName(enemyKey));
		Metroidvania.UI.ShadowSlotPrompt.Open(this, title, TranslationServer.Translate("UI_SHADOW_SWAP_DECLINE"), discarded =>
		{
			bool accepted = discarded is not null && SaveManager.Instance.AddShadowUse(enemyKey);
			if (accepted)
				_selectedSummon = enemyKey;
			EnsureValidSummonSelection();
			onDone?.Invoke(accepted);
		});
	}

	private void CycleSummon()
	{
		var keys = new System.Collections.Generic.List<string>(SaveManager.Instance.GetShadowSlotKeys());
		if (keys.Count == 0)
			return;
		int index = _selectedSummon is null ? -1 : keys.IndexOf(_selectedSummon);
		_selectedSummon = keys[(index + 1) % keys.Count];
		UpdateSummonLabel();
	}

	private void DiscardSummonSlot()
	{
		if (SaveManager.Instance.GetAllShadowUses().Count == 0)
			return;
		Metroidvania.UI.ShadowSlotPrompt.Open(this, TranslationServer.Translate("UI_SHADOW_DISCARD_TITLE"),
			TranslationServer.Translate("UI_SHADOW_CANCEL"), _ => EnsureValidSummonSelection());
	}

	private void EnsureValidSummonSelection()
	{
		if (_selectedSummon is null || !SaveManager.Instance.HasShadowSlot(_selectedSummon))
		{
			_selectedSummon = null;
			foreach (string key in SaveManager.Instance.GetShadowSlotKeys())
			{
				_selectedSummon = key;
				break;
			}
		}
		UpdateSummonLabel();
	}

	private void CastShadowSummon()
	{
		EnsureValidSummonSelection();
		double now = Time.GetTicksMsec() / 1000.0;
		if (_selectedSummon is null || now < _summonReadyAt || SaveManager.Instance.GetShadowUses(_selectedSummon) <= 0
			|| GetTree().CurrentScene is not Node scene || !_stats.TrySpendStamina(ShadowSummonStaminaCost))
			return;
		SaveManager.Instance.SpendShadowUse(_selectedSummon);
		_summonReadyAt = now + ShadowSummonCooldown;

		var summon = new ShadowSummon();
		summon.Setup(_selectedSummon, _stats, Mathf.Max(1, Mathf.RoundToInt(_stats.AttackPower * ShadowSummonDamageMultiplier)),
			_facingRight, ShadowSummonSearchRadius, ShadowSummonReach, ShadowSummonColor, ShadowSummonIntensity);
		scene.AddChild(summon);
		summon.GlobalPosition = GlobalPosition + new Vector2(_facingRight ? 30f : -30f, 0f);
	}

	private void UpdateSummonLabel()
	{
		if (_summonHud is null)
			return;
		EnsureSelectionSilently();
		_summonHud.Refresh(_selectedSummon);
	}

	private void EnsureSelectionSilently()
	{
		if (_selectedSummon is not null && SaveManager.Instance.HasShadowSlot(_selectedSummon))
			return;
		_selectedSummon = null;
		foreach (string key in SaveManager.Instance.GetShadowSlotKeys())
		{
			_selectedSummon = key;
			break;
		}
	}

	private void UpdateGoldLabel(int gold)
	{
		_goldLabel.Text = string.Format(TranslationServer.Translate("UI_HUD_GOLD"), gold);
		_goldHud?.SetGold(gold);
	}

	private void UpdateHealChargesLabel(int current, int max)
	{
		_healChargesLabel.Text = string.Format(TranslationServer.Translate("UI_HUD_HEAL_CHARGES"), current, max);
	}

	private void FlashHit(Node2D visual, Color flashColor)
	{
		var tween = CreateTween();
		visual.Modulate = flashColor * 2f;
		tween.TweenProperty(visual, "modulate", Colors.White, 0.25f);
	}

	public void ApplyKnockback(Vector2 direction, float force)
	{
		// Inside the wind shield nothing reaches the player — not even the shove.
		if (_windShieldActive)
			return;
		_knockbackVelocity = new Vector2(direction.X * force, -KnockbackUpwardVelocity);
		_knockbackTimer = KnockbackDuration;

		if (_isLedgeHanging || _isLedgeClimbing)
		{
			_isLedgeHanging = false;
			_isLedgeClimbing = false;
			_ledgeGrabLockout = LedgeGrabLockoutDuration;
			_visual.Position = Vector2.Zero;
		}
	}

	// Called by hazards (e.g. PoisonPool) via HasMethod/Call duck-typing, same convention as
	// ApplyKnockback above. Re-touching the source while already poisoned restarts the clock
	// instead of stacking a second parallel tick loop — the sequence guard makes the old loop's
	// next iteration see a stale _poisonSequence and quietly stop.
	private int _poisonSequence;

	// Tint on the character while poisoned, so being poisoned reads at a glance.
	[Export] public Color PoisonTint = new(0.7f, 1.25f, 0.6f, 1f);

	public async void ApplyPoison(int tickDamage, float tickInterval, float duration)
	{
		if (_windShieldActive)
			return;
		int mySequence = ++_poisonSequence;
		float elapsed = 0f;
		_sprite.SelfModulate = PoisonTint;

		while (elapsed < duration)
		{
			await ToSignal(GetTree().CreateTimer(tickInterval), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || mySequence != _poisonSequence)
				return;

			// Ticks don't grant i-frames: with the 1s post-hit invulnerability, every tick used to
			// make a poisoned player immune to real hits for as long as the poison lasted.
			_stats.TakeDamage(tickDamage, ignoreInvulnerability: true, armInvulnerability: false);
			elapsed += tickInterval;
		}

		if (IsInstanceValid(this) && mySequence == _poisonSequence)
			_sprite.SelfModulate = Colors.White;
	}

	private bool IsTouchingClimbableWall(out float wallNormalX)
	{
		for (int i = 0; i < GetSlideCollisionCount(); i++)
		{
			KinematicCollision2D collision = GetSlideCollision(i);
			if (collision.GetCollider() is not CollisionObject2D body)
				continue;
			if ((body.CollisionLayer & PhysicsLayers.ClimbableWalls) == 0)
				continue;

			Vector2 normal = collision.GetNormal();
			if (Mathf.Abs(normal.Y) > Mathf.Abs(normal.X))
				continue; // floor/ceiling-like normal, not a wall

			wallNormalX = normal.X;
			return true;
		}

		wallNormalX = 0f;
		return false;
	}

	private bool TryDropThroughOneWayPlatform()
	{
		for (int i = 0; i < GetSlideCollisionCount(); i++)
		{
			KinematicCollision2D collision = GetSlideCollision(i);
			if (collision.GetCollider() is not CollisionObject2D body)
				continue;
			if ((body.CollisionLayer & PhysicsLayers.OneWayPlatforms) == 0)
				continue;

			Vector2 normal = collision.GetNormal();
			if (normal.Y > -0.5f)
				continue; // not standing on top of it

			if (body is not Node bodyNode)
				continue;

			foreach (Node child in bodyNode.GetChildren())
			{
				if (child is CollisionShape2D shape)
					DisableShapeTemporarily(shape);
				else if (child is CollisionPolygon2D polygon)
					DisablePolygonTemporarily(polygon);
			}

			// The same platform is often tagged both OneWayPlatforms and ClimbableWalls (cave
			// floors double as ledges), so without this the auto ledge-grab/wall-climb would just
			// catch the player again the instant the shape re-enables, undoing the drop.
			_ledgeGrabLockout = DropThroughLedgeLockoutDuration;
			_wallJumpLockout = DropThroughLedgeLockoutDuration;

			return true;
		}

		return false;
	}

	private async void DisableShapeTemporarily(CollisionShape2D shape)
	{
		shape.Disabled = true;
		await ToSignal(GetTree().CreateTimer(DropThroughDuration), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(shape))
			shape.Disabled = false;
	}

	// MiniRampa (and anything else built on CollisionPolygon2D instead of CollisionShape2D, e.g.
	// so the polygon can be rewritten live — see MiniRampa.cs) needs its own disable path: the two
	// node types don't share a common base with a Disabled property, so TryDropThroughOneWayPlatform
	// can't just handle CollisionShape2D and expect polygon-based platforms to drop-through too.
	private async void DisablePolygonTemporarily(CollisionPolygon2D polygon)
	{
		polygon.Disabled = true;
		await ToSignal(GetTree().CreateTimer(DropThroughDuration), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(polygon))
			polygon.Disabled = false;
	}

	// Ledge detection is two steps rather than a fixed two-raycast band: first find ANY wall in
	// front near head height (a small shapecast, not a single pixel-precise ray, but kept tight
	// and high so the hang reads as grabbing with the hands near the top of the sprite instead of
	// snagging on the chest), then probe straight down from well above the player to find the
	// ledge's actual top surface. Using the real surface height (instead of just keeping the
	// player's current Y) is what makes the grab position — and therefore the climb-up target —
	// land correctly on top.
	private bool TryDetectLedge(out Vector2 grabPosition)
	{
		float dir = _facingRight ? 1f : -1f;
		grabPosition = Vector2.Zero;

		_ledgeWallCheck.TargetPosition = new Vector2(Mathf.Abs(_ledgeWallCheck.TargetPosition.X) * dir, _ledgeWallCheck.TargetPosition.Y);
		_ledgeWallCheck.ForceShapecastUpdate();
		if (!_ledgeWallCheck.IsColliding())
			return false;

		Vector2 wallPoint = _ledgeWallCheck.GetCollisionPoint(0);
		Vector2 probeFrom = new(wallPoint.X + dir * 4f, GlobalPosition.Y - LedgeSurfaceProbeHeight);
		Vector2 probeTo = new(probeFrom.X, GlobalPosition.Y + 8f);

		var query = PhysicsRayQueryParameters2D.Create(probeFrom, probeTo, PhysicsLayers.ClimbableWalls);
		Godot.Collections.Dictionary result = GetWorld2D().DirectSpaceState.IntersectRay(query);
		if (result.Count == 0)
			return false;

		float surfaceY = ((Vector2)result["position"]).Y;
		_ledgeSurfaceY = surfaceY;
		grabPosition = new Vector2(wallPoint.X - dir * LedgeGrabOffsetX, surfaceY + LedgeHangDropY);
		return true;
	}

	public void EnterLadder(Ladder ladder)
	{
		_ladder = ladder;
	}

	// Arriving through a LevelTransition with SpawnOnLadder: latch onto the ladder we spawned
	// inside as soon as its Area2D registers us (a physics frame or two after the teleport),
	// instead of falling off it until up is pressed.
	private float _latchLadderOnSpawnTimer;

	public void LatchLadderOnSpawn() => _latchLadderOnSpawnTimer = 0.5f;

	public void ExitLadder(Ladder ladder)
	{
		if (_ladder != ladder)
			return;

		_ladder = null;
		_isClimbing = false;
	}

	public void EnterWater(Water water)
	{
		_water = water;
	}

	public void ExitWater(Water water)
	{
		if (_water != water)
			return;

		_water = null;
		_isSwimming = false;
	}

	public void EnterRope(Rope rope)
	{
		_ropeInZone = rope;
	}

	public void ExitRope(Rope rope)
	{
		if (_ropeInZone == rope)
			_ropeInZone = null;
		if (_lastReleasedRope == rope)
			_lastReleasedRope = null;
	}

	public override void _PhysicsProcess(double delta)
	{
		UpdateLightningBolts((float)delta);
		if (_isDead)
			return;

		if (_debugFlashlight != null && Input.IsActionJustPressed("debug_flashlight"))
			_debugFlashlight.Enabled = !_debugFlashlight.Enabled;

		if (OS.HasFeature("debug") && Input.IsActionJustPressed("debug_infinite_jump"))
			_infiniteJumpDebug = !_infiniteJumpDebug;

		if (OS.HasFeature("debug") && Input.IsActionJustPressed("debug_invincible"))
			_stats.ExternalInvulnerable = !_stats.ExternalInvulnerable;

		// While a scene transition is fading/loading, this (old-scene) player keeps falling through
		// a pit exit — that fall must not count as a death (see SceneFader.IsChangingScene).
		if (GlobalPosition.Y > FallDeathY && !SceneFader.IsChangingScene)
		{
			_stats.Kill();
			return;
		}

		// Water has no solid collision — anything that moves the player fast enough (dash, pound,
		// roll, run-thrust) can carry them below the float line before the state machine ever
		// reaches the swim block further down to catch it. Rather than chase every individual
		// movement mode with its own water check, just clamp back up here every frame, unconditionally,
		// whenever inside a water zone and sunk past where floating should hold — never pushes
		// down, only up, so a jump arcing over/through water isn't yanked back to the surface.
		if (_water != null && _abilities.Has(PlayerAbilities.Swim))
		{
			float floatY = _water.GlobalPosition.Y - SwimFloatOffset;
			if (GlobalPosition.Y > floatY)
				GlobalPosition = new Vector2(GlobalPosition.X, floatY);
		}

		bool isGroundedOrClimbing = IsOnFloor() || _isClimbing || _isWallClimbing || _isLedgeHanging || _isLedgeClimbing || _isSwinging || _isSwimming;
		_airborneTimer = isGroundedOrClimbing ? 0f : _airborneTimer + (float)delta;
		if (_airborneTimer > MaxAirborneTime && !SceneFader.IsChangingScene)
		{
			// Safety net against out-of-bounds/collision bugs that let the player fall forever
			// without ever crossing FallDeathY (e.g. a hole in the level's collision).
			_stats.Kill();
			return;
		}

		if (_comboResetTimer > 0f)
		{
			_comboResetTimer -= (float)delta;
			if (_comboResetTimer <= 0f)
			{
				_comboStep = 0;
				_airComboStep = 0;
			}
		}
		if (IsOnFloor())
			_airComboStep = 0;

		_attackBufferTimer = Mathf.Max(0f, _attackBufferTimer - (float)delta);
		_dashBufferTimer = Mathf.Max(0f, _dashBufferTimer - (float)delta);
		_rollBufferTimer = Mathf.Max(0f, _rollBufferTimer - (float)delta);
		if (Input.IsActionJustPressed("attack"))
			_attackBufferTimer = InputBufferWindow;
		if (Input.IsActionJustPressed("dash"))
			_dashBufferTimer = InputBufferWindow;
		if (Input.IsActionJustPressed("roll"))
			_rollBufferTimer = InputBufferWindow;

		Vector2 velocity = Velocity;

		if (_knockbackTimer > 0)
		{
			_knockbackTimer -= (float)delta;
			// Locked to the stored launch vector rather than IsOnFloor()-clamped like the old
			// horizontal-only shove — that clamp would zero the upward pop the instant it's applied
			// whenever the hit lands while grounded, which is the common case for most hits.
			velocity = _knockbackVelocity;
			_knockbackVelocity.Y += Gravity * (float)delta;
			Velocity = velocity;
			MoveAndSlide();
			UpdateAnimation(delta, false);
			return;
		}

		// Water has no solid collision, so a dash that reaches it would otherwise force
		// velocity.Y = 0 for the whole dash duration without ever reaching the swim check
		// further down — letting the player glide clean over/through the water and only find
		// out there was nothing solid there once the dash ends past it. Cancelling the instant
		// it touches water falls through to the ladder/swim block below on this same frame,
		// same idea as the pound-vs-water cancel a few lines down.
		if (_isDashing && _water != null)
			_isDashing = false;

		if (_isDashing)
		{
			velocity.X = _dashDirection * DashSpeed;
			velocity.Y = 0;
			Velocity = velocity;
			MoveAndSlide();
			UpdateAnimation(delta, false);
			return;
		}

		if (_isRolling)
		{
			velocity.X = _rollDirection * RollSpeed;
			velocity.Y = 0;
			Velocity = velocity;
			MoveAndSlide();
			UpdateAnimation(delta, false);
			return;
		}

		if (_isRunThrusting)
		{
			velocity.X = _runThrustDirection * RunThrustSpeed;
			velocity.Y = 0;
			Velocity = velocity;
			MoveAndSlide();
			UpdateAnimation(delta, false);
			return;
		}

		// Water has no solid collision, so a pound dive that reaches it would otherwise never
		// hit IsOnFloor() and just plunge at PoundSpeed straight through to FallDeathY — cancel
		// the pound the instant it touches water so this frame falls through to the swim/float
		// block below instead of continuing the dive.
		if (_isPounding && _water != null)
			EndPound();

		if (_isPounding)
		{
			// The hit itself is detected via a signal fired mid-physics-step (while Godot is
			// still flushing collision queries), so the actual bounce/deactivate is deferred
			// to here — the start of the next physics frame — rather than applied synchronously
			// from inside that signal callback, which Godot's physics server rejects.
			if (_poundHitLanded)
			{
				Velocity = new Vector2(Velocity.X, PoundBounceVelocity);
				_jumpCount = 0;
				_isDoubleJumping = false;
				EndPound();
				PlayDownAttackBounceSlam();
				UpdateAnimation(delta, false);
				return;
			}

			velocity.X = 0f;
			velocity.Y = PoundSpeed;
			Velocity = velocity;
			MoveAndSlide();
			if (_currentAttackAnimation == "down_attack_start" && _sprite.Animation == "down_attack_start" && !_sprite.IsPlaying())
				_currentAttackAnimation = "down_attack_loop";
			UpdateAnimation(delta, false);

			if (IsOnFloor())
			{
				EndPound();
				PlayDownAttackLanding();
			}
			return;
		}

		if (_ladderGrabLockout > 0f)
			_ladderGrabLockout -= (float)delta;
		if (_latchLadderOnSpawnTimer > 0f)
			_latchLadderOnSpawnTimer -= (float)delta;

		if (_ladder != null && !_isClimbing && _latchLadderOnSpawnTimer > 0f)
		{
			_latchLadderOnSpawnTimer = 0f;
			_isClimbing = true;
			velocity = Vector2.Zero;
			GlobalPosition = new Vector2(_ladder.GlobalPosition.X, GlobalPosition.Y);
		}

		if (_ladder != null)
		{
			// Only up latches onto a ladder; down is reserved for crouching/dropping through
			// platforms and must not snap the player onto a ladder they're merely standing next to.
			// Once latched, down still climbs down (see climbInput below).
			if (!_isClimbing && _ladderGrabLockout <= 0f && Input.IsActionPressed("move_up"))
			{
				_isClimbing = true;
				GlobalPosition = new Vector2(_ladder.GlobalPosition.X, GlobalPosition.Y);
			}
		}
		else
		{
			_isClimbing = false;
		}

		if (_isClimbing)
		{
			if (_isLadderAttacking)
			{
				// Hold position on the rungs while swinging.
				velocity = Vector2.Zero;
			}
			else
			{
				float climbInput = (Input.IsActionPressed("move_down") ? 1f : 0f) - (Input.IsActionPressed("move_up") ? 1f : 0f);
				velocity.Y = climbInput * ClimbSpeed;

				float climbDirection = Input.GetAxis("move_left", "move_right");
				velocity.X = climbDirection * Speed * ClimbHorizontalSpeedMultiplier;
				if (climbDirection != 0)
					_facingRight = climbDirection > 0;

				if (_attackBufferTimer > 0f && !_attacking && _sprite.SpriteFrames.HasAnimation("ladder_attack"))
				{
					_attackBufferTimer = 0f;
					LadderAttack();
				}
			}

			_visual.Scale = new Vector2(_facingRight ? 1 : -1, 1f);
			_visual.Position = Vector2.Zero;

			if (Input.IsActionJustPressed("jump") && !_isLadderAttacking)
			{
				_isClimbing = false;
				_ladderGrabLockout = LadderJumpLockoutDuration;
				velocity.Y = JumpVelocity;
				_jumpCount = 1;
			}

			Velocity = velocity;
			MoveAndSlide();
			UpdateAnimation(delta, false, velocity.X, _isClimbing);
			return;
		}

		// Same zone-based pattern as the ladder above — Y-locked instead of X-locked, since this
		// is a surface float (bobs up to sit on top of the water, no dive control) rather than a
		// free swim, gated by the Swim ability (same idea as RopeSwing/WallClimb/LedgeGrab) rather
		// than always available like Ladder, since it's meant to be a found-later traversal
		// upgrade, not a default one.
		if (_water != null && _abilities.Has(PlayerAbilities.Swim))
			_isSwimming = true;

		if (_isSwimming)
		{
			// Floating counts as grounded for jump purposes — without this, a jump input that
			// lands on a frame where the player isn't yet re-latched into swimming (or any other
			// brief gap while bobbing at the surface) could burn a double-jump charge that then
			// never resets, since IsOnFloor() never becomes true in water.
			_jumpCount = 0;
			_isDoubleJumping = false;

			if (Input.IsActionJustPressed("jump"))
			{
				_isSwimming = false;
				_water = null;
				velocity.Y = JumpVelocity;
				_jumpCount = 1;
				Velocity = velocity;
				MoveAndSlide();
				UpdateAnimation(delta, false, velocity.X);
				return;
			}

			float horizontal = Input.GetAxis("move_left", "move_right");
			velocity = new Vector2(horizontal * SwimSpeed, 0f);
			if (horizontal != 0)
				_facingRight = horizontal > 0;

			_visual.Scale = new Vector2(_facingRight ? 1 : -1, 1f);
			_visual.Position = Vector2.Zero;

			Velocity = velocity;
			MoveAndSlide();

			float targetY = _water.GlobalPosition.Y - SwimFloatOffset;
			GlobalPosition = new Vector2(GlobalPosition.X, Mathf.MoveToward(GlobalPosition.Y, targetY, SwimFloatCorrectionSpeed * (float)delta));

			UpdateAnimation(delta, false, velocity.X);
			return;
		}

		if (!_isSwinging && _ropeInZone != null && _ropeInZone != _lastReleasedRope
			&& _abilities.Has(PlayerAbilities.RopeSwing) && !IsOnFloor())
		{
			_rope = _ropeInZone;
			_isSwinging = true;
		}

		if (_isSwinging)
		{
			// The rope swings on its own fixed schedule whether or not anyone's attached —
			// grabbing on doesn't influence its motion at all, Player just rides wherever the
			// rope's tip currently is each frame.
			GlobalPosition = _rope.EndPosition;
			Velocity = Vector2.Zero;

			_visual.Scale = new Vector2(_facingRight ? 1 : -1, 1f);
			_visual.Position = Vector2.Zero;

			// No dedicated swing art yet — the wall-hang pose reads fine for "hanging from
			// something", same stopgap Wall Climb uses until its own animation exists.
			if (_sprite.Animation != "wall_hang")
				_sprite.Play("wall_hang");

			if (Input.IsActionJustPressed("jump"))
			{
				// Keeps the swing's horizontal fling but replaces the vertical component with a
				// normal jump impulse instead of whatever the tangential velocity happened to be —
				// otherwise letting go right near the bottom/top of the swing (where vertical
				// speed is near zero) barely launches at all and just reads as "dropped", not
				// "jumped".
				Velocity = new Vector2(_rope.EndVelocity.X, JumpVelocity);
				_isSwinging = false;
				_lastReleasedRope = _rope;
				_rope = null;
				_jumpCount = 1;
			}

			return;
		}

		if (_ledgeGrabLockout > 0f)
			_ledgeGrabLockout -= (float)delta;

		if (_isLedgeClimbing)
		{
			_ledgeClimbTimer += (float)delta;
			float climbT;
			bool climbDone;
			if (_sprite.Animation == "ledge_climb")
			{
				float frame = _sprite.Frame + _sprite.FrameProgress;
				float moveFrames = Mathf.Max(1, LedgeClimbMoveEndFrame - LedgeClimbMoveStartFrame);
				climbT = Mathf.Clamp((frame - LedgeClimbMoveStartFrame) / moveFrames, 0f, 1f);
				climbDone = !_sprite.IsPlaying();
				if (climbDone)
					climbT = 1f;
			}
			else
			{
				climbT = Mathf.Clamp(_ledgeClimbTimer / LedgeClimbDuration, 0f, 1f);
				climbDone = climbT >= 1f;
				if (_sprite.Animation != "wall_climb")
					_sprite.Play("wall_climb");
			}
			GlobalPosition = _ledgeClimbStartPosition.Lerp(_ledgeClimbEndPosition, climbT);
			_visual.Position = new Vector2((_facingRight ? 1f : -1f) * LedgeHangVisualShiftX * (1f - climbT), 0f);

			if (climbDone)
			{
				_visual.Position = Vector2.Zero;
				_isLedgeClimbing = false;
				_jumpCount = 0;
				_isDoubleJumping = false;
			}
			return;
		}

		if (_isLedgeHanging)
		{
			Velocity = Vector2.Zero;
			_visual.Position = new Vector2((_facingRight ? 1f : -1f) * LedgeHangVisualShiftX, 0f);
			string hangAnim = _sprite.SpriteFrames.HasAnimation("ledge_hang") ? "ledge_hang" : "wall_hang";
			if (_sprite.Animation != hangAnim)
				_sprite.Play(hangAnim);

			// Brief beat on the grab pose so the catch actually reads, then climb up on its own —
			// no input wait, the ability is meant to always carry you over the ledge.
			_ledgeHangTimer += (float)delta;
			if (_ledgeHangTimer >= LedgeAutoClimbDelay)
			{
				_isLedgeHanging = false;
				_isLedgeClimbing = true;
				_ledgeClimbTimer = 0f;
				_ledgeClimbStartPosition = GlobalPosition;
				float climbDir = _facingRight ? 1f : -1f;
				_ledgeClimbEndPosition = new Vector2(
					GlobalPosition.X + climbDir * LedgeClimbForwardX,
					_ledgeSurfaceY - _standHalfHeight - 2f);
				// Restart from frame 0 even if the previous climb left this clip parked on its
				// last frame — the climb above ends when the clip stops playing.
				if (_sprite.SpriteFrames.HasAnimation("ledge_climb"))
				{
					_sprite.Stop();
					_sprite.Play("ledge_climb");
				}
			}
			return;
		}

		if (_wallJumpLockout > 0f)
			_wallJumpLockout -= (float)delta;

		if (_abilities.Has(PlayerAbilities.LedgeGrab) && _ledgeGrabLockout <= 0f && !IsOnFloor()
			&& TryDetectLedge(out Vector2 ledgeGrabPosition))
		{
			_isLedgeHanging = true;
			_ledgeHangTimer = 0f;
			GlobalPosition = ledgeGrabPosition;
			Velocity = Vector2.Zero;
			_isWallClimbing = false;
			_sprite.Stop();
			_sprite.Play(_sprite.SpriteFrames.HasAnimation("ledge_hang") ? "ledge_hang" : "wall_hang");
			return;
		}

		bool wasWallClimbing = _isWallClimbing;
		bool touchingClimbableWall = IsTouchingClimbableWall(out float climbableWallNormalX);
		if (_abilities.Has(PlayerAbilities.WallClimb) && _wallJumpLockout <= 0f && touchingClimbableWall && !IsOnFloor())
		{
			float wallInputDir = Input.GetAxis("move_left", "move_right");
			bool pressingIntoWall = wallInputDir != 0f && Mathf.Sign(wallInputDir) == -Mathf.Sign(climbableWallNormalX);

			if (pressingIntoWall)
				_isWallClimbing = true;
		}
		else
		{
			_isWallClimbing = false;
		}

		// Kill any leftover jump momentum right at the grab instant — otherwise the character
		// keeps rising for a moment before the slide-down eases in, instead of catching in place.
		if (_isWallClimbing && !wasWallClimbing)
			velocity.Y = 0f;

		if (_isWallClimbing)
		{
			float wallNormalX = climbableWallNormalX;
			// No climbing art exists for this pack — only the wall-hang (grab) pose reads
			// correctly, so this is a wall-cling + controlled slide, not free vertical movement.
			velocity.Y = Mathf.MoveToward(velocity.Y, WallSlideSpeed, WallClimbSpeed * (float)delta);
			velocity.X = 0f;

			_facingRight = wallNormalX < 0f;
			_visual.Scale = new Vector2(_facingRight ? 1 : -1, 1f);

			if (Input.IsActionJustPressed("jump"))
			{
				_isWallClimbing = false;
				_wallJumpLockout = WallJumpLockoutDuration;
				velocity.X = wallNormalX * WallJumpVelocityX;
				velocity.Y = WallJumpVelocityY;
				_jumpCount = 1;
			}

			Velocity = velocity;
			MoveAndSlide();
			UpdateAnimation(delta, false, velocity.X, false, _isWallClimbing);
			return;
		}

		if (!IsOnFloor())
			velocity.Y += Gravity * (float)delta;
		else
		{
			_jumpCount = 0;
			_isDoubleJumping = false;
		}

		int maxJumps = _abilities.Has(PlayerAbilities.DoubleJump) ? 2 : 1;
		bool droppedThrough = Input.IsActionPressed("move_down") && Input.IsActionJustPressed("jump")
			&& IsOnFloor() && TryDropThroughOneWayPlatform();

		bool jumpPressed = !droppedThrough && Input.IsActionJustPressed("jump") && _jumpSquatTimer <= 0f
			&& (IsOnFloor() || _infiniteJumpDebug || _jumpCount < maxJumps);
		// Ground jumps crouch for JumpSquatDuration on the floor before lifting off (a short
		// anticipation beat, like a fighting game's jump squat); air jumps launch at once.
		if (jumpPressed && IsOnFloor() && JumpSquatDuration > 0f && _sprite.SpriteFrames.HasAnimation("jump_squat"))
			_jumpSquatTimer = JumpSquatDuration;
		else if (jumpPressed)
			LaunchJump(ref velocity);

		if (_jumpSquatTimer > 0f)
		{
			_jumpSquatTimer -= (float)delta;
			if (_jumpSquatTimer <= 0f)
				LaunchJump(ref velocity);
		}

		UpdateCrouch();
		UpdateBlock(delta);

		float rawDirection = Input.GetAxis("move_left", "move_right");
		// Facing stays put while throwing the sword, so the catch pose faces where it was thrown.
		if (rawDirection != 0 && !_isShooting)
			_facingRight = rawDirection > 0;

		float direction = (_attacking || _healing || _crouching || _isBlocking || _isTransforming || _isShooting
			|| _isCastingSpell || _isCastingSpell2) ? 0f : rawDirection;
		bool sprinting = !_attacking && !_healing && _abilities.Has(PlayerAbilities.Sprint) && Input.IsActionPressed("sprint")
			&& !_crouching && direction != 0;
		float speed = _crouching ? Speed * CrouchSpeedMultiplier : sprinting ? Speed * SprintSpeedMultiplier : Speed;
		if (AirControl || IsOnFloor())
			velocity.X = direction != 0 ? direction * speed : Mathf.MoveToward(velocity.X, 0, speed);

		_visual.Scale = new Vector2(_facingRight ? 1 : -1, 1f);

		UpdateCameraLook(delta);

		if (_dashBufferTimer > 0f && _abilities.Has(PlayerAbilities.Dash) && _canDash && !_healing && !_isRolling)
		{
			_dashBufferTimer = 0f;
			StartDash();
		}

		if (_rollBufferTimer > 0f && _abilities.Has(PlayerAbilities.Roll) && _canRoll && !_healing && !_isDashing
			&& IsOnFloor())
		{
			_rollBufferTimer = 0f;
			StartRoll();
		}

		if (_attackBufferTimer > 0f && !_attacking && !_healing && !_isBlocking && !_isTransforming && !_windShieldActive)
		{
			_attackBufferTimer = 0f;
			bool wantsRunThrust = Input.IsActionPressed("run_thrust") && _abilities.Has(PlayerAbilities.RunThrust)
				&& _canRunThrust && rawDirection != 0;
			bool wantsPound = !IsOnFloor() && Input.IsActionPressed("move_down") && _abilities.Has(PlayerAbilities.PoundAttack);
			bool wantsUpAttack = Input.IsActionPressed("move_up") && _abilities.Has(PlayerAbilities.UpAttack);
			if (wantsPound)
				StartPound();
			else if (wantsRunThrust)
				RunThrust();
			else if (wantsUpAttack)
				UpAttack();
			else if (_crouching)
				CrouchAttack();
			else
				Attack();
		}

		if (Input.IsActionJustPressed("charged_attack") && _abilities.Has(PlayerAbilities.ChargedAttack) && !_isCharging && !_windShieldActive
			&& !_attacking && !_healing && !_isBlocking && !_isTransforming && !_crouching
			&& !_isDashing && !_isRolling && !_isRunThrusting)
		{
			StartCharging();
		}

		if (Input.IsActionJustPressed("shoot_bow") && !_isShooting
			&& !_attacking && !_healing && !_isBlocking && !_isTransforming && !_crouching
			&& !_isDashing && !_isRolling && !_isRunThrusting && !_isCharging
			&& !_isCastingSpell && !_isCastingSpell2)
		{
			// Sheets with the flying sword clips throw the sword (its own unlock); others keep the bow.
			if (_sprite.SpriteFrames.HasAnimation("flying_sword_start"))
			{
				if (_abilities.Has(PlayerAbilities.FlyingSword))
					ThrowFlyingSword();
			}
			else if (_abilities.Has(PlayerAbilities.Bow))
			{
				ShootBow();
			}
		}

		if (Input.IsActionJustPressed("spell") && _abilities.Has(PlayerAbilities.Spell) && !_isCastingSpell
			&& !_isCastingSpell2 && !_attacking && !_healing && !_isBlocking && !_isTransforming && !_crouching
			&& !_isDashing && !_isRolling && !_isRunThrusting && !_isCharging && !_isShooting)
		{
			CastSpell();
		}

		if (Input.IsActionJustPressed("spell2") && _abilities.Has(PlayerAbilities.Spell2) && !_isCastingSpell2
			&& !_isCastingSpell && !_attacking && !_healing && !_isBlocking && !_isTransforming && !_crouching
			&& !_isDashing && !_isRolling && !_isRunThrusting && !_isCharging && !_isShooting)
		{
			CastSpell2();
		}

		if (Input.IsActionJustPressed("spell_tornado") && !_isCastingSpell
			&& !_isCastingSpell2 && !_attacking && !_healing && !_isBlocking && !_isTransforming && !_crouching
			&& !_isDashing && !_isRolling && !_isRunThrusting && !_isCharging && !_isShooting)
		{
			CastTornado();
		}

		if (Input.IsActionJustPressed("spell_poison") && !_isCastingSpell
			&& !_isCastingSpell2 && !_attacking && !_healing && !_isBlocking && !_isTransforming && !_crouching
			&& !_isDashing && !_isRolling && !_isRunThrusting && !_isCharging && !_isShooting)
		{
			CastPoisonSpit();
		}

		if (Input.IsActionJustPressed("spell_fireball") && !_isCastingSpell
			&& !_isCastingSpell2 && !_attacking && !_healing && !_isBlocking && !_isTransforming && !_crouching
			&& !_isDashing && !_isRolling && !_isRunThrusting && !_isCharging && !_isShooting)
		{
			CastFireball();
		}

		if (Input.IsActionJustPressed("tornado_cycle"))
			CycleTornadoVariant();

		if (Input.IsActionJustPressed("wind_shield") && !_windShieldActive && !_isCastingSpell
			&& !_isCastingSpell2 && !_attacking && !_healing && !_isTransforming
			&& !_isDashing && !_isRolling && !_isRunThrusting && !_isCharging && !_isShooting)
		{
			StartWindShield();
		}

		if (Input.IsActionJustPressed("wind_hurricane") && !_isCastingSpell
			&& !_isCastingSpell2 && !_attacking && !_healing && !_isBlocking && !_isTransforming && !_crouching
			&& !_isDashing && !_isRolling && !_isRunThrusting && !_isCharging && !_isShooting)
		{
			CastHurricane();
		}
		if (Input.IsActionJustPressed("summon_cycle"))
			CycleSummon();
		if (Input.IsActionJustPressed("summon_discard"))
			DiscardSummonSlot();
		if (Input.IsActionJustPressed("summon_cast") && !_attacking && !_healing && !_isBlocking && !_isTransforming
			&& !_isDashing && !_isRolling && !_isRunThrusting && !_isCharging)
			CastShadowSummon();

		if (Input.IsActionJustPressed("heal") && !_attacking && !_healing && !_isDashing && !_isRolling)
			UseHealFlask();

		// Debounced on release rather than IsActionJustPressed: OS key-repeat while the
		// button is held sends repeated keydown events that Godot treats as fresh presses,
		// which used to re-trigger the toggle mid-transform and corrupt the sprite state.
		bool fireImbuePressed = Input.IsActionPressed("fire_imbue");
		if (!fireImbuePressed)
			_fireImbueKeyReleased = true;

		if (fireImbuePressed && _fireImbueKeyReleased && !_attacking && !_healing && !_isDashing && !_isRolling
			&& !_isRunThrusting && !_isTransforming)
		{
			_fireImbueKeyReleased = false;
			CycleElement();
		}

		bool equipTorchPressed = Input.IsActionPressed("equip_torch");
		if (!equipTorchPressed)
			_equipTorchKeyReleased = true;

		if (equipTorchPressed && _equipTorchKeyReleased && _abilities.Has(PlayerAbilities.Torch)
			&& !_attacking && !_isDashing && !_isRolling && !_isTransforming && !_isFireImbued)
		{
			_equipTorchKeyReleased = false;
			_torchEquipped = !_torchEquipped;
			SaveManager.Instance.SessionTorchEquipped = _torchEquipped;
			if (_torchEquipped)
				Sfx.Play(this, "World/Torch", "Light Torch");
		}

		_torchLight.Enabled = _torchEquipped && !_isFireImbued;
		_swordFireLight.Enabled = _isFireImbued;

		Velocity = velocity;
		MoveAndSlide();
		UpdateAnimation(delta, sprinting, velocity.X);
	}

	// Only idle/run/jump have a "_torch" sprite variant today (the rest of the moveset needs
	// both hands, so the torch is treated as dropped rather than drawn for those animations).
	private string TorchAnim(string baseAnim) => _torchEquipped && !_isFireImbued ? $"{baseAnim}_torch" : baseAnim;

	private void UpdateAnimation(double delta, bool sprinting, float velocityX = 0f, bool isLadderClimbing = false, bool isWallClimbing = false)
	{
		// Only the ladder-idle case below ever changes this away from 1 — reset it up front so
		// that pause doesn't leak into whatever animation plays next after leaving the ladder.
		_sprite.SpeedScale = 1f;

		bool onFloor = IsOnFloor();
		bool justLanded = _wasAirborne && onFloor;
		_wasAirborne = !onFloor;
		if (justLanded)
			SpawnDust("land_dust");

		// Surface swimming: treading water when still, stroking when moving, its own hit flinch.
		// Sheets without swim clips fall through to the regular air animations as before.
		if (_isSwimming && _sprite.SpriteFrames.HasAnimation("swim_idle"))
		{
			string swimAnim = Mathf.Abs(velocityX) > 1f && _sprite.SpriteFrames.HasAnimation("swim") ? "swim" : "swim_idle";
			if (_knockbackTimer > 0f && _sprite.SpriteFrames.HasAnimation("swim_hit"))
				swimAnim = "swim_hit";
			if (_sprite.Animation != swimAnim)
				_sprite.Play(swimAnim);
			return;
		}

		if (_knockbackTimer > 0f)
		{
			_sprite.Play("hurt");
			return;
		}

		if (_healing)
		{
			if (_sprite.Animation != "heal")
				_sprite.Play("heal");
			return;
		}

		if (_isTransforming)
		{
			string transformAnim = _isFireImbued && _sprite.SpriteFrames.HasAnimation("transformation_out") ? "transformation_out" : "transformation";
			if (_sprite.Animation != transformAnim)
				_sprite.Play(transformAnim);
			return;
		}

		if (_isShooting)
		{
			string shootAnim = _shootAnimation ?? "shoot_bow";
			if (_sprite.Animation != shootAnim)
				_sprite.Play(shootAnim);
			return;
		}

		if (_isCastingSpell)
		{
			if (_sprite.Animation != "spell")
				_sprite.Play("spell");
			return;
		}

		if (_isCastingSpell2)
		{
			if (_sprite.Animation != "spell2")
				_sprite.Play("spell2");
			return;
		}

		if (isLadderClimbing && _isLadderAttacking)
		{
			if (_sprite.Animation != "ladder_attack")
				_sprite.Play("ladder_attack");
			return;
		}

		if (isLadderClimbing)
		{
			if (_sprite.Animation != "ladder_climb")
				_sprite.Play("ladder_climb");
			// Play() alone would keep the climb cycle advancing on its own even when the player
			// is holding still on the ladder — gate actual frame playback on real climb movement
			// so the character freezes on its current grip pose instead of climbing in place.
			_sprite.SpeedScale = Mathf.Abs(Velocity.Y) > 0.01f || Mathf.Abs(velocityX) > 0.01f ? 1f : 0f;

			_walkPhase += Mathf.Abs(Velocity.Y) * (float)delta * 0.3f;
			float climbSwing = Mathf.Sin(_walkPhase) * 15f;
			_legLeft.RotationDegrees = climbSwing;
			_legRight.RotationDegrees = -climbSwing;
			_armLeft.RotationDegrees = -climbSwing;
			_armRight.RotationDegrees = climbSwing;
			return;
		}

		if (isWallClimbing)
		{
			if (_sprite.Animation != "wall_hang")
				_sprite.Play("wall_hang");
			return;
		}

		if (_attacking)
		{
			if (_sprite.Animation != _currentAttackAnimation)
				_sprite.Play(_currentAttackAnimation);
		}
		else if (_isDashing)
		{
			_sprite.Play("dash");
			_legLeft.RotationDegrees = -10f;
			_legRight.RotationDegrees = -14f;
			_armLeft.RotationDegrees = -30f;
			_armRight.RotationDegrees = -30f;
			return;
		}
		else if (_isRolling)
		{
			_sprite.Play("roll");
			_legLeft.RotationDegrees = -10f;
			_legRight.RotationDegrees = -14f;
			_armLeft.RotationDegrees = -30f;
			_armRight.RotationDegrees = -30f;
			return;
		}
		else if (_jumpSquatTimer > 0f)
		{
			if (_sprite.Animation != "jump_squat")
				_sprite.Play("jump_squat");
			return;
		}
		else if (!onFloor)
		{
			// Air sequence is jump (rising) -> jump_to_fall (once, at the apex) -> fall (loop).
			// Torch variants only exist for the rising clip, so with a torch the jump clip is
			// held for the whole airtime rather than swapping to the torchless fall.
			bool torchHeld = TorchAnim("jump") != "jump";
			if (Velocity.Y < 0f || torchHeld)
				_sprite.Play(_isDoubleJumping ? TorchAnim("double_jump") : TorchAnim("jump"));
			else if (_sprite.Animation == "fall" || (_sprite.Animation == "jump_to_fall" && !_sprite.IsPlaying()))
				_sprite.Play("fall");
			else if (_sprite.Animation != "jump_to_fall")
				_sprite.Play("jump_to_fall");
			_legLeft.RotationDegrees = JumpLegAngleDeg;
			_legRight.RotationDegrees = JumpLegAngleDeg * 0.6f;
			_armLeft.RotationDegrees = -JumpArmAngleDeg;
			_armRight.RotationDegrees = JumpArmAngleDeg;
			return;
		}
		else if (_crouching)
		{
			if (_sprite.Animation != "crouch")
				_sprite.Play("crouch");
			return;
		}
		else if (_isBlocking)
		{
			// Raise the guard with block_start (when the sheet has it), then hold idle_block.
			if (_sprite.Animation == "block_start")
			{
				if (!_sprite.IsPlaying())
					_sprite.Play("idle_block");
			}
			else if (_sprite.Animation != "idle_block")
			{
				_sprite.Play(_sprite.SpriteFrames.HasAnimation("block_start") ? "block_start" : "idle_block");
			}
			return;
		}

		float speedRatio = Mathf.Abs(velocityX) / Speed;

		// Lowering the guard: block_end plays once when standing still; moving or attacking cuts it.
		if (!_attacking && speedRatio < 0.05f)
		{
			if ((_sprite.Animation == "idle_block" || _sprite.Animation == "block_start") && _sprite.SpriteFrames.HasAnimation("block_end"))
				_sprite.Play("block_end");
			if (_sprite.Animation == "block_end" && _sprite.IsPlaying())
				return;
		}
		if (speedRatio < 0.05f)
		{
			if (!_attacking)
			{
				// Standing still on touchdown plays the landing squat once before idle; moving
				// or attacking cuts it. Skipped for sheets without a "landing" clip.
				if (justLanded && TorchAnim("idle") == "idle" && _sprite.SpriteFrames.HasAnimation("landing"))
					_sprite.Play("landing");
				if (_sprite.Animation != "landing" || !_sprite.IsPlaying())
					_sprite.Play(TorchAnim("idle"));
			}

			float t = (float)delta * 10f;
			_legLeft.RotationDegrees = Mathf.Lerp(_legLeft.RotationDegrees, 0f, t);
			_legRight.RotationDegrees = Mathf.Lerp(_legRight.RotationDegrees, 0f, t);
			_armLeft.RotationDegrees = Mathf.Lerp(_armLeft.RotationDegrees, 0f, t);
			_armRight.RotationDegrees = Mathf.Lerp(_armRight.RotationDegrees, 0f, t);
			_walkPhase = 0f;
			_lastStepSin = 0f;
			return;
		}

		if (!_attacking)
			_sprite.Play(TorchAnim("run"));

		float amplitude = sprinting ? RunSwingAmplitudeDeg : WalkSwingAmplitudeDeg;
		float cycleSpeed = WalkCycleSpeed * (sprinting ? 1.6f : 1f) * Mathf.Max(speedRatio, 0.3f);
		_walkPhase += cycleSpeed * (float)delta;

		float swing = Mathf.Sin(_walkPhase) * amplitude;
		_legLeft.RotationDegrees = swing;
		_legRight.RotationDegrees = -swing;
		_armLeft.RotationDegrees = -swing * 0.7f;
		_armRight.RotationDegrees = swing * 0.7f;

		float stepSin = Mathf.Sin(_walkPhase);
		if (Mathf.Sign(stepSin) != 0f && Mathf.Sign(stepSin) != Mathf.Sign(_lastStepSin))
		{
			Sfx.Play(this, "Footsteps/Dirt", "Dirt Walk");
		}
		_lastStepSin = stepSin;
	}

	private void UpdateCrouch()
	{
		bool holdingCrouch = Input.IsActionPressed("move_down");
		// Once crouched, only releasing the key stands you back up — we don't re-check
		// IsOnFloor() every frame here, since a one-frame floor-detection blip (e.g. right
		// after swapping collision shapes) would otherwise pop you back up and down.
		bool wantsCrouch = _crouching ? holdingCrouch : holdingCrouch && IsOnFloor();
		if (wantsCrouch == _crouching)
			return;

		_crouching = wantsCrouch;
		_standCollision.Disabled = _crouching;
		_crouchCollision.Disabled = !_crouching;
	}

	private void UpdateBlock(double delta)
	{
		bool holdingBlock = _abilities.Has(PlayerAbilities.Block) && Input.IsActionPressed("block")
			&& !_attacking && !_healing && !_isDashing && !_isRolling && !_isRunThrusting;

		if (holdingBlock && !_isBlocking)
			_parryWindowTimer = ParryWindowDuration;

		_isBlocking = holdingBlock;
		_stats.RegenRateMultiplier = holdingBlock ? BlockStaminaRegenMultiplier : 1f;

		if (_parryWindowTimer > 0f)
			_parryWindowTimer -= (float)delta;
	}

	private bool TryBlockIncomingHit()
	{
		if (!_isBlocking)
			return false;

		if (_parryWindowTimer > 0f)
		{
			_parryFlash.Visible = true;
			_parryFlash.Play("parry_flash");
			ShowParryBubble();
		}
		else
		{
			_stats.SpendStaminaClamped(Mathf.RoundToInt(_stats.MaxStamina * BlockStaminaCostFraction));
		}

		return true;
	}

	// Blooms in, holds, then fades out — a lingering energy shield rather than a single-frame
	// flash (see ParryFlash for that instead). Killing any tween already running on _parryBubble
	// first means mashing parries in quick succession restarts the bloom cleanly instead of two
	// tweens fighting over the same modulate alpha.
	private void ShowParryBubble()
	{
		_parryBubbleTween?.Kill();
		_parryBubble.Visible = true;
		_parryBubble.Modulate = new Color(1f, 1f, 1f, 0f);

		_parryBubbleTween = GetTree().CreateTween();
		_parryBubbleTween.TweenProperty(_parryBubble, "modulate:a", 1f, ParryBubbleFadeIn);
		_parryBubbleTween.TweenInterval(ParryBubbleHold);
		_parryBubbleTween.TweenProperty(_parryBubble, "modulate:a", 0f, ParryBubbleFadeOut);
		_parryBubbleTween.TweenCallback(Callable.From(() => _parryBubble.Visible = false));
	}

	private void UpdateCameraLook(double delta)
	{
		bool lookingUp = Input.IsActionPressed("move_up");
		float targetY = BaseCameraOffsetY + ProfileCameraOffsetY + (lookingUp ? LookUpOffset : _crouching ? LookDownOffset : 0f);

		Vector2 targetOffset = new Vector2(0, targetY + _bossFramingOffsetY);
		// Tracked separately from _camera.Offset so Shake() can add a jitter on top of it each
		// frame without that jitter itself getting lerped-toward next frame (which would just
		// smooth the shake away to nothing).
		_lookOffset = _lookOffset.Lerp(targetOffset, (float)delta * LookSmoothSpeed);
		_camera.Offset = _lookOffset + GetShakeOffset(delta);

		float targetHeadAngle = lookingUp ? HeadLookUpAngle : 0f;
		_head.RotationDegrees = Mathf.Lerp(_head.RotationDegrees, targetHeadAngle, (float)delta * LookSmoothSpeed);

		UpdateBossZoom(delta);
	}

	// strength is in pixels; the shake decays linearly to 0 over duration.
	public void ShakeCamera(float strength, float duration)
	{
		_shakeStrength = strength;
		_shakeDuration = Mathf.Max(0.0001f, duration);
		_shakeTimer = duration;
	}

	private Vector2 GetShakeOffset(double delta)
	{
		if (_shakeTimer <= 0f)
			return Vector2.Zero;

		_shakeTimer -= (float)delta;
		float t = Mathf.Clamp(_shakeTimer / _shakeDuration, 0f, 1f);
		float strength = _shakeStrength * t;
		return new Vector2(
			(float)GD.RandRange(-1.0, 1.0) * strength,
			(float)GD.RandRange(-1.0, 1.0) * strength);
	}

	private void UpdateBossZoom(double delta)
	{
		float targetZoom = ProfileZoom;

		Godot.Collections.Array<Node> bosses = GetTree().GetNodesInGroup("boss");
		Enemy bossEnemy = bosses.Count == 1 ? bosses[0] as Enemy : null;

		// A boss sitting in the group from scene load isn't an active encounter yet — wait for the
		// same PlayerDetected flip CaveCollapseOverlay reacts to, then latch it (instead of
		// re-checking PlayerDetected live) so the bar doesn't blink off if the fight carries the
		// player back out of DetectionRange for a moment.
		if (bossEnemy != _trackedBossEnemy)
		{
			_trackedBossEnemy = bossEnemy;
			_bossEncounterActive = false;
		}

		if (bossEnemy is not null && bossEnemy.PlayerDetected)
			_bossEncounterActive = true;

		Node2D boss = _bossEncounterActive ? bossEnemy : null;

		if (boss != _trackedBoss)
			TrackBoss(boss);

		float framingOffsetY = 0f;
		if (boss is not null)
		{
			float distance = GlobalPosition.DistanceTo(boss.GlobalPosition);
			float t = Mathf.Clamp(distance / BossZoomDistance, 0f, 1f);
			targetZoom = ProfileZoom * Mathf.Lerp(BossZoomInMultiplier, 1f, t);

			// Boss high above (e.g. MushroomBoss's scarlet-flower leap): shift the camera up halfway
			// toward it and pull back just enough to keep both in frame, instead of losing it off the top.
			float above = GlobalPosition.Y - boss.GlobalPosition.Y;
			if (above > BossFramingThreshold)
			{
				framingOffsetY = -above * 0.5f;
				float halfViewHeight = GetViewportRect().Size.Y * 0.5f;
				float requiredZoom = halfViewHeight / (above * 0.5f + BossFramingMargin);
				targetZoom = Mathf.Min(targetZoom, Mathf.Max(requiredZoom, BossFramingMinZoom));
			}
		}
		_bossFramingOffsetY = Mathf.Lerp(_bossFramingOffsetY, framingOffsetY, (float)delta * ZoomSmoothSpeed);

		float newZoom = Mathf.Lerp(_camera.Zoom.X, targetZoom, (float)delta * ZoomSmoothSpeed);
		_camera.Zoom = new Vector2(newZoom, newZoom);
	}

	// Only (re)subscribes when the tracked boss actually changes (not every frame) — the closure
	// below double-checks _trackedBoss == boss before touching the bar, so a stale subscription
	// from a since-defeated boss just quietly no-ops instead of needing an explicit unsubscribe.
	private void TrackBoss(Node2D boss)
	{
		_trackedBoss = boss;
		_bossHealthBar.Visible = boss is not null;
		if (boss is null)
			return;

		Stats bossStats = boss.GetNodeOrNull<Stats>("Stats");
		if (bossStats is null)
			return;

		_bossHealthBar.SetRatio((float)bossStats.CurrentHealth / bossStats.MaxHealth);
		bossStats.HealthChanged += (current, max) =>
		{
			if (_trackedBoss == boss)
				_bossHealthBar.SetRatio((float)current / max);
		};
	}

	private async void StartDash()
	{
		if (!_stats.TrySpendStamina(DashStaminaCost))
			return;

		_isDashing = true;
		_canDash = false;
		_dashDirection = _facingRight ? 1f : -1f;
		_stats.ExternalInvulnerable = true;
		RunDashGhosts();

		await ToSignal(GetTree().CreateTimer(DashDuration), SceneTreeTimer.SignalName.Timeout);
		_isDashing = false;

		float remainingIframeTime = Mathf.Max(0f, DashIframeDuration - DashDuration);
		if (remainingIframeTime > 0f)
			await ToSignal(GetTree().CreateTimer(remainingIframeTime), SceneTreeTimer.SignalName.Timeout);
		// The wind shield keeps its own invulnerability going if it was raised mid-dash.
		_stats.ExternalInvulnerable = _windShieldActive;

		await ToSignal(GetTree().CreateTimer(DashCooldown), SceneTreeTimer.SignalName.Timeout);
		_canDash = true;
	}

	// Neon afterimages while dashing: every DashGhostInterval a copy of the current frame is left
	// behind in the world, additive-tinted in the same colour as the sword trails (blue, or orange
	// while fire-imbued), fading out over DashGhostLifetime. Plain Sprite2Ds parented to the scene
	// (not the player) so they stay where they were dropped.
	private async void RunDashGhosts()
	{
		while (_isDashing && IsInstanceValid(this))
		{
			SpawnGhost(DashGhostAlpha, DashGhostIntensity, DashGhostLifetime);
			await ToSignal(GetTree().CreateTimer(DashGhostInterval), SceneTreeTimer.SignalName.Timeout);
		}
	}

	// Subtler version of the dash ghosts for the rising part of a jump (and double jump). Ends
	// at the apex, on landing, or when a dash takes over with its own brighter ghosts. The run id
	// stops a double jump from stacking a second loop on top of the first.
	private async void RunJumpGhosts()
	{
		int runId = ++_jumpGhostRunId;
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		while (IsInstanceValid(this) && runId == _jumpGhostRunId && Velocity.Y < 0f && !IsOnFloor() && !_isDashing)
		{
			SpawnGhost(JumpGhostAlpha, JumpGhostIntensity, JumpGhostLifetime);
			await ToSignal(GetTree().CreateTimer(JumpGhostInterval), SceneTreeTimer.SignalName.Timeout);
		}
	}

	private void LaunchJump(ref Vector2 velocity)
	{
		if (IsOnFloor())
			SpawnDust("jump_dust");
		velocity.Y = JumpVelocity;
		// _jumpCount still holds its pre-increment value here, so >=1 means this is the second
		// (double) jump about to happen.
		bool isDoubleJump = !_infiniteJumpDebug && _jumpCount >= 1;
		Sfx.Play(this, "Voice/Player", isDoubleJump ? "DobleSalto" : "Salto");
		if (!_infiniteJumpDebug)
		{
			_jumpCount++;
			_isDoubleJumping = _jumpCount >= 2;
		}
		RunJumpGhosts();
	}

	// One-shot dust at the feet, left in the world (not parented to the player) so it stays put
	// while the player moves off. Sized like the character sprite so the pixel scale matches.
	private void SpawnDust(string animation)
	{
		if (DustFrames is null || !DustFrames.HasAnimation(animation) || GetTree().CurrentScene is not Node scene)
			return;

		Texture2D first = DustFrames.GetFrameTexture(animation, 0);
		var dust = new AnimatedSprite2D
		{
			SpriteFrames = DustFrames,
			Offset = new Vector2(0f, -first.GetHeight() / 2f),
			Scale = new Vector2(Mathf.Abs(_sprite.Scale.X), _sprite.Scale.Y),
			FlipH = !_facingRight,
			ZIndex = ZIndex,
		};
		scene.AddChild(dust);
		dust.GlobalPosition = GlobalPosition + new Vector2(0f, _standHalfHeight);
		dust.AnimationFinished += dust.QueueFree;
		dust.Play(animation);
	}

	private void SpawnGhost(float alpha, float intensity, float lifetime)
	{
		Texture2D frame = _sprite.SpriteFrames?.GetFrameTexture(_sprite.Animation, _sprite.Frame);
		if (frame is null || GetTree().CurrentScene is not Node scene)
			return;

		Color tint = ElementGhostColor();
		// Solid neon silhouette (the frame's alpha filled with the tint, additive) — tinting the
		// sprite's own colours instead left the mostly dark character nearly invisible.
		var material = NeonSilhouette.CreateMaterial(tint, intensity);
		var ghost = new Sprite2D
		{
			Texture = frame,
			Centered = _sprite.Centered,
			Offset = _sprite.Offset,
			FlipH = _sprite.FlipH,
			Material = material,
			Modulate = new Color(1f, 1f, 1f, alpha),
			ZIndex = ZIndex - 1,
		};
		scene.AddChild(ghost);
		ghost.GlobalTransform = _sprite.GlobalTransform;

		Tween fade = ghost.CreateTween();
		// Ease-in: stays bright most of its life, then drops off — a linear fade read as dim navy.
		fade.TweenProperty(ghost, "modulate:a", 0f, lifetime).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
		fade.TweenCallback(Callable.From(ghost.QueueFree));
	}

	private async void StartRoll()
	{
		if (!_stats.TrySpendStamina(RollStaminaCost))
			return;

		_isRolling = true;
		_canRoll = false;
		_rollDirection = _facingRight ? 1f : -1f;

		await ToSignal(GetTree().CreateTimer(RollDuration), SceneTreeTimer.SignalName.Timeout);
		_isRolling = false;

		await ToSignal(GetTree().CreateTimer(RollCooldown), SceneTreeTimer.SignalName.Timeout);
		_canRoll = true;
	}

	private async void StartCharging()
	{
		_isCharging = true;
		_attacking = true;
		// Sheets with a dedicated windup play charge -> charge_hold (loop); older ones fall back
		// to holding the guard pose.
		bool hasChargeAnims = _sprite.SpriteFrames.HasAnimation("charge") && _sprite.SpriteFrames.HasAnimation("charge_hold");
		_currentAttackAnimation = hasChargeAnims ? "charge" : "idle_block";

		// No effects while building up — the only cue is a red blink once the charge is ready.
		Tween readyBlink = null;
		float heldTime = 0f;
		while (Input.IsActionPressed("charged_attack"))
		{
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			heldTime += (float)GetPhysicsProcessDeltaTime();
			if (_currentAttackAnimation == "charge" && _sprite.Animation == "charge" && !_sprite.IsPlaying())
				_currentAttackAnimation = "charge_hold";

			if (readyBlink is null && heldTime >= ChargedAttackDuration)
			{
				readyBlink = CreateTween().SetLoops();
				readyBlink.TweenProperty(_visual, "modulate", ChargedReadyBlinkColor, 0.1f);
				readyBlink.TweenProperty(_visual, "modulate", Colors.White, 0.1f);
			}
		}

		readyBlink?.Kill();
		_visual.Modulate = Colors.White;

		if (heldTime < ChargedAttackDuration)
		{
			// Released before finishing the charge — no attack, just drop back to normal.
			_isCharging = false;
			_attacking = false;
			return;
		}

		bool hasChargedAnim = _sprite.SpriteFrames.HasAnimation("charged_attack");
		_currentAttackAnimation = hasChargedAnim ? "charged_attack" : "attack3";

		bool hitLanded = false;
		void OnHitLanded() => hitLanded = true;
		_hitbox.HitDealt += OnHitLanded;

		_hitboxShape.Size = ChargedAttackHitboxSize;
		_hitbox.Position = new Vector2(_facingRight ? ChargedAttackReach : -ChargedAttackReach, 0);
		_hitbox.Activate(_stats, element: _currentElement);

		RunChargedNeonTrail();

		await ToSignal(GetTree().CreateTimer(AttackDuration), SceneTreeTimer.SignalName.Timeout);
		_hitbox.Deactivate();
		_hitbox.HitDealt -= OnHitLanded;
		if (!hitLanded)
			Sfx.Play(this, "Combat/Sword", "Sword Attack");

		// Let the full lunge + recover play out when the sheet has it, never shorter than the old
		// fixed 0.2s tail.
		float tail = 0.2f;
		if (hasChargedAnim)
		{
			float clipLength = _sprite.SpriteFrames.GetFrameCount("charged_attack") / (float)_sprite.SpriteFrames.GetAnimationSpeed("charged_attack");
			tail = Mathf.Max(tail, clipLength - AttackDuration);
		}
		await ToSignal(GetTree().CreateTimer(tail), SceneTreeTimer.SignalName.Timeout);
		_isCharging = false;
		_attacking = false;
	}

	private void StartPound()
	{
		if (!_stats.TrySpendStamina(AttackStaminaCost))
			return;

		_attacking = true;
		_isPounding = true;
		_poundHitLanded = false;
		// Sheets with a real plunge (start -> loop, then a landing clip) use it; older ones fake
		// the dive by turning attack3 sideways.
		bool hasDownAttack = _sprite.SpriteFrames.HasAnimation("down_attack_start")
			&& _sprite.SpriteFrames.HasAnimation("down_attack_loop");
		_currentAttackAnimation = hasDownAttack ? "down_attack_start" : "attack3";

		_hitboxShape.Size = PoundHitboxSize;
		_hitbox.Position = new Vector2(0, PoundHitboxReach);
		_hitbox.Activate(_stats, element: _currentElement);

		if (!hasDownAttack)
			_sprite.Rotation = Mathf.Pi / 2f;

		_hitbox.HitDealt += OnPoundHit;
		RunPoundFallTrail();
	}

	// While diving: a neon line anchored where the dive began and stretched down to the blade
	// (thin at the top, full width at the blade — the same taper as the sword trails), with faint
	// afterimages stacking up behind the body. Ends when the dive does; a landing hands over to
	// the slam's own trail (PlayDownAttackLanding), a bounce/cancel just fades it out.
	private async void RunPoundFallTrail()
	{
		if (!_sprite.SpriteFrames.HasAnimation("down_attack_loop"))
			return;

		int runId = ++_swordArcRunId;
		Color tint = ElementGhostColor();
		Vector2 frameSize = _sprite.SpriteFrames.GetFrameTexture("down_attack_loop", 0).GetSize();
		Vector2 bladeLocal = _sprite.Position + (PoundTrailBladePixel - frameSize / 2f + _sprite.Offset) * _sprite.Scale;
		Vector2 startGlobal = _visual.ToGlobal(bladeLocal);

		_swordArcCore.Modulate = tint;
		_swordArcGlow.Modulate = tint;
		_swordArcCore.Width = _swordArcBaseCoreWidth * 1.8f;
		_swordArcGlow.Width = _swordArcBaseGlowWidth * 1.8f;
		PrepareTrailSparks(16, tint);
		_swordArcSparks.Emitting = true;

		float ghostTimer = 0f;
		while (_isPounding)
		{
			Vector2 bladeGlobal = _visual.ToGlobal(bladeLocal);
			Vector2[] points = { _swordArcCore.ToLocal(startGlobal), _swordArcCore.ToLocal(startGlobal.Lerp(bladeGlobal, 0.5f)), _swordArcCore.ToLocal(bladeGlobal) };
			_swordArcCore.Points = points;
			_swordArcGlow.Points = points;
			_swordArcSparks.Position = points[^1];

			ghostTimer -= (float)GetPhysicsProcessDeltaTime();
			if (ghostTimer <= 0f)
			{
				SpawnGhost(JumpGhostAlpha * 1.5f, JumpGhostIntensity, JumpGhostLifetime * 1.5f);
				ghostTimer = JumpGhostInterval;
			}

			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			if (!IsInstanceValid(this) || runId != _swordArcRunId)
				return;
		}

		_swordArcSparks.Emitting = false;
		Tween fadeTween = CreateTween();
		fadeTween.TweenProperty(_swordArcCore, "modulate:a", 0f, 0.18f);
		fadeTween.Parallel().TweenProperty(_swordArcGlow, "modulate:a", 0f, 0.18f);
	}

	private void OnPoundHit()
	{
		// Just raise the flag here — _PhysicsProcess applies the bounce and deactivates the
		// hitbox next frame. See the comment at the _isPounding branch for why.
		_poundHitLanded = true;
	}

	private void EndPound()
	{
		if (!_isPounding)
			return;

		_isPounding = false;
		_attacking = false;
		_sprite.Rotation = 0f;
		_weaponTrail.Rotation = 0f;
		_hitbox.Deactivate();
		_hitbox.HitDealt -= OnPoundHit;
	}

	// Bouncing off an enemy still shows the slam: the landing clip's crescent frame alone
	// ("down_attack_slam"), held for PoundBounceSlamHold while rising, with the same slam trail —
	// without the grounded crouch/get-up frames, which would read wrong in mid-air.
	private async void PlayDownAttackBounceSlam()
	{
		if (!_sprite.SpriteFrames.HasAnimation("down_attack_slam"))
			return;

		_attacking = true;
		_currentAttackAnimation = "down_attack_slam";
		RunPathNeonTrail("down_attack_landing", PoundLandingTrailPath, PoundLandingTrailTiming.Y, PoundLandingTrailTiming.X, 1.8f, 20);
		await ToSignal(GetTree().CreateTimer(PoundBounceSlamHold), SceneTreeTimer.SignalName.Timeout);
		if (_currentAttackAnimation == "down_attack_slam")
			_attacking = false;
	}

	// The plunge's touchdown recovery — only when the dive actually reached the floor (an enemy
	// bounce or a water cancel goes straight back to normal air/swim animations).
	private async void PlayDownAttackLanding()
	{
		if (!_sprite.SpriteFrames.HasAnimation("down_attack_landing"))
			return;

		_attacking = true;
		_currentAttackAnimation = "down_attack_landing";
		RunPathNeonTrail("down_attack_landing", PoundLandingTrailPath, PoundLandingTrailTiming.Y, PoundLandingTrailTiming.X, 1.8f, 20);
		float clipLength = _sprite.SpriteFrames.GetFrameCount("down_attack_landing") / (float)_sprite.SpriteFrames.GetAnimationSpeed("down_attack_landing");
		await ToSignal(GetTree().CreateTimer(clipLength), SceneTreeTimer.SignalName.Timeout);
		if (_currentAttackAnimation == "down_attack_landing")
			_attacking = false;
	}

	private async void UpAttack()
	{
		if (!_canUpAttack)
			return;

		_canUpAttack = false;
		if (!_stats.TrySpendStamina(AttackStaminaCost))
		{
			_canUpAttack = true;
			return;
		}

		_attacking = true;
		// Sheets with the air combo's rising slash use it for the up attack (it's the pack's only
		// upward swing), with that slash's trail pushed further out to match this hitbox's longer
		// upward reach. Older sheets keep attack1 + the rotated sprite trail.
		bool sheetUpSlash = _sprite.SpriteFrames.HasAnimation("air_attack1") && AirAttack1TrailPath.Length >= 2;
		_currentAttackAnimation = sheetUpSlash ? "air_attack1" : "attack1";

		bool hitLanded = false;
		void OnHitLanded() => hitLanded = true;
		_hitbox.HitDealt += OnHitLanded;

		if (sheetUpSlash)
		{
			var reachPath = new Vector2[AirAttack1TrailPath.Length];
			for (int i = 0; i < reachPath.Length; i++)
				reachPath[i] = UpAttackTrailPivot + (AirAttack1TrailPath[i] - UpAttackTrailPivot) * UpAttackTrailReachScale;
			RunPathNeonTrail("air_attack1", reachPath, AirAttack1TrailTiming.Y, AirAttack1TrailTiming.X, 1.8f, 18);
		}
		else
		{
			_weaponTrail.Rotation = -Mathf.Pi / 2f;
		}

		if (AttackHitboxDelay > 0f)
			await ToSignal(GetTree().CreateTimer(AttackHitboxDelay), SceneTreeTimer.SignalName.Timeout);

		_hitboxShape.Size = UpAttackHitboxSize;
		_hitbox.Position = new Vector2(0, -UpAttackHitboxReach);
		_hitbox.Activate(_stats, element: _currentElement);

		if (!sheetUpSlash)
		{
			_weaponTrail.Position = new Vector2(0, -UpAttackHitboxReach);
			string upAttackTrail = WeaponTrailAnimation(_currentAttackAnimation);
			_weaponTrail.Scale = WeaponTrailScale(upAttackTrail);
			_weaponTrail.Visible = true;
			_weaponTrail.Play(upAttackTrail);
		}

		await ToSignal(GetTree().CreateTimer(AttackDuration), SceneTreeTimer.SignalName.Timeout);
		_hitbox.Deactivate();
		_hitbox.HitDealt -= OnHitLanded;
		if (!hitLanded)
			Sfx.Play(this, "Combat/Sword", "Sword Attack");

		_weaponTrail.Rotation = 0f;

		float remainingAnimTime = Mathf.Max(0f, AttackAnimDuration - AttackHitboxDelay - AttackDuration);
		await ToSignal(GetTree().CreateTimer(remainingAnimTime), SceneTreeTimer.SignalName.Timeout);
		_attacking = false;

		await ToSignal(GetTree().CreateTimer(UpAttackCooldown), SceneTreeTimer.SignalName.Timeout);
		_canUpAttack = true;
	}

	// Flying sword (G, replaces the bow on sheets that have the clips): throw (flying_sword_start),
	// kneel concentrating while it's out (flying_sword_loop), catch (flying_sword_end). The sword
	// itself is a FlyingSword boomerang that hits on the way out and back.
	private async void ThrowFlyingSword()
	{
		if (_swordOut || !_stats.TrySpendStamina(FlyingSwordStaminaCost))
			return;

		_isShooting = true;
		_shootAnimation = "flying_sword_start";
		bool facingRight = _facingRight;

		await ToSignal(GetTree().CreateTimer(FlyingSwordReleaseDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || _isDead || GetTree().CurrentScene is not Node scene)
		{
			_shootAnimation = null;
			_isShooting = false;
			return;
		}

		var sword = new FlyingSword();
		Vector2 catchOffset = new(0f, FlyingSwordHeight);
		sword.Setup(this, catchOffset, facingRight ? 1f : -1f,
			Mathf.Max(1, Mathf.RoundToInt(_stats.AttackPower * FlyingSwordDamageMultiplier)), _currentElement, ElementGhostColor());
		sword.MaxDistance = FlyingSwordRange;
		_swordOut = true;
		sword.Returned += () => _swordOut = false;
		scene.AddChild(sword);
		sword.GlobalPosition = GlobalPosition + new Vector2(facingRight ? 24f : -24f, FlyingSwordHeight);
		Sfx.Play(this, "Combat/Sword", "Sword Attack");

		_shootAnimation = "flying_sword_loop";
		while (_swordOut && IsInstanceValid(sword))
		{
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			if (!IsInstanceValid(this))
				return;
		}
		_swordOut = false;

		_shootAnimation = "flying_sword_end";
		float catchTime = _sprite.SpriteFrames.GetFrameCount("flying_sword_end") / (float)_sprite.SpriteFrames.GetAnimationSpeed("flying_sword_end");
		await ToSignal(GetTree().CreateTimer(catchTime), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this))
			return;
		_shootAnimation = null;
		_isShooting = false;
	}

	private async void ShootBow()
	{
		_isShooting = true;

		await ToSignal(GetTree().CreateTimer(BowReleaseDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || _isDead)
			return;

		Vector2 direction = new(_facingRight ? 1f : -1f, 0f);
		Projectile arrow = ArrowProjectileScene.Instantiate<Projectile>();
		GetTree().CurrentScene.AddChild(arrow);
		arrow.GlobalPosition = GlobalPosition + new Vector2(0, ArrowSpawnYOffset);
		arrow.Speed = ArrowSpeed;
		arrow.Launch(direction, _stats);
		Sfx.Play(this, "Combat/Bow", "Bow Attack");

		float remainingAnimTime = Mathf.Max(0f, BowAnimDuration - BowReleaseDelay);
		await ToSignal(GetTree().CreateTimer(remainingAnimTime), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			_isShooting = false;
	}

	// Plays the "spell"/"spell2" clip only — no effect/projectile yet, see the SpellAnimDuration
	// export doc. Split into two methods (rather than one CastSpell(int slot)) to match how every
	// other single-purpose action here (ShootBow, StartDash, StartRoll...) is its own method.
	// Spell 1: shadow clone. Partway through the "spell" clip a neon silhouette of the player pops
	// in beside them (facing side), throws one normal attack1 with the player's own hitbox size,
	// reach, timing and AttackPower, then fades out (see ShadowClone). Costs stamina, has its own
	// cooldown on top of the cast animation.
	private async void CastSpell()
	{
		double now = Time.GetTicksMsec() / 1000.0;
		if (now < _shadowCloneReadyAt || !_stats.TrySpendStamina(ShadowCloneStaminaCost))
			return;

		_isCastingSpell = true;
		_shadowCloneReadyAt = now + ShadowCloneCooldown;

		await ToSignal(GetTree().CreateTimer(ShadowCloneSpawnDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || _isDead)
			return;
		SpawnShadowClone();

		await ToSignal(GetTree().CreateTimer(Mathf.Max(0f, SpellAnimDuration - ShadowCloneSpawnDelay)), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			_isCastingSpell = false;
	}

	private void SpawnShadowClone()
	{
		if (GetTree().CurrentScene is not Node scene)
			return;

		var clone = new ShadowClone();
		clone.Setup(_sprite.SpriteFrames, "attack1", _facingRight, _sprite.Position, _sprite.Offset, _sprite.Scale, _stats,
			ElementGhostColor(), ShadowCloneIntensity, ShadowCloneAlpha,
			_hitboxBaseSize, AttackHitboxReach, AttackHitboxYOffset, AttackHitboxDelay, AttackDuration,
			ShadowCloneFadeIn, ShadowCloneFadeOut);
		clone.ZIndex = ZIndex;
		scene.AddChild(clone);
		clone.GlobalPosition = GlobalPosition + new Vector2(_facingRight ? ShadowCloneOffsetX : -ShadowCloneOffsetX, 0f);
	}

	// Plays the "spell" clip and releases a Tornado in front of the player, heading the way they face.
	private async void CastTornado()
	{
		double now = Time.GetTicksMsec() / 1000.0;
		if (now < _tornadoReadyAt || !_stats.TrySpendStamina(TornadoStaminaCost))
			return;

		_isCastingSpell = true;
		_tornadoReadyAt = now + TornadoCooldown;
		bool facingRight = _facingRight;

		await ToSignal(GetTree().CreateTimer(TornadoCastDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || _isDead || GetTree().CurrentScene is not Node scene)
			return;

		SpawnTornadoVariant(scene, facingRight);

		await ToSignal(GetTree().CreateTimer(Mathf.Max(0f, SpellAnimDuration - TornadoCastDelay)), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			_isCastingSpell = false;
	}

	// Viento variants: which pattern key 1 casts, picked with tornado_cycle (selectable, they don't
	// stack). Damage per tornado = AttackPower × TornadoDamageMultiplier, scaled per variant below.
	public enum TornadoVariant { Normal, Double, Bidirectional, Mini, Growing }

	private static readonly string[] TornadoVariantNameKeys =
	{
		"UI_TORNADO_NORMAL", "UI_TORNADO_DOUBLE", "UI_TORNADO_BIDIRECTIONAL", "UI_TORNADO_MINI", "UI_TORNADO_GROWING",
	};

	private void CycleTornadoVariant()
	{
		int count = TornadoVariantNameKeys.Length;
		_tornadoVariant = (TornadoVariant)(((int)_tornadoVariant + 1) % count);
		SaveManager.Instance.SessionTornadoVariant = (int)_tornadoVariant;
		_tornadoHud?.Refresh((int)_tornadoVariant);
	}

	private void SpawnTornadoVariant(Node scene, bool facingRight)
	{
		float facing = facingRight ? 1f : -1f;
		int damage = Mathf.Max(1, Mathf.RoundToInt(_stats.AttackPower * TornadoDamageMultiplier));
		// Ground tornadoes stand on the floor under the player; airborne minis start at chest height.
		Vector2 ground = GlobalPosition + new Vector2(0f, BodyHalfHeight());

		Metroidvania.World.Tornado Spawn(Vector2 travel, Vector2 at, int power, float size = 1f, bool growing = false, float lifetime = -1f)
		{
			var tornado = new Metroidvania.World.Tornado();
			tornado.Setup(travel, power, _currentElement, size, growing);
			if (lifetime > 0f)
				tornado.Lifetime = lifetime;
			scene.AddChild(tornado);
			tornado.GlobalPosition = at;
			return tornado;
		}

		switch (_tornadoVariant)
		{
			case TornadoVariant.Double:
				Spawn(new Vector2(facing, 0f), ground + new Vector2(facing * TornadoOffsetX, 0f), damage);
				Spawn(new Vector2(facing, 0f), ground + new Vector2(facing * (TornadoOffsetX + DoubleTornadoGap), 0f), damage);
				break;
			case TornadoVariant.Bidirectional:
				Spawn(Vector2.Right, ground + new Vector2(TornadoOffsetX, 0f), damage);
				Spawn(Vector2.Left, ground + new Vector2(-TornadoOffsetX, 0f), damage);
				break;
			case TornadoVariant.Mini:
				int miniDamage = Mathf.Max(1, Mathf.RoundToInt(damage * MiniTornadoDamageMultiplier));
				Vector2 chest = GlobalPosition + new Vector2(facing * TornadoOffsetX * 0.6f, -MiniTornadoLaunchHeight);
				foreach (float angle in MiniTornadoAngles)
				{
					Vector2 dir = new Vector2(facing, 0f).Rotated(Mathf.DegToRad(angle) * facing);
					Spawn(dir, chest, miniDamage, MiniTornadoScale, lifetime: MiniTornadoLifetime);
				}
				break;
			case TornadoVariant.Growing:
				Spawn(new Vector2(facing, 0f), ground + new Vector2(facing * TornadoOffsetX, 0f), damage, GrowingTornadoStartScale, growing: true);
				break;
			default:
				Spawn(new Vector2(facing, 0f), ground + new Vector2(facing * TornadoOffsetX, 0f), damage);
				break;
		}
	}

	private async void StartWindShield()
	{
		double now = Time.GetTicksMsec() / 1000.0;
		if (now < _windShieldReadyAt || !_stats.TrySpendStamina(WindShieldStaminaCost))
			return;
		_windShieldReadyAt = now + WindShieldCooldown;
		_windShieldActive = true;
		_stats.ExternalInvulnerable = true;

		// A short, wide, nearly straight column hugging the body (not a cone), parented to the
		// player so it moves with them; drawn over the player so they read as inside it.
		var shield = new Metroidvania.World.Tornado
		{
			Height = 110f,
			TipRadius = 34f,
			TopRadius = 48f,
			ConePower = 0.3f,
			BandCount = 7f,
			ThicknessBottom = 7f,
			ThicknessTop = 10f,
			Flatten = 0.35f,
			SwayAmplitude = 4f,
			HitHalfWidth = 40f,
			Speed = 0f,
			Lifetime = WindShieldDuration,
			GrowTime = 0.15f,
			FadeTime = 0.25f,
			TickInterval = 0.25f,
			ZIndex = 1,
		};
		shield.Setup(Vector2.Right, Mathf.Max(1, Mathf.RoundToInt(_stats.AttackPower * WindShieldDamageMultiplier)), _currentElement);
		AddChild(shield);
		shield.Position = new Vector2(0f, BodyHalfHeight());

		await ToSignal(GetTree().CreateTimer(WindShieldDuration), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this))
			return;
		_windShieldActive = false;
		_stats.ExternalInvulnerable = _isDashing;
	}

	private async void CastHurricane()
	{
		double now = Time.GetTicksMsec() / 1000.0;
		int needed = Mathf.CeilToInt(_stats.MaxStamina * HurricaneStaminaFraction);
		if (now < _hurricaneReadyAt || _stats.CurrentStamina < needed || _camera is null)
			return;
		_stats.SpendStaminaClamped(_stats.MaxStamina);
		_hurricaneReadyAt = now + HurricaneCooldown;
		_isCastingSpell = true;
		DamageElement element = _currentElement;

		await ToSignal(GetTree().CreateTimer(TornadoCastDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || _isDead || GetTree().CurrentScene is not Node scene)
			return;

		// Sized from what the camera shows right now: the funnel's point sits at the bottom-centre
		// of the screen and it opens up to cover the whole view.
		Vector2 view = GetViewport().GetVisibleRect().Size / _camera.Zoom;
		Vector2 screenCenter = _camera.GetScreenCenterPosition();
		float height = view.Y * 1.02f;
		var hurricane = new Metroidvania.World.Tornado
		{
			Height = height,
			TipRadius = 40f,
			TopRadius = view.X * 0.43f,
			ConePower = 0.35f,
			BandCount = Mathf.Round(height / 30f),
			ThicknessBottom = 14f,
			ThicknessTop = 46f,
			// Rounder, thinner rings than the default proportions: very thick lines on very flat
			// ellipses fold over themselves at the ring ends (wedge artifacts).
			Flatten = 0.24f,
			SwayAmplitude = 12f,
			HitHalfWidth = view.X * 0.5f,
			Speed = 0f,
			Lifetime = HurricaneDuration,
			GrowTime = 0.35f,
			FadeTime = 0.45f,
			TickInterval = HurricaneTickInterval,
			ZIndex = 2,
		};
		hurricane.Setup(Vector2.Right, Mathf.Max(1, Mathf.RoundToInt(_stats.AttackPower * HurricaneDamageMultiplier)), element);
		scene.AddChild(hurricane);
		hurricane.GlobalPosition = screenCenter + new Vector2(0f, view.Y * 0.5f);

		await ToSignal(GetTree().CreateTimer(Mathf.Max(0f, SpellAnimDuration - TornadoCastDelay)), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			_isCastingSpell = false;
	}

	// Plays the "spell" clip and lobs the spiders' PoisonSpit forward, retargeted at enemies (its
	// mask hits world + enemies, not the player, and its puddle poisons the "enemy" group).
	private async void CastPoisonSpit()
	{
		double now = Time.GetTicksMsec() / 1000.0;
		if (PoisonSpitScene is null || now < _poisonSpitReadyAt || !_stats.TrySpendStamina(PoisonSpitStaminaCost))
			return;

		_isCastingSpell = true;
		_poisonSpitReadyAt = now + PoisonSpitCooldown;
		bool facingRight = _facingRight;

		await ToSignal(GetTree().CreateTimer(PoisonSpitCastDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || _isDead || GetTree().CurrentScene is not Node scene)
			return;

		var spit = PoisonSpitScene.Instantiate<Metroidvania.World.PoisonSpit>();
		// World (1) + enemies (4); not the player (2).
		spit.CollisionMask = 1u | 4u;
		spit.ContactDamage = _stats.AttackPower;
		spit.PoisonTickDamage = PoisonSpitTickDamage;
		spit.PuddleTargetGroup = "enemy";
		spit.PuddleCollisionMask = 4u;
		scene.AddChild(spit);
		spit.GlobalPosition = GlobalPosition + new Vector2(facingRight ? 20f : -20f, PoisonSpitSpawnYOffset);
		spit.Launch(new Vector2(facingRight ? PoisonSpitVelocity.X : -PoisonSpitVelocity.X, PoisonSpitVelocity.Y), _stats);

		await ToSignal(GetTree().CreateTimer(Mathf.Max(0f, SpellAnimDuration - PoisonSpitCastDelay)), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			_isCastingSpell = false;
	}

	// Plays the "spell" clip and fires a Fireball straight ahead.
	private async void CastFireball()
	{
		double now = Time.GetTicksMsec() / 1000.0;
		if (now < _fireballReadyAt || !_stats.TrySpendStamina(FireballStaminaCost))
			return;

		_isCastingSpell = true;
		_fireballReadyAt = now + FireballCooldown;
		bool facingRight = _facingRight;

		await ToSignal(GetTree().CreateTimer(FireballCastDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || _isDead || GetTree().CurrentScene is not Node scene)
			return;

		var fireball = new Metroidvania.World.Fireball();
		fireball.Launch(new Vector2(facingRight ? 1f : -1f, 0f),
			Mathf.Max(1, Mathf.RoundToInt(_stats.AttackPower * FireballDamageMultiplier)), _stats);
		scene.AddChild(fireball);
		fireball.GlobalPosition = GlobalPosition + new Vector2(facingRight ? 24f : -24f, FireballSpawnYOffset);

		await ToSignal(GetTree().CreateTimer(Mathf.Max(0f, SpellAnimDuration - FireballCastDelay)), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			_isCastingSpell = false;
	}

	private float BodyHalfHeight() =>
		GetNodeOrNull<CollisionShape2D>("CollisionShape2D")?.Shape is RectangleShape2D rect ? rect.Size.Y / 2f
		: GetNodeOrNull<CollisionShape2D>("CollisionShape2D")?.Shape is CapsuleShape2D capsule ? capsule.Height / 2f
		: 30f;

	private async void CastSpell2()
	{
		_isCastingSpell2 = true;
		await ToSignal(GetTree().CreateTimer(Spell2AnimDuration), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			_isCastingSpell2 = false;
	}

	private async void RunThrust()
	{
		if (!_stats.TrySpendStamina(RunThrustStaminaCost))
			return;

		_attacking = true;
		_isRunThrusting = true;
		_canRunThrust = false;
		// The lunge uses the leaning, legs-tucked dash pose ("air_thrust") both in the air and on
		// the ground — it reads as a lunge either way; sheets without it fall back below.
		if (_sprite.SpriteFrames.HasAnimation("air_thrust"))
			_currentAttackAnimation = "air_thrust";
		else
			_currentAttackAnimation = _sprite.SpriteFrames.HasAnimation("run_thrust") ? "run_thrust" : "attack3";
		_runThrustDirection = _facingRight ? 1f : -1f;

		bool hitLanded = false;
		void OnHitLanded() => hitLanded = true;
		_hitbox.HitDealt += OnHitLanded;

		RunThrustEffects(RunThrustDuration);

		if (RunThrustHitboxDelay > 0f)
			await ToSignal(GetTree().CreateTimer(RunThrustHitboxDelay), SceneTreeTimer.SignalName.Timeout);

		_hitboxShape.Size = RunThrustHitboxSize;
		_hitbox.Position = new Vector2(_runThrustDirection * (RunThrustHitboxSize.X / 2f), 0);
		_hitbox.Activate(_stats, element: _currentElement);

		// Two separate hitbox pulses (with a brief gap in between) so an enemy still standing
		// in the lunge's path when it re-activates takes a second tick of damage, instead of
		// Area2D's body_entered only ever firing once for a target that never left the shape.
		float remainingThrustTime = Mathf.Max(0f, RunThrustDuration - RunThrustHitboxDelay);
		float firstPulseTime = Mathf.Max(0f, remainingThrustTime - RunThrustSecondHitGap) / 2f;
		float secondPulseTime = Mathf.Max(0f, remainingThrustTime - firstPulseTime - RunThrustSecondHitGap);

		await ToSignal(GetTree().CreateTimer(firstPulseTime), SceneTreeTimer.SignalName.Timeout);
		_hitbox.Deactivate();

		if (RunThrustSecondHitGap > 0f)
			await ToSignal(GetTree().CreateTimer(RunThrustSecondHitGap), SceneTreeTimer.SignalName.Timeout);

		_hitbox.Activate(_stats, element: _currentElement);
		await ToSignal(GetTree().CreateTimer(secondPulseTime), SceneTreeTimer.SignalName.Timeout);
		_hitbox.Deactivate();
		_hitbox.HitDealt -= OnHitLanded;
		if (!hitLanded)
			Sfx.Play(this, "Combat/Sword", "Sword Attack");

		_isRunThrusting = false;
		_attacking = false;
		Velocity = new Vector2(0f, Velocity.Y);

		await ToSignal(GetTree().CreateTimer(RunThrustCooldown), SceneTreeTimer.SignalName.Timeout);
		_canRunThrust = true;
	}

	// The Ctrl lunge's effect: the old Effect 5 piercing shockwave (it wraps the whole body and
	// opens into a wide arrowhead) rendered as a solid additive neon silhouette, so it matches the
	// rest of the neon kit, plus a dense run of afterimages for the lunge itself. The trail's
	// normal material comes back when the clip finishes (see the AnimationFinished hook in _Ready).
	private async void RunThrustEffects(float duration)
	{
		Color tint = ElementGhostColor();
		_weaponTrail.Material = NeonSilhouette.CreateMaterial(tint, RunThrustNeonIntensity);
		_weaponTrail.Rotation = 0f;
		_weaponTrail.Scale = WeaponTrailScale("thrust_05");
		_weaponTrail.Position = new Vector2(_weaponTrailBaseX, _weaponTrailBaseY);
		_weaponTrail.Visible = true;
		_weaponTrail.Stop();
		_weaponTrail.Play("thrust_05");

		float elapsed = 0f;
		while (elapsed < duration && IsInstanceValid(this) && _isRunThrusting)
		{
			SpawnGhost(DashGhostAlpha, DashGhostIntensity, DashGhostLifetime);
			float step = DashGhostInterval * 0.7f;
			await ToSignal(GetTree().CreateTimer(step), SceneTreeTimer.SignalName.Timeout);
			elapsed += step;
		}
	}

	private async void Attack()
	{
		if (!_stats.TrySpendStamina(AttackStaminaCost))
			return;

		_attacking = true;
		// In the air, sheets with their own air combo (air_attack1..3) swing that instead; it
		// reuses the matching ground hit's trail/pulses below via comboKey.
		if (!IsOnFloor() && _sprite.SpriteFrames.HasAnimation("air_attack1"))
		{
			_currentAttackAnimation = $"air_attack{_airComboStep + 1}";
			int airHits = 1;
			while (airHits < EffectiveComboHitCount && _sprite.SpriteFrames.HasAnimation($"air_attack{airHits + 1}"))
				airHits++;
			_airComboStep = (_airComboStep + 1) % airHits;
		}
		else
		{
			_currentAttackAnimation = $"attack{_comboStep + 1}";
			// Opening the combo while moving on the ground swings the running slash instead of
			// the planted attack1; the rest of the combo continues as usual.
			if (_comboStep == 0 && IsOnFloor() && Mathf.Abs(Velocity.X) > Speed * RunAttackSpeedThreshold
				&& _sprite.SpriteFrames.HasAnimation("run_attack"))
				_currentAttackAnimation = "run_attack";
			_comboStep = (_comboStep + 1) % EffectiveComboHitCount;
		}
		string comboKey = _currentAttackAnimation switch
		{
			"run_attack" => "attack1",
			_ when _currentAttackAnimation.StartsWith("air_") => _currentAttackAnimation[4..],
			_ => _currentAttackAnimation,
		};
		if (_currentAttackAnimation == "attack3" && IsOnFloor())
			SpawnDust("pierce_dust");
		_comboResetTimer = 0f;

		bool hitLanded = false;
		void OnHitLanded() => hitLanded = true;
		_hitbox.HitDealt += OnHitLanded;

		_weaponPivot.RotationDegrees = WeaponSwingStartAngle;
		Tween swingTween = GetTree().CreateTween();
		swingTween.TweenProperty(_weaponPivot, "rotation_degrees", WeaponSwingEndAngle, AttackHitboxDelay + AttackDuration);

		// All three combo hits now get their own procedural trail instead of a pre-baked sprite:
		// attack1 traces the actual swing (RunSwordArcTrail), attack2 is its own quick circular
		// spin (RunSwordSpinTrail), attack3 is a forward-piercing thrust (RunSwordThrustTrail) —
		// see each method's comment for why none of them just reuse another one's shape. Started
		// here (not after the await below) so attack1's trail captures the windup portion of the
		// swing too.
		(Vector2[] sheetPath, Vector2 sheetTiming) = _currentAttackAnimation switch
		{
			"air_attack1" => (AirAttack1TrailPath, AirAttack1TrailTiming),
			"air_attack2" => (AirAttack2TrailPath, AirAttack2TrailTiming),
			"air_attack3" => (AirAttack3TrailPath, AirAttack3TrailTiming),
			"attack3" => (Attack3TrailPath, Attack3TrailTiming),
			"attack4" => (Attack4TrailPath, Attack4TrailTiming),
			"run_attack" => (RunAttackTrailPath, RunAttackTrailTiming),
			_ => (null, Vector2.Zero),
		};
		bool useSheetPath = sheetPath is { Length: >= 2 } && _sprite.SpriteFrames.HasAnimation(_currentAttackAnimation);
		bool useProceduralArcTrail = useSheetPath || comboKey is "attack1" or "attack2" or "attack3";
		if (useSheetPath)
			RunPathNeonTrail(_currentAttackAnimation, sheetPath, sheetTiming.Y, sheetTiming.X, 1.5f, 16);
		else if (comboKey == "attack1")
			RunSwordArcTrail(AttackHitboxDelay + AttackDuration);
		else if (comboKey == "attack2")
			RunSwordSpinTrail();
		else if (comboKey == "attack3")
			RunSwordThrustTrail();

		if (AttackHitboxDelay > 0f)
			await ToSignal(GetTree().CreateTimer(AttackHitboxDelay), SceneTreeTimer.SignalName.Timeout);

		_hitboxShape.Size = _hitboxBaseSize;
		_hitbox.Position = new Vector2(_facingRight ? AttackHitboxReach : -AttackHitboxReach, AttackHitboxYOffset);
		if (_currentAttackAnimation == "attack3")
			RunThrustVibration();

		if (!useProceduralArcTrail)
		{
			string comboTrail = WeaponTrailAnimation(comboKey);
			_weaponTrail.Scale = WeaponTrailScale(comboTrail);
			_weaponTrail.Position = new Vector2(_weaponTrailBaseX, _weaponTrailBaseY);
			_weaponTrail.Visible = true;
			_weaponTrail.Play(comboTrail);
		}

		int comboPulseCount = comboKey switch
		{
			"attack2" => 2,
			"attack3" => 3,
			"attack4" => 4,
			_ => 1,
		};

		if (comboPulseCount > 1)
		{
			await ComboMultiHit(comboPulseCount);
		}
		else
		{
			_hitbox.Activate(_stats, element: _currentElement, customImpactEffect: SpawnComboImpact);
			await ToSignal(GetTree().CreateTimer(AttackDuration), SceneTreeTimer.SignalName.Timeout);
			_hitbox.Deactivate();
		}

		_hitbox.HitDealt -= OnHitLanded;
		if (!hitLanded)
			Sfx.Play(this, "Combat/Sword", "Sword Attack");

		Tween returnTween = GetTree().CreateTween();
		returnTween.TweenProperty(_weaponPivot, "rotation_degrees", WeaponRestAngle, 0.1f);

		float remainingAnimTime = Mathf.Max(0f, AttackAnimDuration - AttackHitboxDelay - AttackDuration);
		await ToSignal(GetTree().CreateTimer(remainingAnimTime), SceneTreeTimer.SignalName.Timeout);
		_attacking = false;
		_comboResetTimer = ComboResetWindow;
	}

	// Jitters only the sprite along X (alternating forward/back from its rest spot) and always puts
	// it back, so the body, hitbox and trails never move.
	private async void RunThrustVibration()
	{
		if (ThrustVibrationAmplitude <= 0f || ThrustVibrationDuration <= 0f)
			return;

		Vector2 rest = _sprite.Position;
		float elapsed = 0f;
		bool forward = true;
		while (elapsed < ThrustVibrationDuration && IsInstanceValid(this) && _attacking)
		{
			_sprite.Position = rest + new Vector2(forward ? ThrustVibrationAmplitude : -ThrustVibrationAmplitude * 0.5f, 0f);
			forward = !forward;
			float step = Mathf.Max(ThrustVibrationInterval, 0.01f);
			await ToSignal(GetTree().CreateTimer(step), SceneTreeTimer.SignalName.Timeout);
			elapsed += step;
		}
		if (IsInstanceValid(this))
			_sprite.Position = rest;
	}

	// A single swing while hanging on a ladder: the climber stops in place (see the _isClimbing
	// branch), swings in the facing direction, then can climb again once the clip ends.
	private async void LadderAttack()
	{
		if (!_stats.TrySpendStamina(AttackStaminaCost))
			return;

		_attacking = true;
		_isLadderAttacking = true;
		_currentAttackAnimation = "ladder_attack";
		_sprite.Stop();
		_sprite.Play("ladder_attack");
		RunPathNeonTrail("ladder_attack", LadderAttackTrailPath, LadderAttackTrailTiming.Y, LadderAttackTrailTiming.X, 1.5f, 14);

		bool hitLanded = false;
		void OnHitLanded() => hitLanded = true;
		_hitbox.HitDealt += OnHitLanded;

		if (LadderAttackHitDelay > 0f)
			await ToSignal(GetTree().CreateTimer(LadderAttackHitDelay), SceneTreeTimer.SignalName.Timeout);

		_hitboxShape.Size = _hitboxBaseSize;
		_hitbox.Position = new Vector2(_facingRight ? AttackHitboxReach : -AttackHitboxReach, AttackHitboxYOffset);
		_hitbox.Activate(_stats, element: _currentElement);
		await ToSignal(GetTree().CreateTimer(AttackDuration), SceneTreeTimer.SignalName.Timeout);
		_hitbox.Deactivate();
		_hitbox.HitDealt -= OnHitLanded;
		if (!hitLanded)
			Sfx.Play(this, "Combat/Sword", "Sword Attack");

		float clipLength = _sprite.SpriteFrames.GetFrameCount("ladder_attack") / (float)_sprite.SpriteFrames.GetAnimationSpeed("ladder_attack");
		float remaining = Mathf.Max(0f, clipLength - LadderAttackHitDelay - AttackDuration);
		await ToSignal(GetTree().CreateTimer(remaining), SceneTreeTimer.SignalName.Timeout);
		_isLadderAttacking = false;
		_attacking = false;
	}

	private async void CrouchAttack() => await RunSingleAttack("crouch_attack", "slash_vertical", CrouchWeaponTrailYOffset);

	// Returns whether the attack actually landed/spent stamina — callers use this to skip their
	// own cooldown when it didn't (matches how every other stamina-gated move, e.g. UpAttack,
	// only locks out after actually committing to the attack).
	private async Task<bool> RunSingleAttack(string animationName, string trailAnimation, float trailYOffset = 0f)
	{
		if (!_stats.TrySpendStamina(AttackStaminaCost))
			return false;

		_attacking = true;
		_currentAttackAnimation = animationName;

		bool hitLanded = false;
		void OnHitLanded() => hitLanded = true;
		_hitbox.HitDealt += OnHitLanded;

		if (AttackHitboxDelay > 0f)
			await ToSignal(GetTree().CreateTimer(AttackHitboxDelay), SceneTreeTimer.SignalName.Timeout);

		_hitboxShape.Size = _hitboxBaseSize;
		_hitbox.Position = new Vector2(_facingRight ? AttackHitboxReach : -AttackHitboxReach, 0);
		_hitbox.Activate(_stats, element: _currentElement);

		if (animationName == "crouch_attack")
		{
			if (CrouchTrailPath.Length >= 2 && _sprite.SpriteFrames.HasAnimation("crouch_attack"))
				RunPathNeonTrail("crouch_attack", CrouchTrailPath, CrouchTrailTiming.Y, CrouchTrailTiming.X, 1.3f, 12);
			else
				RunCrouchArcTrail();
		}
		else
		{
			_weaponTrail.Scale = new Vector2(2.904f, 2.904f);
			_weaponTrail.Position = new Vector2(_weaponTrailBaseX, _weaponTrailBaseY + trailYOffset);
			_weaponTrail.Visible = true;
			_weaponTrail.Play(trailAnimation);
		}

		await ToSignal(GetTree().CreateTimer(AttackDuration), SceneTreeTimer.SignalName.Timeout);
		_hitbox.Deactivate();
		_hitbox.HitDealt -= OnHitLanded;
		if (!hitLanded)
			Sfx.Play(this, "Combat/Sword", "Sword Attack");

		float remainingAnimTime = Mathf.Max(0f, AttackAnimDuration - AttackHitboxDelay - AttackDuration);
		await ToSignal(GetTree().CreateTimer(remainingAnimTime), SceneTreeTimer.SignalName.Timeout);
		_attacking = false;
		return true;
	}

	// The charged attack's neon trail: draws a smooth curve through ChargedTrailPath (the blade
	// tip measured per frame of the "charged_attack" sheet), revealed over ChargedTrailDrawTime
	// so it rides along with the sprite's own sweep instead of a canned smear. Reuses the
	// SwordArc lines/sparks (same additive neon look as attack1), just thicker.
	private void RunChargedNeonTrail() =>
		RunPathNeonTrail("charged_attack", ChargedTrailPath, ChargedTrailDrawTime, 0f, 2.2f, 24);

	// Shared by the charged attack and the air combo: path is in the given clip's own frame-pixel
	// space, revealed over drawTime after an optional delay (to line up with the frame where the
	// sheet's swing actually happens).
	private async void RunPathNeonTrail(string animation, Vector2[] path, float drawTime, float delay, float widthMul, int sparkAmount)
	{
		if (path is null || path.Length < 2 || !_sprite.SpriteFrames.HasAnimation(animation))
			return;

		int runId = ++_swordArcRunId;
		if (delay > 0f)
		{
			await ToSignal(GetTree().CreateTimer(delay), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || runId != _swordArcRunId)
				return;
		}
		Color tint = ElementTint();

		// Sheet pixel -> Visual-local (the lines live under Visual, so facing flips for free).
		Vector2 frameSize = _sprite.SpriteFrames.GetFrameTexture(animation, 0).GetSize();
		Vector2 ToVisual(Vector2 px) => _sprite.Position + (px - frameSize / 2f + _sprite.Offset) * _sprite.Scale - _swordArcCore.Position;

		const int samplesPerSegment = 8;
		var curve = new List<Vector2>();
		for (int i = 0; i < path.Length - 1; i++)
		{
			Vector2 pre = ToVisual(path[Mathf.Max(i - 1, 0)]);
			Vector2 a = ToVisual(path[i]);
			Vector2 b = ToVisual(path[i + 1]);
			Vector2 post = ToVisual(path[Mathf.Min(i + 2, path.Length - 1)]);
			for (int s = 0; s < samplesPerSegment; s++)
				curve.Add(a.CubicInterpolate(b, pre, post, s / (float)samplesPerSegment));
		}
		curve.Add(ToVisual(path[^1]));

		_swordArcPoints.Clear();
		_swordArcCore.Points = System.Array.Empty<Vector2>();
		_swordArcGlow.Points = System.Array.Empty<Vector2>();
		_swordArcCore.Modulate = tint;
		_swordArcGlow.Modulate = tint;
		_swordArcCore.Width = _swordArcBaseCoreWidth * widthMul;
		_swordArcGlow.Width = _swordArcBaseGlowWidth * widthMul;
		PrepareTrailSparks(sparkAmount, tint);
		_swordArcSparks.Emitting = true;

		float elapsed = 0f;
		while (true)
		{
			float progress = Mathf.Clamp(elapsed / Mathf.Max(drawTime, 0.01f), 0f, 1f);
			int count = Mathf.Max(2, Mathf.CeilToInt(curve.Count * progress));
			Vector2[] shown = curve.GetRange(0, count).ToArray();
			_swordArcCore.Points = shown;
			_swordArcGlow.Points = shown;
			_swordArcSparks.Position = shown[^1];
			if (progress >= 1f)
				break;

			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			if (!IsInstanceValid(this) || runId != _swordArcRunId)
				return;
			elapsed += (float)GetPhysicsProcessDeltaTime();
		}

		_swordArcSparks.Emitting = false;
		await ToSignal(GetTree().CreateTimer(0.06f), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || runId != _swordArcRunId)
			return;
		Tween fadeTween = CreateTween();
		fadeTween.TweenProperty(_swordArcCore, "modulate:a", 0f, 0.18f);
		fadeTween.Parallel().TweenProperty(_swordArcGlow, "modulate:a", 0f, 0.18f);
	}

	// attack1's own procedural trail: instead of WeaponTrail's pre-baked smear sprite, this
	// literally traces the blade tip's position while _weaponPivot swings, so the streak always
	// matches the real swing's speed/arc instead of a canned animation playing at its own fixed
	// pace. _swordArcRunId guards the fade-out below against a second swing starting before it
	// finishes (shouldn't happen given _attacking's own gating, but it's a cheap guard).
	private async void RunSwordArcTrail(float duration)
	{
		int runId = ++_swordArcRunId;
		Color tint = ElementTint();

		_swordArcPoints.Clear();
		_swordArcCore.Points = System.Array.Empty<Vector2>();
		_swordArcGlow.Points = System.Array.Empty<Vector2>();
		_swordArcCore.Modulate = tint;
		_swordArcGlow.Modulate = tint;
		_swordArcCore.Width = _swordArcBaseCoreWidth * 1.5f;
		_swordArcGlow.Width = _swordArcBaseGlowWidth * 1.5f;
		PrepareTrailSparks(14, tint);
		_swordArcSparks.Emitting = true;

		float elapsed = 0f;
		while (elapsed < duration)
		{
			Vector2 tip = _weaponPivot.Position + Vector2.Right.Rotated(-_weaponPivot.Rotation) * SwordArcTrailRadius;
			// attack2's combo hit-stop (see ComboMultiHit) briefly drops Engine.TimeScale near 0
			// without pausing this loop's own frame ticks — without this check, the barely-moving
			// tip during that freeze piles up a knot of near-duplicate points, reading as a kink/
			// cut in the line once time resumes. Skipping points that haven't actually moved keeps
			// the line paused-but-smooth through the freeze instead.
			if (_swordArcPoints.Count == 0 || _swordArcPoints[^1].DistanceSquaredTo(tip) > MinSwordArcPointSpacing * MinSwordArcPointSpacing)
			{
				_swordArcPoints.Add(tip);
				_swordArcCore.Points = _swordArcPoints.ToArray();
				_swordArcGlow.Points = _swordArcPoints.ToArray();
			}
			_swordArcSparks.Position = tip;

			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			if (!IsInstanceValid(this) || runId != _swordArcRunId)
				return;
			elapsed += (float)GetPhysicsProcessDeltaTime();
		}

		_swordArcSparks.Emitting = false;
		Tween fadeTween = CreateTween();
		fadeTween.TweenProperty(_swordArcCore, "modulate:a", 0f, 0.08f);
		fadeTween.Parallel().TweenProperty(_swordArcGlow, "modulate:a", 0f, 0.08f);
	}

	// attack2's own thing: its old bought trail ("Effect 1", see WeaponTrailSpriteFrames.tres's
	// slash_horizontal_09) was a near-full circular spin, not a scaled copy of attack1's straight
	// diagonal swipe ("Effect 7") — so this traces an independent sweep around the weapon hand
	// instead of reusing RunSwordArcTrail's _weaponPivot-following logic. The crouch attack reuses
	// this same crescent-sweep idea (see RunCrouchArcTrail) with its own tighter numbers rather
	// than duplicating the loop.
	private async void RunSwordSpinTrail()
	{
		Color tint = ElementTint();
		await RunSwordCrescentTrail(SwordSpinRadius, SwordSpinDegrees, SwordSpinDuration, tint, widthMul: 1.5f, sparkAmount: 20);
	}

	// The crouch attack's own trail: unlike attack2's invented circle (RunSwordCrescentTrail),
	// this actually swings _weaponPivot through a shallow angle range and traces THAT real
	// rotation — same idea as RunSwordArcTrail (attack1), just its own smaller/lower arc and
	// centered CrouchWeaponTrailYOffset lower to match the crouched stance. RunSingleAttack
	// (crouch_attack's only caller) never otherwise touches _weaponPivot, so this owns driving
	// the swing tween itself, including restoring it to WeaponRestAngle afterward.
	private async void RunCrouchArcTrail()
	{
		Color tint = ElementTint();

		_weaponPivot.RotationDegrees = CrouchSwingStartAngle;
		Tween swingTween = GetTree().CreateTween();
		swingTween.TweenProperty(_weaponPivot, "rotation_degrees", CrouchSwingEndAngle, CrouchArcDuration);

		int runId = ++_swordArcRunId;
		_swordArcPoints.Clear();
		_swordArcCore.Points = System.Array.Empty<Vector2>();
		_swordArcGlow.Points = System.Array.Empty<Vector2>();
		_swordArcCore.Modulate = tint;
		_swordArcGlow.Modulate = tint;
		_swordArcCore.Width = _swordArcBaseCoreWidth;
		_swordArcGlow.Width = _swordArcBaseGlowWidth;
		PrepareTrailSparks(12, tint);
		_swordArcSparks.Emitting = true;

		Vector2 center = _weaponPivot.Position + new Vector2(0f, CrouchWeaponTrailYOffset);

		float elapsed = 0f;
		while (elapsed < CrouchArcDuration)
		{
			Vector2 tip = center + Vector2.Right.Rotated(-_weaponPivot.Rotation) * CrouchArcRadius;
			if (_swordArcPoints.Count == 0 || _swordArcPoints[^1].DistanceSquaredTo(tip) > MinSwordArcPointSpacing * MinSwordArcPointSpacing)
			{
				_swordArcPoints.Add(tip);
				_swordArcCore.Points = _swordArcPoints.ToArray();
				_swordArcGlow.Points = _swordArcPoints.ToArray();
			}
			_swordArcSparks.Position = tip;

			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			if (!IsInstanceValid(this) || runId != _swordArcRunId)
				return;
			elapsed += (float)GetPhysicsProcessDeltaTime();
		}

		_swordArcSparks.Emitting = false;
		Tween fadeTween = CreateTween();
		fadeTween.TweenProperty(_swordArcCore, "modulate:a", 0f, 0.08f);
		fadeTween.Parallel().TweenProperty(_swordArcGlow, "modulate:a", 0f, 0.08f);

		Tween restoreTween = GetTree().CreateTween();
		restoreTween.TweenProperty(_weaponPivot, "rotation_degrees", WeaponRestAngle, 0.1f);
	}

	// Shared crescent-sweep tracer: builds an arc of `sweepDegrees` around WeaponPivot's origin
	// over `duration`, with the unswept gap centered behind the character (180°, since 0°=front)
	// so any sweep amount reads as a "C" bulging forward rather than a bowl open at the top. Used
	// by both RunSwordSpinTrail (attack2) and RunCrouchArcTrail with different numbers instead of
	// each duplicating this loop.
	private async Task RunSwordCrescentTrail(float radius, float sweepDegrees, float duration, Color tint, float widthMul, int sparkAmount)
	{
		int runId = ++_swordArcRunId;

		_swordArcPoints.Clear();
		_swordArcCore.Points = System.Array.Empty<Vector2>();
		_swordArcGlow.Points = System.Array.Empty<Vector2>();
		_swordArcCore.Modulate = tint;
		_swordArcGlow.Modulate = tint;
		_swordArcCore.Width = _swordArcBaseCoreWidth * widthMul;
		_swordArcGlow.Width = _swordArcBaseGlowWidth * widthMul;
		PrepareTrailSparks(sparkAmount, tint);
		_swordArcSparks.Emitting = true;

		// No _facingRight branch needed: Visual's own scale flip already mirrors these
		// Visual-local points for facing left (same as RunSwordArcTrail), and a crescent traced
		// either way still reads as a crescent once mirrored.
		Vector2 center = _weaponPivot.Position;
		float startAngleDeg = 180f - (sweepDegrees + 360f) / 2f;

		float elapsed = 0f;
		while (elapsed < duration)
		{
			float t = Mathf.Clamp(elapsed / duration, 0f, 1f);
			float angleRad = Mathf.DegToRad(startAngleDeg + sweepDegrees * t);
			Vector2 tip = center + Vector2.Right.Rotated(angleRad) * radius;
			if (_swordArcPoints.Count == 0 || _swordArcPoints[^1].DistanceSquaredTo(tip) > MinSwordArcPointSpacing * MinSwordArcPointSpacing)
			{
				_swordArcPoints.Add(tip);
				_swordArcCore.Points = _swordArcPoints.ToArray();
				_swordArcGlow.Points = _swordArcPoints.ToArray();
			}
			_swordArcSparks.Position = tip;

			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			if (!IsInstanceValid(this) || runId != _swordArcRunId)
				return;
			elapsed += (float)GetPhysicsProcessDeltaTime();
		}

		_swordArcSparks.Emitting = false;
		Tween fadeTween = CreateTween();
		fadeTween.TweenProperty(_swordArcCore, "modulate:a", 0f, 0.08f);
		fadeTween.Parallel().TweenProperty(_swordArcGlow, "modulate:a", 0f, 0.08f);
	}

	// attack3 (the finisher, "la estocada"): a straight piercing beam that grows forward from the
	// blade instead of an arc or a spin — it's a lunge, not a swing, so the trail should shoot
	// out and hold rather than sweep. Same taper (thin at the hilt, thick at the tip) as the
	// other two comes for free from the WidthCurve set up in _Ready.
	private async void RunSwordThrustTrail()
	{
		int runId = ++_swordArcRunId;
		Color tint = ElementTint();

		_swordArcCore.Modulate = tint;
		_swordArcGlow.Modulate = tint;
		_swordArcSparks.Modulate = tint;
		_swordArcCore.Width = _swordArcBaseCoreWidth * 1.4f;
		_swordArcGlow.Width = _swordArcBaseGlowWidth * 1.4f;

		Vector2 basePoint = _weaponPivot.Position;

		float elapsed = 0f;
		while (elapsed < SwordThrustExtendDuration)
		{
			float t = Mathf.Clamp(elapsed / SwordThrustExtendDuration, 0f, 1f);
			Vector2 tip = basePoint + Vector2.Right * SwordThrustReach * t;
			Vector2[] points = { basePoint, tip };
			_swordArcCore.Points = points;
			_swordArcGlow.Points = points;
			_swordArcSparks.Position = tip;

			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			if (!IsInstanceValid(this) || runId != _swordArcRunId)
				return;
			elapsed += (float)GetPhysicsProcessDeltaTime();
		}

		PrepareTrailSparks(18, tint);
		_swordArcSparks.Emitting = true;

		await ToSignal(GetTree().CreateTimer(SwordThrustHoldDuration), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || runId != _swordArcRunId)
			return;

		_swordArcSparks.Emitting = false;
		Tween fadeTween = CreateTween();
		fadeTween.TweenProperty(_swordArcCore, "modulate:a", 0f, 0.1f);
		fadeTween.Parallel().TweenProperty(_swordArcGlow, "modulate:a", 0f, 0.1f);
	}

	private string WeaponTrailAnimation(string attackAnimation) => attackAnimation switch
	{
		"attack2" => _isFireImbued ? "slash_horizontal_09_fire" : "slash_horizontal_09",
		"attack3" => _isFireImbued ? "thrust_01_fire" : "thrust_01",
		_ => _isFireImbued ? "slash_horizontal_fire" : "slash_horizontal",
	};

	// The combo's three hits now use much bigger source canvases (Effect Pack #7) than every
	// other move sharing this same AnimatedSprite2D node (crouch/dash-thrust/run-thrust still
	// use the original tiny 48x48 smear frames), so the node's scale has to switch per-animation
	// instead of staying at one fixed value.
	private static Vector2 WeaponTrailScale(string trailAnimation) => trailAnimation switch
	{
		"slash_horizontal" or "slash_horizontal_fire" => new Vector2(0.84f, 0.84f),
		"slash_horizontal_09" or "slash_horizontal_09_fire" => new Vector2(0.756f, 0.756f),
		"thrust_01" or "thrust_01_fire" => new Vector2(1.1025f, 1.1025f),
		"thrust_05" or "thrust_05_fire" => new Vector2(1.155f, 1.155f),
		"thrust_02" or "thrust_02_fire" => new Vector2(0.82f, 0.82f),
		_ => new Vector2(2.904f, 2.904f),
	};

	// Shared by attack2 (2 pulses) and attack3 (3 pulses, the finisher): the hitbox re-activates
	// once per pulse, each landing its own hit-stop freeze, reading as a "thunk-thunk[-thunk]"
	// flurry instead of a single hit.
	private async Task ComboMultiHit(int pulseCount)
	{
		try
		{
			for (int i = 0; i < pulseCount; i++)
			{
				bool pulseHitLanded = false;
				void OnPulseHit() => pulseHitLanded = true;
				_hitbox.HitDealt += OnPulseHit;

				_hitbox.Activate(_stats, element: _currentElement, ignoreTargetInvulnerability: true, customImpactEffect: SpawnComboImpact);

				// Give the physics step a moment at normal speed to actually detect the overlap
				// and fire HitDealt before we freeze time out from under it.
				await ToSignal(GetTree().CreateTimer(HitStopDetectWindow), SceneTreeTimer.SignalName.Timeout);
				_hitbox.HitDealt -= OnPulseHit;
				if (!IsInstanceValid(this))
					return;

				// Whiffing shouldn't slow anything down — only a pulse that actually connected
				// earns its freeze.
				if (pulseHitLanded)
				{
					Engine.TimeScale = HitStopFreezeScale;
					await ToSignal(GetTree().CreateTimer(HitStopFreezeDuration, true, false, true), SceneTreeTimer.SignalName.Timeout);
					Engine.TimeScale = 1.0;
					if (!IsInstanceValid(this))
						return;
				}

				_hitbox.Deactivate();

				bool isLastPulse = i == pulseCount - 1;
				if (!isLastPulse)
				{
					await ToSignal(GetTree().CreateTimer(HitStopGapDuration), SceneTreeTimer.SignalName.Timeout);
					if (!IsInstanceValid(this))
						return;
				}
			}
		}
		finally
		{
			Engine.TimeScale = 1.0;
			if (IsInstanceValid(this))
				_hitbox.Deactivate();
		}
	}

	// Homemade replacement for the old bought impact_medium/impact_big sprite sheets: attack1 is
	// a quick light burst, attack2 medium, attack3 (the finisher) the biggest one with its own
	// flash. Passed to Hitbox.Activate as a customImpactEffect so this only changes the player's
	// own combo — every other Hitbox caller (enemies, bosses) is untouched.
	private void SpawnComboImpact(Vector2 point)
	{
		HitImpactBurst.ImpactTier tier = _currentAttackAnimation switch
		{
			"attack2" => HitImpactBurst.ImpactTier.Medium,
			"attack3" => HitImpactBurst.ImpactTier.Heavy,
			_ => HitImpactBurst.ImpactTier.Light,
		};
		HitImpactBurst.SpawnAt(this, point, tier);
	}

	private async void UseHealFlask()
	{
		if (!_healFlask.TryUse(_stats))
			return;

		Sfx.Play(this, "Magic", "Firebuff"); // TODO: swap for a real potion/heal sound once we have one
		_healing = true;
		await ToSignal(GetTree().CreateTimer(HealAnimDuration), SceneTreeTimer.SignalName.Timeout);
		_healing = false;
	}

	private async void CycleElement()
	{
		if (_isTransforming)
			return;

		DamageElement previous = _currentElement;
		_currentElement = previous switch
		{
			DamageElement.Normal => DamageElement.Fire,
			DamageElement.Fire => DamageElement.Poison,
			DamageElement.Poison => DamageElement.Ice,
			DamageElement.Ice => DamageElement.Lightning,
			_ => DamageElement.Normal,
		};
		SaveManager.Instance.SessionElement = _currentElement;

		// Every switch plays the transformation; the new look lands when it ends.
		if (_currentElement == DamageElement.Fire)
		{
			_torchEquipped = false;
			SaveManager.Instance.SessionTorchEquipped = false;
		}
		_isTransforming = true;
		float duration = _currentElement == DamageElement.Normal ? TransformationOutDuration : TransformationInDuration;
		await ToSignal(GetTree().CreateTimer(duration), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this))
			return;
		ApplyElementLook(_currentElement);
		// Sheets that swap SpriteFrames (FireSpriteFrames fallback) can leave the node on a clip the
		// new set lacks — reset to a name every set has.
		_sprite.Play("idle");
		_isFireImbued = _currentElement == DamageElement.Fire;
		_isTransforming = false;
	}

	private void ApplyElementLook(DamageElement element)
	{
		if (FireMaterial != null)
		{
			_sprite.Material = element switch
			{
				DamageElement.Fire => FireMaterial,
				DamageElement.Poison => _poisonMaterial ?? _normalMaterial,
				DamageElement.Ice => _iceMaterial ?? _normalMaterial,
				DamageElement.Lightning => _lightningMaterial ?? _normalMaterial,
				_ => _normalMaterial,
			};
		}
		else if (FireSpriteFrames != null)
		{
			_sprite.SpriteFrames = element == DamageElement.Fire ? FireSpriteFrames : _normalSpriteFrames;
		}
	}

	// A copy of FireMaterial (same palette + flicker shader) with its blade colours — the last three
	// target colours — swapped for `blade`. Null when FireMaterial isn't a palette ShaderMaterial.
	private ShaderMaterial BuildBladeVariant(Color[] blade, float flicker)
	{
		if (FireMaterial is not ShaderMaterial fire || blade is null || blade.Length < 3)
			return null;
		var material = (ShaderMaterial)fire.Duplicate();
		Color[] targets = material.GetShaderParameter("target_colors").AsColorArray();
		if (targets.Length < 3)
			return null;
		for (int i = 0; i < 3; i++)
			targets[targets.Length - 3 + i] = blade[i];
		material.SetShaderParameter("target_colors", targets);
		material.SetShaderParameter("flicker_strength", flicker);
		return material;
	}

	// Per-element behaviour of the shared trail sparks (SwordArcSparks): fire = more embers drifting
	// up, ice = more fine frost slowly falling, lightning = fast short crackle (plus the bolts spawned
	// in UpdateLightningBolts); normal/poison keep the scene's original sparks.
	private void BuildElementSparkProcesses()
	{
		_baseSparkProcess = _swordArcSparks.ProcessMaterial as ParticleProcessMaterial;
		_baseSparkLifetime = (float)_swordArcSparks.Lifetime;
		if (_baseSparkProcess is null)
			return;

		_fireSparkProcess = (ParticleProcessMaterial)_baseSparkProcess.Duplicate();
		_fireSparkProcess.Gravity = new Vector3(0f, -150f, 0f);
		_fireSparkProcess.InitialVelocityMin = 30f;
		_fireSparkProcess.InitialVelocityMax = 130f;
		_fireSparkProcess.DampingMin = 10f;
		_fireSparkProcess.DampingMax = 30f;
		_fireSparkProcess.ScaleMin = 0.18f;
		_fireSparkProcess.ScaleMax = 0.5f;

		_iceSparkProcess = (ParticleProcessMaterial)_baseSparkProcess.Duplicate();
		_iceSparkProcess.Gravity = new Vector3(0f, 70f, 0f);
		_iceSparkProcess.InitialVelocityMin = 20f;
		_iceSparkProcess.InitialVelocityMax = 90f;
		_iceSparkProcess.DampingMin = 30f;
		_iceSparkProcess.DampingMax = 60f;
		_iceSparkProcess.ScaleMin = 0.08f;
		_iceSparkProcess.ScaleMax = 0.26f;

		_lightningSparkProcess = (ParticleProcessMaterial)_baseSparkProcess.Duplicate();
		_lightningSparkProcess.Gravity = Vector3.Zero;
		_lightningSparkProcess.InitialVelocityMin = 160f;
		_lightningSparkProcess.InitialVelocityMax = 340f;
		_lightningSparkProcess.ScaleMin = 0.1f;
		_lightningSparkProcess.ScaleMax = 0.3f;
	}

	private void PrepareTrailSparks(int baseAmount, Color tint)
	{
		_swordArcSparks.Modulate = tint;
		(ParticleProcessMaterial process, float lifetime, float multiplier) = _currentElement switch
		{
			DamageElement.Fire => (_fireSparkProcess, 0.6f, ElementSparkMultiplier),
			DamageElement.Ice => (_iceSparkProcess, 0.75f, ElementSparkMultiplier),
			DamageElement.Lightning => (_lightningSparkProcess, 0.18f, ElementSparkMultiplier),
			_ => (_baseSparkProcess, _baseSparkLifetime, 1f),
		};
		if (process is not null)
		{
			_swordArcSparks.ProcessMaterial = process;
			_swordArcSparks.Lifetime = lifetime;
		}
		_swordArcSparks.Amount = Mathf.Max(1, Mathf.RoundToInt(baseAmount * multiplier));
	}

	// Lightning imbue: while any trail's sparks are running, little jagged bolts crackle off the
	// spark point every LightningBoltInterval — short-lived additive zigzags in the trail's tint.
	private void UpdateLightningBolts(float delta)
	{
		if (_currentElement != DamageElement.Lightning || !_swordArcSparks.Emitting)
			return;
		_lightningBoltTimer -= delta;
		if (_lightningBoltTimer > 0f)
			return;
		_lightningBoltTimer = LightningBoltInterval;
		SpawnLightningBolt(_swordArcSparks.Position);
	}

	private void SpawnLightningBolt(Vector2 at)
	{
		var rng = new RandomNumberGenerator();
		rng.Randomize();
		Vector2 dir = Vector2.Right.Rotated(rng.RandfRange(0f, Mathf.Tau));
		Vector2 normal = new(-dir.Y, dir.X);
		float length = rng.RandfRange(10f, 24f);
		const int segments = 5;
		var points = new Vector2[segments + 1];
		for (int i = 0; i <= segments; i++)
		{
			float t = (float)i / segments;
			float jitter = i == 0 || i == segments ? 0f : rng.RandfRange(-4f, 4f);
			points[i] = at + dir * length * t + normal * jitter;
		}

		var bolt = new Line2D
		{
			Points = points,
			Width = rng.RandfRange(1.2f, 2.2f),
			DefaultColor = LightningElementTint.Lightened(0.35f),
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			JointMode = Line2D.LineJointMode.Sharp,
		};
		_visual.AddChild(bolt);
		Tween fade = bolt.CreateTween();
		fade.TweenProperty(bolt, "modulate:a", 0f, 0.1f).SetDelay(0.03f);
		fade.TweenCallback(Callable.From(bolt.QueueFree));
	}

	private Color ElementTint() => _currentElement switch
	{
		DamageElement.Fire => new Color(1f, 0.55f, 0.2f, 1f),
		DamageElement.Poison => PoisonElementTint,
		DamageElement.Ice => IceElementTint,
		DamageElement.Lightning => LightningElementTint,
		_ => new Color(0.15f, 0.65f, 1f, 1f),
	};

	private Color ElementGhostColor() => _currentElement switch
	{
		DamageElement.Fire => DashGhostFireColor,
		DamageElement.Poison => PoisonElementTint,
		DamageElement.Ice => IceElementTint,
		DamageElement.Lightning => LightningElementTint,
		_ => DashGhostColor,
	};

	private void OnDied()
	{
		// Killed while the scene is being swapped out (hazard, fall, enemy hit during the fade):
		// ignore it — this whole scene is about to be freed, and the death screen would otherwise
		// show over the new map and call RespawnPlayer on a level that no longer exists.
		if (SceneFader.IsChangingScene)
			return;

		// Dying mid-Pound skips the rest of _PhysicsProcess (see the _isDead guard at its top),
		// which otherwise reaches EndPound() itself — without this, the 90° sprite/trail
		// rotation and the active hitbox would stay stuck through the death animation.
		_crystalEffect.Visible = false;
		EndPound();

		_isDead = true;
		_sprite.Play("death");
		// Only this player's own level may drive the respawn — never a scene that already replaced it.
		if (GetTree().CurrentScene is LevelBootstrap level && level.IsAncestorOf(this))
			DeathScreen.Instance.Show(level.RespawnPlayer);
	}
}
