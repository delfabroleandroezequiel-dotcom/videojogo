using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Zombi escupidor / Zombie Spitter (Zombies pack, 128x128 cells, faces right). A bloated zombie
// full of acid: it lobs it at you from range and bursts when it dies.
// Sheet beats used:
//   attack (19): 0-5 swelling up, 6-9 the spray out of its mouth, 10-18 sagging back.
//   death (19): 1-5 its chest bursting open, then collapsing. idle / walk / hurt.
//   Glob: the pack's projectile sheet (ZombieSpit).
// Moves:
//   Acid volley — swells (green blinks), then lobs SpitCount globs in a spread around you: one
//     at you, the others short and long. Where they land they leave poison puddles.
//   Spacing — keeps MinDistance..MaxDistance away, never off a ledge.
//   Burst — when it dies its chest pops: acid splashes everything within BurstRadius and leaves a
//     puddle. Don't finish it point-blank.
public partial class ZombieSpitter : Enemy
{
	[ExportGroup("Spacing")]
	[Export] public float MinDistance = 130f;
	[Export] public float MaxDistance = 230f;

	[ExportGroup("Acid volley")]
	[Export] public PackedScene PuddleScene;
	[Export] public float SpitRange = 320f;
	[Export] public float SpitWindup = 0.6f;
	[Export] public int SpitCount = 3;
	[Export] public float SpitSpread = 45f;
	[Export] public float SpitFlightTime = 0.75f;
	[Export] public float SpitGravity = 650f;
	[Export] public float SpitDamageMultiplier = 0.7f;
	[Export] public float AttackCooldown = 2.6f;
	// The mouth on attack frame 6 (scale 2, sprite at -2,1).
	[Export] public Vector2 MouthOffset = new(12f, -22f);

	[ExportGroup("Burst")]
	[Export] public float BurstDelay = 0.25f;
	[Export] public float BurstRadius = 55f;
	[Export] public float BurstDamageMultiplier = 1f;

	[ExportGroup("Look")]
	[Export] public Color FlashTint = new(1.4f, 2.6f, 1.5f, 1f);

	private static readonly RandomNumberGenerator Rng = new();

	private Tween _flashTween;
	private bool _busy;
	private bool _retreating;
	private float _attackCooldownTimer;

	static ZombieSpitter()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_attackCooldownTimer = Rng.RandfRange(0.6f, 1.2f);
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
		_attackCooldownTimer -= (float)delta;
		var player = PlayerReads.Find(this);
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval || player is null || !PlayerDetected || _busy || !IsOnFloor())
			return;
		if (_attackCooldownTimer <= 0f && GlobalPosition.DistanceTo(player.GlobalPosition) <= SpitRange)
			_ = AcidVolley();
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_busy)
			return 0f;
		return KitingMoveX(distanceX, currentVelocityX, MinDistance, MaxDistance, 30f, 1f, ref _retreating);
	}

	protected override bool DesiredFacingRight(Node2D player, float distanceX) => _retreating ? distanceX < 0 : distanceX >= 0;

	protected override bool CanTurnToFacePlayer => !_busy;

	// ── Acid volley ──────────────────────────────────────────────────────────────────────────────

	private async Task AcidVolley()
	{
		if (!Begin())
			return;
		try
		{
			Sprite.Play("attack");
			Sprite.Pause();
			FlashBody(SpitWindup);
			for (int f = 0; f <= 5; f++)
			{
				Sprite.Frame = f;
				if (!await Wait(SpitWindup / 6f))
					return;
			}
			ClearFlash();
			for (int f = 6; f <= 9; f++)
			{
				Sprite.Frame = f;
				if (f == 6)
					SpitAll();
				if (!await Wait(0.07f))
					return;
			}
			for (int f = 10; f <= 18; f++)
			{
				Sprite.Frame = f;
				if (!await Wait(0.05f))
					return;
			}
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * Rng.RandfRange(0.85f, 1.25f);
	}

	// One glob at the player's feet, the rest spread short/long around it; each flies SpitFlightTime.
	private void SpitAll()
	{
		if (GetTree().CurrentScene is not Node scene || PlayerReads.Find(this) is not { } player)
			return;
		Vector2 mouth = GlobalPosition + new Vector2(FacingRight ? MouthOffset.X : -MouthOffset.X, MouthOffset.Y);
		float forward = FacingRight ? 1f : -1f;
		for (int i = 0; i < SpitCount; i++)
		{
			float offset = SpitCount <= 1 ? 0f : Mathf.Lerp(-SpitSpread, SpitSpread, i / (float)(SpitCount - 1));
			Vector2 target = player.GlobalPosition + new Vector2(forward * offset, 0f);
			float time = SpitFlightTime * Rng.RandfRange(0.9f, 1.1f);
			Vector2 velocity = new((target.X - mouth.X) / time, (target.Y - mouth.Y - 0.5f * SpitGravity * time * time) / time);
			var spit = new ZombieSpit
			{
				Velocity = velocity,
				GravityScale = SpitGravity,
				ThrowerStats = Stats,
				DamageMultiplier = SpitDamageMultiplier,
				PuddleScene = PuddleScene,
			};
			scene.AddChild(spit);
			spit.GlobalPosition = mouth;
		}
		Sfx.PlayAt(this, "Combat/Bow", "Bow Attack");
	}

	// ── Burst ────────────────────────────────────────────────────────────────────────────────────

	private async void Burst()
	{
		Vector2 at = GlobalPosition;
		Stats stats = Stats;
		await ToSignal(GetTree().CreateTimer(BurstDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this))
			return;
		var palette = new GroundSlamFx.Palette
		{
			Hot = new Color(0.45f, 1f, 0.55f, 1f),
			Core = new Color(0.9f, 1f, 0.9f, 1f),
			Rock = new Color(0.3f, 0.45f, 0.3f, 1f),
			Dust = new Color(0.4f, 0.6f, 0.4f, 0.5f),
			Light = new Color(0.45f, 1f, 0.55f, 1f),
		};
		GroundSlamFx.Impact(this, at + new Vector2(0f, BodyBottom), 0.45f, palette);
		if (PlayerReads.Find(this) is { } player && player.GlobalPosition.DistanceTo(at) <= BurstRadius
			&& player.GetNodeOrNull<Stats>("Stats") is { IsInvulnerable: false } playerStats)
		{
			playerStats.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(stats.AttackPower * BurstDamageMultiplier)),
				isProjectile: true, element: DamageElement.Poison);
			player.ApplyKnockback(new Vector2(Mathf.Sign(player.GlobalPosition.X - at.X), -0.4f).Normalized(), 220f);
		}
		if (PuddleScene is not null && GetTree().CurrentScene is Node scene)
		{
			Node2D puddle = PuddleScene.Instantiate<Node2D>();
			scene.AddChild(puddle);
			puddle.GlobalPosition = at + new Vector2(0f, BodyBottom);
		}
	}

	// ── Shared ───────────────────────────────────────────────────────────────────────────────────

	private async Task Flinch()
	{
		_busy = true;
		try
		{
			Sprite.Play("hurt");
			await ToSignal(GetTree().CreateTimer(0.25f), SceneTreeTimer.SignalName.Timeout);
		}
		finally
		{
			if (IsInstanceValid(this))
				_busy = false;
		}
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
		Burst();
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
