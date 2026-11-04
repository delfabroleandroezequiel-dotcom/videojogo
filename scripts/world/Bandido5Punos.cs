using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// PixelLab variant of the Wild Bandit (Bandido5) that fights with iron knuckles. A boxer: it sits
// right on top of the player, barely winds up and throws a jab-hook pair, then bounces back a step
// on its toes to reset instead of standing in front of the player's counter.
public partial class Bandido5Punos : MeleeEnemy
{
	[Export] public float WindupDuration = 0.18f;
	// attack plays at 14fps (Bandido5PunosSpriteFrames.tres) from TelegraphFrame 0: jab out on
	// frame 1, hook on frame 3 — one hitbox window covers both.
	[Export] public float JabDelay = 1f / 14f;
	[Export] public float HookDelay = 3f / 14f;
	[Export] public Vector2 BounceHop = new(160f, -170f);

	// Short, tight arcs right in front. Angles: 0° = front, -90° = up, +90° = down.
	[ExportGroup("Trails")]
	[Export] public Color TrailGlowColor = new(1f, 0.35f, 0.05f, 1f);
	[Export] public Color TrailCoreColor = new(1f, 0.85f, 0.6f, 1f);
	[Export] public Vector2 JabCenter = new(-4f, -10f);
	[Export] public float JabRadius = 44f;
	[Export] public float JabStartAngle = -6f;
	[Export] public float JabEndAngle = 6f;
	[Export] public Vector2 HookCenter = new(10f, -8f);
	[Export] public float HookRadius = 30f;
	[Export] public float HookStartAngle = -100f;
	[Export] public float HookEndAngle = 40f;
	[Export] public float TrailDuration = 0.07f;

	private EnemySlashTrail _jabTrail;
	private EnemySlashTrail _hookTrail;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_jabTrail = MakeTrail();
		_hookTrail = MakeTrail();
	}

	private EnemySlashTrail MakeTrail()
	{
		var trail = new EnemySlashTrail { CoreWidth = 2f, GlowWidth = 7f, GlowColor = TrailGlowColor, CoreColor = TrailCoreColor, SparkAmount = 8 };
		Visual.AddChild(trail);
		return trail;
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null)
			return;
		if (Hopping && PlayAirAnimation(velocity))
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
		_jabTrail.Play(JabCenter, JabRadius, JabStartAngle, JabEndAngle, TrailDuration);
		PlayHook();
		await ToSignal(GetTree().CreateTimer(JabDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;
		await base.Attack();
		if (!IsInstanceValid(this) || IsQueuedForRemoval || PlayerRef is null)
			return;
		float away = -Mathf.Sign(PlayerRef.GlobalPosition.X - GlobalPosition.X);
		TryHop(new Vector2(away * BounceHop.X, BounceHop.Y));
	}

	private async void PlayHook()
	{
		await ToSignal(GetTree().CreateTimer(HookDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval || !Attacking)
			return;
		_hookTrail.Play(HookCenter, HookRadius, HookStartAngle, HookEndAngle, TrailDuration);
	}
}
