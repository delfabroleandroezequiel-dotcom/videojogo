using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// PixelLab-made bandit archer (Bandido1's faction look, short bow). Keeps its distance and fires
// a single arrow that leaves a crimson neon streak. It borrows MeleeEnemy only for the shared
// attack-slot / cooldown plumbing: its AttackHitbox has collision_mask 0 in the scene, so the
// base swing never hurts anyone and the arrow is the only damage it deals.
public partial class BandidoArquero : MeleeEnemy
{
	[Export] public PackedScene ArrowScene;
	[Export] public float ArrowSpeed = 360f;
	[Export] public Color ArrowTrailColor = new(1f, 0.1f, 0.15f, 1f);
	[Export] public float ArrowTrailWidth = 4f;
	// Where the arrow leaves the bow, in Visual space (mirrored with facing).
	[Export] public Vector2 ArrowOrigin = new(18f, -12f);
	[Export] public Vector2 PlayerAimOffset = new(0f, -10f);
	[Export] public float MaxAimAngleDegrees = 25f;

	// Holds the drawn bow (TelegraphFrame 3 in the scene) as the telegraph, then releases: the
	// arrow leaves on frame 5 of the 14fps attack.
	[Export] public float AimDuration = 0.5f;
	[Export] public float ReleaseDelay = 2f / 14f;

	// Steps back at a walk if the player gets this close, otherwise holds around AttackRange.
	[Export] public float TooCloseDistance = 150f;

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
		float aim = AimDuration * WindupScale;
		WindupScale = 1f;
		await ToSignal(GetTree().CreateTimer(aim), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		Sprite.Play("attack");
		await ToSignal(GetTree().CreateTimer(ReleaseDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval || AttackWasInterrupted)
			return;

		Fire();
		await base.Attack();
	}

	private void Fire()
	{
		if (ArrowScene is null)
			return;

		float forward = FacingRight ? 1f : -1f;
		Vector2 origin = GlobalPosition + new Vector2(ArrowOrigin.X * forward, ArrowOrigin.Y);
		Vector2 direction = new(forward, 0f);
		if (PlayerRef is not null)
		{
			Vector2 toPlayer = (PlayerRef.GlobalPosition + PlayerAimOffset - origin).Normalized();
			float angle = Mathf.Clamp(Mathf.Atan2(toPlayer.Y, toPlayer.X * forward),
				-Mathf.DegToRad(MaxAimAngleDegrees), Mathf.DegToRad(MaxAimAngleDegrees));
			direction = new Vector2(Mathf.Cos(angle) * forward, Mathf.Sin(angle));
		}

		var arrow = ArrowScene.Instantiate<ElfArrow>();
		arrow.Speed = ArrowSpeed;
		arrow.TrailColor = ArrowTrailColor;
		arrow.TrailWidth = ArrowTrailWidth;
		GetTree().CurrentScene.AddChild(arrow);
		arrow.GlobalPosition = origin;
		arrow.Launch(direction, Stats);
		Sfx.PlayAt(this, "Combat/Bow", "Bow Attack");
	}
}
