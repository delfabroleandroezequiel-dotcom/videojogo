using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// The Light Bandit (Bandits pack, not Bandits2) is a much smaller kit than the other two: one
// attack, no fake "block" this time (this pack doesn't even ship one), no walk animation, and its
// animation names (attack/run/idle) already match MeleeEnemy's own defaults, so no UpdateAnimation
// override is needed here. Only addition over the base class is the windup telegraph — same reason
// every other melee enemy has one, so the swing doesn't land the instant it arrives. The "Recover"
// animation (a full get-up-from-the-ground sequence) is real but unused here — it reads as a
// knockdown-recovery pose, not a per-swing animation, and wiring an actual knockdown state is more
// than this simpler bandit calls for.
public partial class Bandido3 : MeleeEnemy
{
	[Export] public float WindupDuration = 0.35f;

	// "attack" plays at 14fps (see LightBanditSpriteFrames.tres) — frame 4 of the resumed swing is
	// where the hit actually connects.
	[Export] public float HitFrameDelay = 4f / 14f;

	// Neon-red streak swept OUT AHEAD of the blade, not on top of it: it rides the sheet's own swing
	// (over the head, down the front — top-down, starts on TrailFrame, the frame the baked smear first
	// shows) but on a much wider oval, so it shows the attack's real reach. The front of the oval
	// (center.x + radius = 81) lines up with the far edge of the AttackHitbox in Bandido3.tscn
	// (50.6 + 67/2 = 84) and its height with the hitbox's, while the blade's own smear only reaches
	// ~43px. Angles: 0° = front, -90° = up, +90° = down.
	[ExportGroup("Trail")]
	[Export] public int TrailFrame = 4;
	[Export] public Vector2 SlashTrailCenter = new(9f, -10f);
	[Export] public float SlashTrailRadius = 72f;
	[Export] public float SlashTrailRadiusY = 45f;
	[Export] public float SlashTrailStartAngle = -120f;
	[Export] public float SlashTrailEndAngle = 95f;
	[Export] public float SlashTrailDuration = 0.12f;
	[Export] public float SlashTrailCoreWidth = 3f;
	[Export] public float SlashTrailGlowWidth = 9f;
	[Export] public Color SlashTrailGlowColor = new(1f, 0.05f, 0.12f, 1f);
	[Export] public Color SlashTrailCoreColor = new(1f, 0.55f, 0.6f, 1f);
	[Export] public float MinSlashTrailPointSpacing = 1.5f;

	// ── Anti-air duelist AI ──
	// Its swing starts by going over its head, so it's the one bandit that punishes jumping:
	// a player dropping onto it from above gets met with a fast overhead cut. Plays hit-and-run —
	// often hops back after a swing (only onto ground) — and lunges at whiffs/dash endings.
	[ExportGroup("AI")]
	[Export] public float AntiAirRange = 80f;
	[Export] public float AntiAirWindupScale = 0.3f;
	[Export] public float HitAndRunChance = 0.6f;
	[Export] public Vector2 BackHop = new(210f, -250f);
	[Export] public float LungeRange = 150f;
	[Export] public float LungeSpeed = 290f;
	[Export] public float LungeMaxTime = 0.45f;
	[Export] public float PunishWindupScale = 0.4f;

	private EnemySlashTrail _slashTrail;
	private bool _lunging;
	private float _lungeTimer;
	private bool _wasAttacking;
	private bool _trailPending;
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
		Sprite.FrameChanged += OnSpriteFrameChanged;
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval)
			return;
		Think((float)delta);
	}

	private void Think(float dt)
	{
		var player = PlayerRef;
		bool justFinishedSwing = _wasAttacking && !Attacking;
		_wasAttacking = Attacking;
		if (player is null || !PlayerDetected)
			return;

		float distanceX = player.GlobalPosition.X - GlobalPosition.X;
		float absDistance = Mathf.Abs(distanceX);
		float toward = Mathf.Sign(distanceX);

		if (justFinishedSwing && Rng.Randf() < HitAndRunChance)
			TryHop(new Vector2(-toward * BackHop.X, BackHop.Y));

		if (Attacking || Hopping)
			return;

		// Anti-air: player above and coming down (or hanging at the apex) right on top of it.
		bool playerAbove = !player.IsOnFloor() && player.GlobalPosition.Y < GlobalPosition.Y - 15f;
		if (playerAbove && absDistance <= AntiAirRange && player.Velocity.Y > -80f)
		{
			FacingRight = distanceX >= 0f;
			Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);
			TryAttackNow(AntiAirWindupScale);
			return;
		}

		if ((Whiff.JustWhiffed || Dash.SinceDashEnded < 0.1f) && absDistance <= LungeRange)
		{
			Whiff.Consume();
			_lunging = true;
			_lungeTimer = LungeMaxTime;
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

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (Hopping)
			return currentVelocityX;
		float toward = Mathf.Sign(distanceX);
		if (_lunging && !Attacking)
			return CanStepTo(toward) ? toward * LungeSpeed : 0f;
		return SpacingMoveX(distanceX, currentVelocityX, StopDistance, 0f);
	}

	private static readonly RandomNumberGenerator Rng = new();

	static Bandido3()
	{
		Rng.Randomize();
	}

	// Fires the trail on the exact frame the sheet's own smear appears, so it sits on the drawn blade path.
	private void OnSpriteFrameChanged()
	{
		if (!_trailPending || !Attacking || Sprite.Animation != "attack" || Sprite.Frame != TrailFrame)
			return;
		_trailPending = false;
		_slashTrail.Play(SlashTrailCenter, SlashTrailRadius, SlashTrailStartAngle, SlashTrailEndAngle, SlashTrailDuration, SlashTrailRadiusY);
	}

	protected override async Task Attack()
	{
		Attacking = true;
		CanAttack = false;

		HoldTelegraphFrame("attack");
		float windup = WindupDuration * WindupScale;
		WindupScale = 1f;
		await ToSignal(GetTree().CreateTimer(windup), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		_trailPending = true;
		Sprite.Play("attack");
		await ToSignal(GetTree().CreateTimer(HitFrameDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		await base.Attack();
	}
}
