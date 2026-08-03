using Godot;
using System.Threading.Tasks;

namespace Metroidvania.World;

// Its own script rather than the raw MeleeEnemy — same reasoning as OrcEnemy: even a "basic"
// sword enemy gets independent AI logic instead of being a bare shared-template instance.
// Reuses OrcEnemy's settle-then-telegraph pattern (stand still for a beat, then hold the
// sword-raise pose before swinging) so it doesn't read as attacking the instant it arrives.
public partial class WarriorEnemy : MeleeEnemy
{
	[Export] public float SettleDelay = 0.3f;
	[Export] public float SettleSpeedThreshold = 10f;
	[Export] public float WindupDuration = 0.5f;

	private float _settleTimer;

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);

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

		Sprite.Play("attack");
		await base.Attack();
	}
}
