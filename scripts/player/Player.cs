using Godot;
using Metroidvania.Save;
using Metroidvania.Shared;
using Metroidvania.UI;

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
	[Export] public float CrouchSpeedMultiplier = 0.5f;
	[Export] public float LookUpOffset = -80f;
	[Export] public float LookSmoothSpeed = 4f;
	[Export] public float HeadLookUpAngle = -25f;
	[Export] public float WeaponRestAngle = -20f;
	[Export] public float WeaponSwingStartAngle = -70f;
	[Export] public float WeaponSwingEndAngle = 60f;
	[Export] public float KnockbackDuration = 0.2f;
	[Export] public float FallDeathY = 700f;

	private Hitbox _hitbox;
	private Stats _stats;
	private PlayerAbilities _abilities;
	private Node2D _visual;
	private Node2D _head;
	private Node2D _weaponPivot;
	private CollisionShape2D _standCollision;
	private CollisionShape2D _crouchCollision;
	private Camera2D _camera;
	private bool _facingRight = true;
	private bool _attacking;
	private int _jumpCount;
	private bool _isDashing;
	private bool _canDash = true;
	private bool _crouching;
	private float _dashDirection;
	private float _knockbackTimer;
	private Vector2 _knockbackVelocity;
	private bool _isDead;

	public override void _Ready()
	{
		_hitbox = GetNode<Hitbox>("AttackHitbox");
		_stats = GetNode<Stats>("Stats");
		_abilities = GetNode<PlayerAbilities>("Abilities");
		_visual = GetNode<Node2D>("Visual");
		_head = GetNode<Node2D>("Visual/Head");
		_weaponPivot = GetNode<Node2D>("Visual/WeaponPivot");
		_standCollision = GetNode<CollisionShape2D>("CollisionShape2D");
		_crouchCollision = GetNode<CollisionShape2D>("CrouchCollisionShape2D");
		_camera = GetNode<Camera2D>("Camera2D");
		_stats.Died += OnDied;

		StatBar healthBar = GetNode<StatBar>("HealthBar");
		StatBar staminaBar = GetNode<StatBar>("StaminaBar");
		_stats.HealthChanged += (current, max) => healthBar.SetRatio((float)current / max);
		_stats.StaminaChanged += (current, max) => staminaBar.SetRatio((float)current / max);
	}

	public void ApplyKnockback(Vector2 direction, float force)
	{
		_knockbackVelocity = direction * force;
		_knockbackTimer = KnockbackDuration;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_isDead)
			return;

		if (GlobalPosition.Y > FallDeathY)
		{
			_stats.Kill();
			return;
		}

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

		UpdateCrouch();

		float speed = _crouching ? Speed * CrouchSpeedMultiplier : Speed;
		float direction = Input.GetAxis("move_left", "move_right");
		velocity.X = direction != 0 ? direction * speed : Mathf.MoveToward(velocity.X, 0, speed);

		if (direction != 0)
			_facingRight = direction > 0;

		_visual.Scale = new Vector2(_facingRight ? 1 : -1, _crouching ? 0.7f : 1f);
		_visual.Position = new Vector2(0, _crouching ? 13 : 0);

		UpdateLookUp(delta);

		if (Input.IsActionJustPressed("dash") && _abilities.Has(PlayerAbilities.Dash) && _canDash)
			StartDash();

		if (Input.IsActionJustPressed("attack") && !_attacking)
			Attack();

		Velocity = velocity;
		MoveAndSlide();
	}

	private void UpdateCrouch()
	{
		bool wantsCrouch = Input.IsActionPressed("ui_down") && IsOnFloor();
		if (wantsCrouch == _crouching)
			return;

		_crouching = wantsCrouch;
		_standCollision.Disabled = _crouching;
		_crouchCollision.Disabled = !_crouching;
	}

	private void UpdateLookUp(double delta)
	{
		bool lookingUp = Input.IsActionPressed("ui_up");

		Vector2 targetOffset = lookingUp ? new Vector2(0, LookUpOffset) : Vector2.Zero;
		_camera.Offset = _camera.Offset.Lerp(targetOffset, (float)delta * LookSmoothSpeed);

		float targetHeadAngle = lookingUp ? HeadLookUpAngle : 0f;
		_head.RotationDegrees = Mathf.Lerp(_head.RotationDegrees, targetHeadAngle, (float)delta * LookSmoothSpeed);
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

		_weaponPivot.RotationDegrees = WeaponSwingStartAngle;
		Tween swingTween = GetTree().CreateTween();
		swingTween.TweenProperty(_weaponPivot, "rotation_degrees", WeaponSwingEndAngle, AttackDuration);

		await ToSignal(GetTree().CreateTimer(AttackDuration), SceneTreeTimer.SignalName.Timeout);
		_hitbox.Deactivate();
		_attacking = false;

		Tween returnTween = GetTree().CreateTween();
		returnTween.TweenProperty(_weaponPivot, "rotation_degrees", WeaponRestAngle, 0.1f);
	}

	private void OnDied()
	{
		_isDead = true;
		if (GetTree().CurrentScene is LevelBootstrap level)
			level.RespawnPlayer();
	}
}
