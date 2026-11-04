using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// PixelLab variant of the Light Bandit (Bandido3) with a sling. Whirls it overhead as the tell and
// lets fly a pair of stones a beat apart, the second aimed where the player is moving to, so a
// single jump over the first doesn't clear the volley. Borrows MeleeEnemy only for the slot/cooldown
// plumbing (its AttackHitbox has collision_mask 0).
public partial class Bandido3Honda : MeleeEnemy
{
	[Export] public PackedScene StoneScene;
	[Export] public float StoneSpeed = 330f;
	[Export] public float StoneDamageMultiplier = 0.7f;
	[Export] public Vector2 StoneOrigin = new(14f, -24f);
	[Export] public Vector2 PlayerAimOffset = new(0f, -12f);
	[Export] public float SecondStoneDelay = 0.22f;
	[Export] public float LeadSeconds = 0.35f;
	[Export] public float MaxAimAngleDegrees = 30f;
	// The sling whirls on TelegraphFrame 4 and the stone leaves on frame 5 (14fps).
	[Export] public float WhirlDuration = 0.55f;
	[Export] public float ReleaseDelay = 1f / 14f;
	[Export] public float TooCloseDistance = 130f;

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
		float whirl = WhirlDuration * WindupScale;
		WindupScale = 1f;
		await ToSignal(GetTree().CreateTimer(whirl), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		Sprite.Play("attack");
		await ToSignal(GetTree().CreateTimer(ReleaseDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval || AttackWasInterrupted)
			return;
		Throw(lead: 0f);
		ThrowSecond();
		await base.Attack();
	}

	private async void ThrowSecond()
	{
		await ToSignal(GetTree().CreateTimer(SecondStoneDelay), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this) && !IsQueuedForRemoval && !IsStunned)
			Throw(LeadSeconds);
	}

	private void Throw(float lead)
	{
		if (StoneScene is null)
			return;
		float forward = FacingRight ? 1f : -1f;
		Vector2 origin = GlobalPosition + new Vector2(StoneOrigin.X * forward, StoneOrigin.Y);
		Vector2 direction = new(forward, 0f);
		if (PlayerRef is not null)
		{
			Vector2 target = PlayerRef.GlobalPosition + PlayerAimOffset + PlayerRef.Velocity * lead;
			Vector2 toTarget = (target - origin).Normalized();
			float angle = Mathf.Clamp(Mathf.Atan2(toTarget.Y, toTarget.X * forward),
				-Mathf.DegToRad(MaxAimAngleDegrees), Mathf.DegToRad(MaxAimAngleDegrees));
			direction = new Vector2(Mathf.Cos(angle) * forward, Mathf.Sin(angle));
		}

		var stone = StoneScene.Instantiate<ElfArrow>();
		stone.Speed = StoneSpeed;
		stone.DamageMultiplier = StoneDamageMultiplier;
		GetTree().CurrentScene.AddChild(stone);
		stone.GlobalPosition = origin;
		stone.Launch(direction, Stats);
		Sfx.PlayAt(this, "Combat/Bow", "Bow Attack");
	}
}
