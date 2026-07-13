using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

public partial class Projectile : Area2D
{
	[Export] public float Speed = 250f;
	[Export] public float Lifetime = 3f;
	[Export] public float KnockbackForce = 200f;

	// Layer 6 / "OneWayPlatforms" in Project Settings > Layer Names > 2D Physics — those bodies
	// also carry the World bit (so the player still stands on them), so we can't tell them apart
	// from a regular wall by mask alone; check the tag bit directly instead.
	private const uint OneWayPlatformLayer = 1u << 5;

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
		if (targetStats is not null && targetStats != _shooterStats && !targetStats.IsInvulnerable)
		{
			targetStats.TakeDamage(_shooterStats.AttackPower, isProjectile: true);

			if (body.HasMethod("ApplyKnockback"))
				body.Call("ApplyKnockback", _direction, KnockbackForce);

			ImpactEffect.SpawnAt(this, GlobalPosition);
			QueueFree();
		}
		else if (body is StaticBody2D staticBody)
		{
			if ((staticBody.CollisionLayer & OneWayPlatformLayer) != 0)
				return;

			ImpactEffect.SpawnAt(this, GlobalPosition);
			QueueFree();
		}
	}
}
