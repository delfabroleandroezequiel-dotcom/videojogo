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
		_hurtTimer -= dt;

		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval || _busy || !IsOnFloor())
			return;

		if (!_phaseTwoStarted && Stats.CurrentHealth <= Stats.MaxHealth * PhaseTwoHealthRatio)
		{
			_ = EnterPhaseTwo();
			return;
		}

		if (!PlayerDetected)
			return;

		var player = PlayerReads.Find(this);
		if (player is null)
			return;

		float distance = Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X);

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
		bool danceReady = IsPhaseTwo && !_openWithFlower && _danceCooldownTimer <= 0f && distance >= DanceMinRange && distance <= DanceMaxRange;
		bool flowerReady = IsPhaseTwo && _flowerCooldownTimer <= 0f && (_openWithFlower || distance >= FlowerMinRange);

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

		if (!await Wait(RageTransitionPause))
			return;
		EndMove(0f);
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
		var points = new Vector2[segments + 1];
		for (int i = 0; i <= segments; i++)
			points[i] = Vector2.Right.Rotated(Mathf.Tau * i / segments) * radius;
		return new Line2D { Points = points, Width = width, Modulate = color, Material = material, DefaultColor = Colors.White };
	}

	private void SpawnShockwave(Color color, float radius)
	{
		if (GetTree().CurrentScene is not Node scene)
			return;

		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		var blast = new Node2D();
		scene.AddChild(blast);
		blast.GlobalPosition = GlobalPosition;

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
