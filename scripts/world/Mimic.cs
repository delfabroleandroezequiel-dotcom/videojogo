using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Mímico / Mimic (Monsters Creatures Fantasy 2, 146x146 cells, faces right). A treasure chest that
// isn't one: it sits shut until you come close, then sprouts teeth, a tongue and legs.
// Sheet beats used:
//   idle_closed: an ordinary chest. opening (6) → transform (7): waking up. idle_transformed: awake.
//   attack_1 (14): 3-12 rearing up, maw gaping (the wind-up), 13 lunging forward and chomping.
//   attack_2 (13): 4-7 the tongue whipped back behind it, 8-10 lashed far out in front, 11-12 reeled
//     back in.
//   walk: hopping along.
// Moves:
//   Disguise — a plain chest (no health bars, harmless) until you step within WakeRange or hit it.
//     Hitting it wakes it straight into a punishing chomp.
//   Chomp — rears up (flashing) and lunges into a heavy bite.
//   Tongue lash — from mid range the tongue shoots out ~100px; a hit yanks you in toward its mouth,
//     often straight into a chomp.
//   Back to sleep — leave it alone long enough and it shuts and pretends to be a chest again.
//   Hoard — drops a pile of coins when it dies (its profile's loot table).
// Telegraphs: body flashes white before each attack.
public partial class Mimic : Enemy
{
	[ExportGroup("Disguise")]
	[Export] public float WakeRange = 60f;
	[Export] public float SleepAfter = 4f;
	[Export] public float SleepDistance = 320f;

	[ExportGroup("Chomp")]
	[Export] public float ChompRange = 60f;
	[Export] public float AttackCooldown = 1.4f;
	[Export] public float ChompTelegraph = 0.45f;
	[Export] public float ChompLunge = 40f;
	[Export] public float ChompDamageMultiplier = 1.4f;
	[Export] public float ChompKnockback = 260f;
	// attack_1 frame 13 (scale 1.5, sprite at 0,0): the jaws reach ~63px in front, low.
	[Export] public Vector2 ChompHitboxCenter = new(40f, -4f);
	[Export] public Vector2 ChompHitboxSize = new(46f, 38f);

	[ExportGroup("Tongue")]
	[Export] public float TongueMinDistance = 50f;
	[Export] public float TongueMaxDistance = 115f;
	[Export] public float TongueCooldown = 3f;
	[Export] public float TongueDamageMultiplier = 0.6f;
	[Export] public float TongueYank = 320f;
	// attack_2 frames 8-10: the tongue lashes out ~106px, at mouth height.
	[Export] public Vector2 TongueHitboxCenter = new(64f, -3f);
	[Export] public Vector2 TongueHitboxSize = new(84f, 22f);

	[ExportGroup("Look")]
	[Export] public Color FlashTint = new(2.2f, 2.2f, 2.2f, 1f);

	private const float Fps = 10f;

	private static readonly RandomNumberGenerator Rng = new();

	private Tween _flashTween;
	private bool _disguised = true;
	private bool _busy;
	private bool _wokenByHit;
	private float _moveOverride;
	private float _attackCooldownTimer;
	private float _tongueCooldownTimer;
	private float _lonelyTimer;

	static Mimic()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		Sprite.Play("idle_closed");
		Stats.HitTaken += _ => OnHit();
	}

	private void OnHit()
	{
		if (_disguised && !_busy)
		{
			_wokenByHit = true;
			_ = WakeUp();
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;
		float dt = (float)delta;
		_attackCooldownTimer -= dt;
		_tongueCooldownTimer -= dt;
		var player = PlayerReads.Find(this);
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval || player is null || _busy || !IsOnFloor())
			return;

		float distanceX = player.GlobalPosition.X - GlobalPosition.X;
		float absDistance = Mathf.Abs(distanceX);
		if (_disguised)
		{
			if (absDistance <= WakeRange && Mathf.Abs(player.GlobalPosition.Y - GlobalPosition.Y) < 60f)
				_ = WakeUp();
			return;
		}

		// Left alone for a while → back to being a chest.
		_lonelyTimer = absDistance > SleepDistance ? _lonelyTimer + dt : 0f;
		if (_lonelyTimer >= SleepAfter)
		{
			_ = GoToSleep();
			return;
		}
		if (!PlayerDetected)
			return;

		if (absDistance <= ChompRange && _attackCooldownTimer <= 0f)
			_ = Chomp(ChompTelegraph);
		else if (absDistance >= TongueMinDistance && absDistance <= TongueMaxDistance && _tongueCooldownTimer <= 0f
			&& _attackCooldownTimer <= AttackCooldown * 0.85f)
			_ = TongueLash();
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_disguised)
			return 0f;
		if (_busy)
			return _moveOverride == 0f || CanStepTo(Mathf.Sign(_moveOverride)) ? _moveOverride : 0f;
		float toward = Mathf.Sign(distanceX);
		if (Mathf.Abs(distanceX) > ChompRange * 0.8f && CanStepTo(toward))
			return toward * MoveSpeed;
		return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
	}

	protected override bool CanTurnToFacePlayer => !_busy && !_disguised;

	// ── Disguise ────────────────────────────────────────────────────────────────────────────

	private async Task WakeUp()
	{
		_busy = true;
		try
		{
			FacePlayer();
			Sprite.Play("opening");
			if (!await Wait(6f / Fps))
				return;
			Sprite.Play("transform");
			if (!await Wait(7f / Fps))
				return;
			_disguised = false;
			_lonelyTimer = 0f;
			Sprite.Play("idle_transformed");
		}
		finally
		{
			if (IsInstanceValid(this))
				_busy = false;
		}
		// Hit while pretending → snaps straight back.
		if (_wokenByHit)
		{
			_wokenByHit = false;
			_attackCooldownTimer = 0f;
			_ = Chomp(0.15f);
		}
		else
			_attackCooldownTimer = 0.4f;
	}

	private async Task GoToSleep()
	{
		_busy = true;
		try
		{
			Sprite.PlayBackwards("transform");
			if (!await Wait(7f / Fps))
				return;
			Sprite.PlayBackwards("opening");
			if (!await Wait(6f / Fps))
				return;
			_disguised = true;
			_lonelyTimer = 0f;
			Sprite.Play("idle_closed");
		}
		finally
		{
			if (IsInstanceValid(this))
				_busy = false;
		}
	}

	// ── Chomp ───────────────────────────────────────────────────────────────────────────────

	private async Task Chomp(float telegraph)
	{
		if (!Begin())
			return;
		try
		{
			Sprite.Play("attack_1");
			Sprite.Pause();
			for (int f = 1; f <= 3; f++)
			{
				Sprite.Frame = f;
				if (!await Wait(0.05f))
					return;
			}
			// Maw gaping, held for the telegraph (frames 4-12 cycle a little).
			FlashBody(telegraph);
			float t = 0f;
			int frame = 4;
			while (t < telegraph)
			{
				Sprite.Frame = frame;
				frame = frame >= 12 ? 8 : frame + 1;
				if (!await Wait(0.05f))
					return;
				t += 0.05f;
			}
			Sprite.Frame = 13;
			_moveOverride = (FacingRight ? 1f : -1f) * ChompLunge / 0.1f;
			bool hit = Strike(ChompHitboxCenter, ChompHitboxSize, ChompDamageMultiplier, ChompKnockback, yank: false);
			if (!await Wait(0.1f))
				return;
			_moveOverride = 0f;
			if (!hit)
				Strike(ChompHitboxCenter, ChompHitboxSize, ChompDamageMultiplier, ChompKnockback, yank: false);
			if (!await Wait(0.35f))
				return;
		}
		finally
		{
			if (IsInstanceValid(this))
				_moveOverride = 0f;
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * Rng.RandfRange(0.85f, 1.2f);
	}

	// ── Tongue lash ─────────────────────────────────────────────────────────────────────────

	private async Task TongueLash()
	{
		if (!Begin())
			return;
		_tongueCooldownTimer = TongueCooldown;
		bool yanked = false;
		try
		{
			Sprite.Play("attack_2");
			Sprite.Pause();
			FlashBody(0.4f);
			for (int f = 1; f <= 7; f++)
			{
				Sprite.Frame = f;
				if (!await Wait(f >= 4 ? 0.08f : 0.05f))
					return;
			}
			for (int f = 8; f <= 12; f++)
			{
				Sprite.Frame = f;
				if (f <= 10 && !yanked)
					yanked = Strike(TongueHitboxCenter, TongueHitboxSize, TongueDamageMultiplier, TongueYank, yank: true);
				if (!await Wait(1f / Fps))
					return;
			}
		}
		finally
		{
			Finish();
		}
		// Reeled them in: straight into a quick chomp.
		if (yanked)
		{
			_attackCooldownTimer = 0f;
			_ = Chomp(0.2f);
		}
		else
			_attackCooldownTimer = AttackCooldown * 0.6f;
	}

	// One damage check. yank: pulls the player toward the mimic instead of shoving them away.
	private bool Strike(Vector2 center, Vector2 size, float damageMultiplier, float knockback, bool yank)
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
			stats.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(Stats.AttackPower * damageMultiplier)));
			if (stats.CurrentHealth < before)
			{
				float away = Mathf.Sign(player.GlobalPosition.X - GlobalPosition.X);
				player.ApplyKnockback(new Vector2(yank ? -away : away, -0.25f).Normalized(), knockback);
				BloodEffect.SpawnAt(this, player.GlobalPosition);
				return true;
			}
			return false;
		}
		return false;
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
		base.OnDefeated();
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null || _busy || _disguised)
			return;
		string anim = Mathf.Abs(velocity.X) > 5f ? "walk" : "idle_transformed";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	protected override bool ContactDamageEnabled => false;
}
