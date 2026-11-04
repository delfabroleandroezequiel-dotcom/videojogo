using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// PixelLab variant of the Torcher (Bandido4) with a torch and a sickle. A punisher: it rushes in
// the moment the player whiffs or ends a dash, and its sickle cut is fire — the streak cools from
// red to orange and, now and then, the torch drops a patch of GroundFire where the cut ended.
public partial class Bandido4Hoz : MeleeEnemy
{
	[Export] public float WindupDuration = 0.32f;
	// attack plays at 14fps (Bandido4HozSpriteFrames.tres): sickle cocked on TelegraphFrame 2,
	// cut lands on frame 3.
	[Export] public float HitFrameDelay = 1f / 14f;
	[Export] public float LungeRange = 160f;
	[Export] public float LungeSpeed = 300f;
	[Export] public float LungeMaxTime = 0.45f;
	[Export] public float PunishWindupScale = 0.4f;
	[Export] public PackedScene GroundFireScene;
	[Export] public float GroundFireChance = 0.35f;
	[Export] public float GroundFireForward = 40f;

	// Angles: 0° = front, -90° = up, +90° = down. Front edge = hitbox far edge (52).
	[ExportGroup("Trail")]
	[Export] public Vector2 TrailCenter = new(6f, -8f);
	[Export] public float TrailRadius = 46f;
	[Export] public float TrailRadiusY = 36f;
	[Export] public float TrailStartAngle = -80f;
	[Export] public float TrailEndAngle = 70f;
	[Export] public float TrailDuration = 0.1f;

	private EnemySlashTrail _trail;
	private bool _lunging;
	private float _lungeTimer;
	private static readonly RandomNumberGenerator Rng = new();

	static Bandido4Hoz() => Rng.Randomize();

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		var ramp = new Gradient();
		ramp.SetColor(0, new Color(0.8f, 0.05f, 0f, 0f));
		ramp.SetColor(1, new Color(1f, 0.6f, 0.1f, 1f));
		_trail = new EnemySlashTrail { CoreWidth = 3f, GlowWidth = 10f, GlowColor = Colors.White, CoreColor = new Color(1f, 0.9f, 0.6f), GlowRamp = ramp, SparkColor = new Color(1f, 0.5f, 0.1f) };
		Visual.AddChild(_trail);
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval)
			return;
		var player = PlayerRef;
		if (player is null || !PlayerDetected || Attacking || Hopping)
			return;
		float absDistance = Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X);
		if ((Whiff.JustWhiffed || Dash.SinceDashEnded < 0.1f) && absDistance <= LungeRange)
		{
			Whiff.Consume();
			_lunging = true;
			_lungeTimer = LungeMaxTime;
		}
		if (!_lunging)
			return;
		_lungeTimer -= (float)delta;
		if (absDistance <= AttackRange)
		{
			_lunging = false;
			TryAttackNow(PunishWindupScale);
		}
		else if (_lungeTimer <= 0f)
		{
			_lunging = false;
		}
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		float toward = Mathf.Sign(distanceX);
		if (_lunging && !Attacking)
			return CanStepTo(toward) ? toward * LungeSpeed : 0f;
		return base.ComputeMoveX(player, distanceX, currentVelocityX, delta);
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

		Sprite.Play("attack");
		_trail.Play(TrailCenter, TrailRadius, TrailStartAngle, TrailEndAngle, TrailDuration, TrailRadiusY);
		await ToSignal(GetTree().CreateTimer(HitFrameDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;
		if (GroundFireScene is not null && Rng.Randf() < GroundFireChance && IsOnFloor())
			DropFire();
		await base.Attack();
	}

	private void DropFire()
	{
		float forward = FacingRight ? 1f : -1f;
		var fire = GroundFireScene.Instantiate<Node2D>();
		GetTree().CurrentScene.AddChild(fire);
		fire.GlobalPosition = GlobalPosition + new Vector2(GroundFireForward * forward, 22f);
	}
}
