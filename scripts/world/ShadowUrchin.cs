using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Erizo sombrío / Shadow Urchin (Hero & Opponents pack, single 128x96 frames, faces LEFT — mirrored
// on the scene). A low mound of living shadow that creeps along the floor and lashes out with
// branching tendrils — or bristles into spikes when you stand too close.
// Sheet beats used:
//   attack_a (10): 1-5 two branched tendrils growing up out of it (the wind-up), 6-7 whipped out
//     far in front, 8-9 drawn back.
//   attack_b (11): 2-3 rearing up, 4-5 lunging out with its tendrils (a bite), 6-10 settling.
//   attack_c (8): 3-4 bursting into spines all around itself, 5-7 settling.
//   hit / dead.
// Moves:
//   Tendril lash — the slow-growing tendrils, then a long whip (~90px). The growth is the tell.
//   Lunge — up close it rears and lunges a short way, biting.
//   Spine burst — stand over it, behind it or hug it and it bristles all around (both sides).
//   Low and creeping — tiny, slow, easy to miss in a dark room.
// Telegraphs: body flashes white before each attack.
public partial class ShadowUrchin : Enemy
{
	[ExportGroup("Tendril lash")]
	[Export] public float LashMinDistance = 50f;
	[Export] public float LashMaxDistance = 100f;
	[Export] public float AttackCooldown = 1.6f;
	[Export] public float LashGrowTime = 0.5f;
	[Export] public float LashDamageMultiplier = 1.1f;
	// Measured (scale 1.5 mirrored, sprite at -6,-61): attack_a frames 6-7 whip out ~90px in front
	// at about waist height.
	[Export] public Vector2 LashCenter = new(50f, -18f);
	[Export] public Vector2 LashSize = new(84f, 40f);

	[ExportGroup("Lunge")]
	[Export] public float LungeRange = 50f;
	[Export] public float LungeSpeed = 160f;
	[Export] public float LungeDamageMultiplier = 1f;
	[Export] public Vector2 LungeCenter = new(34f, -6f);
	[Export] public Vector2 LungeSize = new(40f, 30f);

	[ExportGroup("Spine burst")]
	[Export] public float BurstTrigger = 34f;
	[Export] public float BurstCooldown = 3f;
	[Export] public float BurstDamageMultiplier = 1.2f;
	[Export] public Vector2 BurstSize = new(96f, 52f);

	[ExportGroup("Look")]
	[Export] public Color FlashTint = new(2.2f, 2.2f, 2.2f, 1f);

	private static readonly RandomNumberGenerator Rng = new();

	private Tween _flashTween;
	private bool _busy;
	private float _moveOverride;
	private float _attackCooldownTimer;
	private float _burstCooldownTimer;

	static ShadowUrchin()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_attackCooldownTimer = Rng.RandfRange(0.4f, 1f);
		// The sheet names its death clip "dead"; alias it as "death" on this instance's own copy.
		if (Sprite.SpriteFrames is { } frames && !frames.HasAnimation("death") && frames.HasAnimation("dead"))
		{
			var copy = (SpriteFrames)frames.Duplicate();
			copy.AddAnimation("death");
			copy.SetAnimationLoop("death", false);
			copy.SetAnimationSpeed("death", frames.GetAnimationSpeed("dead"));
			for (int i = 0; i < frames.GetFrameCount("dead"); i++)
				copy.AddFrame("death", frames.GetFrameTexture("dead", i));
			Sprite.SpriteFrames = copy;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;
		float dt = (float)delta;
		_attackCooldownTimer -= dt;
		_burstCooldownTimer -= dt;
		var player = PlayerReads.Find(this);
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval || player is null || !PlayerDetected || _busy || !IsOnFloor())
			return;

		float distanceX = player.GlobalPosition.X - GlobalPosition.X;
		float absDistance = Mathf.Abs(distanceX);
		bool behind = (distanceX >= 0) != FacingRight;
		if ((absDistance <= BurstTrigger || (behind && absDistance <= BurstTrigger * 1.6f)) && _burstCooldownTimer <= 0f)
		{
			_ = SpineBurst();
			return;
		}
		if (_attackCooldownTimer > 0f)
			return;
		if (absDistance <= LungeRange)
			_ = Lunge();
		else if (absDistance >= LashMinDistance && absDistance <= LashMaxDistance)
			_ = TendrilLash();
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_busy)
			return _moveOverride == 0f || CanStepTo(Mathf.Sign(_moveOverride)) ? _moveOverride : 0f;
		float toward = Mathf.Sign(distanceX);
		// Creeps to lash range.
		if (Mathf.Abs(distanceX) > LashMaxDistance * 0.8f && CanStepTo(toward))
			return toward * MoveSpeed;
		return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
	}

	protected override bool CanTurnToFacePlayer => !_busy;

	// ── Tendril lash ────────────────────────────────────────────────────────────────────────

	private async Task TendrilLash()
	{
		if (!Begin())
			return;
		try
		{
			Sprite.Play("attack_a");
			Sprite.Pause();
			FlashBody(LashGrowTime);
			for (int f = 1; f <= 5; f++)
			{
				Sprite.Frame = f;
				if (!await Wait(LashGrowTime / 5f))
					return;
			}
			FacePlayer();
			bool hit = false;
			for (int f = 6; f <= 9; f++)
			{
				Sprite.Frame = f;
				if (!hit && f <= 7)
					hit = Strike(LashCenter, LashSize, LashDamageMultiplier, 240f, centred: false);
				if (!await Wait(0.08f))
					return;
			}
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * Rng.RandfRange(0.85f, 1.2f);
	}

	// ── Lunge ───────────────────────────────────────────────────────────────────────────────

	private async Task Lunge()
	{
		if (!Begin())
			return;
		try
		{
			Sprite.Play("attack_b");
			Sprite.Pause();
			for (int f = 1; f <= 3; f++)
			{
				Sprite.Frame = f;
				if (f == 2)
					FlashBody(0.2f);
				if (!await Wait(0.08f))
					return;
			}
			_moveOverride = (FacingRight ? 1f : -1f) * LungeSpeed;
			bool hit = false;
			for (int f = 4; f <= 10; f++)
			{
				Sprite.Frame = f;
				if (!hit && f <= 5)
					hit = Strike(LungeCenter, LungeSize, LungeDamageMultiplier, 200f, centred: false);
				if (f == 6)
					_moveOverride = 0f;
				if (!await Wait(0.07f))
					return;
			}
		}
		finally
		{
			if (IsInstanceValid(this))
				_moveOverride = 0f;
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * Rng.RandfRange(0.85f, 1.2f);
	}

	// ── Spine burst ─────────────────────────────────────────────────────────────────────────

	private async Task SpineBurst()
	{
		if (!Begin())
			return;
		_burstCooldownTimer = BurstCooldown;
		try
		{
			Sprite.Play("attack_c");
			Sprite.Pause();
			for (int f = 1; f <= 2; f++)
			{
				Sprite.Frame = f;
				if (f == 1)
					FlashBody(0.2f);
				if (!await Wait(0.1f))
					return;
			}
			bool hit = false;
			for (int f = 3; f <= 7; f++)
			{
				Sprite.Frame = f;
				if (!hit && f <= 4)
					hit = Strike(new Vector2(0f, -12f), BurstSize, BurstDamageMultiplier, 260f, centred: true);
				if (!await Wait(0.08f))
					return;
			}
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = Mathf.Max(_attackCooldownTimer, 0.6f);
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

	private bool Strike(Vector2 center, Vector2 size, float damageMultiplier, float knockback, bool centred)
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
				player.ApplyKnockback(new Vector2(Mathf.Sign(player.GlobalPosition.X - GlobalPosition.X), -0.4f).Normalized(), knockback);
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
