using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// The Bandits2 Assassin ships two distinct swings (attack1/attack2) — this alternates between them
// instead of always playing one, and holds a windup pose before each so it never reads as hitting
// the instant it arrives (same settle-then-telegraph idea as WarriorEnemy/OrcEnemy). It also reacts
// to a player dash that crosses from one side to the other by breaking off and repositioning, so
// dashing past it isn't a free "get behind it" trick.
public partial class Bandido1 : MeleeEnemy
{
	[Export] public float SecondAttackChance = 0.5f;
	// How long it holds the raised-dagger windup before the swing actually fires. attack1's windup
	// (overhead stab) and attack2's (drawn-back slash) are different poses at different frame
	// indices, confirmed by cropping the sheet, not guessed.
	[Export] public float WindupDuration = 0.4f;
	[Export] public int Attack1TelegraphFrame = 1;
	[Export] public int Attack2TelegraphFrame = 2;

	// Both attack1/attack2 play at 14fps (see AssassinSpriteFrames.tres) — frame 3 of the resumed
	// swing is where the dagger actually connects, same for either variant.
	[Export] public float HitFrameDelay = 3f / 14f;

	// If the player dashes past it (crosses from one side to the other mid-dash — the classic
	// "dash through and hit from behind" trick), it breaks off and runs a short burst back toward
	// where the player started, re-opening the gap instead of just standing there flat-footed.
	[Export] public float RepositionSpeed = 220f;
	[Export] public float RepositionDuration = 0.3f;
	[Export] public float RepositionCooldown = 1.5f;

	// ── Assassin trails (its own look, not the shared bandit crescent) ──
	// Thinner "razor" streaks than Bandido2/3: a crimson glow around an almost-white core.
	// Both are traced over the white smear the sheet already bakes into the swing, measured from
	// the sheet's pixels (frame center 55,40, sprite scale 1.5556, AttackSpriteYOffset -4), and
	// start on the sprite frame where that smear first shows (TrailFrame) instead of on a timer.
	// Angles: 0° = front, -90° = up, +90° = down.
	[ExportGroup("Trails")]
	[Export] public int TrailFrame = 3;
	[Export] public Color TrailGlowColor = new(0.9f, 0.02f, 0.15f, 1f);
	[Export] public Color TrailCoreColor = new(1f, 0.85f, 0.88f, 1f);
	[Export] public float MinTrailPointSpacing = 1.5f;
	// attack1: overhead blade coming down in front — the sheet's crescent runs from high-front to
	// just below horizontal around frame px (50,45), radius ~43 frame px.
	[Export] public Vector2 CutTrailCenter = new(-7.8f, 3.8f);
	[Export] public float CutTrailRadius = 67f;
	[Export] public float CutTrailStartAngle = -60f;
	[Export] public float CutTrailEndAngle = 15f;
	[Export] public float CutTrailDuration = 0.11f;
	[Export] public float CutTrailCoreWidth = 1.5f;
	[Export] public float CutTrailGlowWidth = 5f;
	[Export] public float CutFlashSize = 0.7f;
	// attack2: a short off-hand flick rising at shoulder height (the sheet's small hook smear),
	// so a tight little arc, not a full slash.
	[Export] public Vector2 FlickTrailCenter = new(17f, -21f);
	[Export] public float FlickTrailRadius = 25f;
	[Export] public float FlickTrailStartAngle = 75f;
	[Export] public float FlickTrailEndAngle = -15f;
	[Export] public float FlickTrailDuration = 0.08f;
	[Export] public float FlickTrailCoreWidth = 1.2f;
	[Export] public float FlickTrailGlowWidth = 4f;
	[Export] public float FlickFlashSize = 0.45f;
	// Crimson neon ghosts left behind while it lunges in or repositions (same afterimage recipe as
	// GhostWarrior2) — the assassin's signature, it reads as a blur when it closes the gap.
	[Export] public float AfterimageInterval = 0.035f;
	[Export] public float AfterimageLifetime = 0.22f;
	[Export] public float AfterimageAlpha = 0.6f;
	[Export] public float AfterimageIntensity = 1.6f;
	[Export] public Color AfterimageColor = new(1f, 0.08f, 0.2f, 1f);

	// ── Smart-duelist AI (same "read the player" approach as the elves) ──
	// Evade: when the player starts a swing close in front of it, sometimes hops back out of reach
	// (only if the hop lands on ground) and immediately lunges back in with a fast stab.
	[ExportGroup("AI")]
	[Export] public float EvadeChance = 0.45f;
	[Export] public float EvadeRange = 85f;
	[Export] public float EvadeCooldown = 2.5f;
	[Export] public Vector2 EvadeHop = new(230f, -260f);
	// Punish: a whiffed swing or a dash ending near it → lunge in and stab with a short wind-up.
	[Export] public float LungeRange = 170f;
	[Export] public float LungeSpeed = 330f;
	[Export] public float LungeMaxTime = 0.5f;
	[Export] public float PunishWindupScale = 0.35f;
	// Flank: the player whiffs a parry toward it → vault over them during the recovery and stab
	// from behind.
	[Export] public Vector2 VaultHop = new(270f, -390f);

	private EnemySlashTrail _cutTrail;
	private EnemySlashTrail _flickTrail;
	private bool _trailPending;
	private float _afterimageTimer;
	private bool _lunging;
	private float _lungeTimer;
	private bool _counterPending;
	private float _evadeCooldownTimer;
	private string _currentAttackAnimation = "attack1";
	private bool _isRepositioning;
	private bool _canReposition = true;
	private float _repositionDirection;
	private bool _hasPlayerSideSample;
	private bool _wasPlayerOnRight;
	private static readonly RandomNumberGenerator Rng = new();

	static Bandido1()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;

		_cutTrail = MakeTrail(CutTrailCoreWidth, CutTrailGlowWidth);
		_flickTrail = MakeTrail(FlickTrailCoreWidth, FlickTrailGlowWidth);
		Sprite.FrameChanged += OnSpriteFrameChanged;
	}

	private EnemySlashTrail MakeTrail(float coreWidth, float glowWidth)
	{
		var trail = new EnemySlashTrail
		{
			CoreWidth = coreWidth,
			GlowWidth = glowWidth,
			GlowColor = TrailGlowColor,
			CoreColor = TrailCoreColor,
			MinPointSpacing = MinTrailPointSpacing,
			SparkAmount = 8,
			SparkLifetime = 0.25,
		};
		Visual.AddChild(trail);
		return trail;
	}

	// Fires the swing's trail on the exact frame the sheet's own smear appears, so the neon sits
	// on top of the drawn blade path instead of running ahead of it.
	private void OnSpriteFrameChanged()
	{
		if (!_trailPending || !Attacking || Sprite.Animation != _currentAttackAnimation || Sprite.Frame != TrailFrame)
			return;
		_trailPending = false;

		if (_currentAttackAnimation == "attack2")
			PlayTrail(_flickTrail, FlickTrailCenter, FlickTrailRadius, FlickTrailStartAngle, FlickTrailEndAngle, FlickTrailDuration, FlickFlashSize);
		else
			PlayTrail(_cutTrail, CutTrailCenter, CutTrailRadius, CutTrailStartAngle, CutTrailEndAngle, CutTrailDuration, CutFlashSize);
	}

	private async void PlayTrail(EnemySlashTrail trail, Vector2 center, float radius, float startAngle, float endAngle, float duration, float flashSize)
	{
		trail.Play(center, radius, startAngle, endAngle, duration);
		await ToSignal(GetTree().CreateTimer(duration), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;
		SpawnTipFlash(center + Vector2.Right.Rotated(Mathf.DegToRad(endAngle)) * radius, flashSize);
	}

	// Small additive glint where the blade's path ends — the "tick" of a razor cut.
	private void SpawnTipFlash(Vector2 localPosition, float size)
	{
		var flash = new Sprite2D
		{
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			Modulate = TrailCoreColor,
			Position = localPosition,
			Scale = Vector2.One * size * 0.3f,
		};
		Visual.AddChild(flash);

		Tween tween = flash.CreateTween();
		tween.TweenProperty(flash, "scale", Vector2.One * size, 0.08f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
		tween.Parallel().TweenProperty(flash, "modulate", new Color(TrailGlowColor, 0f), 0.14f);
		tween.TweenCallback(Callable.From(flash.QueueFree));
	}

	private void UpdateAfterimages(float dt)
	{
		bool blurring = _isRepositioning || (_lunging && !Attacking && Mathf.Abs(Velocity.X) > 5f);
		if (!blurring)
		{
			_afterimageTimer = 0f;
			return;
		}

		_afterimageTimer -= dt;
		if (_afterimageTimer > 0f)
			return;
		_afterimageTimer = AfterimageInterval;
		SpawnAfterimage();
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
			Material = Metroidvania.Player.NeonSilhouette.CreateMaterial(AfterimageColor, AfterimageIntensity),
			Modulate = new Color(1f, 1f, 1f, AfterimageAlpha),
			ZIndex = ZIndex - 1,
		};
		scene.AddChild(ghost);
		ghost.GlobalTransform = Sprite.GlobalTransform;

		Tween fade = ghost.CreateTween();
		fade.TweenProperty(ghost, "modulate:a", 0f, AfterimageLifetime).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
		fade.TweenCallback(Callable.From(ghost.QueueFree));
	}
	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;

		if (_isRepositioning)
		{
			Vector2 velocity = Velocity;
			velocity.Y = IsOnFloor() ? 0f : velocity.Y + Gravity * (float)delta;
			velocity.X = _repositionDirection * RepositionSpeed;
			Velocity = velocity;
			MoveAndSlide();

			UpdateAfterimages((float)delta);
			if (Sprite is not null && Sprite.Animation != "run")
				Sprite.Play("run");
			return;
		}

		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval)
			return;

		CheckPlayerDashCross();
		UpdateAfterimages((float)delta);
		Think((float)delta);
	}

	private void Think(float dt)
	{
		_evadeCooldownTimer -= dt;
		var player = PlayerRef;
		if (player is null || _isRepositioning || !PlayerDetected)
			return;

		float distanceX = player.GlobalPosition.X - GlobalPosition.X;
		float absDistance = Mathf.Abs(distanceX);
		float toward = Mathf.Sign(distanceX);
		bool playerFacesMe = player.IsFacingRight == (GlobalPosition.X > player.GlobalPosition.X);

		// Landed from an evade/flank hop → strike right away.
		if (_counterPending && !Hopping && IsOnFloor())
		{
			_counterPending = false;
			StartLunge();
		}

		if (!Attacking && !Hopping)
		{
			if (Whiff.PlayerStartedSwing && absDistance <= EvadeRange && playerFacesMe && _evadeCooldownTimer <= 0f
				&& Rng.Randf() < EvadeChance && TryHop(new Vector2(-toward * EvadeHop.X, EvadeHop.Y)))
			{
				_evadeCooldownTimer = EvadeCooldown;
				_counterPending = true;
				return;
			}

			if ((Whiff.JustWhiffed || Dash.SinceDashEnded < 0.1f) && absDistance <= LungeRange)
			{
				Whiff.Consume();
				StartLunge();
			}

			if (PlayerReads.WhiffedParryToward(player, GlobalPosition.X) && absDistance <= 110f
				&& TryHop(new Vector2(toward * VaultHop.X, VaultHop.Y)))
				_counterPending = true;
		}

		if (_lunging)
		{
			_lungeTimer -= dt;
			if (absDistance <= AttackRange)
			{
				_lunging = false;
				TryAttackNow(PunishWindupScale);
			}
			else if (_lungeTimer <= 0f)
			{
				_lunging = false;
			}
		}
	}

	private void StartLunge()
	{
		_lunging = true;
		_lungeTimer = LungeMaxTime;
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (Hopping)
			return currentVelocityX;
		float toward = Mathf.Sign(distanceX);
		if (_lunging && !Attacking)
			return CanStepTo(toward) ? toward * LungeSpeed : 0f;
		return SpacingMoveX(distanceX, currentVelocityX, StopDistance, 0f);
	}
	// Tracks which side of the assassin the player is on only while they're mid-dash, so a plain
	// walk-around doesn't count — only an actual dash crossing counts as "got past me".
	private void CheckPlayerDashCross()
	{
		if (_isRepositioning || !_canReposition)
			return;

		if (GetTree().GetFirstNodeInGroup("player") is not Metroidvania.Player.Player player || !player.IsDashing)
		{
			_hasPlayerSideSample = false;
			return;
		}

		bool isOnRight = player.GlobalPosition.X > GlobalPosition.X;
		if (!_hasPlayerSideSample)
		{
			_wasPlayerOnRight = isOnRight;
			_hasPlayerSideSample = true;
			return;
		}

		if (isOnRight == _wasPlayerOnRight)
			return;

		// Crossed sides mid-dash — retreat back toward wherever the player started from, i.e.
		// away from where they just ended up.
		float direction = _wasPlayerOnRight ? 1f : -1f;
		_ = RunReposition(direction);
	}

	private async Task RunReposition(float direction)
	{
		_isRepositioning = true;
		_canReposition = false;
		_repositionDirection = direction;
		CanAttack = false;

		await ToSignal(GetTree().CreateTimer(RepositionDuration), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		_isRepositioning = false;
		CanAttack = true;

		await ToSignal(GetTree().CreateTimer(RepositionCooldown), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			_canReposition = true;
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null)
			return;
		if (Hopping && PlayAirAnimation(velocity))
			return;

		string anim = Attacking ? _currentAttackAnimation
			: Mathf.Abs(velocity.X) > 5f ? "run" : "idle";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	protected override async Task Attack()
	{
		Attacking = true;
		CanAttack = false;

		bool useSecondAttack = Rng.Randf() < SecondAttackChance;
		_currentAttackAnimation = useSecondAttack ? "attack2" : "attack1";
		int telegraphFrame = useSecondAttack ? Attack2TelegraphFrame : Attack1TelegraphFrame;

		HoldTelegraphFrame(_currentAttackAnimation);
		Sprite.Frame = telegraphFrame;
		float windup = WindupDuration * WindupScale;
		WindupScale = 1f;
		await ToSignal(GetTree().CreateTimer(windup), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		_trailPending = true;
		Sprite.Play(_currentAttackAnimation);
		await ToSignal(GetTree().CreateTimer(HitFrameDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		await base.Attack();
	}
}
