using Godot;
using Metroidvania.Save;
using Metroidvania.Shared;
using Metroidvania.UI;

namespace Metroidvania.World;

public partial class Enemy : CharacterBody2D
{
	[Export] public float DetectionRange = 400f;
	[Export] public float MoveSpeed = 80f;
	[Export] public float Gravity = 900f;
	[Export] public float StopDistance = 0f;
	[Export] public float KnockbackDuration = 0.2f;
	[Export] public float ExplosionScale = 1f;

	protected Stats Stats;
	protected Node2D Visual;
	protected bool FacingRight = true;

	protected bool IsQueuedForRemoval;
	protected string PersistenceId;

	private float _knockbackTimer;
	private Vector2 _knockbackVelocity;

	public override void _Ready()
	{
		PersistenceId = GetPath().ToString();
		if (IsDefeated())
		{
			IsQueuedForRemoval = true;
			QueueFree();
			return;
		}

		Stats = GetNode<Stats>("Stats");
		Visual = GetNode<Node2D>("Visual");
		Stats.Died += OnDefeated;

		StatBar healthBar = GetNode<StatBar>("HealthBar");
		StatBar staminaBar = GetNode<StatBar>("StaminaBar");
		Stats.HealthChanged += (current, max) => healthBar.SetRatio((float)current / max);
		Stats.StaminaChanged += (current, max) => staminaBar.SetRatio((float)current / max);
	}

	protected virtual bool IsDefeated() => SaveManager.Instance.IsCommonEnemyDefeated(PersistenceId);

	protected virtual void OnDefeated()
	{
		SaveManager.Instance.MarkCommonEnemyDefeated(PersistenceId);
		SpawnExplosion();
		QueueFree();
	}

	protected void SpawnExplosion()
	{
		PackedScene explosionScene = GD.Load<PackedScene>("res://scenes/world/Explosion.tscn");
		Node explosionNode = explosionScene.Instantiate();
		if (explosionNode is Explosion explosion)
			explosion.TargetScale = ExplosionScale;

		GetTree().CurrentScene.AddChild(explosionNode);
		((Node2D)explosionNode).GlobalPosition = GlobalPosition;
	}

	public void ApplyKnockback(Vector2 direction, float force)
	{
		_knockbackVelocity = direction * force;
		_knockbackTimer = KnockbackDuration;
	}

	public override void _PhysicsProcess(double delta)
	{
		Vector2 velocity = Velocity;

		if (_knockbackTimer > 0)
		{
			_knockbackTimer -= (float)delta;
			velocity.X = _knockbackVelocity.X;
			velocity.Y = IsOnFloor() ? 0 : velocity.Y + Gravity * (float)delta;
			Velocity = velocity;
			MoveAndSlide();
			return;
		}

		if (!IsOnFloor())
			velocity.Y += Gravity * (float)delta;

		Node2D player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		if (player is not null)
		{
			float distanceX = player.GlobalPosition.X - GlobalPosition.X;
			if (Mathf.Abs(distanceX) <= DetectionRange)
			{
				FacingRight = distanceX >= 0;
				Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);

				velocity.X = Mathf.Abs(distanceX) > StopDistance
					? Mathf.Sign(distanceX) * MoveSpeed
					: Mathf.MoveToward(velocity.X, 0, MoveSpeed);
			}
			else
			{
				velocity.X = Mathf.MoveToward(velocity.X, 0, MoveSpeed);
			}
		}

		Velocity = velocity;
		MoveAndSlide();
	}
}
