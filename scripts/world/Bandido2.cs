using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// Same shape as Bandido1 (Assassin): windup telegraph before the swing, reposition when the player
// dashes past. The Bandits2 Raider only ships one real attack though (an overhead axe chop — no
// second swing like the Assassin's dagger), so there's no alternation here, just the single
// animation. "block" on this sheet is fake too (checked, same near-transparent idle-pose fingerprint
// as the Assassin's and the Hound's) — not used.
public partial class Bandido2 : MeleeEnemy
{
	// The axe windup (fully raised overhead) reads clearly at frame index 2, confirmed by cropping
	// the sheet — index 1 is still mid-raise, index 2 is the held-high peak right before the chop.
	[Export] public float WindupDuration = 0.4f;
	[Export] public int AttackTelegraphFrame = 2;

	// Same dash-cross reposition as Bandido1 — breaks off and retreats a short burst if the player
	// dashes past it, instead of standing there while they circle behind.
	[Export] public float RepositionSpeed = 220f;
	[Export] public float RepositionDuration = 0.3f;
	[Export] public float RepositionCooldown = 1.5f;

	private bool _isRepositioning;
	private bool _canReposition = true;
	private float _repositionDirection;
	private bool _hasPlayerSideSample;
	private bool _wasPlayerOnRight;

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;

		if (_isRepositioning)
		{
			Vector2 velocity = Velocity;
			velocity.Y = IsOnFloor() ? 0f : velocity.Y + Gravity * (float)delta;
			velocity.X = _repositionDirection * RepositionSpeed;
			Velocity = velocity;
			MoveAndSlide();

			if (Sprite is not null && Sprite.Animation != "run")
				Sprite.Play("run");
			return;
		}

		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval)
			return;

		CheckPlayerDashCross();
	}

	private void CheckPlayerDashCross()
	{
		if (_isRepositioning || !_canReposition)
			return;

		if (GetTree().GetFirstNodeInGroup("player") is not Metroidvania.Player.Player player || !player.IsDashing)
		{
			_hasPlayerSideSample = false;
			return;
		}

		bool isOnRight = player.GlobalPosition.X > GlobalPosition.X;
		if (!_hasPlayerSideSample)
		{
			_wasPlayerOnRight = isOnRight;
			_hasPlayerSideSample = true;
			return;
		}

		if (isOnRight == _wasPlayerOnRight)
			return;

		float direction = _wasPlayerOnRight ? 1f : -1f;
		_ = RunReposition(direction);
	}

	private async Task RunReposition(float direction)
	{
		_isRepositioning = true;
		_canReposition = false;
		_repositionDirection = direction;
		CanAttack = false;

		await ToSignal(GetTree().CreateTimer(RepositionDuration), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		_isRepositioning = false;
		CanAttack = true;

		await ToSignal(GetTree().CreateTimer(RepositionCooldown), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			_canReposition = true;
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null)
			return;

		string anim = Attacking ? "attack1" : Mathf.Abs(velocity.X) > 5f ? "run" : "idle";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

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
		await base.Attack();
	}
}
