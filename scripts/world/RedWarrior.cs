using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Guerrero rojo / Red Warrior (170x96 cells, faces right). A berserker with a broadsword: relentless
// pressure, a whirlwind, and a roar that sends him into a frenzy.
// Sheet beats used:
//   attack (30): 0-4 the sword drawn back, 5-9 a low slash out front, 11-13 pulled back up,
//     14-18 a second, higher slash, 23-25 a flourish, 26-29 recovery.
//   dash (8): a lunging thrust — the figure itself travels ~59px across the cell (root motion).
//   spin_attack (30): 4-8 winding up, 9-23 a spinning whirlwind that drifts ~34px forward
//     (root motion), 24-29 recovery.
//   taunt (18): 7-15 arms raised in a roar.
// Root motion: for dash and spin_attack the sprite's Offset cancels the drawn drift frame by frame
// while the body moves by the same amount, so he really travels and nothing pops back afterwards.
// Moves:
//   Double slash — low slash, then (often) the high one. The high one hits harder.
//   Lunge — from mid range a dashing thrust that covers ~100px.
//   Whirlwind — spins forward, hitting on both sides several times. Hard to approach; back off.
//   Frenzy — below FrenzyHealthRatio he roars (taunt) once: faster and harder-hitting from then on.
//   Punish — a whiffed swing near him gets a quick low slash.
// Telegraphs: body flashes white before each attack; crimson neon trails.
public partial class RedWarrior : Enemy
{
	[ExportGroup("Double slash")]
	[Export] public float SlashRange = 80f;
	[Export] public float AttackCooldown = 1.3f;
	[Export] public float SlashTelegraph = 0.3f;
	[Export] public float SecondChance = 0.6f;
	[Export] public float LowDamageMultiplier = 1f;
	[Export] public float HighDamageMultiplier = 1.25f;
	// Measured (scale 1.8, sprite at 9,-19.4): attack frame 5 slash reaches ~83px in front, waist to
	// feet; frames 16-18 ~92px in front, from above the head down to the waist.
	[Export] public Vector2 LowSlashCenter = new(45f, 3f);
	[Export] public Vector2 LowSlashSize = new(76f, 52f);
	[Export] public Vector2 HighSlashCenter = new(48f, -6f);
	[Export] public Vector2 HighSlashSize = new(84f, 72f);

	[ExportGroup("Lunge")]
	[Export] public float LungeMinDistance = 100f;
	[Export] public float LungeMaxDistance = 190f;
	[Export] public float LungeChance = 0.45f;
	[Export] public float LungeCooldown = 3.5f;
	[Export] public Vector2 LungeHitboxCenter = new(50f, 0f);
	[Export] public Vector2 LungeHitboxSize = new(70f, 40f);

	[ExportGroup("Whirlwind")]
	[Export] public float WhirlRange = 90f;
	[Export] public float WhirlChance = 0.25f;
	[Export] public float WhirlCooldown = 6f;
	[Export] public float WhirlHitInterval = 0.2f;
	[Export] public float WhirlDamageMultiplier = 0.8f;
	[Export] public Vector2 WhirlHitboxCenter = new(0f, -4f);
	[Export] public Vector2 WhirlHitboxSize = new(112f, 60f);

	[ExportGroup("Frenzy")]
	[Export] public float FrenzyHealthRatio = 0.5f;
	[Export] public float FrenzySpeedScale = 1.3f;
	[Export] public float FrenzyDamageBonus = 0.25f;

	[ExportGroup("Look")]
	[Export] public Color TrailGlowColor = new(1f, 0.1f, 0.12f, 1f);
	[Export] public Color FlashTint = new(2.2f, 2.2f, 2.2f, 1f);
	[Export] public float DecisionInterval = 0.35f;

	// Drawn drift of the figure per frame (cell px, forward), measured off the sheet.
	private static readonly float[] DashDrift = { 0f, 5f, 17f, 17f, 17f, 17f, 59f, 59f };
	private static readonly float[] SpinDrift =
	{
		0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 4f, 6f, 9f, 11f, 13f, 15f, 17f, 19f, 21f, 23f, 26f, 28f, 30f,
		32f, 33f, 34f, 34f, 34f, 34f, 34f,
	};
	private const float SpriteScale = 1.8f;

	private static readonly RandomNumberGenerator Rng = new();
	private readonly WhiffWatcher _whiff = new();

	private EnemySlashTrail _trail;
	private Tween _flashTween;
	private bool _busy;
	private bool _frenzied;
	private float _baseMoveSpeed;
	private float _decisionTimer;
	private float _attackCooldownTimer;
	private float _lungeCooldownTimer;
	private float _whirlCooldownTimer;
	private float[] _drift;
	private float _appliedDrift;
	private Vector2 _baseOffset;

	static RedWarrior()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_baseMoveSpeed = MoveSpeed;
		_baseOffset = Sprite.Offset;
		_attackCooldownTimer = Rng.RandfRange(0.4f, 0.9f);
		_lungeCooldownTimer = Rng.RandfRange(0.8f, 1.6f);
		_whirlCooldownTimer = Rng.RandfRange(2f, 3f);
		_trail = new EnemySlashTrail
		{
			CoreWidth = 3f,
			GlowWidth = 10f,
			GlowColor = TrailGlowColor,
			CoreColor = new Color(1f, 0.65f, 0.6f, 1f),
			MinPointSpacing = 1.5f,
			SparkAmount = 12,
		};
		Visual.AddChild(_trail);
		Sprite.FrameChanged += ApplyRootMotion;
		Stats.HitTaken += _ => _whiff.NotifyHitTaken();
	}

	// ── Root motion ─────────────────────────────────────────────────────────────────────────

	private void BeginRootMotion(float[] drift)
	{
		_drift = drift;
		_appliedDrift = 0f;
		Sprite.Offset = _baseOffset;
	}

	// Each frame: hide the drawn drift with the offset, and move the body by the newly drawn amount.
	private void ApplyRootMotion()
	{
		if (_drift is null)
			return;
		float drift = _drift[Mathf.Clamp(Sprite.Frame, 0, _drift.Length - 1)];
		float step = (drift - _appliedDrift) * SpriteScale;
		_appliedDrift = drift;
		Sprite.Offset = _baseOffset + new Vector2(-drift, 0f);
		if (Mathf.Abs(step) > 0.01f)
		{
			float dir = FacingRight ? 1f : -1f;
			// Stop at walls/ledges: the figure then just stays put (offset still hides the drift).
			if (!TestMove(GlobalTransform, new Vector2(dir * step, 0f)) && HasGroundAhead(dir))
				MoveAndCollide(new Vector2(dir * step, 0f));
		}
	}

	private void EndRootMotion()
	{
		_drift = null;
		_appliedDrift = 0f;
		if (Sprite is not null)
			Sprite.Offset = _baseOffset;
	}

	// ── Main loop ───────────────────────────────────────────────────────────────────────────

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;
		float dt = (float)delta;
		_decisionTimer -= dt;
		_attackCooldownTimer -= dt * Speed();
		_lungeCooldownTimer -= dt;
		_whirlCooldownTimer -= dt;
		var player = PlayerReads.Find(this);
		_whiff.Update(player, dt);
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval || player is null || !PlayerDetected || _busy || !IsOnFloor())
			return;

		float distanceX = player.GlobalPosition.X - GlobalPosition.X;
		float absDistance = Mathf.Abs(distanceX);

		if (!_frenzied && Stats.CurrentHealth <= Stats.MaxHealth * FrenzyHealthRatio)
		{
			_ = Roar();
			return;
		}
		if (_whiff.JustWhiffed && absDistance <= SlashRange + 10f && _attackCooldownTimer <= AttackCooldown * 0.5f)
		{
			_whiff.Consume();
			_ = DoubleSlash(0.12f, allowSecond: false);
			return;
		}
		if (absDistance <= WhirlRange && _whirlCooldownTimer <= 0f && _attackCooldownTimer <= 0f && Rng.Randf() < WhirlChance)
		{
			_ = Whirlwind();
			return;
		}
		if (absDistance <= SlashRange && _attackCooldownTimer <= 0f)
		{
			_ = DoubleSlash(SlashTelegraph, allowSecond: true);
			return;
		}
		if (_decisionTimer > 0f || _attackCooldownTimer > AttackCooldown * 0.7f)
			return;
		_decisionTimer = DecisionInterval * Rng.RandfRange(0.8f, 1.3f);
		if (absDistance >= LungeMinDistance && absDistance <= LungeMaxDistance && _lungeCooldownTimer <= 0f && Rng.Randf() < LungeChance)
			_ = Lunge();
	}

	private float Speed() => _frenzied ? FrenzySpeedScale : 1f;
	private float Damage(float multiplier) => multiplier + (_frenzied ? FrenzyDamageBonus : 0f);

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_busy)
			return 0f;
		float toward = Mathf.Sign(distanceX);
		if (Mathf.Abs(distanceX) > SlashRange * 0.8f && CanStepTo(toward))
			return toward * MoveSpeed;
		return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
	}

	protected override bool CanTurnToFacePlayer => !_busy;

	// ── Double slash ────────────────────────────────────────────────────────────────────────

	private async Task DoubleSlash(float telegraph, bool allowSecond)
	{
		if (!Begin())
			return;
		try
		{
			Sprite.Play("attack");
			Sprite.Pause();
			if (!await Frames(0, 3, 0.04f))
				return;
			Sprite.Frame = 4;
			FlashBody(telegraph);
			if (!await Wait(telegraph))
				return;
			Sprite.Frame = 5;
			_trail.Play(new Vector2(0f, 4f), 60f, -40f, 60f, 0.08f, 34f);
			Strike(LowSlashCenter, LowSlashSize, Damage(LowDamageMultiplier), 200f);
			if (!await Frames(6, 10, 0.05f))
				return;
			if (allowSecond && Rng.Randf() < SecondChance)
			{
				FacePlayer();
				if (!await Frames(11, 13, 0.05f))
					return;
				Sprite.Frame = 14;
				_trail.Play(new Vector2(0f, -10f), 64f, -110f, 30f, 0.08f);
				Strike(HighSlashCenter, HighSlashSize, Damage(HighDamageMultiplier), 280f);
				if (!await Frames(15, 22, 0.045f))
					return;
			}
			if (!await Frames(26, 29, 0.05f))
				return;
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * Rng.RandfRange(0.85f, 1.2f);
	}

	private async Task<bool> Frames(int from, int to, float each)
	{
		for (int f = from; f <= to; f++)
		{
			Sprite.Frame = f;
			if (!await Wait(each / Speed()))
				return false;
		}
		return true;
	}

	// ── Lunge ───────────────────────────────────────────────────────────────────────────────

	private async Task Lunge()
	{
		if (!Begin())
			return;
		_lungeCooldownTimer = LungeCooldown;
		try
		{
			Sprite.Play("dash");
			Sprite.Pause();
			BeginRootMotion(DashDrift);
			Sprite.Frame = 0;
			FlashBody(0.3f);
			if (!await Wait(0.3f))
				return;
			bool hit = false;
			for (int f = 1; f <= 7; f++)
			{
				Sprite.Frame = f;
				if (!hit && f >= 2 && f <= 5)
					hit = Strike(LungeHitboxCenter, LungeHitboxSize, Damage(1.1f), 300f);
				if (!await Wait(0.06f / Speed()))
					return;
			}
			if (!await Wait(0.15f))
				return;
		}
		finally
		{
			EndRootMotion();
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * 0.6f;
	}

	// ── Whirlwind ───────────────────────────────────────────────────────────────────────────

	private async Task Whirlwind()
	{
		if (!Begin())
			return;
		_whirlCooldownTimer = WhirlCooldown;
		try
		{
			Sprite.Play("spin_attack");
			Sprite.Pause();
			BeginRootMotion(SpinDrift);
			if (!await Frames(0, 3, 0.04f))
				return;
			FlashBody(0.35f);
			if (!await Frames(4, 8, 0.07f))
				return;
			float hitTimer = 0f;
			for (int f = 9; f <= 23; f++)
			{
				Sprite.Frame = f;
				hitTimer -= 0.055f;
				if (hitTimer <= 0f)
				{
					hitTimer = WhirlHitInterval;
					Strike(WhirlHitboxCenter, WhirlHitboxSize, Damage(WhirlDamageMultiplier), 180f, ignoreFacing: true);
				}
				if (!await Wait(0.055f / Speed()))
					return;
			}
			if (!await Frames(24, 29, 0.05f))
				return;
		}
		finally
		{
			EndRootMotion();
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * 0.8f;
	}

	// ── Frenzy ──────────────────────────────────────────────────────────────────────────────

	private async Task Roar()
	{
		_frenzied = true;
		_busy = true;
		try
		{
			Sprite.Play("taunt");
			Sprite.Pause();
			for (int f = 0; f <= 17; f++)
			{
				Sprite.Frame = f;
				if (f == 7)
				{
					Shake(3f, 0.3f);
					Sprite.SelfModulate = new Color(1.8f, 0.6f, 0.6f, 1f);
					CreateTween().TweenProperty(Sprite, "self_modulate", Colors.White, 0.8f);
				}
				if (!await Wait(1f / 15f))
					return;
			}
			MoveSpeed = _baseMoveSpeed * FrenzySpeedScale;
			Sprite.SpeedScale = FrenzySpeedScale;
		}
		finally
		{
			if (IsInstanceValid(this))
				_busy = false;
		}
	}

	// ── Shared ──────────────────────────────────────────────────────────────────────────────

	private bool Begin()
	{
		if (!EnemyCombatCoordinator.TryAcquireAttackSlot())
		{
			_attackCooldownTimer = 0.25f;
			return false;
		}
		HoldingAttackSlot = true;
		_busy = true;
		FacePlayer();
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

	// ignoreFacing: the box is centred on him (the whirlwind), so either side counts.
	private bool Strike(Vector2 center, Vector2 size, float damageMultiplier, float knockback, bool ignoreFacing = false)
	{
		Vector2 at = GlobalPosition + new Vector2(FacingRight || ignoreFacing ? center.X : -center.X, center.Y);
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

	private void Shake(float strength, float duration)
	{
		if (PlayerReads.Find(this) is { } player && player.GlobalPosition.DistanceTo(GlobalPosition) < 700f)
			player.ShakeCamera(strength, duration);
	}

	private void FacePlayer()
	{
		if (PlayerReads.Find(this) is { } player)
		{
			FacingRight = player.GlobalPosition.X >= GlobalPosition.X;
			Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);
		}
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
		EndRootMotion();
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
