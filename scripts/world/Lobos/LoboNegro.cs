using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// Lobo Negro — the stalker. While the player faces it, it creeps around at StalkDistance with its
// low "stalk" walk and doesn't commit: it only bites if cornered up close. The moment the player
// turns his back (fighting another wolf, walking away) it sprints in and bites from behind with a
// short wind-up and extra damage. Turning to face it in time is the counter: it aborts the rush
// and goes back to stalking. A whiffed swing nearby is punished the same way.
public partial class LoboNegro : LoboBase
{
	[ExportGroup("Acecho")]
	[Export] public float StalkDistance = 150f;
	[Export] public float StalkSpeed = 55f;
	[Export] public float BackBiteDamage = 1.5f;
	[Export] public float BackBiteWindupScale = 0.45f;
	[Export] public float PunishRange = 170f;

	protected override float MoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		float abs = Mathf.Abs(distanceX);
		float toward = Mathf.Sign(distanceX);
		if (abs > StalkDistance + 60f)
			return CanStepTo(toward) ? toward * MoveSpeed * SpeedMul : 0f;
		// In the stalking band: creep to keep StalkDistance, sliding in/out slowly.
		if (abs > StalkDistance + 10f && CanStepTo(toward))
			return toward * StalkSpeed * SpeedMul;
		if (abs < StalkDistance - 30f && CanStepTo(-toward))
			return -toward * StalkSpeed * SpeedMul;
		return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
	}

	protected override string MoveAnimation(float speed) => speed <= StalkSpeed * 1.4f ? "stalk" : "run";

	protected override void Think(float dt)
	{
		if (!CanAttack || Target is null || TargetDistanceX > PunishRange)
			return;
		if (Whiff.JustWhiffed || TargetBackTurned)
		{
			Whiff.Consume();
			TryAttackNow(BackBiteWindupScale);
		}
	}

	protected override async Task<bool> Kit()
	{
		if (TargetBackTurned || KitWindupScale < 1f)
		{
			// Sprint in; if he turns around before it gets there, it peels off.
			bool punish = KitWindupScale < 1f;
			StartRush(1.1f);
			while (TargetDistanceX > BiteRange * 0.7f)
			{
				TurnToTarget();
				if (!await WaitPhysicsFrame())
					return false;
				bool spotted = !TargetBackTurned && !punish;
				if (spotted || !Rushing || TargetDistanceX > PunishRange + 40f)
				{
					StopRush();
					NextCooldown = 0.4f;
					return true;
				}
			}
			StopRush();
			return await Bite(BackBiteWindupScale, TargetBackTurned ? BackBiteDamage : 1f);
		}

		// Faced: only bites when cornered right on top of it, otherwise keeps stalking.
		if (TargetDistanceX <= BiteRange * 0.8f)
			return await Bite();
		NextCooldown = 0.25f;
		return true;
	}
}
