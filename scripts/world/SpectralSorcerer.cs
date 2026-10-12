using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Hechicero espectral / Spectral Sorcerer (Evil Wizard 2, 250x250 cells, faces right). A hooded
// warlock bound to a red-eyed spirit: the spirit lunges and reaps for him up close, and he lets it
// loose to hunt you at range.
// Sheet beats used:
//   attack1 (8): 0-2 the spirit coiling above his staff, 3-6 lunging out in front and up, 7 back.
//   attack2 (8): 1-3 the staff drawn back, 4-6 the spirit sweeping a great scythe-like arc in front.
//   jump / fall: the hop back.
// Moves:
//   Spirit lunge — the spirit shoots out ahead of him, high and low (~60px).
//   Spirit reap — a wide arc in front (~65px); hits harder.
//   Wandering spirit — at range he releases the spirit as a slow seeking wraith (NecroBolt, crimson
//     glow); it turns slowly, so a change of direction shakes it.
//   Hop back — cornered, he springs away.
// Telegraphs: body flashes white before each attack.
public partial class SpectralSorcerer : Enemy
{
	[ExportGroup("Spacing")]
	[Export] public float MinDistance = 110f;
	[Export] public float MaxDistance = 240f;

	[ExportGroup("Spirit melee")]
	[Export] public float MeleeRange = 62f;
	[Export] public float AttackCooldown = 1.6f;
	[Export] public float MeleeTelegraph = 0.3f;
	[Export] public float LungeDamageMultiplier = 1f;
	[Export] public float ReapDamageMultiplier = 1.3f;
	// Measured (scale 0.6, sprite at -6.6,5.8): attack1 frames 3-6 reach ~59px in front, from well
	// above the head to the knees; attack2 frames 4-6 sweep ~64px in front.
	[Export] public Vector2 LungeCenter = new(36f, -16f);
	[Export] public Vector2 LungeSize = new(48f, 86f);
	[Export] public Vector2 ReapCenter = new(36f, 0f);
	[Export] public Vector2 ReapSize = new(58f, 88f);

	[ExportGroup("Wandering spirit")]
	[Export] public float SpiritMinDistance = 110f;
	[Export] public float SpiritMaxDistance = 380f;
	[Export] public float SpiritCooldown = 3f;
	[Export] public float SpiritDamageMultiplier = 1f;
	[Export] public Vector2 SpiritOrigin = new(10f, -40f);

	[ExportGroup("Hop back")]
	[Export] public float HopTrigger = 45f;
	[Export] public float HopCooldown = 3f;
	[Export] public Vector2 HopVelocity = new(200f, -280f);

	[ExportGroup("Look")]
	[Export] public Color SpiritColor = new(0.85f, 0.15f, 0.35f, 1f);
	[Export] public Color FlashTint = new(2.2f, 2.2f, 2.2f, 1f);
	[Export] public float DecisionInterval = 0.35f;

	private static readonly RandomNumberGenerator Rng = new();

	private Tween _flashTween;
	private bool _busy;
	private bool _hopping;
	private bool _retreating;
	private float _decisionTimer;
	private float _attackCooldownTimer;
	private float _spiritCooldownTimer;
	private float _hopCooldownTimer;

	static SpectralSorcerer()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_attackCooldownTimer = Rng.RandfRange(0.5f, 1f);
		_spiritCooldownTimer = Rng.RandfRange(0.6f, 1.4f);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;
		float dt = (float)delta;
		_decisionTimer -= dt;
		_attackCooldownTimer -= dt;
		_spiritCooldownTimer -= dt;
		_hopCooldownTimer -= dt;
		var player = PlayerReads.Find(this);
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval)
			return;
		if (_hopping && IsOnFloor() && Velocity.Y >= 0f)
			_hopping = false;
		if (player is null || !PlayerDetected || _busy || _hopping || !IsOnFloor())
			return;

		float distanceX = player.GlobalPosition.X - GlobalPosition.X;
		float absDistance = Mathf.Abs(distanceX);
		if (absDistance <= HopTrigger && _hopCooldownTimer <= 0f)
		{
			_ = HopBack(-Mathf.Sign(distanceX));
			return;
		}
		if (absDistance <= MeleeRange && _attackCooldownTimer <= 0f)
		{
			_ = SpiritMelee(Rng.Randf() < 0.5f);
			return;
		}
		if (_decisionTimer > 0f)
			return;
		_decisionTimer = DecisionInterval;
		if (absDistance >= SpiritMinDistance && absDistance <= SpiritMaxDistance && _spiritCooldownTimer <= 0f)
			_ = ReleaseSpirit();
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_hopping)
			return currentVelocityX;
		if (_busy)
			return 0f;
		return KitingMoveX(distanceX, currentVelocityX, MinDistance, MaxDistance, 30f, 1.1f, ref _retreating);
	}

	protected override bool DesiredFacingRight(Node2D player, float distanceX) => _retreating ? distanceX < 0 : distanceX >= 0;

	protected override bool CanTurnToFacePlayer => !_busy && !_hopping;

	// ── Spirit melee ────────────────────────────────────────────────────────────────────────

	private async Task SpiritMelee(bool reap)
	{
		if (!Begin())
			return;
		try
		{
			string anim = reap ? "attack2" : "attack1";
			Sprite.Play(anim);
			Sprite.Pause();
			int windupEnd = reap ? 3 : 2;
			for (int f = 0; f < windupEnd; f++)
			{
				Sprite.Frame = f;
				if (!await Wait(0.06f))
					return;
			}
			Sprite.Frame = windupEnd;
			FlashBody(MeleeTelegraph);
			if (!await Wait(MeleeTelegraph))
				return;
			bool hit = false;
			for (int f = windupEnd + 1; f <= 7; f++)
			{
				Sprite.Frame = f;
				if (!hit && f <= 6)
					hit = reap
						? Strike(ReapCenter, ReapSize, ReapDamageMultiplier, 260f)
						: Strike(LungeCenter, LungeSize, LungeDamageMultiplier, 220f);
				if (!await Wait(0.08f))
					return;
			}
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * Rng.RandfRange(0.85f, 1.2f);
	}

	// ── Wandering spirit ────────────────────────────────────────────────────────────────────

	private async Task ReleaseSpirit()
	{
		if (!Begin())
			return;
		_spiritCooldownTimer = SpiritCooldown;
		try
		{
			Sprite.Play("attack1");
			Sprite.Pause();
			Sprite.Frame = 2;
			FlashBody(0.35f);
			if (!await Wait(0.35f))
				return;
			Sprite.Frame = 3;
			if (GetTree().CurrentScene is Node scene && PlayerReads.Find(this) is { } player)
			{
				Vector2 origin = GlobalPosition + new Vector2(FacingRight ? SpiritOrigin.X : -SpiritOrigin.X, SpiritOrigin.Y);
				var spirit = new NecroBolt
				{
					Direction = new Vector2(FacingRight ? 1f : -1f, -0.3f).Normalized(),
					ThrowerStats = Stats,
					DamageMultiplier = SpiritDamageMultiplier,
					GlowColor = SpiritColor,
					Speed = 140f,
					TurnRateDegrees = 140f,
					Lifetime = 3.5f,
					PoisonDuration = 0f,
				};
				scene.AddChild(spirit);
				spirit.GlobalPosition = origin;
			}
			for (int f = 4; f <= 7; f++)
			{
				Sprite.Frame = f;
				if (!await Wait(0.08f))
					return;
			}
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = Mathf.Max(_attackCooldownTimer, 0.6f);
	}

	// ── Hop back ────────────────────────────────────────────────────────────────────────────

	private async Task HopBack(float away)
	{
		var launch = new Vector2(away * HopVelocity.X, HopVelocity.Y);
		if (!LandsSafely(launch, 40f))
		{
			_hopCooldownTimer = 1f;
			return;
		}
		_hopCooldownTimer = HopCooldown;
		_busy = true;
		Velocity = launch;
		_hopping = true;
		Sprite.Play("jump");
		await ToSignal(GetTree().CreateTimer(0.1f), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			_busy = false;
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

	private bool Strike(Vector2 center, Vector2 size, float damageMultiplier, float knockback)
	{
		Vector2 at = GlobalPosition + new Vector2(FacingRight ? center.X : -center.X, center.Y);
		var query = new PhysicsShapeQueryParameters2D
		{
			Shape = new RectangleShape2D { Size = size },
			Transform = new Transform2D(0f, at),
			CollisionMask = 2,
			CollideWithBodies = true,
		};
		foreach (var hit in GetWorld2D().DirectSpaceState.IntersectShape(query, 8))
		{
			if (hit["collider"].AsGodotObject() is not Metroidvania.Player.Player player)
				continue;
			if (player.GetNodeOrNull<Stats>("Stats") is not { } stats || stats.IsInvulnerable)
				continue;
			int before = stats.CurrentHealth;
			stats.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(Stats.AttackPower * damageMultiplier)));
			if (stats.CurrentHealth < before)
			{
				player.ApplyKnockback(new Vector2(Mathf.Sign(player.GlobalPosition.X - GlobalPosition.X), -0.3f).Normalized(), knockback);
				BloodEffect.SpawnAt(this, player.GlobalPosition);
			}
			return true;
		}
		return false;
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
		base.OnDefeated();
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null || _busy || _hopping)
			return;
		string anim = !IsOnFloor() ? (velocity.Y < 0f ? "jump" : "fall") : Mathf.Abs(velocity.X) > 5f ? "run" : "idle";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	protected override bool ContactDamageEnabled => false;
}
