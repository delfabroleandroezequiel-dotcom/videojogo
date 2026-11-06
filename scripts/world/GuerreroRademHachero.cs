using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// Hachero de Radem — a brute in spiked red plate with a huge bearded axe. Doesn't flinch while
// swinging (HyperArmorWhileAttacking).
// - Charge: from far away he lowers his head and runs at you in a straight line (long red flash
//   first). The run itself hurts; it ends in a spin when he reaches you or runs out of track.
//   Jump over him or roll through — he can't turn mid-charge.
// - Spin: a full turn with the axe, front then back. Being behind him isn't safe on this one.
// - Overhead chop: raised high, slammed into the ground (rock burst). Slowest tell, biggest hit.
public partial class GuerreroRademHachero : GuerreroReino
{
	[Export] public float ChargeMinDistance = 140f;
	[Export] public float ChargeWindup = 0.55f;
	[Export] public float ChargeSpeed = 330f;
	[Export] public float ChargeMaxTime = 0.9f;
	[Export] public float SpinWindup = 0.45f;
	[Export] public float ChopWindup = 0.7f;

	// attack (spin): 1 coiled, 2 axe in front, 4-5 axe behind. attack2: 2-4 raised, 6 into ground.
	[Export] public int SpinWindupFrame = 1;
	[Export] public int SpinFrontFrame = 2;
	[Export] public int SpinBackFrame = 4;
	[Export] public int ChopWindupFrame = 4;
	[Export] public int ChopFrame = 6;

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval || Attacking || !CanAttack || Target is null || !PlayerDetected)
			return;
		if (TargetDistanceX > ChargeMinDistance && TargetDistanceX < ChargeMinDistance * 2.6f && IsOnFloor()
			&& Mathf.Abs(Target.GlobalPosition.Y - GlobalPosition.Y) < 40f && Roll() < 0.02f)
			TryAttackNow();
	}

	protected override async Task<bool> Kit()
	{
		if (TargetDistanceX > ChargeMinDistance)
			return await Charge();
		return Roll() < 0.5f ? await Spin(SpinWindup * WindupScale) : await Chop();
	}

	private async Task<bool> Charge()
	{
		if (!await Telegraph("walk", 3, ChargeWindup))
			return false;
		Bark("Ataque", 0.9f);
		Sprite.Play("run");
		Sprite.SpeedScale = 1.6f;
		float t = 0f;
		HitOn(16f, new Vector2(40f, 50f), -4f, 0.9f);
		try
		{
			while (t < ChargeMaxTime && TargetDistanceX > 46f && CanStepTo(Forward))
			{
				Lunge(ChargeSpeed, 0.1f);
				if (!await Wait(0.05f))
					return false;
				t += 0.05f;
				if (TargetInFront == false)
					break;
				if ((int)(t * 20f) % 3 == 0)
					GroundSlamFx.Chip(this, GroundPoint(-10f), 0.3f);
			}
		}
		finally
		{
			Sprite.SpeedScale = 1f;
			AttackHitbox.Deactivate();
			StopLunge();
		}
		return await Spin(0.12f);
	}

	private async Task<bool> Spin(float windup)
	{
		WindupScale = 1f;
		if (!await Telegraph("attack", SpinWindupFrame, windup))
			return false;
		MaybeShout();
		if (!await Strike("attack", SpinFrontFrame, SpinFrontFrame + 1, SpinFrontFrame, 36f, new Vector2(60f, 40f), y: -6f,
			trail: new TrailArc(new Vector2(0f, -6f), 52f, -40f, 60f, 0.08f, 22f)))
			return false;
		// The axe comes round behind him.
		Trail.Play(new Vector2(0f, -6f), 52f, 120f, 220f, 0.08f, 22f);
		HitOn(-36f, new Vector2(60f, 40f), -6f);
		if (!await Strike("attack", SpinBackFrame, 8, 99, 0f, Vector2.Zero))
			return false;
		ExtraCooldown = 0.3f;
		return true;
	}

	private async Task<bool> Chop()
	{
		if (!await Strike("attack2", 1, ChopWindupFrame - 1, 99, 0f, Vector2.Zero, fps: 9f))
			return false;
		if (!await Telegraph("attack2", ChopWindupFrame, ChopWindup))
			return false;
		Bark("Ataque", 0.9f);
		if (!await Strike("attack2", ChopWindupFrame + 1, ChopFrame, ChopFrame, 34f, new Vector2(46f, 60f), damage: 1.5f, activeFrames: 1,
			trail: new TrailArc(new Vector2(-2f, -10f), 54f, -110f, 50f, 0.1f, 48f)))
			return false;
		GroundSlamFx.Impact(this, GroundPoint(40f), 1.1f);
		Shake(6f, 0.22f);
		ExtraCooldown = 0.6f;
		return await Strike("attack2", ChopFrame + 1, 8, 99, 0f, Vector2.Zero, fps: 8f);
	}

	protected override float MoveX(Node2D player, float distanceX, float currentVelocityX, double delta) =>
		Attacking ? Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed) : SpacingMoveX(distanceX, currentVelocityX, StopDistance, 0f, 0.85f);
}
