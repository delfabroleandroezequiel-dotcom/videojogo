using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// PixelLab variant of the Heavy Bandit (Bandido7) with a two-handed greatsword. The biggest single
// hit of the armored bandits: a long, readable wind-up with the sword over its head, a cleave it
// can't be flinched out of (HyperArmorWhileAttacking in the scene), and a step forward on the cut
// so retreating one step isn't enough. Missing leaves it stuck in a long recovery.
public partial class Bandido7Mandoble : MeleeEnemy
{
	[Export] public float WindupDuration = 0.75f;
	// attack plays at 14fps (Bandido7MandobleSpriteFrames.tres): sword overhead on TelegraphFrame 1,
	// cleaving through on frame 2.
	[Export] public float HitFrameDelay = 1f / 14f;
	[Export] public float CleaveStepSpeed = 220f;
	[Export] public float CleaveStepTime = 0.12f;

	// Angles: 0° = front, -90° = up, +90° = down. Down to the hitbox's far edge (58).
	[ExportGroup("Trail")]
	[Export] public Vector2 TrailCenter = new(2f, -8f);
	[Export] public float TrailRadius = 56f;
	[Export] public float TrailRadiusY = 52f;
	[Export] public float TrailStartAngle = -140f;
	[Export] public float TrailEndAngle = 75f;
	[Export] public float TrailDuration = 0.14f;
	[Export] public Color TrailGlowColor = new(0.45f, 0.75f, 1f, 1f);
	[Export] public Color TrailCoreColor = new(0.95f, 1f, 1f, 1f);

	private EnemySlashTrail _trail;
	private float _stepTimer;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_trail = new EnemySlashTrail { CoreWidth = 4f, GlowWidth = 13f, GlowColor = TrailGlowColor, CoreColor = TrailCoreColor, SparkAmount = 26 };
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
			return forward * CleaveStepSpeed;
		return base.ComputeMoveX(player, distanceX, currentVelocityX, delta);
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null)
			return;
		string anim = Attacking ? "attack" : Mathf.Abs(velocity.X) > 5f ? "walk" : "idle";
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
		_stepTimer = CleaveStepTime;
		_trail.Play(TrailCenter, TrailRadius, TrailStartAngle, TrailEndAngle, TrailDuration, TrailRadiusY);
		await ToSignal(GetTree().CreateTimer(HitFrameDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;
		await base.Attack();
	}
}
