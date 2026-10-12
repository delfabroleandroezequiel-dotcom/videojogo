using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Nigromante / Necromancer (Undead pack, 110x90 cells, faces right). The undead's caster and
// commander: fragile, keeps its distance, and fills the room with skeletons if left alone.
// Sheet beats used:
//   attack (8.8fps): 2-6 necrotic energy gathering over the off hand, 7 the hand thrust forward,
//     8-11 a green casting ring opening in front of it (the bolt leaves from it), 12-13 recovery.
//   spell (8.8fps): 2-3 staff raised, 4-7 a green flame swelling on the staff head (the charge),
//     8 the staff slammed down, 9-12 a necrotic burst erupting all around its body, 13-14 recovery.
//   jump: the hop it backs off with.
// Moves:
//   Necrotic bolt — the ring spits a slow orb that bends toward you and poisons (NecroBolt).
//   Raise dead — charges the staff and slams it: skeleton warriors climb out of the floor near you
//     (SkeletonWarrior with Summoned set). At most MaxSummons alive; they crumble when it dies.
//   Death nova — if you get in close, the staff slam bursts necrotic energy all around it, hurting
//     and throwing you back.
//   Back off — keeps its distance, and hops away when cornered with the nova on cooldown.
// Telegraphs: the body flashes white before every cast; the staff glows while charging.
public partial class Necromancer : Enemy
{
	[ExportGroup("Spacing")]
	[Export] public float MinDistance = 130f;
	[Export] public float MaxDistance = 260f;
	[Export] public float RetreatOvershoot = 40f;
	[Export] public float RetreatSpeedMultiplier = 1.2f;

	[ExportGroup("Bolt")]
	[Export] public float BoltRange = 380f;
	[Export] public float AttackCooldown = 2f;
	[Export] public float BoltDamageMultiplier = 0.8f;
	// The casting ring, measured off attack frames 9-11 (scale 1.5, sprite at 2,-12).
	[Export] public Vector2 BoltOrigin = new(40f, -8f);

	[ExportGroup("Raise dead")]
	[Export] public PackedScene SummonScene;
	[Export] public int MaxSummons = 2;
	[Export] public float SummonCooldown = 10f;
	[Export] public float SummonCharge = 0.8f;
	[Export] public int SummonHealth = 18;

	[ExportGroup("Death nova")]
	[Export] public float NovaTrigger = 70f;
	[Export] public float NovaCooldown = 4f;
	[Export] public float NovaCharge = 0.45f;
	[Export] public float NovaRadius = 62f;
	[Export] public float NovaDamageMultiplier = 1.2f;
	[Export] public float NovaKnockback = 360f;

	[ExportGroup("Back off")]
	[Export] public Vector2 HopVelocity = new(190f, -300f);
	[Export] public float HopCooldown = 3f;

	[ExportGroup("Look")]
	[Export] public Color GlowColor = new(0.55f, 1f, 0.3f, 1f);
	[Export] public Color FlashTint = new(2.2f, 2.2f, 2.2f, 1f);
	[Export] public float DecisionInterval = 0.35f;

	private const float CastFps = 8.8f;

	private static readonly RandomNumberGenerator Rng = new();
	private readonly DashWatcher _dash = new();
	private readonly List<SkeletonWarrior> _summons = new();

	private Tween _flashTween;
	private bool _busy;
	private bool _hopping;
	private bool _retreating;
	private float _decisionTimer;
	private float _attackCooldownTimer;
	private float _summonCooldownTimer;
	private float _novaCooldownTimer;
	private float _hopCooldownTimer;

	static Necromancer()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_attackCooldownTimer = Rng.RandfRange(0.6f, 1.2f);
		_summonCooldownTimer = Rng.RandfRange(1.5f, 3f);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;
		float dt = (float)delta;
		_decisionTimer -= dt;
		_attackCooldownTimer -= dt;
		_summonCooldownTimer -= dt;
		_novaCooldownTimer -= dt;
		_hopCooldownTimer -= dt;
		_summons.RemoveAll(s => !IsInstanceValid(s) || s.Stats is null || s.Stats.CurrentHealth <= 0);

		var player = PlayerReads.Find(this);
		_dash.Update(player, dt);

		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval)
			return;
		if (_hopping && IsOnFloor() && Velocity.Y >= 0f)
			_hopping = false;
		if (player is null || !PlayerDetected || _busy || _hopping || !IsOnFloor())
			return;
		Think(player);
	}

	// ── Decisions ───────────────────────────────────────────────────────────────────────────

	private void Think(Metroidvania.Player.Player player)
	{
		float distanceX = player.GlobalPosition.X - GlobalPosition.X;
		float absDistance = Mathf.Abs(distanceX);

		// Crowded: the nova if it's ready, otherwise hop away.
		if (absDistance <= NovaTrigger)
		{
			if (_novaCooldownTimer <= 0f)
			{
				_ = DeathNova();
				return;
			}
			if (_hopCooldownTimer <= 0f)
			{
				_ = HopAway(-Mathf.Sign(distanceX));
				return;
			}
		}

		if (_decisionTimer > 0f)
			return;
		_decisionTimer = DecisionInterval;

		if (_summonCooldownTimer <= 0f && _summons.Count < MaxSummons && SummonScene is not null && absDistance > NovaTrigger)
		{
			_ = RaiseDead(player);
			return;
		}
		if (_attackCooldownTimer <= 0f && absDistance <= BoltRange && _dash.GoodMomentToRelease(player, GlobalPosition.X, 0.1f))
			_ = CastBolt();
	}

	// ── Movement ────────────────────────────────────────────────────────────────────────────

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_hopping)
			return currentVelocityX;
		if (_busy)
			return 0f;
		return KitingMoveX(distanceX, currentVelocityX, MinDistance, MaxDistance, RetreatOvershoot, RetreatSpeedMultiplier, ref _retreating);
	}

	protected override bool DesiredFacingRight(Node2D player, float distanceX) => _retreating ? distanceX < 0 : distanceX >= 0;

	protected override bool CanTurnToFacePlayer => !_busy && !_hopping;

	// ── Bolt ────────────────────────────────────────────────────────────────────────────────

	private async Task CastBolt()
	{
		if (!Begin())
			return;
		try
		{
			Sprite.Play("attack");
			Sprite.Pause();
			FlashBody(0.6f);
			for (int f = 2; f <= 11; f++)
			{
				Sprite.Frame = f;
				if (f == 10)
					SpawnBolt();
				if (!await Wait(1f / CastFps * (f < 7 ? 0.8f : 1f)))
					return;
			}
			Sprite.Frame = 12;
			if (!await Wait(0.15f))
				return;
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * Rng.RandfRange(0.85f, 1.25f);
	}

	private void SpawnBolt()
	{
		if (GetTree().CurrentScene is not Node scene || PlayerReads.Find(this) is not { } player)
			return;
		Vector2 origin = GlobalPosition + new Vector2(FacingRight ? BoltOrigin.X : -BoltOrigin.X, BoltOrigin.Y);
		var bolt = new NecroBolt
		{
			Direction = (player.GlobalPosition - origin).Normalized(),
			ThrowerStats = Stats,
			DamageMultiplier = BoltDamageMultiplier,
			GlowColor = GlowColor,
		};
		scene.AddChild(bolt);
		bolt.GlobalPosition = origin;
	}

	// ── Raise dead ──────────────────────────────────────────────────────────────────────────

	private async Task RaiseDead(Metroidvania.Player.Player player)
	{
		if (!Begin())
			return;
		_summonCooldownTimer = SummonCooldown;
		try
		{
			if (!await StaffCharge(SummonCharge))
				return;
			Sprite.Frame = 8;
			if (!await Wait(1f / CastFps))
				return;
			Sprite.Frame = 9;
			// One skeleton per free slot, up to two, on the floor between it and the player.
			int count = Mathf.Min(2, MaxSummons - _summons.Count);
			for (int i = 0; i < count; i++)
			{
				float t = count == 1 ? 0.55f : 0.35f + 0.4f * i;
				float x = Mathf.Lerp(GlobalPosition.X, player.GlobalPosition.X, t) + Rng.RandfRange(-12f, 12f);
				SpawnSkeleton(x);
			}
			for (int f = 10; f <= 14; f++)
			{
				if (!await Wait(1f / CastFps))
					return;
				Sprite.Frame = f;
			}
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * 0.6f;
	}

	private void SpawnSkeleton(float x)
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		// Find the floor under that spot (from the necromancer's height).
		var query = PhysicsRayQueryParameters2D.Create(new Vector2(x, GlobalPosition.Y - 30f), new Vector2(x, GlobalPosition.Y + 160f), 1u);
		var hit = GetWorld2D().DirectSpaceState.IntersectRay(query);
		if (hit.Count == 0 || SummonScene.Instantiate() is not SkeletonWarrior skeleton)
			return;
		skeleton.Summoned = true;
		skeleton.CustomPersistenceId = SkeletonWarrior.SummonPersistenceId;
		scene.AddChild(skeleton);
		// The collision box is 60 tall and centred, so its feet sit 30 below its origin.
		skeleton.GlobalPosition = (Vector2)hit["position"] + new Vector2(0f, -31f);
		skeleton.Stats.MaxHealth = SummonHealth;
		skeleton.Stats.ResetToFull();
		_summons.Add(skeleton);
		RiseBurst((Vector2)hit["position"]);
	}

	// ── Death nova ──────────────────────────────────────────────────────────────────────────

	private async Task DeathNova()
	{
		if (!Begin())
			return;
		_novaCooldownTimer = NovaCooldown;
		try
		{
			if (!await StaffCharge(NovaCharge))
				return;
			Sprite.Frame = 8;
			if (!await Wait(0.06f))
				return;
			Sprite.Frame = 9;
			Nova();
			for (int f = 10; f <= 14; f++)
			{
				if (!await Wait(1f / CastFps))
					return;
				Sprite.Frame = f;
			}
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = Mathf.Max(_attackCooldownTimer, 0.6f);
	}

	private void Nova()
	{
		Vector2 center = GlobalPosition + new Vector2(0f, 4f);
		RingFx(center, NovaRadius);
		if (PlayerReads.Find(this) is not { } player || player.GlobalPosition.DistanceTo(center) > NovaRadius + 16f)
			return;
		var stats = player.GetNodeOrNull<Stats>("Stats");
		if (stats is null || stats.IsInvulnerable)
			return;
		int before = stats.CurrentHealth;
		stats.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(Stats.AttackPower * NovaDamageMultiplier)), element: DamageElement.Poison);
		if (stats.CurrentHealth < before)
			player.ApplyKnockback(new Vector2(Mathf.Sign(player.GlobalPosition.X - GlobalPosition.X), -0.5f).Normalized(), NovaKnockback);
		if (player.GlobalPosition.DistanceTo(GlobalPosition) < 700f)
			player.ShakeCamera(2.5f, 0.15f);
	}

	// Staff raised (2-3) then the flame swelling on its head (4-7 looping) for `duration`.
	private async Task<bool> StaffCharge(float duration)
	{
		Sprite.Play("spell");
		Sprite.Pause();
		Sprite.Frame = 2;
		if (!await Wait(1f / CastFps))
			return false;
		Sprite.Frame = 3;
		FlashBody(duration);
		float t = 0f;
		int frame = 4;
		while (t < duration)
		{
			Sprite.Frame = frame;
			frame = frame >= 7 ? 4 : frame + 1;
			if (!await Wait(1f / CastFps))
				return false;
			t += 1f / CastFps;
		}
		return true;
	}

	// ── Back off ────────────────────────────────────────────────────────────────────────────

	private async Task HopAway(float away)
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

	// ── FX ──────────────────────────────────────────────────────────────────────────────────

	private void RingFx(Vector2 center, float radius)
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		const int segments = 28;
		var points = new Vector2[segments + 1];
		for (int i = 0; i <= segments; i++)
		{
			float a = Mathf.Tau * i / segments;
			points[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.55f);
		}
		var ring = new Line2D
		{
			Points = points,
			Width = 0.12f,
			DefaultColor = GlowColor,
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			Scale = Vector2.One * radius * 0.3f,
		};
		scene.AddChild(ring);
		ring.GlobalPosition = center;
		Tween tween = ring.CreateTween();
		tween.TweenProperty(ring, "scale", Vector2.One * radius, 0.22f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		tween.Parallel().TweenProperty(ring, "modulate:a", 0f, 0.35f);
		tween.TweenCallback(Callable.From(ring.QueueFree));
	}

	// A green flare where a skeleton climbs out of the floor.
	private void RiseBurst(Vector2 ground)
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		var flare = new Sprite2D
		{
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			Modulate = GlowColor,
			Scale = new Vector2(1.1f, 0.35f),
		};
		scene.AddChild(flare);
		flare.GlobalPosition = ground + new Vector2(0f, -4f);
		Tween tween = flare.CreateTween();
		tween.TweenProperty(flare, "modulate:a", 0f, 1.2f);
		tween.TweenCallback(Callable.From(flare.QueueFree));
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

	// Its skeletons crumble with it.
	protected override void OnDefeated()
	{
		ClearFlash();
		foreach (var skeleton in _summons)
		{
			if (IsInstanceValid(skeleton) && skeleton.Stats is { CurrentHealth: > 0 } stats)
				stats.Kill();
		}
		_summons.Clear();
		base.OnDefeated();
	}

	// ── Animation ───────────────────────────────────────────────────────────────────────────

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null || _busy || _hopping)
			return;
		string anim = !IsOnFloor() ? "jump" : Mathf.Abs(velocity.X) > 5f ? "walk" : "idle";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	protected override bool ContactDamageEnabled => false;
}
