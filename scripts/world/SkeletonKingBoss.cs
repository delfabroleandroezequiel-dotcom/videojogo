using Godot;

namespace Metroidvania.World;

// Boss.cs's wander/lunge/retreat/melee chassis is disabled entirely by data on the scene
// (MoveSpeed=0, AttackRange=0, LungeChance=0, RetreatChance=0) — same "stationary needs no code"
// trick as SpiderBossArena, since this king never actually leaves his throne. The pack's own art
// only ships an emerge-from-ground intro and one long idle loop (no walk/attack/hurt/death frames
// exist at all), so every attack here is a telegraphed lightning strike using ThunderEffectPack's
// Effect 10 bolt — just at different targets/patterns: a single strike on the player (3-phase
// escalation, ratio-based), a map-wide "rain" that leaves a few safe gaps to stand in, and an
// "accordion" sweep that marches from one side of the map to the other.
public partial class SkeletonKingBoss : Boss
{
	[Export] public string LightningFramesPath = "res://resources/sprites/ThunderStrikeSpriteFrames.tres";
	[Export] public Vector2 LightningVfxOffset = new(0f, -185f);
	[Export] public float LightningVfxScale = 1f;
	[Export] public string MarkerFramesPath = "res://resources/sprites/ThunderMarkerSpriteFrames.tres";

	// Now that the map itself runs dark (night CanvasModulate), every bolt needs to actually throw
	// light rather than just draw a bright sprite over a dark background — same PointLightGradient
	// texture ElementGlow/Torch use elsewhere, just tuned per-effect here instead of a shared helper
	// since the strike flash and the marker's glow want very different energy/scale/lifetimes.
	[Export] public string LightTexturePath = "res://resources/lighting/PointLightGradient.tres";
	[Export] public Color LightningLightColor = new(0.75f, 0.85f, 1f, 1f);
	[Export] public float LightningFlashEnergy = 2.2f;
	[Export] public float LightningFlashScale = 4f;
	[Export] public float LightningFlashDuration = 0.25f;
	[Export] public Color MarkerLightColor = new(1f, 0.9f, 0.55f, 1f);
	[Export] public float MarkerLightEnergy = 0.9f;
	[Export] public float MarkerLightScale = 1.5f;

	// The map's walkable floor, in world coordinates — the rain and accordion attacks spread their
	// strikes across this range rather than around the player, so they need it explicitly instead
	// of inferring it from collision shapes. Defaults match CaminoDesierto's floor.
	[Export] public float MapFloorY = 268f;
	[Export] public float MapMinX = 0f;
	[Export] public float MapMaxX = 1774f;

	[Export] public float LightningStrikeCooldown = 4.5f;
	[Export] public float LightningTelegraphDuration = 0.9f;
	[Export] public float LightningActiveDuration = 0.3f;
	[Export] public int LightningDamage = 25;
	[Export] public float LightningRadius = 60f;
	// Index 0/1/2 = phase 1/2/3's cooldown multiplier — same escalation idea as SpiderBoss's
	// PhaseWebSpitCooldownMultiplier, just for this king's single-target strike.
	[Export] public float[] PhaseLightningCooldownMultiplier = { 1f, 0.65f, 0.4f };

	// A map-wide volley: RainStrikeCount evenly spaced points across the floor all telegraph at
	// once, then all strike together except RainSafeGapCount of them (picked fresh each cast) —
	// those stay as the "eggs" the player has to be standing near when the telegraph runs out.
	// Uses the thinner Effect 9 bolt (not Effect 10) since this many at once reads better thin.
	[Export] public string RainFramesPath = "res://resources/sprites/ThunderRainStrikeSpriteFrames.tres";
	[Export] public Vector2 RainVfxOffset = new(0f, -563.2f);
	[Export] public float RainVfxScale = 2.2f;
	[Export] public float RainCooldown = 9f;
	[Export] public int RainStrikeCount = 14;
	[Export] public int RainSafeGapCount = 1;
	[Export] public float RainTelegraphDuration = 1.1f;
	[Export] public float RainActiveDuration = 0.3f;
	[Export] public int RainDamage = 20;
	// Effect 9 is a thin beam, not a blob — a CircleShape2D hazard wide enough to look right against
	// the old radius (55) was hitting well outside where the sprite actually is. This rectangle is
	// sized to the beam's own scaled width (32 * RainVfxScale) instead, so the hazard only covers
	// what's actually drawn.
	[Export] public Vector2 RainHitboxSize = new(70f, 100f);

	// A wave marching from a randomly picked side of the map to the other, AccordionStrikeGap apart.
	// Strikes are packed AccordionSpacing apart (not spread evenly by a fixed count) so they sit
	// touching/overlapping regardless of map width — walking through the wall isn't an option, only
	// a dash (i-frames) gets past it. Each one's own short telegraph/impact overlaps the next
	// starting, which is what actually reads as a sweeping wave instead of N separate strikes.
	[Export] public float AccordionCooldown = 11f;
	[Export] public float AccordionSpacing = 90f;
	[Export] public float AccordionStrikeGap = 0.1f;
	[Export] public float AccordionTelegraphDuration = 0.4f;
	[Export] public float AccordionActiveDuration = 0.25f;
	[Export] public int AccordionDamage = 18;
	[Export] public float AccordionRadius = 60f;

	private float _lightningCooldownTimer;
	private float _rainCooldownTimer;
	private float _accordionCooldownTimer;
	private bool _isCasting;
	private bool _isEmerging;
	private int _currentPhase = 1;
	private readonly RandomNumberGenerator _rng = new();

	// This king's only offense is the lightning strike, so unlike RatEnemy (whose only offense IS
	// contact), a player brushing against his stone body/arms shouldn't also chip damage — without
	// this, his huge wide-armed collision box read as an unexplained "hitting us with his arms".
	protected override bool ContactDamageEnabled => false;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;

		_rng.Randomize();
		_lightningCooldownTimer = LightningStrikeCooldown * 0.5f;
		_rainCooldownTimer = RainCooldown * 0.5f;
		_accordionCooldownTimer = AccordionCooldown * 0.75f;

		if (Sprite is not null && Sprite.SpriteFrames.HasAnimation("emerge"))
		{
			_isEmerging = true;
			Sprite.Play("emerge");
			Sprite.AnimationFinished += OnEmergeFinished;
		}
	}

	private void OnEmergeFinished()
	{
		if (!_isEmerging)
			return;

		_isEmerging = false;
		Sprite.AnimationFinished -= OnEmergeFinished;
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (_isEmerging)
			return;

		base.UpdateAnimation(velocity);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;

		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval || _isEmerging)
			return;

		UpdatePhase();

		if (!PlayerDetected || _isCasting)
			return;

		_rainCooldownTimer -= (float)delta;
		_accordionCooldownTimer -= (float)delta;
		_lightningCooldownTimer -= (float)delta;

		// Priority order: the two map-wide patterns are the "big" moments, so they preempt the
		// single-target strike on the same tick instead of all three ever landing at once.
		if (_rainCooldownTimer <= 0f)
		{
			_rainCooldownTimer = RainCooldown;
			CastRain();
		}
		else if (_accordionCooldownTimer <= 0f)
		{
			_accordionCooldownTimer = AccordionCooldown;
			CastAccordion();
		}
		else if (_lightningCooldownTimer <= 0f)
		{
			_lightningCooldownTimer = LightningStrikeCooldown * CurrentLightningMultiplier;
			CastLightningStrike();
		}
	}

	private void UpdatePhase()
	{
		float ratio = Stats.MaxHealth > 0 ? (float)Stats.CurrentHealth / Stats.MaxHealth : 0f;
		_currentPhase = ratio > 0.66f ? 1 : ratio > 0.33f ? 2 : 3;
	}

	private float CurrentLightningMultiplier => _currentPhase >= 1 && _currentPhase <= PhaseLightningCooldownMultiplier.Length
		? PhaseLightningCooldownMultiplier[_currentPhase - 1]
		: 1f;

	// Shared by all three attacks: marker held for telegraphDuration, then the bolt (framesPath,
	// with its own vfxOffset/vfxScale since Effect 9's tall thin beam and Effect 10's bigger bolt
	// don't share the same anchor) plays and a hazard sits active for activeDuration. hazardShape is
	// built by the caller (a circle for the blobbier Effect 10 impacts, a rectangle matching Effect
	// 9's actual beam width for the rain) so the hitbox always matches what's actually drawn. Doesn't
	// touch _isCasting itself — callers decide whether/how to gate re-entry around one or many of
	// these running concurrently.
	private async void StrikeAt(Vector2 position, float telegraphDuration, float activeDuration, int damage, Shape2D hazardShape,
		string framesPath, Vector2 vfxOffset, float vfxScale)
	{
		var marker = new AnimatedSprite2D
		{
			SpriteFrames = GD.Load<SpriteFrames>(MarkerFramesPath),
			Animation = "marker",
		};
		GetTree().CurrentScene.AddChild(marker);
		marker.GlobalPosition = position;
		marker.Play("marker");

		// Child of the marker (not a separate tracked node) so it's freed automatically the instant
		// marker.QueueFree() runs below — no separate lifetime to manage for a light this short-lived.
		marker.AddChild(new PointLight2D
		{
			Color = MarkerLightColor,
			Energy = MarkerLightEnergy,
			Texture = GD.Load<Texture2D>(LightTexturePath),
			TextureScale = MarkerLightScale,
		});

		await ToSignal(GetTree().CreateTimer(telegraphDuration), SceneTreeTimer.SignalName.Timeout);
		marker.QueueFree();
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		VfxSpawner.SpawnAt(this, position, framesPath, "strike", vfxOffset, vfxScale);
		SpawnLightFlash(position, LightningLightColor, LightningFlashEnergy, LightningFlashScale, LightningFlashDuration);

		Hazard hazard = Hazard.CreateArea(this, instantKill: false, damage: damage, knockbackForce: 220f);
		hazard.GlobalPosition = position;
		hazard.AddChild(new CollisionShape2D { Shape = hazardShape });

		await ToSignal(GetTree().CreateTimer(activeDuration), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(hazard))
			hazard.QueueFree();
	}

	// Brief standalone flash at the impact point — not parented to the bolt sprite VfxSpawner
	// creates internally (that call doesn't hand back a reference), so this tracks its own short
	// lifetime instead.
	private async void SpawnLightFlash(Vector2 position, Color color, float energy, float textureScale, float duration)
	{
		var light = new PointLight2D
		{
			Color = color,
			Energy = energy,
			Texture = GD.Load<Texture2D>(LightTexturePath),
			TextureScale = textureScale,
		};
		GetTree().CurrentScene.AddChild(light);
		light.GlobalPosition = position;

		await ToSignal(GetTree().CreateTimer(duration), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(light))
			light.QueueFree();
	}

	private async void CastLightningStrike()
	{
		Node2D player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		if (player is null)
			return;

		_isCasting = true;
		StrikeAt(player.GlobalPosition, LightningTelegraphDuration, LightningActiveDuration, LightningDamage,
			new CircleShape2D { Radius = LightningRadius }, LightningFramesPath, LightningVfxOffset, LightningVfxScale);

		await ToSignal(GetTree().CreateTimer(LightningTelegraphDuration + LightningActiveDuration), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			_isCasting = false;
	}

	// RainSafeGapCount of the RainStrikeCount points are skipped entirely (both telegraph and
	// strike) — those are the "eggs" the player needs to be standing on when the rest land.
	private async void CastRain()
	{
		if (RainStrikeCount <= 0)
			return;

		_isCasting = true;

		bool[] isSafe = new bool[RainStrikeCount];
		int safeCount = Mathf.Clamp(RainSafeGapCount, 0, RainStrikeCount);
		while (safeCount > 0)
		{
			int index = _rng.RandiRange(0, RainStrikeCount - 1);
			if (isSafe[index])
				continue;
			isSafe[index] = true;
			safeCount--;
		}

		float step = RainStrikeCount > 1 ? (MapMaxX - MapMinX) / (RainStrikeCount - 1) : 0f;
		for (int i = 0; i < RainStrikeCount; i++)
		{
			if (isSafe[i])
				continue;

			float x = MapMinX + step * i;
			StrikeAt(new Vector2(x, MapFloorY), RainTelegraphDuration, RainActiveDuration, RainDamage,
				new RectangleShape2D { Size = RainHitboxSize }, RainFramesPath, RainVfxOffset, RainVfxScale);
		}

		await ToSignal(GetTree().CreateTimer(RainTelegraphDuration + RainActiveDuration), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			_isCasting = false;
	}

	// Marches strikes AccordionSpacing apart from a random side of the map to the other, staggered
	// AccordionStrikeGap apart in time. Count is derived from spacing (not fixed) so the wall always
	// stays tightly packed regardless of map width. Fires all the per-step tasks up front (not
	// awaited in sequence) so each one's own telegraph/impact naturally overlaps the next step's
	// start — that overlap is what reads as a sweeping wave instead of a slow one-at-a-time march.
	private async void CastAccordion()
	{
		if (AccordionSpacing <= 0f)
			return;

		_isCasting = true;

		bool leftToRight = _rng.Randf() < 0.5f;
		int strikeCount = Mathf.Max(2, Mathf.CeilToInt((MapMaxX - MapMinX) / AccordionSpacing) + 1);

		for (int i = 0; i < strikeCount; i++)
		{
			float x = leftToRight ? MapMinX + AccordionSpacing * i : MapMaxX - AccordionSpacing * i;
			RunAccordionStep(new Vector2(x, MapFloorY), i * AccordionStrikeGap);
		}

		float totalDuration = (strikeCount - 1) * AccordionStrikeGap + AccordionTelegraphDuration + AccordionActiveDuration;
		await ToSignal(GetTree().CreateTimer(totalDuration), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			_isCasting = false;
	}

	private async void RunAccordionStep(Vector2 position, float startDelay)
	{
		if (startDelay > 0f)
			await ToSignal(GetTree().CreateTimer(startDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		StrikeAt(position, AccordionTelegraphDuration, AccordionActiveDuration, AccordionDamage,
			new CircleShape2D { Radius = AccordionRadius }, LightningFramesPath, LightningVfxOffset, LightningVfxScale);
	}
}
