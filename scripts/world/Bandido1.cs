using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// The Bandits2 Assassin ships two distinct swings (attack1/attack2) — this alternates between them
// instead of always playing one, and holds a windup pose before each so it never reads as hitting
// the instant it arrives (same settle-then-telegraph idea as WarriorEnemy/OrcEnemy). It also reacts
// to a player dash that crosses from one side to the other by breaking off and repositioning, so
// dashing past it isn't a free "get behind it" trick.
public partial class Bandido1 : MeleeEnemy
{
	[Export] public float SecondAttackChance = 0.5f;
	// How long it holds the raised-dagger windup before the swing actually fires. attack1's windup
	// (overhead stab) and attack2's (drawn-back slash) are different poses at different frame
	// indices, confirmed by cropping the sheet, not guessed.
	[Export] public float WindupDuration = 0.4f;
	[Export] public int Attack1TelegraphFrame = 1;
	[Export] public int Attack2TelegraphFrame = 2;

	// If the player dashes past it (crosses from one side to the other mid-dash — the classic
	// "dash through and hit from behind" trick), it breaks off and runs a short burst back toward
	// where the player started, re-opening the gap instead of just standing there flat-footed.
	[Export] public float RepositionSpeed = 220f;
	[Export] public float RepositionDuration = 0.3f;
	[Export] public float RepositionCooldown = 1.5f;

	private string _currentAttackAnimation = "attack1";
	private bool _isRepositioning;
	private bool _canReposition = true;
	private float _repositionDirection;
	private bool _hasPlayerSideSample;
	private bool _wasPlayerOnRight;
	private static readonly RandomNumberGenerator Rng = new();

	static Bandido1()
	{
		Rng.Randomize();
	}

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

	// Tracks which side of the assassin the player is on only while they're mid-dash, so a plain
	// walk-around doesn't count — only an actual dash crossing counts as "got past me".
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

		// Crossed sides mid-dash — retreat back toward wherever the player started from, i.e.
		// away from where they just ended up.
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

		string anim = Attacking ? _currentAttackAnimation
			: Mathf.Abs(velocity.X) > 5f ? "run" : "idle";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	protected override async Task Attack()
	{
		Attacking = true;
		CanAttack = false;

		bool useSecondAttack = Rng.Randf() < SecondAttackChance;
		_currentAttackAnimation = useSecondAttack ? "attack2" : "attack1";
		int telegraphFrame = useSecondAttack ? Attack2TelegraphFrame : Attack1TelegraphFrame;

		HoldTelegraphFrame(_currentAttackAnimation);
		Sprite.Frame = telegraphFrame;
		await ToSignal(GetTree().CreateTimer(WindupDuration), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		Sprite.Play(_currentAttackAnimation);
		await base.Attack();
	}
}
