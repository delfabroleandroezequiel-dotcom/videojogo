using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// PixelLab variant of the Raider (Bandido2) with a round shield and a spiked club. The shield is
// the identity: hits from the front don't push it back (knockback only lands from behind), and it
// doesn't flinch out of its swing (HyperArmorWhileAttacking in the scene). It leads with a shield
// shove before the club comes down, so the swing reads as one heavy "bash, then crack".
public partial class Bandido2Escudo : MeleeEnemy
{
	[Export] public float WindupDuration = 0.45f;
	// attack plays at 14fps (Bandido2EscudoSpriteFrames.tres): shield forward on TelegraphFrame 2,
	// club landing on frame 4.
	[Export] public float HitFrameDelay = 2f / 14f;
	[Export] public float BashLungeSpeed = 180f;
	[Export] public float BashLungeTime = 0.1f;

	// Angles: 0° = front, -90° = up, +90° = down. The club arc reaches the hitbox's far edge (52).
	[ExportGroup("Trail")]
	[Export] public Vector2 ClubTrailCenter = new(6f, -6f);
	[Export] public float ClubTrailRadius = 46f;
	[Export] public float ClubTrailRadiusY = 40f;
	[Export] public float ClubTrailStartAngle = -110f;
	[Export] public float ClubTrailEndAngle = 50f;
	[Export] public float ClubTrailDuration = 0.12f;
	[Export] public Color TrailGlowColor = new(1f, 0.12f, 0.08f, 1f);
	[Export] public Color TrailCoreColor = new(1f, 0.7f, 0.55f, 1f);

	private EnemySlashTrail _clubTrail;
	private float _lungeTimer;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_clubTrail = new EnemySlashTrail { CoreWidth = 3f, GlowWidth = 10f, GlowColor = TrailGlowColor, CoreColor = TrailCoreColor, SparkAmount = 20 };
		Visual.AddChild(_clubTrail);
	}

	// Only a hit from behind moves it; the shield eats anything coming from the front.
	public override void ApplyKnockback(Vector2 direction, float force)
	{
		bool fromFront = Mathf.Sign(direction.X) == (FacingRight ? -1f : 1f);
		if (fromFront)
			return;
		base.ApplyKnockback(direction, force);
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		if (_lungeTimer > 0f)
			_lungeTimer -= (float)delta;
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		float forward = FacingRight ? 1f : -1f;
		if (_lungeTimer > 0f && CanStepTo(forward))
			return forward * BashLungeSpeed;
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
		_lungeTimer = BashLungeTime;
		_clubTrail.Play(ClubTrailCenter, ClubTrailRadius, ClubTrailStartAngle, ClubTrailEndAngle, ClubTrailDuration, ClubTrailRadiusY);
		await ToSignal(GetTree().CreateTimer(HitFrameDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;
		await base.Attack();
	}
}
