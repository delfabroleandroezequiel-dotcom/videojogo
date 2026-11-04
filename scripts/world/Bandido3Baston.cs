using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// PixelLab variant of the Light Bandit (Bandido3) with an iron-capped quarterstaff. The staff sweeps
// a huge overhead arc that starts behind it, so its hitbox covers both sides at once — standing at
// its back is no safer than in front. In exchange the wind-up is long and the staff knocks the
// player far away instead of chaining hits.
public partial class Bandido3Baston : MeleeEnemy
{
	[Export] public float WindupDuration = 0.5f;
	// attack plays at 14fps (Bandido3BastonSpriteFrames.tres): staff back on TelegraphFrame 3,
	// slamming down in front on frame 5.
	[Export] public float HitFrameDelay = 2f / 14f;

	// One arc from behind its back, over the head, down in front. Its front edge meets the hitbox's
	// far edge (the hitbox is wide and centred close so it also catches the back).
	// Angles: 0° = front, -90° = up, +90° = down.
	[ExportGroup("Trail")]
	[Export] public Vector2 TrailCenter = new(0f, -6f);
	[Export] public float TrailRadius = 50f;
	[Export] public float TrailRadiusY = 44f;
	[Export] public float TrailStartAngle = -200f;
	[Export] public float TrailEndAngle = 60f;
	[Export] public float TrailDuration = 0.16f;
	[Export] public Color TrailGlowColor = new(1f, 0.5f, 0.05f, 1f);
	[Export] public Color TrailCoreColor = new(1f, 0.92f, 0.65f, 1f);

	private EnemySlashTrail _trail;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_trail = new EnemySlashTrail { CoreWidth = 3f, GlowWidth = 10f, GlowColor = TrailGlowColor, CoreColor = TrailCoreColor, SparkAmount = 18 };
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
	}
}
