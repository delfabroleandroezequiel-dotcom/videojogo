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
	[Export] public float DashSpeed = 560f;
	[Export] public float DashDuration = 0.2f;
	[Export] public float DashIframeDuration = 0.28f;
	[Export] public float DashCooldown = 0.5f;
	[Export] public int DashStaminaCost = 20;
	[Export] public float CrouchSpeedMultiplier = 0.5f;
	[Export] public float LookUpOffset = -80f;
	[Export] public float LookDownOffset = 60f;
	[Export] public float LookSmoothSpeed = 4f;
	[Export] public float BaseCameraOffsetY = -108f;
	public float ProfileCameraOffsetY { get; set; }
	public float ProfileZoom { get; set; } = 1.5f;
	[Export] public float BossZoomDistance = 500f;
	[Export] public float BossZoomInMultiplier = 0.75f;
	[Export] public float ZoomSmoothSpeed = 3f;
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
	[Export] public float InputBufferWindow = 0.15f;
	[Export] public float WallClimbSpeed = 110f;
	[Export] public float WallSlideSpeed = 60f;
	[Export] public float WallJumpVelocityX = 260f;
	[Export] public float WallJumpVelocityY = -380f;
	[Export] public float WallJumpLockoutDuration = 0.2f;

	private Hitbox _hitbox;
	private Stats _stats;
	private PlayerAbilities _abilities;
	private HealFlask _healFlask;
	private Label _healChargesLabel;
	private Label _goldLabel;
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
	private bool _isWallClimbing;
	private float _wallJumpLockout;
	private float _attackBufferTimer;
	private float _dashBufferTimer;

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

		if (SaveManager.Instance.SessionCurrentHealth.HasValue)
			_stats.SetCurrentHealth(SaveManager.Instance.SessionCurrentHealth.Value);
		if (SaveManager.Instance.SessionHealCharges.HasValue)
			_healFlask.SetCurrentCharges(SaveManager.Instance.SessionHealCharges.Value);

		_stats.HealthChanged += (current, max) => SaveManager.Instance.SessionCurrentHealth = current;
		_healFlask.ChargesChanged += (current, max) => SaveManager.Instance.SessionHealCharges = current;

		HudBar healthBar = GetNode<HudBar>("HUD/VBox/HealthBar");
		HudBar staminaBar = GetNode<HudBar>("HUD/VBox/StaminaBar");
		_healChargesLabel = GetNode<Label>("HUD/VBox/HealChargesLabel");
		_goldLabel = GetNode<Label>("HUD/VBox/GoldLabel");
		_stats.HealthChanged += (current, max) => healthBar.SetRatio((float)current / max);
		_stats.StaminaChanged += (current, max) => staminaBar.SetRatio((float)current / max);
		_stats.HitTaken += (isProjectile) => FlashHit(_visual, new Color(1f, 0.2f, 0.2f));
		_stats.HitTaken += (isProjectile) => Sfx.Play(this, isProjectile ? Sfx.HitRecibidoFlecha : Sfx.HitRecibido);
		_hitbox.HitDealt += () => FlashHit(_visual, new Color(1f, 1f, 0.2f));
		_hitbox.HitDealt += () => Sfx.Play(this, Sfx.HitDado);
		_healFlask.ChargesChanged += (current, max) => UpdateHealChargesLabel(current, max);
		UpdateHealChargesLabel(_healFlask.CurrentCharges, _healFlask.MaxCharges);
		LocaleManager.Instance.LocaleChanged += _ => UpdateHealChargesLabel(_healFlask.CurrentCharges, _healFlask.MaxCharges);

		SaveManager.Instance.GoldChanged += UpdateGoldLabel;
		UpdateGoldLabel(SaveManager.Instance.Gold);
		LocaleManager.Instance.LocaleChanged += _ => UpdateGoldLabel(SaveManager.Instance.Gold);
	}

	private void UpdateGoldLabel(int gold)
	{
		_goldLabel.Text = string.Format(TranslationServer.Translate("UI_HUD_GOLD"), gold);
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

		_attackBufferTimer = Mathf.Max(0f, _attackBufferTimer - (float)delta);
		_dashBufferTimer = Mathf.Max(0f, _dashBufferTimer - (float)delta);
		if (Input.IsActionJustPressed("attack"))
			_attackBufferTimer = InputBufferWindow;
		if (Input.IsActionJustPressed("dash"))
			_dashBufferTimer = InputBufferWindow;

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
				&& (Input.IsActionPressed("move_up") || Input.IsActionPressed("move_down")))
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
			float climbInput = (Input.IsActionPressed("move_down") ? 1f : 0f) - (Input.IsActionPressed("move_up") ? 1f : 0f);
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

		if (_wallJumpLockout > 0f)
			_wallJumpLockout -= (float)delta;

		if (_abilities.Has(PlayerAbilities.WallClimb) && _wallJumpLockout <= 0f && IsOnWall() && !IsOnFloor())
		{
			float wallNormalX = GetWallNormal().X;
			float wallInputDir = Input.GetAxis("move_left", "move_right");
			bool pressingIntoWall = wallInputDir != 0f && Mathf.Sign(wallInputDir) == -Mathf.Sign(wallNormalX);

			if (pressingIntoWall)
				_isWallClimbing = true;
		}
		else
		{
			_isWallClimbing = false;
		}

		if (_isWallClimbing)
		{
			float wallNormalX = GetWallNormal().X;
			float climbInput = (Input.IsActionPressed("move_down") ? 1f : 0f) - (Input.IsActionPressed("move_up") ? 1f : 0f);
			velocity.Y = climbInput != 0f ? climbInput * WallClimbSpeed : Mathf.MoveToward(velocity.Y, WallSlideSpeed, WallClimbSpeed * (float)delta);
			velocity.X = 0f;

			_facingRight = wallNormalX < 0f;
			_visual.Scale = new Vector2(_facingRight ? 1 : -1, 1f);

			if (Input.IsActionJustPressed("jump"))
			{
				_isWallClimbing = false;
				_wallJumpLockout = WallJumpLockoutDuration;
				velocity.X = wallNormalX * WallJumpVelocityX;
				velocity.Y = WallJumpVelocityY;
				_jumpCount = 1;
			}

			Velocity = velocity;
			MoveAndSlide();
			UpdateAnimation(delta, false, velocity.X, _isWallClimbing);
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

		float rawDirection = Input.GetAxis("move_left", "move_right");
		if (rawDirection != 0)
			_facingRight = rawDirection > 0;

		float direction = (_attacking || _healing) ? 0f : rawDirection;
		bool sprinting = !_attacking && !_healing && _abilities.Has(PlayerAbilities.Sprint) && Input.IsActionPressed("sprint")
			&& !_crouching && direction != 0;
		float speed = _crouching ? Speed * CrouchSpeedMultiplier : sprinting ? Speed * SprintSpeedMultiplier : Speed;
		velocity.X = direction != 0 ? direction * speed : Mathf.MoveToward(velocity.X, 0, speed);

		_visual.Scale = new Vector2(_facingRight ? 1 : -1, 1f);

		UpdateCameraLook(delta);

		if (_dashBufferTimer > 0f && _abilities.Has(PlayerAbilities.Dash) && _canDash && !_healing)
		{
			_dashBufferTimer = 0f;
			StartDash();
		}

		if (_attackBufferTimer > 0f && !_attacking && !_healing)
		{
			_attackBufferTimer = 0f;
			Attack();
		}

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
		bool wantsCrouch = Input.IsActionPressed("move_down") && IsOnFloor();
		if (wantsCrouch == _crouching)
			return;

		_crouching = wantsCrouch;
		_standCollision.Disabled = _crouching;
		_crouchCollision.Disabled = !_crouching;
	}

	private void UpdateCameraLook(double delta)
	{
		bool lookingUp = Input.IsActionPressed("move_up");
		float targetY = BaseCameraOffsetY + ProfileCameraOffsetY + (lookingUp ? LookUpOffset : _crouching ? LookDownOffset : 0f);

		Vector2 targetOffset = new Vector2(0, targetY);
		_camera.Offset = _camera.Offset.Lerp(targetOffset, (float)delta * LookSmoothSpeed);

		float targetHeadAngle = lookingUp ? HeadLookUpAngle : 0f;
		_head.RotationDegrees = Mathf.Lerp(_head.RotationDegrees, targetHeadAngle, (float)delta * LookSmoothSpeed);

		UpdateBossZoom(delta);
	}

	private void UpdateBossZoom(double delta)
	{
		float targetZoom = ProfileZoom;

		Godot.Collections.Array<Node> bosses = GetTree().GetNodesInGroup("boss");
		if (bosses.Count == 1 && bosses[0] is Node2D boss)
		{
			float distance = GlobalPosition.DistanceTo(boss.GlobalPosition);
			float t = Mathf.Clamp(distance / BossZoomDistance, 0f, 1f);
			targetZoom = ProfileZoom * Mathf.Lerp(BossZoomInMultiplier, 1f, t);
		}

		float newZoom = Mathf.Lerp(_camera.Zoom.X, targetZoom, (float)delta * ZoomSmoothSpeed);
		_camera.Zoom = new Vector2(newZoom, newZoom);
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

		float remainingIframeTime = Mathf.Max(0f, DashIframeDuration - DashDuration);
		if (remainingIframeTime > 0f)
			await ToSignal(GetTree().CreateTimer(remainingIframeTime), SceneTreeTimer.SignalName.Timeout);
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
