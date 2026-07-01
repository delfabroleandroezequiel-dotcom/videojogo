using Godot;
using Metroidvania.Save;
using Metroidvania.Shared;
using Metroidvania.UI;
using Metroidvania.World;

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
	[Export] public float SprintSpeedMultiplier = 1.6f;
	[Export] public float WalkSwingAmplitudeDeg = 18f;
	[Export] public float RunSwingAmplitudeDeg = 30f;
	[Export] public float WalkCycleSpeed = 8f;
	[Export] public float JumpLegAngleDeg = -15f;
	[Export] public float JumpArmAngleDeg = 20f;
	[Export] public float ClimbSpeed = 130f;
	[Export] public float ClimbHorizontalSpeedMultiplier = 0.6f;
	[Export] public float LadderJumpLockoutDuration = 0.25f;

	private Hitbox _hitbox;
	private Stats _stats;
	private PlayerAbilities _abilities;
	private Node2D _visual;
	private Node2D _head;
	private Node2D _weaponPivot;
	private Node2D _legLeft;
	private Node2D _legRight;
	private Node2D _armLeft;
	private Node2D _armRight;
	private float _walkPhase;
	private CollisionShape2D _standCollision;
	private CollisionShape2D _crouchCollision;
	private Camera2D _camera;
	private AnimatedSprite2D _sprite;
	private bool _facingRight = true;
	private bool _attacking;
	private int _jumpCount;
	private bool _isDoubleJumping;
	private bool _isDashing;
	private bool _canDash = true;
	private bool _crouching;
	private float _dashDirection;
	private float _knockbackTimer;
	private Vector2 _knockbackVelocity;
	private bool _isDead;
	private Ladder _ladder;
	private bool _isClimbing;
	private float _ladderGrabLockout;

	public override void _Ready()
	{
		_hitbox = GetNode<Hitbox>("AttackHitbox");
		_stats = GetNode<Stats>("Stats");
		_abilities = GetNode<PlayerAbilities>("Abilities");
		_visual = GetNode<Node2D>("Visual");
		_head = GetNode<Node2D>("Visual/Head");
		_weaponPivot = GetNode<Node2D>("Visual/WeaponPivot");
		_legLeft = GetNode<Node2D>("Visual/LegLeft");
		_legRight = GetNode<Node2D>("Visual/LegRight");
		_armLeft = GetNode<Node2D>("Visual/ArmLeft");
		_armRight = GetNode<Node2D>("Visual/ArmRight");
		_standCollision = GetNode<CollisionShape2D>("CollisionShape2D");
		_crouchCollision = GetNode<CollisionShape2D>("CrouchCollisionShape2D");
		_camera = GetNode<Camera2D>("Camera2D");
		_sprite = GetNode<AnimatedSprite2D>("Visual/CharacterSprite");
		_stats.Died += OnDied;

		HudBar healthBar = GetNode<HudBar>("HUD/VBox/HealthBar");
		HudBar staminaBar = GetNode<HudBar>("HUD/VBox/StaminaBar");
		_stats.HealthChanged += (current, max) => healthBar.SetRatio((float)current / max);
		_stats.StaminaChanged += (current, max) => staminaBar.SetRatio((float)current / max);
		_stats.HitTaken += () => FlashHit(_visual, new Color(1f, 0.2f, 0.2f));
		_hitbox.HitDealt += () => FlashHit(_visual, new Color(1f, 1f, 0.2f));
	}

	private void FlashHit(Node2D visual, Color flashColor)
	{
		var tween = CreateTween();
		visual.Modulate = flashColor * 2f;
		tween.TweenProperty(visual, "modulate", Colors.White, 0.25f);
	}

	public void ApplyKnockback(Vector2 direction, float force)
	{
		_knockbackVelocity = direction * force;
		_knockbackTimer = KnockbackDuration;
	}

	public void EnterLadder(Ladder ladder)
	{
		_ladder = ladder;
	}

	public void ExitLadder(Ladder ladder)
	{
		if (_ladder != ladder)
			return;

		_ladder = null;
		_isClimbing = false;
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
			UpdateAnimation(delta, false);
			return;
		}

		if (_isDashing)
		{
			velocity.X = _dashDirection * DashSpeed;
			velocity.Y = 0;
			Velocity = velocity;
			MoveAndSlide();
			UpdateAnimation(delta, false);
			return;
		}

		if (_ladderGrabLockout > 0f)
			_ladderGrabLockout -= (float)delta;

		if (_ladder != null)
		{
			if (!_isClimbing && _ladderGrabLockout <= 0f
				&& (Input.IsActionPressed("ui_up") || Input.IsActionPressed("ui_down")))
			{
				_isClimbing = true;
				GlobalPosition = new Vector2(_ladder.GlobalPosition.X, GlobalPosition.Y);
			}
		}
		else
		{
			_isClimbing = false;
		}

		if (_isClimbing)
		{
			float climbInput = (Input.IsActionPressed("ui_down") ? 1f : 0f) - (Input.IsActionPressed("ui_up") ? 1f : 0f);
			velocity.Y = climbInput * ClimbSpeed;

			float climbDirection = Input.GetAxis("move_left", "move_right");
			velocity.X = climbDirection * Speed * ClimbHorizontalSpeedMultiplier;
			if (climbDirection != 0)
				_facingRight = climbDirection > 0;

			_visual.Scale = new Vector2(_facingRight ? 1 : -1, 1f);
			_visual.Position = Vector2.Zero;

			if (Input.IsActionJustPressed("jump"))
			{
				_isClimbing = false;
				_ladderGrabLockout = LadderJumpLockoutDuration;
				velocity.Y = JumpVelocity;
				_jumpCount = 1;
			}

			Velocity = velocity;
			MoveAndSlide();
			UpdateAnimation(delta, false, velocity.X, _isClimbing);
			return;
		}

		if (!IsOnFloor())
			velocity.Y += Gravity * (float)delta;
		else
		{
			_jumpCount = 0;
			_isDoubleJumping = false;
		}

		int maxJumps = _abilities.Has(PlayerAbilities.DoubleJump) ? 2 : 1;
		if (Input.IsActionJustPressed("jump") && (IsOnFloor() || _jumpCount < maxJumps))
		{
			velocity.Y = JumpVelocity;
			_jumpCount++;
			_isDoubleJumping = _jumpCount >= 2;
		}

		UpdateCrouch();

		float direction = Input.GetAxis("move_left", "move_right");
		bool sprinting = _abilities.Has(PlayerAbilities.Sprint) && Input.IsActionPressed("sprint")
			&& !_crouching && direction != 0;
		float speed = _crouching ? Speed * CrouchSpeedMultiplier : sprinting ? Speed * SprintSpeedMultiplier : Speed;
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
		UpdateAnimation(delta, sprinting, velocity.X);
	}

	private void UpdateAnimation(double delta, bool sprinting, float velocityX = 0f, bool isClimbing = false)
	{
		if (isClimbing)
		{
			_sprite.Play("idle");
			_walkPhase += Mathf.Abs(Velocity.Y) * (float)delta * 0.3f;
			float climbSwing = Mathf.Sin(_walkPhase) * 15f;
			_legLeft.RotationDegrees = climbSwing;
			_legRight.RotationDegrees = -climbSwing;
			_armLeft.RotationDegrees = -climbSwing;
			_armRight.RotationDegrees = climbSwing;
			return;
		}

		if (_attacking)
		{
			_sprite.Play("attack");
		}
		else if (_isDashing)
		{
			_sprite.Play("run");
			_legLeft.RotationDegrees = -10f;
			_legRight.RotationDegrees = -14f;
			_armLeft.RotationDegrees = -30f;
			_armRight.RotationDegrees = -30f;
			return;
		}
		else if (!IsOnFloor())
		{
			_sprite.Play(_isDoubleJumping ? "double_jump" : "jump");
			_legLeft.RotationDegrees = JumpLegAngleDeg;
			_legRight.RotationDegrees = JumpLegAngleDeg * 0.6f;
			_armLeft.RotationDegrees = -JumpArmAngleDeg;
			_armRight.RotationDegrees = JumpArmAngleDeg;
			return;
		}

		float speedRatio = Mathf.Abs(velocityX) / Speed;
		if (speedRatio < 0.05f)
		{
			if (!_attacking)
				_sprite.Play("idle");

			float t = (float)delta * 10f;
			_legLeft.RotationDegrees = Mathf.Lerp(_legLeft.RotationDegrees, 0f, t);
			_legRight.RotationDegrees = Mathf.Lerp(_legRight.RotationDegrees, 0f, t);
			_armLeft.RotationDegrees = Mathf.Lerp(_armLeft.RotationDegrees, 0f, t);
			_armRight.RotationDegrees = Mathf.Lerp(_armRight.RotationDegrees, 0f, t);
			_walkPhase = 0f;
			return;
		}

		if (!_attacking)
			_sprite.Play("run");

		float amplitude = sprinting ? RunSwingAmplitudeDeg : WalkSwingAmplitudeDeg;
		float cycleSpeed = WalkCycleSpeed * (sprinting ? 1.6f : 1f) * Mathf.Max(speedRatio, 0.3f);
		_walkPhase += cycleSpeed * (float)delta;

		float swing = Mathf.Sin(_walkPhase) * amplitude;
		_legLeft.RotationDegrees = swing;
		_legRight.RotationDegrees = -swing;
		_armLeft.RotationDegrees = -swing * 0.7f;
		_armRight.RotationDegrees = swing * 0.7f;
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
