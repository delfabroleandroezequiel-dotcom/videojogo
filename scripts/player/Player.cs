using Godot;
using Metroidvania.Shared;

namespace Metroidvania.Player;

public partial class Player : CharacterBody2D
{
	[Export] public float Speed = 200f;
	[Export] public float JumpVelocity = -400f;
	[Export] public float Gravity = 900f;
	[Export] public float AttackDuration = 0.15f;
	[Export] public int AttackStaminaCost = 10;
	[Export] public float DashSpeed = 500f;
	[Export] public float DashDuration = 0.2f;
	[Export] public float DashCooldown = 0.5f;
	[Export] public int DashStaminaCost = 20;

	private Hitbox _hitbox;
	private Stats _stats;
	private PlayerAbilities _abilities;
	private bool _facingRight = true;
	private bool _attacking;
	private int _jumpCount;
	private bool _isDashing;
	private bool _canDash = true;
	private float _dashDirection;

	public override void _Ready()
	{
		_hitbox = GetNode<Hitbox>("AttackHitbox");
		_stats = GetNode<Stats>("Stats");
		_abilities = GetNode<PlayerAbilities>("Abilities");
		_stats.Died += OnDied;
	}

	public override void _PhysicsProcess(double delta)
	{
		Vector2 velocity = Velocity;

		if (_isDashing)
		{
			velocity.X = _dashDirection * DashSpeed;
			velocity.Y = 0;
			Velocity = velocity;
			MoveAndSlide();
			return;
		}

		if (!IsOnFloor())
			velocity.Y += Gravity * (float)delta;
		else
			_jumpCount = 0;

		int maxJumps = _abilities.Has(PlayerAbilities.DoubleJump) ? 2 : 1;
		if (Input.IsActionJustPressed("jump") && (IsOnFloor() || _jumpCount < maxJumps))
		{
			velocity.Y = JumpVelocity;
			_jumpCount++;
		}

		float direction = Input.GetAxis("move_left", "move_right");
		velocity.X = direction != 0 ? direction * Speed : Mathf.MoveToward(velocity.X, 0, Speed);

		if (direction != 0)
			_facingRight = direction > 0;

		if (Input.IsActionJustPressed("dash") && _abilities.Has(PlayerAbilities.Dash) && _canDash)
			StartDash();

		if (Input.IsActionJustPressed("attack") && !_attacking)
			Attack();

		Velocity = velocity;
		MoveAndSlide();
	}

	private async void StartDash()
	{
		if (!_stats.TrySpendStamina(DashStaminaCost))
			return;

		_isDashing = true;
		_canDash = false;
		_dashDirection = _facingRight ? 1f : -1f;

		await ToSignal(GetTree().CreateTimer(DashDuration), SceneTreeTimer.SignalName.Timeout);
		_isDashing = false;

		await ToSignal(GetTree().CreateTimer(DashCooldown), SceneTreeTimer.SignalName.Timeout);
		_canDash = true;
	}

	private async void Attack()
	{
		if (!_stats.TrySpendStamina(AttackStaminaCost))
			return;

		_attacking = true;
		_hitbox.Position = new Vector2(_facingRight ? 24 : -24, 0);
		_hitbox.Activate(_stats);
		await ToSignal(GetTree().CreateTimer(AttackDuration), SceneTreeTimer.SignalName.Timeout);
		_hitbox.Deactivate();
		_attacking = false;
	}

	private void OnDied()
	{
		QueueFree();
	}
}
