using Godot;
using Metroidvania.Player;

namespace Metroidvania.World;

public partial class MeleeEnemy : Enemy
{
	[Export] public float AttackRange = 40f;
	[Export] public float AttackCooldown = 1f;
	[Export] public float AttackDuration = 0.3f;

	private Hitbox _hitbox;
	private bool _attacking;
	private bool _canAttack = true;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;

		StopDistance = AttackRange * 0.8f;
		_hitbox = GetNode<Hitbox>("AttackHitbox");
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

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null) return;
		string anim = _attacking ? "attack" : (Mathf.Abs(velocity.X) > 5f ? "run" : "idle");
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	private async void Attack()
	{
		_attacking = true;
		_canAttack = false;
		_hitbox.Position = new Vector2(FacingRight ? 24 : -24, 0);
		_hitbox.Activate(Stats);

		await ToSignal(GetTree().CreateTimer(AttackDuration), SceneTreeTimer.SignalName.Timeout);
		_hitbox.Deactivate();
		_attacking = false;

		await ToSignal(GetTree().CreateTimer(AttackCooldown), SceneTreeTimer.SignalName.Timeout);
		_canAttack = true;
	}
}
