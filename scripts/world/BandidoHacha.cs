using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// PixelLab-made heavy bandit (Bandido1's faction look, axe). One committed overhead chop: it holds
// the axe high as the telegraph, then brings it down in front of it. Heavy kit — walks instead of
// running and shrugs off hits while swinging (HyperArmorWhileAttacking in the scene), so the answer
// is to parry or get out of the way, not to trade.
public partial class BandidoHacha : MeleeEnemy
{
	[Export] public float WindupDuration = 0.55f;

	// attack plays at 14fps (BandidoHachaSpriteFrames.tres): frame 3 is the axe held high
	// (TelegraphFrame in the scene) and frame 4 is the axe already down in front, so the hit lands
	// one frame after the swing resumes.
	[Export] public float HitFrameDelay = 1f / 14f;

	// Neon chop swept OUT AHEAD of the axe, over the head and down the front, sized so its front
	// edge reaches the AttackHitbox's far edge (reach 34 + half width 30 = 64), not just the blade.
	// Angles: 0° = front, -90° = up, +90° = down.
	[ExportGroup("Trail")]
	[Export] public Vector2 ChopTrailCenter = new(4f, -10f);
	[Export] public float ChopTrailRadius = 60f;
	[Export] public float ChopTrailRadiusY = 52f;
	[Export] public float ChopTrailStartAngle = -125f;
	[Export] public float ChopTrailEndAngle = 70f;
	[Export] public float ChopTrailDuration = 0.13f;
	[Export] public float ChopTrailCoreWidth = 4f;
	[Export] public float ChopTrailGlowWidth = 12f;
	[Export] public Color ChopTrailGlowColor = new(1f, 0.25f, 0.05f, 1f);
	[Export] public Color ChopTrailCoreColor = new(1f, 0.8f, 0.55f, 1f);

	private EnemySlashTrail _chopTrail;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;

		_chopTrail = new EnemySlashTrail
		{
			CoreWidth = ChopTrailCoreWidth,
			GlowWidth = ChopTrailGlowWidth,
			GlowColor = ChopTrailGlowColor,
			CoreColor = ChopTrailCoreColor,
			SparkAmount = 24,
		};
		Visual.AddChild(_chopTrail);
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
		_chopTrail.Play(ChopTrailCenter, ChopTrailRadius, ChopTrailStartAngle, ChopTrailEndAngle, ChopTrailDuration, ChopTrailRadiusY);
		await ToSignal(GetTree().CreateTimer(HitFrameDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		await base.Attack();
	}
}
