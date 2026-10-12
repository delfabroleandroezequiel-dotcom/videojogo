using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Archidemonio / Arch Demon (DuskBorne, 128x128 cells, faces right). A haloed demon sorceress who
// never touches the ground: she floats at a distance and pelts you with dark orbs, and blinks away
// when you get close.
// Sheet beats used:
//   attack (20): 3-6 a dark orb gathering in her hand (the charge), 7 the cast. The sheet's attack
//     strip is two cells wide and got split frame by frame, so the even frames 8-16 are only the
//     projectile flying off on its own — those are skipped (7, 9, 11, ... are the body frames) and
//     the orb is our own NecroBolt.
//   idle: hovering (she has no walk clip — she drifts). hurt / death (fades upward).
// Moves:
//   Dark orbs — charges, then fires 1-3 slow orbs that bend toward you (the volley grows as she's
//     hurt).
//   Hover — floats ~HoverHeight above the floor, keeping MinDistance..MaxDistance away, over gaps
//     too. Walls and floors still stop her.
//   Blink — get too close and she fades out and reappears further away on the other side.
// Telegraphs: body flashes violet while charging; the fade before a blink.
public partial class ArchDemon : Enemy
{
	[ExportGroup("Hover")]
	[Export] public float HoverHeight = 50f;
	[Export] public float MinDistance = 150f;
	[Export] public float MaxDistance = 260f;
	[Export] public float DriftAcceleration = 240f;
	[Export] public float BobAmplitude = 4f;

	[ExportGroup("Dark orbs")]
	[Export] public float OrbRange = 380f;
	[Export] public float AttackCooldown = 2.2f;
	[Export] public float ChargeTime = 0.6f;
	[Export] public float OrbDamageMultiplier = 0.9f;
	[Export] public float OrbSpeed = 180f;
	[Export] public float VolleyInterval = 0.25f;
	// Her casting hand at frame 7 (scale 1, sprite at 2,14.5).
	[Export] public Vector2 HandOffset = new(26f, -2f);

	[ExportGroup("Blink")]
	[Export] public float BlinkTrigger = 80f;
	[Export] public float BlinkCooldown = 3f;
	[Export] public float BlinkDistance = 200f;
	[Export] public float FadeTime = 0.3f;

	[ExportGroup("Look")]
	[Export] public Color OrbColor = new(0.6f, 0.2f, 0.9f, 1f);
	[Export] public Color FlashTint = new(1.8f, 1.3f, 2.4f, 1f);

	private static readonly RandomNumberGenerator Rng = new();

	private Tween _flashTween;
	private bool _busy;
	private bool _blinking;
	private float _time;
	private float _attackCooldownTimer;
	private float _blinkCooldownTimer;

	static ArchDemon()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_time = Rng.Randf() * Mathf.Tau;
		_attackCooldownTimer = Rng.RandfRange(0.6f, 1.2f);
		Stats.IncomingHitInterceptor = () => _blinking;
	}

	// Flies (replaces Enemy's walk-and-gravity loop).
	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;
		float dt = (float)delta;
		_time += dt;
		_attackCooldownTimer -= dt;
		_blinkCooldownTimer -= dt;
		var player = PlayerReads.Find(this);
		PlayerDetected = player is not null && GlobalPosition.DistanceTo(player.GlobalPosition) <= DetectionRange;

		if (_busy)
		{
			Velocity = Velocity.MoveToward(Vector2.Zero, DriftAcceleration * dt);
			MoveAndSlide();
			return;
		}

		Vector2 desired = Vector2.Zero;
		if (PlayerDetected)
		{
			FacingRight = player.GlobalPosition.X >= GlobalPosition.X;
			Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);
			float dx = GlobalPosition.X - player.GlobalPosition.X;
			float side = Mathf.Sign(dx == 0f ? 1f : dx);
			float absDx = Mathf.Abs(dx);
			float targetX = absDx < MinDistance ? player.GlobalPosition.X + side * MinDistance
				: absDx > MaxDistance ? player.GlobalPosition.X + side * MaxDistance
				: GlobalPosition.X;
			float targetY = player.GlobalPosition.Y - HoverHeight + Mathf.Sin(_time * 2f) * BobAmplitude * 4f;
			Vector2 to = new(targetX - GlobalPosition.X, targetY - GlobalPosition.Y);
			if (to.Length() > 6f)
				desired = to.Normalized() * MoveSpeed;
		}
		Velocity = Velocity.MoveToward(desired, DriftAcceleration * dt);
		MoveAndSlide();
		if (Sprite.Animation != "idle")
			Sprite.Play("idle");

		if (!PlayerDetected)
			return;
		float distance = Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X);
		if (distance <= BlinkTrigger && _blinkCooldownTimer <= 0f)
			_ = Blink(player);
		else if (_attackCooldownTimer <= 0f && GlobalPosition.DistanceTo(player.GlobalPosition) <= OrbRange)
			_ = DarkOrbs();
	}

	// ── Dark orbs ───────────────────────────────────────────────────────────────────────────

	private async Task DarkOrbs()
	{
		if (!EnemyCombatCoordinator.TryAcquireAttackSlot())
		{
			_attackCooldownTimer = 0.3f;
			return;
		}
		HoldingAttackSlot = true;
		_busy = true;
		try
		{
			int count = 1 + (Stats.CurrentHealth < Stats.MaxHealth * 0.66f ? 1 : 0) + (Stats.CurrentHealth < Stats.MaxHealth * 0.33f ? 1 : 0);
			Sprite.Play("attack");
			Sprite.Pause();
			FlashBody(ChargeTime);
			for (int f = 0; f <= 6; f++)
			{
				Sprite.Frame = f;
				if (!await Wait(ChargeTime / 7f))
					return;
			}
			for (int i = 0; i < count; i++)
			{
				Sprite.Frame = 7 + i * 2;
				FireOrb();
				if (!await Wait(VolleyInterval))
					return;
			}
			for (int f = 17; f <= 19; f++)
			{
				Sprite.Frame = f;
				if (!await Wait(0.08f))
					return;
			}
		}
		finally
		{
			if (IsInstanceValid(this))
			{
				_busy = false;
				ClearFlash();
				if (HoldingAttackSlot)
				{
					EnemyCombatCoordinator.ReleaseAttackSlot();
					HoldingAttackSlot = false;
				}
			}
		}
		_attackCooldownTimer = AttackCooldown * Rng.RandfRange(0.85f, 1.25f);
	}

	private void FireOrb()
	{
		if (GetTree().CurrentScene is not Node scene || PlayerReads.Find(this) is not { } player)
			return;
		Vector2 origin = GlobalPosition + new Vector2(FacingRight ? HandOffset.X : -HandOffset.X, HandOffset.Y);
		var orb = new NecroBolt
		{
			Direction = (player.GlobalPosition - origin).Normalized(),
			ThrowerStats = Stats,
			DamageMultiplier = OrbDamageMultiplier,
			GlowColor = OrbColor,
			Speed = OrbSpeed,
			TurnRateDegrees = 70f,
			PoisonDuration = 0f,
		};
		scene.AddChild(orb);
		orb.GlobalPosition = origin;
	}

	// ── Blink ───────────────────────────────────────────────────────────────────────────────

	private async Task Blink(Metroidvania.Player.Player player)
	{
		_busy = true;
		_blinking = true;
		_blinkCooldownTimer = BlinkCooldown;
		try
		{
			Tween fadeOut = CreateTween();
			fadeOut.TweenProperty(Sprite, "modulate:a", 0f, FadeTime);
			if (!await Wait(FadeTime + 0.15f))
				return;
			// Reappear on the far side of the player, at hover height, if there's room; else straight back.
			float side = GlobalPosition.X <= player.GlobalPosition.X ? 1f : -1f;
			Vector2 spot = new(player.GlobalPosition.X + side * BlinkDistance, player.GlobalPosition.Y - HoverHeight);
			var probe = GlobalTransform;
			probe.Origin = spot;
			if (TestMove(probe, Vector2.Zero))
			{
				spot = new Vector2(player.GlobalPosition.X - side * BlinkDistance, player.GlobalPosition.Y - HoverHeight);
				probe.Origin = spot;
			}
			if (!TestMove(probe, Vector2.Zero))
				GlobalPosition = spot;
			Velocity = Vector2.Zero;
			Tween fadeIn = CreateTween();
			fadeIn.TweenProperty(Sprite, "modulate:a", 1f, FadeTime);
			if (!await Wait(FadeTime))
				return;
		}
		finally
		{
			if (IsInstanceValid(this))
			{
				_blinking = false;
				_busy = false;
				Sprite.Modulate = Colors.White;
			}
		}
		_attackCooldownTimer = Mathf.Min(_attackCooldownTimer, 0.4f);
	}

	// ── Shared ──────────────────────────────────────────────────────────────────────────────

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

	// Floating: a shove just pushes her drift.
	public override void ApplyKnockback(Vector2 direction, float force)
	{
		if (!_busy)
			Velocity += direction * force * 0.4f;
	}

	protected override void OnDefeated()
	{
		ClearFlash();
		if (Sprite is not null)
			Sprite.Modulate = Colors.White;
		base.OnDefeated();
	}

	protected override bool ContactDamageEnabled => false;
}
