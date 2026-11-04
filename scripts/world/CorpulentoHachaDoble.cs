using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// PixelLab variant of BandidoCorpulento with a double-bladed battle axe. A charger: from mid range it
// lowers its head and barrels in during the wind-up, then the axe comes around in one huge flat
// swing. Can't be flinched while swinging (HyperArmorWhileAttacking in the scene); the charge is
// what to dodge, the swing is what to parry.
public partial class CorpulentoHachaDoble : MeleeEnemy
{
	[Export] public float WindupDuration = 0.55f;
	// attack plays at 14fps (CorpulentoHachaDobleSpriteFrames.tres): axe swung all the way back on
	// TelegraphFrame 5, around in front on frame 6.
	[Export] public float HitFrameDelay = 1f / 14f;
	[Export] public float ChargeSpeed = 300f;

	// A wide, nearly flat arc out to the hitbox's far edge (62). Angles: 0° = front, -90° = up.
	[ExportGroup("Trail")]
	[Export] public Vector2 TrailCenter = new(-4f, -6f);
	[Export] public float TrailRadius = 64f;
	[Export] public float TrailRadiusY = 26f;
	[Export] public float TrailStartAngle = -150f;
	[Export] public float TrailEndAngle = 40f;
	[Export] public float TrailDuration = 0.14f;
	[Export] public Color TrailGlowColor = new(0.9f, 0.05f, 0.05f, 1f);
	[Export] public Color TrailCoreColor = new(1f, 0.6f, 0.5f, 1f);

	private EnemySlashTrail _trail;
	private bool _charging;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_trail = new EnemySlashTrail { CoreWidth = 4f, GlowWidth = 13f, GlowColor = TrailGlowColor, CoreColor = TrailCoreColor, SparkAmount = 26 };
		Visual.AddChild(_trail);
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		float toward = Mathf.Sign(distanceX);
		if (_charging)
			return Mathf.Abs(distanceX) > 40f && CanStepTo(toward) ? toward * ChargeSpeed : 0f;
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
		_charging = true;
		float windup = WindupDuration * WindupScale;
		WindupScale = 1f;
		await ToSignal(GetTree().CreateTimer(windup), SceneTreeTimer.SignalName.Timeout);
		_charging = false;
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
