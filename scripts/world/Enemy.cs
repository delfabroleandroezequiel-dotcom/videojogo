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
	[Export] public string CustomPersistenceId = "";
	[Export] public LootEntry[] LootTable = System.Array.Empty<LootEntry>();
	[Export] public float ContactDamageMultiplier = 0.3f;

	// Any ground enemy with a LedgeCheck RayCast2D in its scene refuses to step off a platform
	// edge while chasing instead of walking straight into a pit. Scenes without that node (or
	// flying enemies) just skip the check — CanMoveInDirection defaults to "yes".
	[Export] public float LedgeCheckAheadX = 14f;
	[Export] public float LedgeCheckDownY = 28f;

	private RayCast2D _ledgeCheck;

	public Stats Stats { get; private set; }
	protected Node2D Visual;
	protected AnimatedSprite2D Sprite;
	protected bool FacingRight = true;

	protected bool IsQueuedForRemoval;
	public string PersistenceId { get; private set; }

	// True while the player is within DetectionRange — updated every physics frame alongside the
	// existing chase check below, so it's free (no extra distance query) for anything that wants
	// to react to "this enemy has spotted the player" (e.g. CaveCollapseOverlay).
	public bool PlayerDetected { get; protected set; }

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
		Visual = GetNode<Node2D>("Visual");
		_baseModulate = Visual.Modulate;
		Sprite = Visual.GetNodeOrNull<AnimatedSprite2D>("CharacterSprite");
		ContactArea = GetNode<Area2D>("ContactArea");
		_ledgeCheck = GetNodeOrNull<RayCast2D>("LedgeCheck");
		Stats.Died += OnDefeated;

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

	protected virtual bool IsDefeated() => SaveManager.Instance.IsCommonEnemyDefeated(PersistenceId);

	protected virtual void OnDefeated()
	{
		if (HoldingAttackSlot)
		{
			EnemyCombatCoordinator.ReleaseAttackSlot();
			HoldingAttackSlot = false;
		}

		SaveManager.Instance.MarkCommonEnemyDefeated(PersistenceId);
		SpawnExplosion();
		CallDeferred(MethodName.SpawnLoot);
		QueueFree();
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

	public override void _PhysicsProcess(double delta)
	{
		Vector2 velocity = Velocity;

		if (_knockbackTimer > 0)
		{
			_knockbackTimer -= (float)delta;
			velocity.X = _knockbackVelocity.X;
			velocity.Y = IsOnFloor() ? 0 : velocity.Y + Gravity * (float)delta;
			Velocity = velocity;
			MoveAndSlide();
			return;
		}

		if (!IsOnFloor())
			velocity.Y += Gravity * (float)delta;

		Node2D player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		if (player is not null)
		{
			float distanceX = player.GlobalPosition.X - GlobalPosition.X;
			float absDistance = Mathf.Abs(distanceX);
			PlayerDetected = absDistance <= DetectionRange;
			if (PlayerDetected)
			{
				bool desiredFacingRight = distanceX >= 0;
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

				velocity.X = !_isHoldingDistance && CanChase && CanMoveInDirection(moveSign)
					? moveSign * MoveSpeed
					: Mathf.MoveToward(velocity.X, 0, MoveSpeed);
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
		if (ContactDamageEnabled)
			ApplyContactDamage();
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

			targetStats.TakeDamage(Mathf.RoundToInt(Stats.AttackPower * ContactDamageMultiplier));
		}
	}
}
