using Godot;
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

	private Hitbox _hitbox;
	private bool _attacking;
	private bool _canAttack = true;
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
		_hitbox = GetNode<Hitbox>("AttackHitbox");
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

		if (_attacking || !_canAttack || _yieldTimer > 0f)
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

		if (!EnemyCombatCoordinator.TryAcquireAttackSlot())
		{
			_yieldTimer = DenialRetryDelay;
			return;
		}

		Attack();
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null) return;
		string anim = _attacking ? "attack" : (Mathf.Abs(velocity.X) > 5f ? "run" : "idle");
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	private async void Attack()
	{
		_attacking = true;
		_canAttack = false;
		Sprite.Position = new Vector2(Sprite.Position.X, AttackSpriteYOffset);
		_hitbox.Position = new Vector2(FacingRight ? AttackHitboxReach : -AttackHitboxReach, 0);
		_hitbox.Activate(Stats);

		try
		{
			await ToSignal(GetTree().CreateTimer(AttackDuration), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;
			_hitbox.Deactivate();

			float remainingAnimTime = Mathf.Max(0f, AttackAnimDuration - AttackDuration);
			await ToSignal(GetTree().CreateTimer(remainingAnimTime), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;
			_attacking = false;
			Sprite.Position = new Vector2(Sprite.Position.X, 0f);
		}
		finally
		{
			EnemyCombatCoordinator.ReleaseAttackSlot();
		}

		float jitter = 1f + JitterRng.RandfRange(-AttackCooldownJitter, AttackCooldownJitter);
		await ToSignal(GetTree().CreateTimer(AttackCooldown * jitter), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			_canAttack = true;
	}
}
