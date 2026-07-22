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
	[Export] public float AttackHitboxYOffset = -12f;

	// The combo's third hit (the thrust) actually lands 3 separate hits, not one — the hitbox
	// re-activates 3 times, each landing its own hit-stop freeze pulse, so it reads as a
	// "thunk-thunk-thunk" flurry rather than a single stab. Each pulse briefly runs at normal
	// speed BEFORE freezing: Area2D overlap detection happens during _physics_process, and
	// dropping TimeScale immediately would starve that step of simulated delta, so the hit could
	// silently fail to register before the freeze even ends. The freeze timers explicitly ignore
	// Engine.TimeScale — otherwise a timer measuring the freeze's own duration would itself get
	// slowed down by the freeze it's producing.
	[Export] public float HitStopDetectWindow = 0.02f;
	[Export] public float HitStopFreezeScale = 0.02f;
	[Export] public float HitStopFreezeDuration = 0.07f;
	[Export] public float HitStopGapDuration = 0.03f;
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
	[Export] public Vector2 RunThrustHitboxSize = new(105f, 44.1f);
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

	// Read-only combat state for enemy AI to react to (see EnemyCombatCoordinator) — an enemy
	// that can tell whether the player is blocking, mid-swing, or dashing can make an actual
	// tactical choice instead of just swinging whenever it's in range.
	public bool IsBlocking => _isBlocking;
	public bool IsAttacking => _attacking;
	public bool IsDashing => _isDashing;
	[Export] public float BossZoomDistance = 500f;
	[Export] public float BossZoomInMultiplier = 0.75f;
	[Export] public float ZoomSmoothSpeed = 3f;
	[Export] public float HeadLookUpAngle = -25f;
	[Export] public float WeaponRestAngle = -20f;
	[Export] public float WeaponSwingStartAngle = -70f;
	[Export] public float WeaponSwingEndAngle = 60f;
	[Export] public float KnockbackDuration = 0.2f;
	[Export] public float FallDeathY = 700f;
	[Export] public float MaxAirborneTime = 15f;
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
	[Export] public float LedgeGrabOffsetX = 16f;
	[Export] public float LedgeSurfaceProbeHeight = 70f;
	[Export] public float LedgeHangDropY = 26f;
	[Export] public float LedgeClimbForwardX = 26f;
	[Export] public float LedgeClimbDuration = 0.35f;
	[Export] public float LedgeGrabLockoutDuration = 0.3f;
	[Export] public float LedgeAutoClimbDelay = 0.15f;
	[Export] public float DropThroughDuration = 0.3f;
	[Export] public float DropThroughLedgeLockoutDuration = 0.5f;

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
	private Vector2 _lookOffset;
	private float _shakeTimer;
	private float _shakeDuration;
	private float _shakeStrength;
	private AnimatedSprite2D _sprite;
	private PointLight2D _debugFlashlight;
	private const int ComboHitCount = 3;

	// The third combo hit (the multi-pulse thrust finisher) is gated behind an ability — until
	// unlocked, the combo just cycles attack1/attack2 and never reaches "attack3" at all.
	private int EffectiveComboHitCount => _abilities.Has(PlayerAbilities.ComboFinisher) ? ComboHitCount : ComboHitCount - 1;

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
	// The attack element cycles Normal -> Fire -> Ice -> Poison -> Lightning -> Normal. Fire is
	// the only one with a full alternate spriteframes transformation right now (see
	// ToggleFireImbue/_isFireImbued); the other elements just tag outgoing hits for the
	// Stats weakness multiplier until they get their own art.
	private DamageElement _currentElement = DamageElement.Normal;
	private bool _isCharging;
	private AnimatedSprite2D _chargeEffect;
	private AnimatedSprite2D _chargeReadyEffect;
	private bool _isPounding;
	private bool _poundHitLanded;
	private float _knockbackTimer;
	private Vector2 _knockbackVelocity;
	private bool _isDead;
	private float _airborneTimer;
	private Ladder _ladder;
	private bool _isClimbing;
	private float _ladderGrabLockout;
	private Rope _ropeInZone;
	private Rope _rope;
	private bool _isSwinging;
	// The rope just jumped off of — refusing to re-grab specifically this one (rather than just
	// waiting out a timer) means it doesn't matter how slow the release velocity happens to be at
	// that point in the swing; you're only blocked from THIS rope, and only until you've actually
	// left its grab zone once, so aiming for a neighboring rope is never held up by it.
	private Rope _lastReleasedRope;
	private bool _isWallClimbing;
	private float _wallJumpLockout;
	private ShapeCast2D _ledgeWallCheck;
	private bool _isLedgeHanging;
	private bool _isLedgeClimbing;
	private float _ledgeClimbTimer;
	private float _ledgeGrabLockout;
	private float _ledgeHangTimer;
	private Vector2 _ledgeClimbStartPosition;
	private Vector2 _ledgeClimbEndPosition;
	private float _ledgeSurfaceY;
	private float _standHalfHeight;
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
		_chargeEffect = GetNode<AnimatedSprite2D>("Visual/ChargeEffect");
		_chargeReadyEffect = GetNode<AnimatedSprite2D>("Visual/ChargeReadyEffect");
		_legLeft = GetNode<Node2D>("Visual/LegLeft");
		_legRight = GetNode<Node2D>("Visual/LegRight");
		_armLeft = GetNode<Node2D>("Visual/ArmLeft");
		_armRight = GetNode<Node2D>("Visual/ArmRight");
		_standCollision = GetNode<CollisionShape2D>("CollisionShape2D");
		_crouchCollision = GetNode<CollisionShape2D>("CrouchCollisionShape2D");
		_ledgeWallCheck = GetNode<ShapeCast2D>("LedgeWallCheck");
		_standHalfHeight = ((RectangleShape2D)_standCollision.Shape).Size.Y / 2f;
		_camera = GetNode<Camera2D>("Camera2D");
		_sprite = GetNode<AnimatedSprite2D>("Visual/CharacterSprite");
		_debugFlashlight = GetNode<PointLight2D>("DebugFlashlight");
		if (!OS.HasFeature("debug"))
		{
			_debugFlashlight.QueueFree();
			_debugFlashlight = null;
		}
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

		if (_isLedgeHanging || _isLedgeClimbing)
		{
			_isLedgeHanging = false;
			_isLedgeClimbing = false;
			_ledgeGrabLockout = LedgeGrabLockoutDuration;
		}
	}

	private bool IsTouchingClimbableWall(out float wallNormalX)
	{
		for (int i = 0; i < GetSlideCollisionCount(); i++)
		{
			KinematicCollision2D collision = GetSlideCollision(i);
			if (collision.GetCollider() is not CollisionObject2D body)
				continue;
			if ((body.CollisionLayer & PhysicsLayers.ClimbableWalls) == 0)
				continue;

			Vector2 normal = collision.GetNormal();
			if (Mathf.Abs(normal.Y) > Mathf.Abs(normal.X))
				continue; // floor/ceiling-like normal, not a wall

			wallNormalX = normal.X;
			return true;
		}

		wallNormalX = 0f;
		return false;
	}

	private bool TryDropThroughOneWayPlatform()
	{
		for (int i = 0; i < GetSlideCollisionCount(); i++)
		{
			KinematicCollision2D collision = GetSlideCollision(i);
			if (collision.GetCollider() is not CollisionObject2D body)
				continue;
			if ((body.CollisionLayer & PhysicsLayers.OneWayPlatforms) == 0)
				continue;

			Vector2 normal = collision.GetNormal();
			if (normal.Y > -0.5f)
				continue; // not standing on top of it

			if (body is not Node bodyNode)
				continue;

			foreach (Node child in bodyNode.GetChildren())
			{
				if (child is CollisionShape2D shape)
					DisableShapeTemporarily(shape);
			}

			// The same platform is often tagged both OneWayPlatforms and ClimbableWalls (cave
			// floors double as ledges), so without this the auto ledge-grab/wall-climb would just
			// catch the player again the instant the shape re-enables, undoing the drop.
			_ledgeGrabLockout = DropThroughLedgeLockoutDuration;
			_wallJumpLockout = DropThroughLedgeLockoutDuration;

			return true;
		}

		return false;
	}

	private async void DisableShapeTemporarily(CollisionShape2D shape)
	{
		shape.Disabled = true;
		await ToSignal(GetTree().CreateTimer(DropThroughDuration), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(shape))
			shape.Disabled = false;
	}

	// Ledge detection is two steps rather than a fixed two-raycast band: first find ANY wall in
	// front near chest height (forgiving — a short, tall shapecast, not a single pixel-precise
	// ray), then probe straight down from well above the player to find the ledge's actual top
	// surface. Using the real surface height (instead of just keeping the player's current Y) is
	// what makes the grab position — and therefore the climb-up target — land correctly on top.
	private bool TryDetectLedge(out Vector2 grabPosition)
	{
		float dir = _facingRight ? 1f : -1f;
		grabPosition = Vector2.Zero;

		_ledgeWallCheck.TargetPosition = new Vector2(Mathf.Abs(_ledgeWallCheck.TargetPosition.X) * dir, _ledgeWallCheck.TargetPosition.Y);
		_ledgeWallCheck.ForceShapecastUpdate();
		if (!_ledgeWallCheck.IsColliding())
			return false;

		Vector2 wallPoint = _ledgeWallCheck.GetCollisionPoint(0);
		Vector2 probeFrom = new(wallPoint.X + dir * 4f, GlobalPosition.Y - LedgeSurfaceProbeHeight);
		Vector2 probeTo = new(probeFrom.X, GlobalPosition.Y + 8f);

		var query = PhysicsRayQueryParameters2D.Create(probeFrom, probeTo, PhysicsLayers.ClimbableWalls);
		Godot.Collections.Dictionary result = GetWorld2D().DirectSpaceState.IntersectRay(query);
		if (result.Count == 0)
			return false;

		float surfaceY = ((Vector2)result["position"]).Y;
		_ledgeSurfaceY = surfaceY;
		grabPosition = new Vector2(wallPoint.X - dir * LedgeGrabOffsetX, surfaceY + LedgeHangDropY);
		return true;
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

	public void EnterRope(Rope rope)
	{
		_ropeInZone = rope;
	}

	public void ExitRope(Rope rope)
	{
		if (_ropeInZone == rope)
			_ropeInZone = null;
		if (_lastReleasedRope == rope)
			_lastReleasedRope = null;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_isDead)
			return;

		if (_debugFlashlight != null && Input.IsActionJustPressed("debug_flashlight"))
			_debugFlashlight.Enabled = !_debugFlashlight.Enabled;

		if (GlobalPosition.Y > FallDeathY)
		{
			_stats.Kill();
			return;
		}

		bool isGroundedOrClimbing = IsOnFloor() || _isClimbing || _isWallClimbing || _isLedgeHanging || _isLedgeClimbing || _isSwinging;
		_airborneTimer = isGroundedOrClimbing ? 0f : _airborneTimer + (float)delta;
		if (_airborneTimer > MaxAirborneTime)
		{
			// Safety net against out-of-bounds/collision bugs that let the player fall forever
			// without ever crossing FallDeathY (e.g. a hole in the level's collision).
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

			// The trail clip is a short one-shot (it auto-hides on AnimationFinished), but a
			// dive with no enemy to bounce off can fall for way longer than that clip's length.
			// Keep re-triggering it every time it finishes so the effect covers the whole drop
			// instead of vanishing partway down.
			if (!_weaponTrail.IsPlaying())
			{
				string poundFallTrail = WeaponTrailAnimation(_currentAttackAnimation);
				_weaponTrail.Scale = WeaponTrailScale(poundFallTrail);
				_weaponTrail.Visible = true;
				_weaponTrail.Play(poundFallTrail);
			}

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

		if (!_isSwinging && _ropeInZone != null && _ropeInZone != _lastReleasedRope
			&& _abilities.Has(PlayerAbilities.RopeSwing) && !IsOnFloor())
		{
			_rope = _ropeInZone;
			_isSwinging = true;
		}

		if (_isSwinging)
		{
			// The rope swings on its own fixed schedule whether or not anyone's attached —
			// grabbing on doesn't influence its motion at all, Player just rides wherever the
			// rope's tip currently is each frame.
			GlobalPosition = _rope.EndPosition;
			Velocity = Vector2.Zero;

			_visual.Scale = new Vector2(_facingRight ? 1 : -1, 1f);
			_visual.Position = Vector2.Zero;

			// No dedicated swing art yet — the wall-hang pose reads fine for "hanging from
			// something", same stopgap Wall Climb uses until its own animation exists.
			if (_sprite.Animation != "wall_hang")
				_sprite.Play("wall_hang");

			if (Input.IsActionJustPressed("jump"))
			{
				// Keeps the swing's horizontal fling but replaces the vertical component with a
				// normal jump impulse instead of whatever the tangential velocity happened to be —
				// otherwise letting go right near the bottom/top of the swing (where vertical
				// speed is near zero) barely launches at all and just reads as "dropped", not
				// "jumped".
				Velocity = new Vector2(_rope.EndVelocity.X, JumpVelocity);
				_isSwinging = false;
				_lastReleasedRope = _rope;
				_rope = null;
				_jumpCount = 1;
			}

			return;
		}

		if (_ledgeGrabLockout > 0f)
			_ledgeGrabLockout -= (float)delta;

		if (_isLedgeClimbing)
		{
			_ledgeClimbTimer += (float)delta;
			float climbT = Mathf.Clamp(_ledgeClimbTimer / LedgeClimbDuration, 0f, 1f);
			GlobalPosition = _ledgeClimbStartPosition.Lerp(_ledgeClimbEndPosition, climbT);
			if (_sprite.Animation != "wall_climb")
				_sprite.Play("wall_climb");

			if (climbT >= 1f)
			{
				_isLedgeClimbing = false;
				_jumpCount = 0;
				_isDoubleJumping = false;
			}
			return;
		}

		if (_isLedgeHanging)
		{
			Velocity = Vector2.Zero;
			if (_sprite.Animation != "wall_hang")
				_sprite.Play("wall_hang");

			// Brief beat on the grab pose so the catch actually reads, then climb up on its own —
			// no input wait, the ability is meant to always carry you over the ledge.
			_ledgeHangTimer += (float)delta;
			if (_ledgeHangTimer >= LedgeAutoClimbDelay)
			{
				_isLedgeHanging = false;
				_isLedgeClimbing = true;
				_ledgeClimbTimer = 0f;
				_ledgeClimbStartPosition = GlobalPosition;
				float climbDir = _facingRight ? 1f : -1f;
				_ledgeClimbEndPosition = new Vector2(
					GlobalPosition.X + climbDir * LedgeClimbForwardX,
					_ledgeSurfaceY - _standHalfHeight - 2f);
			}
			return;
		}

		if (_wallJumpLockout > 0f)
			_wallJumpLockout -= (float)delta;

		if (_abilities.Has(PlayerAbilities.LedgeGrab) && _ledgeGrabLockout <= 0f && !IsOnFloor()
			&& TryDetectLedge(out Vector2 ledgeGrabPosition))
		{
			_isLedgeHanging = true;
			_ledgeHangTimer = 0f;
			GlobalPosition = ledgeGrabPosition;
			Velocity = Vector2.Zero;
			_isWallClimbing = false;
			_sprite.Play("wall_hang");
			return;
		}

		bool wasWallClimbing = _isWallClimbing;
		bool touchingClimbableWall = IsTouchingClimbableWall(out float climbableWallNormalX);
		if (_abilities.Has(PlayerAbilities.WallClimb) && _wallJumpLockout <= 0f && touchingClimbableWall && !IsOnFloor())
		{
			float wallInputDir = Input.GetAxis("move_left", "move_right");
			bool pressingIntoWall = wallInputDir != 0f && Mathf.Sign(wallInputDir) == -Mathf.Sign(climbableWallNormalX);

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
			float wallNormalX = climbableWallNormalX;
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
		bool droppedThrough = Input.IsActionPressed("move_down") && Input.IsActionJustPressed("jump")
			&& IsOnFloor() && TryDropThroughOneWayPlatform();

		if (!droppedThrough && Input.IsActionJustPressed("jump") && (IsOnFloor() || _jumpCount < maxJumps))
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
			CycleElement();
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
		// Tracked separately from _camera.Offset so Shake() can add a jitter on top of it each
		// frame without that jitter itself getting lerped-toward next frame (which would just
		// smooth the shake away to nothing).
		_lookOffset = _lookOffset.Lerp(targetOffset, (float)delta * LookSmoothSpeed);
		_camera.Offset = _lookOffset + GetShakeOffset(delta);

		float targetHeadAngle = lookingUp ? HeadLookUpAngle : 0f;
		_head.RotationDegrees = Mathf.Lerp(_head.RotationDegrees, targetHeadAngle, (float)delta * LookSmoothSpeed);

		UpdateBossZoom(delta);
	}

	// strength is in pixels; the shake decays linearly to 0 over duration.
	public void ShakeCamera(float strength, float duration)
	{
		_shakeStrength = strength;
		_shakeDuration = Mathf.Max(0.0001f, duration);
		_shakeTimer = duration;
	}

	private Vector2 GetShakeOffset(double delta)
	{
		if (_shakeTimer <= 0f)
			return Vector2.Zero;

		_shakeTimer -= (float)delta;
		float t = Mathf.Clamp(_shakeTimer / _shakeDuration, 0f, 1f);
		float strength = _shakeStrength * t;
		return new Vector2(
			(float)GD.RandRange(-1.0, 1.0) * strength,
			(float)GD.RandRange(-1.0, 1.0) * strength);
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

		_chargeEffect.Visible = true;
		_chargeEffect.Play("charge");

		Tween flickerTween = CreateTween().SetLoops();
		flickerTween.TweenProperty(_visual, "modulate:a", 0.35f, 0.15f);
		flickerTween.TweenProperty(_visual, "modulate:a", 1f, 0.15f);

		float heldTime = 0f;
		while (Input.IsActionPressed("charged_attack"))
		{
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			heldTime += (float)GetPhysicsProcessDeltaTime();

			if (heldTime >= ChargedAttackDuration && !_chargeReadyEffect.Visible)
			{
				_chargeReadyEffect.Visible = true;
				_chargeReadyEffect.Play("ready");
			}
		}

		flickerTween.Kill();
		Color modulate = _visual.Modulate;
		_visual.Modulate = new Color(modulate.R, modulate.G, modulate.B, 1f);
		_chargeEffect.Stop();
		_chargeEffect.Visible = false;
		_chargeReadyEffect.Stop();
		_chargeReadyEffect.Visible = false;

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
		_hitbox.Activate(_stats, element: _currentElement);

		string chargedTrail = _isFireImbued ? "thrust_02_fire" : "thrust_02";
		_weaponTrail.Scale = WeaponTrailScale(chargedTrail);
		_weaponTrail.Position = new Vector2(_weaponTrailBaseX, _weaponTrailBaseY);
		_weaponTrail.Visible = true;
		_weaponTrail.Play(chargedTrail);

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
		_hitbox.Activate(_stats, element: _currentElement);

		_sprite.Rotation = Mathf.Pi / 2f;

		_weaponTrail.Rotation = Mathf.Pi / 2f;
		_weaponTrail.Position = new Vector2(0, PoundWeaponTrailYOffset);
		string poundTrail = WeaponTrailAnimation(_currentAttackAnimation);
		_weaponTrail.Scale = WeaponTrailScale(poundTrail);
		_weaponTrail.Visible = true;
		_weaponTrail.Play(poundTrail);

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
		_hitbox.Activate(_stats, element: _currentElement);

		_weaponTrail.Position = new Vector2(0, -UpAttackHitboxReach);
		string upAttackTrail = WeaponTrailAnimation(_currentAttackAnimation);
		_weaponTrail.Scale = WeaponTrailScale(upAttackTrail);
		_weaponTrail.Visible = true;
		_weaponTrail.Play(upAttackTrail);

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
		_hitbox.Activate(_stats, element: _currentElement);
		string runThrustTrail = _isFireImbued ? "thrust_05_fire" : "thrust_05";
		_weaponTrail.Scale = WeaponTrailScale(runThrustTrail);
		_weaponTrail.Position = new Vector2(_weaponTrailBaseX, _weaponTrailBaseY);
		_weaponTrail.Visible = true;
		_weaponTrail.Play(runThrustTrail);

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

		_hitbox.Activate(_stats, element: _currentElement);
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
		_comboStep = (_comboStep + 1) % EffectiveComboHitCount;
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
		_hitbox.Position = new Vector2(_facingRight ? AttackHitboxReach : -AttackHitboxReach, AttackHitboxYOffset);

		string comboTrail = WeaponTrailAnimation(_currentAttackAnimation);
		_weaponTrail.Scale = WeaponTrailScale(comboTrail);
		_weaponTrail.Position = new Vector2(_weaponTrailBaseX, _weaponTrailBaseY);
		_weaponTrail.Visible = true;
		_weaponTrail.Play(comboTrail);

		int comboPulseCount = _currentAttackAnimation switch
		{
			"attack2" => 2,
			"attack3" => 3,
			_ => 1,
		};

		if (comboPulseCount > 1)
		{
			await ComboMultiHit(comboPulseCount);
		}
		else
		{
			_hitbox.Activate(_stats, ComboImpactFramesPath(_currentAttackAnimation), _currentElement);
			await ToSignal(GetTree().CreateTimer(AttackDuration), SceneTreeTimer.SignalName.Timeout);
			_hitbox.Deactivate();
		}

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
		bool attacked = await RunSingleAttack("crouch_attack", "slash_vertical", CrouchWeaponTrailYOffset);
		if (!attacked)
		{
			_canCrouchAttack = true;
			return;
		}

		await ToSignal(GetTree().CreateTimer(CrouchAttackCooldown), SceneTreeTimer.SignalName.Timeout);
		_canCrouchAttack = true;
	}

	// Returns whether the attack actually landed/spent stamina — callers use this to skip their
	// own cooldown when it didn't (matches how every other stamina-gated move, e.g. UpAttack,
	// only locks out after actually committing to the attack).
	private async Task<bool> RunSingleAttack(string animationName, string trailAnimation, float trailYOffset = 0f)
	{
		if (!_stats.TrySpendStamina(AttackStaminaCost))
			return false;

		_attacking = true;
		_currentAttackAnimation = animationName;

		bool hitLanded = false;
		void OnHitLanded() => hitLanded = true;
		_hitbox.HitDealt += OnHitLanded;

		if (AttackHitboxDelay > 0f)
			await ToSignal(GetTree().CreateTimer(AttackHitboxDelay), SceneTreeTimer.SignalName.Timeout);

		_hitboxShape.Size = _hitboxBaseSize;
		_hitbox.Position = new Vector2(_facingRight ? AttackHitboxReach : -AttackHitboxReach, 0);
		_hitbox.Activate(_stats, element: _currentElement);

		_weaponTrail.Scale = new Vector2(2.904f, 2.904f);
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
		return true;
	}

	private string WeaponTrailAnimation(string attackAnimation) => attackAnimation switch
	{
		"attack2" => _isFireImbued ? "slash_horizontal_09_fire" : "slash_horizontal_09",
		"attack3" => _isFireImbued ? "thrust_01_fire" : "thrust_01",
		_ => _isFireImbued ? "slash_horizontal_fire" : "slash_horizontal",
	};

	// The combo's three hits now use much bigger source canvases (Effect Pack #7) than every
	// other move sharing this same AnimatedSprite2D node (crouch/dash-thrust/run-thrust still
	// use the original tiny 48x48 smear frames), so the node's scale has to switch per-animation
	// instead of staying at one fixed value.
	private static Vector2 WeaponTrailScale(string trailAnimation) => trailAnimation switch
	{
		"slash_horizontal" or "slash_horizontal_fire" => new Vector2(0.84f, 0.84f),
		"slash_horizontal_09" or "slash_horizontal_09_fire" => new Vector2(0.756f, 0.756f),
		"thrust_01" or "thrust_01_fire" => new Vector2(1.1025f, 1.1025f),
		"thrust_05" or "thrust_05_fire" => new Vector2(1.155f, 1.155f),
		"thrust_02" or "thrust_02_fire" => new Vector2(0.82f, 0.82f),
		_ => new Vector2(2.904f, 2.904f),
	};

	// Shared by attack2 (2 pulses) and attack3 (3 pulses, the finisher): the hitbox re-activates
	// once per pulse, each landing its own hit-stop freeze, reading as a "thunk-thunk[-thunk]"
	// flurry instead of a single hit.
	private async Task ComboMultiHit(int pulseCount)
	{
		string impactFramesPath = ComboImpactFramesPath(_currentAttackAnimation);
		try
		{
			for (int i = 0; i < pulseCount; i++)
			{
				bool pulseHitLanded = false;
				void OnPulseHit() => pulseHitLanded = true;
				_hitbox.HitDealt += OnPulseHit;

				_hitbox.Activate(_stats, impactFramesPath, _currentElement, ignoreTargetInvulnerability: true);

				// Give the physics step a moment at normal speed to actually detect the overlap
				// and fire HitDealt before we freeze time out from under it.
				await ToSignal(GetTree().CreateTimer(HitStopDetectWindow), SceneTreeTimer.SignalName.Timeout);
				_hitbox.HitDealt -= OnPulseHit;
				if (!IsInstanceValid(this))
					return;

				// Whiffing shouldn't slow anything down — only a pulse that actually connected
				// earns its freeze.
				if (pulseHitLanded)
				{
					Engine.TimeScale = HitStopFreezeScale;
					await ToSignal(GetTree().CreateTimer(HitStopFreezeDuration, true, false, true), SceneTreeTimer.SignalName.Timeout);
					Engine.TimeScale = 1.0;
					if (!IsInstanceValid(this))
						return;
				}

				_hitbox.Deactivate();

				bool isLastPulse = i == pulseCount - 1;
				if (!isLastPulse)
				{
					await ToSignal(GetTree().CreateTimer(HitStopGapDuration), SceneTreeTimer.SignalName.Timeout);
					if (!IsInstanceValid(this))
						return;
				}
			}
		}
		finally
		{
			Engine.TimeScale = 1.0;
			if (IsInstanceValid(this))
				_hitbox.Deactivate();
		}
	}

	private static string ComboImpactFramesPath(string attackAnimation) => attackAnimation switch
	{
		"attack1" => "res://resources/sprites/HitImpactMediumSpriteFrames.tres",
		"attack2" => "res://resources/sprites/HitImpactMediumSpriteFrames.tres",
		"attack3" => "res://resources/sprites/HitImpactBigSpriteFrames.tres",
		_ => null,
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

	private async void CycleElement()
	{
		if (_isTransforming)
			return;

		DamageElement previous = _currentElement;
		_currentElement = previous switch
		{
			DamageElement.Normal => DamageElement.Fire,
			DamageElement.Fire => DamageElement.Ice,
			DamageElement.Ice => DamageElement.Poison,
			DamageElement.Poison => DamageElement.Lightning,
			_ => DamageElement.Normal,
		};

		// Fire is the only element with a full alternate spriteframes transformation today —
		// entering/leaving it plays that transformation. The other elements just update the
		// tag used by outgoing hits (see Stats.Weakness) until they get their own art.
		if (_currentElement == DamageElement.Fire)
		{
			_isTransforming = true;
			await ToSignal(GetTree().CreateTimer(TransformationInDuration), SceneTreeTimer.SignalName.Timeout);
			_sprite.SpriteFrames = FireSpriteFrames;
			// The node was still holding "transformation" (only valid in the normal set) the
			// instant SpriteFrames swapped out from under it — reset to a name that exists in
			// both sets immediately, instead of leaving it dangling until the next animation update.
			_sprite.Play("idle");
			_isFireImbued = true;
			VfxSpawner.SpawnAt(this, _visual.GlobalPosition, "res://resources/sprites/BuffGlowSpriteFrames.tres", "glow");
			_isTransforming = false;
		}
		else if (previous == DamageElement.Fire)
		{
			_isTransforming = true;
			await ToSignal(GetTree().CreateTimer(TransformationOutDuration), SceneTreeTimer.SignalName.Timeout);
			_sprite.SpriteFrames = _normalSpriteFrames;
			_sprite.Play("idle");
			_isFireImbued = false;
			_isTransforming = false;
		}
	}

	private void OnDied()
	{
		// Dying mid-Pound skips the rest of _PhysicsProcess (see the _isDead guard at its top),
		// which otherwise reaches EndPound() itself — without this, the 90° sprite/trail
		// rotation and the active hitbox would stay stuck through the death animation.
		EndPound();

		_isDead = true;
		_sprite.Play("death");
		if (GetTree().CurrentScene is LevelBootstrap level)
			DeathScreen.Instance.Show(level.RespawnPlayer);
	}
}
