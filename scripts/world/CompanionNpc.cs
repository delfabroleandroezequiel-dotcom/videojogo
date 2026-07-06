using Godot;
using Metroidvania.Player;
using Metroidvania.Shared;
using Metroidvania.UI;

namespace Metroidvania.World;

public partial class CompanionNpc : CharacterBody2D
{
	[Export] public float FollowDistance = 60f;
	[Export] public float StopDistance = 30f;
	[Export] public float CatchUpDistance = 220f;
	[Export] public float TeleportDistance = 600f;
	[Export] public float StuckDuration = 1.5f;
	[Export] public float MoveSpeed = 140f;
	[Export] public float CatchUpSpeedMultiplier = 1.6f;
	[Export] public float Acceleration = 700f;
	[Export] public float Gravity = 900f;
	[Export] public float DetectionRange = 260f;
	[Export] public float AttackRange = 70f;
	[Export] public float AttackCooldown = 1f;
	[Export] public float AttackDuration = 0.3f;
	[Export] public float AttackAnimDuration = 0.5f;
	[Export] public float AttackSpriteYOffset = -9f;
	[Export] public bool Aggressive = false;

	private Node2D _visual;
	private AnimatedSprite2D _sprite;
	private Hitbox _hitbox;
	private Stats _stats;

	private bool _facingRight = true;
	private bool _isFollowing;
	private float _stuckTimer;
	private bool _attacking;
	private bool _canAttack = true;

	public override void _Ready()
	{
		_visual = GetNode<Node2D>("Visual");
		_sprite = GetNodeOrNull<AnimatedSprite2D>("Visual/CharacterSprite");
		_hitbox = GetNode<Hitbox>("AttackHitbox");
		_stats = GetNode<Stats>("Stats");
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!@event.IsActionPressed("companion_stance"))
			return;

		Aggressive = !Aggressive;
		ZoneTitle.Instance.Show(Aggressive ? "UI_COMPANION_AGGRESSIVE" : "UI_COMPANION_PASSIVE");
	}

	public override void _PhysicsProcess(double delta)
	{
		Vector2 velocity = Velocity;
		if (!IsOnFloor())
			velocity.Y += Gravity * (float)delta;

		Node2D player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		if (player is null)
		{
			Velocity = velocity;
			MoveAndSlide();
			return;
		}

		float toPlayerX = player.GlobalPosition.X - GlobalPosition.X;
		float toPlayerAbs = Mathf.Abs(toPlayerX);

		if (toPlayerAbs > TeleportDistance)
		{
			SnapToPlayer(player);
			return;
		}

		Node2D target = _attacking ? null : (Aggressive ? FindNearestEnemy() : null);
		float targetVelocityX;
		bool isCatchingUp = false;

		if (target is not null)
		{
			float toTargetX = target.GlobalPosition.X - GlobalPosition.X;
			_facingRight = toTargetX >= 0;

			if (Mathf.Abs(toTargetX) <= AttackRange)
			{
				targetVelocityX = 0f;
				if (_canAttack)
					Attack();
			}
			else
			{
				targetVelocityX = Mathf.Sign(toTargetX) * MoveSpeed;
			}
		}
		else if (_attacking)
		{
			targetVelocityX = 0f;
		}
		else
		{
			if (_isFollowing)
			{
				if (toPlayerAbs <= StopDistance)
					_isFollowing = false;
			}
			else if (toPlayerAbs > FollowDistance)
			{
				_isFollowing = true;
			}

			if (_isFollowing)
			{
				isCatchingUp = toPlayerAbs > CatchUpDistance;
				float speed = isCatchingUp ? MoveSpeed * CatchUpSpeedMultiplier : MoveSpeed;
				targetVelocityX = Mathf.Sign(toPlayerX) * speed;
				_facingRight = toPlayerX >= 0;
			}
			else
			{
				targetVelocityX = 0f;
			}
		}

		bool tryingToMove = Mathf.Abs(targetVelocityX) > 1f;
		if (tryingToMove && IsOnFloor() && Mathf.Abs(velocity.X) < 5f)
			_stuckTimer += (float)delta;
		else
			_stuckTimer = 0f;

		if (_stuckTimer >= StuckDuration)
		{
			SnapToPlayer(player);
			return;
		}

		velocity.X = Mathf.MoveToward(velocity.X, targetVelocityX, Acceleration * (float)delta);
		_visual.Scale = new Vector2(_facingRight ? 1 : -1, 1);

		Velocity = velocity;
		MoveAndSlide();

		UpdateAnimation(velocity, isCatchingUp);
	}

	private Node2D FindNearestEnemy()
	{
		Node2D nearest = null;
		float nearestDistance = DetectionRange;

		foreach (Node node in GetTree().GetNodesInGroup("enemy"))
		{
			if (node is not Node2D enemy)
				continue;

			float distance = enemy.GlobalPosition.DistanceTo(GlobalPosition);
			if (distance < nearestDistance)
			{
				nearest = enemy;
				nearestDistance = distance;
			}
		}

		return nearest;
	}

	private void SnapToPlayer(Node2D player)
	{
		float side = _facingRight ? -1f : 1f;
		GlobalPosition = player.GlobalPosition + new Vector2(side * StopDistance, 0f);
		Velocity = Vector2.Zero;
		_stuckTimer = 0f;
		_isFollowing = false;
	}

	private void UpdateAnimation(Vector2 velocity, bool isCatchingUp)
	{
		if (_sprite is null) return;
		string anim = _attacking ? "attack" : (Mathf.Abs(velocity.X) > 5f ? "run" : "idle");
		if (_sprite.Animation != anim)
			_sprite.Play(anim);
	}

	private async void Attack()
	{
		_attacking = true;
		_canAttack = false;
		if (_sprite is not null)
			_sprite.Position = new Vector2(_sprite.Position.X, AttackSpriteYOffset);
		_hitbox.Position = new Vector2(_facingRight ? 24 : -24, 0);
		_hitbox.Activate(_stats);

		await ToSignal(GetTree().CreateTimer(AttackDuration), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this))
			return;
		_hitbox.Deactivate();

		float remainingAnimTime = Mathf.Max(0f, AttackAnimDuration - AttackDuration);
		await ToSignal(GetTree().CreateTimer(remainingAnimTime), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this))
			return;
		_attacking = false;
		if (_sprite is not null)
			_sprite.Position = new Vector2(_sprite.Position.X, 0f);

		await ToSignal(GetTree().CreateTimer(AttackCooldown), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			_canAttack = true;
	}
}
