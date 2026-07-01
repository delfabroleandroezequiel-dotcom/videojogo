using Godot;
using Metroidvania.Player;
using Metroidvania.Save;

namespace Metroidvania.World;

public partial class Boss : Enemy
{
	[Export] public float FlySpeed = 100f;
	[Export] public float HoverAmplitude = 10f;
	[Export] public float HoverFrequency = 1.5f;
	[Export] public float AttackRange = 60f;
	[Export] public float AttackCooldown = 1.5f;
	[Export] public float AttackDuration = 0.3f;

	private Hitbox _hitbox;
	private float _hoverTimer;
	private bool _attacking;
	private bool _canAttack = true;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;

		_hitbox = GetNode<Hitbox>("AttackHitbox");
	}

	protected override bool IsDefeated() => SaveManager.Instance.IsBossDefeated(PersistenceId);

	protected override void OnDefeated()
	{
		SaveManager.Instance.MarkBossDefeated(PersistenceId);
		SpawnExplosion();
		QueueFree();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;

		_hoverTimer += (float)delta;

		Vector2 velocity;
		Node2D player = GetTree().GetFirstNodeInGroup("player") as Node2D;

		if (player is not null)
		{
			Vector2 toPlayer = player.GlobalPosition - GlobalPosition;
			float distance = toPlayer.Length();

			if (distance <= DetectionRange)
			{
				FacingRight = toPlayer.X >= 0;
				Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);

				velocity = distance > StopDistance
					? toPlayer.Normalized() * FlySpeed
					: Vector2.Zero;

				if (!_attacking && _canAttack && distance <= AttackRange)
					Attack();
			}
			else
			{
				velocity = new Vector2(0, Mathf.Cos(_hoverTimer * HoverFrequency) * HoverAmplitude * HoverFrequency);
			}
		}
		else
		{
			velocity = Vector2.Zero;
		}

		Velocity = velocity;
		MoveAndSlide();

		ApplyContactDamage();
	}

	private async void Attack()
	{
		_attacking = true;
		_canAttack = false;
		_hitbox.Position = new Vector2(FacingRight ? 30 : -30, 0);
		_hitbox.Activate(Stats);

		await ToSignal(GetTree().CreateTimer(AttackDuration), SceneTreeTimer.SignalName.Timeout);
		_hitbox.Deactivate();
		_attacking = false;

		await ToSignal(GetTree().CreateTimer(AttackCooldown), SceneTreeTimer.SignalName.Timeout);
		_canAttack = true;
	}
}
