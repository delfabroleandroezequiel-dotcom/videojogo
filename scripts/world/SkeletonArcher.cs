using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Esqueleto arquero / Skeleton Archer (Undead pack, 110x80 cells, faces right). A rattling, clumsy
// shooter: it keeps its distance and peppers you, but its aim is poor (it barely leads) — what
// makes it dangerous is the volume of arrows and that, like every skeleton, it reassembles once.
// Sheet beats used:
//   attack (8.3fps): 0-1 reaching for the quiver, 2-3 nocking, 4-7 the arrow drawn (6 is held as the
//     aim), 8 released (bow empty), 9 recovery.
//   jumpfall: 0-2 springing back, 3-4 airborne with the bow raised — the backstep shot.
//   death / revival: collapsing into a bone pile / reassembling from it.
// Moves:
//   Aimed shot — draws, holds its aim (flashing), and looses. Leads the player only a little.
//   Rattle volley — three quick shots in a row, each weaker, when you're far or in the air.
//   Backstep shot — if you get in close it springs backwards and shoots you from the air
//     (only when there's ground to land on; cornered, it just shoots point-blank).
//   Reassemble — same trait as the warrior: its first "death" only collapses it into a bone pile;
//     hit the pile before it reassembles or it gets back up with part of its health.
// Arrows: ElfArrow (pass through terrain) with a spectral-cyan streak.
public partial class SkeletonArcher : Enemy
{
	[ExportGroup("Spacing")]
	[Export] public float MinDistance = 120f;
	[Export] public float MaxDistance = 270f;
	[Export] public float RetreatOvershoot = 40f;
	[Export] public float RetreatSpeedMultiplier = 1.25f;

	[ExportGroup("Shots")]
	[Export] public PackedScene ArrowScene;
	[Export] public float ArrowSpeed = 340f;
	[Export] public float ShotRange = 360f;
	[Export] public float AttackCooldown = 1.6f;
	[Export] public float AimHold = 0.45f;
	[Export] public float LeadAccuracy = 0.35f;
	[Export] public float VolleyChance = 0.3f;
	[Export] public int VolleyCount = 3;
	[Export] public float VolleyDamageMultiplier = 0.8f;
	[Export] public float VolleyInterval = 0.28f;
	// Bow hand, measured off attack frame 6 (scale 1.5, sprite at -5,-6).
	[Export] public Vector2 ArrowOrigin = new(18f, -17f);
	[Export] public Color ArrowTrailColor = new(0.3f, 0.85f, 1f, 1f);
	[Export] public Color FlashTint = new(2.2f, 2.2f, 2.2f, 1f);

	[ExportGroup("Backstep shot")]
	[Export] public float BackstepTrigger = 60f;
	[Export] public float BackstepCooldown = 3.5f;
	[Export] public Vector2 BackstepVelocity = new(200f, -300f);

	[ExportGroup("Reassemble")]
	[Export] public float ReassembleDelay = 2.5f;
	[Export] public float ReviveHealthRatio = 0.5f;

	[Export] public float DecisionInterval = 0.3f;

	private static readonly RandomNumberGenerator Rng = new();
	private readonly DashWatcher _dash = new();

	private Tween _flashTween;
	private bool _busy;
	private bool _hopping;
	private bool _retreating;
	private bool _collapsed;
	private bool _revived;
	private bool _shatter;
	private float _decisionTimer;
	private float _attackCooldownTimer;
	private float _backstepCooldownTimer;

	static SkeletonArcher()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_attackCooldownTimer = Rng.RandfRange(0.5f, 1.1f);
		Stats.MinHealth = 1;
		Stats.HealthChanged += (current, max) => OnHealthChanged(current);
	}

	private void OnHealthChanged(int current)
	{
		if (_collapsed)
		{
			_shatter = true;
			return;
		}
		if (current <= 1 && !_revived && Stats.MinHealth > 0)
			_ = Collapse();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;
		float dt = (float)delta;
		_decisionTimer -= dt;
		_attackCooldownTimer -= dt;
		_backstepCooldownTimer -= dt;

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

		// Crowded → spring back and shoot from the air.
		if (absDistance <= BackstepTrigger && _backstepCooldownTimer <= 0f)
		{
			_ = BackstepShot(player);
			return;
		}

		if (_attackCooldownTimer > 0f || _decisionTimer > 0f || absDistance > ShotRange)
			return;
		_decisionTimer = DecisionInterval;

		// Don't waste a shot into a raised guard or mid-dash; wait a beat.
		if (!_dash.GoodMomentToRelease(player, GlobalPosition.X, 0.1f) && absDistance > BackstepTrigger)
			return;

		bool volley = Rng.Randf() < VolleyChance + (!player.IsOnFloor() || absDistance > MaxDistance ? 0.3f : 0f);
		_ = volley ? Volley() : AimedShot();
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

	// Shoots facing the player, but runs away facing where it's going.
	protected override bool DesiredFacingRight(Node2D player, float distanceX) => _retreating ? distanceX < 0 : distanceX >= 0;

	protected override bool CanTurnToFacePlayer => !_busy && !_hopping;

	// ── Shots ───────────────────────────────────────────────────────────────────────────────

	private async Task AimedShot()
	{
		if (!Begin())
			return;
		try
		{
			if (!await Draw())
				return;
			Sprite.Frame = 6;
			FlashBody(AimHold);
			if (!await Wait(AimHold))
				return;
			Loose(1f);
			if (!await Recover())
				return;
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * Rng.RandfRange(0.85f, 1.25f);
	}

	private async Task Volley()
	{
		if (!Begin())
			return;
		try
		{
			if (!await Draw())
				return;
			FlashBody(0.25f);
			for (int i = 0; i < VolleyCount; i++)
			{
				Sprite.Frame = 6;
				if (!await Wait(i == 0 ? 0.25f : VolleyInterval * 0.5f))
					return;
				FacePlayer();
				Loose(VolleyDamageMultiplier);
				if (!await Wait(VolleyInterval * 0.5f))
					return;
			}
			if (!await Recover())
				return;
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * 1.3f;
	}

	// Quiver → nock → full draw, played a little faster than the sheet so it isn't sluggish.
	private async Task<bool> Draw()
	{
		Sprite.Play("attack");
		Sprite.Pause();
		for (int f = 0; f <= 5; f++)
		{
			Sprite.Frame = f;
			if (!await Wait(0.07f))
				return false;
		}
		return true;
	}

	private async Task<bool> Recover()
	{
		Sprite.Frame = 8;
		if (!await Wait(0.12f))
			return false;
		Sprite.Frame = 9;
		return await Wait(0.15f);
	}

	private void Loose(float damageMultiplier)
	{
		Sprite.Frame = 8;
		if (ArrowScene is null || PlayerReads.Find(this) is not { } player || GetTree().CurrentScene is not Node scene)
			return;
		Vector2 origin = GlobalPosition + new Vector2(FacingRight ? ArrowOrigin.X : -ArrowOrigin.X, ArrowOrigin.Y);
		Vector2 direction = PlayerReads.PredictAimDirection(origin, player, ArrowSpeed, LeadAccuracy, 0.2f, new Vector2(0f, -6f));
		// A bow can't shoot behind itself: clamp to its front half.
		if (Mathf.Sign(direction.X) != (FacingRight ? 1f : -1f))
			direction = new Vector2(FacingRight ? 1f : -1f, direction.Y).Normalized();
		var arrow = ArrowScene.Instantiate<ElfArrow>();
		arrow.Speed = ArrowSpeed;
		arrow.DamageMultiplier = damageMultiplier;
		arrow.TrailColor = ArrowTrailColor;
		scene.AddChild(arrow);
		arrow.GlobalPosition = origin;
		arrow.Launch(direction, Stats);
		Sfx.PlayAt(this, "Combat/Bow", "Bow Attack");
	}

	// ── Backstep shot ───────────────────────────────────────────────────────────────────────

	private async Task BackstepShot(Metroidvania.Player.Player player)
	{
		float away = -Mathf.Sign(player.GlobalPosition.X - GlobalPosition.X);
		if (away == 0f)
			away = FacingRight ? -1f : 1f;
		var launch = new Vector2(away * BackstepVelocity.X, BackstepVelocity.Y);
		bool canHop = LandsSafely(launch, 40f);
		if (!Begin())
			return;
		_backstepCooldownTimer = BackstepCooldown;
		try
		{
			if (!canHop)
			{
				// Cornered: a hurried point-blank shot.
				Sprite.Play("attack");
				Sprite.Pause();
				Sprite.Frame = 5;
				FlashBody(0.2f);
				if (!await Wait(0.2f))
					return;
				Loose(1f);
				await Recover();
				return;
			}

			FacePlayer();
			Velocity = launch;
			_hopping = true;
			Sprite.Play("jumpfall");
			Sprite.Pause();
			Sprite.Frame = 0;
			float t = 0f;
			bool shot = false;
			while (_hopping && t < 1.2f)
			{
				await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
				if (!IsInstanceValid(this) || IsQueuedForRemoval || _collapsed)
					return;
				t += (float)GetPhysicsProcessDeltaTime();
				Sprite.Frame = t < 0.08f ? 1 : t < 0.16f ? 2 : 3;
				// Looses at the top of the arc.
				if (!shot && Velocity.Y > -40f)
				{
					shot = true;
					FacePlayer();
					Sprite.Play("attack");
					Sprite.Pause();
					Loose(1f);
				}
			}
			Sprite.Play("jumpfall");
			Sprite.Pause();
			Sprite.Frame = 4;
			if (!await Wait(0.15f))
				return;
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * 0.6f;
	}

	// ── Reassemble ──────────────────────────────────────────────────────────────────────────

	private async Task Collapse()
	{
		_collapsed = true;
		_shatter = false;
		_busy = true;
		ClearFlash();
		if (HoldingAttackSlot)
		{
			EnemyCombatCoordinator.ReleaseAttackSlot();
			HoldingAttackSlot = false;
		}
		Sprite.Play("death");
		float t = 0f;
		while (t < ReassembleDelay)
		{
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;
			t += (float)GetPhysicsProcessDeltaTime();
			if (_shatter)
			{
				Stats.MinHealth = 0;
				ImpactEffect.SpawnAt(this, GlobalPosition + new Vector2(0f, 20f));
				Stats.Kill();
				return;
			}
			if (ReassembleDelay - t < 0.5f)
				Visual.Position = new Vector2(Mathf.Sin(t * 70f) * 1.5f, 0f);
		}
		Visual.Position = Vector2.Zero;

		Sprite.Play("revival");
		FlashBody(0.8f);
		await ToSignal(GetTree().CreateTimer(10f / 7.1f), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;
		_revived = true;
		_collapsed = false;
		Stats.MinHealth = 0;
		Stats.SetCurrentHealth(Mathf.Max(1, Mathf.RoundToInt(Stats.MaxHealth * ReviveHealthRatio)));
		_busy = false;
		_attackCooldownTimer = 0.4f;
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
		if (!_collapsed)
			_busy = false;
		ClearFlash();
		if (HoldingAttackSlot)
		{
			EnemyCombatCoordinator.ReleaseAttackSlot();
			HoldingAttackSlot = false;
		}
	}

	// False once it's gone or collapsed, so an interrupted shot stops instead of driving frames
	// over the death animation.
	private async Task<bool> Wait(float seconds)
	{
		await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
		return IsInstanceValid(this) && !IsQueuedForRemoval && !_collapsed;
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
		bool alreadyDown = _collapsed;
		ClearFlash();
		Visual.Position = Vector2.Zero;
		base.OnDefeated();
		if (alreadyDown && Sprite?.Animation == "death")
			Sprite.Frame = Sprite.SpriteFrames.GetFrameCount("death") - 1;
	}

	// ── Animation ───────────────────────────────────────────────────────────────────────────

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null || _busy || _hopping)
			return;
		string anim = !IsOnFloor() ? "jumpfall"
			: _retreating && Mathf.Abs(velocity.X) > 5f ? "run"
			: Mathf.Abs(velocity.X) > 5f ? "walk"
			: "idle";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	protected override bool ContactDamageEnabled => false;
}
