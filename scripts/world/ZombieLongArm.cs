using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Zombi brazo largo / Zombie Long-Arm (Zombies pack, 128x128 cells, faces right). A gaunt zombie
// with one grotesquely stretched arm that shoots out like a whip.
// Sheet beats used:
//   attack (19): 0-5 the arm drawn back (wind-up), 6-9 flung straight out ~100px, 10-15 the arm
//     reeling back in, 16-18 back to rest.
//   idle / walk / hurt (frame 1 white flash) / death.
// Moves:
//   Hook — from mid range it flings the arm out in a long, thin line at chest height. If it catches
//     you it reels you in close...
//   ...Reel-in jab — and immediately jabs again, point-blank, with a much shorter wind-up. Roll
//     out of the pull to dodge it.
//   Spacing — keeps you at the end of its reach, backing away if you get inside it.
// Telegraphs: green blinks on the wind-up (short ones on the follow-up).
public partial class ZombieLongArm : Enemy
{
	[ExportGroup("Spacing")]
	[Export] public float MinDistance = 60f;
	[Export] public float MaxDistance = 105f;

	[ExportGroup("Hook")]
	[Export] public float HookMinDistance = 40f;
	[Export] public float HookMaxDistance = 118f;
	[Export] public float HookWindup = 0.55f;
	[Export] public float HookDamageMultiplier = 0.8f;
	// A knockback of force F carries the player about F * PullTravelTime px (measured headless:
	// 320 dragged them ~90px). The pull is scaled to stop them PullStopDistance away.
	[Export] public float PullTravelTime = 0.28f;
	[Export] public float PullStopDistance = 34f;
	[Export] public float MaxPullForce = 340f;
	[Export] public float AttackCooldown = 1.7f;
	// Frames 6-9: a thin line from ~15 to ~105px in front, chest high.
	[Export] public Vector2 HookHitboxCenter = new(60f, -16f);
	[Export] public Vector2 HookHitboxSize = new(92f, 18f);

	[ExportGroup("Reel-in jab")]
	[Export] public float JabWindup = 0.18f;
	[Export] public float JabDamageMultiplier = 1.1f;
	[Export] public Vector2 JabHitboxCenter = new(30f, -14f);
	[Export] public Vector2 JabHitboxSize = new(46f, 26f);

	[ExportGroup("Look")]
	[Export] public Color FlashTint = new(1.4f, 2.6f, 1.5f, 1f);
	[Export] public Color TrailGlowColor = new(0.7f, 0.3f, 1f, 1f);

	private static readonly RandomNumberGenerator Rng = new();

	private EnemySlashTrail _trail;
	private Tween _flashTween;
	private bool _busy;
	private bool _retreating;
	private float _attackCooldownTimer;

	static ZombieLongArm()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_attackCooldownTimer = Rng.RandfRange(0.3f, 0.9f);
		_trail = new EnemySlashTrail
		{
			CoreWidth = 2f,
			GlowWidth = 6f,
			GlowColor = TrailGlowColor,
			CoreColor = new Color(0.9f, 0.8f, 1f, 1f),
			SparkAmount = 6,
		};
		Visual.AddChild(_trail);
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
		float absDx = Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X);
		float absDy = Mathf.Abs(player.GlobalPosition.Y - GlobalPosition.Y);
		if (absDx >= HookMinDistance && absDx <= HookMaxDistance && absDy <= 30f && _attackCooldownTimer <= 0f)
			_ = Hook();
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_busy)
			return 0f;
		return KitingMoveX(distanceX, currentVelocityX, MinDistance, MaxDistance, 15f, 1f, ref _retreating);
	}

	protected override bool DesiredFacingRight(Node2D player, float distanceX) => _retreating ? distanceX < 0 : distanceX >= 0;

	protected override bool CanTurnToFacePlayer => !_busy;

	// ── Hook + reel-in jab ───────────────────────────────────────────────────────────────────────

	private async Task Hook()
	{
		if (!Begin())
			return;
		try
		{
			Sprite.Play("attack");
			Sprite.Pause();
			if (!await Fling(HookWindup, true))
				return;
			bool caught = false;
			for (int f = 6; f <= 9; f++)
			{
				Sprite.Frame = f;
				if (!caught && Strike(HookHitboxCenter, HookHitboxSize, HookDamageMultiplier, out var player, armInvulnerability: false))
				{
					caught = true;
					ReelIn(player);
				}
				if (!await Wait(0.05f))
					return;
			}
			for (int f = 10; f <= 13; f++)
			{
				Sprite.Frame = f;
				if (!await Wait(caught ? 0.04f : 0.08f))
					return;
			}

			if (caught)
			{
				if (!await Fling(JabWindup, false))
					return;
				Strike(JabHitboxCenter, JabHitboxSize, JabDamageMultiplier, out _);
				for (int f = 7; f <= 13; f++)
				{
					Sprite.Frame = f;
					if (!await Wait(0.05f))
						return;
				}
			}
			for (int f = 14; f <= 18; f++)
			{
				Sprite.Frame = f;
				if (!await Wait(0.08f))
					return;
			}
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * Rng.RandfRange(0.85f, 1.25f);
	}

	// Drags the caught player in to PullStopDistance from it.
	private void ReelIn(Metroidvania.Player.Player player)
	{
		float toward = Mathf.Sign(GlobalPosition.X - player.GlobalPosition.X);
		float gap = Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X) - PullStopDistance;
		if (gap > 0f)
			player.ApplyKnockback(new Vector2(toward, -0.15f).Normalized(), Mathf.Min(MaxPullForce, gap / PullTravelTime));
	}

	// Wind-up frames 0-5 (flashing) then frame 6, the arm out, with its streak.
	private async Task<bool> Fling(float windup, bool full)
	{
		FlashBody(windup);
		for (int f = full ? 0 : 3; f <= 5; f++)
		{
			Sprite.Frame = f;
			if (!await Wait(windup / (full ? 6f : 3f)))
				return false;
		}
		ClearFlash();
		Sfx.PlayAt(this, "Combat/Sword", "Sword Attack");
		_trail.PlayThrust(new Vector2(10f, HookHitboxCenter.Y), new Vector2(full ? 104f : 52f, HookHitboxCenter.Y), 0.05f, 0.1f);
		Sprite.Frame = 6;
		return true;
	}

	// The hook doesn't arm the player's post-hit invulnerability, or the reel-in jab could never land.
	private bool Strike(Vector2 center, Vector2 size, float damageMultiplier, out Metroidvania.Player.Player caught, bool armInvulnerability = true)
	{
		caught = null;
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
			stats.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(Stats.AttackPower * damageMultiplier)), armInvulnerability: armInvulnerability);
			if (stats.CurrentHealth < before)
			{
				BloodEffect.SpawnAt(this, player.GlobalPosition);
				caught = player;
				return true;
			}
			return false;
		}
		return false;
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
