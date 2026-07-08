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
	[Export] public PackedScene ExplosionScene;
	[Export] public string CustomPersistenceId = "";
	[Export] public LootEntry[] LootTable = System.Array.Empty<LootEntry>();

	protected Stats Stats;
	protected Node2D Visual;
	protected AnimatedSprite2D Sprite;
	protected bool FacingRight = true;

	protected bool IsQueuedForRemoval;
	protected string PersistenceId;

	protected Area2D ContactArea;
	private float _knockbackTimer;
	private Vector2 _knockbackVelocity;

	// Shared across all enemies and re-seeded once, rather than a fresh RandomNumberGenerator
	// per death: several enemies dying the same frame (e.g. an AoE) would otherwise all
	// Randomize() off the same coarse time source and roll near-identical loot results.
	private static readonly RandomNumberGenerator LootRng = new();

	static Enemy()
	{
		LootRng.Randomize();
	}

	public override void _Ready()
	{
		AddToGroup("enemy");

		PersistenceId = string.IsNullOrEmpty(CustomPersistenceId) ? GetPath().ToString() : CustomPersistenceId;
		if (IsDefeated())
		{
			IsQueuedForRemoval = true;
			QueueFree();
			return;
		}

		Stats = GetNode<Stats>("Stats");
		Visual = GetNode<Node2D>("Visual");
		Sprite = Visual.GetNodeOrNull<AnimatedSprite2D>("CharacterSprite");
		ContactArea = GetNode<Area2D>("ContactArea");
		Stats.Died += OnDefeated;

		StatBar healthBar = GetNode<StatBar>("HealthBar");
		StatBar staminaBar = GetNode<StatBar>("StaminaBar");
		healthBar.Visible = false;
		staminaBar.Visible = false;
		Stats.HealthChanged += (current, max) => healthBar.SetRatio((float)current / max);
		Stats.StaminaChanged += (current, max) => staminaBar.SetRatio((float)current / max);
		Stats.HitTaken += (isProjectile) => FlashHit();
	}

	private void FlashHit()
	{
		var tween = CreateTween();
		Visual.Modulate = new Color(2f, 0.2f, 0.2f);
		tween.TweenProperty(Visual, "modulate", Colors.White, 0.25f);
	}

	protected virtual bool IsDefeated() => SaveManager.Instance.IsCommonEnemyDefeated(PersistenceId);

	protected virtual void OnDefeated()
	{
		SaveManager.Instance.MarkCommonEnemyDefeated(PersistenceId);
		SpawnExplosion();
		SpawnLoot();
		QueueFree();
	}

	protected void SpawnExplosion()
	{
		if (ExplosionScene is null)
			return;

		Node explosionNode = ExplosionScene.Instantiate();
		if (explosionNode is Explosion explosion)
			explosion.TargetScale = ExplosionScale;

		GetTree().CurrentScene.AddChild(explosionNode);
		((Node2D)explosionNode).GlobalPosition = GlobalPosition;
	}

	protected void SpawnLoot()
	{
		if (LootTable is null || LootTable.Length == 0)
			return;

		foreach (LootEntry entry in LootTable)
		{
			if (entry?.DropScene is null || LootRng.Randf() > entry.DropChance)
				continue;

			Node dropNode = entry.DropScene.Instantiate();
			GetTree().CurrentScene.AddChild(dropNode);

			if (dropNode is Node2D dropNode2D)
				dropNode2D.GlobalPosition = GlobalPosition;

			if (dropNode is Coin coin)
				coin.Value = LootRng.RandiRange(entry.MinAmount, entry.MaxAmount);
			else if (dropNode is ItemPickup pickup && entry.RewardItem is not null)
				pickup.Item = entry.RewardItem;
		}
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

		UpdateAnimation(velocity);
		ApplyContactDamage();
	}

	protected virtual void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null) return;
		string anim = Mathf.Abs(velocity.X) > 5f ? "run" : "idle";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	protected void ApplyContactDamage()
	{
		foreach (Node body in ContactArea.GetOverlappingBodies())
		{
			if (body is not Node2D player || !player.IsInGroup("player"))
				continue;

			Stats targetStats = player.GetNodeOrNull<Stats>("Stats");
			if (targetStats is null)
				continue;

			targetStats.TakeDamage(Stats.AttackPower);
		}
	}
}
