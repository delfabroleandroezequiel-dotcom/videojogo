using System.Threading.Tasks;
using Godot;
using Metroidvania.Player;

namespace Metroidvania.World;

// Ghost Warrior 2 — an Oscuro (the book's living shadows: faceless, superhumanly fast). One big
// overhead sword swing (the sheet's only attack, its crescent already drawn into frames 6-7), made
// dangerous by speed instead of variety:
//  * Shadow step: a very fast dash (run clip + cyan neon afterimages, the player's dash-ghost look)
//    that closes mid-range gaps and chains straight into a quick swing on arrival.
//  * Reads the player like the other smart enemies: steps back out of a swing that starts close in
//    front of it and steps right back in; punishes whiffs and dash endings; if the player dashes
//    past it, it turns and steps after them; if the player turtles behind their block, it steps
//    *through* them and swings from behind (the block only covers the front).
//  * Every swing is announced with a cyan blink (spider-style tell) on the raised-sword frame, and
//    leaves a big cyan neon crescent (EnemySlashTrail) on top of the drawn one.
// Never steps off a ledge (CanStepTo every frame of a step).
public partial class GhostWarrior2 : MeleeEnemy
{
	[ExportGroup("Attack")]
	// Held on TelegraphFrame (raised sword, frame 2) while blinking; punishes use WindupScale.
	[Export] public float WindupDuration = 0.3f;
	[Export] public float QuickWindupScale = 0.35f;
	// The swing resumes at this frame; the blade connects at HitFrame (crescent frames 6-7).
	[Export] public int AttackStartFrame = 3;
	[Export] public int HitFrame = 6;
	[Export] public float AttackFps = 14f;

	[ExportGroup("Slash trail")]
	// Big overhead crescent: from behind-overhead down to the front. 0° = front, -90° = up.
	[Export] public Vector2 SlashTrailCenter = new(18f, -10f);
	[Export] public float SlashTrailRadius = 83f;
	[Export] public float SlashTrailStartAngle = -165f;
	[Export] public float SlashTrailEndAngle = 40f;
	[Export] public float SlashTrailDuration = 0.2f;
	[Export] public float SlashTrailCoreWidth = 6f;
	[Export] public float SlashTrailGlowWidth = 19.5f;
	[Export] public Color SlashTrailGlowColor = new(0.15f, 0.9f, 1f, 1f);
	[Export] public Color SlashTrailCoreColor = new(0.8f, 1f, 1f, 1f);

	[ExportGroup("Telegraph")]
	[Export] public Color TelegraphTint = new(0.6f, 2.4f, 3f, 1f);
	[Export] public Color TelegraphGlowColor = new(0.3f, 0.95f, 1f, 0.9f);
	[Export] public float TelegraphGlowScale = 1.3f;
	[Export] public int TelegraphBlinks = 2;

	[ExportGroup("Shadow step")]
	[Export] public float StepSpeed = 620f;
	[Export] public float StepMaxTime = 0.35f;
	[Export] public float StepMinDistance = 110f;
	[Export] public float StepMaxDistance = 300f;
	[Export] public float StepCooldown = 1.6f;
	[Export] public float StepChance = 0.55f;
	[Export] public float DecisionInterval = 0.3f;
	// Flank: how far past the player the step goes before turning to swing.
	[Export] public float FlankOvershoot = 70f;
	[Export] public float AfterimageInterval = 0.03f;
	[Export] public float AfterimageLifetime = 0.25f;
	[Export] public float AfterimageAlpha = 0.8f;
	[Export] public float AfterimageIntensity = 1.8f;
	[Export] public Color AfterimageColor = new(0.15f, 0.85f, 1f, 1f);

	[ExportGroup("AI")]
	[Export] public float EvadeChance = 0.5f;
	[Export] public float EvadeRange = 115f;
	[Export] public float EvadeCooldown = 1.8f;
	[Export] public float EvadeDuration = 0.16f;
	[Export] public float PunishRange = 230f;
	[Export] public float FlankAfterBlockTime = 0.5f;
	[Export] public float HurtDuration = 0.3f;
	// Below this speed it glides (walk clip), above it sprints (run clip).
	[Export] public float RunAnimSpeed = 130f;

	private enum StepMode { None, Engage, Evade, Flank }

	private EnemySlashTrail _slashTrail;
	private Sprite2D _telegraphGlow;
	private Tween _telegraphTween;
	private StepMode _step = StepMode.None;
	private float _stepDirection;
	private float _stepTimer;
	private float _stepTargetX;
	private float _afterimageTimer;
	private float _stepCooldownTimer;
	private float _evadeCooldownTimer;
	private float _decisionTimer;
	private float _blockTimer;
	private float _hurtTimer;
	private bool _counterAfterEvade;
	private bool _hasDashSide;
	private bool _dashWasOnRight;
	private static readonly RandomNumberGenerator Rng = new();

	static GhostWarrior2()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;

		_slashTrail = new EnemySlashTrail
		{
			CoreWidth = SlashTrailCoreWidth,
			GlowWidth = SlashTrailGlowWidth,
			GlowColor = SlashTrailGlowColor,
			CoreColor = SlashTrailCoreColor,
		};
		Visual.AddChild(_slashTrail);

		_telegraphGlow = new Sprite2D
		{
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			Modulate = TelegraphGlowColor with { A = 0f },
			Scale = Vector2.One * TelegraphGlowScale,
			ShowBehindParent = true,
		};
		Visual.AddChild(_telegraphGlow);

		Stats.HitTaken += _ =>
		{
			if (!Attacking && _step == StepMode.None)
				_hurtTimer = HurtDuration;
		};
		_stepCooldownTimer = Rng.RandfRange(0.3f, 1f);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;

		float dt = (float)delta;
		if (_step != StepMode.None)
		{
			PlayerRef = PlayerReads.Find(this);
			RunStep(dt);
			return;
		}

		_hurtTimer -= dt;
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval)
			return;
		Think(dt);
	}

	// ── Decisions ──

	private void Think(float dt)
	{
		_stepCooldownTimer -= dt;
		_evadeCooldownTimer -= dt;
		_decisionTimer -= dt;

		var player = PlayerRef;
		if (player is null || !PlayerDetected || Attacking || Hopping || _hurtTimer > 0f || !IsOnFloor())
		{
			_hasDashSide = false;
			return;
		}

		float distanceX = player.GlobalPosition.X - GlobalPosition.X;
		float absDistance = Mathf.Abs(distanceX);
		float toward = Mathf.Sign(distanceX);
		bool playerFacesMe = player.IsFacingRight == (GlobalPosition.X > player.GlobalPosition.X);

		// Right after an evade: step straight back in.
		if (_counterAfterEvade)
		{
			_counterAfterEvade = false;
			StartStep(StepMode.Engage, toward);
			return;
		}

		// The player dashed past → turn and chase them down.
		if (player.IsDashing)
		{
			bool onRight = distanceX > 0f;
			if (_hasDashSide && onRight != _dashWasOnRight && absDistance <= StepMaxDistance)
			{
				_hasDashSide = false;
				FacingRight = onRight;
				StartStep(StepMode.Engage, toward);
				return;
			}
			_hasDashSide = true;
			_dashWasOnRight = onRight;
		}
		else
		{
			_hasDashSide = false;
		}

		// A swing starting close in front of it → slip back out of reach, then come right back.
		if (Whiff.PlayerStartedSwing && absDistance <= EvadeRange && playerFacesMe && _evadeCooldownTimer <= 0f
			&& Rng.Randf() < EvadeChance && CanStepTo(-toward))
		{
			_evadeCooldownTimer = EvadeCooldown;
			StartStep(StepMode.Evade, -toward);
			return;
		}

		// Whiffed swing or a dash just ended within reach → punish.
		if ((Whiff.JustWhiffed || Dash.SinceDashEnded < 0.1f) && absDistance <= PunishRange)
		{
			Whiff.Consume();
			if (absDistance <= AttackRange)
				TryAttackNow(QuickWindupScale);
			else
				StartStep(StepMode.Engage, toward);
			return;
		}

		// Turtling behind the block → step through them and swing from behind.
		_blockTimer = PlayerReads.IsBlockingToward(player, GlobalPosition.X) && absDistance <= 140f ? _blockTimer + dt : 0f;
		if (_blockTimer > FlankAfterBlockTime && CanStepTo(toward))
		{
			_blockTimer = 0f;
			_stepTargetX = player.GlobalPosition.X + toward * FlankOvershoot;
			StartStep(StepMode.Flank, toward);
			return;
		}

		// Mid range → sometimes close the gap in one shadow step.
		if (_decisionTimer <= 0f)
		{
			_decisionTimer = DecisionInterval;
			if (_stepCooldownTimer <= 0f && absDistance >= StepMinDistance && absDistance <= StepMaxDistance
				&& Rng.Randf() < StepChance && CanStepTo(toward))
			{
				StartStep(StepMode.Engage, toward);
			}
		}
	}

	// ── Shadow step ──

	private void StartStep(StepMode mode, float direction)
	{
		if (direction == 0f)
			return;
		_step = mode;
		_stepDirection = direction;
		_stepTimer = mode == StepMode.Evade ? EvadeDuration : StepMaxTime;
		_afterimageTimer = 0f;
		if (mode != StepMode.Evade)
		{
			_stepCooldownTimer = StepCooldown;
			FacingRight = direction > 0f;
			Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);
		}
		Sprite?.Play("run");
	}

	private void RunStep(float dt)
	{
		_stepTimer -= dt;
		Vector2 velocity = Velocity;
		// Keep gravity on even when grounded: a zero Y velocity makes MoveAndSlide drop IsOnFloor(),
		// which TryAttackNow needs for the swing on arrival.
		velocity.Y += Gravity * dt;
		velocity.X = CanStepTo(_stepDirection) ? _stepDirection * StepSpeed : 0f;
		Velocity = velocity;
		MoveAndSlide();

		_afterimageTimer -= dt;
		if (_afterimageTimer <= 0f)
		{
			_afterimageTimer = AfterimageInterval;
			SpawnAfterimage();
		}

		var player = PlayerRef;
		float distanceX = player is null ? 0f : player.GlobalPosition.X - GlobalPosition.X;
		bool blocked = velocity.X == 0f || IsOnWall();

		switch (_step)
		{
			case StepMode.Engage:
				if (player is not null && Mathf.Abs(distanceX) <= AttackRange * 0.9f)
				{
					EndStep();
					TryAttackNow(QuickWindupScale);
					return;
				}
				break;
			case StepMode.Flank:
				if (player is not null && (GlobalPosition.X - _stepTargetX) * _stepDirection >= 0f)
				{
					EndStep();
					FacingRight = distanceX > 0f;
					Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);
					TryAttackNow(QuickWindupScale);
					return;
				}
				break;
		}

		if (_stepTimer <= 0f || blocked)
		{
			bool wasEvade = _step == StepMode.Evade;
			EndStep();
			if (wasEvade)
				_counterAfterEvade = true;
		}
	}

	private void EndStep()
	{
		_step = StepMode.None;
		Velocity = new Vector2(0f, Velocity.Y);
	}

	private void SpawnAfterimage()
	{
		Texture2D frame = Sprite?.SpriteFrames?.GetFrameTexture(Sprite.Animation, Sprite.Frame);
		if (frame is null || GetTree().CurrentScene is not Node scene)
			return;

		var ghost = new Sprite2D
		{
			Texture = frame,
			Centered = Sprite.Centered,
			Offset = Sprite.Offset,
			FlipH = Sprite.FlipH,
			Material = NeonSilhouette.CreateMaterial(AfterimageColor, AfterimageIntensity),
			Modulate = new Color(1f, 1f, 1f, AfterimageAlpha),
			ZIndex = ZIndex - 1,
		};
		scene.AddChild(ghost);
		ghost.GlobalTransform = Sprite.GlobalTransform;

		Tween fade = ghost.CreateTween();
		fade.TweenProperty(ghost, "modulate:a", 0f, AfterimageLifetime).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
		fade.TweenCallback(Callable.From(ghost.QueueFree));
	}

	// ── Movement / animation ──

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_hurtTimer > 0f)
			return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
		return SpacingMoveX(distanceX, currentVelocityX, StopDistance, 0f);
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null)
			return;

		string anim = Attacking ? "attack"
			: _hurtTimer > 0f ? "hurt"
			: Mathf.Abs(velocity.X) > RunAnimSpeed ? "run"
			: Mathf.Abs(velocity.X) > 5f ? "walk"
			: "idle";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	// ── Attack ──

	private void Telegraph(float duration)
	{
		if (Sprite is null || duration <= 0f)
			return;
		_telegraphTween?.Kill();
		_telegraphTween = CreateTween();
		float half = duration / Mathf.Max(1, TelegraphBlinks) * 0.5f;
		for (int i = 0; i < TelegraphBlinks; i++)
		{
			_telegraphTween.TweenProperty(Sprite, "self_modulate", TelegraphTint, half);
			_telegraphTween.Parallel().TweenProperty(_telegraphGlow, "modulate:a", TelegraphGlowColor.A, half);
			_telegraphTween.TweenProperty(Sprite, "self_modulate", Colors.White, half);
			_telegraphTween.Parallel().TweenProperty(_telegraphGlow, "modulate:a", 0f, half);
		}
	}

	protected override async Task Attack()
	{
		Attacking = true;
		CanAttack = false;
		_hurtTimer = 0f;

		HoldTelegraphFrame("attack");
		float windup = WindupDuration * WindupScale;
		WindupScale = 1f;
		Telegraph(windup);
		await ToSignal(GetTree().CreateTimer(windup), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval || !IsInsideTree())
			return;

		_telegraphTween?.Kill();
		Sprite.SelfModulate = Colors.White;
		_telegraphGlow.Modulate = TelegraphGlowColor with { A = 0f };
		Sprite.Play("attack");
		Sprite.Frame = AttackStartFrame;
		_slashTrail.Play(SlashTrailCenter, SlashTrailRadius, SlashTrailStartAngle, SlashTrailEndAngle, SlashTrailDuration);
		await ToSignal(GetTree().CreateTimer((HitFrame - AttackStartFrame) / AttackFps), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval || !IsInsideTree())
			return;

		await base.Attack();
	}
}
