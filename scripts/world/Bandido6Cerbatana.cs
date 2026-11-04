using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// PixelLab variant of the Assassin (Bandido6) with a blowpipe. Its darts barely hurt on impact but
// poison: it plays the long game, keeping far away, darting from max range and backing off,
// and it only shoots when the player isn't already poisoned-looking close (no point-blank shots).
// The dart is PoisonSpit launched flat (ArcGravity 0) with no puddle, so it reuses the poison
// damage-over-time the player already understands from spiders.
// Borrows MeleeEnemy only for the slot/cooldown plumbing (its AttackHitbox has collision_mask 0).
public partial class Bandido6Cerbatana : MeleeEnemy
{
	[Export] public PackedScene DartScene;
	[Export] public float DartSpeed = 460f;
	[Export] public int DartImpactDamage = 3;
	[Export] public int DartPoisonTick = 3;
	[Export] public float DartPoisonDuration = 3.5f;
	[Export] public Vector2 DartOrigin = new(22f, -22f);
	[Export] public Vector2 PlayerAimOffset = new(0f, -12f);
	[Export] public float MinShotDistance = 110f;
	// Pipe raised to the mask on TelegraphFrame 4, dart out on frame 5 (14fps).
	[Export] public float AimDuration = 0.55f;
	[Export] public float ReleaseDelay = 1f / 14f;
	[Export] public float TooCloseDistance = 170f;

	protected override bool ReadyToCommitAttack() =>
		PlayerRef is null || Mathf.Abs(PlayerRef.GlobalPosition.X - GlobalPosition.X) >= MinShotDistance;

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta) =>
		SpacingMoveX(distanceX, currentVelocityX, StopDistance, TooCloseDistance);

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null)
			return;
		bool backingOff = Mathf.Abs(velocity.X) > 5f && Mathf.Sign(velocity.X) != (FacingRight ? 1f : -1f);
		string anim = Attacking ? "attack" : Mathf.Abs(velocity.X) <= 5f ? "idle" : backingOff ? "walk" : "run";
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
		Blow();
		await base.Attack();
	}

	private void Blow()
	{
		if (DartScene is null)
			return;
		float forward = FacingRight ? 1f : -1f;
		Vector2 origin = GlobalPosition + new Vector2(DartOrigin.X * forward, DartOrigin.Y);
		Vector2 direction = PlayerRef is null ? new Vector2(forward, 0f)
			: (PlayerRef.GlobalPosition + PlayerAimOffset - origin).Normalized();
		if (Mathf.Sign(direction.X) != forward)
			direction = new Vector2(forward, 0f);

		var dart = DartScene.Instantiate<PoisonSpit>();
		dart.ArcGravity = 0f;
		dart.PuddleScene = null;
		dart.ContactDamage = DartImpactDamage;
		dart.PoisonTickDamage = DartPoisonTick;
		dart.PoisonDuration = DartPoisonDuration;
		dart.Scale = new Vector2(0.6f, 0.35f);
		GetTree().CurrentScene.AddChild(dart);
		dart.GlobalPosition = origin;
		dart.Launch(direction * DartSpeed, Stats);
	}
}
