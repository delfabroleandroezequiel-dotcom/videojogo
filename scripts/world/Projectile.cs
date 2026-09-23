using System;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

public partial class Projectile : Area2D
{
	[Export] public float Speed = 250f;
	[Export] public float Lifetime = 3f;
	[Export] public float KnockbackForce = 200f;

	// Raised once: true when it hits a target, false when it hits a wall or its Lifetime runs out.
	// Lets smarter shooters (see SpellBrain) learn whether their shots are landing.
	public event Action<bool> Resolved;

	private Vector2 _direction = Vector2.Right;
	private Stats _shooterStats;
	private bool _resolved;

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
		GetTree().CreateTimer(Lifetime).Timeout += () =>
		{
			if (!IsInstanceValid(this))
				return;
			Resolve(false);
			QueueFree();
		};
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
		if (targetStats is not null && targetStats != _shooterStats && !targetStats.IsInvulnerable)
		{
			targetStats.TakeDamage(_shooterStats.AttackPower, isProjectile: true);

			if (body.HasMethod("ApplyKnockback"))
				body.Call("ApplyKnockback", _direction, KnockbackForce);

			ImpactEffect.SpawnAt(this, GlobalPosition);
			Resolve(true);
			QueueFree();
		}
		else if (body is StaticBody2D staticBody)
		{
			if ((staticBody.CollisionLayer & PhysicsLayers.OneWayPlatforms) != 0)
				return;

			ImpactEffect.SpawnAt(this, GlobalPosition);
			Resolve(false);
			QueueFree();
		}
	}

	private void Resolve(bool hit)
	{
		if (_resolved)
			return;
		_resolved = true;
		Resolved?.Invoke(hit);
	}
}
