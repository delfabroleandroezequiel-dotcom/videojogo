using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// The arrow fired by ArrowTrap/ArrowTrapAccordion. Deliberately not the combat Projectile.cs —
// that one needs a shooter Stats to tell attacker from target and to read AttackPower, which a
// wall trap doesn't have. This is closer to Hazard.cs (own Damage/InstantKill/KnockbackForce,
// no shooter) but also needs to fly and to stop at whatever it hits, so it stays a single script
// instead of composing Hazard as a child like RollingBoulder does.
public partial class ArrowProjectile : Area2D
{
	[Export] public float Speed = 380f;
	[Export] public float Lifetime = 2.5f;
	[Export] public bool InstantKill;
	[Export] public int Damage = 15;
	[Export] public float KnockbackForce = 260f;
	// Same VixMix explosion Enemy.cs plays on death (see Explosion.cs's TargetScale), just small —
	// this is a wall-mounted trap's arrow hitting a wall, not a boss going up in flames.
	[Export] public PackedScene ExplosionScene;
	[Export] public float ExplosionScale = 0.3f;

	private Vector2 _direction = Vector2.Right;
	private bool _hit;

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
		GetTree().CreateTimer(Lifetime).Timeout += () =>
		{
			if (!_hit)
				Impact();
		};
	}

	public void Launch(Vector2 direction)
	{
		_direction = direction.Normalized();
		Rotation = _direction.Angle();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_hit)
			return;

		Position += _direction * Speed * (float)delta;
	}

	private void OnBodyEntered(Node2D body)
	{
		if (_hit)
			return;

		if (body.IsInGroup("player"))
		{
			Stats stats = body.GetNodeOrNull<Stats>("Stats");
			if (stats is not null && !stats.IsInvulnerable)
			{
				if (InstantKill)
				{
					stats.Kill();
				}
				else
				{
					stats.TakeDamage(Damage, isProjectile: true);
					if (body.HasMethod("ApplyKnockback"))
						body.Call("ApplyKnockback", _direction, KnockbackForce);
				}
			}
			Impact();
		}
		else if (body is StaticBody2D staticBody && (staticBody.CollisionLayer & PhysicsLayers.OneWayPlatforms) == 0)
		{
			// Anything solid that isn't a one-way platform (matches Projectile.cs) stops the arrow —
			// walls, floors, corridor pieces.
			Impact();
		}
	}

	private void Impact()
	{
		_hit = true;
		// Called from OnBodyEntered itself (and possibly the Lifetime timer) — Godot blocks
		// setting Monitoring synchronously while a body_entered signal is still being
		// dispatched, hence set_deferred instead of a direct assignment here.
		SetDeferred(PropertyName.Monitoring, false);
		SpawnExplosion();
		QueueFree();
	}

	private void SpawnExplosion()
	{
		if (ExplosionScene is null)
			return;

		Node explosionNode = ExplosionScene.Instantiate();
		if (explosionNode is Explosion explosion)
			explosion.TargetScale = ExplosionScale;

		GetTree().CurrentScene.AddChild(explosionNode);
		((Node2D)explosionNode).GlobalPosition = GlobalPosition;
	}
}
