using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// PixelLab variant of the Heavy Bandit (Bandido7) with a tower shield and a flanged mace. A slow
// wall: it advances behind the shield, frontal hits don't move it (knockback only lands from
// behind), and it doesn't flinch while swinging (HyperArmorWhileAttacking in the scene). The answer
// is to get around it or parry the mace that comes over the top of the shield.
public partial class Bandido7Escudo : MeleeEnemy
{
	[Export] public float WindupDuration = 0.55f;
	// attack plays at 14fps (Bandido7EscudoSpriteFrames.tres): mace raised on TelegraphFrame 1,
	// over the shield on frame 2.
	[Export] public float HitFrameDelay = 1f / 14f;

	// Angles: 0° = front, -90° = up, +90° = down. Over the top and down to the hitbox's far edge (46).
	[ExportGroup("Trail")]
	[Export] public Vector2 TrailCenter = new(6f, -10f);
	[Export] public float TrailRadius = 40f;
	[Export] public float TrailRadiusY = 40f;
	[Export] public float TrailStartAngle = -130f;
	[Export] public float TrailEndAngle = 30f;
	[Export] public float TrailDuration = 0.12f;
	[Export] public Color TrailGlowColor = new(0.45f, 0.75f, 1f, 1f);
	[Export] public Color TrailCoreColor = new(0.9f, 0.97f, 1f, 1f);

	private EnemySlashTrail _trail;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_trail = new EnemySlashTrail { CoreWidth = 3f, GlowWidth = 10f, GlowColor = TrailGlowColor, CoreColor = TrailCoreColor, SparkAmount = 18 };
		Visual.AddChild(_trail);
	}

	public override void ApplyKnockback(Vector2 direction, float force)
	{
		bool fromFront = Mathf.Sign(direction.X) == (FacingRight ? -1f : 1f);
		if (fromFront)
			return;
		base.ApplyKnockback(direction, force);
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
