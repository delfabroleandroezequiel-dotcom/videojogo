using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Zombi reptante / Zombie Crawler (Zombies pack, 128x128 cells, faces right). A legless torso
// dragging itself along the floor — low, fast, and easy to miss among the corpses.
// Sheet beats used:
//   attack (11): 0-4 pulling itself in, 5-6 the snap forward at your ankles, 7-10 settling.
//   walk (11): the arm-over-arm drag. idle / hurt / death.
// Moves:
//   Playing dead — sits motionless on the floor (paused on idle frame 0) until you come within
//     AmbushDistance, then it springs to life.
//   Ankle bite — a quick snap forward along the floor; low, so jumping clears it. The bite is
//     infected (Poison element).
//   Scuttle — drags itself toward you faster than the walkers.
// Telegraphs: a short green blink before the snap.
public partial class ZombieCrawler : Enemy
{
	[ExportGroup("Playing dead")]
	[Export] public bool StartPlayingDead = true;
	[Export] public float AmbushDistance = 90f;

	[ExportGroup("Ankle bite")]
	[Export] public float BiteTrigger = 50f;
	[Export] public float BiteWindup = 0.25f;
	[Export] public float SnapSpeed = 260f;
	[Export] public float BiteDamageMultiplier = 0.8f;
	[Export] public float AttackCooldown = 1.1f;
	// Frames 5-6: the arm/jaw reaches ~30px in front, at floor level.
	[Export] public Vector2 BiteHitboxCenter = new(22f, 0f);
	[Export] public Vector2 BiteHitboxSize = new(34f, 18f);

	[ExportGroup("Look")]
	[Export] public Color FlashTint = new(1.4f, 2.6f, 1.5f, 1f);

	private static readonly RandomNumberGenerator Rng = new();

	private Tween _flashTween;
	private bool _busy;
	private bool _playingDead;
	private float _moveOverride;
	private float _attackCooldownTimer;

	static ZombieCrawler()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_attackCooldownTimer = Rng.RandfRange(0.1f, 0.4f);
		Stats.HitTaken += _ => OnHit();
		if (StartPlayingDead)
		{
			_playingDead = true;
			Sprite.Play("idle");
			Sprite.Pause();
			Sprite.Frame = 0;
		}
	}

	private void OnHit()
	{
		// Poking the "corpse" wakes it.
		_playingDead = false;
		if (!_busy)
			_ = Flinch();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;
		var player = PlayerReads.Find(this);
		if (_playingDead)
		{
			if (player is not null && Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X) <= AmbushDistance
				&& Mathf.Abs(player.GlobalPosition.Y - GlobalPosition.Y) <= 60f)
				_playingDead = false;
			else
			{
				Velocity = Vector2.Zero;
				return;
			}
		}
		_attackCooldownTimer -= (float)delta;
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval || player is null || !PlayerDetected || _busy || !IsOnFloor())
			return;
		float absDx = Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X);
		float absDy = Mathf.Abs(player.GlobalPosition.Y - GlobalPosition.Y);
		if (absDx <= BiteTrigger && absDy <= 40f && _attackCooldownTimer <= 0f)
			_ = AnkleBite();
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_busy)
			return _moveOverride == 0f || CanStepTo(Mathf.Sign(_moveOverride)) ? _moveOverride : 0f;
		return base.ComputeMoveX(player, distanceX, currentVelocityX, delta);
	}

	protected override bool CanTurnToFacePlayer => !_busy;

	// ── Ankle bite ───────────────────────────────────────────────────────────────────────────────

	private async Task AnkleBite()
	{
		if (!Begin())
			return;
		try
		{
			Sprite.Play("attack");
			Sprite.Pause();
			FlashBody(BiteWindup);
			for (int f = 0; f <= 4; f++)
			{
				Sprite.Frame = f;
				if (!await Wait(BiteWindup / 5f))
					return;
			}
			ClearFlash();
			_moveOverride = (FacingRight ? 1f : -1f) * SnapSpeed;
			bool hit = false;
			for (int f = 5; f <= 6; f++)
			{
				Sprite.Frame = f;
				if (!hit)
					hit = Strike();
				if (!await Wait(0.06f))
					return;
			}
			_moveOverride = 0f;
			for (int f = 7; f <= 10; f++)
			{
				Sprite.Frame = f;
				if (!await Wait(0.07f))
					return;
			}
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * Rng.RandfRange(0.85f, 1.25f);
	}

	private bool Strike()
	{
		Vector2 at = GlobalPosition + new Vector2(FacingRight ? BiteHitboxCenter.X : -BiteHitboxCenter.X, BiteHitboxCenter.Y);
		var query = new PhysicsShapeQueryParameters2D
		{
			Shape = new RectangleShape2D { Size = BiteHitboxSize },
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
			stats.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(Stats.AttackPower * BiteDamageMultiplier)), element: DamageElement.Poison);
			if (stats.CurrentHealth < before)
				BloodEffect.SpawnAt(this, player.GlobalPosition + new Vector2(0f, 20f));
			return true;
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
			await ToSignal(GetTree().CreateTimer(0.2f), SceneTreeTimer.SignalName.Timeout);
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

	protected override void OnDefeated()
	{
		ClearFlash();
		base.OnDefeated();
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null || _busy || _playingDead)
			return;
		string anim = Mathf.Abs(velocity.X) > 5f ? "walk" : "idle";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	protected override bool ContactDamageEnabled => false;
}
