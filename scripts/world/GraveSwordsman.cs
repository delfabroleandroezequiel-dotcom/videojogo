using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Esqueleto espadachín / Grave Swordsman (KBPixelArt Skeleton Enemy Pack, 128x128 cells, faces
// right). The rank-and-file grave skeleton: waits buried and lunges at you with a long low thrust.
// Sheet beats used:
//   attack (15): 1-5 the sword raised over the shoulder (wind-up), 6 the crescent slash as it
//     lunges, 7-11 stretched out in a low thrust, 12-14 back to guard.
//   digout (19): climbing out of the grave (GraveRise) — also played backwards to sink.
//   idle / walk / hurt / death (falls to a bone pile).
// Moves:
//   Lunge slash — wind-up (green blinks), then a crescent + a sliding low thrust that covers
//     ground; whiffing leaves it stretched out for a moment.
//   Grave dive — once, when it drops under half health: sinks into the floor and climbs back out
//     behind you, straight into a lunge.
//   Buried start — sits as a dirt mound until you come near (can't be hit until it's up).
// Telegraphs: green body blinks on the wind-up; the sinking itself before the dive.
public partial class GraveSwordsman : Enemy
{
	[ExportGroup("Grave")]
	[Export] public bool StartBuried = true;
	[Export] public float WakeDistance = 150f;
	[Export] public float WakeHeight = 80f;
	[Export] public float RiseTime = 1.2f;

	[ExportGroup("Lunge slash")]
	[Export] public float LungeTrigger = 95f;
	[Export] public float LungeWindup = 0.4f;
	[Export] public float LungeSpeed = 210f;
	[Export] public float LungeDamageMultiplier = 1.1f;
	[Export] public float AttackCooldown = 1.4f;
	// Frame 6: the crescent sweeps ~10-75px in front, chest high.
	[Export] public Vector2 SlashHitboxCenter = new(42f, -6f);
	[Export] public Vector2 SlashHitboxSize = new(60f, 46f);
	// Frames 7-9: the low thrust, out to ~70px at shin-to-hip height.
	[Export] public Vector2 ThrustHitboxCenter = new(50f, 12f);
	[Export] public Vector2 ThrustHitboxSize = new(44f, 24f);

	[ExportGroup("Grave dive")]
	[Export] public float DiveHealthFraction = 0.5f;
	[Export] public float DiveRange = 220f;
	[Export] public float SinkTime = 0.6f;
	[Export] public float UnderTime = 0.45f;
	[Export] public float ResurfaceTime = 0.7f;
	[Export] public float ResurfaceBehind = 45f;

	[ExportGroup("Look")]
	[Export] public Color FlashTint = new(1.4f, 2.6f, 1.5f, 1f);
	[Export] public Color TrailGlowColor = new(0.55f, 0.9f, 1f, 1f);

	// Raised by a GraveNecro's tombstone: climbs straight out, leaves no save mark and drops nothing.
	public bool Summoned;
	public const string SummonPersistenceId = "grave_necro_summon";

	private static readonly RandomNumberGenerator Rng = new();
	private readonly GraveRise _grave = new();

	private EnemySlashTrail _trail;
	private Tween _flashTween;
	private bool _busy;
	private bool _dived;
	private float _moveOverride;
	private float _attackCooldownTimer;

	static GraveSwordsman()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		if (Summoned)
		{
			LootTable = System.Array.Empty<LootEntry>();
			DropsShadowEssence = false;
		}
		_attackCooldownTimer = Rng.RandfRange(0.2f, 0.6f);
		_trail = new EnemySlashTrail
		{
			CoreWidth = 2f,
			GlowWidth = 8f,
			GlowColor = TrailGlowColor,
			CoreColor = new Color(0.9f, 1f, 1f, 1f),
			SparkAmount = 10,
		};
		Visual.AddChild(_trail);
		Stats.IncomingHitInterceptor = () => _grave.Busy;
		Stats.HitTaken += _ => OnHit();
		if (Summoned)
			_ = _grave.Rise(this, Sprite, RiseTime * 0.8f);
		else if (StartBuried)
			_grave.Bury(Sprite);
	}

	protected override bool IsDefeated() => !Summoned && base.IsDefeated();

	private void OnHit()
	{
		if (!_dived && Stats.CurrentHealth > 0 && Stats.CurrentHealth <= Stats.MaxHealth * DiveHealthFraction
			&& PlayerReads.Find(this) is { } player && GlobalPosition.DistanceTo(player.GlobalPosition) <= DiveRange)
		{
			_dived = true;
			_ = GraveDive(player);
			return;
		}
		if (!_busy)
			_ = Flinch();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;
		if (_grave.Busy)
		{
			_grave.WatchForPlayer(this, Sprite, WakeDistance, WakeHeight, RiseTime);
			Velocity = Vector2.Zero;
			return;
		}
		_attackCooldownTimer -= (float)delta;
		var player = PlayerReads.Find(this);
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval || player is null || !PlayerDetected || _busy || !IsOnFloor())
			return;

		float absDx = Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X);
		float absDy = Mathf.Abs(player.GlobalPosition.Y - GlobalPosition.Y);
		if (absDx <= LungeTrigger && absDy <= 50f && _attackCooldownTimer <= 0f)
			_ = LungeSlash();
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_busy)
			return _moveOverride == 0f || CanStepTo(Mathf.Sign(_moveOverride)) ? _moveOverride : 0f;
		return base.ComputeMoveX(player, distanceX, currentVelocityX, delta);
	}

	protected override bool CanTurnToFacePlayer => !_busy;

	// ── Lunge slash ──────────────────────────────────────────────────────────────────────────────

	private async Task LungeSlash()
	{
		if (!Begin())
			return;
		try
		{
			Sprite.Play("attack");
			Sprite.Pause();
			FlashBody(LungeWindup);
			for (int f = 1; f <= 5; f++)
			{
				Sprite.Frame = f;
				if (!await Wait(LungeWindup / 5f))
					return;
			}
			ClearFlash();

			float forward = FacingRight ? 1f : -1f;
			Sfx.PlayAt(this, "Combat/Sword", "Sword Attack");
			_trail.Play(new Vector2(16f, -2f), 34f, -75f, 70f, 0.08f, 28f);
			_moveOverride = forward * LungeSpeed;
			Sprite.Frame = 6;
			bool hit = Strike(SlashHitboxCenter, SlashHitboxSize, LungeDamageMultiplier);
			if (!await Wait(0.07f))
				return;
			for (int f = 7; f <= 9; f++)
			{
				Sprite.Frame = f;
				if (!hit)
					hit = Strike(ThrustHitboxCenter, ThrustHitboxSize, LungeDamageMultiplier);
				if (!await Wait(0.06f))
					return;
			}
			_moveOverride = 0f;
			// Stretched out — longer when it missed.
			for (int f = 10; f <= 11; f++)
			{
				Sprite.Frame = f;
				if (!await Wait(hit ? 0.08f : 0.18f))
					return;
			}
			for (int f = 12; f <= 14; f++)
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
		_attackCooldownTimer = AttackCooldown * Rng.RandfRange(0.85f, 1.25f);
	}

	// ── Grave dive ───────────────────────────────────────────────────────────────────────────────

	private async Task GraveDive(Metroidvania.Player.Player player)
	{
		// Takes over even mid-attack: the attack coroutine bails on its next Wait (the sprite is ours).
		_busy = true;
		_moveOverride = 0f;
		ClearFlash();
		try
		{
			await _grave.Sink(this, Sprite, SinkTime);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;
			Visual.Visible = false;
			await ToSignal(GetTree().CreateTimer(UnderTime), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;
			if (IsInstanceValid(player) && FindSurfaceBehind(player, out Vector2 spot))
				GlobalPosition = spot;
			Visual.Visible = true;
			if (IsInstanceValid(player))
			{
				FacingRight = player.GlobalPosition.X >= GlobalPosition.X;
				Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);
			}
			await _grave.Rise(this, Sprite, ResurfaceTime);
		}
		finally
		{
			if (IsInstanceValid(this))
			{
				Visual.Visible = true;
				_busy = false;
			}
		}
		_attackCooldownTimer = 0f;
	}

	// Floor spot just behind the player (the side they're facing away from), else in front of them.
	private bool FindSurfaceBehind(Metroidvania.Player.Player player, out Vector2 spot)
	{
		float side = player.GlobalPosition.X >= GlobalPosition.X ? 1f : -1f;
		foreach (float s in new[] { side, -side })
		{
			float x = player.GlobalPosition.X + s * ResurfaceBehind;
			var query = PhysicsRayQueryParameters2D.Create(new Vector2(x, player.GlobalPosition.Y - 20f), new Vector2(x, player.GlobalPosition.Y + 120f), 1u);
			var hit = GetWorld2D().DirectSpaceState.IntersectRay(query);
			if (hit.Count == 0)
				continue;
			spot = (Vector2)hit["position"] + new Vector2(0f, -BodyBottom - 1f);
			var probe = GlobalTransform;
			probe.Origin = spot;
			if (!TestMove(probe, Vector2.Zero))
				return true;
		}
		spot = GlobalPosition;
		return false;
	}

	// ── Shared ───────────────────────────────────────────────────────────────────────────────────

	private bool Strike(Vector2 center, Vector2 size, float damageMultiplier)
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
				player.ApplyKnockback(new Vector2(FacingRight ? 1f : -1f, -0.3f).Normalized(), 220f);
				BloodEffect.SpawnAt(this, player.GlobalPosition);
			}
			return true;
		}
		return false;
	}

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
			if (IsInstanceValid(this) && !_grave.Busy)
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
		if (!_grave.Busy)
			_busy = false;
		_moveOverride = 0f;
		ClearFlash();
		if (HoldingAttackSlot)
		{
			EnemyCombatCoordinator.ReleaseAttackSlot();
			HoldingAttackSlot = false;
		}
	}

	// Also bails when a grave dive took the sprite over mid-attack.
	private async Task<bool> Wait(float seconds)
	{
		await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
		return IsInstanceValid(this) && !IsQueuedForRemoval && !_grave.Busy;
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
		if (Sprite is null || _busy || _grave.Busy)
			return;
		string anim = Mathf.Abs(velocity.X) > 5f ? "walk" : "idle";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	protected override bool ContactDamageEnabled => false;
}
