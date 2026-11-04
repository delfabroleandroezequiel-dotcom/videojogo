using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// PixelLab variant of BandidoCorpulento with a spiked flail. The ball keeps spinning around it while
// it closes in, so the spin itself is the tell; the smash comes forward out of the spin and the
// circle of the swing covers its back too (wide hitbox centred close). Can't be flinched while
// swinging (HyperArmorWhileAttacking in the scene).
public partial class CorpulentoMangual : MeleeEnemy
{
	[Export] public float WindupDuration = 0.6f;
	// attack plays at 14fps (CorpulentoMangualSpriteFrames.tres): ball circling overhead on
	// TelegraphFrame 3, smashing forward on frame 4.
	[Export] public float HitFrameDelay = 1f / 14f;

	// A full circle around it, ending in front at the hitbox's far edge (58).
	// Angles: 0° = front, -90° = up, +90° = down.
	[ExportGroup("Trail")]
	[Export] public Vector2 TrailCenter = new(0f, -10f);
	[Export] public float TrailRadius = 54f;
	[Export] public float TrailRadiusY = 40f;
	[Export] public float TrailStartAngle = -300f;
	[Export] public float TrailEndAngle = 40f;
	[Export] public float TrailDuration = 0.2f;
	[Export] public Color TrailGlowColor = new(0.9f, 0.05f, 0.05f, 1f);
	[Export] public Color TrailCoreColor = new(1f, 0.6f, 0.5f, 1f);

	private EnemySlashTrail _trail;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_trail = new EnemySlashTrail { CoreWidth = 3f, GlowWidth = 11f, GlowColor = TrailGlowColor, CoreColor = TrailCoreColor, SparkAmount = 26 };
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
		await ToSignal(GetTree().CreateTimer(windup * 0.5f), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;
		// The circle starts during the wind-up: the spin is the tell.
		_trail.Play(TrailCenter, TrailRadius, TrailStartAngle, TrailEndAngle, windup * 0.5f + HitFrameDelay, TrailRadiusY);
		await ToSignal(GetTree().CreateTimer(windup * 0.5f), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		Sprite.Play("attack");
		await ToSignal(GetTree().CreateTimer(HitFrameDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;
		await base.Attack();
	}
}
