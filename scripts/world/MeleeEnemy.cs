using Godot;
using System.Threading.Tasks;
using Metroidvania.Player;

namespace Metroidvania.World;

public partial class MeleeEnemy : Enemy
{
	[Export] public float AttackRange = 40f;
	[Export] public float AttackCooldown = 1f;
	[Export] public float AttackDuration = 0.3f;
	[Export] public float AttackAnimDuration = 0.5f;
	[Export] public float AttackSpriteYOffset = -9f;
	[Export] public float AttackHitboxReach = 24f;

	// Which frame of the "attack" animation actually shows the weapon raised — varies per sprite
	// sheet (WarriorEnemy's is frame 1, OrcEnemy's is also frame 1 — its frame 4 looks like a
	// raised pose in isolation but is actually the recovery pose *after* the swing arc, not the
	// wind-up before it). Set per subclass instance to whichever frame index is the raised pose,
	// and check the actual sheet order, not just how a frame looks on its own.
	[Export] public int TelegraphFrame;

	// Randomizes each cooldown by +/-15% so a room full of the same enemy doesn't swing in
	// mechanical lockstep, and so the player can't metronome-time a dodge against it.
	[Export] public float AttackCooldownJitter = 0.15f;

	// A raised block isn't a free hit — most of the time this enemy holds off instead of
	// swinging straight into a shield, waiting for the block to drop. It still throws an
	// occasional attack anyway so a permanently-turtling player doesn't get a free eternal
	// stalemate, and it never bothers swinging at a dashing (i-framed) player at all.
	[Export] public float BlockEngageChance = 0.25f;

	// When another enemy already holds the shared attack slot (see EnemyCombatCoordinator),
	// this one backs off to this multiple of AttackRange instead of pressing to melee distance
	// and standing there uselessly — reads as "taking its turn" rather than a frozen mob.
	[Export] public float DenialStandoffMultiplier = 1.6f;
	[Export] public float DenialRetryDelay = 0.45f;

	// Exposed protected (not private) so a per-enemy-type subclass (e.g. OrcEnemy) can layer its
	// own commit conditions/telegraph on top of Attack() without duplicating the hitbox/cooldown
	// plumbing every melee enemy shares — see ReadyToCommitAttack and Attack below.
	protected Hitbox AttackHitbox;
	protected bool Attacking;
	protected bool CanAttack = true;
	private float _yieldTimer;

	// Opt-in "smart enemy" toolkit for subclasses that want it (the bandits): player reads updated
	// every frame, a safe evasive hop, a way to attack on demand (punishes, anti-air) with a
	// shorter wind-up, and ledge-safe spacing. Unused by default — enemies that don't call these
	// behave exactly as before.
	protected readonly DashWatcher Dash = new();
	protected readonly WhiffWatcher Whiff = new();
	protected Metroidvania.Player.Player PlayerRef;
	protected bool Hopping;
	// Multiplier a subclass's own wind-up/telegraph should apply to its next Attack() (punish and
	// anti-air attacks come out faster); Attack() implementations read it and reset it to 1.
	protected float WindupScale = 1f;
	private static readonly RandomNumberGenerator JitterRng = new();

	static MeleeEnemy()
	{
		JitterRng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;

		StopDistance = AttackRange * 0.8f;
		AttackHitbox = GetNode<Hitbox>("AttackHitbox");
		Stats.HitTaken += _ => Whiff.NotifyHitTaken();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;

		PlayerRef = PlayerReads.Find(this);
		Dash.Update(PlayerRef, (float)delta);
		Whiff.Update(PlayerRef, (float)delta);

		if (_yieldTimer > 0f)
		{
			_yieldTimer -= (float)delta;
			StopDistance = AttackRange * DenialStandoffMultiplier;
		}
		else
		{
			StopDistance = AttackRange * 0.8f;
		}

		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval)
			return;

		if (Hopping && IsOnFloor() && Velocity.Y >= 0f)
			Hopping = false;

		if (Attacking || !CanAttack || _yieldTimer > 0f || Hopping)
			return;

		Node2D playerNode = GetTree().GetFirstNodeInGroup("player") as Node2D;
		if (playerNode is null)
			return;

		float distanceX = Mathf.Abs(playerNode.GlobalPosition.X - GlobalPosition.X);
		if (distanceX > AttackRange)
			return;

		if (playerNode is Metroidvania.Player.Player player)
		{
			if (player.IsDashing)
				return;

			if (player.IsBlocking && JitterRng.Randf() > BlockEngageChance)
			{
				_yieldTimer = DenialRetryDelay;
				return;
			}
		}

		// Checked before reserving the shared attack slot — a subclass still settling into its
		// own commit condition (e.g. OrcEnemy's SettleDelay) shouldn't hold the slot hostage from
		// whichever other enemy is actually ready to swing right now.
		if (!ReadyToCommitAttack())
			return;

		if (!EnemyCombatCoordinator.TryAcquireAttackSlot())
		{
			_yieldTimer = DenialRetryDelay;
			return;
		}

		HoldingAttackSlot = true;
		_ = Attack();
	}

	// Default: always ready the instant range/dash/block checks pass (this is SpiderEnemy's
	// current behavior, unchanged). Override to add a per-enemy-type extra commit condition —
	// e.g. requiring the enemy to have actually stopped moving for a beat first.
	protected virtual bool ReadyToCommitAttack() => true;

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta) =>
		Hopping ? currentVelocityX : base.ComputeMoveX(player, distanceX, currentVelocityX, delta);

	// Starts an attack right now (reactions: punishes, anti-air), skipping the normal range/commit
	// checks but still respecting the cooldown and the shared attack slot.
	protected bool TryAttackNow(float windupScale = 1f)
	{
		if (Attacking || !CanAttack || Hopping || !IsOnFloor())
			return false;
		if (!EnemyCombatCoordinator.TryAcquireAttackSlot())
			return false;

		HoldingAttackSlot = true;
		WindupScale = windupScale;
		_ = Attack();
		return true;
	}

	// Jumps with launchVelocity only if the arc lands on ground (never into a pit).
	protected bool TryHop(Vector2 launchVelocity, float maxDrop = 40f)
	{
		if (Hopping || Attacking || !IsOnFloor() || !LandsSafely(launchVelocity, maxDrop))
			return false;
		Velocity = launchVelocity;
		Hopping = true;
		return true;
	}

	// Ledge-safe spacing: walk up to stopDistance, back off when closer than 	ooClose, never
	// off a ledge or into a wall.
	protected float SpacingMoveX(float distanceX, float currentVelocityX, float stopDistance, float tooClose, float speedMultiplier = 1f)
	{
		if (Hopping)
			return currentVelocityX;
		if (Attacking || !CanChase)
			return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);

		float absDistance = Mathf.Abs(distanceX);
		float toward = Mathf.Sign(distanceX);
		if (absDistance < tooClose && CanStepTo(-toward))
			return -toward * MoveSpeed * 0.8f * speedMultiplier;
		if (absDistance > stopDistance && CanStepTo(toward))
			return toward * MoveSpeed * speedMultiplier;
		return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
	}

	// jump/fall while airborne, if the sheet has them. Returns whether it took over the animation.
	protected bool PlayAirAnimation(Vector2 velocity)
	{
		if (Sprite is null || IsOnFloor())
			return false;
		string anim = velocity.Y < 0f ? "jump" : "fall";
		if (!Sprite.SpriteFrames.HasAnimation(anim))
			anim = Sprite.SpriteFrames.HasAnimation("jump") ? "jump" : null;
		if (anim is null)
			return false;
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
		return true;
	}

	// Locked for the whole Attacking window — covers the swing itself and, for enemies that wrap
	// Attack() with their own windup (e.g. OrcEnemy), the telegraph too, since both set Attacking
	// true immediately. Spinning to face a player who circled around mid-swing would otherwise
	// visually detach the hit from whatever the animation is actually telegraphing.
	protected override bool CanTurnToFacePlayer => !Attacking;
	protected override bool CanChase => !Attacking;
	protected override bool ContactDamageEnabled => false;

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null) return;
		if (Hopping && PlayAirAnimation(velocity)) return;
		string anim = Attacking ? "attack" : (Mathf.Abs(velocity.X) > 5f ? "run" : "idle");
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	// Freezes an animation on TelegraphFrame (the raised-weapon pose) with AttackSpriteYOffset
	// already applied — shared by every subclass that holds a windup telegraph before its swing
	// (OrcEnemy, WarriorEnemy), so both fixes live in one place instead of being hand-copied per
	// enemy. Without AttackSpriteYOffset, the held pose sits at the idle/run Y offset while showing
	// the (usually taller) attack frame, which reads as the sprite sinking into the ground until the
	// real swing (which does apply it) finally plays. Without explicitly setting Frame after Play(),
	// it defaults to frame 0 — which on every sheet checked so far is the weapon-down/mid-swing
	// pose, not the wind-up, so the telegraph read as "not raised" until this was pinned down.
	// Stop() must come BEFORE the Frame assignment, not after — Stop() resets the animation
	// position to 0, so setting Frame first and stopping second silently threw the chosen
	// TelegraphFrame away and always froze on 0 instead.
	protected void HoldTelegraphFrame(string animationName)
	{
		Sprite.Position = new Vector2(Sprite.Position.X, AttackSpriteYOffset);
		Sprite.Play(animationName);
		Sprite.Stop();
		Sprite.Frame = TelegraphFrame;
	}

	// virtual + Task (not async void) so a subclass can wrap this in its own telegraph/windup and
	// still await the real swing instead of firing both concurrently — see OrcEnemy.Attack.
	protected virtual async Task Attack()
	{
		Attacking = true;
		CanAttack = false;
		Sprite.Position = new Vector2(Sprite.Position.X, AttackSpriteYOffset);
		AttackHitbox.Position = new Vector2(FacingRight ? AttackHitboxReach : -AttackHitboxReach, 0);
		AttackHitbox.Activate(Stats);

		try
		{
			await ToSignal(GetTree().CreateTimer(AttackDuration), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;
			AttackHitbox.Deactivate();

			float remainingAnimTime = Mathf.Max(0f, AttackAnimDuration - AttackDuration);
			await ToSignal(GetTree().CreateTimer(remainingAnimTime), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;
			Attacking = false;
			Sprite.Position = new Vector2(Sprite.Position.X, 0f);
		}
		finally
		{
			EnemyCombatCoordinator.ReleaseAttackSlot();
			HoldingAttackSlot = false;
		}

		float jitter = 1f + JitterRng.RandfRange(-AttackCooldownJitter, AttackCooldownJitter);
		await ToSignal(GetTree().CreateTimer(AttackCooldown * jitter), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			CanAttack = true;
	}
}
