using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// PixelLab variant of the Raider (Bandido2) with a crossbow. One heavy, very fast bolt and then a
// long reload: it's the bandit that punishes standing still at range and rewards rushing it during
// the reload. Like BandidoArquero it borrows MeleeEnemy only for the attack-slot/cooldown plumbing
// (its AttackHitbox has collision_mask 0, so the base swing never hurts anyone).
public partial class Bandido2Ballesta : MeleeEnemy
{
	[Export] public PackedScene BoltScene;
	[Export] public float BoltSpeed = 540f;
	[Export] public float BoltDamageMultiplier = 1.6f;
	[Export] public Color BoltTrailColor = new(1f, 0.15f, 0.1f, 1f);
	[Export] public Vector2 BoltOrigin = new(22f, -8f);
	[Export] public Vector2 PlayerAimOffset = new(0f, -10f);
	[Export] public float MaxAimAngleDegrees = 20f;
	// The crossbow is shouldered on TelegraphFrame 2; the bolt leaves on frame 4 (14fps).
	[Export] public float AimDuration = 0.6f;
	[Export] public float ReleaseDelay = 2f / 14f;
	[Export] public float TooCloseDistance = 140f;

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
		Fire();
		await base.Attack();
	}

	private void Fire()
	{
		if (BoltScene is null)
			return;
		float forward = FacingRight ? 1f : -1f;
		Vector2 origin = GlobalPosition + new Vector2(BoltOrigin.X * forward, BoltOrigin.Y);
		Vector2 direction = new(forward, 0f);
		if (PlayerRef is not null)
		{
			Vector2 toPlayer = (PlayerRef.GlobalPosition + PlayerAimOffset - origin).Normalized();
			float angle = Mathf.Clamp(Mathf.Atan2(toPlayer.Y, toPlayer.X * forward),
				-Mathf.DegToRad(MaxAimAngleDegrees), Mathf.DegToRad(MaxAimAngleDegrees));
			direction = new Vector2(Mathf.Cos(angle) * forward, Mathf.Sin(angle));
		}

		var bolt = BoltScene.Instantiate<ElfArrow>();
		bolt.Speed = BoltSpeed;
		bolt.DamageMultiplier = BoltDamageMultiplier;
		bolt.TrailColor = BoltTrailColor;
		bolt.TrailWidth = 4f;
		GetTree().CurrentScene.AddChild(bolt);
		bolt.GlobalPosition = origin;
		bolt.Launch(direction, Stats);
		Sfx.PlayAt(this, "Combat/Bow", "Bow Attack");
	}
}
