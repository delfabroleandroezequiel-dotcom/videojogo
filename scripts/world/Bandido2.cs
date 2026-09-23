using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// Same shape as Bandido1 (Assassin): windup telegraph before the swing, reposition when the player
// dashes past. The Bandits2 Raider only ships one real attack though (an overhead axe chop — no
// second swing like the Assassin's dagger), so there's no alternation here, just the single
// animation. "block" on this sheet is fake too (checked, same near-transparent idle-pose fingerprint
// as the Assassin's and the Hound's) — not used.
public partial class Bandido2 : MeleeEnemy
{
	// The axe windup (fully raised overhead) reads clearly at frame index 2, confirmed by cropping
	// the sheet — index 1 is still mid-raise, index 2 is the held-high peak right before the chop.
	[Export] public float WindupDuration = 0.4f;
	[Export] public int AttackTelegraphFrame = 2;

	// attack1 plays at 14fps (see RaiderSpriteFrames.tres) — frame 3 of the resumed swing is where
	// the axe actually connects.
	[Export] public float HitFrameDelay = 3f / 14f;

	// Same dash-cross reposition as Bandido1 — breaks off and retreats a short burst if the player
	// dashes past it, instead of standing there while they circle behind.
	[Export] public float RepositionSpeed = 220f;
	[Export] public float RepositionDuration = 0.3f;
	[Export] public float RepositionCooldown = 1.5f;

	// The axe chop's neon-red slash trail, drawn by EnemySlashTrail: same top-to-bottom cut and
	// numbers as Bandido1's overhead stab (itself sized to the player's attack2 reach — center 12px
	// in front, radius 56). Angles: 0° = front, -90° = up, +90° = down. The attack hitbox in
	// Bandido2.tscn is sized to cover this arc.
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

	// ── Patient-brute AI ──
	// Hovers just outside the player's reach instead of walking into their sword, waiting for an
	// opening: a whiffed swing, a dash ending nearby or the player turning their back → charges in
	// and chops with a shorter wind-up. Gives up waiting after MaxBaitTime and just walks in.
	// Its axe doesn't care much about a raised shield (high BlockEngageChance).
	[ExportGroup("AI")]
	[Export] public float BaitDistanceExtra = 45f;
	[Export] public float MaxBaitTime = 1.4f;
	[Export] public float ChargeSpeed = 260f;
	[Export] public float ChargeMaxTime = 0.6f;
	[Export] public float PunishWindupScale = 0.5f;
	[Export] public float AxeBlockEngageChance = 0.85f;

	private EnemySlashTrail _slashTrail;
	private bool _charging;
	private float _chargeTimer;
	private float _baitTimer;	private bool _isRepositioning;
	private bool _canReposition = true;
	private float _repositionDirection;
	private bool _hasPlayerSideSample;
	private bool _wasPlayerOnRight;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;

		BlockEngageChance = AxeBlockEngageChance;
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

	private bool Baiting(Metroidvania.Player.Player player) =>
		player is not null && _baitTimer < MaxBaitTime && player.IsOnFloor()
		&& player.IsFacingRight == (GlobalPosition.X > player.GlobalPosition.X);

	private void Think(float dt)
	{
		var player = PlayerRef;
		if (player is null || _isRepositioning || !PlayerDetected || Attacking)
			return;

		float absDistance = Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X);
		bool playerFacesMe = player.IsFacingRight == (GlobalPosition.X > player.GlobalPosition.X);
		float threatZone = AttackRange + BaitDistanceExtra + 30f;

		if (absDistance <= threatZone && playerFacesMe)
			_baitTimer += dt;
		else if (absDistance > threatZone + 60f)
			_baitTimer = 0f;

		bool opening = Whiff.JustWhiffed || Dash.SinceDashEnded < 0.1f || (!playerFacesMe && absDistance <= threatZone);
		if (!_charging && opening && absDistance <= threatZone + 40f)
		{
			Whiff.Consume();
			_charging = true;
			_chargeTimer = ChargeMaxTime;
		}

		if (_charging)
		{
			_chargeTimer -= dt;
			if (absDistance <= AttackRange)
			{
				_charging = false;
				_baitTimer = 0f;
				TryAttackNow(PunishWindupScale);
			}
			else if (_chargeTimer <= 0f)
			{
				_charging = false;
			}
		}
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (Hopping)
			return currentVelocityX;
		float toward = Mathf.Sign(distanceX);
		if (_charging && !Attacking)
			return CanStepTo(toward) ? toward * ChargeSpeed : 0f;
		if (Baiting(PlayerRef))
			return SpacingMoveX(distanceX, currentVelocityX, AttackRange + BaitDistanceExtra, AttackRange * 0.7f);
		return SpacingMoveX(distanceX, currentVelocityX, StopDistance, 0f);
	}

	// While baiting it stays out of range on purpose — don't let the base loop swing at air.
	protected override bool ReadyToCommitAttack() => !Baiting(PlayerRef);
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

		string anim = Attacking ? "attack1" : Mathf.Abs(velocity.X) > 5f ? "run" : "idle";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	protected override async Task Attack()
	{
		Attacking = true;
		CanAttack = false;

		HoldTelegraphFrame("attack1");
		Sprite.Frame = AttackTelegraphFrame;
		float windup = WindupDuration * WindupScale;
		WindupScale = 1f;
		await ToSignal(GetTree().CreateTimer(windup), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		Sprite.Play("attack1");
		_slashTrail.Play(SlashTrailCenter, SlashTrailRadius, SlashTrailStartAngle, SlashTrailEndAngle, SlashTrailDuration);
		await ToSignal(GetTree().CreateTimer(HitFrameDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		await base.Attack();
	}
}
