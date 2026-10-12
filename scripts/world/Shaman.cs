using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Chamán / Shaman (Shaman pack, 144x128 cells, faces right). A forest witch-doctor: breathes fire,
// calls thorns out of the ground, wards himself with a burst of branches, and snacks on a fruit to
// heal when hurt.
// Sheet beats used:
//   fire / walkfire / fireend: the fire breath, standing / walking forward / dying out.
//   branch_up / branch_down: a separate effect — two thorny branches erupting from the ground and
//     sinking back.
//   branchshield (12): 4-8 a burst of branches flaring all around him.
//   eatfruit (15): eating a fruit (bitten around 6-8).
// Moves:
//   Fire breath — a cone of fire in front; he keeps breathing while walking at you (Fire damage,
//     small ticks that don't grant i-frames).
//   Thorns — a scatter of earth under your feet, then thorny branches burst out of the ground there.
//   Branch ward — crowd him and branches flare all around him: knocks you back and blocks hits
//     while it lasts.
//   Fruit — badly hurt and with room to breathe, he eats a fruit and heals. Hit him to make him
//     drop it.
// Telegraphs: body flashes white before the breath and the ward; the earth scatter before thorns.
public partial class Shaman : Enemy
{
	[ExportGroup("Fire breath")]
	[Export] public float BreathRange = 80f;
	[Export] public float AttackCooldown = 2f;
	[Export] public float BreathTelegraph = 0.35f;
	[Export] public float BreathTime = 1.4f;
	[Export] public float BreathWalkSpeed = 40f;
	[Export] public float BreathTickInterval = 0.25f;
	[Export] public float BreathTickDamageMultiplier = 0.45f;
	// fire frames (scale 1, sprite at 8.5,-1): the flames reach ~64px in front, chest to knees.
	[Export] public Vector2 BreathHitboxCenter = new(40f, 4f);
	[Export] public Vector2 BreathHitboxSize = new(52f, 36f);

	[ExportGroup("Thorns")]
	[Export] public float ThornsMinDistance = 100f;
	[Export] public float ThornsMaxDistance = 320f;
	[Export] public float ThornsChance = 0.5f;
	[Export] public float ThornsCooldown = 4f;
	[Export] public float ThornsWarning = 0.6f;
	[Export] public float ThornsHalfWidth = 52f;
	[Export] public float ThornsDamageMultiplier = 1.1f;

	[ExportGroup("Ward")]
	[Export] public float WardTrigger = 50f;
	[Export] public float WardCooldown = 5f;
	[Export] public float WardRadius = 60f;
	[Export] public float WardKnockback = 380f;

	[ExportGroup("Fruit")]
	[Export] public float FruitHealthRatio = 0.4f;
	[Export] public float FruitHealRatio = 0.3f;
	[Export] public int FruitUses = 2;
	[Export] public float FruitSafeDistance = 150f;

	[ExportGroup("Look")]
	[Export] public Color FlashTint = new(2.2f, 2.2f, 2.2f, 1f);
	[Export] public float DecisionInterval = 0.4f;

	private const float Fps = 10f;

	private static readonly RandomNumberGenerator Rng = new();

	private Tween _flashTween;
	private bool _busy;
	private bool _warding;
	private bool _interrupted;
	private float _moveOverride;
	private int _fruitsLeft;
	private float _decisionTimer;
	private float _attackCooldownTimer;
	private float _thornsCooldownTimer;
	private float _wardCooldownTimer;

	static Shaman()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_fruitsLeft = FruitUses;
		_attackCooldownTimer = Rng.RandfRange(0.5f, 1f);
		_thornsCooldownTimer = Rng.RandfRange(1f, 2f);
		Stats.IncomingHitInterceptor = () => _warding;
		Stats.HitTaken += _ => _interrupted = true;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;
		float dt = (float)delta;
		_decisionTimer -= dt;
		_attackCooldownTimer -= dt;
		_thornsCooldownTimer -= dt;
		_wardCooldownTimer -= dt;
		var player = PlayerReads.Find(this);
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval || player is null || !PlayerDetected || _busy || !IsOnFloor())
			return;

		float distanceX = player.GlobalPosition.X - GlobalPosition.X;
		float absDistance = Mathf.Abs(distanceX);

		if (absDistance <= WardTrigger && _wardCooldownTimer <= 0f)
		{
			_ = Ward();
			return;
		}
		if (_fruitsLeft > 0 && Stats.CurrentHealth <= Stats.MaxHealth * FruitHealthRatio && absDistance >= FruitSafeDistance)
		{
			_ = EatFruit();
			return;
		}
		if (absDistance <= BreathRange && _attackCooldownTimer <= 0f)
		{
			_ = FireBreath();
			return;
		}
		if (_decisionTimer > 0f || _attackCooldownTimer > AttackCooldown * 0.5f)
			return;
		_decisionTimer = DecisionInterval * Rng.RandfRange(0.8f, 1.3f);
		if (absDistance >= ThornsMinDistance && absDistance <= ThornsMaxDistance && _thornsCooldownTimer <= 0f
			&& player.IsOnFloor() && Rng.Randf() < ThornsChance)
			_ = Thorns(player);
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_busy)
			return _moveOverride == 0f || CanStepTo(Mathf.Sign(_moveOverride)) ? _moveOverride : 0f;
		float toward = Mathf.Sign(distanceX);
		if (Mathf.Abs(distanceX) > BreathRange * 0.8f && CanStepTo(toward))
			return toward * MoveSpeed;
		return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
	}

	protected override bool CanTurnToFacePlayer => !_busy;

	// ── Fire breath ─────────────────────────────────────────────────────────────────────────

	private async Task FireBreath()
	{
		if (!Begin())
			return;
		try
		{
			Sprite.Play("idle");
			FlashBody(BreathTelegraph);
			if (!await Wait(BreathTelegraph))
				return;
			Sprite.Play("walkfire");
			float forward = FacingRight ? 1f : -1f;
			_moveOverride = forward * BreathWalkSpeed;
			float t = 0f;
			float tick = 0f;
			while (t < BreathTime)
			{
				await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
				if (!IsInstanceValid(this) || IsQueuedForRemoval)
					return;
				float dt = (float)GetPhysicsProcessDeltaTime();
				t += dt;
				tick -= dt;
				if (tick <= 0f)
				{
					tick = BreathTickInterval;
					BurnTick();
				}
			}
			_moveOverride = 0f;
			Sprite.Play("fireend");
			if (!await Wait(8f / Fps))
				return;
		}
		finally
		{
			if (IsInstanceValid(this))
				_moveOverride = 0f;
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * Rng.RandfRange(0.85f, 1.2f);
	}

	private void BurnTick()
	{
		Vector2 at = GlobalPosition + new Vector2(FacingRight ? BreathHitboxCenter.X : -BreathHitboxCenter.X, BreathHitboxCenter.Y);
		var query = new PhysicsShapeQueryParameters2D
		{
			Shape = new RectangleShape2D { Size = BreathHitboxSize },
			Transform = new Transform2D(0f, at),
			CollisionMask = 2,
			CollideWithBodies = true,
		};
		foreach (var hit in GetWorld2D().DirectSpaceState.IntersectShape(query, 8))
		{
			if (hit["collider"].AsGodotObject() is not Metroidvania.Player.Player player)
				continue;
			// Small ticks that neither respect nor grant i-frames (same rule as GroundFire); a block
			// still stops them through the player's own interceptor.
			player.GetNodeOrNull<Stats>("Stats")?.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(Stats.AttackPower * BreathTickDamageMultiplier)),
				element: DamageElement.Fire, ignoreInvulnerability: true, armInvulnerability: false);
			return;
		}
	}

	// ── Thorns ──────────────────────────────────────────────────────────────────────────────

	private async Task Thorns(Metroidvania.Player.Player player)
	{
		if (!Begin())
			return;
		_thornsCooldownTimer = ThornsCooldown;
		try
		{
			FlashBody(0.3f);
			var query = PhysicsRayQueryParameters2D.Create(player.GlobalPosition, player.GlobalPosition + new Vector2(0f, 200f), 1u);
			var floor = GetWorld2D().DirectSpaceState.IntersectRay(query);
			if (floor.Count == 0)
				return;
			Vector2 ground = (Vector2)floor["position"];
			// The branch effect (two branches either side, the sheet's branch_up / branch_down).
			var effect = new AnimatedSprite2D { SpriteFrames = Sprite.SpriteFrames, Animation = "branch_up" };
			GetTree().CurrentScene.AddChild(effect);
			// branch_up's ground line is the cell's y≈95 (cell centre 64), branches centred on x≈64.
			effect.GlobalPosition = ground + new Vector2(8f, -31f);
			effect.Play("branch_up");
			effect.Pause();
			effect.Frame = 0; // the earth scatter: the warning
			if (!await Wait(ThornsWarning))
			{
				effect.QueueFree();
				return;
			}
			effect.Play("branch_up");
			if (!await Wait(2f / Fps))
				return;
			ThornsHit(ground);
			if (!await Wait(0.6f))
				return;
			effect.Play("branch_down");
			await ToSignal(GetTree().CreateTimer(5f / Fps), SceneTreeTimer.SignalName.Timeout);
			if (IsInstanceValid(effect))
				effect.QueueFree();
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * 0.6f;
	}

	private void ThornsHit(Vector2 ground)
	{
		if (PlayerReads.Find(this) is not { } player)
			return;
		Vector2 feet = player.GlobalPosition + new Vector2(0f, 32f);
		if (Mathf.Abs(feet.X - ground.X) > ThornsHalfWidth || Mathf.Abs(feet.Y - ground.Y) > 60f)
			return;
		var stats = player.GetNodeOrNull<Stats>("Stats");
		if (stats is null || stats.IsInvulnerable)
			return;
		int before = stats.CurrentHealth;
		stats.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(Stats.AttackPower * ThornsDamageMultiplier)), isProjectile: true);
		if (stats.CurrentHealth < before)
			player.ApplyKnockback(new Vector2(0f, -1f), 260f);
	}

	// ── Branch ward ─────────────────────────────────────────────────────────────────────────

	private async Task Ward()
	{
		_busy = true;
		_wardCooldownTimer = WardCooldown;
		try
		{
			Sprite.Play("branchshield");
			Sprite.Pause();
			for (int f = 0; f <= 3; f++)
			{
				Sprite.Frame = f;
				if (f == 1)
					FlashBody(0.2f);
				if (!await Wait(0.05f))
					return;
			}
			_warding = true;
			for (int f = 4; f <= 8; f++)
			{
				Sprite.Frame = f;
				if (f == 4)
					WardBurst();
				if (!await Wait(1f / Fps))
					return;
			}
			_warding = false;
			for (int f = 9; f <= 11; f++)
			{
				Sprite.Frame = f;
				if (!await Wait(1f / Fps))
					return;
			}
		}
		finally
		{
			if (IsInstanceValid(this))
			{
				_warding = false;
				_busy = false;
				ClearFlash();
			}
		}
	}

	private void WardBurst()
	{
		if (PlayerReads.Find(this) is not { } player || player.GlobalPosition.DistanceTo(GlobalPosition) > WardRadius + 16f)
			return;
		var stats = player.GetNodeOrNull<Stats>("Stats");
		if (stats is not null && !stats.IsInvulnerable)
			stats.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(Stats.AttackPower * 0.5f)));
		player.ApplyKnockback(new Vector2(Mathf.Sign(player.GlobalPosition.X - GlobalPosition.X), -0.4f).Normalized(), WardKnockback);
	}

	// ── Fruit ───────────────────────────────────────────────────────────────────────────────

	private async Task EatFruit()
	{
		_busy = true;
		_interrupted = false;
		_fruitsLeft--;
		try
		{
			Sprite.Play("eatfruit");
			for (int f = 0; f < 15; f++)
			{
				if (!await Wait(1f / Fps))
					return;
				// Hit mid-meal: drops the fruit, no heal.
				if (_interrupted)
					return;
				if (f == 8)
				{
					Stats.Heal(Mathf.RoundToInt(Stats.MaxHealth * FruitHealRatio));
					HealSparkle();
				}
			}
		}
		finally
		{
			if (IsInstanceValid(this))
				_busy = false;
		}
	}

	private void HealSparkle()
	{
		var glow = new Sprite2D
		{
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			Modulate = new Color(0.5f, 1f, 0.4f, 1f),
			Scale = Vector2.One * 0.2f,
		};
		AddChild(glow);
		Tween tween = glow.CreateTween();
		tween.TweenProperty(glow, "scale", Vector2.One * 0.9f, 0.35f);
		tween.Parallel().TweenProperty(glow, "modulate:a", 0f, 0.5f);
		tween.TweenCallback(Callable.From(glow.QueueFree));
	}

	// ── Shared ──────────────────────────────────────────────────────────────────────────────

	private bool Begin()
	{
		if (!EnemyCombatCoordinator.TryAcquireAttackSlot())
		{
			_attackCooldownTimer = 0.3f;
			return false;
		}
		HoldingAttackSlot = true;
		_busy = true;
		if (PlayerReads.Find(this) is { } player)
		{
			FacingRight = player.GlobalPosition.X >= GlobalPosition.X;
			Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);
		}
		return true;
	}

	private void Finish()
	{
		if (!IsInstanceValid(this))
			return;
		_busy = false;
		ClearFlash();
		if (HoldingAttackSlot)
		{
			EnemyCombatCoordinator.ReleaseAttackSlot();
			HoldingAttackSlot = false;
		}
	}

	private async Task<bool> Wait(float seconds)
	{
		await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
		return IsInstanceValid(this) && !IsQueuedForRemoval;
	}

	private void FlashBody(float duration)
	{
		if (Sprite is null || duration <= 0f)
			return;
		_flashTween?.Kill();
		_flashTween = CreateTween();
		float half = duration / 4f;
		for (int i = 0; i < 2; i++)
		{
			_flashTween.TweenProperty(Sprite, "self_modulate", FlashTint, half);
			_flashTween.TweenProperty(Sprite, "self_modulate", Colors.White, half);
		}
	}

	private void ClearFlash()
	{
		_flashTween?.Kill();
		if (Sprite is not null)
			Sprite.SelfModulate = Colors.White;
	}

	protected override void OnDefeated()
	{
		ClearFlash();
		_warding = false;
		base.OnDefeated();
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null || _busy)
			return;
		string anim = Mathf.Abs(velocity.X) > 5f ? "walk" : "idle";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	protected override bool ContactDamageEnabled => false;
}
