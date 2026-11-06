using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// Campeón de la arena de Radem — gladiator with a golden trident. Radem's elite: everything the
// other red soldiers do, he does with showmanship and a read on you.
// - Trident chain: two thrusts stepping in (attack 4 and 9). If the first one is parried he
//   doesn't follow; if it lands or is dodged he always follows with the second.
// - Leap: when you keep your distance he jumps at you (aimed where you'll be) and drives the
//   trident into the ground where he lands (attack2 5): rock burst + a short low shockwave both ways.
// - He taunts after a whiffed parry of yours: slams the trident and goes straight into the chain.
public partial class GuerreroRademCampeon : GuerreroReino
{
	[Export] public float ThrustWindup = 0.38f;
	[Export] public float LeapMinDistance = 150f;
	[Export] public float LeapMaxDistance = 300f;
	[Export] public float LeapWindup = 0.45f;
	[Export] public float LeapHeight = 360f;

	// attack: 3 drawn back, 4 thrust, 8 drawn back, 9 thrust. attack2: 2-4 raised, 5 into ground.
	[Export] public int FirstWindupFrame = 3;
	[Export] public int FirstThrustFrame = 4;
	[Export] public int SecondWindupFrame = 8;
	[Export] public int SecondThrustFrame = 9;
	[Export] public int RaiseFrame = 3;
	[Export] public int PlungeFrame = 5;

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval || Attacking || !CanAttack || Target is null || !PlayerDetected || !IsOnFloor())
			return;
		if (TargetDistanceX > LeapMinDistance && TargetDistanceX < LeapMaxDistance && Roll() < 0.015f)
			TryAttackNow();
	}

	protected override async Task<bool> Kit()
	{
		if (TargetDistanceX > LeapMinDistance)
			return await Leap();
		return await Chain(Target is { } p && PlayerReads.WhiffedParryToward(p, GlobalPosition.X) ? 0.12f : ThrustWindup * WindupScale);
	}

	private async Task<bool> Chain(float windup)
	{
		WindupScale = 1f;
		if (!await Telegraph("attack", FirstWindupFrame, windup))
			return false;
		MaybeShout();
		var thrust = new TrailArc(new Vector2(-24f, -8f), 84f, -4f, 4f, 0.07f, 8f);
		Lunge(240f, 0.1f);
		if (!await Strike("attack", FirstThrustFrame, SecondWindupFrame - 1, FirstThrustFrame, 40f, new Vector2(58f, 16f), y: -8f, trail: thrust))
			return false;
		if (Target is { } p && PlayerReads.IsBlockingToward(p, GlobalPosition.X))
			return true;
		TurnToTarget();
		Pose("attack", SecondWindupFrame);
		FlashBody(0.18f);
		if (!await Wait(0.18f))
			return false;
		Lunge(280f, 0.12f);
		ExtraCooldown = 0.2f;
		return await Strike("attack", SecondThrustFrame, 12, SecondThrustFrame, 42f, new Vector2(60f, 16f), y: -8f, damage: 1.2f, trail: thrust);
	}

	private async Task<bool> Leap()
	{
		if (!await Strike("attack2", 1, RaiseFrame - 1, 99, 0f, Vector2.Zero, fps: 10f))
			return false;
		if (!await Telegraph("attack2", RaiseFrame, LeapWindup))
			return false;
		Bark("Ataque", 0.9f);
		float airTime = 2f * LeapHeight / Gravity;
		float landX = Target is null ? GlobalPosition.X : Target.GlobalPosition.X + Target.Velocity.X * airTime * 0.5f;
		float vx = Mathf.Clamp((landX - GlobalPosition.X) / airTime, -360f, 360f);
		if (!TryAttackHop(new Vector2(vx, -LeapHeight), 60f))
			return await Chain(ThrustWindup);
		Pose("attack2", RaiseFrame + 1);
		// Wait to leave the floor, then to land.
		for (int k = 0; k < 6 && IsOnFloor(); k++)
			if (!await Wait(1f / 30f))
				return false;
		float guard = 0f;
		while (!IsOnFloor() && guard < 2f)
		{
			if (!await Wait(1f / 30f))
				return false;
			guard += 1f / 30f;
		}
		Velocity = new Vector2(0f, Velocity.Y);
		if (!await Strike("attack2", PlungeFrame, PlungeFrame, PlungeFrame, 30f, new Vector2(50f, 50f), y: 6f, damage: 1.3f, activeFrames: 1,
			trail: new TrailArc(new Vector2(10f, -30f), 50f, -90f, 70f, 0.08f, 40f)))
			return false;
		GroundSlamFx.Impact(this, GroundPoint(30f), 1f);
		Shake(5f, 0.2f);
		HitOn(0f, new Vector2(140f, 14f), BodyBottom - 8f, 0.6f);
		if (!await Wait(1f / AttackFps))
			return false;
		AttackHitbox.Deactivate();
		ExtraCooldown = 0.5f;
		return await Strike("attack2", PlungeFrame + 1, 8, 99, 0f, Vector2.Zero, fps: 9f);
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Hopping && !Attacking)
			return;
		base.UpdateAnimation(velocity);
	}
}
