using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Zombi / Zombie Walker (Zombies pack, 128x128 cells, faces right). The plain shambler: slow and
// clumsy, but it smells blood.
// Sheet beats used:
//   attack (17): 0-8 leaning back with its arms up (a long, readable wind-up), 9-13 the lunging
//     bite/claw, 14-16 back upright.
//   idle / walk / hurt (frame 1 is a white hit-flash) / death (topples over).
// Moves:
//   Lunging bite — slow wind-up, then it lurches forward and bites. A bite that lands heals it.
//   Smell of blood — once the player is under BloodThreshold health, it shambles and winds up
//     faster, and its animations speed up with it.
// Telegraphs: green blinks on the wind-up.
public partial class ZombieWalker : Enemy
{
	[ExportGroup("Bite")]
	[Export] public float BiteTrigger = 48f;
	[Export] public float BiteWindup = 0.7f;
	[Export] public float BiteLurchSpeed = 120f;
	[Export] public float BiteDamageMultiplier = 1f;
	[Export] public float BiteHealFraction = 0.5f;
	[Export] public float AttackCooldown = 1.6f;
	// Frames 9-13: arms/teeth reach ~35px in front at head-to-chest height.
	[Export] public Vector2 BiteHitboxCenter = new(24f, -6f);
	[Export] public Vector2 BiteHitboxSize = new(32f, 36f);

	[ExportGroup("Smell of blood")]
	[Export] public float BloodThreshold = 0.4f;
	[Export] public float FrenzySpeedMultiplier = 1.7f;
	[Export] public float FrenzyWindupMultiplier = 0.55f;

	[ExportGroup("Look")]
	[Export] public Color FlashTint = new(1.4f, 2.6f, 1.5f, 1f);

	private static readonly RandomNumberGenerator Rng = new();

	private Tween _flashTween;
	private bool _busy;
	private bool _frenzied;
	private float _baseSpeed;
	private float _moveOverride;
	private float _attackCooldownTimer;

	static ZombieWalker()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_baseSpeed = MoveSpeed;
		_attackCooldownTimer = Rng.RandfRange(0.2f, 0.8f);
		Stats.HitTaken += _ => OnHit();
	}

	private void OnHit()
	{
		if (!_busy)
			_ = Flinch();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;
		_attackCooldownTimer -= (float)delta;
		var player = PlayerReads.Find(this);
		UpdateFrenzy(player);
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval || player is null || !PlayerDetected || _busy || !IsOnFloor())
			return;
		float absDx = Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X);
		float absDy = Mathf.Abs(player.GlobalPosition.Y - GlobalPosition.Y);
		if (absDx <= BiteTrigger && absDy <= 40f && _attackCooldownTimer <= 0f)
			_ = Bite();
	}

	private void UpdateFrenzy(Metroidvania.Player.Player player)
	{
		bool frenzied = player?.GetNodeOrNull<Stats>("Stats") is { } stats && stats.MaxHealth > 0
			&& stats.CurrentHealth <= stats.MaxHealth * BloodThreshold;
		if (frenzied == _frenzied)
			return;
		_frenzied = frenzied;
		MoveSpeed = frenzied ? _baseSpeed * FrenzySpeedMultiplier : _baseSpeed;
		if (Sprite is not null)
			Sprite.SpeedScale = frenzied ? 1.5f : 1f;
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_busy)
			return _moveOverride == 0f || CanStepTo(Mathf.Sign(_moveOverride)) ? _moveOverride : 0f;
		return base.ComputeMoveX(player, distanceX, currentVelocityX, delta);
	}

	protected override bool CanTurnToFacePlayer => !_busy;

	// ── Bite ─────────────────────────────────────────────────────────────────────────────────────

	private async Task Bite()
	{
		if (!Begin())
			return;
		try
		{
			float windup = BiteWindup * (_frenzied ? FrenzyWindupMultiplier : 1f);
			Sprite.Play("attack");
			Sprite.Pause();
			FlashBody(windup);
			for (int f = 0; f <= 8; f++)
			{
				Sprite.Frame = f;
				if (!await Wait(windup / 9f))
					return;
			}
			ClearFlash();
			_moveOverride = (FacingRight ? 1f : -1f) * BiteLurchSpeed;
			bool hit = false;
			for (int f = 9; f <= 13; f++)
			{
				Sprite.Frame = f;
				if (!hit && f <= 11)
					hit = Strike();
				if (!await Wait(0.06f))
					return;
			}
			_moveOverride = 0f;
			for (int f = 14; f <= 16; f++)
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
		_attackCooldownTimer = AttackCooldown * (_frenzied ? 0.6f : 1f) * Rng.RandfRange(0.85f, 1.25f);
	}

	private bool Strike()
	{
		Vector2 at = GlobalPosition + new Vector2(FacingRight ? BiteHitboxCenter.X : -BiteHitboxCenter.X, BiteHitboxCenter.Y);
		var query = new PhysicsShapeQueryParameters2D
		{
			Shape = new RectangleShape2D { Size = BiteHitboxSize },
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
			stats.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(Stats.AttackPower * BiteDamageMultiplier)));
			int dealt = before - stats.CurrentHealth;
			if (dealt > 0)
			{
				player.ApplyKnockback(new Vector2(FacingRight ? 1f : -1f, -0.25f).Normalized(), 160f);
				BloodEffect.SpawnAt(this, player.GlobalPosition);
				Stats.Heal(Mathf.Max(1, Mathf.RoundToInt(dealt * BiteHealFraction)));
			}
			return true;
		}
		return false;
	}

	// ── Shared ───────────────────────────────────────────────────────────────────────────────────

	private async Task Flinch()
	{
		_busy = true;
		try
		{
			Sprite.Play("hurt");
			await ToSignal(GetTree().CreateTimer(0.25f), SceneTreeTimer.SignalName.Timeout);
		}
		finally
		{
			if (IsInstanceValid(this))
				_busy = false;
		}
	}

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
		_moveOverride = 0f;
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
		if (Sprite is not null)
			Sprite.SpeedScale = 1f;
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
