using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// Lancero de Enterbill — long spear, fights at its length.
// - Keeps you just past sword reach: walks back when you close in, steps up when you drift away.
// - Long thrust with a lunge: the point arrives further than its stance suggests.
// - If you get inside the spear, it sweeps low along the ground: jump it or eat it. The sweep is
//   lower and wider than the thrust, so it reads differently (crouch pose before it).
// - Never thrusts into an open parry: holds the drawn-back spear until the parry drops.
public partial class GuerreroEnterbillLancero : GuerreroReino
{
	[Export] public float PreferredDistance = 78f;
	[Export] public float TooCloseDistance = 52f;
	[Export] public float ThrustWindup = 0.42f;
	[Export] public float SweepWindup = 0.5f;
	[Export] public float MaxParryHold = 0.8f;

	// attack: 1-3 spear drawn back, 4 full thrust, 5-8 recover. attack2: 2 crouch, 3-5 low sweep.
	[Export] public int ThrustWindupFrame = 3;
	[Export] public int ThrustFrame = 4;
	[Export] public int SweepWindupFrame = 2;
	[Export] public int SweepFrame = 3;

	protected override async Task<bool> Kit()
	{
		bool inside = TargetDistanceX < TooCloseDistance;
		return inside ? await Sweep() : await Thrust();
	}

	private async Task<bool> Thrust()
	{
		float windup = ThrustWindup * WindupScale;
		WindupScale = 1f;
		if (!await Telegraph("attack", ThrustWindupFrame, windup))
			return false;
		// Reads the parry: keep the spear cocked while it's up toward me.
		float held = 0f;
		while (Target is { } p && PlayerReads.IsBlockingToward(p, GlobalPosition.X) && held < MaxParryHold)
		{
			if (!await Wait(0.05f))
				return false;
			held += 0.05f;
		}
		MaybeShout();
		Lunge(260f, 0.12f);
		return await Strike("attack", ThrustFrame, 8, ThrustFrame, 42f, new Vector2(54f, 14f), y: -6f, activeFrames: 2,
			trail: new TrailArc(new Vector2(-30f, -6f), 98f, -5f, 4f, 0.08f, 10f));
	}

	private async Task<bool> Sweep()
	{
		if (!await Telegraph("attack2", SweepWindupFrame, SweepWindup))
			return false;
		return await Strike("attack2", SweepWindupFrame + 1, 8, SweepFrame, 34f, new Vector2(88f, 14f), y: 22f, activeFrames: 2,
			trail: new TrailArc(new Vector2(0f, 10f), 58f, 70f, 10f, 0.12f, 16f));
	}

	protected override float MoveX(Node2D player, float distanceX, float currentVelocityX, double delta) =>
		SpacingMoveX(distanceX, currentVelocityX, PreferredDistance, TooCloseDistance * 0.8f);
}
