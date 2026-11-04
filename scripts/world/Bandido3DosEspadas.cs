using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// PixelLab variant of the Light Bandit (Bandido3) with two short swords. Starts its combo with no
// visible wind-up beyond a short crouch and spins forward through two cuts, sliding a step between
// them, so backing off by one dodge isn't enough. Both cuts share one hitbox window (frames 1 to 6),
// each with its own amber streak.
public partial class Bandido3DosEspadas : MeleeEnemy
{
	[Export] public float WindupDuration = 0.28f;
	// attack plays at 14fps (Bandido3DosEspadasSpriteFrames.tres) from TelegraphFrame 0: first cut on
	// frame 1, second on frame 6.
	[Export] public float FirstCutDelay = 1f / 14f;
	[Export] public float SecondCutDelay = 6f / 14f;
	[Export] public float SpinSlideSpeed = 150f;
	[Export] public float SpinSlideTime = 0.3f;

	// Angles: 0° = front, -90° = up, +90° = down. Both reach the hitbox's far edge (48).
	[ExportGroup("Trails")]
	[Export] public Color TrailGlowColor = new(1f, 0.55f, 0.05f, 1f);
	[Export] public Color TrailCoreColor = new(1f, 0.9f, 0.6f, 1f);
	[Export] public Vector2 FirstTrailCenter = new(4f, -4f);
	[Export] public float FirstTrailStartAngle = -60f;
	[Export] public float FirstTrailEndAngle = 60f;
	[Export] public Vector2 SecondTrailCenter = new(4f, -2f);
	[Export] public float SecondTrailStartAngle = 70f;
	[Export] public float SecondTrailEndAngle = -70f;
	[Export] public float TrailRadius = 44f;
	[Export] public float TrailRadiusY = 34f;
	[Export] public float TrailDuration = 0.09f;

	private EnemySlashTrail _firstTrail;
	private EnemySlashTrail _secondTrail;
	private float _slideTimer;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_firstTrail = MakeTrail();
		_secondTrail = MakeTrail();
	}

	private EnemySlashTrail MakeTrail()
	{
		var trail = new EnemySlashTrail { CoreWidth = 2f, GlowWidth = 7f, GlowColor = TrailGlowColor, CoreColor = TrailCoreColor, SparkAmount = 10 };
		Visual.AddChild(trail);
		return trail;
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		if (_slideTimer > 0f)
			_slideTimer -= (float)delta;
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		float forward = FacingRight ? 1f : -1f;
		if (_slideTimer > 0f && CanStepTo(forward))
			return forward * SpinSlideSpeed;
		return base.ComputeMoveX(player, distanceX, currentVelocityX, delta);
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null)
			return;
		string anim = Attacking ? "attack" : Mathf.Abs(velocity.X) > 5f ? "run" : "idle";
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
		_slideTimer = SpinSlideTime;
		_firstTrail.Play(FirstTrailCenter, TrailRadius, FirstTrailStartAngle, FirstTrailEndAngle, TrailDuration, TrailRadiusY);
		PlaySecondCut();
		await ToSignal(GetTree().CreateTimer(FirstCutDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;
		await base.Attack();
	}

	private async void PlaySecondCut()
	{
		await ToSignal(GetTree().CreateTimer(SecondCutDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval || !Attacking)
			return;
		_secondTrail.Play(SecondTrailCenter, TrailRadius, SecondTrailStartAngle, SecondTrailEndAngle, TrailDuration, TrailRadiusY);
	}
}
