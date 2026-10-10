using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Elf swordsman (Elves pack) — a fencing master with a greatsword, on the same "read the player,
// score the options" approach as the other elves. Its sheet (138x96 cells, 12fps, faces right):
//   attack1: 0 sword low behind (wind-up), 1 a wide cut swept over the top to the front, 2-3 the
//            blade carried back up behind it — which is attack2's frame 0, so they chain.
//   attack2: 0 sword up behind, 1 raised straight up, 2 the heavy vertical cleave down the front
//            to the floor, 3-5 follow-through and recovery.
// Moves:
//   Footsies — keeps the player at the tip of its sword, steps in and out, never off a ledge.
//   Sweep (attack1), Sweep → Cleave chain, or the heavy Cleave alone.
//   Guard & riposte — reacts to a swing starting in front of it by raising the sword upright
//     (attack2 frame 1): frontal hits and projectiles are parried, and every parry is answered
//     by an instant sweep. From behind it's open.
//   Wind cleave — from mid range the heavy cleave sends an elf-green crescent sliding along the
//     floor (ElfSwordWave).
//   Lunge — a whiffed swing or a dash ending near it gets rushed and swept with a short wind-up.
// Telegraphs: the body flashes white before every cut; elf-green neon trails reach out to each
// cut's real reach.
public partial class ElfSwordsman : Enemy
{
	[ExportGroup("Spacing")]
	[Export] public float IdealDistance = 70f;
	[Export] public float TooClose = 42f;
	[Export] public float RunDistance = 220f;
	[Export] public float RunSpeedMultiplier = 1.6f;

	[ExportGroup("Cuts")]
	[Export] public float SwordReach = 80f;
	[Export] public float AttackCooldown = 1.3f;
	[Export] public float SweepTelegraph = 0.3f;
	[Export] public float CleaveTelegraph = 0.45f;
	[Export] public float ChainChance = 0.35f;
	[Export] public float HeavyAloneChance = 0.2f;
	[Export] public float SweepDamageMultiplier = 1f;
	[Export] public float CleaveDamageMultiplier = 1.35f;
	[Export] public float CutKnockback = 240f;
	// Measured off the attack rows (scale 1.5, sprite at -1.5,-38.5): the sweep reaches ~73px in
	// front and over the head; the cleave ~78px in front, from overhead to the floor.
	[Export] public Vector2 SweepHitboxCenter = new(40f, -22f);
	[Export] public Vector2 SweepHitboxSize = new(70f, 62f);
	[Export] public Vector2 CleaveHitboxCenter = new(46f, -16f);
	[Export] public Vector2 CleaveHitboxSize = new(64f, 92f);
	[Export] public Color TrailColor = new(0.35f, 1f, 0.45f, 1f);
	[Export] public Color FlashTint = new(2.2f, 2.2f, 2.2f, 1f);

	[ExportGroup("Guard")]
	[Export] public float GuardChance = 0.5f;
	[Export] public float GuardRange = 95f;
	[Export] public float GuardDuration = 0.9f;
	[Export] public float GuardCooldown = 2.2f;
	[Export] public float RiposteTelegraph = 0.06f;

	[ExportGroup("Wind cleave")]
	[Export] public float WaveMinDistance = 140f;
	[Export] public float WaveMaxDistance = 340f;
	[Export] public float WaveCooldown = 4.5f;
	[Export] public float WaveChance = 0.45f;

	[ExportGroup("Lunge")]
	[Export] public float PunishRange = 170f;
	[Export] public float LungeSpeed = 320f;
	[Export] public float LungeMaxTime = 0.5f;
	[Export] public float PunishTelegraph = 0.12f;
	[Export] public float DecisionInterval = 0.35f;
	[Export] public float HurtDuration = 0.25f;

	private const float AttackFps = 12f;

	private static readonly RandomNumberGenerator Rng = new();
	private readonly DashWatcher _dash = new();
	private readonly WhiffWatcher _whiff = new();

	private EnemySlashTrail _trail;
	private Tween _flashTween;
	private bool _busy;
	private bool _uncancellable;
	private bool _cancel;
	private bool _guarding;
	private bool _parried;
	private bool _lunging;
	private float _lungeTimer;
	private float _hurtTimer;
	private float _decisionTimer;
	private float _attackCooldownTimer;
	private float _guardCooldownTimer;
	private float _waveCooldownTimer;

	static ElfSwordsman()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_attackCooldownTimer = Rng.RandfRange(0.3f, 0.8f);
		_waveCooldownTimer = Rng.RandfRange(0.3f, 0.9f);
		_trail = new EnemySlashTrail
		{
			CoreWidth = 2.5f,
			GlowWidth = 8f,
			GlowColor = TrailColor,
			CoreColor = new Color(0.88f, 1f, 0.9f, 1f),
			MinPointSpacing = 1.5f,
			SparkAmount = 12,
		};
		Visual.AddChild(_trail);
		Stats.IncomingHitInterceptor = TryParry;
		Stats.HitTaken += _ => OnHurt();
		Stats.HitTaken += _ => _whiff.NotifyHitTaken();
	}

	private void OnHurt()
	{
		_hurtTimer = HurtDuration;
		if (_busy && !_uncancellable)
			_cancel = true;
	}

	private bool PlayerInFront(Node2D player) => (player.GlobalPosition.X >= GlobalPosition.X) == FacingRight;

	// While guarding, frontal hits (and projectiles) are parried; from behind they land.
	private bool TryParry()
	{
		if (!_guarding)
			return false;
		var player = PlayerReads.Find(this);
		if (player is null || !PlayerInFront(player))
			return false;
		_parried = true;
		ParryFlash();
		Sfx.PlayAt(this, "Combat/Sword", "Sword Blocked");
		return true;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;
		float dt = (float)delta;
		_hurtTimer -= dt;
		_decisionTimer -= dt;
		_attackCooldownTimer -= dt;
		_guardCooldownTimer -= dt;
		_waveCooldownTimer -= dt;
		_lungeTimer -= dt;

		var player = PlayerReads.Find(this);
		_dash.Update(player, dt);
		_whiff.Update(player, dt);

		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval || player is null || !PlayerDetected || _busy || !IsOnFloor())
			return;
		Think(player);
	}

	// ── Decisions ───────────────────────────────────────────────────────────────────────────

	private void Think(Metroidvania.Player.Player player)
	{
		float distanceX = player.GlobalPosition.X - GlobalPosition.X;
		float absDistance = Mathf.Abs(distanceX);

		// A swing starting in front of it → raise the guard.
		if (_whiff.PlayerStartedSwing && absDistance <= GuardRange && PlayerInFront(player)
			&& _guardCooldownTimer <= 0f && Rng.Randf() < GuardChance)
		{
			_ = Guard();
			return;
		}

		// A whiff or a dash ending nearby → lunge in.
		if ((_whiff.JustWhiffed || _dash.SinceDashEnded < 0.1f) && absDistance <= PunishRange && _attackCooldownTimer <= 0f)
		{
			_whiff.Consume();
			_lunging = true;
			_lungeTimer = LungeMaxTime;
		}

		if (_lunging)
		{
			if (absDistance <= SwordReach)
			{
				_lunging = false;
				_ = Cuts(PunishTelegraph, sweep: true, cleave: false, wave: false);
			}
			else if (_lungeTimer <= 0f)
			{
				_lunging = false;
			}
			return;
		}

		if (_attackCooldownTimer > 0f)
			return;

		if (absDistance <= SwordReach)
		{
			float roll = Rng.Randf();
			if (roll < HeavyAloneChance)
				_ = Cuts(CleaveTelegraph, sweep: false, cleave: true, wave: false);
			else if (roll < HeavyAloneChance + ChainChance)
				_ = Cuts(SweepTelegraph, sweep: true, cleave: true, wave: false);
			else
				_ = Cuts(SweepTelegraph, sweep: true, cleave: false, wave: false);
			return;
		}

		if (_decisionTimer > 0f)
			return;
		_decisionTimer = DecisionInterval * Rng.RandfRange(0.8f, 1.3f);
		if (absDistance >= WaveMinDistance && absDistance <= WaveMaxDistance && _waveCooldownTimer <= 0f
			&& Rng.Randf() < WaveChance)
			_ = Cuts(CleaveTelegraph, sweep: false, cleave: true, wave: true);
	}

	// ── Movement ────────────────────────────────────────────────────────────────────────────

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_busy || _hurtTimer > 0f)
			return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
		float absDistance = Mathf.Abs(distanceX);
		float toward = Mathf.Sign(distanceX);
		if (_lunging)
			return CanStepTo(toward) ? toward * LungeSpeed : 0f;
		if (absDistance < TooClose && CanStepTo(-toward))
			return -toward * MoveSpeed * 0.8f;
		if (absDistance > IdealDistance && CanStepTo(toward))
			return toward * MoveSpeed * (absDistance > RunDistance ? RunSpeedMultiplier : 1f);
		return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
	}

	protected override bool DesiredFacingRight(Node2D player, float distanceX) => distanceX >= 0;

	protected override bool CanTurnToFacePlayer => !_busy;

	// ── Cuts ────────────────────────────────────────────────────────────────────────────────

	private async Task Cuts(float telegraph, bool sweep, bool cleave, bool wave)
	{
		if (!EnemyCombatCoordinator.TryAcquireAttackSlot())
		{
			_attackCooldownTimer = 0.3f;
			return;
		}
		HoldingAttackSlot = true;
		_busy = true;
		_cancel = false;
		var player = PlayerReads.Find(this);
		if (player is not null)
			FaceTowards(player.GlobalPosition.X);

		try
		{
			if (sweep)
			{
				// Wind-up: sword low behind (attack1 frame 0).
				Sprite.Play("attack1");
				Sprite.Pause();
				Sprite.Frame = 0;
				FlashBody(telegraph);
				if (!await WaitChecked(telegraph))
					return;
				_uncancellable = true;
				Sprite.Frame = 1;
				_trail.Play(new Vector2(4f, -26f), 72f, -150f, 40f, 0.09f);
				Strike(SweepHitboxCenter, SweepHitboxSize, SweepDamageMultiplier);
				if (!await Wait(1f / AttackFps))
					return;
				Sprite.Frame = 2;
				if (!await Wait(1f / AttackFps))
					return;
				Sprite.Frame = 3;
				if (!await Wait(1f / AttackFps))
					return;
				_uncancellable = false;
			}

			if (cleave)
			{
				// Wind-up: sword raised straight up (attack2 frame 1); chained, it's quicker.
				Sprite.Play("attack2");
				Sprite.Pause();
				Sprite.Frame = sweep ? 0 : 1;
				float hold = sweep ? 1f / AttackFps : telegraph;
				if (!sweep)
					FlashBody(telegraph);
				if (!await WaitChecked(hold))
					return;
				if (sweep)
				{
					Sprite.Frame = 1;
					FlashBody(0.12f);
					if (!await WaitChecked(0.12f))
						return;
				}
				_uncancellable = true;
				Sprite.Frame = 2;
				_trail.Play(new Vector2(10f, -32f), 76f, -85f, 72f, 0.08f);
				Strike(CleaveHitboxCenter, CleaveHitboxSize, CleaveDamageMultiplier);
				float forward = FacingRight ? 1f : -1f;
				if (TryFindGround(GlobalPosition.X + forward * 70f, out Vector2 tip))
					GroundSlamFx.Chip(this, tip, 0.6f, ElfPalette());
				if (wave)
				{
					SpawnWave(forward);
					_waveCooldownTimer = WaveCooldown;
				}
				for (int f = 3; f <= 5; f++)
				{
					if (!await Wait(1f / AttackFps))
						return;
					Sprite.Frame = f;
				}
			}
			if (!await Wait(0.18f))
				return;
		}
		finally
		{
			if (IsInstanceValid(this))
				_uncancellable = false;
		}
		_attackCooldownTimer = AttackCooldown * Rng.RandfRange(0.85f, 1.2f);
		EndAction();
	}

	private void SpawnWave(float forward)
	{
		if (!TryFindGround(GlobalPosition.X + forward * 40f, out Vector2 ground))
			return;
		var wave = new ElfSwordWave { Direction = forward, ThrowerStats = Stats, GlowColor = TrailColor };
		GetTree().CurrentScene.AddChild(wave);
		wave.GlobalPosition = ground;
	}

	// ── Guard ───────────────────────────────────────────────────────────────────────────────

	private async Task Guard()
	{
		_busy = true;
		_uncancellable = true;
		_guarding = true;
		_parried = false;
		_guardCooldownTimer = GuardCooldown;
		var player = PlayerReads.Find(this);
		if (player is not null)
			FaceTowards(player.GlobalPosition.X);

		Sprite.Play("attack2");
		Sprite.Pause();
		Sprite.Frame = 1;
		GuardGlint();

		float t = 0f;
		while (t < GuardDuration && !_parried)
		{
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;
			t += (float)GetPhysicsProcessDeltaTime();
		}
		_guarding = false;
		_uncancellable = false;
		_busy = false;

		// Parried → instant riposte sweep.
		if (_parried)
		{
			_attackCooldownTimer = 0f;
			_ = Cuts(RiposteTelegraph, sweep: true, cleave: Rng.Randf() < 0.4f, wave: false);
		}
	}

	private void GuardGlint()
	{
		var glint = new Sprite2D
		{
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			Modulate = TrailColor,
			Position = new Vector2(0f, -70f),
			Scale = Vector2.One * 0.15f,
		};
		Visual.AddChild(glint);
		Tween tween = glint.CreateTween();
		tween.TweenProperty(glint, "scale", Vector2.One * 0.55f, 0.1f);
		tween.TweenProperty(glint, "modulate:a", 0f, GuardDuration);
		tween.TweenCallback(Callable.From(glint.QueueFree));
	}

	private void ParryFlash()
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		const int segments = 20;
		var points = new Vector2[segments + 1];
		for (int i = 0; i <= segments; i++)
		{
			float a = Mathf.Tau * i / segments;
			points[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 26f;
		}
		var ring = new Line2D
		{
			Points = points,
			Width = 3f,
			DefaultColor = new Color(0.85f, 1f, 0.9f, 1f),
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			Scale = Vector2.One * 0.3f,
			ZIndex = 3,
		};
		scene.AddChild(ring);
		ring.GlobalPosition = GlobalPosition + new Vector2(FacingRight ? 22f : -22f, -26f);
		Tween tween = ring.CreateTween();
		tween.TweenProperty(ring, "scale", Vector2.One, 0.15f).SetEase(Tween.EaseType.Out);
		tween.Parallel().TweenProperty(ring, "modulate:a", 0f, 0.2f);
		tween.TweenCallback(Callable.From(ring.QueueFree));
		ImpactEffect.SpawnAt(this, ring.GlobalPosition);
	}

	// ── Shared ──────────────────────────────────────────────────────────────────────────────

	private void Strike(Vector2 center, Vector2 size, float damageMultiplier)
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
			if (hit["collider"].AsGodotObject() is not Node2D body || !body.IsInGroup("player"))
				continue;
			if (body.GetNodeOrNull<Stats>("Stats") is not { } stats || stats.IsInvulnerable)
				continue;
			stats.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(Stats.AttackPower * damageMultiplier)));
			if (body.HasMethod("ApplyKnockback"))
				body.Call("ApplyKnockback", new Vector2(Mathf.Sign(body.GlobalPosition.X - GlobalPosition.X), -0.2f).Normalized(), CutKnockback);
			BloodEffect.SpawnAt(this, body.GlobalPosition);
			break;
		}
	}

	private bool TryFindGround(float x, out Vector2 point)
	{
		point = default;
		var query = PhysicsRayQueryParameters2D.Create(new Vector2(x, GlobalPosition.Y - 10f), new Vector2(x, GlobalPosition.Y + BodyBottom + 60f),
			1u | PhysicsLayers.ClimbableWalls | PhysicsLayers.OneWayPlatforms, new Godot.Collections.Array<Rid> { GetRid() });
		var hit = GetWorld2D().DirectSpaceState.IntersectRay(query);
		if (hit.Count == 0)
			return false;
		point = (Vector2)hit["position"];
		return true;
	}

	private GroundSlamFx.Palette ElfPalette() => new()
	{
		Hot = TrailColor,
		Core = new Color(0.9f, 1f, 0.92f, 1f),
		Rock = new Color(0.4f, 0.36f, 0.3f, 1f),
		Dust = new Color(0.55f, 0.55f, 0.45f, 0.5f),
		Light = TrailColor,
	};

	private async Task<bool> Wait(float seconds)
	{
		await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
		return IsInstanceValid(this) && !IsQueuedForRemoval;
	}

	// Like Wait, but a hit during it cancels the action (wind-ups only).
	private async Task<bool> WaitChecked(float seconds)
	{
		float t = 0f;
		while (t < seconds)
		{
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return false;
			if (_cancel)
			{
				_attackCooldownTimer = AttackCooldown * 0.6f;
				EndAction();
				return false;
			}
			t += (float)GetPhysicsProcessDeltaTime();
		}
		return true;
	}

	private void EndAction()
	{
		_busy = false;
		_cancel = false;
		ClearFlash();
		if (HoldingAttackSlot)
		{
			EnemyCombatCoordinator.ReleaseAttackSlot();
			HoldingAttackSlot = false;
		}
	}

	private void FaceTowards(float targetX)
	{
		FacingRight = targetX >= GlobalPosition.X;
		Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);
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
		_guarding = false;
		ClearFlash();
		base.OnDefeated();
	}

	// ── Animation ───────────────────────────────────────────────────────────────────────────

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null || _busy)
			return;
		string anim;
		if (!IsOnFloor())
			anim = velocity.Y < 0f ? "jump" : "fall";
		else if (_hurtTimer > 0f)
			anim = "hitnoeffect";
		else if (Mathf.Abs(velocity.X) > MoveSpeed * 1.1f)
			anim = "run";
		else if (Mathf.Abs(velocity.X) > 5f)
			anim = "walk";
		else
			anim = "idle";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	protected override bool ContactDamageEnabled => false;
}
