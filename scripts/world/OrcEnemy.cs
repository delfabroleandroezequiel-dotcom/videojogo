using Godot;
using System.Threading.Tasks;

namespace Metroidvania.World;

// Deliberately its own script rather than a MeleeEnemy behavior tweak — even a "basic", widely
// reused enemy gets independent AI logic, same reasoning as why each boss gets its own script
// instead of sharing one template. Tuning the orc's pacing here should never risk changing how
// SpiderEnemy (the other current MeleeEnemy) feels.
public partial class OrcEnemy : MeleeEnemy
{
	// Must be standing roughly still, already in range, for this long before it's allowed to
	// commit to a swing — charging in and attacking the instant it arrives is what made this
	// "basic" enemy read as unpredictable to dodge. A beat of idle first reads as "about to do
	// something" instead.
	[Export] public float SettleDelay = 0.3f;
	[Export] public float SettleSpeedThreshold = 10f;

	// Sword-raise telegraph: holds on TelegraphFrame (see MeleeEnemy), hitbox off, for this long
	// before the actual swing plays — gives the player a real read-and-dodge window instead of the
	// hit landing the instant the animation starts.
	[Export] public float WindupDuration = 0.5f;

	private float _settleTimer;

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);

		// Read after base's own movement/attack-trigger step, so ReadyToCommitAttack (called from
		// within that step) always judges off last frame's settledness — at most one frame of lag,
		// irrelevant at a 0.3s threshold.
		if (Mathf.Abs(Velocity.X) > SettleSpeedThreshold)
			_settleTimer = 0f;
		else
			_settleTimer += (float)delta;
	}

	protected override bool ReadyToCommitAttack() => _settleTimer >= SettleDelay;

	protected override async Task Attack()
	{
		Attacking = true;
		CanAttack = false;

		HoldTelegraphFrame("attack");
		await ToSignal(GetTree().CreateTimer(WindupDuration), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		// Resume from frame 0 into the real swing — base Attack() activates the hitbox and handles
		// the rest (deactivate, cooldown, slot release) exactly as it does for any other MeleeEnemy.
		Sprite.Play("attack");
		await base.Attack();
	}
}
