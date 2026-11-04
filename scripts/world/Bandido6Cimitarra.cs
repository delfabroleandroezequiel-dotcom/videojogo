using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// PixelLab variant of the Assassin (Bandido6) with a scimitar. Attacks from outside the player's
// reach: after a short crouch it dashes across the gap leaving crimson afterimages and cuts on
// arrival, so the tell to read is the crouch, not the distance.
public partial class Bandido6Cimitarra : MeleeEnemy
{
	[Export] public float WindupDuration = 0.4f;
	// attack plays at 14fps (Bandido6CimitarraSpriteFrames.tres): crouched on TelegraphFrame 4,
	// blade through on frame 5.
	[Export] public float HitFrameDelay = 1f / 14f;
	[Export] public float DashSpeed = 520f;
	[Export] public float DashTime = 0.16f;
	[Export] public float AfterimageInterval = 0.03f;
	[Export] public float AfterimageLifetime = 0.2f;
	[Export] public Color AfterimageColor = new(1f, 0.08f, 0.2f, 1f);

	// Angles: 0° = front, -90° = up, +90° = down. Diagonal cut to the hitbox's far edge (50).
	[ExportGroup("Trail")]
	[Export] public Vector2 TrailCenter = new(2f, -4f);
	[Export] public float TrailRadius = 48f;
	[Export] public float TrailRadiusY = 36f;
	[Export] public float TrailStartAngle = -75f;
	[Export] public float TrailEndAngle = 65f;
	[Export] public float TrailDuration = 0.08f;
	[Export] public Color TrailGlowColor = new(0.95f, 0.02f, 0.18f, 1f);
	[Export] public Color TrailCoreColor = new(1f, 0.85f, 0.9f, 1f);

	private EnemySlashTrail _trail;
	private float _dashTimer;
	private float _afterimageTimer;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_trail = new EnemySlashTrail { CoreWidth = 1.5f, GlowWidth = 6f, GlowColor = TrailGlowColor, CoreColor = TrailCoreColor, SparkAmount = 8 };
		Visual.AddChild(_trail);
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		if (_dashTimer <= 0f)
			return;
		_dashTimer -= (float)delta;
		_afterimageTimer -= (float)delta;
		if (_afterimageTimer <= 0f)
		{
			_afterimageTimer = AfterimageInterval;
			SpawnAfterimage();
		}
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		float forward = FacingRight ? 1f : -1f;
		if (_dashTimer > 0f)
			return CanStepTo(forward) ? forward * DashSpeed : 0f;
		return base.ComputeMoveX(player, distanceX, currentVelocityX, delta);
	}

	private void SpawnAfterimage()
	{
		Texture2D frame = Sprite?.SpriteFrames?.GetFrameTexture(Sprite.Animation, Sprite.Frame);
		if (frame is null || GetTree().CurrentScene is not Node scene)
			return;
		var ghost = new Sprite2D
		{
			Texture = frame,
			FlipH = Sprite.FlipH,
			Material = Metroidvania.Player.NeonSilhouette.CreateMaterial(AfterimageColor, 1.6f),
			Modulate = new Color(1f, 1f, 1f, 0.6f),
			ZIndex = ZIndex - 1,
		};
		scene.AddChild(ghost);
		ghost.GlobalTransform = Sprite.GlobalTransform;
		Tween fade = ghost.CreateTween();
		fade.TweenProperty(ghost, "modulate:a", 0f, AfterimageLifetime);
		fade.TweenCallback(Callable.From(ghost.QueueFree));
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null)
			return;
		string anim = Attacking ? "attack" : Mathf.Abs(velocity.X) > 5f ? "run" : "idle";
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

		_dashTimer = DashTime;
		await ToSignal(GetTree().CreateTimer(DashTime), SceneTreeTimer.SignalName.Timeout);
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
