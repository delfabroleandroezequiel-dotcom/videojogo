using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Zombi gordo / Zombie Brute (Zombies pack, 128x128 cells, faces right). A huge bloated zombie that
// throws its whole weight at you.
// Sheet beats used:
//   attack (22): 0-7 rearing up onto its toes (wind-up), 8 the belly-flop forward with a splash,
//     9-12 the splash spraying out, 13-16 lying on its face, 17-21 heaving itself back up.
//   idle / walk / hurt (frame 1 white flash) / death.
// Moves:
//   Belly flop — long wind-up (green blinks), then it flops forward a body length: a big hit in
//     front and a low rot wave rolling out to both sides along the floor (jump it).
//   Face down — after the flop it lies there for LieTime: the opening to punish it.
//   Bulk — never flinches and can't be knocked back.
public partial class ZombieBrute : Enemy
{
	[ExportGroup("Belly flop")]
	[Export] public float FlopTrigger = 85f;
	[Export] public float FlopWindup = 0.85f;
	[Export] public float FlopLurchSpeed = 200f;
	[Export] public float FlopDamageMultiplier = 1.6f;
	[Export] public float AttackCooldown = 1.8f;
	// Frame 8: the body lands ~0-110px in front, from chest height to the floor.
	[Export] public Vector2 FlopHitboxCenter = new(58f, 6f);
	[Export] public Vector2 FlopHitboxSize = new(100f, 50f);
	[Export] public float ImpactOffsetX = 80f;
	[Export] public float ImpactScale = 0.55f;
	[Export] public float LieTime = 1.1f;

	[ExportGroup("Rot wave")]
	[Export] public int WaveSteps = 4;
	[Export] public float WaveStepLength = 26f;
	[Export] public float WaveStepTime = 0.07f;
	[Export] public float WaveDamageMultiplier = 0.7f;
	[Export] public Vector2 WaveHitboxSize = new(24f, 18f);

	[ExportGroup("Look")]
	[Export] public Color FlashTint = new(1.4f, 2.6f, 1.5f, 1f);

	private static readonly RandomNumberGenerator Rng = new();
	private static readonly GroundSlamFx.Palette RotPalette = new()
	{
		Hot = new Color(0.45f, 1f, 0.75f, 1f),
		Core = new Color(0.85f, 1f, 0.95f, 1f),
		Rock = new Color(0.25f, 0.33f, 0.3f, 1f),
		Dust = new Color(0.4f, 0.5f, 0.45f, 0.6f),
		Light = new Color(0.4f, 1f, 0.7f, 1f),
	};

	private Tween _flashTween;
	private bool _busy;
	private float _moveOverride;
	private float _attackCooldownTimer;

	static ZombieBrute()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_attackCooldownTimer = Rng.RandfRange(0.4f, 1f);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;
		_attackCooldownTimer -= (float)delta;
		var player = PlayerReads.Find(this);
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval || player is null || !PlayerDetected || _busy || !IsOnFloor())
			return;
		float absDx = Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X);
		float absDy = Mathf.Abs(player.GlobalPosition.Y - GlobalPosition.Y);
		if (absDx <= FlopTrigger && absDy <= 50f && _attackCooldownTimer <= 0f)
			_ = BellyFlop();
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_busy)
			return _moveOverride == 0f || CanStepTo(Mathf.Sign(_moveOverride)) ? _moveOverride : 0f;
		return base.ComputeMoveX(player, distanceX, currentVelocityX, delta);
	}

	protected override bool CanTurnToFacePlayer => !_busy;

	// ── Belly flop ───────────────────────────────────────────────────────────────────────────────

	private async Task BellyFlop()
	{
		if (!Begin())
			return;
		try
		{
			Sprite.Play("attack");
			Sprite.Pause();
			FlashBody(FlopWindup);
			for (int f = 0; f <= 7; f++)
			{
				Sprite.Frame = f;
				if (!await Wait(FlopWindup / 8f))
					return;
			}
			ClearFlash();

			float forward = FacingRight ? 1f : -1f;
			_moveOverride = forward * FlopLurchSpeed;
			Sprite.Frame = 8;
			if (!await Wait(0.08f))
				return;
			_moveOverride = 0f;
			Strike(FlopHitboxCenter, FlopHitboxSize, FlopDamageMultiplier, 280f);
			Vector2 ground = GlobalPosition + new Vector2(forward * ImpactOffsetX, BodyBottom);
			GroundSlamFx.Impact(this, ground, ImpactScale, RotPalette);
			_ = RotWave(1f, ground);
			_ = RotWave(-1f, GlobalPosition + new Vector2(0f, BodyBottom));
			for (int f = 9; f <= 12; f++)
			{
				Sprite.Frame = f;
				if (!await Wait(0.07f))
					return;
			}

			// Face down — the opening.
			for (int f = 13; f <= 16; f++)
			{
				Sprite.Frame = f;
				if (!await Wait(LieTime / 4f))
					return;
			}
			for (int f = 17; f <= 21; f++)
			{
				Sprite.Frame = f;
				if (!await Wait(0.09f))
					return;
			}
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * Rng.RandfRange(0.85f, 1.25f);
	}

	// A low wave of rot rolling along the floor from `start`; stops at a wall or a drop.
	private async Task RotWave(float direction, Vector2 start)
	{
		Vector2 at = start;
		bool hit = false;
		for (int i = 0; i < WaveSteps; i++)
		{
			await ToSignal(GetTree().CreateTimer(WaveStepTime), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this))
				return;
			Vector2 next = at + new Vector2(direction * WaveStepLength, 0f);
			var wall = PhysicsRayQueryParameters2D.Create(at + new Vector2(0f, -8f), next + new Vector2(0f, -8f), 1u);
			if (GetWorld2D().DirectSpaceState.IntersectRay(wall).Count > 0)
				return;
			var floor = PhysicsRayQueryParameters2D.Create(next + new Vector2(0f, -10f), next + new Vector2(0f, 14f), 1u);
			var ground = GetWorld2D().DirectSpaceState.IntersectRay(floor);
			if (ground.Count == 0)
				return;
			at = (Vector2)ground["position"];
			GroundSlamFx.Chip(this, at, 0.4f, RotPalette);
			if (!hit)
				hit = StrikeAt(at + new Vector2(0f, -WaveHitboxSize.Y * 0.5f), WaveHitboxSize, WaveDamageMultiplier, 160f);
		}
	}

	// ── Shared ───────────────────────────────────────────────────────────────────────────────────

	private bool Strike(Vector2 center, Vector2 size, float damageMultiplier, float knockback) =>
		StrikeAt(GlobalPosition + new Vector2(FacingRight ? center.X : -center.X, center.Y), size, damageMultiplier, knockback);

	private bool StrikeAt(Vector2 at, Vector2 size, float damageMultiplier, float knockback)
	{
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
				float away = Mathf.Sign(player.GlobalPosition.X - GlobalPosition.X);
				player.ApplyKnockback(new Vector2(away == 0f ? 1f : away, -0.4f).Normalized(), knockback);
				BloodEffect.SpawnAt(this, player.GlobalPosition);
			}
			return true;
		}
		return false;
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
		_busy = false;
		_moveOverride = 0f;
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

	// Too heavy to be shoved around — not by hits, not by the player's body.
	public override bool BodyPushable => false;

	public override void ApplyKnockback(Vector2 direction, float force)
	{
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
