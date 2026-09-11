using Godot;
using System.Threading.Tasks;

namespace Metroidvania.World;

// Gransin (el buen Gransin del libro), for now built as a plain enemy behaving like Bandido4 —
// same settle-then-telegraph-then-swing pattern, own script per the project's enemy-uniqueness
// convention (not a Bandido4 subclass). Uses the NpcGuardiaNix sprite set (MedievalWarrior2 pack),
// whose swing is "attack1" (4 frames) instead of a plain "attack" animation. Will later be reworked
// into a recruitable ally instead of a hostile enemy.
public partial class Gransin : MeleeEnemy
{
	[Export] public float SettleDelay = 0.3f;
	[Export] public float SettleSpeedThreshold = 10f;
	[Export] public float WindupDuration = 0.35f;

	// attack1 plays at 14fps (see MedievalWarrior2SpriteFrames.tres, 4 frames) — frame 2 (the sword
	// coming down from the raised telegraph) is where the hit actually connects.
	[Export] public float HitFrameDelay = 2f / 14f;

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

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null) return;
		string anim = Attacking ? "attack1" : (Mathf.Abs(velocity.X) > 5f ? "run" : "idle");
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	protected override async Task Attack()
	{
		Attacking = true;
		CanAttack = false;

		HoldTelegraphFrame("attack1");
		await ToSignal(GetTree().CreateTimer(WindupDuration), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		Sprite.Play("attack1");
		await ToSignal(GetTree().CreateTimer(HitFrameDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		await base.Attack();
	}
}
