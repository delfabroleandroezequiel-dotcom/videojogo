using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

public partial class Projectile : Area2D
{
	[Export] public float Speed = 250f;
	[Export] public float Lifetime = 3f;
	[Export] public float KnockbackForce = 200f;

	private Vector2 _direction = Vector2.Right;
	private Stats _shooterStats;

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
		GetTree().CreateTimer(Lifetime).Timeout += QueueFree;
	}

	public void Launch(Vector2 direction, Stats shooterStats)
	{
		_direction = direction.Normalized();
		_shooterStats = shooterStats;
		Rotation = _direction.Angle();
	}

	public override void _PhysicsProcess(double delta)
	{
		Position += _direction * Speed * (float)delta;
	}

	private void OnBodyEntered(Node2D body)
	{
		Stats targetStats = body.GetNodeOrNull<Stats>("Stats");
		if (targetStats is not null && targetStats != _shooterStats)
		{
			targetStats.TakeDamage(_shooterStats.AttackPower);

			if (body.HasMethod("ApplyKnockback"))
				body.Call("ApplyKnockback", _direction, KnockbackForce);

			QueueFree();
		}
		else if (body is StaticBody2D)
		{
			QueueFree();
		}
	}
}
