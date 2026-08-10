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
	// sheet (WarriorEnemy's is frame 1, OrcEnemy's is frame 4; frame 0 turned out to be the
	// weapon-down/mid-swing pose on both, not the wind-up, despite that being the original
	// assumption). Set per subclass instance to whichever frame index is the raised pose.
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
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;

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

		if (Attacking || !CanAttack || _yieldTimer > 0f)
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
