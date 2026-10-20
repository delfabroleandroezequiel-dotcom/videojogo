using Godot;
using Metroidvania.Save;
using Metroidvania.Shared;
using Metroidvania.UI;

namespace Metroidvania.World;

public partial class Enemy : CharacterBody2D
{
	// Shared balance data for this enemy TYPE. When set, its values override the fields below
	// at _Ready() — see EnemyProfile.cs. Leave unassigned to keep using this scene's own inline
	// defaults (handy for one-off/unique enemies that don't need a shared profile).
	[Export] public EnemyProfile Profile;

	[Export] public float DetectionRange = 400f;
	[Export] public float MoveSpeed = 80f;
	[Export] public float Gravity = 900f;
	[Export] public float StopDistance = 0f;

	// Once the enemy stops at StopDistance, the player has to retreat this much further before it
	// resumes closing in — without this, a single-threshold check re-triggers a chase step from the
	// smallest jitter around StopDistance (e.g. the two CharacterBody2Ds nudging each other apart
	// on overlap), which reads as the enemy endlessly walking into and bouncing off the player.
	[Export] public float ChaseHysteresis = 12f;

	// How long the player has to stay on the opposite side before this enemy actually commits to
	// facing them — instant re-facing let a last-instant dash behind an enemy mid-windup do nothing,
	// since FacingRight (and the attack hitbox direction derived from it) just snapped to the
	// player's new side on the very frame the swing fired. A brief window means a well-timed dash
	// can still catch it mid-swing facing the wrong way.
	[Export] public float TurnDelay = 0.15f;

	// Off by default — see EnemyProfile.KnockbackEnabled, which overrides this the same way it
	// overrides every other inline default below whenever a Profile is assigned.
	[Export] public bool KnockbackEnabled = false;
	[Export] public float KnockbackDuration = 0.2f;
	[Export] public float ExplosionScale = 1f;
	[Export] public PackedScene ExplosionScene;
	// Off = play the sprite's own "death" animation and leave the corpse lying there (it goes away
	// with the scene; on reload IsDefeated() skips it) instead of the ExplosionScene burst. Falls back to exploding if the sprite has no "death" animation.
	[Export] public bool ExplodeOnDeath = true;
	// On = instead of exploding (or the death animation), the sprite freezes and burns away in
	// pixel-sized chunks with a glowing edge (DissolveShader, see Dissolve.gdshader), shedding
	// sparks, then the body is removed. Takes priority over ExplodeOnDeath. Needs DissolveShader set.
	[Export] public bool DissolveOnDeath = false;
	[Export] public Shader DissolveShader;
	[Export] public float DissolveDuration = 0.9f;
	[Export] public Color DissolveEdgeColor = new(0.35f, 1f, 0.5f, 1f);
	// Size of one rising spark, in world pixels (1-2x this).
	[Export] public float DissolveSparkPixelSize = 1.5f;
	[Export] public string CustomPersistenceId = "";
	[Export] public LootEntry[] LootTable = System.Array.Empty<LootEntry>();
	[Export] public float ContactDamageMultiplier = 0.3f;

	// Any ground enemy with a LedgeCheck RayCast2D in its scene refuses to step off a platform
	// edge while chasing instead of walking straight into a pit. Scenes without that node (or
	// flying enemies) just skip the check — CanMoveInDirection defaults to "yes".
	[Export] public float LedgeCheckAheadX = 14f;
	[Export] public float LedgeCheckDownY = 28f;

	// Bosses only (anything in the "boss" group): the look of its HUD health bar (one of
	// resources/ui/BarrasJefe; empty = the bar's default) and the translation key of the name shown
	// above it (empty = no name).
	[Export] public BossBarStyle BossBarStyle;
	[Export] public string BossNameKey = "";

	// Regular enemies only, opt-in: the player can execute it from behind (see Player.TryBackstab).
	// Ignored on bosses.
	[Export] public bool Backstabbable;

	// Parry stun: this many melee parries in a row (each within ParryChainWindow of the last) leave
	// it dazed for StunDuration — frozen in its idle with stars over its head, open to an execution
	// from any side. 0 = can't be stunned. Bosses never are.
	[Export] public int ParriesToStun = 2;
	[Export] public float ParryChainWindow = 2.5f;
	[Export] public float StunDuration = 2f;
	[Export] public Color StunStarColor = new(1f, 0.85f, 0.3f, 1f);

	// Poise (postura): each player hit chips it by that move's poise damage (see Hitbox.PoiseDamage);
	// at 0 the guard breaks into the same daze a parry chain causes (Stun), open to an execution. It
	// refills in full once PoiseRegenDelay passes without a hit, so breaking it takes sustained
	// pressure, not scattered pokes. 0 = no poise (never breaks). Bosses never break, like parry stuns.
	[Export] public float MaxPoise = 40f;
	[Export] public float PoiseRegenDelay = 1.5f;
	// How long a hit that doesn't break poise leaves the enemy reeling, when it reacts at all (see
	// OnPoiseHit — the base enemy shrugs hits off; MeleeEnemy flinches out of its swing).
	[Export] public float FlinchDuration = 0.25f;

	private RayCast2D _ledgeCheck;
	private int _freezeCount;
	private ProcessModeEnum _processModeBeforeFreeze;
	private int _parryCount;
	private ulong _lastParryMsec;
	// Clon señuelo: how close counts as "arrived" at the decoy, and how long a hit keeps this enemy
	// from being lured again (it's onto the player now).
	private const float LureArriveDistance = 20f;
	private const ulong LureImmunityMsec = 5000;
	private ulong _lureImmuneUntilMsec;
	private int _stunRunId;
	private Node2D _stunStars;
	private float _poise;
	private ulong _lastPoiseHitMsec;
	private float _flinchTimer;

	public Stats Stats { get; private set; }
	protected Node2D Visual;
	protected AnimatedSprite2D Sprite;
	protected bool FacingRight = true;

	protected bool IsQueuedForRemoval;
	public string PersistenceId { get; private set; }

	public bool IsFacingRight => FacingRight;
	public bool IsStunned { get; private set; }
	public bool CanBeExecuted => !IsInGroup("boss") && !IsQueuedForRemoval && Stats is { CurrentHealth: > 0 };
	public bool CanBeBackstabbed => Backstabbable && CanBeExecuted;
	// True while ApplyContactDamage is hurting the player, so a block that catches mere body contact
	// isn't mistaken for parrying an attack (see Player.ReportMeleeParry).
	public static bool DealingContactDamage { get; private set; }

	// Freezes the enemy in place (AI, attacks, contact damage, animation) while the player's
	// execution plays out on it, and lets it go again.
	public void HoldForExecution(bool hold)
	{
		if (hold)
			Freeze();
		else
			Unfreeze();
	}

	// Counted, so a stun and an execution can overlap and the enemy only wakes once both let go.
	private void Freeze()
	{
		if (_freezeCount++ > 0)
			return;
		_processModeBeforeFreeze = ProcessMode;
		Velocity = Vector2.Zero;
		// KeepActive: the default DisableMode (Remove) pulls a disabled body out of the physics
		// space, so a stunned enemy couldn't be hit by normal attacks, hazards or blasts.
		DisableMode = DisableModeEnum.KeepActive;
		ProcessMode = ProcessModeEnum.Disabled;
		GetNodeOrNull<Metroidvania.Player.Hitbox>("AttackHitbox")?.Deactivate();
	}

	private void Unfreeze()
	{
		if (_freezeCount == 0 || --_freezeCount > 0)
			return;
		ProcessMode = _processModeBeforeFreeze;
	}

	public void RegisterParried()
	{
		if (ParriesToStun <= 0 || IsStunned || !CanBeExecuted)
			return;
		ulong now = Time.GetTicksMsec();
		if (now - _lastParryMsec > (ulong)(ParryChainWindow * 1000f))
			_parryCount = 0;
		_lastParryMsec = now;
		if (++_parryCount < ParriesToStun)
			return;
		_parryCount = 0;
		Stun();
	}

	public bool IsFlinching => _flinchTimer > 0f;

	// Returns true when this hit broke the guard (the caller adds the heavier hit-stop for it).
	public bool TakePoiseDamage(float amount)
	{
		if (MaxPoise <= 0f || amount <= 0f || IsStunned || !CanBeExecuted)
			return false;
		ulong now = Time.GetTicksMsec();
		if (now - _lastPoiseHitMsec > (ulong)(PoiseRegenDelay * 1000f))
			_poise = MaxPoise;
		_lastPoiseHitMsec = now;

		_poise -= amount;
		if (_poise > 0f)
		{
			OnPoiseHit();
			return false;
		}
		_poise = MaxPoise;
		_parryCount = 0;
		_flinchTimer = 0f;
		Stun();
		return true;
	}

	// A hit that chipped poise without breaking it. Default: no reaction (the enemy keeps doing
	// whatever its own attack coroutine is doing — interrupting that safely is per enemy type).
	protected virtual void OnPoiseHit() { }

	// Reels for FlinchDuration: no AI movement, attacks or turning (see _PhysicsProcess), playing
	// the sheet's hurt/hit animation when it has one.
	protected void Flinch()
	{
		_flinchTimer = FlinchDuration;
		Velocity = new Vector2(0f, Velocity.Y);
		if (Sprite?.SpriteFrames is { } frames)
		{
			string anim = frames.HasAnimation("hurt") ? "hurt" : frames.HasAnimation("hit") ? "hit" : null;
			if (anim is not null)
				Sprite.Play(anim);
		}
	}

	private async void Stun()
	{
		int runId = ++_stunRunId;
		IsStunned = true;
		Freeze();
		// The idle keeps playing under the freeze, so it reads as dazed rather than paused.
		if (Sprite is not null)
		{
			Sprite.ProcessMode = ProcessModeEnum.Always;
			if (Sprite.SpriteFrames?.HasAnimation("idle") == true)
				Sprite.Play("idle");
		}
		ShowStunStars();
		await ToSignal(GetTree().CreateTimer(StunDuration), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this) && runId == _stunRunId)
			EndStun();
	}

	public void EndStun()
	{
		if (!IsStunned)
			return;
		IsStunned = false;
		_stunRunId++;
		if (Sprite is not null)
			Sprite.ProcessMode = ProcessModeEnum.Inherit;
		_stunStars?.QueueFree();
		_stunStars = null;
		Unfreeze();
	}

	// Three neon dots circling flat above the head (a ring spun inside a vertically squashed
	// parent reads as an orbit seen from the side). Runs while the enemy itself is frozen.
	private void ShowStunStars()
	{
		_stunStars = new Node2D
		{
			ProcessMode = ProcessModeEnum.Always,
			Position = new Vector2(0f, BodyTop - 8f),
			Scale = new Vector2(1f, 0.4f),
		};
		var ring = new Node2D();
		_stunStars.AddChild(ring);
		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		var dot = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres");
		for (int i = 0; i < 3; i++)
		{
			ring.AddChild(new Sprite2D
			{
				Texture = dot,
				Material = additive,
				Modulate = StunStarColor,
				Scale = Vector2.One * 0.12f,
				Position = Vector2.Right.Rotated(Mathf.Tau * i / 3f) * 13f,
			});
		}
		AddChild(_stunStars);
		Tween spin = ring.CreateTween().SetLoops();
		spin.TweenProperty(ring, "rotation", Mathf.Tau, 0.8f).From(0f);
	}

	// True while the player is within DetectionRange — updated every physics frame alongside the
	// existing chase check below, so it's free (no extra distance query) for anything that wants
	// to react to "this enemy has spotted the player" (e.g. CaveCollapseOverlay).
	public bool PlayerDetected { get; protected set; }

	// Alerted by a noise (a sprung bear trap, an alarm...): for a while it notices the player from
	// AlertRange away instead of DetectionRange, so a whole camp comes running.
	[Export] public float AlertRange = 900f;
	private ulong _alertUntilMsec;
	public void Alert(float seconds) =>
		_alertUntilMsec = System.Math.Max(_alertUntilMsec, Time.GetTicksMsec() + (ulong)(seconds * 1000f));

	protected Area2D ContactArea;
	private bool _isHoldingDistance;
	private float _turnAwayTimer;
	private float _knockbackTimer;
	private Vector2 _knockbackVelocity;
	private Color _baseModulate;

	// Shared across all enemies and re-seeded once, rather than a fresh RandomNumberGenerator
	// per death: several enemies dying the same frame (e.g. an AoE) would otherwise all
	// Randomize() off the same coarse time source and roll near-identical loot results.
	private static readonly RandomNumberGenerator LootRng = new();

	static Enemy()
	{
		LootRng.Randomize();
	}

	public override void _Ready()
	{
		AddToGroup("enemy");

		PersistenceId = string.IsNullOrEmpty(CustomPersistenceId) ? GetPath().ToString() : CustomPersistenceId;
		if (IsDefeated())
		{
			IsQueuedForRemoval = true;
			QueueFree();
			return;
		}

		Stats = GetNode<Stats>("Stats");
		ApplyProfile();
		_poise = MaxPoise;
		Visual = GetNode<Node2D>("Visual");
		_baseModulate = Visual.Modulate;
		Sprite = Visual.GetNodeOrNull<AnimatedSprite2D>("CharacterSprite");
		ContactArea = GetNode<Area2D>("ContactArea");
		_ledgeCheck = GetNodeOrNull<RayCast2D>("LedgeCheck");
		CacheBodyExtents();
		AddChild(new ContactShadow { Width = BodyHalfWidth * 2.4f, FeetOffset = BodyBottom });
		Stats.Died += OnDefeated;
		Stats.Died += GrantEcos;
		Stats.HitTaken += _ => _lureImmuneUntilMsec = Time.GetTicksMsec() + LureImmunityMsec;
		// Killed while frozen (stunned, mid-execution): wake up so the death plays out.
		Stats.Died += () =>
		{
			EndStun();
			while (_freezeCount > 0)
				Unfreeze();
		};

		StatBar healthBar = GetNode<StatBar>("HealthBar");
		StatBar staminaBar = GetNode<StatBar>("StaminaBar");
		healthBar.Visible = false;
		staminaBar.Visible = false;
		Stats.HealthChanged += (current, max) => healthBar.SetRatio((float)current / max);
		Stats.StaminaChanged += (current, max) => staminaBar.SetRatio((float)current / max);
		Stats.HitTaken += (isProjectile) => FlashHit();
	}

	private void ApplyProfile()
	{
		if (Profile is null)
			return;

		Stats.MaxHealth = Profile.MaxHealth;
		Stats.AttackPower = Profile.AttackPower;
		Stats.Defense = Profile.Defense;
		Stats.MaxStamina = Profile.MaxStamina;
		Stats.StaminaRegenPerSecond = Profile.StaminaRegenPerSecond;
		Stats.Weakness = Profile.Weakness;
		Stats.WeaknessDamageMultiplier = Profile.WeaknessDamageMultiplier;
		Stats.ResetToFull();

		MoveSpeed = Profile.MoveSpeed;
		DetectionRange = Profile.DetectionRange;
		StopDistance = Profile.StopDistance;
		Gravity = Profile.Gravity;
		KnockbackEnabled = Profile.KnockbackEnabled;
		KnockbackDuration = Profile.KnockbackDuration;
		ContactDamageMultiplier = Profile.ContactDamageMultiplier;
		if (Profile.MaxPoise >= 0f)
			MaxPoise = Profile.MaxPoise;
		LootTable = Profile.LootTable;
	}

	private void FlashHit()
	{
		var tween = CreateTween();
		Visual.Modulate = new Color(2f, 0.2f, 0.2f);
		// Tweens back to this enemy's own authored tint, not a hardcoded white — a boss/enemy with a
		// non-default Visual.Modulate (e.g. SpiderBossArena's darker recolor) would otherwise lose
		// that tint permanently the first time it took a hit.
		tween.TweenProperty(Visual, "modulate", _baseModulate, 0.25f);
	}

	// Set by a subclass right after EnemyCombatCoordinator.TryAcquireAttackSlot() succeeds, cleared
	// once its attack coroutine releases the slot normally. OnDefeated() below uses this as a
	// safety net: an enemy killed mid-attack/mid-windup has no guarantee its own coroutine ever
	// resumes to hit that release (see EnemyCombatCoordinator's comment on the slot otherwise
	// staying stuck until the next level load) — dying always frees a held slot immediately
	// instead of leaving every other enemy waiting on one that's never coming back.
	protected bool HoldingAttackSlot;

	// Ecos for the kill, straight into the player's count. < 0 = derived from max health (see
	// LevelingConfig.EcosPerEnemyHealth). Summoners clear GrantsEcos on what they raise, so an
	// endless summon can't be farmed.
	[Export] public int EcosReward = -1;
	public bool GrantsEcos { get; set; } = true;

	private void GrantEcos()
	{
		if (!GrantsEcos)
			return;
		LevelingConfig config = LevelingConfig.Get();
		int reward = EcosReward >= 0
			? EcosReward
			: Mathf.Max(config.MinEcosReward, Mathf.RoundToInt(Stats.MaxHealth * config.EcosPerEnemyHealth));
		SaveManager.Instance.AddEcos(reward);
	}

	protected virtual bool IsDefeated() => SaveManager.Instance.IsCommonEnemyDefeated(PersistenceId);

	protected virtual void OnDefeated()
	{
		if (HoldingAttackSlot)
		{
			EnemyCombatCoordinator.ReleaseAttackSlot();
			HoldingAttackSlot = false;
		}

		SaveManager.Instance.MarkCommonEnemyDefeated(PersistenceId);
		CallDeferred(MethodName.SpawnLoot);
		if (DropsShadowEssence && !string.IsNullOrEmpty(SceneFilePath))
			CallDeferred(MethodName.SpawnShadowEssence);

		if (DissolveOnDeath && DissolveShader is not null && Sprite is not null)
		{
			DissolveAway();
			return;
		}

		if (!ExplodeOnDeath && Sprite?.SpriteFrames?.HasAnimation("death") == true)
		{
			PlayDeathAnimation();
			return;
		}

		SpawnExplosion();
		QueueFree();
	}

	// IsQueuedForRemoval makes every AI/attack loop bail out (same flag the subclasses already
	// check), and dropping the collision layer/contact area stops the corpse from being hit again
	// or hurting the player while the animation plays. The attack hitbox is switched off here too,
	// since an attack coroutine that bails on IsQueuedForRemoval skips its own Deactivate().
	private void PlayDeathAnimation()
	{
		DisableForDeath();
		// Non-looping, so it holds on its last frame — the corpse stays until the scene unloads.
		Sprite.Play("death");
	}

	// Shared by every non-exploding death: stops AI/attacks, collisions, contact damage, the attack
	// hitbox and the stat bars while the body is still on screen.
	private void DisableForDeath()
	{
		IsQueuedForRemoval = true;
		// The corpse stays in the tree, so drop it from "enemy" or companions (CompanionNpc) keep targeting it.
		RemoveFromGroup("enemy");
		Velocity = Vector2.Zero;
		SetDeferred(CollisionObject2D.PropertyName.CollisionLayer, 0);
		ContactArea.SetDeferred(Area2D.PropertyName.Monitoring, false);
		if (GetNodeOrNull<Metroidvania.Player.Hitbox>("AttackHitbox") is { } attackHitbox)
			attackHitbox.CallDeferred(Metroidvania.Player.Hitbox.MethodName.Deactivate);
		GetNode<StatBar>("HealthBar").Visible = false;
		GetNode<StatBar>("StaminaBar").Visible = false;

		Visual.Modulate = _baseModulate;
	}

	private async void DissolveAway()
	{
		DisableForDeath();
		Sprite.Pause();

		Texture2D frame = Sprite.SpriteFrames.GetFrameTexture(Sprite.Animation, Sprite.Frame);
		Vector2 frameSize = frame?.GetSize() ?? new Vector2(48f, 48f);
		var material = new ShaderMaterial { Shader = DissolveShader };
		material.SetShaderParameter("progress", 0f);
		material.SetShaderParameter("edge_color", DissolveEdgeColor);
		material.SetShaderParameter("sprite_height", frameSize.Y);
		Sprite.Material = material;

		GpuParticles2D sparks = BuildDissolveSparks(frameSize * Sprite.Scale.Abs());
		Visual.AddChild(sparks);
		sparks.Position = Sprite.Position;
		sparks.Emitting = true;

		Tween tween = CreateTween();
		tween.TweenMethod(Callable.From<float>(p => material.SetShaderParameter("progress", p)), 0f, 1f, DissolveDuration)
			.SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Sine);
		await ToSignal(tween, Tween.SignalName.Finished);
		if (!IsInstanceValid(this))
			return;

		sparks.Emitting = false;
		Sprite.Visible = false;
		await ToSignal(GetTree().CreateTimer(sparks.Lifetime), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			QueueFree();
	}

	// Tiny square pixel sparks rising off the dissolving body, same color as the burning edge —
	private GpuParticles2D BuildDissolveSparks(Vector2 bodySize)
	{
		var process = new ParticleProcessMaterial
		{
			EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
			EmissionBoxExtents = new Vector3(bodySize.X * 0.35f, bodySize.Y * 0.35f, 1f),
			Direction = new Vector3(0f, -1f, 0f),
			Spread = 30f,
			InitialVelocityMin = 18f,
			InitialVelocityMax = 45f,
			Gravity = new Vector3(0f, -25f, 0f),
			ScaleMin = DissolveSparkPixelSize / 64f,
			ScaleMax = DissolveSparkPixelSize * 2f / 64f,
		};
		var fade = new Gradient();
		fade.SetColor(0, new Color(1f, 1f, 1f, 1f));
		fade.SetColor(1, new Color(1f, 1f, 1f, 0f));
		process.ColorRamp = new GradientTexture1D { Gradient = fade };

		return new GpuParticles2D
		{
			Amount = 16,
			Lifetime = 0.8,
			Randomness = 0.5f,
			ProcessMaterial = process,
			Texture = GD.Load<Texture2D>("res://assets/sprites/effects/white_square.png"),
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			Modulate = DissolveEdgeColor,
			Emitting = false,
		};
	}

	protected void SpawnExplosion()
	{
		if (ExplosionScene is null)
			return;

		Node explosionNode = ExplosionScene.Instantiate();
		if (explosionNode is Explosion explosion)
			explosion.TargetScale = ExplosionScale;

		GetTree().CurrentScene.AddChild(explosionNode);
		((Node2D)explosionNode).GlobalPosition = GlobalPosition;
	}

	// Bastón: the defeated enemy leaves a wisp of shadow; taking it stores one summon use of this enemy
	// type (keyed by its scene). Bosses override OnDefeated and don't drop one.
	[Export] public bool DropsShadowEssence = true;

	private void SpawnShadowEssence()
	{
		if (GetTree()?.CurrentScene is not Node scene)
			return;
		var essence = new ShadowEssence { EnemyKey = SceneFilePath };
		scene.AddChild(essence);
		essence.GlobalPosition = GlobalPosition + new Vector2(0f, -10f);
	}

	protected void SpawnLoot()
	{
		if (LootTable is null || LootTable.Length == 0)
			return;

		foreach (LootEntry entry in LootTable)
		{
			if (entry?.DropScene is null || LootRng.Randf() > entry.DropChance)
				continue;

			Node dropNode = entry.DropScene.Instantiate();
			GetTree().CurrentScene.AddChild(dropNode);

			if (dropNode is Node2D dropNode2D)
				dropNode2D.GlobalPosition = GlobalPosition;

			if (dropNode is Coin coin)
				coin.Value = LootRng.RandiRange(entry.MinAmount, entry.MaxAmount);
			else if (dropNode is ItemPickup pickup && entry.RewardItem is not null)
				pickup.Item = entry.RewardItem;
		}
	}

	public virtual void ApplyKnockback(Vector2 direction, float force)
	{
		if (!KnockbackEnabled)
			return;

		_knockbackVelocity = direction * force;
		_knockbackTimer = KnockbackDuration;
	}

	// Poison damage-over-time — same contract as Player.ApplyPoison (PoisonSpit/PoisonPuddle call it
	// through HasMethod), so the player's own poison spit works on enemies. A new poison replaces the
	// running one; ticks don't arm invulnerability, so they never eat a real hit.
	[Export] public Color PoisonTint = new(0.7f, 1.25f, 0.6f, 1f);
	private int _poisonSequence;

	public async void ApplyPoison(int tickDamage, float tickInterval, float duration)
	{
		if (IsQueuedForRemoval || Stats is null)
			return;
		int sequence = ++_poisonSequence;
		if (Sprite is not null)
			Sprite.SelfModulate = PoisonTint;
		float elapsed = 0f;
		while (elapsed < duration)
		{
			await ToSignal(GetTree().CreateTimer(tickInterval), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval || sequence != _poisonSequence)
				return;
			Stats.TakeDamage(tickDamage, element: DamageElement.Poison, ignoreInvulnerability: true, armInvulnerability: false);
			elapsed += tickInterval;
		}
		if (IsInstanceValid(this) && sequence == _poisonSequence && Sprite is not null)
			Sprite.SelfModulate = Colors.White;
	}

	// Forced displacement from the environment/spells (e.g. a Tornado pulling and lifting), applied
	// even to enemies with KnockbackEnabled off: horizontal velocity.X for duration, and an upward
	// velocity.Y (negative) launches it into the air, then gravity takes over.
	public void Launch(Vector2 velocity, float duration)
	{
		if (IsQueuedForRemoval)
			return;
		_knockbackVelocity = new Vector2(velocity.X, 0f);
		_knockbackTimer = Mathf.Max(_knockbackTimer, duration);
		if (velocity.Y < 0f)
			Velocity = new Vector2(Velocity.X, Mathf.Min(Velocity.Y, velocity.Y));
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;

		Vector2 velocity = Velocity;

		if (_knockbackTimer > 0)
		{
			_knockbackTimer -= (float)delta;
			velocity.X = _knockbackVelocity.X;
			// Only pin to the floor when not moving up (a Launch lift starts on the floor).
			velocity.Y = IsOnFloor() && velocity.Y >= 0f ? 0 : velocity.Y + Gravity * (float)delta;
			Velocity = velocity;
			MoveAndSlide();
			return;
		}

		if (_flinchTimer > 0f)
		{
			_flinchTimer -= (float)delta;
			velocity.X = Mathf.MoveToward(velocity.X, 0f, MoveSpeed);
			velocity.Y = IsOnFloor() && velocity.Y >= 0f ? 0f : velocity.Y + Gravity * (float)delta;
			Velocity = velocity;
			MoveAndSlide();
			return;
		}

		if (!IsOnFloor())
			velocity.Y += Gravity * (float)delta;

		// Clon señuelo: a lured enemy walks over to the decoy and ignores the player (PlayerDetected
		// off, which most subclasses' own AI also gates on) until the lure ends or something hurts it.
		if (!IsInGroup("boss") && Time.GetTicksMsec() >= _lureImmuneUntilMsec
			&& Metroidvania.Player.ShadowDecoy.TryGetLure(GlobalPosition, out Vector2 lure))
		{
			PlayerDetected = false;
			_isHoldingDistance = false;
			float dx = lure.X - GlobalPosition.X;
			float sign = Mathf.Sign(dx);
			if (Mathf.Abs(dx) > LureArriveDistance)
			{
				FacingRight = dx > 0f;
				Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);
			}
			velocity.X = Mathf.Abs(dx) > LureArriveDistance && CanStepTo(sign) ? sign * MoveSpeed : Mathf.MoveToward(velocity.X, 0, MoveSpeed);
			Velocity = velocity;
			MoveAndSlide();
			UpdateAnimation(Velocity);
			UpdateFootsteps(Velocity, delta);
			return;
		}

		Node2D player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		if (player is not null)
		{
			float distanceX = player.GlobalPosition.X - GlobalPosition.X;
			float absDistance = Mathf.Abs(distanceX);
			PlayerDetected = absDistance <= DetectionRange || (Time.GetTicksMsec() < _alertUntilMsec && absDistance <= AlertRange);
			if (PlayerDetected)
			{
				bool desiredFacingRight = DesiredFacingRight(player, distanceX);
				if (CanTurnToFacePlayer && desiredFacingRight != FacingRight)
				{
					_turnAwayTimer += (float)delta;
					if (_turnAwayTimer >= TurnDelay)
					{
						FacingRight = desiredFacingRight;
						_turnAwayTimer = 0f;
					}
				}
				else
				{
					_turnAwayTimer = 0f;
				}
				Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);

				velocity.X = ComputeMoveX(player, distanceX, velocity.X, delta);
			}
			else
			{
				_isHoldingDistance = false;
				_turnAwayTimer = 0f;
				velocity.X = Mathf.MoveToward(velocity.X, 0, MoveSpeed);
			}
		}

		Velocity = velocity;
		MoveAndSlide();

		// The actual post-slide Velocity, not the pre-MoveAndSlide local above — walking straight
		// into a wall zeroes this out via collision response while the AI's commanded velocity
		// above stays at full MoveSpeed, so animating off the commanded value kept playing "run"
		// against a wall the enemy wasn't actually moving through.
		UpdateAnimation(Velocity);
		UpdateFootsteps(Velocity, delta);
		if (ContactDamageEnabled)
			ApplyContactDamage();
	}

	// Horizontal movement while the player is detected. Default = walk straight at the player until
	// StopDistance, with ChaseHysteresis. Enemies with their own spacing logic (e.g. ElfArcher
	// kiting away, hopping) override this instead of re-implementing gravity/knockback/animation.
	protected virtual float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		float absDistance = Mathf.Abs(distanceX);
		float moveSign = Mathf.Sign(distanceX);

		if (_isHoldingDistance)
		{
			if (absDistance > StopDistance + ChaseHysteresis)
				_isHoldingDistance = false;
		}
		else if (absDistance <= StopDistance)
		{
			_isHoldingDistance = true;
		}

		return !_isHoldingDistance && CanChase && CanMoveInDirection(moveSign)
			? moveSign * MoveSpeed
			: Mathf.MoveToward(currentVelocityX, 0, MoveSpeed);
	}

	// Which way the enemy wants to face while the player is detected (still subject to
	// CanTurnToFacePlayer and TurnDelay). Default: toward the player.
	protected virtual bool DesiredFacingRight(Node2D player, float distanceX) => distanceX >= 0;

	// ── Terrain-aware movement helpers (used by the "smart" enemies: ElfArcher, the casters) ──

	// Half width of the body and how far below the origin its feet are, read from the root
	// CollisionShape2D so helpers don't need a hand-measured offset per enemy.
	protected float BodyHalfWidth { get; private set; } = 16f;
	protected float BodyBottom { get; private set; } = 30f;
	protected float BodyTop { get; private set; } = -30f;

	private void CacheBodyExtents()
	{
		CollisionShape2D body = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		switch (body?.Shape)
		{
			case RectangleShape2D rect:
				BodyHalfWidth = rect.Size.X * 0.5f;
				BodyBottom = body.Position.Y + rect.Size.Y * 0.5f;
				BodyTop = body.Position.Y - rect.Size.Y * 0.5f;
				break;
			case CapsuleShape2D capsule:
				BodyHalfWidth = capsule.Radius;
				BodyBottom = body.Position.Y + capsule.Height * 0.5f;
				BodyTop = body.Position.Y - capsule.Height * 0.5f;
				break;
		}
	}

	// Ground right past the body's leading edge — works with or without a LedgeCheck node.
	protected bool HasGroundAhead(float sign, float maxStepDown = 14f)
	{
		float x = GlobalPosition.X + sign * (BodyHalfWidth + 2f);
		float feetY = GlobalPosition.Y + BodyBottom;
		var query = PhysicsRayQueryParameters2D.Create(new Vector2(x, feetY - 6f), new Vector2(x, feetY + maxStepDown),
			CollisionMask, new Godot.Collections.Array<Rid> { GetRid() });
		return GetWorld2D().DirectSpaceState.IntersectRay(query).Count > 0;
	}

	// Safe to walk this way: ground ahead and no wall right in front.
	protected bool CanStepTo(float sign) =>
		CanMoveInDirection(sign) && HasGroundAhead(sign) && !TestMove(GlobalTransform, new Vector2(sign * 12f, 0f));

	// Steps a jump's ballistic arc with TestMove (stopping where the body would bump into something),
	// then checks there's ground under that spot within maxDrop of the current feet level. Uses the
	// body's own CollisionMask so one-way platforms it can't stand on don't count. Lets an enemy
	// refuse a hop that would land it in a pit or off a ledge.
	protected bool LandsSafely(Vector2 launchVelocity, float maxDrop)
	{
		const float step = 1f / 30f;
		float airTime = 2f * -launchVelocity.Y / Mathf.Max(1f, Gravity);
		Transform2D probe = GlobalTransform;
		Vector2 velocity = launchVelocity;
		for (float t = 0f; t < airTime; t += step)
		{
			Vector2 motion = velocity * step;
			if (TestMove(probe, motion))
				break;
			probe.Origin += motion;
			velocity.Y += Gravity * step;
		}

		float feetY = GlobalPosition.Y + BodyBottom;
		Vector2 from = new(probe.Origin.X, Mathf.Min(probe.Origin.Y, GlobalPosition.Y) - BodyBottom);
		Vector2 to = new(probe.Origin.X, feetY + maxDrop);
		var query = PhysicsRayQueryParameters2D.Create(from, to, CollisionMask, new Godot.Collections.Array<Rid> { GetRid() });
		var hit = GetWorld2D().DirectSpaceState.IntersectRay(query);
		return hit.Count > 0 && ((Vector2)hit["normal"]).Y < -0.7f;
	}

	// Keeps the player inside a [minDistance, maxDistance] band: backs off (with `overshoot`
	// hysteresis so it opens a real gap instead of stuttering at the threshold) when they're too
	// close, walks up when too far, never off a ledge or into a wall. `retreating` is the caller's
	// own state flag (also handy for facing the way it flees).
	protected float KitingMoveX(float distanceX, float currentVelocityX, float minDistance, float maxDistance,
		float overshoot, float retreatSpeedMultiplier, ref bool retreating)
	{
		float absDistance = Mathf.Abs(distanceX);
		float toward = Mathf.Sign(distanceX);
		float away = -toward;

		bool wantsRetreat = absDistance < minDistance || (retreating && absDistance < minDistance + overshoot);
		if (wantsRetreat && CanStepTo(away))
		{
			retreating = true;
			return away * MoveSpeed * retreatSpeedMultiplier;
		}
		retreating = false;

		if (absDistance > maxDistance && CanStepTo(toward))
			return toward * MoveSpeed;

		return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
	}

	[Export] public float FootstepInterval = 0.35f;
	private float _footstepTimer;

	// Shared across every enemy (not just melee/ranged) so nothing needs to opt in per subclass —
	// same reasoning as contact damage/knockback living here instead of being hand-copied. Always
	// "Footsteps/Dirt" for now since there's no surface-detection under the enemy yet; the player's
	// own footsteps (Player.cs) have the same limitation, so this isn't a regression, just the same
	// placeholder extended to enemies.
	private void UpdateFootsteps(Vector2 velocity, double delta)
	{
		if (!IsOnFloor() || Mathf.Abs(velocity.X) <= 5f)
		{
			_footstepTimer = 0f;
			return;
		}

		_footstepTimer -= (float)delta;
		if (_footstepTimer > 0f)
			return;

		Sfx.PlayAt(this, "Footsteps/Dirt", "Dirt Walk");
		_footstepTimer = FootstepInterval;
	}

	// False for enemies with a real attack hitbox (see MeleeEnemy) — otherwise just walking into
	// their body chips contact damage on top of whatever their actual attack swing already does.
	// Stays true for enemies whose only offense IS contact (RatEnemy has no hitbox of its own).
	protected virtual bool ContactDamageEnabled => true;

	// Override to lock facing while committed to an action (e.g. mid-attack/windup) — default
	// always allows turning, since a plain contact-damage enemy (RatEnemy) has no such commitment
	// state to lock against.
	protected virtual bool CanTurnToFacePlayer => true;

	// Same idea, for chasing: without this, a mid-attack/windup enemy keeps sliding toward wherever
	// the player repositioned to (the AI never stopped commanding chase velocity, it just couldn't
	// switch out of the "attack" animation to show a run), reading as the player getting magnet-
	// pulled back into a dodged hit instead of the attack whiffing like it should.
	protected virtual bool CanChase => true;

	protected virtual bool CanMoveInDirection(float sign)
	{
		if (_ledgeCheck is null)
			return true;

		_ledgeCheck.Position = new Vector2(sign * LedgeCheckAheadX, 0f);
		_ledgeCheck.TargetPosition = new Vector2(0f, LedgeCheckDownY);
		_ledgeCheck.ForceRaycastUpdate();
		return _ledgeCheck.IsColliding();
	}

	protected virtual void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null) return;
		string anim = Mathf.Abs(velocity.X) > 5f ? "run" : "idle";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	protected void ApplyContactDamage()
	{
		foreach (Node body in ContactArea.GetOverlappingBodies())
		{
			if (body is not Node2D player || !player.IsInGroup("player"))
				continue;

			Stats targetStats = player.GetNodeOrNull<Stats>("Stats");
			if (targetStats is null)
				continue;

			DealingContactDamage = true;
			targetStats.TakeDamage(Mathf.RoundToInt(Stats.AttackPower * ContactDamageMultiplier));
			DealingContactDamage = false;
		}
	}
}
