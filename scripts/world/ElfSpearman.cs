using System.Threading.Tasks;
using Godot;
using Metroidvania.Player;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Elf spearman — shield-and-spear soldier AI (same "read the player, then decide" approach as
// ElfArcher/the casters, built around guarding instead of running away):
//  * Footsies: keeps the player at the tip of its spear (IdealDistance band) — walks up when out
//    of reach, backsteps when the player hugs it (too close for a spear), never off a ledge.
//  * Shield: reacts to the player starting a swing in front of it by raising the shield (not
//    always, and with a human-ish reaction delay). While guarding, hits (and projectiles) from the
//    FRONT are negated through Stats.IncomingHitInterceptor — from behind they land. Every
//    blocked hit costs stamina; running out breaks the guard (stunned, can't guard) — pressure
//    and cross-ups are how you beat it.
//  * Punishes: a blocked hit is answered with a fast counter-thrust; a dash that ends inside its
//    reach gets thrust at right away. If the player turtles behind their own block, it holds its
//    guard and waits a moment before poking anyway.
//  * Thrust: telegraphed wind-up (hit cancels it), damage from attack frame HitStartFrame to
//    HitEndFrame, with a green thrust streak drawn over the spear.
public partial class ElfSpearman : Enemy
{
	[Export] public bool HoldPosition = false;

	[ExportGroup("Spacing")]
	// Centre-to-centre distance it tries to keep (spear tip on the player).
	[Export] public float IdealDistance = 78f;
	[Export] public float IdealBand = 16f;
	// Closer than this the spear is useless — back off (shield up while doing it).
	[Export] public float TooCloseDistance = 42f;
	[Export] public float BackstepSpeedMultiplier = 0.75f;

	[ExportGroup("Thrust")]
	[Export] public float AttackRange = 96f;
	[Export] public float WindupDuration = 0.35f;
	[Export] public float CounterWindupDuration = 0.1f;
	[Export] public float AttackCooldown = 1.2f;
	// Attack sheet: 0 wind-up, 1 spear drawn back, 2-3 full thrust, 4 recovery. The hitbox is live
	// from HitStartFrame through HitEndFrame (inclusive).
	[Export] public int HitStartFrame = 1;
	[Export] public int HitEndFrame = 3;
	[Export] public Vector2 HitboxOffset = new(50f, -15f);
	// Waits at most this long in guard while the player blocks toward it before poking anyway.
	[Export] public float MaxWaitOnPlayerBlock = 0.8f;

	[ExportGroup("Guard")]
	[Export] public float GuardReactionChance = 0.75f;
	[Export] public float GuardReactionDelay = 0.12f;
	[Export] public float GuardHoldMin = 0.45f;
	[Export] public float GuardTriggerDistance = 140f;
	[Export] public int GuardStaminaCostPerBlock = 14;
	[Export] public float GuardBreakStun = 1.2f;
	[Export] public float HurtDuration = 0.3f;

	[ExportGroup("Trail")]
	[Export] public Vector2 TrailFrom = new(8f, -18f);
	[Export] public Vector2 TrailTo = new(82f, -18f);
	[Export] public float TrailExtendDuration = 0.07f;
	[Export] public float TrailHoldDuration = 0.12f;
	[Export] public Color TrailGlowColor = new(0.2f, 1f, 0.35f, 1f);
	[Export] public Color TrailCoreColor = new(0.8f, 1f, 0.85f, 1f);

	private enum State { Neutral, Guarding, Attacking, Stunned }

	private readonly DashWatcher _dash = new();
	private readonly RandomNumberGenerator _rng = new();

	private State _state = State.Neutral;
	private Hitbox _attackHitbox;
	private EnemySlashTrail _trail;
	private float _attackCooldownTimer;
	private float _guardTimer;
	private float _stunTimer;
	private float _hurtTimer;
	private float _pendingGuardTimer = -1f;
	private float _waitOnBlockTimer;
	private bool _playerWasAttacking;
	private bool _counterQueued;
	private bool _cancelAttack;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;

		_rng.Randomize();
		_attackHitbox = GetNode<Hitbox>("AttackHitbox");
		_trail = new EnemySlashTrail
		{
			GlowColor = TrailGlowColor,
			CoreColor = TrailCoreColor,
			CoreWidth = 3f,
			GlowWidth = 9f,
			ZIndex = 1,
		};
		Visual.AddChild(_trail);

		Stats.IncomingHitInterceptor = TryBlockIncomingHit;
		Stats.HitTaken += _ => OnHurt();
		_attackCooldownTimer = _rng.RandfRange(0.3f, 0.8f);
	}

	// ── Guard ───────────────────────────────────────────────────────────────────────────────

	private bool PlayerInFront(Node2D player) => (player.GlobalPosition.X >= GlobalPosition.X) == FacingRight;

	private bool TryBlockIncomingHit()
	{
		if (_state != State.Guarding)
			return false;
		var player = PlayerReads.Find(this);
		if (player is null || !PlayerInFront(player))
			return false;

		Stats.SpendStaminaClamped(GuardStaminaCostPerBlock);
		if (Stats.CurrentStamina <= 0)
		{
			BreakGuard();
			return true;
		}

		Sprite?.Play("block");
		Sfx.PlayAt(this, "Combat/Sword", "Sword Blocked");
		_guardTimer = Mathf.Max(_guardTimer, 0.25f);
		// Answer the blocked hit with a quick counter-thrust once the guard drops.
		if (Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X) <= AttackRange)
			_counterQueued = true;
		return true;
	}

	private void BreakGuard()
	{
		_state = State.Stunned;
		_stunTimer = GuardBreakStun;
		_counterQueued = false;
		Sprite?.Play("hit");
		Tween flash = CreateTween();
		Visual.Modulate = new Color(1.6f, 1.6f, 0.6f);
		flash.TweenProperty(Visual, "modulate", Colors.White, 0.4f);
	}

	private void StartGuard(float minHold)
	{
		if (_state != State.Neutral)
			return;
		_state = State.Guarding;
		_guardTimer = minHold;
		Sprite?.Play("blocknoeffect");
	}

	private void OnHurt()
	{
		_hurtTimer = HurtDuration;
		if (_state == State.Attacking)
			_cancelAttack = true;
		if (_state == State.Guarding)
			_state = State.Neutral;
	}

	// ── Main loop ───────────────────────────────────────────────────────────────────────────

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;

		float dt = (float)delta;
		_attackCooldownTimer -= dt;
		_hurtTimer -= dt;
		var player = PlayerReads.Find(this);
		_dash.Update(player, dt);

		if (_state == State.Stunned)
		{
			_stunTimer -= dt;
			if (_stunTimer <= 0f)
				_state = State.Neutral;
		}

		if (player is not null)
			Think(player, dt);

		base._PhysicsProcess(delta);
	}

	private void Think(Metroidvania.Player.Player player, float dt)
	{
		float distanceX = player.GlobalPosition.X - GlobalPosition.X;
		float absDistance = Mathf.Abs(distanceX);
		bool playerStartedSwing = player.IsAttacking && !_playerWasAttacking;
		_playerWasAttacking = player.IsAttacking;

		// Reactive guard: the player winds up a swing in front of us → maybe raise the shield,
		// after a short reaction delay (never frame-perfect).
		if (playerStartedSwing && _state == State.Neutral && _hurtTimer <= 0f && absDistance <= GuardTriggerDistance
			&& PlayerInFront(player) && Stats.CurrentStamina > GuardStaminaCostPerBlock && _rng.Randf() < GuardReactionChance)
		{
			_pendingGuardTimer = GuardReactionDelay;
		}
		if (_pendingGuardTimer >= 0f)
		{
			_pendingGuardTimer -= dt;
			if (_pendingGuardTimer < 0f)
				StartGuard(GuardHoldMin);
		}

		if (_state == State.Guarding)
		{
			_guardTimer -= dt;
			bool threatOngoing = player.IsAttacking && absDistance <= GuardTriggerDistance;
			if (_guardTimer <= 0f && !threatOngoing)
			{
				_state = State.Neutral;
				if (_counterQueued && absDistance <= AttackRange)
				{
					_counterQueued = false;
					TryStartAttack(CounterWindupDuration, ignoreCooldown: true);
				}
			}
			return;
		}

		if (_state != State.Neutral || _hurtTimer > 0f || !PlayerDetected)
			return;

		// Dash punish: their dash just ended inside our reach.
		if (_dash.SinceDashEnded < 0.1f && absDistance <= AttackRange && PlayerInFront(player))
		{
			TryStartAttack(CounterWindupDuration, ignoreCooldown: true);
			return;
		}

		if (absDistance > AttackRange || _attackCooldownTimer > 0f || !IsOnFloor())
			return;

		// Player turtling behind their own block: hold our guard a moment instead of poking the shield.
		if (PlayerReads.IsBlockingToward(player, GlobalPosition.X) && _waitOnBlockTimer < MaxWaitOnPlayerBlock)
		{
			_waitOnBlockTimer += dt;
			StartGuard(0.2f);
			return;
		}
		_waitOnBlockTimer = 0f;

		TryStartAttack(WindupDuration, ignoreCooldown: false);
	}

	// ── Movement ────────────────────────────────────────────────────────────────────────────

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (HoldPosition || _state is State.Attacking or State.Stunned || _hurtTimer > 0f)
			return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);

		float absDistance = Mathf.Abs(distanceX);
		float toward = Mathf.Sign(distanceX);
		float speedMultiplier = _state == State.Guarding ? 0.4f : 1f;

		if (absDistance < TooCloseDistance && CanStepTo(-toward))
			return -toward * MoveSpeed * BackstepSpeedMultiplier * speedMultiplier;
		if (absDistance > IdealDistance + IdealBand && CanStepTo(toward))
			return toward * MoveSpeed * speedMultiplier;
		return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
	}

	protected override bool CanTurnToFacePlayer => _state is State.Neutral or State.Guarding;

	// ── Thrust ──────────────────────────────────────────────────────────────────────────────

	private void TryStartAttack(float windup, bool ignoreCooldown)
	{
		if (_state != State.Neutral || (!ignoreCooldown && _attackCooldownTimer > 0f))
			return;
		if (!EnemyCombatCoordinator.TryAcquireAttackSlot())
		{
			_attackCooldownTimer = 0.25f;
			return;
		}
		HoldingAttackSlot = true;
		_ = Thrust(windup);
	}

	private async Task Thrust(float windup)
	{
		_state = State.Attacking;
		_cancelAttack = false;
		var player = PlayerReads.Find(this);
		if (player is not null)
		{
			FacingRight = player.GlobalPosition.X >= GlobalPosition.X;
			Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);
		}

		// Telegraph: hold the wind-up pose.
		Sprite?.Play("attack");
		Sprite?.Pause();
		Sprite.Frame = 0;
		if (!await Wait(windup))
			return;

		float fps = (float)Sprite.SpriteFrames.GetAnimationSpeed("attack");
		Sprite.Play("attack");

		// Reach HitStartFrame (the wind-up can still be cancelled by a hit until then).
		if (!await Wait(HitStartFrame / fps))
			return;

		_attackHitbox.Position = new Vector2(FacingRight ? HitboxOffset.X : -HitboxOffset.X, HitboxOffset.Y);
		_attackHitbox.Activate(Stats);
		_trail.PlayThrust(TrailFrom, TrailTo, TrailExtendDuration, TrailHoldDuration);
		Sfx.PlayAt(this, "Combat/Sword", "Sword Attack");

		// Committed from here on — the thrust finishes even if it gets hit.
		_cancelAttack = false;
		await ToSignal(GetTree().CreateTimer((HitEndFrame - HitStartFrame + 1) / fps), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this))
			return;
		_attackHitbox.Deactivate();
		if (IsQueuedForRemoval)
			return;

		int framesLeft = Sprite.SpriteFrames.GetFrameCount("attack") - (HitEndFrame + 1);
		await ToSignal(GetTree().CreateTimer(Mathf.Max(0, framesLeft) / fps), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		EndAttack(AttackCooldown * _rng.RandfRange(0.85f, 1.2f));
	}

	private async Task<bool> Wait(float seconds)
	{
		float elapsed = 0f;
		do
		{
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return false;
			if (_cancelAttack)
			{
				EndAttack(AttackCooldown * 0.5f);
				return false;
			}
			elapsed += (float)GetPhysicsProcessDeltaTime();
		} while (elapsed < seconds);
		return true;
	}

	private void EndAttack(float cooldown)
	{
		_attackHitbox.Deactivate();
		_cancelAttack = false;
		if (_state == State.Attacking)
			_state = State.Neutral;
		_attackCooldownTimer = cooldown;
		if (HoldingAttackSlot)
		{
			EnemyCombatCoordinator.ReleaseAttackSlot();
			HoldingAttackSlot = false;
		}
	}

	// ── Animation ───────────────────────────────────────────────────────────────────────────

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null || _state is State.Attacking or State.Guarding or State.Stunned)
			return;

		string anim;
		if (_hurtTimer > 0f)
			anim = "hit";
		else if (!IsOnFloor())
			anim = velocity.Y < 0f ? "jump" : "fall";
		else if (Mathf.Abs(velocity.X) > 5f)
			anim = Mathf.Abs(velocity.X) > MoveSpeed * 0.8f ? "run" : "walk";
		else
			anim = "idle";

		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	protected override bool ContactDamageEnabled => false;
}
