using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// PixelLab variant of the Raider (Bandido2) with a spear. Fights at the spear's length: it keeps the
// player just past sword reach, backing off at a walk when they get close, and its stab steps
// forward as it thrusts so the point arrives further than its stance suggests.
public partial class Bandido2Lanza : MeleeEnemy
{
	[Export] public float WindupDuration = 0.4f;
	// attack plays at 14fps (Bandido2LanzaSpriteFrames.tres): spear drawn back on TelegraphFrame 1,
	// fully extended on frame 2.
	[Export] public float HitFrameDelay = 1f / 14f;
	[Export] public float PreferredDistance = 70f;
	[Export] public float TooCloseDistance = 45f;
	[Export] public float ThrustStepSpeed = 260f;
	[Export] public float ThrustStepTime = 0.12f;

	// A long, flat streak along the thrust line, out past the tip to the hitbox's far edge (60).
	// Angles: 0° = front, -90° = up, +90° = down.
	[ExportGroup("Trail")]
	[Export] public Vector2 TrailCenter = new(-30f, -6f);
	[Export] public float TrailRadius = 90f;
	[Export] public float TrailRadiusY = 10f;
	[Export] public float TrailStartAngle = -8f;
	[Export] public float TrailEndAngle = 6f;
	[Export] public float TrailDuration = 0.08f;
	[Export] public Color TrailGlowColor = new(1f, 0.08f, 0.12f, 1f);
	[Export] public Color TrailCoreColor = new(1f, 0.75f, 0.7f, 1f);

	private EnemySlashTrail _trail;
	private float _stepTimer;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_trail = new EnemySlashTrail { CoreWidth = 2f, GlowWidth = 7f, GlowColor = TrailGlowColor, CoreColor = TrailCoreColor };
		Visual.AddChild(_trail);
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		if (_stepTimer > 0f)
			_stepTimer -= (float)delta;
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		float forward = FacingRight ? 1f : -1f;
		if (_stepTimer > 0f && CanStepTo(forward))
			return forward * ThrustStepSpeed;
		return SpacingMoveX(distanceX, currentVelocityX, PreferredDistance, TooCloseDistance);
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null)
			return;
		bool backingOff = Mathf.Abs(velocity.X) > 5f && Mathf.Sign(velocity.X) != (FacingRight ? 1f : -1f);
		string anim = Attacking ? "attack" : Mathf.Abs(velocity.X) <= 5f ? "idle" : backingOff ? "walk" : "run";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
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

		Sprite.Play("attack");
		_stepTimer = ThrustStepTime;
		_trail.Play(TrailCenter, TrailRadius, TrailStartAngle, TrailEndAngle, TrailDuration, TrailRadiusY);
		await ToSignal(GetTree().CreateTimer(HitFrameDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;
		await base.Attack();
	}
}
