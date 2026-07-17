using Godot;

namespace Metroidvania.World;

// Hunts the player like any FlyingEnemy, then commits to a full dive-bomb once in range: lunge
// in, hang at the player for a beat, and peel off into a short retreat before it's willing to
// dive again. A beast, not a swordsman — there's no separate attack hitbox here, the dive just
// puts its body where the player is and ordinary contact damage (see Enemy.ApplyContactDamage)
// does the rest. The retreat is what keeps it from just being a slow projectile — it disengages
// instead of grinding contact damage against the player forever.
public partial class BatEnemy : FlyingEnemy
{
	[Export] public float AttackRange = 56f;
	[Export] public float AttackCooldown = 1.6f;
	[Export] public float DiveSpeed = 260f;
	[Export] public float DiveDuration = 0.3f;
	[Export] public float BiteDuration = 0.18f;
	[Export] public float RecoverDuration = 0.45f;
	[Export] public float RetreatSpeed = 140f;

	private enum AttackPhase { Dive, Bite, Retreat }

	private bool _attacking;
	private bool _canAttack = true;
	private Vector2 _diveDirection;
	private float _phaseTimer;
	private AttackPhase _phase;

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;

		base._PhysicsProcess(delta);

		if (_attacking || !_canAttack || !Hunting || Player is null)
			return;

		if (Player is Metroidvania.Player.Player player && player.IsDashing)
			return;

		if (GlobalPosition.DistanceTo(Player.GlobalPosition) <= AttackRange)
			StartAttack();
	}

	protected override bool OverrideSteering(double delta)
	{
		if (!_attacking)
			return false;

		_phaseTimer -= (float)delta;
		switch (_phase)
		{
			case AttackPhase.Dive:
				GlobalPosition += _diveDirection * DiveSpeed * (float)delta;
				if (_phaseTimer <= 0f)
					EnterBitePhase();
				break;
			case AttackPhase.Bite:
				GlobalPosition += _diveDirection * DiveSpeed * 0.4f * (float)delta;
				if (_phaseTimer <= 0f)
					EnterRetreatPhase();
				break;
			case AttackPhase.Retreat:
				GlobalPosition -= _diveDirection * RetreatSpeed * (float)delta;
				if (_phaseTimer <= 0f)
					EndAttack();
				break;
		}

		Anchor = GlobalPosition;
		return true;
	}

	private void StartAttack()
	{
		_attacking = true;
		_canAttack = false;
		Sprite?.Play("attack");

		_diveDirection = (Player.GlobalPosition - GlobalPosition).Normalized();
		_phase = AttackPhase.Dive;
		_phaseTimer = DiveDuration;
	}

	private void EnterBitePhase()
	{
		_phase = AttackPhase.Bite;
		_phaseTimer = BiteDuration;
	}

	private void EnterRetreatPhase()
	{
		_phase = AttackPhase.Retreat;
		_phaseTimer = RecoverDuration;
	}

	private async void EndAttack()
	{
		_attacking = false;
		Sprite?.Play("fly");

		await ToSignal(GetTree().CreateTimer(AttackCooldown), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			_canAttack = true;
	}
}
