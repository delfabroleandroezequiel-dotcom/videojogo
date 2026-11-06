using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// Caballero de Enterbill — longsword knight, trained in the royal school. A three-beat combo with a
// deliberate pause in the middle, built to punish panic:
//   1. overhead chop (attack 1-4),
//   2. the pause: if you rolled or dashed through the chop, it waits for your evade to END and then
//      goes, so rolling on reflex eats beat 3; if you stood your ground, the pause is short,
//   3. horizontal cut into a forward thrust (attack2).
// It stops after beat 1 when you're already out of reach (it doesn't swing at air).
public partial class GuerreroEnterbillCaballero : GuerreroReino
{
	[Export] public float ChopWindup = 0.5f;
	[Export] public float ShortPause = 0.22f;
	[Export] public float MaxEvadeWait = 0.7f;
	[Export] public float FollowUpRange = 110f;

	// attack: 1-2 sword raised, 3 chop smear, 4-8 recover. attack2: 1 cocked, 2 horizontal smear,
	// 4-5 thrust extended.
	[Export] public int ChopWindupFrame = 2;
	[Export] public int ChopFrame = 3;
	[Export] public int CutFrame = 2;
	[Export] public int ThrustFrame = 4;

	protected override async Task<bool> Kit()
	{
		float windup = ChopWindup * WindupScale;
		WindupScale = 1f;
		if (!await Telegraph("attack", ChopWindupFrame, windup))
			return false;
		MaybeShout();
		Lunge(140f, 0.1f);
		if (!await Strike("attack", ChopFrame, 4, ChopFrame, 36f, new Vector2(52f, 56f), y: -6f,
			trail: new TrailArc(new Vector2(0f, -4f), 50f, -100f, 60f, 0.1f, 44f)))
			return false;

		// The pause that reads the player.
		float waited = 0f;
		while (Target is { } p && (p.IsRolling || p.IsDashing) && waited < MaxEvadeWait)
		{
			if (!await Wait(0.04f))
				return false;
			waited += 0.04f;
		}
		Pose("attack2", 1);
		FlashBody(ShortPause);
		if (!await Wait(ShortPause))
			return false;
		if (TargetDistanceX > FollowUpRange)
			return true;

		TurnToTarget();
		Lunge(180f, 0.1f);
		if (!await Strike("attack2", CutFrame, 3, CutFrame, 38f, new Vector2(60f, 34f), y: -6f,
			trail: new TrailArc(new Vector2(4f, -6f), 54f, -40f, 30f, 0.09f, 30f)))
			return false;
		Lunge(220f, 0.1f);
		ExtraCooldown = 0.3f;
		return await Strike("attack2", ThrustFrame, 8, ThrustFrame, 40f, new Vector2(56f, 14f), y: -8f, damage: 1.2f,
			trail: new TrailArc(new Vector2(-20f, -8f), 80f, -4f, 4f, 0.08f, 8f));
	}
}
