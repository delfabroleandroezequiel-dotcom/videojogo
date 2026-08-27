using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// The Bandits2 Hound only ships one real attack — a pounce-bite where the dog physically lunges
// forward as it bites (confirmed by cropping attack1's own frames), not a stationary swing like a
// sword enemy. This makes that lunge an actual position change instead of just playing the
// animation in place, so it reads as a pounce. Same finding as Bandido1: "block" in this sheet is
// a mislabeled duplicate of the run cycle (checked every row on the sheet, not just this one, since
// getting burned once on that already) — not used here.
public partial class Perrito1 : MeleeEnemy
{
	// Short — a dog doesn't wind up long, but it still crouches for a beat before pouncing so the
	// bite isn't instant the moment it's in range.
	[Export] public float WindupDuration = 0.3f;
	[Export] public int AttackTelegraphFrame = 0;
	[Export] public float LungeSpeed = 260f;
	[Export] public float LungeDuration = 0.25f;

	// Two-speed approach: trots over on noticing the player from afar, then breaks into a sprint
	// once close enough that a charge actually reads as one. RunSpeed reuses the profile's
	// MoveSpeed (already tuned for chase feel); WalkSpeed is deliberately its own slower value.
	// The sheet's own "walk"/"run" animation names were swapped when checked — HoundSpriteFrames.tres
	// has been fixed to match the real per-frame stride, not the original (wrong) label.
	[Export] public float WalkSpeed = 90f;
	[Export] public float RunTriggerDistance = 200f;

	private bool _isLunging;
	private float _lungeDirection;
	private bool _isRunningSpeed;
	private float _runSpeed;

	public override void _Ready()
	{
		base._Ready();
		_runSpeed = MoveSpeed;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;

		if (_isLunging)
		{
			Vector2 velocity = Velocity;
			velocity.Y = IsOnFloor() ? 0f : velocity.Y + Gravity * (float)delta;
			velocity.X = _lungeDirection * LungeSpeed;
			Velocity = velocity;
			MoveAndSlide();
			return;
		}

		if (!Attacking && GetTree().GetFirstNodeInGroup("player") is Node2D player)
		{
			float distance = Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X);
			_isRunningSpeed = distance <= RunTriggerDistance;
			MoveSpeed = _isRunningSpeed ? _runSpeed : WalkSpeed;
		}

		base._PhysicsProcess(delta);
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null)
			return;

		string anim = Attacking ? "attack1"
			: Mathf.Abs(velocity.X) <= 5f ? "idle"
			: _isRunningSpeed ? "run" : "walk";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	// Full replacement of MeleeEnemy.Attack (not a call to base) since the lunge needs exclusive
	// control of Velocity for its duration — replicates the same attack-slot/cooldown bookkeeping
	// base.Attack() would have done, just around a moving swing instead of a stationary one.
	protected override async Task Attack()
	{
		Attacking = true;
		CanAttack = false;

		HoldTelegraphFrame("attack1");
		Sprite.Frame = AttackTelegraphFrame;
		await ToSignal(GetTree().CreateTimer(WindupDuration), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		Sprite.Play("attack1");
		_lungeDirection = FacingRight ? 1f : -1f;
		_isLunging = true;

		AttackHitbox.Position = new Vector2(FacingRight ? AttackHitboxReach : -AttackHitboxReach, 0);
		AttackHitbox.Activate(Stats);

		try
		{
			await ToSignal(GetTree().CreateTimer(LungeDuration), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;
			_isLunging = false;
			AttackHitbox.Deactivate();

			float remainingAnimTime = Mathf.Max(0f, AttackAnimDuration - WindupDuration - LungeDuration);
			await ToSignal(GetTree().CreateTimer(remainingAnimTime), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;
			Attacking = false;
			Sprite.Position = new Vector2(Sprite.Position.X, 0f);
		}
		finally
		{
			_isLunging = false;
			EnemyCombatCoordinator.ReleaseAttackSlot();
			HoldingAttackSlot = false;
		}

		float jitter = 1f + (GD.Randf() * 2f - 1f) * AttackCooldownJitter;
		await ToSignal(GetTree().CreateTimer(AttackCooldown * jitter), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			CanAttack = true;
	}
}
