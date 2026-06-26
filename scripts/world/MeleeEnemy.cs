using Godot;
using Metroidvania.Player;

namespace Metroidvania.World;

public partial class MeleeEnemy : Enemy
{
	[Export] public float AttackRange = 40f;
	[Export] public float AttackCooldown = 1f;
	[Export] public float AttackDuration = 0.15f;
	[Export] public float WeaponRestAngle = -20f;
	[Export] public float WeaponSwingStartAngle = -70f;
	[Export] public float WeaponSwingEndAngle = 60f;

	private Hitbox _hitbox;
	private Node2D _weaponPivot;
	private bool _attacking;
	private bool _canAttack = true;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;

		StopDistance = AttackRange * 0.8f;
		_hitbox = GetNode<Hitbox>("AttackHitbox");
		_weaponPivot = GetNode<Node2D>("Visual/WeaponPivot");
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;

		base._PhysicsProcess(delta);

		if (_attacking || !_canAttack)
			return;

		Node2D player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		if (player is null)
			return;

		float distanceX = Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X);
		if (distanceX <= AttackRange)
			Attack();
	}

	private async void Attack()
	{
		_attacking = true;
		_canAttack = false;
		_hitbox.Position = new Vector2(FacingRight ? 24 : -24, 0);
		_hitbox.Activate(Stats);

		_weaponPivot.RotationDegrees = WeaponSwingStartAngle;
		Tween swingTween = GetTree().CreateTween();
		swingTween.TweenProperty(_weaponPivot, "rotation_degrees", WeaponSwingEndAngle, AttackDuration);

		await ToSignal(GetTree().CreateTimer(AttackDuration), SceneTreeTimer.SignalName.Timeout);
		_hitbox.Deactivate();
		_attacking = false;

		Tween returnTween = GetTree().CreateTween();
		returnTween.TweenProperty(_weaponPivot, "rotation_degrees", WeaponRestAngle, 0.1f);

		await ToSignal(GetTree().CreateTimer(AttackCooldown), SceneTreeTimer.SignalName.Timeout);
		_canAttack = true;
	}
}
