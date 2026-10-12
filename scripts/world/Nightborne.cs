using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Nacido de la noche / Nightborne (80x80 cells, faces right). A horned shadow swordsman crackling
// with violet lightning: one devastating charged crescent, a blinding dash, and it goes out with a
// bang.
// Sheet beats used:
//   attack (12): 2-8 the sword raised high, the body outlined in crackling lightning (the charge),
//     9 a huge crescent sweeping over the top and down the front, 10 a low follow-up sweep, 11 reset.
//   run: the dash. death (23): collapsing (0-8), imploding (9-11), then bursting in a violet blast
//     (12-15) and fading (16-22).
// Moves:
//   Storm crescent — the long, crackling charge, then the crescent: enormous reach and damage.
//     Read the lightning and get out (or get in behind it).
//   Blitz — from mid range it dashes in (afterimages) and cuts with the crescent at once, no charge.
//   Last burst — when it dies it implodes and blasts the area around it: back off when it falls.
// Telegraphs: lightning flicker during the charge; violet afterimages on the dash.
public partial class Nightborne : Enemy
{
	[ExportGroup("Storm crescent")]
	[Export] public float CrescentRange = 85f;
	[Export] public float AttackCooldown = 1.6f;
	[Export] public float ChargeTime = 0.8f;
	[Export] public float CrescentDamageMultiplier = 1.5f;
	[Export] public float CrescentKnockback = 320f;
	// Measured off attack frames 9-10 (scale 2.2, sprite at 8.8,-21.8): ~92px in front, from high
	// over the head down to the floor.
	[Export] public Vector2 CrescentCenter = new(40f, -22f);
	[Export] public Vector2 CrescentSize = new(100f, 106f);

	[ExportGroup("Blitz")]
	[Export] public float BlitzMinDistance = 120f;
	[Export] public float BlitzMaxDistance = 260f;
	[Export] public float BlitzChance = 0.45f;
	[Export] public float BlitzCooldown = 3.5f;
	[Export] public float BlitzSpeed = 480f;
	[Export] public float BlitzDamageMultiplier = 1f;

	[ExportGroup("Last burst")]
	[Export] public float BurstDelay = 1f;
	[Export] public float BurstRadius = 80f;
	[Export] public float BurstDamageMultiplier = 1.4f;

	[ExportGroup("Look")]
	[Export] public Color GlowColor = new(0.7f, 0.4f, 1f, 1f);
	[Export] public Color FlashTint = new(2.2f, 2.2f, 2.2f, 1f);
	[Export] public float DecisionInterval = 0.35f;

	private static readonly RandomNumberGenerator Rng = new();

	private EnemySlashTrail _trail;
	private bool _busy;
	private float _moveOverride;
	private float _decisionTimer;
	private float _attackCooldownTimer;
	private float _blitzCooldownTimer;
	private float _afterimageTimer;

	static Nightborne()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_attackCooldownTimer = Rng.RandfRange(0.5f, 1f);
		_blitzCooldownTimer = Rng.RandfRange(0.8f, 1.6f);
		_trail = new EnemySlashTrail
		{
			CoreWidth = 3.5f,
			GlowWidth = 12f,
			GlowColor = GlowColor,
			CoreColor = new Color(0.95f, 0.9f, 1f, 1f),
			MinPointSpacing = 1.5f,
			SparkAmount = 14,
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
		_blitzCooldownTimer -= dt;
		var player = PlayerReads.Find(this);
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval || player is null || !PlayerDetected || _busy || !IsOnFloor())
			return;

		float absDistance = Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X);
		if (absDistance <= CrescentRange && _attackCooldownTimer <= 0f)
		{
			_ = StormCrescent();
			return;
		}
		if (_decisionTimer > 0f || _attackCooldownTimer > AttackCooldown * 0.6f)
			return;
		_decisionTimer = DecisionInterval * Rng.RandfRange(0.8f, 1.3f);
		if (absDistance >= BlitzMinDistance && absDistance <= BlitzMaxDistance && _blitzCooldownTimer <= 0f && Rng.Randf() < BlitzChance)
			_ = Blitz(Mathf.Sign(player.GlobalPosition.X - GlobalPosition.X));
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_busy)
			return _moveOverride == 0f || CanStepTo(Mathf.Sign(_moveOverride)) ? _moveOverride : 0f;
		float toward = Mathf.Sign(distanceX);
		if (Mathf.Abs(distanceX) > CrescentRange * 0.75f && CanStepTo(toward))
			return toward * MoveSpeed;
		return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
	}

	protected override bool CanTurnToFacePlayer => !_busy;

	// ── Storm crescent ──────────────────────────────────────────────────────────────────────

	private async Task StormCrescent()
	{
		if (!Begin())
			return;
		try
		{
			Sprite.Play("attack");
			Sprite.Pause();
			Sprite.Frame = 1;
			if (!await Wait(0.06f))
				return;
			// The crackling charge: frames 2-8 cycled, with lightning flicker.
			float t = 0f;
			int frame = 2;
			while (t < ChargeTime)
			{
				Sprite.Frame = frame;
				frame = frame >= 8 ? 2 : frame + 1;
				Sprite.SelfModulate = Rng.Randf() < 0.35f ? FlashTint : Colors.White;
				if (!await Wait(0.06f))
					return;
				t += 0.06f;
			}
			Sprite.SelfModulate = Colors.White;
			await CrescentStrike(CrescentDamageMultiplier);
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * Rng.RandfRange(0.85f, 1.2f);
	}

	private async Task CrescentStrike(float damageMultiplier)
	{
		Sprite.Play("attack");
		Sprite.Pause();
		Sprite.Frame = 9;
		_trail.Play(new Vector2(0f, -10f), 80f, -160f, 60f, 0.09f);
		bool hit = Strike(damageMultiplier);
		if (!await Wait(0.08f))
			return;
		Sprite.Frame = 10;
		if (!hit)
			Strike(damageMultiplier * 0.8f);
		if (!await Wait(0.1f))
			return;
		Sprite.Frame = 11;
		await Wait(0.12f);
	}

	// ── Blitz ───────────────────────────────────────────────────────────────────────────────

	private async Task Blitz(float direction)
	{
		if (!Begin())
			return;
		_blitzCooldownTimer = BlitzCooldown;
		try
		{
			Sprite.Play("attack");
			Sprite.Pause();
			Sprite.Frame = 1;
			Sprite.SelfModulate = FlashTint;
			if (!await Wait(0.18f))
				return;
			Sprite.SelfModulate = Colors.White;
			Sprite.Play("run");
			Sprite.SpeedScale = 2f;
			_moveOverride = direction * BlitzSpeed;
			float t = 0f;
			while (t < 0.45f)
			{
				await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
				if (!IsInstanceValid(this) || IsQueuedForRemoval)
					return;
				float dt = (float)GetPhysicsProcessDeltaTime();
				t += dt;
				Afterimage(dt);
				if (PlayerReads.Find(this) is { } p && Mathf.Abs(p.GlobalPosition.X - GlobalPosition.X) <= CrescentRange * 0.6f)
					break;
				if (TestMove(GlobalTransform, new Vector2(direction * 8f, 0f)) || !HasGroundAhead(direction))
					break;
			}
			_moveOverride = 0f;
			Sprite.SpeedScale = 1f;
			FacePlayer();
			await CrescentStrike(BlitzDamageMultiplier);
		}
		finally
		{
			if (IsInstanceValid(this))
			{
				_moveOverride = 0f;
				Sprite.SpeedScale = 1f;
			}
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * 0.8f;
	}

	private void Afterimage(float dt)
	{
		_afterimageTimer -= dt;
		if (_afterimageTimer > 0f)
			return;
		_afterimageTimer = 0.035f;
		Texture2D frame = Sprite?.SpriteFrames?.GetFrameTexture(Sprite.Animation, Sprite.Frame);
		if (frame is null || GetTree().CurrentScene is not Node scene)
			return;
		var ghost = new Sprite2D
		{
			Texture = frame,
			Centered = Sprite.Centered,
			Offset = Sprite.Offset,
			FlipH = Sprite.FlipH,
			Material = Metroidvania.Player.NeonSilhouette.CreateMaterial(GlowColor, 1.5f),
			Modulate = new Color(1f, 1f, 1f, 0.5f),
			ZIndex = ZIndex - 1,
		};
		scene.AddChild(ghost);
		ghost.GlobalTransform = Sprite.GlobalTransform;
		Tween fade = ghost.CreateTween();
		fade.TweenProperty(ghost, "modulate:a", 0f, 0.22f);
		fade.TweenCallback(Callable.From(ghost.QueueFree));
	}

	// ── Last burst ──────────────────────────────────────────────────────────────────────────

	protected override void OnDefeated()
	{
		if (Sprite is not null)
		{
			Sprite.SelfModulate = Colors.White;
			Sprite.SpeedScale = 1f;
		}
		_moveOverride = 0f;
		Vector2 center = GlobalPosition;
		base.OnDefeated();
		_ = LastBurst(center);
	}

	// The death clip implodes then blasts (frame 12 ≈ 1s in at 12fps): hurt whoever is close then.
	private async Task LastBurst(Vector2 center)
	{
		await ToSignal(GetTree().CreateTimer(BurstDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || PlayerReads.Find(this) is not { } player)
			return;
		if (player.GlobalPosition.DistanceTo(center) < 700f)
			player.ShakeCamera(3f, 0.25f);
		if (player.GlobalPosition.DistanceTo(center) > BurstRadius + 14f)
			return;
		var stats = player.GetNodeOrNull<Stats>("Stats");
		if (stats is null || stats.IsInvulnerable)
			return;
		int before = stats.CurrentHealth;
		stats.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(Stats.AttackPower * BurstDamageMultiplier)), element: DamageElement.Lightning);
		if (stats.CurrentHealth < before)
			player.ApplyKnockback((player.GlobalPosition - center).Normalized() + new Vector2(0f, -0.5f), 360f);
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
		if (Sprite is not null)
			Sprite.SelfModulate = Colors.White;
		if (HoldingAttackSlot)
		{
			EnemyCombatCoordinator.ReleaseAttackSlot();
			HoldingAttackSlot = false;
		}
	}

	private bool Strike(float damageMultiplier)
	{
		Vector2 at = GlobalPosition + new Vector2(FacingRight ? CrescentCenter.X : -CrescentCenter.X, CrescentCenter.Y);
		var query = new PhysicsShapeQueryParameters2D
		{
			Shape = new RectangleShape2D { Size = CrescentSize },
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
				player.ApplyKnockback(new Vector2(Mathf.Sign(player.GlobalPosition.X - GlobalPosition.X), -0.35f).Normalized(), CrescentKnockback);
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

	private void FacePlayer()
	{
		if (PlayerReads.Find(this) is { } player)
		{
			FacingRight = player.GlobalPosition.X >= GlobalPosition.X;
			Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);
		}
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
