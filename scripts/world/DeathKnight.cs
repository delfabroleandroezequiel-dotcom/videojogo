using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Caballero de la muerte / Death Knight (Undead pack, 110x80 cells, faces right). The undead elite:
// heavy, armoured, relentless, and its runic sword drinks the life of whatever it cuts.
// Sheet beats used:
//   attack (8fps): 0-2 settle, 3 sword raised overhead (held as the wind-up), 4 the big cleave down
//     the front, 5 the low follow-through, 6-7 recovery.
//   spell (8.5fps): 2-5 a soul flame gathering in the off hand (the charge), 6 it's drawn into the
//     blade, 7-9 a rising slash from low front to high, 10 recovery.
//   run: the cape-flying charge it closes distance with.
// Moves:
//   Cleave — the big overhead cut. When it misses it often flows into the rising slash, catching
//     whoever dodged or rolled past the first cut.
//   Rising slash — the spell's upswing on its own up close: launches you up and away.
//   Soul spikes — from mid range it charges the flame (glowing hand) and the rising slash sends a
//     row of spectral spikes marching along the floor at you (SoulSpikeWave).
//   Rush — far away it breaks into a run and arrives swinging.
//   Life drain — every cut that lands heals it for DrainRatio of the damage dealt.
//   Wrath — below WrathHealthRatio its cape trails afterimages, it attacks faster and its
//     telegraphs shorten. Never flinches during an attack.
//   Punish — a whiffed swing or a dash ending in reach gets a quick cleave.
// Telegraphs: body flashes white before every attack; the soul-flame charge glows teal.
public partial class DeathKnight : Enemy
{
	[ExportGroup("Cleave")]
	[Export] public float CleaveRange = 58f;
	[Export] public float AttackCooldown = 1.35f;
	[Export] public float CleaveTelegraph = 0.5f;
	[Export] public float ChainChance = 0.45f;
	[Export] public float CleaveDamageMultiplier = 1f;
	[Export] public float CleaveKnockback = 260f;
	// Measured off attack frame 4 (scale 1.5, sprite at 0,-3): the arc reaches ~58px in front, from
	// well above the helmet down to the feet.
	[Export] public Vector2 CleaveHitboxCenter = new(34f, -10f);
	[Export] public Vector2 CleaveHitboxSize = new(50f, 82f);

	[ExportGroup("Rising slash")]
	[Export] public float RisingDamageMultiplier = 0.9f;
	[Export] public float RisingKnockback = 320f;
	// spell frames 7-8: an upswing reaching ~39px in front, from the knees to over the head.
	[Export] public Vector2 RisingHitboxCenter = new(28f, -22f);
	[Export] public Vector2 RisingHitboxSize = new(44f, 72f);

	[ExportGroup("Soul spikes")]
	[Export] public float SpikesMinDistance = 80f;
	[Export] public float SpikesMaxDistance = 300f;
	[Export] public float SpikesChance = 0.5f;
	[Export] public float SpikesCooldown = 5f;
	[Export] public float SpikesCharge = 0.7f;
	[Export] public float SpikesDamageMultiplier = 0.9f;
	[Export] public int SpikesCount = 8;

	[ExportGroup("Rush")]
	[Export] public float RushDistance = 200f;
	[Export] public float RushSpeedMultiplier = 2f;

	[ExportGroup("Drain & wrath")]
	[Export] public float DrainRatio = 0.4f;
	[Export] public float WrathHealthRatio = 0.4f;
	[Export] public float WrathCooldownMultiplier = 0.7f;
	[Export] public float WrathTelegraphMultiplier = 0.7f;
	[Export] public float WrathSpeedMultiplier = 1.2f;

	[ExportGroup("Look")]
	[Export] public Color TrailGlowColor = new(0.2f, 1f, 0.8f, 1f);
	[Export] public Color TrailCoreColor = new(0.85f, 1f, 0.96f, 1f);
	[Export] public Color AfterimageColor = new(0.85f, 0.1f, 0.15f, 1f);
	[Export] public Color FlashTint = new(2.2f, 2.2f, 2.2f, 1f);

	[Export] public float DecisionInterval = 0.4f;

	private const float AttackFps = 8f;
	private const float SpellFps = 8.5f;

	private static readonly RandomNumberGenerator Rng = new();
	private readonly DashWatcher _dash = new();
	private readonly WhiffWatcher _whiff = new();

	private EnemySlashTrail _trail;
	private Tween _flashTween;
	private bool _busy;
	private bool _wrath;
	private float _moveOverride;
	private float _baseMoveSpeed;
	private float _decisionTimer;
	private float _attackCooldownTimer;
	private float _spikesCooldownTimer;
	private float _afterimageTimer;

	static DeathKnight()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_baseMoveSpeed = MoveSpeed;
		_attackCooldownTimer = Rng.RandfRange(0.5f, 1f);
		_spikesCooldownTimer = Rng.RandfRange(0.8f, 1.6f);
		_trail = new EnemySlashTrail
		{
			CoreWidth = 3.5f,
			GlowWidth = 12f,
			GlowColor = TrailGlowColor,
			CoreColor = TrailCoreColor,
			MinPointSpacing = 1.5f,
			SparkAmount = 16,
		};
		Visual.AddChild(_trail);
		Stats.HitTaken += _ => _whiff.NotifyHitTaken();
		Stats.HealthChanged += (current, max) => CheckWrath(current, max);
	}

	private void CheckWrath(int current, int max)
	{
		if (_wrath || current <= 0 || current > max * WrathHealthRatio)
			return;
		_wrath = true;
		MoveSpeed = _baseMoveSpeed * WrathSpeedMultiplier;
		Shake(3f, 0.25f);
		FlashBody(0.5f);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;
		float dt = (float)delta;
		_decisionTimer -= dt;
		_attackCooldownTimer -= dt;
		_spikesCooldownTimer -= dt;

		var player = PlayerReads.Find(this);
		_dash.Update(player, dt);
		_whiff.Update(player, dt);

		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval)
			return;
		// Wrath (or rushing): the cape leaves red afterimages.
		if (_wrath || Mathf.Abs(Velocity.X) > _baseMoveSpeed * 1.5f)
			Afterimage(dt);
		if (player is null || !PlayerDetected || _busy || !IsOnFloor())
			return;
		Think(player);
	}

	// ── Decisions ───────────────────────────────────────────────────────────────────────────

	private void Think(Metroidvania.Player.Player player)
	{
		float distanceX = player.GlobalPosition.X - GlobalPosition.X;
		float absDistance = Mathf.Abs(distanceX);

		if ((_whiff.JustWhiffed || _dash.SinceDashEnded < 0.1f) && absDistance <= CleaveRange + 12f
			&& _attackCooldownTimer <= AttackCooldown * 0.5f)
		{
			_whiff.Consume();
			_ = Cleave(0.18f, chain: false);
			return;
		}

		if (absDistance <= CleaveRange && _attackCooldownTimer <= 0f)
		{
			// Up close: mostly the cleave (maybe chained), sometimes the rising slash alone.
			if (Rng.Randf() < 0.25f)
				_ = RisingSlash();
			else
				_ = Cleave(CleaveTelegraph * TelegraphScale(), chain: Rng.Randf() < ChainChance);
			return;
		}

		// The spikes have their own cooldown: they only wait for the melee recovery to be half over.
		if (_decisionTimer > 0f || _attackCooldownTimer > AttackCooldown * 0.5f)
			return;
		_decisionTimer = DecisionInterval * Rng.RandfRange(0.8f, 1.3f);
		if (absDistance >= SpikesMinDistance && absDistance <= SpikesMaxDistance && _spikesCooldownTimer <= 0f
			&& player.IsOnFloor() && Rng.Randf() < SpikesChance)
			_ = SoulSpikes(Mathf.Sign(distanceX));
	}

	// ── Movement ────────────────────────────────────────────────────────────────────────────

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_busy)
			return Mathf.Abs(distanceX) < 34f ? 0f : _moveOverride;
		float toward = Mathf.Sign(distanceX);
		float absDistance = Mathf.Abs(distanceX);
		if (absDistance > CleaveRange * 0.8f && CanStepTo(toward))
			return toward * MoveSpeed * (absDistance > RushDistance ? RushSpeedMultiplier : 1f);
		return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
	}

	protected override bool CanTurnToFacePlayer => !_busy;

	// ── Cleave (+ rising slash chain) ───────────────────────────────────────────────────────

	private async Task Cleave(float telegraph, bool chain)
	{
		if (!Begin())
			return;
		try
		{
			Sprite.Play("attack");
			Sprite.Pause();
			Sprite.Frame = 3;
			FlashBody(telegraph);
			if (!await Wait(telegraph))
				return;

			Sprite.Frame = 4;
			_trail.Play(new Vector2(4f, -14f), 58f, -125f, 78f, 0.1f);
			bool landed = Strike(CleaveHitboxCenter, CleaveHitboxSize, CleaveDamageMultiplier, CleaveKnockback);
			Shake(1.5f, 0.1f);
			for (int f = 5; f <= 6; f++)
			{
				if (!await Wait(1f / AttackFps))
					return;
				Sprite.Frame = f;
			}

			// The upswing only follows a cleave that missed (the player's i-frames would eat it anyway):
			// it catches whoever dodged or rolled past the first cut.
			if (chain && !landed)
			{
				// Straight into the upswing from the low follow-through, stepping in after them.
				FacePlayer();
				_moveOverride = (FacingRight ? 1f : -1f) * 150f;
				bool ok = await PlayRisingSlash(0.12f);
				_moveOverride = 0f;
				if (!ok)
					return;
			}
			else
			{
				if (!await Wait(1f / AttackFps))
					return;
				Sprite.Frame = 7;
			}
			if (!await Wait(0.25f))
				return;
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * CooldownScale() * Rng.RandfRange(0.85f, 1.2f);
	}

	private async Task RisingSlash()
	{
		if (!Begin())
			return;
		try
		{
			Sprite.Play("spell");
			Sprite.Pause();
			Sprite.Frame = 6;
			FlashBody(0.35f * TelegraphScale());
			if (!await Wait(0.35f * TelegraphScale()))
				return;
			if (!await PlayRisingSlash(0f))
				return;
			if (!await Wait(0.25f))
				return;
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * CooldownScale();
	}

	// spell frames 7-10: the upswing, hitting on frame 7.
	private async Task<bool> PlayRisingSlash(float windup)
	{
		Sprite.Play("spell");
		Sprite.Pause();
		Sprite.Frame = 6;
		if (windup > 0f && !await Wait(windup))
			return false;
		Sprite.Frame = 7;
		_trail.Play(new Vector2(2f, -18f), 46f, 60f, -95f, 0.1f);
		Strike(RisingHitboxCenter, RisingHitboxSize, RisingDamageMultiplier, RisingKnockback, launch: true);
		for (int f = 8; f <= 10; f++)
		{
			if (!await Wait(1f / SpellFps))
				return false;
			_moveOverride = 0f;
			Sprite.Frame = f;
		}
		return true;
	}

	// ── Soul spikes ─────────────────────────────────────────────────────────────────────────

	private async Task SoulSpikes(float direction)
	{
		if (!Begin())
			return;
		_spikesCooldownTimer = SpikesCooldown;
		try
		{
			// The flame gathers in the hand (frames 2-5 cycling) — the telegraph.
			Sprite.Play("spell");
			Sprite.Pause();
			float charge = SpikesCharge * TelegraphScale();
			var glow = HandGlow(charge);
			float t = 0f;
			int frame = 2;
			while (t < charge)
			{
				Sprite.Frame = frame;
				frame = frame >= 5 ? 2 : frame + 1;
				if (!await Wait(1f / SpellFps))
					return;
				t += 1f / SpellFps;
			}
			glow?.QueueFree();
			FacePlayer();
			Sprite.Frame = 6;
			if (!await Wait(0.08f))
				return;
			Sprite.Frame = 7;
			_trail.Play(new Vector2(2f, -18f), 46f, 60f, -95f, 0.1f);
			Strike(RisingHitboxCenter, RisingHitboxSize, RisingDamageMultiplier, RisingKnockback, launch: true);
			SpawnSpikes(FacingRight ? 1f : -1f);
			for (int f = 8; f <= 10; f++)
			{
				if (!await Wait(1f / SpellFps))
					return;
				Sprite.Frame = f;
			}
			if (!await Wait(0.3f))
				return;
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * 0.8f * CooldownScale();
	}

	private void SpawnSpikes(float forward)
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		var wave = new SoulSpikeWave
		{
			Direction = forward,
			ThrowerStats = Stats,
			DamageMultiplier = SpikesDamageMultiplier,
			GlowColor = TrailGlowColor,
			Count = SpikesCount + (_wrath ? 3 : 0),
		};
		scene.AddChild(wave);
		// Starts at its feet, a step in front.
		wave.GlobalPosition = GlobalPosition + new Vector2(forward * 10f, BodyBottom);
	}

	// A teal flame growing in the off hand (spell frames 2-5 hold it out in front, ~x+28,-12).
	private Node2D HandGlow(float duration)
	{
		var glow = new Sprite2D
		{
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			Modulate = TrailGlowColor,
			Position = new Vector2(28f, -14f),
			Scale = Vector2.One * 0.1f,
		};
		Visual.AddChild(glow);
		Tween tween = glow.CreateTween();
		tween.TweenProperty(glow, "scale", Vector2.One * 0.5f, duration);
		return glow;
	}

	// ── Shared ──────────────────────────────────────────────────────────────────────────────

	private float TelegraphScale() => _wrath ? WrathTelegraphMultiplier : 1f;
	private float CooldownScale() => _wrath ? WrathCooldownMultiplier : 1f;

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

	// One damage check. Landed cuts drain life back into the knight. launch: knocks the player
	// up and away (the rising slash).
	private bool Strike(Vector2 center, Vector2 size, float damageMultiplier, float knockback, bool launch = false)
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
			int before = stats.CurrentHealth;
			stats.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(Stats.AttackPower * damageMultiplier)));
			int dealt = before - stats.CurrentHealth;
			if (dealt > 0)
			{
				float side = Mathf.Sign(body.GlobalPosition.X - GlobalPosition.X);
				if (body.HasMethod("ApplyKnockback"))
					body.Call("ApplyKnockback", (launch ? new Vector2(side * 0.5f, -1f) : new Vector2(side, -0.25f)).Normalized(), knockback);
				BloodEffect.SpawnAt(this, body.GlobalPosition);
				Drain(dealt, body.GlobalPosition);
			}
			return true;
		}
		return false;
	}

	// Heals for part of the damage dealt, with a teal wisp flying from the victim back to the knight.
	private void Drain(int dealt, Vector2 from)
	{
		int heal = Mathf.Max(1, Mathf.RoundToInt(dealt * DrainRatio));
		Stats.Heal(heal);
		if (GetTree().CurrentScene is not Node scene)
			return;
		var wisp = new Sprite2D
		{
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			Modulate = TrailGlowColor,
			Scale = Vector2.One * 0.25f,
		};
		scene.AddChild(wisp);
		wisp.GlobalPosition = from;
		Tween tween = wisp.CreateTween();
		tween.TweenProperty(wisp, "global_position", GlobalPosition + new Vector2(0f, -20f), 0.35f)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
		tween.Parallel().TweenProperty(wisp, "scale", Vector2.One * 0.08f, 0.35f);
		tween.TweenCallback(Callable.From(wisp.QueueFree));
	}

	private void Afterimage(float dt)
	{
		_afterimageTimer -= dt;
		if (_afterimageTimer > 0f)
			return;
		_afterimageTimer = _wrath ? 0.08f : 0.05f;
		Texture2D frame = Sprite?.SpriteFrames?.GetFrameTexture(Sprite.Animation, Sprite.Frame);
		if (frame is null || GetTree().CurrentScene is not Node scene)
			return;
		var ghost = new Sprite2D
		{
			Texture = frame,
			Centered = Sprite.Centered,
			Offset = Sprite.Offset,
			FlipH = Sprite.FlipH,
			Material = Metroidvania.Player.NeonSilhouette.CreateMaterial(AfterimageColor, 1.4f),
			Modulate = new Color(1f, 1f, 1f, 0.45f),
			ZIndex = ZIndex - 1,
		};
		scene.AddChild(ghost);
		ghost.GlobalTransform = Sprite.GlobalTransform;
		Tween fade = ghost.CreateTween();
		fade.TweenProperty(ghost, "modulate:a", 0f, 0.25f);
		fade.TweenCallback(Callable.From(ghost.QueueFree));
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
		base.OnDefeated();
	}

	// ── Animation ───────────────────────────────────────────────────────────────────────────

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null || _busy)
			return;
		float speed = Mathf.Abs(velocity.X);
		string anim = !IsOnFloor() ? "jump"
			: speed > _baseMoveSpeed * 1.3f ? "run"
			: speed > 5f ? "walk"
			: "idle";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	protected override bool ContactDamageEnabled => false;
}
