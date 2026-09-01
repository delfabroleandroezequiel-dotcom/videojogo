using Godot;

namespace Metroidvania.World;

public partial class WanderingCritter : CharacterBody2D
{
	[Export] public float RunSpeed = 60f;
	[Export] public float Gravity = 900f;
	[Export] public float MinIdleTime = 2f;
	[Export] public float MaxIdleTime = 5f;
	[Export] public float MinRunTime = 1.5f;
	[Export] public float MaxRunTime = 4f;
	[Export] public float LedgeCheckAheadX = 14f;
	[Export] public float LedgeCheckDownY = 40f;

	// The raycast's origin sits at the critter's feet, right where the floor collider already
	// touches it — starting the ray there means it begins embedded in the floor, and Godot's
	// raycasts ignore a shape they start inside of. Lifting the origin above the feet first
	// guarantees the ray actually crosses the floor surface instead of missing it entirely.
	private const float LedgeCheckClearance = 10f;

	private AnimatedSprite2D _sprite;
	private RayCast2D _ledgeCheck;
	private RandomNumberGenerator _rng = new();
	private bool _isRunning;
	private float _stateTimer;
	private float _moveSign = 1f;

	public override void _Ready()
	{
		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_ledgeCheck = GetNodeOrNull<RayCast2D>("LedgeCheck");
		_rng.Randomize();
		EnterIdle();
	}

	public override void _PhysicsProcess(double delta)
	{
		_stateTimer -= (float)delta;

		Vector2 velocity = Velocity;
		velocity.Y = IsOnFloor() ? 0f : velocity.Y + Gravity * (float)delta;

		if (_isRunning)
		{
			if (_stateTimer <= 0f || !CanMoveInDirection(_moveSign))
				EnterIdle();
			else
				velocity.X = _moveSign * RunSpeed;
		}
		else
		{
			velocity.X = 0f;
			if (_stateTimer <= 0f)
				EnterRun();
		}

		Velocity = velocity;
		MoveAndSlide();

		if (_isRunning && GetSlideCollisionCount() > 0)
			EnterIdle();
	}

	private void EnterIdle()
	{
		_isRunning = false;
		_stateTimer = _rng.RandfRange(MinIdleTime, MaxIdleTime);
		_sprite.Play("idle");
	}

	private void EnterRun()
	{
		_isRunning = true;
		_stateTimer = _rng.RandfRange(MinRunTime, MaxRunTime);
		_moveSign = _rng.Randf() < 0.5f ? -1f : 1f;
		_sprite.Scale = new Vector2(Mathf.Abs(_sprite.Scale.X) * _moveSign, _sprite.Scale.Y);
		_sprite.Play("run");
	}

	private bool CanMoveInDirection(float sign)
	{
		if (_ledgeCheck is null)
			return true;

		_ledgeCheck.Position = new Vector2(sign * LedgeCheckAheadX, -LedgeCheckClearance);
		_ledgeCheck.TargetPosition = new Vector2(0f, LedgeCheckDownY + LedgeCheckClearance);
		_ledgeCheck.ForceRaycastUpdate();
		return _ledgeCheck.IsColliding();
	}
}
