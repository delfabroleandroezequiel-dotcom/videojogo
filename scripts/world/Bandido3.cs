using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// The Light Bandit (Bandits pack, not Bandits2) is a much smaller kit than the other two: one
// attack, no fake "block" this time (this pack doesn't even ship one), no walk animation, and its
// animation names (attack/run/idle) already match MeleeEnemy's own defaults, so no UpdateAnimation
// override is needed here. Only addition over the base class is the windup telegraph — same reason
// every other melee enemy has one, so the swing doesn't land the instant it arrives. The "Recover"
// animation (a full get-up-from-the-ground sequence) is real but unused here — it reads as a
// knockdown-recovery pose, not a per-swing animation, and wiring an actual knockdown state is more
// than this simpler bandit calls for.
public partial class Bandido3 : MeleeEnemy
{
	[Export] public float WindupDuration = 0.35f;

	protected override async Task Attack()
	{
		Attacking = true;
		CanAttack = false;

		HoldTelegraphFrame("attack");
		await ToSignal(GetTree().CreateTimer(WindupDuration), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		Sprite.Play("attack");
		await base.Attack();
	}
}
