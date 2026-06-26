using Godot;

namespace Metroidvania.World;

public partial class RangedEnemy : Enemy
{
	[Export] public float ShootInterval = 2f;
	[Export] public float ShootRange = 350f;
	[Export] public PackedScene ProjectileScene;

	private double _cooldown;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;

		StopDistance = ShootRange * 0.9f;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;

		base._PhysicsProcess(delta);

		_cooldown -= delta;

		Node2D player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		if (player is null)
			return;

		float distanceX = Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X);
		if (distanceX <= ShootRange && _cooldown <= 0)
		{
			Shoot(player.GlobalPosition);
			_cooldown = ShootInterval;
		}
	}

	private void Shoot(Vector2 targetPosition)
	{
		Projectile projectile = ProjectileScene.Instantiate<Projectile>();
		GetTree().CurrentScene.AddChild(projectile);
		projectile.GlobalPosition = GlobalPosition;
		projectile.Launch(targetPosition - GlobalPosition, Stats);
	}
}
