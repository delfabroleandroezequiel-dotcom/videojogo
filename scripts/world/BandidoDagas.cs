using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// PixelLab-made dual-dagger bandit (Bandido1's faction look). Fast and light: it closes in at a run
// and throws a two-cut combo — a forward slash and a rising one — each with its own crimson neon
// streak. Both cuts share one hitbox window (frames 2 to 5 of the sheet), so the combo deals its
// damage once; the second streak shows the window is still live.
public partial class BandidoDagas : MeleeEnemy
{
	[Export] public float WindupDuration = 0.3f;

	// attack plays at 14fps (BandidoDagasSpriteFrames.tres): resumes from TelegraphFrame 1 (the
	// lunge), the first cut shows on frame 2 and the second on frame 5.
	[Export] public float FirstCutDelay = 1f / 14f;
	[Export] public float SecondCutDelay = 3f / 14f;

	// Backs off at a walk when the player is right on top of it, runs everywhere else.
	[Export] public float TooCloseDistance = 22f;

	// Angles: 0° = front, -90° = up, +90° = down. Both arcs reach the AttackHitbox's far edge
	// (reach 30 + half width 28 = 58).
	[ExportGroup("Trails")]
	[Export] public Color TrailGlowColor = new(0.95f, 0.03f, 0.18f, 1f);
	[Export] public Color TrailCoreColor = new(1f, 0.82f, 0.86f, 1f);
	[Export] public float TrailCoreWidth = 1.6f;
	[Export] public float TrailGlowWidth = 6f;
	[Export] public Vector2 SlashTrailCenter = new(4f, -6f);
	[Export] public float SlashTrailRadius = 52f;
	[Export] public float SlashTrailRadiusY = 30f;
	[Export] public float SlashTrailStartAngle = -70f;
	[Export] public float SlashTrailEndAngle = 55f;
	[Export] public Vector2 RiseTrailCenter = new(6f, -4f);
	[Export] public float RiseTrailRadius = 50f;
	[Export] public float RiseTrailRadiusY = 40f;
	[Export] public float RiseTrailStartAngle = 70f;
	[Export] public float RiseTrailEndAngle = -80f;
	[Export] public float TrailDuration = 0.09f;

	private EnemySlashTrail _slashTrail;
	private EnemySlashTrail _riseTrail;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;

		_slashTrail = MakeTrail();
		_riseTrail = MakeTrail();
	}

	private EnemySlashTrail MakeTrail()
	{
		var trail = new EnemySlashTrail
		{
			CoreWidth = TrailCoreWidth,
			GlowWidth = TrailGlowWidth,
			GlowColor = TrailGlowColor,
			CoreColor = TrailCoreColor,
			SparkAmount = 8,
			SparkLifetime = 0.25,
		};
		Visual.AddChild(trail);
		return trail;
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta) =>
		SpacingMoveX(distanceX, currentVelocityX, StopDistance, TooCloseDistance);

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null)
			return;
		bool backingOff = Mathf.Abs(velocity.X) > 5f && Mathf.Sign(velocity.X) != (FacingRight ? 1f : -1f);
		string anim = Attacking ? "attack"
			: Mathf.Abs(velocity.X) <= 5f ? "idle"
			: backingOff ? "walk" : "run";
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
		_slashTrail.Play(SlashTrailCenter, SlashTrailRadius, SlashTrailStartAngle, SlashTrailEndAngle, TrailDuration, SlashTrailRadiusY);
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
		_riseTrail.Play(RiseTrailCenter, RiseTrailRadius, RiseTrailStartAngle, RiseTrailEndAngle, TrailDuration, RiseTrailRadiusY);
	}
}
