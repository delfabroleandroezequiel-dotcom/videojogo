using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Heraldo de la muerte / Bringer of Death (single-file sprites, 140x93 cells, faces LEFT — mirrored
// on the scene; its body sits right of centre in the cell, so the sprite is offset to put the body on
// the origin). A reaper-like elite: a scythe with frightening reach, and a hand of death it calls
// down from above onto wherever you stand.
// Sheet beats used:
//   attack (10): 1-3 the scythe drawn back (the wind-up), 4-8 a huge sweep out in front.
//   cast (9): 0-4 the arm raising, 5-8 held up — calling the hand.
//   spell (16): the separate effect of the hand of death coming down out of the dark (reaches the
//     floor around 6-8), then dissolving.
// Moves:
//   Scythe sweep — long wind-up, then a sweep that reaches ~130px in front and well over your head.
//   Hand of death — raises its arm; a dark mark spreads on the floor under you, then the hand
//     slams down there. Keep moving.
//   Shadow step — fades into the dark and reappears behind you (then usually sweeps).
//   Punish — a dash ending close to it gets a quick sweep.
// Telegraphs: body flashes white before the sweep; the floor mark before the hand.
public partial class BringerOfDeath : Enemy
{
	[ExportGroup("Scythe")]
	[Export] public float SweepRange = 120f;
	[Export] public float AttackCooldown = 1.8f;
	[Export] public float SweepTelegraph = 0.6f;
	[Export] public float SweepDamageMultiplier = 1.3f;
	[Export] public float SweepKnockback = 300f;
	// Measured off attack frames 4-8 (scale 1.5 mirrored, sprite at 53,-30): the scythe sweeps out to
	// ~130px in front, from high above the head down to the floor.
	[Export] public Vector2 SweepHitboxCenter = new(70f, -20f);
	[Export] public Vector2 SweepHitboxSize = new(130f, 110f);

	[ExportGroup("Hand of death")]
	[Export] public float HandMinDistance = 110f;
	[Export] public float HandMaxDistance = 360f;
	[Export] public float HandChance = 0.5f;
	[Export] public float HandCooldown = 4.5f;
	[Export] public float HandMarkTime = 0.75f;
	[Export] public float HandDamageMultiplier = 1.2f;
	[Export] public float HandRadius = 30f;

	[ExportGroup("Shadow step")]
	[Export] public float StepCooldown = 6f;
	[Export] public float StepChance = 0.3f;
	[Export] public float StepFadeTime = 0.35f;
	[Export] public float StepBehind = 60f;

	[ExportGroup("Look")]
	[Export] public Color MarkColor = new(0.55f, 0.15f, 0.75f, 1f);
	[Export] public Color FlashTint = new(2.2f, 2.2f, 2.2f, 1f);
	[Export] public float DecisionInterval = 0.4f;

	private const float Fps = 10f;

	private static readonly RandomNumberGenerator Rng = new();
	private readonly DashWatcher _dash = new();

	private EnemySlashTrail _trail;
	private Tween _flashTween;
	private bool _busy;
	private float _decisionTimer;
	private float _attackCooldownTimer;
	private float _handCooldownTimer;
	private float _stepCooldownTimer;

	static BringerOfDeath()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_attackCooldownTimer = Rng.RandfRange(0.5f, 1f);
		_handCooldownTimer = Rng.RandfRange(1f, 2f);
		_stepCooldownTimer = Rng.RandfRange(3f, 5f);
		_trail = new EnemySlashTrail
		{
			CoreWidth = 3.5f,
			GlowWidth = 12f,
			GlowColor = MarkColor,
			CoreColor = new Color(0.9f, 0.8f, 1f, 1f),
			MinPointSpacing = 1.5f,
			SparkAmount = 12,
		};
		Visual.AddChild(_trail);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;
		float dt = (float)delta;
		_decisionTimer -= dt;
		_attackCooldownTimer -= dt;
		_handCooldownTimer -= dt;
		_stepCooldownTimer -= dt;
		var player = PlayerReads.Find(this);
		_dash.Update(player, dt);
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval || player is null || !PlayerDetected || _busy || !IsOnFloor())
			return;

		float distanceX = player.GlobalPosition.X - GlobalPosition.X;
		float absDistance = Mathf.Abs(distanceX);
		if (_dash.SinceDashEnded < 0.1f && absDistance <= SweepRange && _attackCooldownTimer <= AttackCooldown * 0.5f)
		{
			_ = Sweep(0.3f);
			return;
		}
		if (absDistance <= SweepRange && _attackCooldownTimer <= 0f)
		{
			_ = Sweep(SweepTelegraph);
			return;
		}
		if (_decisionTimer > 0f || _attackCooldownTimer > AttackCooldown * 0.5f)
			return;
		_decisionTimer = DecisionInterval * Rng.RandfRange(0.8f, 1.3f);
		if (_stepCooldownTimer <= 0f && player.IsOnFloor() && Rng.Randf() < StepChance)
			_ = ShadowStep(player);
		else if (absDistance >= HandMinDistance && absDistance <= HandMaxDistance && _handCooldownTimer <= 0f
			&& Rng.Randf() < HandChance)
			_ = HandOfDeath(player);
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_busy)
			return 0f;
		float toward = Mathf.Sign(distanceX);
		if (Mathf.Abs(distanceX) > SweepRange * 0.75f && CanStepTo(toward))
			return toward * MoveSpeed;
		return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
	}

	protected override bool CanTurnToFacePlayer => !_busy;

	// ── Scythe sweep ────────────────────────────────────────────────────────────────────────

	private async Task Sweep(float telegraph)
	{
		if (!Begin())
			return;
		try
		{
			Sprite.Play("attack");
			Sprite.Pause();
			Sprite.Frame = 1;
			if (!await Wait(1f / Fps))
				return;
			Sprite.Frame = 2;
			FlashBody(telegraph);
			if (!await Wait(telegraph))
				return;
			Sprite.Frame = 3;
			if (!await Wait(1f / Fps))
				return;
			Sprite.Frame = 4;
			_trail.Play(new Vector2(10f, -30f), 100f, -120f, 30f, 0.12f, 70f);
			bool hit = Strike();
			for (int f = 5; f <= 9; f++)
			{
				if (!await Wait(1f / Fps))
					return;
				Sprite.Frame = f;
				if (!hit && f <= 6)
					hit = Strike();
			}
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * Rng.RandfRange(0.85f, 1.2f);
	}

	private bool Strike()
	{
		Vector2 at = GlobalPosition + new Vector2(FacingRight ? SweepHitboxCenter.X : -SweepHitboxCenter.X, SweepHitboxCenter.Y);
		var query = new PhysicsShapeQueryParameters2D
		{
			Shape = new RectangleShape2D { Size = SweepHitboxSize },
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
			stats.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(Stats.AttackPower * SweepDamageMultiplier)));
			if (stats.CurrentHealth < before)
			{
				player.ApplyKnockback(new Vector2(Mathf.Sign(player.GlobalPosition.X - GlobalPosition.X), -0.35f).Normalized(), SweepKnockback);
				BloodEffect.SpawnAt(this, player.GlobalPosition);
			}
			return true;
		}
		return false;
	}

	// ── Hand of death ───────────────────────────────────────────────────────────────────────

	private async Task HandOfDeath(Metroidvania.Player.Player player)
	{
		if (!Begin())
			return;
		_handCooldownTimer = HandCooldown;
		try
		{
			Sprite.Play("cast");
			if (!await Wait(5f / Fps))
				return;
			// The mark lands under the player as the arm goes up.
			if (!TryFloorUnder(player.GlobalPosition, out Vector2 ground))
				return;
			Node2D mark = SpawnMark(ground);
			if (!await Wait(HandMarkTime))
				return;
			mark?.QueueFree();
			_ = DropHand(ground);
			if (!await Wait(0.5f))
				return;
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * 0.6f;
	}

	private bool TryFloorUnder(Vector2 at, out Vector2 ground)
	{
		var query = PhysicsRayQueryParameters2D.Create(at + new Vector2(0f, -10f), at + new Vector2(0f, 200f), 1u);
		var hit = GetWorld2D().DirectSpaceState.IntersectRay(query);
		ground = hit.Count > 0 ? (Vector2)hit["position"] : at;
		return hit.Count > 0;
	}

	private Node2D SpawnMark(Vector2 ground)
	{
		if (GetTree().CurrentScene is not Node scene)
			return null;
		var mark = new Sprite2D
		{
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			Modulate = MarkColor,
			Scale = new Vector2(0.2f, 0.06f),
		};
		scene.AddChild(mark);
		mark.GlobalPosition = ground + new Vector2(0f, -2f);
		Tween tween = mark.CreateTween();
		tween.TweenProperty(mark, "scale", new Vector2(HandRadius / 40f, HandRadius / 140f), HandMarkTime);
		return mark;
	}

	// The hand effect (the sheet's "spell" clip) slamming down onto `ground`.
	private async Task DropHand(Vector2 ground)
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		var hand = new AnimatedSprite2D
		{
			SpriteFrames = Sprite.SpriteFrames,
			Scale = new Vector2(1.5f, 1.5f),
			Animation = "spell",
		};
		scene.AddChild(hand);
		// The hand reaches the floor at cell y≈91 (cell centre 46.5), x centred ≈67.
		hand.GlobalPosition = ground + new Vector2(-4f, -67f);
		hand.Play("spell");
		await ToSignal(GetTree().CreateTimer(6f / 10f), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this) && !IsQueuedForRemoval)
		{
			HandHit(ground);
			if (PlayerReads.Find(this) is { } p && p.GlobalPosition.DistanceTo(ground) < 400f)
				p.ShakeCamera(2f, 0.12f);
		}
		await ToSignal(GetTree().CreateTimer(1f), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(hand))
			hand.QueueFree();
	}

	private void HandHit(Vector2 ground)
	{
		if (PlayerReads.Find(this) is not { } player)
			return;
		Vector2 feet = player.GlobalPosition + new Vector2(0f, 32f);
		if (Mathf.Abs(feet.X - ground.X) > HandRadius + 12f || Mathf.Abs(feet.Y - ground.Y) > 70f)
			return;
		var stats = player.GetNodeOrNull<Stats>("Stats");
		if (stats is null || stats.IsInvulnerable)
			return;
		int before = stats.CurrentHealth;
		stats.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(Stats.AttackPower * HandDamageMultiplier)), isProjectile: true);
		if (stats.CurrentHealth < before)
			player.ApplyKnockback(new Vector2(0f, 1f), 100f);
	}

	// ── Shadow step ─────────────────────────────────────────────────────────────────────────

	private async Task ShadowStep(Metroidvania.Player.Player player)
	{
		float behind = player.IsFacingRight ? -1f : 1f;
		Vector2 target = player.GlobalPosition + new Vector2(behind * StepBehind, 0f);
		// Only if there's floor there and no wall in the way of its body.
		if (!TryFloorUnder(target, out Vector2 ground))
			return;
		Vector2 destination = new(target.X, ground.Y - BodyBottom - 1f);
		var probe = GlobalTransform;
		probe.Origin = destination;
		if (TestMove(probe, Vector2.Zero))
			return;
		if (!Begin())
			return;
		_stepCooldownTimer = StepCooldown;
		try
		{
			Tween fadeOut = CreateTween();
			fadeOut.TweenProperty(Sprite, "modulate:a", 0f, StepFadeTime);
			if (!await Wait(StepFadeTime + 0.15f))
				return;
			GlobalPosition = destination;
			FacePlayer();
			Tween fadeIn = CreateTween();
			fadeIn.TweenProperty(Sprite, "modulate:a", 1f, StepFadeTime * 0.6f);
			if (!await Wait(StepFadeTime * 0.6f))
				return;
		}
		finally
		{
			if (IsInstanceValid(this) && Sprite is not null)
				Sprite.Modulate = Colors.White;
			Finish();
		}
		// Appears right behind them: a quicker sweep.
		_attackCooldownTimer = 0f;
		_ = Sweep(SweepTelegraph * 0.7f);
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

	private async Task<bool> Wait(float seconds)
	{
		await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
		return IsInstanceValid(this) && !IsQueuedForRemoval;
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
			Sprite.Modulate = Colors.White;
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
