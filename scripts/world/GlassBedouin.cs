using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Beduino del vidrio / Glass Bedouin (Desert enemy pack, 160x64 cells, faces right). A short desert
// dweller carrying a great disc of polished glass: a club up close, a sun mirror at range.
// Sheet beats used:
//   attack_melle (12fps): 2-4 the glass hoisted over the head (wind-up), 5 slammed down in a huge
//     arc in front, 6-12 crouched over it (the long recovery), 13-15 standing back up.
//   attack_ranged (12fps): 3-6 the mirror raised overhead, 7 the sun glint (bright ring), 8-11 the
//     reflected beam striking the ground in front, 12-17 sweeping up and out to the horizontal,
//     18-21 lowering it.
// Moves:
//   Glass slam — a big overhead slam that bursts glass shards; afterwards it stays crouched for a
//     long moment — the window to punish it.
//   Sun beam — from mid range it raises the mirror (glint telegraph), then a burning beam strikes
//     the ground ahead and sweeps up to head height and beyond (Fire damage). Its foot starts close
//     and climbs: get in tight under the mirror, or roll through it.
// Telegraphs: body flashes white before the slam; the glint flares before the beam.
public partial class GlassBedouin : Enemy
{
	[ExportGroup("Spacing")]
	[Export] public float BeamMinDistance = 80f;
	[Export] public float BeamMaxDistance = 280f;

	[ExportGroup("Glass slam")]
	[Export] public float SlamRange = 46f;
	[Export] public float AttackCooldown = 1.6f;
	[Export] public float SlamTelegraph = 0.45f;
	[Export] public float SlamDamageMultiplier = 1.3f;
	[Export] public float SlamKnockback = 280f;
	// Measured off attack_melle frame 5 (scale 1.5, sprite at 0.75,-19): the glass arc reaches ~45px
	// in front, from above the head down to the floor.
	[Export] public Vector2 SlamHitboxCenter = new(24f, -6f);
	[Export] public Vector2 SlamHitboxSize = new(44f, 70f);
	[Export] public float CrouchTime = 0.55f;

	[ExportGroup("Sun beam")]
	[Export] public float BeamCooldown = 4.5f;
	[Export] public float BeamChance = 0.6f;
	[Export] public float GlintTime = 0.45f;
	[Export] public float BeamLength = 260f;
	[Export] public float BeamWidth = 7f;
	[Export] public float BeamDamageMultiplier = 1.1f;
	// The mirror held overhead (attack_ranged frames 7-17).
	[Export] public Vector2 MirrorOffset = new(2f, -37f);
	// Beam angle (degrees below the horizontal, facing right) for frames 8-17, read off the sheet.
	[Export] public float[] BeamAngles = { 70f, 68f, 66f, 48f, 33f, 20f, 2f, -2f, -3f, -3f };
	[Export] public Color BeamColor = new(1f, 0.85f, 0.35f, 1f);
	[Export] public Color FlashTint = new(2.2f, 2.2f, 2.2f, 1f);

	[Export] public float DecisionInterval = 0.35f;

	private const float AttackFps = 12f;

	private static readonly RandomNumberGenerator Rng = new();
	private readonly WhiffWatcher _whiff = new();

	private Tween _flashTween;
	private bool _busy;
	private float _decisionTimer;
	private float _attackCooldownTimer;
	private float _beamCooldownTimer;

	static GlassBedouin()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_attackCooldownTimer = Rng.RandfRange(0.4f, 0.9f);
		_beamCooldownTimer = Rng.RandfRange(0.6f, 1.6f);
		Stats.HitTaken += _ => _whiff.NotifyHitTaken();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;
		float dt = (float)delta;
		_decisionTimer -= dt;
		_attackCooldownTimer -= dt;
		_beamCooldownTimer -= dt;
		var player = PlayerReads.Find(this);
		_whiff.Update(player, dt);
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval || player is null || !PlayerDetected || _busy || !IsOnFloor())
			return;

		float absDistance = Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X);
		if (absDistance <= SlamRange && (_attackCooldownTimer <= 0f || (_whiff.JustWhiffed && _attackCooldownTimer < AttackCooldown * 0.5f)))
		{
			_whiff.Consume();
			_ = Slam();
			return;
		}
		if (_decisionTimer > 0f || _attackCooldownTimer > AttackCooldown * 0.5f)
			return;
		_decisionTimer = DecisionInterval * Rng.RandfRange(0.8f, 1.3f);
		if (absDistance >= BeamMinDistance && absDistance <= BeamMaxDistance && _beamCooldownTimer <= 0f && Rng.Randf() < BeamChance)
			_ = SunBeam();
	}

	// Walks in for the slam unless the beam is ready and it's already at beam range.
	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_busy)
			return 0f;
		float toward = Mathf.Sign(distanceX);
		float absDistance = Mathf.Abs(distanceX);
		bool beamReady = _beamCooldownTimer <= 0.4f;
		if (beamReady && absDistance >= BeamMinDistance && absDistance <= BeamMaxDistance)
			return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
		if (absDistance > SlamRange * 0.8f && CanStepTo(toward))
			return toward * MoveSpeed;
		return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
	}

	protected override bool CanTurnToFacePlayer => !_busy;

	// ── Glass slam ──────────────────────────────────────────────────────────────────────────

	private async Task Slam()
	{
		if (!Begin())
			return;
		try
		{
			Sprite.Play("attack_melle");
			Sprite.Pause();
			for (int f = 1; f <= 3; f++)
			{
				Sprite.Frame = f;
				if (!await Wait(1f / AttackFps))
					return;
			}
			Sprite.Frame = 4;
			FlashBody(SlamTelegraph);
			if (!await Wait(SlamTelegraph))
				return;
			Sprite.Frame = 5;
			StrikeBox(SlamHitboxCenter, SlamHitboxSize, SlamDamageMultiplier, SlamKnockback, DamageElement.Normal);
			Shards();
			Shake(2f, 0.12f);
			// Crouched over the glass: the punish window.
			for (int f = 6; f <= 12; f++)
			{
				if (!await Wait(CrouchTime / 7f))
					return;
				Sprite.Frame = f;
			}
			for (int f = 13; f <= 15; f++)
			{
				if (!await Wait(1f / AttackFps))
					return;
				Sprite.Frame = f;
			}
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * Rng.RandfRange(0.85f, 1.2f);
	}

	// A burst of glittering glass splinters off the slam (cosmetic).
	private void Shards()
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		Vector2 at = GlobalPosition + new Vector2(FacingRight ? 34f : -34f, 24f);
		for (int i = 0; i < 6; i++)
		{
			var shard = new Polygon2D
			{
				Polygon = new[] { new Vector2(-2f, 0f), new Vector2(0f, -4f), new Vector2(2f, 0f), new Vector2(0f, 2f) },
				Color = new Color(0.85f, 0.95f, 1f, 1f),
			};
			scene.AddChild(shard);
			shard.GlobalPosition = at;
			Vector2 velocity = Vector2.FromAngle(Mathf.DegToRad(Rng.RandfRange(-160f, -20f))) * Rng.RandfRange(80f, 180f);
			Tween tween = shard.CreateTween();
			tween.TweenProperty(shard, "global_position", at + velocity * 0.35f + new Vector2(0f, 30f), 0.45f).SetEase(Tween.EaseType.Out);
			tween.Parallel().TweenProperty(shard, "rotation", Rng.RandfRange(-6f, 6f), 0.45f);
			tween.Parallel().TweenProperty(shard, "modulate:a", 0f, 0.45f);
			tween.TweenCallback(Callable.From(shard.QueueFree));
		}
	}

	// ── Sun beam ────────────────────────────────────────────────────────────────────────────

	private async Task SunBeam()
	{
		if (!Begin())
			return;
		_beamCooldownTimer = BeamCooldown;
		Line2D beam = null;
		Line2D core = null;
		try
		{
			Sprite.Play("attack_ranged");
			Sprite.Pause();
			for (int f = 2; f <= 6; f++)
			{
				Sprite.Frame = f;
				if (!await Wait(1f / AttackFps))
					return;
			}
			// The glint: a flare on the mirror — the warning.
			Sprite.Frame = 7;
			Glint();
			if (!await Wait(GlintTime))
				return;

			var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
			beam = new Line2D { Width = BeamWidth * 2.2f, DefaultColor = BeamColor * new Color(1f, 1f, 1f, 0.55f), Material = additive };
			core = new Line2D { Width = BeamWidth * 0.6f, DefaultColor = new Color(1f, 1f, 0.9f, 1f), Material = additive };
			Visual.AddChild(beam);
			Visual.AddChild(core);
			bool hit = false;
			for (int i = 0; i < BeamAngles.Length; i++)
			{
				Sprite.Frame = 8 + i;
				// Sub-steps between frames so the sweep is smooth and can't skip over the player.
				float from = BeamAngles[i];
				float to = BeamAngles[Mathf.Min(i + 1, BeamAngles.Length - 1)];
				const int steps = 5;
				for (int s = 0; s < steps; s++)
				{
					float angle = Mathf.Lerp(from, to, (float)s / steps);
					Vector2 end = BeamEnd(angle, out Vector2 local);
					beam.Points = new[] { MirrorOffset, local };
					core.Points = new[] { MirrorOffset, local };
					if (!hit)
						hit = BeamHits(end);
					if (!await Wait(1f / AttackFps / steps))
						return;
				}
			}
			beam.QueueFree();
			core.QueueFree();
			beam = core = null;
			for (int f = 18; f <= 21; f++)
			{
				Sprite.Frame = f;
				if (!await Wait(1f / AttackFps))
					return;
			}
		}
		finally
		{
			if (IsInstanceValid(beam))
				beam.QueueFree();
			if (IsInstanceValid(core))
				core.QueueFree();
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * 0.8f;
	}

	// The beam's far end for an angle (degrees below the horizontal): cut short by terrain. Returns
	// the global end, and the same point in Visual's local space (Visual is mirrored when facing left).
	private Vector2 BeamEnd(float angleDegrees, out Vector2 local)
	{
		Vector2 dir = Vector2.FromAngle(Mathf.DegToRad(angleDegrees));
		Vector2 start = GlobalPosition + new Vector2(FacingRight ? MirrorOffset.X : -MirrorOffset.X, MirrorOffset.Y);
		Vector2 worldDir = new(FacingRight ? dir.X : -dir.X, dir.Y);
		Vector2 end = start + worldDir * BeamLength;
		var query = PhysicsRayQueryParameters2D.Create(start, end, 1u);
		var hit = GetWorld2D().DirectSpaceState.IntersectRay(query);
		if (hit.Count > 0)
			end = (Vector2)hit["position"];
		float length = start.DistanceTo(end);
		local = MirrorOffset + dir * length;
		return end;
	}

	private bool BeamHits(Vector2 end)
	{
		if (PlayerReads.Find(this) is not { } player)
			return false;
		Vector2 start = GlobalPosition + new Vector2(FacingRight ? MirrorOffset.X : -MirrorOffset.X, MirrorOffset.Y);
		// Player body as a vertical segment: close enough to the beam anywhere along it?
		Vector2 top = player.GlobalPosition + new Vector2(0f, -28f);
		Vector2 bottom = player.GlobalPosition + new Vector2(0f, 28f);
		float best = float.MaxValue;
		for (int i = 0; i <= 4; i++)
		{
			Vector2 p = top.Lerp(bottom, i / 4f);
			Vector2 closest = Geometry2D.GetClosestPointToSegment(p, start, end);
			best = Mathf.Min(best, closest.DistanceTo(p));
		}
		if (best > 14f + BeamWidth * 0.5f)
			return false;
		var stats = player.GetNodeOrNull<Stats>("Stats");
		if (stats is null || stats.IsInvulnerable)
			return false;
		int before = stats.CurrentHealth;
		stats.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(Stats.AttackPower * BeamDamageMultiplier)), isProjectile: true, element: DamageElement.Fire);
		if (stats.CurrentHealth < before)
			player.ApplyKnockback(new Vector2(Mathf.Sign(player.GlobalPosition.X - GlobalPosition.X), -0.4f).Normalized(), 220f);
		return true;
	}

	private void Glint()
	{
		var glow = new Sprite2D
		{
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			Modulate = BeamColor,
			Position = MirrorOffset,
			Scale = Vector2.One * 0.1f,
		};
		Visual.AddChild(glow);
		Tween tween = glow.CreateTween();
		tween.TweenProperty(glow, "scale", Vector2.One * 0.8f, GlintTime * 0.6f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(glow, "modulate:a", 0f, GlintTime * 0.6f);
		tween.TweenCallback(Callable.From(glow.QueueFree));
		Sprite.SelfModulate = FlashTint;
		CreateTween().TweenProperty(Sprite, "self_modulate", Colors.White, GlintTime);
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

	private bool StrikeBox(Vector2 center, Vector2 size, float damageMultiplier, float knockback, DamageElement element)
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
			stats.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(Stats.AttackPower * damageMultiplier)), element: element);
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
		if (Sprite is null || _busy)
			return;
		string anim = Mathf.Abs(velocity.X) > 5f ? "walk" : "idle";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	protected override bool ContactDamageEnabled => false;
}
