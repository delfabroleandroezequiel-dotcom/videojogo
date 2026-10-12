using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Nigromante de sangre / Blood Necromancer (Necromancer by CreativeKind, 128x128 cells, faces right).
// A blood mage: keeps its distance, fires seeking blood bolts, and makes something horrible burst out
// of the floor in front of it when you get close.
// The sheet draws its casting clips (attack, spawn) 13px further right and 16px lower in the cell
// than the rest (idle/walk/hurt/death), so those clips get a Sprite.Offset to line the body up.
// Sheet beats used:
//   attack (47): 0-31 dark power gathering (played sped up as the charge), 32 the cast, 33+ the
//     drawn bolt flying off (we fire our own bolt there), 45-46 recovery.
//   spawn (20): 0-8 calling, 9-13 a horror rising out of the floor ~in front of it (skull → blob →
//     tentacled spirit), 14-18 it bursting apart, 19 recovery.
//   hurt: flinch.
// Moves:
//   Blood bolt — after the charge it looses a crimson orb that bends toward you (NecroBolt, red).
//   Blood bloom — get close and it calls the horror up out of the floor in front of it: hits hard
//     up close. Its main defence.
//   Back off — keeps its distance; flinches when hit (it's frail).
// Telegraphs: body flashes crimson while charging.
public partial class BloodNecromancer : Enemy
{
	[ExportGroup("Spacing")]
	[Export] public float MinDistance = 140f;
	[Export] public float MaxDistance = 270f;

	[ExportGroup("Blood bolt")]
	[Export] public float BoltRange = 380f;
	[Export] public float AttackCooldown = 2f;
	[Export] public float BoltCharge = 0.6f;
	[Export] public float BoltDamageMultiplier = 0.9f;
	[Export] public Vector2 BoltOrigin = new(26f, -6f);

	[ExportGroup("Blood bloom")]
	[Export] public float BloomTrigger = 85f;
	[Export] public float BloomCooldown = 3.5f;
	[Export] public float BloomCall = 0.45f;
	[Export] public float BloomDamageMultiplier = 1.3f;
	// spawn frames 11-13 (scale 1.3): the horror rises ~30-80px in front, up to ~60px tall.
	[Export] public Vector2 BloomHitboxCenter = new(52f, 0f);
	[Export] public Vector2 BloomHitboxSize = new(56f, 62f);

	[ExportGroup("Look")]
	[Export] public Color BloodColor = new(0.95f, 0.15f, 0.25f, 1f);
	[Export] public Color FlashTint = new(2.2f, 0.9f, 0.9f, 1f);
	[Export] public Vector2 CastClipOffset = new(-13f, -16f);
	[Export] public float DecisionInterval = 0.35f;

	private static readonly RandomNumberGenerator Rng = new();

	private Tween _flashTween;
	private bool _busy;
	private bool _retreating;
	private float _decisionTimer;
	private float _attackCooldownTimer;
	private float _bloomCooldownTimer;

	static BloodNecromancer()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_attackCooldownTimer = Rng.RandfRange(0.6f, 1.2f);
		Sprite.AnimationChanged += () =>
			Sprite.Offset = Sprite.Animation == "attack" || Sprite.Animation == "spawn" ? CastClipOffset : Vector2.Zero;
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
		float dt = (float)delta;
		_decisionTimer -= dt;
		_attackCooldownTimer -= dt;
		_bloomCooldownTimer -= dt;
		var player = PlayerReads.Find(this);
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval || player is null || !PlayerDetected || _busy || !IsOnFloor())
			return;

		float absDistance = Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X);
		if (absDistance <= BloomTrigger && _bloomCooldownTimer <= 0f)
		{
			_ = BloodBloom();
			return;
		}
		if (_decisionTimer > 0f)
			return;
		_decisionTimer = DecisionInterval;
		if (_attackCooldownTimer <= 0f && absDistance <= BoltRange)
			_ = BloodBolt();
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_busy)
			return 0f;
		return KitingMoveX(distanceX, currentVelocityX, MinDistance, MaxDistance, 40f, 1.15f, ref _retreating);
	}

	protected override bool DesiredFacingRight(Node2D player, float distanceX) => _retreating ? distanceX < 0 : distanceX >= 0;

	protected override bool CanTurnToFacePlayer => !_busy;

	// ── Blood bolt ──────────────────────────────────────────────────────────────────────────

	private async Task BloodBolt()
	{
		if (!Begin())
			return;
		try
		{
			Sprite.Play("attack");
			Sprite.Pause();
			FlashBody(BoltCharge);
			// 32 frames of gathering squeezed into the charge.
			for (int f = 0; f <= 31; f += 2)
			{
				Sprite.Frame = f;
				if (!await Wait(BoltCharge / 16f))
					return;
			}
			Sprite.Frame = 32;
			if (!await Wait(0.06f))
				return;
			Sprite.Frame = 33;
			FireBolt();
			if (!await Wait(0.2f))
				return;
			Sprite.Frame = 45;
			if (!await Wait(0.1f))
				return;
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * Rng.RandfRange(0.85f, 1.25f);
	}

	private void FireBolt()
	{
		if (GetTree().CurrentScene is not Node scene || PlayerReads.Find(this) is not { } player)
			return;
		Vector2 origin = GlobalPosition + new Vector2(FacingRight ? BoltOrigin.X : -BoltOrigin.X, BoltOrigin.Y);
		var bolt = new NecroBolt
		{
			Direction = (player.GlobalPosition - origin).Normalized(),
			ThrowerStats = Stats,
			DamageMultiplier = BoltDamageMultiplier,
			GlowColor = BloodColor,
			Speed = 190f,
			PoisonTickDamage = 0,
			PoisonDuration = 0f,
		};
		scene.AddChild(bolt);
		bolt.GlobalPosition = origin;
	}

	// ── Blood bloom ─────────────────────────────────────────────────────────────────────────

	private async Task BloodBloom()
	{
		if (!Begin())
			return;
		_bloomCooldownTimer = BloomCooldown;
		try
		{
			Sprite.Play("spawn");
			Sprite.Pause();
			FlashBody(BloomCall);
			for (int f = 0; f <= 8; f++)
			{
				Sprite.Frame = f;
				if (!await Wait(BloomCall / 9f))
					return;
			}
			bool hit = false;
			for (int f = 9; f <= 19; f++)
			{
				Sprite.Frame = f;
				if (!hit && f >= 11 && f <= 14)
					hit = Strike();
				if (!await Wait(0.07f))
					return;
			}
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = Mathf.Max(_attackCooldownTimer, 0.6f);
	}

	private bool Strike()
	{
		Vector2 at = GlobalPosition + new Vector2(FacingRight ? BloomHitboxCenter.X : -BloomHitboxCenter.X, BloomHitboxCenter.Y);
		var query = new PhysicsShapeQueryParameters2D
		{
			Shape = new RectangleShape2D { Size = BloomHitboxSize },
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
			stats.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(Stats.AttackPower * BloomDamageMultiplier)));
			if (stats.CurrentHealth < before)
			{
				player.ApplyKnockback(new Vector2(Mathf.Sign(player.GlobalPosition.X - GlobalPosition.X), -0.6f).Normalized(), 320f);
				BloodEffect.SpawnAt(this, player.GlobalPosition);
			}
			return true;
		}
		return false;
	}

	// ── Flinch ──────────────────────────────────────────────────────────────────────────────

	private async Task Flinch()
	{
		_busy = true;
		try
		{
			Sprite.Play("hurt");
			await ToSignal(GetTree().CreateTimer(0.3f), SceneTreeTimer.SignalName.Timeout);
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
