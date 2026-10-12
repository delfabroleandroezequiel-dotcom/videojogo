using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Guerrero de la necrópolis / Necropolis Warrior (Dark Crimson Necropolis pack, single 142x142
// frames, faces right). A violet-souled reaper knight: a scythe-axe, pillars of soulfire, and the
// trick of sinking into the floor and rising wherever it likes.
// Sheet beats used:
//   quick_attack (7): 1-2 drawn back, 3 an arc over the top and down the front, 4 a low follow-up
//     sweep, 5-6 recovery.
//   power_attack (17): 1-5 the blade drawn back (the wind-up), 6 a huge overhead arc, 7 a soul orb
//     striking the floor in front, 8-13 a pillar of violet flame bursting up there, 14-16 recovery.
//   blade_spin_1 (16): a spinning whirl of the blade around its body (hits on both sides).
//   teleport_out (24): sinking into the floor in a swirl. summon_in (31): rising out of it.
// Moves:
//   Quick slash — fast arc + low sweep.
//   Soul pillar — the slow overhead slam: a pillar of soulfire erupts ~75px in front. Close in or
//     stay well out of it.
//   Sink & rise — sinks into the floor (untouchable), then rises right next to you and slashes.
//   Blade whirl — when you hug it, it spins the blade around itself.
// Telegraphs: body flashes white before every attack; violet neon trails.
public partial class NecropolisWarrior : Enemy
{
	[ExportGroup("Quick slash")]
	[Export] public float SlashRange = 65f;
	[Export] public float AttackCooldown = 1.4f;
	[Export] public float SlashTelegraph = 0.25f;
	[Export] public float SlashDamageMultiplier = 1f;
	// Measured (scale 1.3, sprite at 0,-39): quick_attack frame 3 arcs ~66px in front over the head;
	// frame 4 sweeps ~60px in front low.
	[Export] public Vector2 ArcCenter = new(34f, -18f);
	[Export] public Vector2 ArcSize = new(64f, 70f);
	[Export] public Vector2 SweepCenter = new(30f, 16f);
	[Export] public Vector2 SweepSize = new(60f, 30f);

	[ExportGroup("Soul pillar")]
	[Export] public float PillarMinDistance = 58f;
	[Export] public float PillarMaxDistance = 98f;
	[Export] public float PillarChance = 0.45f;
	[Export] public float PillarCooldown = 4f;
	[Export] public float PillarTelegraph = 0.55f;
	[Export] public float PillarDamageMultiplier = 1.5f;
	// power_attack frames 8-13: the pillar rises ~65-90px in front, ~100px tall.
	[Export] public Vector2 PillarCenter = new(77f, -20f);
	[Export] public Vector2 PillarSize = new(30f, 100f);

	[ExportGroup("Sink & rise")]
	[Export] public float SinkMinDistance = 120f;
	[Export] public float SinkChance = 0.35f;
	[Export] public float SinkCooldown = 6f;
	[Export] public float HiddenTime = 0.5f;
	[Export] public float RiseOffset = 55f;

	[ExportGroup("Blade whirl")]
	[Export] public float WhirlTrigger = 40f;
	[Export] public float WhirlCooldown = 4f;
	[Export] public float WhirlDamageMultiplier = 0.9f;
	[Export] public Vector2 WhirlSize = new(100f, 56f);

	[ExportGroup("Look")]
	[Export] public Color TrailGlowColor = new(0.7f, 0.35f, 1f, 1f);
	[Export] public Color FlashTint = new(2.2f, 2.2f, 2.2f, 1f);
	[Export] public float DecisionInterval = 0.4f;

	private static readonly RandomNumberGenerator Rng = new();

	private EnemySlashTrail _trail;
	private Tween _flashTween;
	private bool _busy;
	private bool _hidden;
	private float _decisionTimer;
	private float _attackCooldownTimer;
	private float _pillarCooldownTimer;
	private float _sinkCooldownTimer;
	private float _whirlCooldownTimer;

	static NecropolisWarrior()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_attackCooldownTimer = Rng.RandfRange(0.5f, 1f);
		_pillarCooldownTimer = Rng.RandfRange(1f, 2f);
		_sinkCooldownTimer = Rng.RandfRange(0.8f, 1.6f);
		_trail = new EnemySlashTrail
		{
			CoreWidth = 3f,
			GlowWidth = 11f,
			GlowColor = TrailGlowColor,
			CoreColor = new Color(0.95f, 0.85f, 1f, 1f),
			MinPointSpacing = 1.5f,
			SparkAmount = 12,
		};
		Visual.AddChild(_trail);
		Stats.IncomingHitInterceptor = () => _hidden;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;
		float dt = (float)delta;
		_decisionTimer -= dt;
		_attackCooldownTimer -= dt;
		_pillarCooldownTimer -= dt;
		_sinkCooldownTimer -= dt;
		_whirlCooldownTimer -= dt;
		var player = PlayerReads.Find(this);
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval || player is null || !PlayerDetected || _busy || !IsOnFloor())
			return;

		float absDistance = Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X);
		if (absDistance <= WhirlTrigger && _whirlCooldownTimer <= 0f && _attackCooldownTimer <= AttackCooldown * 0.5f)
		{
			_ = BladeWhirl();
			return;
		}
		if (absDistance <= SlashRange && _attackCooldownTimer <= 0f)
		{
			_ = QuickSlash(SlashTelegraph);
			return;
		}
		if (_decisionTimer > 0f || _attackCooldownTimer > AttackCooldown * 0.6f)
			return;
		_decisionTimer = DecisionInterval * Rng.RandfRange(0.8f, 1.3f);
		if (absDistance >= SinkMinDistance && _sinkCooldownTimer <= 0f && player.IsOnFloor() && Rng.Randf() < SinkChance)
			_ = SinkAndRise(player);
		else if (absDistance >= PillarMinDistance && absDistance <= PillarMaxDistance && _pillarCooldownTimer <= 0f
			&& Rng.Randf() < PillarChance)
			_ = SoulPillar();
	}

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

	// ── Quick slash ─────────────────────────────────────────────────────────────────────────

	private async Task QuickSlash(float telegraph)
	{
		if (!Begin())
			return;
		try
		{
			Sprite.Play("quick_attack");
			Sprite.Pause();
			Sprite.Frame = 1;
			if (!await Wait(0.05f))
				return;
			Sprite.Frame = 2;
			FlashBody(telegraph);
			if (!await Wait(telegraph))
				return;
			Sprite.Frame = 3;
			_trail.Play(new Vector2(0f, -10f), 56f, -150f, 40f, 0.08f);
			bool hit = Strike(ArcCenter, ArcSize, SlashDamageMultiplier, 200f, centred: false);
			if (!await Wait(0.07f))
				return;
			Sprite.Frame = 4;
			if (!hit)
				Strike(SweepCenter, SweepSize, SlashDamageMultiplier, 200f, centred: false);
			if (!await Frames(5, 6, 0.07f))
				return;
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * Rng.RandfRange(0.85f, 1.2f);
	}

	// ── Soul pillar ─────────────────────────────────────────────────────────────────────────

	private async Task SoulPillar()
	{
		if (!Begin())
			return;
		_pillarCooldownTimer = PillarCooldown;
		try
		{
			Sprite.Play("power_attack");
			Sprite.Pause();
			if (!await Frames(1, 4, 0.05f))
				return;
			Sprite.Frame = 5;
			FlashBody(PillarTelegraph);
			if (!await Wait(PillarTelegraph))
				return;
			Sprite.Frame = 6;
			_trail.Play(new Vector2(4f, -14f), 64f, -150f, 30f, 0.09f);
			Strike(ArcCenter, ArcSize, SlashDamageMultiplier, 220f, centred: false);
			if (!await Wait(0.07f))
				return;
			Sprite.Frame = 7;
			Shake(2f, 0.12f);
			bool hit = false;
			for (int f = 8; f <= 16; f++)
			{
				Sprite.Frame = f;
				if (!hit && f <= 12)
					hit = Strike(PillarCenter, PillarSize, PillarDamageMultiplier, 300f, centred: false, launch: true);
				if (!await Wait(0.07f))
					return;
			}
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown;
	}

	// ── Sink & rise ─────────────────────────────────────────────────────────────────────────

	private async Task SinkAndRise(Metroidvania.Player.Player player)
	{
		if (!Begin())
			return;
		_sinkCooldownTimer = SinkCooldown;
		try
		{
			Sprite.Play("teleport_out");
			Sprite.SpeedScale = 2f;
			if (!await Wait(6f / 15f))
				return;
			_hidden = true;
			if (!await Wait(18f / 15f / 2f))
				return;
			Sprite.Visible = false;
			if (!await Wait(HiddenTime))
				return;
			if (!IsInstanceValid(player))
				return;
			// Rises beside the player (behind them if there's floor there, else in front).
			float behind = player.IsFacingRight ? -1f : 1f;
			if (!TryRiseAt(player.GlobalPosition.X + behind * RiseOffset) && !TryRiseAt(player.GlobalPosition.X - behind * RiseOffset))
				TryRiseAt(GlobalPosition.X);
			FacePlayer();
			Sprite.Visible = true;
			Sprite.Play("summon_in");
			Sprite.SpeedScale = 2.5f;
			if (!await Wait(31f / 15f / 2.5f))
				return;
			_hidden = false;
			Sprite.SpeedScale = 1f;
		}
		finally
		{
			if (IsInstanceValid(this))
			{
				Sprite.Visible = true;
				Sprite.SpeedScale = 1f;
				_hidden = false;
			}
			Finish();
		}
		_attackCooldownTimer = 0f;
		_ = QuickSlash(0.12f);
	}

	private bool TryRiseAt(float x)
	{
		var query = PhysicsRayQueryParameters2D.Create(new Vector2(x, GlobalPosition.Y - 60f), new Vector2(x, GlobalPosition.Y + 200f), 1u);
		var hit = GetWorld2D().DirectSpaceState.IntersectRay(query);
		if (hit.Count == 0)
			return false;
		Vector2 spot = new(x, ((Vector2)hit["position"]).Y - BodyBottom - 1f);
		var probe = GlobalTransform;
		probe.Origin = spot;
		if (TestMove(probe, Vector2.Zero))
			return false;
		GlobalPosition = spot;
		return true;
	}

	// ── Blade whirl ─────────────────────────────────────────────────────────────────────────

	private async Task BladeWhirl()
	{
		if (!Begin())
			return;
		_whirlCooldownTimer = WhirlCooldown;
		try
		{
			Sprite.Play("blade_spin_1");
			Sprite.Pause();
			FlashBody(0.3f);
			if (!await Frames(0, 3, 0.07f))
				return;
			float hitTimer = 0f;
			for (int f = 4; f <= 15; f++)
			{
				Sprite.Frame = f;
				hitTimer -= 0.06f;
				if (hitTimer <= 0f)
				{
					hitTimer = 0.25f;
					Strike(new Vector2(0f, 2f), WhirlSize, WhirlDamageMultiplier, 200f, centred: true);
				}
				if (!await Wait(0.06f))
					return;
			}
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * 0.7f;
	}

	// ── Shared ──────────────────────────────────────────────────────────────────────────────

	private async Task<bool> Frames(int from, int to, float each)
	{
		for (int f = from; f <= to; f++)
		{
			Sprite.Frame = f;
			if (!await Wait(each))
				return false;
		}
		return true;
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

	private bool Strike(Vector2 center, Vector2 size, float damageMultiplier, float knockback, bool centred, bool launch = false)
	{
		Vector2 at = GlobalPosition + new Vector2(FacingRight || centred ? center.X : -center.X, center.Y);
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
				float side = Mathf.Sign(player.GlobalPosition.X - GlobalPosition.X);
				player.ApplyKnockback((launch ? new Vector2(side * 0.4f, -1f) : new Vector2(side, -0.3f)).Normalized(), knockback);
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
		if (Sprite is not null)
		{
			Sprite.Visible = true;
			Sprite.SpeedScale = 1f;
		}
		base.OnDefeated();
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null || _busy)
			return;
		string anim = Mathf.Abs(velocity.X) > 5f ? "run" : "idle";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	protected override bool ContactDamageEnabled => false;
}
