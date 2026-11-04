using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// PixelLab variant of the Torcher (Bandido4) with a flaming whip. The longest melee reach of the
// bandits: it hangs back at whip length and cracks the lash straight out, a long thin streak of fire
// that extends far past what the sprite draws (the whip's reach is the neon, not the pixels).
public partial class Bandido4Latigo : MeleeEnemy
{
	[Export] public float WindupDuration = 0.45f;
	// attack plays at 14fps (Bandido4LatigoSpriteFrames.tres): arm back on TelegraphFrame 3,
	// lash fully out on frame 4.
	[Export] public float HitFrameDelay = 1f / 14f;
	[Export] public float PreferredDistance = 85f;
	[Export] public float TooCloseDistance = 55f;

	// A shallow, very long arc out to the hitbox's far edge (95). Angles: 0° = front, -90° = up.
	[ExportGroup("Trail")]
	[Export] public Vector2 TrailCenter = new(-20f, -10f);
	[Export] public float TrailRadius = 112f;
	[Export] public float TrailRadiusY = 26f;
	[Export] public float TrailStartAngle = -40f;
	[Export] public float TrailEndAngle = 12f;
	[Export] public float TrailDuration = 0.1f;

	private EnemySlashTrail _trail;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		var ramp = new Gradient();
		ramp.SetColor(0, new Color(0.7f, 0.05f, 0f, 0f));
		ramp.SetColor(1, new Color(1f, 0.7f, 0.15f, 1f));
		_trail = new EnemySlashTrail { CoreWidth = 1.5f, GlowWidth = 6f, GlowColor = Colors.White, CoreColor = new Color(1f, 0.95f, 0.7f), GlowRamp = ramp, SparkColor = new Color(1f, 0.55f, 0.1f), SparkAmount = 12 };
		Visual.AddChild(_trail);
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta) =>
		SpacingMoveX(distanceX, currentVelocityX, PreferredDistance, TooCloseDistance);

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
		_trail.Play(TrailCenter, TrailRadius, TrailStartAngle, TrailEndAngle, TrailDuration, TrailRadiusY);
		await ToSignal(GetTree().CreateTimer(HitFrameDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;
		await base.Attack();
	}
}
