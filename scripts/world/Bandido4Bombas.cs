using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// PixelLab variant of the Torcher (Bandido4) that throws clay fire bombs. Lobs them on an arc timed
// to land where the player is standing, and every bomb that lands leaves fire on the ground
// (BombaFuego -> GroundFire), so it herds the player instead of chasing them. Keeps its distance and
// borrows MeleeEnemy only for the slot/cooldown plumbing (its AttackHitbox has collision_mask 0).
public partial class Bandido4Bombas : MeleeEnemy
{
	[Export] public PackedScene BombScene;
	[Export] public float FlightTime = 0.75f;
	[Export] public float BombGravity = 700f;
	[Export] public Vector2 ThrowOrigin = new(10f, -30f);
	[Export] public float MaxThrowSpeedX = 420f;
	// The lit bomb is held up on TelegraphFrame 1 and leaves the hand on frame 2 (14fps).
	[Export] public float AimDuration = 0.5f;
	[Export] public float ReleaseDelay = 1f / 14f;
	[Export] public float TooCloseDistance = 120f;

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
		Throw();
		await base.Attack();
	}

	// Solves the arc so the bomb reaches the player's current feet position after FlightTime.
	private void Throw()
	{
		if (BombScene is null || PlayerRef is null)
			return;
		float forward = FacingRight ? 1f : -1f;
		Vector2 origin = GlobalPosition + new Vector2(ThrowOrigin.X * forward, ThrowOrigin.Y);
		Vector2 delta = PlayerRef.GlobalPosition - origin;
		float t = FlightTime;
		float vx = Mathf.Clamp(delta.X / t, -MaxThrowSpeedX, MaxThrowSpeedX);
		float vy = (delta.Y - 0.5f * BombGravity * t * t) / t;

		var bomb = BombScene.Instantiate<BombaFuego>();
		bomb.ArcGravity = BombGravity;
		bomb.ContactDamage = Mathf.Max(1, Stats.AttackPower);
		GetTree().CurrentScene.AddChild(bomb);
		bomb.GlobalPosition = origin;
		bomb.Launch(new Vector2(vx, vy), Stats);
	}
}
