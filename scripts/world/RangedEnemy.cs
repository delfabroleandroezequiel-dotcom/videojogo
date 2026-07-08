using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

public partial class RangedEnemy : Enemy
{
	[Export] public float ShootInterval = 2f;
	[Export] public float ShootRange = 350f;
	[Export] public float ShootAnimDuration = 0.92f;
	[Export] public float ShootReleaseDelay = 0.67f;
	[Export] public float HurtAnimDuration = 0.4f;
	[Export] public float ProjectileSpeed = 250f;
	[Export] public PackedScene ProjectileScene;

	private double _cooldown;
	private bool _isShooting;
	private float _hurtTimer;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;

		StopDistance = ShootRange * 0.9f;
		Stats.HitTaken += (isProjectile) => _hurtTimer = HurtAnimDuration;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;

		base._PhysicsProcess(delta);

		if (_hurtTimer > 0f)
			_hurtTimer -= (float)delta;

		_cooldown -= delta;

		Node2D player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		if (player is null)
			return;

		float distanceX = Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X);
		if (distanceX <= ShootRange && _cooldown <= 0 && !_isShooting)
		{
			Shoot(player.GlobalPosition);
			_cooldown = ShootInterval;
		}
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null) return;
		string anim = _hurtTimer > 0f ? "hurt" : _isShooting ? "shoot" : (Mathf.Abs(velocity.X) > 5f ? "run" : "idle");
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	private async void Shoot(Vector2 targetPosition)
	{
		_isShooting = true;

		await ToSignal(GetTree().CreateTimer(ShootReleaseDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		Projectile projectile = ProjectileScene.Instantiate<Projectile>();
		GetTree().CurrentScene.AddChild(projectile);
		projectile.GlobalPosition = GlobalPosition;
		projectile.Speed = ProjectileSpeed;
		projectile.Launch(targetPosition - GlobalPosition, Stats);
		Sfx.Play(this, Sfx.FalloGolpe);

		await ToSignal(GetTree().CreateTimer(ShootAnimDuration - ShootReleaseDelay), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			_isShooting = false;
	}
}
