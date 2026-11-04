using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// PixelLab variant of the Wild Bandit (Bandido5) with a huge spiked club. A crouched brute that
// lumbers in and brings the club down into the floor: slow, can't be flinched out of the swing
// (HyperArmorWhileAttacking in the scene), and the impact throws grit sparks along the ground.
// If the player stays in its face after a slam, it goes straight into a second, faster one.
public partial class Bandido5Garrote : MeleeEnemy
{
	[Export] public float WindupDuration = 0.6f;
	[Export] public float FollowUpWindupScale = 0.45f;
	[Export] public float FollowUpRange = 60f;
	// attack plays at 14fps (Bandido5GarroteSpriteFrames.tres): club raised on TelegraphFrame 3,
	// buried in the floor on frame 5.
	[Export] public float HitFrameDelay = 2f / 14f;

	// Angles: 0° = front, -90° = up, +90° = down. Ends low, at the hitbox's far edge (56).
	[ExportGroup("Trail")]
	[Export] public Vector2 TrailCenter = new(4f, -2f);
	[Export] public float TrailRadius = 52f;
	[Export] public float TrailRadiusY = 46f;
	[Export] public float TrailStartAngle = -120f;
	[Export] public float TrailEndAngle = 80f;
	[Export] public float TrailDuration = 0.15f;
	[Export] public Color TrailGlowColor = new(1f, 0.3f, 0.05f, 1f);
	[Export] public Color TrailCoreColor = new(1f, 0.85f, 0.6f, 1f);

	private EnemySlashTrail _trail;
	private bool _followUpReady = true;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_trail = new EnemySlashTrail { CoreWidth = 4f, GlowWidth = 13f, GlowColor = TrailGlowColor, CoreColor = TrailCoreColor, SparkAmount = 30, SparkLifetime = 0.5 };
		Visual.AddChild(_trail);
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
		_trail.Play(TrailCenter, TrailRadius, TrailStartAngle, TrailEndAngle, TrailDuration, TrailRadiusY);
		await ToSignal(GetTree().CreateTimer(HitFrameDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;
		await base.Attack();
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		// Second slam only once in a row, and only if the player stayed right in front of it.
		if (_followUpReady && PlayerRef is not null && Mathf.Abs(PlayerRef.GlobalPosition.X - GlobalPosition.X) <= FollowUpRange)
		{
			_followUpReady = false;
			TryAttackNow(FollowUpWindupScale);
		}
		else
		{
			_followUpReady = true;
		}
	}
}
