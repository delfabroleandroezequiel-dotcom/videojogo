using Godot;
using System.Threading.Tasks;

namespace Metroidvania.World;

// Plain melee bandit (Incendiario/TorcherBandit) — no special ability, just the standard
// settle-then-telegraph-then-swing pattern shared by OrcEnemy/WarriorEnemy. Its own script
// rather than reusing one of those directly, same reasoning as every other enemy in this
// project: independent AI/tuning even for a "basic" one.
public partial class Bandido4 : MeleeEnemy
{
	[Export] public float SettleDelay = 0.3f;
	[Export] public float SettleSpeedThreshold = 10f;
	[Export] public float WindupDuration = 0.5f;

	// Attack.png plays at 14fps (see TorcherBanditSpriteFrames.tres) — frame 7 of the resumed
	// swing (after the telegraph hold below) is where the torch actually connects.
	[Export] public float HitFrameDelay = 7f / 14f;

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
		await ToSignal(GetTree().CreateTimer(HitFrameDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		await base.Attack();
	}
}
