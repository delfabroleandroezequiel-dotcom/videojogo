using System.Threading.Tasks;
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
	[Export] public float AttackHitboxReach = 50.6f;
	[Export] public float AttackAnimDuration = 0.5f;
	[Export] public float ComboResetWindow = 0.6f;
	[Export] public int AttackStaminaCost = 10;
	[Export] public float CrouchAttackCooldown = 1f;
	[Export] public float CrouchWeaponTrailYOffset = 30f;
	[Export] public float HealAnimDuration = 0.8f;
	[Export] public float DashSpeed = 560f;
	[Export] public float DashDuration = 0.2f;
	[Export] public float DashIframeDuration = 0.28f;
	[Export] public float DashCooldown = 0.5f;
	[Export] public int DashStaminaCost = 20;
	[Export] public float RunThrustSpeed = 420f;
	[Export] public float RunThrustDuration = 0.3f;
	[Export] public float RunThrustHitboxDelay = 0.05f;
	[Export] public float RunThrustCooldown = 0.6f;
	[Export] public int RunThrustStaminaCost = 15;
	[Export] public Vector2 RunThrustHitboxSize = new(100f, 42f);
	[Export] public float RunThrustSecondHitGap = 0.06f;
	[Export] public float CrouchSpeedMultiplier = 0.5f;
	[Export] public float ParryWindowDuration = 0.2f;
	[Export] public float BlockStaminaCostFraction = 0.7f;
	[Export] public SpriteFrames FireSpriteFrames;
	[Export] public float TransformationInDuration = 1.5f;
	[Export] public float TransformationOutDuration = 0.9f;
	[Export] public float ChargedAttackDuration = 2f;
	[Export] public Vector2 ChargedAttackHitboxSize = new(90f, 50f);
	[Export] public float ChargedAttackReach = 60f;
	[Export] public float PoundSpeed = 500f;
	[Export] public float PoundBounceVelocity = -420f;
	[Export] public Vector2 PoundHitboxSize = new(50f, 70f);
	[Export] public float PoundHitboxReach = 40f;
	[Export] public float PoundWeaponTrailYOffset = 25f;
	[Export] public Vector2 UpAttackHitboxSize = new(46f, 90f);
	[Export] public float UpAttackHitboxReach = 40f;
	[Export] public float UpAttackCooldown = 0.4f;
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
	private AnimatedSprite2D _weaponTrail;
	private float _weaponTrailBaseX;
	private float _weaponTrailBaseY;
	private RectangleShape2D _hitboxShape;
	private Vector2 _hitboxBaseSize;
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
	private bool _isRunThrusting;
	private bool _canRunThrust = true;
	private float _runThrustDirection;
	private bool _canCrouchAttack = true;
	private bool _canUpAttack = true;
	private bool _isBlocking;
	private float _parryWindowTimer;
	private AnimatedSprite2D _parryFlash;
	private SpriteFrames _normalSpriteFrames;
	private bool _isFireImbued;
	private bool _isTransforming;
	private bool _fireImbueKeyReleased = true;
	private bool _isCharging;
	private bool _isPounding;
	private bool _poundHitLanded;
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
		_hitboxShape = GetNode<CollisionShape2D>("AttackHitbox/CollisionShape2D").Shape as RectangleShape2D;
		_hitboxBaseSize = _hitboxShape.Size;
		_stats = GetNode<Stats>("Stats");
		_abilities = GetNode<PlayerAbilities>("Abilities");
		_healFlask = GetNode<HealFlask>("HealFlask");
		_visual = GetNode<Node2D>("Visual");
		_head = GetNode<Node2D>("Visual/Head");
		_weaponPivot = GetNode<Node2D>("Visual/WeaponPivot");
		_weaponTrail = GetNode<AnimatedSprite2D>("Visual/WeaponTrail");
		_weaponTrail.AnimationFinished += () => _weaponTrail.Visible = false;
		_weaponTrailBaseX = _weaponTrail.Position.X;
		_weaponTrailBaseY = _weaponTrail.Position.Y;
		_parryFlash = GetNode<AnimatedSprite2D>("Visual/ParryFlash");
		_parryFlash.AnimationFinished += () => _parryFlash.Visible = false;
		_legLeft = GetNode<Node2D>("Visual/LegLeft");
		_legRight = GetNode<Node2D>("Visual/LegRight");
		_armLeft = GetNode<Node2D>("Visual/ArmLeft");
		_armRight = GetNode<Node2D>("Visual/ArmRight");
		_standCollision = GetNode<CollisionShape2D>("CollisionShape2D");
		_crouchCollision = GetNode<CollisionShape2D>("CrouchCollisionShape2D");
		_camera = GetNode<Camera2D>("Camera2D");
		_sprite = GetNode<AnimatedSprite2D>("Visual/CharacterSprite");
		_normalSpriteFrames = _sprite.SpriteFrames;
		_stats.Died += OnDied;
		_stats.IncomingHitInterceptor = TryBlockIncomingHit;

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
		healthBar.SetRatio((float)_stats.CurrentHealth / _stats.MaxHealth);
		staminaBar.SetRatio((float)_stats.CurrentStamina / _stats.MaxStamina);
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

		if (_isRunThrusting)
		{
			velocity.X = _runThrustDirection * RunThrustSpeed;
			velocity.Y = 0;
			Velocity = velocity;
			MoveAndSlide();
			UpdateAnimation(delta, false);
			return;
		}

		if (_isPounding)
		{
			// The hit itself is detected via a signal fired mid-physics-step (while Godot is
			// still flushing collision queries), so the actual bounce/deactivate is deferred
			// to here — the start of the next physics frame — rather than applied synchronously
			// from inside that signal callback, which Godot's physics server rejects.
			if (_poundHitLanded)
			{
				Velocity = new Vector2(Velocity.X, PoundBounceVelocity);
				_jumpCount = 0;
				_isDoubleJumping = false;
				EndPound();
				UpdateAnimation(delta, false);
				return;
			}

			velocity.X = 0f;
			velocity.Y = PoundSpeed;
			Velocity = velocity;
			MoveAndSlide();
			UpdateAnimation(delta, false);
			if (IsOnFloor())
				EndPound();
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

		bool wasWallClimbing = _isWallClimbing;
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

		// Kill any leftover jump momentum right at the grab instant — otherwise the character
		// keeps rising for a moment before the slide-down eases in, instead of catching in place.
		if (_isWallClimbing && !wasWallClimbing)
			velocity.Y = 0f;

		if (_isWallClimbing)
		{
			float wallNormalX = GetWallNormal().X;
			// No climbing art exists for this pack — only the wall-hang (grab) pose reads
			// correctly, so this is a wall-cling + controlled slide, not free vertical movement.
			velocity.Y = Mathf.MoveToward(velocity.Y, WallSlideSpeed, WallClimbSpeed * (float)delta);
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
			UpdateAnimation(delta, false, velocity.X, false, _isWallClimbing);
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
		UpdateBlock(delta);

		float rawDirection = Input.GetAxis("move_left", "move_right");
		if (rawDirection != 0)
			_facingRight = rawDirection > 0;

		float direction = (_attacking || _healing || _crouching || _isBlocking || _isTransforming) ? 0f : rawDirection;
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

		if (_attackBufferTimer > 0f && !_attacking && !_healing && !_isBlocking && !_isTransforming)
		{
			_attackBufferTimer = 0f;
			bool wantsRunThrust = Input.IsActionPressed("run_thrust") && _abilities.Has(PlayerAbilities.RunThrust)
				&& _canRunThrust && rawDirection != 0;
			bool wantsPound = !IsOnFloor() && Input.IsActionPressed("move_down") && _abilities.Has(PlayerAbilities.PoundAttack);
			bool wantsUpAttack = Input.IsActionPressed("move_up") && _abilities.Has(PlayerAbilities.UpAttack);
			if (wantsPound)
				StartPound();
			else if (wantsRunThrust)
				RunThrust();
			else if (wantsUpAttack)
				UpAttack();
			else if (_crouching)
				CrouchAttack();
			else
				Attack();
		}

		if (Input.IsActionJustPressed("charged_attack") && _abilities.Has(PlayerAbilities.ChargedAttack) && !_isCharging
			&& !_attacking && !_healing && !_isBlocking && !_isTransforming && !_crouching
			&& !_isDashing && !_isRunThrusting)
		{
			StartCharging();
		}

		if (Input.IsActionJustPressed("heal") && !_attacking && !_healing && !_isDashing)
			UseHealFlask();

		// Debounced on release rather than IsActionJustPressed: OS key-repeat while the
		// button is held sends repeated keydown events that Godot treats as fresh presses,
		// which used to re-trigger the toggle mid-transform and corrupt the sprite state.
		bool fireImbuePressed = Input.IsActionPressed("fire_imbue");
		if (!fireImbuePressed)
			_fireImbueKeyReleased = true;

		if (fireImbuePressed && _fireImbueKeyReleased && !_attacking && !_healing && !_isDashing
			&& !_isRunThrusting && !_isTransforming)
		{
			_fireImbueKeyReleased = false;
			ToggleFireImbue();
		}

		Velocity = velocity;
		MoveAndSlide();
		UpdateAnimation(delta, sprinting, velocity.X);
	}

	private void UpdateAnimation(double delta, bool sprinting, float velocityX = 0f, bool isLadderClimbing = false, bool isWallClimbing = false)
	{
		if (_knockbackTimer > 0f)
		{
			_sprite.Play("hurt");
			return;
		}

		if (_healing)
		{
			if (_sprite.Animation != "heal")
				_sprite.Play("heal");
			return;
		}

		if (_isTransforming)
		{
			string transformAnim = _isFireImbued ? "transformation_out" : "transformation";
			if (_sprite.Animation != transformAnim)
				_sprite.Play(transformAnim);
			return;
		}

		if (isLadderClimbing)
		{
			_sprite.Play("ladder_climb");
			_walkPhase += Mathf.Abs(Velocity.Y) * (float)delta * 0.3f;
			float climbSwing = Mathf.Sin(_walkPhase) * 15f;
			_legLeft.RotationDegrees = climbSwing;
			_legRight.RotationDegrees = -climbSwing;
			_armLeft.RotationDegrees = -climbSwing;
			_armRight.RotationDegrees = climbSwing;
			return;
		}

		if (isWallClimbing)
		{
			if (_sprite.Animation != "wall_hang")
				_sprite.Play("wall_hang");
			return;
		}

		if (_attacking)
		{
			if (_sprite.Animation != _currentAttackAnimation)
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
		else if (_crouching)
		{
			if (_sprite.Animation != "crouch")
				_sprite.Play("crouch");
			return;
		}
		else if (_isBlocking)
		{
			if (_sprite.Animation != "idle_block")
				_sprite.Play("idle_block");
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
		bool holdingCrouch = Input.IsActionPressed("move_down");
		// Once crouched, only releasing the key stands you back up — we don't re-check
		// IsOnFloor() every frame here, since a one-frame floor-detection blip (e.g. right
		// after swapping collision shapes) would otherwise pop you back up and down.
		bool wantsCrouch = _crouching ? holdingCrouch : holdingCrouch && IsOnFloor();
		if (wantsCrouch == _crouching)
			return;

		_crouching = wantsCrouch;
		_standCollision.Disabled = _crouching;
		_crouchCollision.Disabled = !_crouching;
	}

	private void UpdateBlock(double delta)
	{
		bool holdingBlock = _abilities.Has(PlayerAbilities.Block) && Input.IsActionPressed("block")
			&& !_attacking && !_healing && !_isDashing && !_isRunThrusting;

		if (holdingBlock && !_isBlocking)
			_parryWindowTimer = ParryWindowDuration;

		_isBlocking = holdingBlock;

		if (_parryWindowTimer > 0f)
			_parryWindowTimer -= (float)delta;
	}

	private bool TryBlockIncomingHit()
	{
		if (!_isBlocking)
			return false;

		if (_parryWindowTimer > 0f)
		{
			_parryFlash.Visible = true;
			_parryFlash.Play("parry_flash");
		}
		else
		{
			_stats.SpendStaminaClamped(Mathf.RoundToInt(_stats.MaxStamina * BlockStaminaCostFraction));
		}

		return true;
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

	private async void StartCharging()
	{
		_isCharging = true;
		_attacking = true;
		_currentAttackAnimation = "idle_block";

		Tween flickerTween = CreateTween().SetLoops();
		flickerTween.TweenProperty(_visual, "modulate:a", 0.35f, 0.15f);
		flickerTween.TweenProperty(_visual, "modulate:a", 1f, 0.15f);

		float heldTime = 0f;
		while (Input.IsActionPressed("charged_attack"))
		{
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			heldTime += (float)GetPhysicsProcessDeltaTime();
		}

		flickerTween.Kill();
		Color modulate = _visual.Modulate;
		_visual.Modulate = new Color(modulate.R, modulate.G, modulate.B, 1f);

		if (heldTime < ChargedAttackDuration)
		{
			// Released before finishing the charge — no attack, just drop back to normal.
			_isCharging = false;
			_attacking = false;
			return;
		}

		_currentAttackAnimation = "attack3";

		bool hitLanded = false;
		void OnHitLanded() => hitLanded = true;
		_hitbox.HitDealt += OnHitLanded;

		_hitboxShape.Size = ChargedAttackHitboxSize;
		_hitbox.Position = new Vector2(_facingRight ? ChargedAttackReach : -ChargedAttackReach, 0);
		_hitbox.Activate(_stats);

		_weaponTrail.Position = new Vector2(_weaponTrailBaseX, _weaponTrailBaseY);
		_weaponTrail.Visible = true;
		_weaponTrail.Play(_isFireImbued ? "thrust_02_fire" : "thrust_02");

		await ToSignal(GetTree().CreateTimer(AttackDuration), SceneTreeTimer.SignalName.Timeout);
		_hitbox.Deactivate();
		_hitbox.HitDealt -= OnHitLanded;
		if (!hitLanded)
			Sfx.Play(this, Sfx.FalloGolpe);

		await ToSignal(GetTree().CreateTimer(0.2f), SceneTreeTimer.SignalName.Timeout);
		_isCharging = false;
		_attacking = false;
	}

	private void StartPound()
	{
		if (!_stats.TrySpendStamina(AttackStaminaCost))
			return;

		_attacking = true;
		_isPounding = true;
		_poundHitLanded = false;
		_currentAttackAnimation = "attack3";

		_hitboxShape.Size = PoundHitboxSize;
		_hitbox.Position = new Vector2(0, PoundHitboxReach);
		_hitbox.Activate(_stats);

		_sprite.Rotation = Mathf.Pi / 2f;

		_weaponTrail.Rotation = Mathf.Pi / 2f;
		_weaponTrail.Position = new Vector2(0, PoundWeaponTrailYOffset);
		_weaponTrail.Visible = true;
		_weaponTrail.Play(WeaponTrailAnimation(_currentAttackAnimation));

		_hitbox.HitDealt += OnPoundHit;
	}

	private void OnPoundHit()
	{
		// Just raise the flag here — _PhysicsProcess applies the bounce and deactivates the
		// hitbox next frame. See the comment at the _isPounding branch for why.
		_poundHitLanded = true;
	}

	private void EndPound()
	{
		if (!_isPounding)
			return;

		_isPounding = false;
		_attacking = false;
		_sprite.Rotation = 0f;
		_weaponTrail.Rotation = 0f;
		_hitbox.Deactivate();
		_hitbox.HitDealt -= OnPoundHit;
	}

	private async void UpAttack()
	{
		if (!_canUpAttack)
			return;

		_canUpAttack = false;
		if (!_stats.TrySpendStamina(AttackStaminaCost))
		{
			_canUpAttack = true;
			return;
		}

		_attacking = true;
		_currentAttackAnimation = "attack1";

		bool hitLanded = false;
		void OnHitLanded() => hitLanded = true;
		_hitbox.HitDealt += OnHitLanded;

		_weaponTrail.Rotation = -Mathf.Pi / 2f;

		if (AttackHitboxDelay > 0f)
			await ToSignal(GetTree().CreateTimer(AttackHitboxDelay), SceneTreeTimer.SignalName.Timeout);

		_hitboxShape.Size = UpAttackHitboxSize;
		_hitbox.Position = new Vector2(0, -UpAttackHitboxReach);
		_hitbox.Activate(_stats);

		_weaponTrail.Position = new Vector2(0, -UpAttackHitboxReach);
		_weaponTrail.Visible = true;
		_weaponTrail.Play(WeaponTrailAnimation(_currentAttackAnimation));

		await ToSignal(GetTree().CreateTimer(AttackDuration), SceneTreeTimer.SignalName.Timeout);
		_hitbox.Deactivate();
		_hitbox.HitDealt -= OnHitLanded;
		if (!hitLanded)
			Sfx.Play(this, Sfx.FalloGolpe);

		_weaponTrail.Rotation = 0f;

		float remainingAnimTime = Mathf.Max(0f, AttackAnimDuration - AttackHitboxDelay - AttackDuration);
		await ToSignal(GetTree().CreateTimer(remainingAnimTime), SceneTreeTimer.SignalName.Timeout);
		_attacking = false;

		await ToSignal(GetTree().CreateTimer(UpAttackCooldown), SceneTreeTimer.SignalName.Timeout);
		_canUpAttack = true;
	}

	private async void RunThrust()
	{
		if (!_stats.TrySpendStamina(RunThrustStaminaCost))
			return;

		_attacking = true;
		_isRunThrusting = true;
		_canRunThrust = false;
		_currentAttackAnimation = "attack3";
		_runThrustDirection = _facingRight ? 1f : -1f;

		bool hitLanded = false;
		void OnHitLanded() => hitLanded = true;
		_hitbox.HitDealt += OnHitLanded;

		if (RunThrustHitboxDelay > 0f)
			await ToSignal(GetTree().CreateTimer(RunThrustHitboxDelay), SceneTreeTimer.SignalName.Timeout);

		_hitboxShape.Size = RunThrustHitboxSize;
		_hitbox.Position = new Vector2(_runThrustDirection * (RunThrustHitboxSize.X / 2f), 0);
		_hitbox.Activate(_stats);
		_weaponTrail.Position = new Vector2(_weaponTrailBaseX, _weaponTrailBaseY);
		_weaponTrail.Visible = true;
		_weaponTrail.Play(_isFireImbued ? "thrust_05_fire" : "thrust_05");

		// Two separate hitbox pulses (with a brief gap in between) so an enemy still standing
		// in the lunge's path when it re-activates takes a second tick of damage, instead of
		// Area2D's body_entered only ever firing once for a target that never left the shape.
		float remainingThrustTime = Mathf.Max(0f, RunThrustDuration - RunThrustHitboxDelay);
		float firstPulseTime = Mathf.Max(0f, remainingThrustTime - RunThrustSecondHitGap) / 2f;
		float secondPulseTime = Mathf.Max(0f, remainingThrustTime - firstPulseTime - RunThrustSecondHitGap);

		await ToSignal(GetTree().CreateTimer(firstPulseTime), SceneTreeTimer.SignalName.Timeout);
		_hitbox.Deactivate();

		if (RunThrustSecondHitGap > 0f)
			await ToSignal(GetTree().CreateTimer(RunThrustSecondHitGap), SceneTreeTimer.SignalName.Timeout);

		_hitbox.Activate(_stats);
		await ToSignal(GetTree().CreateTimer(secondPulseTime), SceneTreeTimer.SignalName.Timeout);
		_hitbox.Deactivate();
		_hitbox.HitDealt -= OnHitLanded;
		if (!hitLanded)
			Sfx.Play(this, Sfx.FalloGolpe);

		_isRunThrusting = false;
		_attacking = false;
		Velocity = new Vector2(0f, Velocity.Y);

		await ToSignal(GetTree().CreateTimer(RunThrustCooldown), SceneTreeTimer.SignalName.Timeout);
		_canRunThrust = true;
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

		_hitboxShape.Size = _hitboxBaseSize;
		_hitbox.Position = new Vector2(_facingRight ? AttackHitboxReach : -AttackHitboxReach, 0);
		_hitbox.Activate(_stats);

		_weaponTrail.Position = new Vector2(_weaponTrailBaseX, _weaponTrailBaseY);
		_weaponTrail.Visible = true;
		_weaponTrail.Play(WeaponTrailAnimation(_currentAttackAnimation));

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

	private async void CrouchAttack()
	{
		if (!_canCrouchAttack)
			return;

		_canCrouchAttack = false;
		await RunSingleAttack("crouch_attack", "slash_vertical", CrouchWeaponTrailYOffset);
		await ToSignal(GetTree().CreateTimer(CrouchAttackCooldown), SceneTreeTimer.SignalName.Timeout);
		_canCrouchAttack = true;
	}

	private async Task RunSingleAttack(string animationName, string trailAnimation, float trailYOffset = 0f)
	{
		if (!_stats.TrySpendStamina(AttackStaminaCost))
			return;

		_attacking = true;
		_currentAttackAnimation = animationName;

		bool hitLanded = false;
		void OnHitLanded() => hitLanded = true;
		_hitbox.HitDealt += OnHitLanded;

		if (AttackHitboxDelay > 0f)
			await ToSignal(GetTree().CreateTimer(AttackHitboxDelay), SceneTreeTimer.SignalName.Timeout);

		_hitboxShape.Size = _hitboxBaseSize;
		_hitbox.Position = new Vector2(_facingRight ? AttackHitboxReach : -AttackHitboxReach, 0);
		_hitbox.Activate(_stats);

		_weaponTrail.Position = new Vector2(_weaponTrailBaseX, _weaponTrailBaseY + trailYOffset);
		_weaponTrail.Visible = true;
		_weaponTrail.Play(trailAnimation);

		await ToSignal(GetTree().CreateTimer(AttackDuration), SceneTreeTimer.SignalName.Timeout);
		_hitbox.Deactivate();
		_hitbox.HitDealt -= OnHitLanded;
		if (!hitLanded)
			Sfx.Play(this, Sfx.FalloGolpe);

		float remainingAnimTime = Mathf.Max(0f, AttackAnimDuration - AttackHitboxDelay - AttackDuration);
		await ToSignal(GetTree().CreateTimer(remainingAnimTime), SceneTreeTimer.SignalName.Timeout);
		_attacking = false;
	}

	private string WeaponTrailAnimation(string attackAnimation) => attackAnimation switch
	{
		"attack2" => _isFireImbued ? "slash_horizontal_09_fire" : "slash_horizontal_09",
		"attack3" => _isFireImbued ? "thrust_01_fire" : "thrust_01",
		_ => _isFireImbued ? "slash_horizontal_fire" : "slash_horizontal",
	};

	private async void UseHealFlask()
	{
		if (!_healFlask.TryUse(_stats))
			return;

		Sfx.Play(this, Sfx.EstusFlask);
		_healing = true;
		await ToSignal(GetTree().CreateTimer(HealAnimDuration), SceneTreeTimer.SignalName.Timeout);
		_healing = false;
	}

	private async void ToggleFireImbue()
	{
		if (_isTransforming)
			return;

		_isTransforming = true;

		if (!_isFireImbued)
		{
			await ToSignal(GetTree().CreateTimer(TransformationInDuration), SceneTreeTimer.SignalName.Timeout);
			_sprite.SpriteFrames = FireSpriteFrames;
			// The node was still holding "transformation" (only valid in the normal set) the
			// instant SpriteFrames swapped out from under it — reset to a name that exists in
			// both sets immediately, instead of leaving it dangling until the next animation update.
			_sprite.Play("idle");
			_isFireImbued = true;
		}
		else
		{
			await ToSignal(GetTree().CreateTimer(TransformationOutDuration), SceneTreeTimer.SignalName.Timeout);
			_sprite.SpriteFrames = _normalSpriteFrames;
			_sprite.Play("idle");
			_isFireImbued = false;
		}

		_isTransforming = false;
	}

	private void OnDied()
	{
		_isDead = true;
		_sprite.Play("death");
		if (GetTree().CurrentScene is LevelBootstrap level)
			DeathScreen.Instance.Show(level.RespawnPlayer);
	}
}
