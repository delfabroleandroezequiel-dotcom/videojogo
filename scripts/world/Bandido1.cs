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

	// attack1's neon-red slash trail, drawn by EnemySlashTrail (attack2 overrides the shape below):
	// a vertical top-to-bottom cut in front of the assassin. Angles: 0° = front, -90° = up, +90° = down.
	// Same geometry as the player's attack2 crescent: centered 12px in front of the body (the
	// player's WeaponPivot) with radius 56 (Player.SwordSpinRadius), so both slashes reach equally
	// far. The attack hitbox in Bandido1.tscn is sized to cover this same arc.
	[Export] public Vector2 SlashTrailCenter = new(12f, 0f);
	[Export] public float SlashTrailRadius = 56f;
	[Export] public float SlashTrailStartAngle = -115f;
	[Export] public float SlashTrailEndAngle = 85f;
	[Export] public float SlashTrailDuration = 0.2f;
	[Export] public float SlashTrailCoreWidth = 3f;
	[Export] public float SlashTrailGlowWidth = 9f;
	[Export] public Color SlashTrailGlowColor = new(1f, 0.05f, 0.12f, 1f);
	[Export] public Color SlashTrailCoreColor = new(1f, 0.55f, 0.6f, 1f);
	[Export] public float MinSlashTrailPointSpacing = 1.5f;
	// attack2 (the drawn-back dagger slash) gets the player's attack1 arc instead, same numbers
	// and orientation: Player.RunSwordArcTrail traces the tip at -WeaponPivot.Rotation while the
	// pivot tweens -70° -> 60° (WeaponSwingStartAngle/EndAngle) over AttackHitboxDelay +
	// AttackDuration (0.12 + 0.22), radius SwordArcTrailRadius 52 — so on screen the arc rises
	// from low-front (+70°) to high-front (-60°).
	[Export] public float Attack2TrailRadius = 52f;
	[Export] public float Attack2TrailStartAngle = 70f;
	[Export] public float Attack2TrailEndAngle = -60f;
	[Export] public float Attack2TrailDuration = 0.34f;

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
	// Flank: the player turtles behind their block toward it this long → vault over them and stab
	// from behind (the player's block only covers the front).
	[Export] public float FlankAfterBlockTime = 0.7f;
	[Export] public Vector2 VaultHop = new(270f, -390f);

	private EnemySlashTrail _slashTrail;
	private bool _lunging;
	private float _lungeTimer;
	private bool _counterPending;
	private float _evadeCooldownTimer;
	private float _playerBlockTimer;
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

		_slashTrail = new EnemySlashTrail
		{
			CoreWidth = SlashTrailCoreWidth,
			GlowWidth = SlashTrailGlowWidth,
			GlowColor = SlashTrailGlowColor,
			CoreColor = SlashTrailCoreColor,
			MinPointSpacing = MinSlashTrailPointSpacing,
		};
		Visual.AddChild(_slashTrail);
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

			if (Sprite is not null && Sprite.Animation != "run")
				Sprite.Play("run");
			return;
		}

		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval)
			return;

		CheckPlayerDashCross();
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

			_playerBlockTimer = PlayerReads.IsBlockingToward(player, GlobalPosition.X) && absDistance <= 110f
				? _playerBlockTimer + dt : 0f;
			if (_playerBlockTimer > FlankAfterBlockTime && TryHop(new Vector2(toward * VaultHop.X, VaultHop.Y)))
			{
				_playerBlockTimer = 0f;
				_counterPending = true;
			}
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

		Sprite.Play(_currentAttackAnimation);
		if (useSecondAttack)
			_slashTrail.Play(SlashTrailCenter, Attack2TrailRadius, Attack2TrailStartAngle, Attack2TrailEndAngle, Attack2TrailDuration);
		else
			_slashTrail.Play(SlashTrailCenter, SlashTrailRadius, SlashTrailStartAngle, SlashTrailEndAngle, SlashTrailDuration);
		await ToSignal(GetTree().CreateTimer(HitFrameDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		await base.Attack();
	}
}
