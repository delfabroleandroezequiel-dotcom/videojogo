using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// PixelLab variant of the Heavy Bandit (Bandido7) with a halberd. Holds the player at polearm
// length and answers a jump with the blade: its sweep is low and wide, but if the player is in the
// air above its reach it swaps to a rising cut instead (anti-air), so hopping over the sweep
// isn't a free approach.
public partial class Bandido7Alabarda : MeleeEnemy
{
	[Export] public float WindupDuration = 0.5f;
	// attack plays at 14fps (Bandido7AlabardaSpriteFrames.tres): halberd drawn back on
	// TelegraphFrame 3, blade sweeping out on frame 4.
	[Export] public float HitFrameDelay = 1f / 14f;
	[Export] public float PreferredDistance = 75f;
	[Export] public float TooCloseDistance = 45f;
	[Export] public float AntiAirRange = 90f;

	// Angles: 0° = front, -90° = up, +90° = down. Both reach the hitbox's far edge (64).
	[ExportGroup("Trails")]
	[Export] public Color TrailGlowColor = new(0.45f, 0.75f, 1f, 1f);
	[Export] public Color TrailCoreColor = new(0.9f, 0.97f, 1f, 1f);
	[Export] public Vector2 SweepCenter = new(0f, -2f);
	[Export] public float SweepRadius = 64f;
	[Export] public float SweepRadiusY = 22f;
	[Export] public float SweepStartAngle = -60f;
	[Export] public float SweepEndAngle = 70f;
	[Export] public Vector2 RisingCenter = new(4f, -10f);
	[Export] public float RisingRadius = 58f;
	[Export] public float RisingStartAngle = 30f;
	[Export] public float RisingEndAngle = -110f;
	[Export] public float TrailDuration = 0.12f;

	private EnemySlashTrail _trail;
	private bool _antiAir;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_trail = new EnemySlashTrail { CoreWidth = 2.5f, GlowWidth = 9f, GlowColor = TrailGlowColor, CoreColor = TrailCoreColor, SparkAmount = 14 };
		Visual.AddChild(_trail);
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		var player = PlayerRef;
		if (IsQueuedForRemoval || player is null || Attacking || !PlayerDetected)
			return;
		bool playerAbove = !player.IsOnFloor() && player.GlobalPosition.Y < GlobalPosition.Y - 20f;
		if (playerAbove && Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X) <= AntiAirRange)
		{
			_antiAir = true;
			if (!TryAttackNow(0.5f))
				_antiAir = false;
		}
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
		bool antiAir = _antiAir;
		_antiAir = false;
		HoldTelegraphFrame("attack");
		float windup = WindupDuration * WindupScale;
		WindupScale = 1f;
		await ToSignal(GetTree().CreateTimer(windup), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		Sprite.Play("attack");
		if (antiAir)
			_trail.Play(RisingCenter, RisingRadius, RisingStartAngle, RisingEndAngle, TrailDuration);
		else
			_trail.Play(SweepCenter, SweepRadius, SweepStartAngle, SweepEndAngle, TrailDuration, SweepRadiusY);
		await ToSignal(GetTree().CreateTimer(HitFrameDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;
		await base.Attack();
	}
}
