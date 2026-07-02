using Godot;

namespace Metroidvania.World;

public partial class Pet : CharacterBody2D
{
	[Export] public float FollowDistance = 60f;
	[Export] public float StopDistance = 30f;
	[Export] public float CatchUpDistance = 220f;
	[Export] public float MoveSpeed = 140f;
	[Export] public float CatchUpSpeedMultiplier = 1.6f;
	[Export] public float Acceleration = 700f;
	[Export] public float Gravity = 900f;

	private Node2D _visual;
	private AnimatedSprite2D _sprite;
	private bool _facingRight = true;
	private bool _isFollowing;

	public override void _Ready()
	{
		_visual = GetNode<Node2D>("Visual");
		_sprite = GetNode<AnimatedSprite2D>("Visual/PetSprite");
	}

	public override void _PhysicsProcess(double delta)
	{
		Vector2 velocity = Velocity;

		if (!IsOnFloor())
			velocity.Y += Gravity * (float)delta;

		bool isCatchingUp = false;
		float targetVelocityX = 0f;

		Node2D player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		if (player is not null)
		{
			float distanceX = player.GlobalPosition.X - GlobalPosition.X;
			float distanceAbs = Mathf.Abs(distanceX);

			// Hysteresis avoids flip-flopping between idle/walk every physics
			// frame when the pet is hovering right at the follow threshold,
			// which was resetting the walk animation back to frame 0 constantly.
			if (_isFollowing)
			{
				if (distanceAbs <= StopDistance)
					_isFollowing = false;
			}
			else if (distanceAbs > FollowDistance)
			{
				_isFollowing = true;
			}

			if (_isFollowing)
			{
				isCatchingUp = distanceAbs > CatchUpDistance;
				float speed = isCatchingUp ? MoveSpeed * CatchUpSpeedMultiplier : MoveSpeed;
				targetVelocityX = Mathf.Sign(distanceX) * speed;
				_facingRight = distanceX >= 0;
			}
		}
		else
		{
			_isFollowing = false;
		}

		velocity.X = Mathf.MoveToward(velocity.X, targetVelocityX, Acceleration * (float)delta);

		_visual.Scale = new Vector2(_facingRight ? 1 : -1, 1);

		Velocity = velocity;
		MoveAndSlide();

		string animation = Mathf.Abs(velocity.X) <= 5f ? "idle" : isCatchingUp ? "run" : "walk";
		_sprite.Play(animation);
	}
}
