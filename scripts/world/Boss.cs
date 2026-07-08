using Godot;
using Metroidvania.Player;
using Metroidvania.Save;

namespace Metroidvania.World;

public partial class Boss : Enemy
{
	[Export] public float AttackRange = 60f;
	[Export] public bool AttackRequiresOverhead = false;
	[Export] public bool AlwaysMoving = false;
	[Export] public bool FleeAndRecharge = false;
	[Export] public float OverheadHeightThreshold = 50f;
	[Export] public float AttackCooldown = 1.5f;
	[Export] public float AttackHitboxDelay = 0.12f;
	[Export] public float AttackDuration = 0.22f;
	[Export] public float AttackAnimDuration = 0.5f;
	[Export] public float AttackHitboxReach = 55f;
	[Export] public float ComboChance = 0.35f;
	[Export] public float WanderInterval = 2.5f;
	[Export] public float WanderRadius = 120f;
	[Export] public float WanderSettleDistance = 8f;
	[Export] public float RetreatChance = 0.6f;
	[Export] public float RetreatSpeed = 320f;
	[Export] public float RetreatDuration = 0.25f;
	[Export] public float RetreatCooldown = 0.9f;
	[Export] public float LungeRange = 180f;
	[Export] public float LungeChance = 0.5f;
	[Export] public float LungeSpeed = 260f;
	[Export] public float LungeDuration = 0.35f;
	[Export] public float LungeHitboxDelay = 0.22f;
	[Export] public float LungeHitboxDuration = 0.1f;
	[Export] public float EnrageHealthPercent = 0.4f;
	[Export] public float EnrageSpeedMultiplier = 1.4f;
	[Export] public float EnrageAttackCooldownMultiplier = 0.6f;
	[Export] public float EnrageRetreatChanceBonus = 0.25f;

	private Hitbox _hitbox;
	private bool _attacking;
	private bool _canAttack = true;
	private bool _isRetreating;
	private bool _canRetreat = true;
	private bool _isLunging;
	private bool _lungeMoving;
	private float _retreatDirection;
	private float _lungeDirection;
	private float _wanderTimer;
	private float _wanderOffsetX;
	private float _homePositionX;
	private bool _enraged;
	private readonly RandomNumberGenerator _rng = new();

	private bool IsEnraged => Stats.CurrentHealth <= Stats.MaxHealth * EnrageHealthPercent;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;

		AddToGroup("boss");
		_hitbox = GetNode<Hitbox>("AttackHitbox");
		Stats.HitTaken += OnHitTaken;

		_homePositionX = GlobalPosition.X;
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
		SpawnLoot();
		QueueFree();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;

		if (IsEnraged && !_enraged)
		{
			_enraged = true;
			Visual.Modulate = new Color(1.4f, 0.65f, 0.65f);
		}

		Vector2 velocity = Velocity;

		if (!IsOnFloor())
			velocity.Y += Gravity * (float)delta;
		else
			velocity.Y = 0;

		if (_isRetreating)
		{
			velocity.X = _retreatDirection * RetreatSpeed;
			Velocity = velocity;
			MoveAndSlide();
			UpdateAnimation(velocity);
			return;
		}

		if (_isLunging)
		{
			velocity.X = _lungeMoving ? _lungeDirection * LungeSpeed : Mathf.MoveToward(velocity.X, 0, LungeSpeed);
			Velocity = velocity;
			MoveAndSlide();
			UpdateAnimation(velocity);
			return;
		}

		_wanderTimer -= (float)delta;
		if (_wanderTimer <= 0f)
			PickWanderOffset();

		float moveSpeed = MoveSpeed * (IsEnraged ? EnrageSpeedMultiplier : 1f);

		Node2D player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		float distanceAbs = player is null ? float.MaxValue : Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X);
		bool playerInRange = player is not null && distanceAbs <= DetectionRange;

		if (AlwaysMoving)
		{
			float wanderBaseX = playerInRange ? player.GlobalPosition.X : _homePositionX;
			float targetX = wanderBaseX + _wanderOffsetX;
			float distanceToTarget = targetX - GlobalPosition.X;

			if (Mathf.Abs(distanceToTarget) <= WanderSettleDistance)
			{
				PickWanderOffset();
				targetX = wanderBaseX + _wanderOffsetX;
				distanceToTarget = targetX - GlobalPosition.X;
			}

			velocity.X = Mathf.Sign(distanceToTarget) * moveSpeed;
			FacingRight = playerInRange ? player.GlobalPosition.X >= GlobalPosition.X : distanceToTarget >= 0;
			Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);
		}
		else if (playerInRange)
		{
			float distanceToPlayer = player.GlobalPosition.X - GlobalPosition.X;
			FacingRight = distanceToPlayer >= 0;
			Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);

			float targetX = player.GlobalPosition.X + _wanderOffsetX;
			float distanceToTarget = targetX - GlobalPosition.X;

			velocity.X = Mathf.Abs(distanceToTarget) > WanderSettleDistance
				? Mathf.Sign(distanceToTarget) * moveSpeed
				: Mathf.MoveToward(velocity.X, 0, moveSpeed);
		}
		else
		{
			velocity.X = Mathf.MoveToward(velocity.X, 0, moveSpeed);
		}

		if (playerInRange && !_attacking && _canAttack)
		{
			bool canMeleeAttack = distanceAbs <= AttackRange
				&& (!AttackRequiresOverhead || player.GlobalPosition.Y - GlobalPosition.Y >= OverheadHeightThreshold);

			if (canMeleeAttack)
				Attack();
			else if (distanceAbs <= LungeRange && _rng.Randf() < LungeChance)
				StartLunge();
		}

		Velocity = velocity;
		MoveAndSlide();

		UpdateAnimation(velocity);
		ApplyContactDamage();
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null) return;
		string anim = _attacking ? "attack1"
			: _isLunging && Sprite.SpriteFrames.HasAnimation("lunge") ? "lunge"
			: Mathf.Abs(velocity.X) > 5f ? "run" : "idle";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	private void OnHitTaken(bool isProjectile)
	{
		if (_isRetreating || _isLunging || !_canRetreat || _attacking)
			return;

		float retreatChance = RetreatChance + (IsEnraged ? EnrageRetreatChanceBonus : 0f);
		if (_rng.Randf() > retreatChance)
			return;

		Node2D player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		_retreatDirection = player is not null && player.GlobalPosition.X > GlobalPosition.X ? -1f : 1f;

		if (FleeAndRecharge)
			StartRetreat();
		else
			StartDodge();
	}

	private async void StartDodge()
	{
		_isRetreating = true;
		_canRetreat = false;
		Stats.ExternalInvulnerable = true;

		await ToSignal(GetTree().CreateTimer(RetreatDuration), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this))
			return;

		_isRetreating = false;
		Stats.ExternalInvulnerable = false;

		await ToSignal(GetTree().CreateTimer(RetreatCooldown), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			_canRetreat = true;
	}

	private async void StartRetreat()
	{
		_isRetreating = true;
		_canRetreat = false;
		_canAttack = false;
		Stats.ExternalInvulnerable = true;

		FacingRight = _retreatDirection > 0;
		Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);

		await ToSignal(GetTree().CreateTimer(RetreatDuration), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this))
			return;

		_isRetreating = false;
		Stats.ExternalInvulnerable = false;

		Node2D player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		if (player is not null)
		{
			FacingRight = player.GlobalPosition.X >= GlobalPosition.X;
			Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);
		}

		StartLunge();

		await ToSignal(GetTree().CreateTimer(RetreatCooldown), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			_canRetreat = true;
	}

	private async void StartLunge()
	{
		_isLunging = true;
		_lungeMoving = true;
		_canAttack = false;
		_lungeDirection = FacingRight ? 1f : -1f;

		if (LungeHitboxDelay > 0f)
			await ToSignal(GetTree().CreateTimer(LungeHitboxDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this))
			return;

		_hitbox.Position = new Vector2(FacingRight ? AttackHitboxReach : -AttackHitboxReach, 0);
		_hitbox.Activate(Stats);

		await ToSignal(GetTree().CreateTimer(LungeHitboxDuration), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this))
			return;
		_hitbox.Deactivate();
		_lungeMoving = false;
		ImpactEffect.SpawnAt(this, _hitbox.GlobalPosition);

		float remaining = Mathf.Max(0f, LungeDuration - LungeHitboxDelay - LungeHitboxDuration);
		if (remaining > 0f)
			await ToSignal(GetTree().CreateTimer(remaining), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this))
			return;

		_isLunging = false;

		float cooldown = AttackCooldown * (IsEnraged ? EnrageAttackCooldownMultiplier : 1f);
		await ToSignal(GetTree().CreateTimer(cooldown), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			_canAttack = true;
	}

	private async void Attack(bool isCombo = false)
	{
		_attacking = true;
		_canAttack = false;
		Sprite?.Play("attack1");

		if (AttackHitboxDelay > 0f)
			await ToSignal(GetTree().CreateTimer(AttackHitboxDelay), SceneTreeTimer.SignalName.Timeout);

		if (!IsInstanceValid(this))
			return;

		_hitbox.Position = new Vector2(FacingRight ? AttackHitboxReach : -AttackHitboxReach, 0);
		_hitbox.Activate(Stats);

		await ToSignal(GetTree().CreateTimer(AttackDuration), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this))
			return;
		_hitbox.Deactivate();

		float remainingAnimTime = Mathf.Max(0f, AttackAnimDuration - AttackHitboxDelay - AttackDuration);
		await ToSignal(GetTree().CreateTimer(remainingAnimTime), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this))
			return;
		_attacking = false;

		if (!isCombo && _rng.Randf() < ComboChance)
		{
			Attack(isCombo: true);
			return;
		}

		float cooldown = AttackCooldown * (IsEnraged ? EnrageAttackCooldownMultiplier : 1f);
		await ToSignal(GetTree().CreateTimer(cooldown), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			_canAttack = true;
	}
}
