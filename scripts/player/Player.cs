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
	[Export] public float AttackHitboxDelay = 0.12f;
	[Export] public float AttackDuration = 0.22f;
	[Export] public float AttackHitboxReach = 60f;
	[Export] public float AttackAnimDuration = 0.5f;
	[Export] public float ComboResetWindow = 0.6f;
	[Export] public int AttackStaminaCost = 10;
	[Export] public float HealAnimDuration = 0.8f;
	[Export] public float DashSpeed = 500f;
	[Export] public float DashDuration = 0.2f;
	[Export] public float DashCooldown = 0.5f;
	[Export] public int DashStaminaCost = 20;
	[Export] public float CrouchSpeedMultiplier = 0.5f;
	[Export] public float LookUpOffset = -80f;
	[Export] public float LookDownOffset = 60f;
	[Export] public float LookSmoothSpeed = 4f;
	[Export] public float BaseCameraOffsetY = -108f;
	public float ProfileCameraOffsetY { get; set; }
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
	private HealFlask _healFlask;
	private Label _healChargesLabel;
	private Node2D _visual;
	private Node2D _head;
	private Node2D _weaponPivot;
	private Node2D _legLeft;
	private Node2D _legRight;
	private Node2D _armLeft;
	private Node2D _armRight;
	private float _walkPhase;
	private float _lastStepSin;
	private int _footstepIndex;
	private CollisionShape2D _standCollision;
	private CollisionShape2D _crouchCollision;
	private Camera2D _camera;
	private AnimatedSprite2D _sprite;
	private const int ComboHitCount = 3;

	private bool _facingRight = true;
	private bool _attacking;
	private bool _healing;
	private int _comboStep;
	private float _comboResetTimer;
	private string _currentAttackAnimation = "attack1";
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
		_healFlask = GetNode<HealFlask>("HealFlask");
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
		_healChargesLabel = GetNode<Label>("HUD/VBox/HealChargesLabel");
		_stats.HealthChanged += (current, max) => healthBar.SetRatio((float)current / max);
		_stats.StaminaChanged += (current, max) => staminaBar.SetRatio((float)current / max);
		_stats.HitTaken += (isProjectile) => FlashHit(_visual, new Color(1f, 0.2f, 0.2f));
		_stats.HitTaken += (isProjectile) => Sfx.Play(this, isProjectile ? Sfx.HitRecibidoFlecha : Sfx.HitRecibido);
		_hitbox.HitDealt += () => FlashHit(_visual, new Color(1f, 1f, 0.2f));
		_hitbox.HitDealt += () => Sfx.Play(this, Sfx.HitDado);
		_healFlask.ChargesChanged += (current, max) => UpdateHealChargesLabel(current, max);
		UpdateHealChargesLabel(_healFlask.CurrentCharges, _healFlask.MaxCharges);
		LocaleManager.Instance.LocaleChanged += _ => UpdateHealChargesLabel(_healFlask.CurrentCharges, _healFlask.MaxCharges);
	}

	private void UpdateHealChargesLabel(int current, int max)
	{
		_healChargesLabel.Text = string.Format(TranslationServer.Translate("UI_HUD_HEAL_CHARGES"), current, max);
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

		if (_comboResetTimer > 0f)
		{
			_comboResetTimer -= (float)delta;
			if (_comboResetTimer <= 0f)
				_comboStep = 0;
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

		float direction = (_attacking || _healing) ? 0f : Input.GetAxis("move_left", "move_right");
		bool sprinting = !_attacking && !_healing && _abilities.Has(PlayerAbilities.Sprint) && Input.IsActionPressed("sprint")
			&& !_crouching && direction != 0;
		float speed = _crouching ? Speed * CrouchSpeedMultiplier : sprinting ? Speed * SprintSpeedMultiplier : Speed;
		velocity.X = direction != 0 ? direction * speed : Mathf.MoveToward(velocity.X, 0, speed);

		if (direction != 0)
			_facingRight = direction > 0;

		_visual.Scale = new Vector2(_facingRight ? 1 : -1, 1f);

		UpdateCameraLook(delta);

		if (Input.IsActionJustPressed("dash") && _abilities.Has(PlayerAbilities.Dash) && _canDash && !_healing)
			StartDash();

		if (Input.IsActionJustPressed("attack") && !_attacking && !_healing)
			Attack();

		if (Input.IsActionJustPressed("heal") && !_attacking && !_healing && !_isDashing)
			UseHealFlask();

		Velocity = velocity;
		MoveAndSlide();
		UpdateAnimation(delta, sprinting, velocity.X);
	}

	private void UpdateAnimation(double delta, bool sprinting, float velocityX = 0f, bool isClimbing = false)
	{
		if (_knockbackTimer > 0f)
		{
			_sprite.Play("hurt");
			return;
		}

		if (_healing)
		{
			_sprite.Play("heal");
			return;
		}

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
			_sprite.Play(_currentAttackAnimation);
		}
		else if (_isDashing)
		{
			_sprite.Play("dash");
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
			_lastStepSin = 0f;
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

		float stepSin = Mathf.Sin(_walkPhase);
		if (Mathf.Sign(stepSin) != 0f && Mathf.Sign(stepSin) != Mathf.Sign(_lastStepSin))
		{
			Sfx.Play(this, _footstepIndex == 0 ? Sfx.Paso1 : Sfx.Paso2);
			_footstepIndex = 1 - _footstepIndex;
		}
		_lastStepSin = stepSin;
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

	private void UpdateCameraLook(double delta)
	{
		bool lookingUp = Input.IsActionPressed("ui_up");
		float targetY = BaseCameraOffsetY + ProfileCameraOffsetY + (lookingUp ? LookUpOffset : _crouching ? LookDownOffset : 0f);

		Vector2 targetOffset = new Vector2(0, targetY);
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
		_stats.ExternalInvulnerable = true;

		await ToSignal(GetTree().CreateTimer(DashDuration), SceneTreeTimer.SignalName.Timeout);
		_isDashing = false;
		_stats.ExternalInvulnerable = false;

		await ToSignal(GetTree().CreateTimer(DashCooldown), SceneTreeTimer.SignalName.Timeout);
		_canDash = true;
	}

	private async void Attack()
	{
		if (!_stats.TrySpendStamina(AttackStaminaCost))
			return;

		_attacking = true;
		_currentAttackAnimation = $"attack{_comboStep + 1}";
		_comboStep = (_comboStep + 1) % ComboHitCount;
		_comboResetTimer = 0f;

		bool hitLanded = false;
		void OnHitLanded() => hitLanded = true;
		_hitbox.HitDealt += OnHitLanded;

		_weaponPivot.RotationDegrees = WeaponSwingStartAngle;
		Tween swingTween = GetTree().CreateTween();
		swingTween.TweenProperty(_weaponPivot, "rotation_degrees", WeaponSwingEndAngle, AttackHitboxDelay + AttackDuration);

		if (AttackHitboxDelay > 0f)
			await ToSignal(GetTree().CreateTimer(AttackHitboxDelay), SceneTreeTimer.SignalName.Timeout);

		_hitbox.Position = new Vector2(_facingRight ? AttackHitboxReach : -AttackHitboxReach, 0);
		_hitbox.Activate(_stats);

		await ToSignal(GetTree().CreateTimer(AttackDuration), SceneTreeTimer.SignalName.Timeout);
		_hitbox.Deactivate();
		_hitbox.HitDealt -= OnHitLanded;
		if (!hitLanded)
			Sfx.Play(this, Sfx.FalloGolpe);

		Tween returnTween = GetTree().CreateTween();
		returnTween.TweenProperty(_weaponPivot, "rotation_degrees", WeaponRestAngle, 0.1f);

		float remainingAnimTime = Mathf.Max(0f, AttackAnimDuration - AttackHitboxDelay - AttackDuration);
		await ToSignal(GetTree().CreateTimer(remainingAnimTime), SceneTreeTimer.SignalName.Timeout);
		_attacking = false;
		_comboResetTimer = ComboResetWindow;
	}

	private async void UseHealFlask()
	{
		if (!_healFlask.TryUse(_stats))
			return;

		Sfx.Play(this, Sfx.EstusFlask);
		_healing = true;
		await ToSignal(GetTree().CreateTimer(HealAnimDuration), SceneTreeTimer.SignalName.Timeout);
		_healing = false;
	}

	private void OnDied()
	{
		_isDead = true;
		_sprite.Play("death");
		if (GetTree().CurrentScene is LevelBootstrap level)
			DeathScreen.Instance.Show(level.RespawnPlayer);
	}
}
