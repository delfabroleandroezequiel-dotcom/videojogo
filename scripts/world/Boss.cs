using Godot;
using Metroidvania.Player;
using Metroidvania.Save;

namespace Metroidvania.World;

public partial class Boss : Enemy
{
	[Export] public float AttackRange = 60f;
	[Export] public float AttackCooldown = 1.5f;
	[Export] public float AttackHitboxDelay = 0.12f;
	[Export] public float AttackDuration = 0.22f;
	[Export] public float AttackAnimDuration = 0.5f;
	[Export] public float AttackHitboxReach = 55f;
	[Export] public float WanderInterval = 2.5f;
	[Export] public float WanderRadius = 120f;
	[Export] public float WanderSettleDistance = 8f;
	[Export] public float DodgeChance = 0.5f;
	[Export] public float DodgeSpeed = 320f;
	[Export] public float DodgeDuration = 0.25f;
	[Export] public float DodgeCooldown = 1.2f;

	private Hitbox _hitbox;
	private bool _attacking;
	private bool _canAttack = true;
	private bool _isDodging;
	private bool _canDodge = true;
	private float _dodgeDirection;
	private float _wanderTimer;
	private float _wanderOffsetX;
	private readonly RandomNumberGenerator _rng = new();

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;

		_hitbox = GetNode<Hitbox>("AttackHitbox");
		Stats.HitTaken += OnHitTaken;

		_rng.Randomize();
		PickWanderOffset();
	}

	private void PickWanderOffset()
	{
		_wanderOffsetX = _rng.RandfRange(-WanderRadius, WanderRadius);
		_wanderTimer = WanderInterval;
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

		Vector2 velocity = Velocity;

		if (!IsOnFloor())
			velocity.Y += Gravity * (float)delta;
		else
			velocity.Y = 0;

		if (_isDodging)
		{
			velocity.X = _dodgeDirection * DodgeSpeed;
			Velocity = velocity;
			MoveAndSlide();
			UpdateAnimation(velocity);
			return;
		}

		_wanderTimer -= (float)delta;
		if (_wanderTimer <= 0f)
			PickWanderOffset();

		Node2D player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		if (player is not null)
		{
			float distanceToPlayer = player.GlobalPosition.X - GlobalPosition.X;

			if (Mathf.Abs(distanceToPlayer) <= DetectionRange)
			{
				FacingRight = distanceToPlayer >= 0;
				Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);

				float targetX = player.GlobalPosition.X + _wanderOffsetX;
				float distanceToTarget = targetX - GlobalPosition.X;

				velocity.X = Mathf.Abs(distanceToTarget) > WanderSettleDistance
					? Mathf.Sign(distanceToTarget) * MoveSpeed
					: Mathf.MoveToward(velocity.X, 0, MoveSpeed);

				if (!_attacking && _canAttack && Mathf.Abs(distanceToPlayer) <= AttackRange)
					Attack();
			}
			else
			{
				velocity.X = Mathf.MoveToward(velocity.X, 0, MoveSpeed);
			}
		}
		else
		{
			velocity.X = Mathf.MoveToward(velocity.X, 0, MoveSpeed);
		}

		Velocity = velocity;
		MoveAndSlide();

		UpdateAnimation(velocity);
		ApplyContactDamage();
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null) return;
		string anim = _isDodging ? "dash" : _attacking ? "attack1" : (Mathf.Abs(velocity.X) > 5f ? "run" : "idle");
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	private void OnHitTaken(bool isProjectile)
	{
		if (_isDodging || !_canDodge || _attacking)
			return;

		if (_rng.Randf() > DodgeChance)
			return;

		Node2D player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		_dodgeDirection = player is not null && player.GlobalPosition.X > GlobalPosition.X ? -1f : 1f;
		StartDodge();
	}

	private async void StartDodge()
	{
		_isDodging = true;
		_canDodge = false;
		Stats.ExternalInvulnerable = true;

		await ToSignal(GetTree().CreateTimer(DodgeDuration), SceneTreeTimer.SignalName.Timeout);
		_isDodging = false;
		Stats.ExternalInvulnerable = false;

		await ToSignal(GetTree().CreateTimer(DodgeCooldown), SceneTreeTimer.SignalName.Timeout);
		_canDodge = true;
	}

	private async void Attack()
	{
		_attacking = true;
		_canAttack = false;

		if (AttackHitboxDelay > 0f)
			await ToSignal(GetTree().CreateTimer(AttackHitboxDelay), SceneTreeTimer.SignalName.Timeout);

		_hitbox.Position = new Vector2(FacingRight ? AttackHitboxReach : -AttackHitboxReach, 0);
		_hitbox.Activate(Stats);

		await ToSignal(GetTree().CreateTimer(AttackDuration), SceneTreeTimer.SignalName.Timeout);
		_hitbox.Deactivate();

		float remainingAnimTime = Mathf.Max(0f, AttackAnimDuration - AttackHitboxDelay - AttackDuration);
		await ToSignal(GetTree().CreateTimer(remainingAnimTime), SceneTreeTimer.SignalName.Timeout);
		_attacking = false;

		await ToSignal(GetTree().CreateTimer(AttackCooldown), SceneTreeTimer.SignalName.Timeout);
		_canAttack = true;
	}
}
