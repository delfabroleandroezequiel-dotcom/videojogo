using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// PixelLab variant of the Assassin (Bandido6) with throwing knives. Flicks a fan of knives at once
// (aimed spread, not a single line) and, if the player is closing in, hops away right after the
// throw to keep the range. Borrows MeleeEnemy only for the slot/cooldown plumbing (its AttackHitbox
// has collision_mask 0).
public partial class Bandido6Cuchillos : MeleeEnemy
{
	[Export] public PackedScene KnifeScene;
	[Export] public float KnifeSpeed = 420f;
	[Export] public float KnifeDamageMultiplier = 0.6f;
	[Export] public int KnivesPerThrow = 3;
	[Export] public float FanDegrees = 16f;
	[Export] public Vector2 ThrowOrigin = new(18f, -14f);
	[Export] public Vector2 PlayerAimOffset = new(0f, -10f);
	[Export] public float MaxAimAngleDegrees = 25f;
	// Arm cocked on TelegraphFrame 2, knives leave on frame 3 (14fps).
	[Export] public float AimDuration = 0.38f;
	[Export] public float ReleaseDelay = 1f / 14f;
	[Export] public float TooCloseDistance = 120f;
	[Export] public float EscapeDistance = 100f;
	[Export] public Vector2 EscapeHop = new(240f, -280f);

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta) =>
		SpacingMoveX(distanceX, currentVelocityX, StopDistance, TooCloseDistance);

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null)
			return;
		if (Hopping && PlayAirAnimation(velocity))
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
		ThrowFan();
		await base.Attack();
		if (!IsInstanceValid(this) || IsQueuedForRemoval || PlayerRef is null)
			return;
		float distanceX = PlayerRef.GlobalPosition.X - GlobalPosition.X;
		if (Mathf.Abs(distanceX) <= EscapeDistance)
			TryHop(new Vector2(-Mathf.Sign(distanceX) * EscapeHop.X, EscapeHop.Y));
	}

	private void ThrowFan()
	{
		if (KnifeScene is null)
			return;
		float forward = FacingRight ? 1f : -1f;
		Vector2 origin = GlobalPosition + new Vector2(ThrowOrigin.X * forward, ThrowOrigin.Y);
		float baseAngle = 0f;
		if (PlayerRef is not null)
		{
			Vector2 toPlayer = (PlayerRef.GlobalPosition + PlayerAimOffset - origin).Normalized();
			baseAngle = Mathf.Clamp(Mathf.Atan2(toPlayer.Y, toPlayer.X * forward),
				-Mathf.DegToRad(MaxAimAngleDegrees), Mathf.DegToRad(MaxAimAngleDegrees));
		}
		for (int i = 0; i < KnivesPerThrow; i++)
		{
			float t = KnivesPerThrow == 1 ? 0f : i / (float)(KnivesPerThrow - 1) - 0.5f;
			float angle = baseAngle + Mathf.DegToRad(FanDegrees * t);
			var knife = KnifeScene.Instantiate<ElfArrow>();
			knife.Speed = KnifeSpeed;
			knife.DamageMultiplier = KnifeDamageMultiplier;
			GetTree().CurrentScene.AddChild(knife);
			knife.GlobalPosition = origin;
			knife.Launch(new Vector2(Mathf.Cos(angle) * forward, Mathf.Sin(angle)), Stats);
		}
		Sfx.PlayAt(this, "Combat/Bow", "Bow Attack");
	}
}
