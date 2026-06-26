using Godot;
using Metroidvania.Shared;

namespace Metroidvania.Player;

public partial class Hitbox : Area2D
{
	private CollisionShape2D _shape;
	private Stats _attackerStats;

	public override void _Ready()
	{
		_shape = GetNode<CollisionShape2D>("CollisionShape2D");
		BodyEntered += OnBodyEntered;
	}

	public void Activate(Stats attackerStats)
	{
		_attackerStats = attackerStats;
		_shape.Disabled = false;
	}

	public void Deactivate() => _shape.Disabled = true;

	private void OnBodyEntered(Node2D body)
	{
		Stats targetStats = body.GetNodeOrNull<Stats>("Stats");
		if (targetStats is null || targetStats == _attackerStats)
			return;

		targetStats.TakeDamage(_attackerStats.AttackPower);
	}
}
