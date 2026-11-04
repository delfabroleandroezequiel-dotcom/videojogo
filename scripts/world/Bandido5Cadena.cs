using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// PixelLab variant of the Wild Bandit (Bandido5) with a chain and hook. Swings the hook out from
// well past sword range and, if it catches the player, yanks them in toward itself — the threat is
// being dragged into its follow-up, not the hook's damage. Keeps to chain length otherwise.
public partial class Bandido5Cadena : MeleeEnemy
{
	[Export] public float WindupDuration = 0.5f;
	// attack plays at 14fps (Bandido5CadenaSpriteFrames.tres): chain wound back on TelegraphFrame 3,
	// hook flung out on frame 5.
	[Export] public float HitFrameDelay = 2f / 14f;
	[Export] public float PreferredDistance = 80f;
	[Export] public float TooCloseDistance = 50f;
	[Export] public float PullForce = 420f;
	[Export] public float PullFollowUpWindupScale = 0.5f;

	// A wide flat arc out to the hitbox's far edge (82). Angles: 0° = front, -90° = up, +90° = down.
	[ExportGroup("Trail")]
	[Export] public Vector2 TrailCenter = new(-10f, -4f);
	[Export] public float TrailRadius = 92f;
	[Export] public float TrailRadiusY = 34f;
	[Export] public float TrailStartAngle = -70f;
	[Export] public float TrailEndAngle = 20f;
	[Export] public float TrailDuration = 0.12f;
	[Export] public Color TrailGlowColor = new(0.85f, 0.85f, 0.95f, 1f);
	[Export] public Color TrailCoreColor = new(1f, 1f, 1f, 1f);

	private EnemySlashTrail _trail;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_trail = new EnemySlashTrail { CoreWidth = 1.5f, GlowWidth = 6f, GlowColor = TrailGlowColor, CoreColor = TrailCoreColor, SparkAmount = 10 };
		Visual.AddChild(_trail);
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta) =>
		SpacingMoveX(distanceX, currentVelocityX, PreferredDistance, TooCloseDistance);

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

		// The hook "caught" if the player lost health during the swing's hitbox window.
		Stats playerStats = PlayerRef?.GetNodeOrNull<Stats>("Stats");
		int healthBefore = playerStats?.CurrentHealth ?? 0;
		WatchForCatch(playerStats, healthBefore);
		await base.Attack();
	}

	private async void WatchForCatch(Stats playerStats, int healthBefore)
	{
		await ToSignal(GetTree().CreateTimer(AttackDuration + 0.02f), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval || playerStats is null || PlayerRef is null)
			return;
		if (playerStats.CurrentHealth >= healthBefore)
			return;
		Vector2 towardMe = new(Mathf.Sign(GlobalPosition.X - PlayerRef.GlobalPosition.X), -0.15f);
		PlayerRef.ApplyKnockback(towardMe.Normalized(), PullForce);
	}
}
