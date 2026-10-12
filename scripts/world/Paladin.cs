using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Paladín / Paladin (128x128 cells, 20fps strips, faces right; the figure stands in the upper half
// of its cell, feet on the cell's centre line). A holy warrior with a great mace: slow, deliberate,
// and everything it does ends in light erupting out of the ground.
// Sheet beats used:
//   attack (41): 0-23 holy power gathering in the raised mace (the charge — played sped up), 24 the
//     mace swung up, 25 slammed into the ground, 25-31 a pillar of light bursting up in front, 32-40
//     recovery.
//   attack2 (63): 0-12 charging, 13 slam, 14-26 the light racing along the ground / crescents flying,
//     27-48 gathering again, 49-55 a row of light pillars, 56-62 recovery.
//   jump (13): the leap of the leaping smash.
// Moves:
//   Smite — the charge (flashing gold), then the slam: hurts up close and raises a pillar of light a
//     step in front of it. Being right next to it or well past the pillar is safe-ish.
//   Judgement — slam sends a golden wave racing along the floor (ElfSwordWave), then it gathers
//     power again and a row of light pillars marches out at you (SoulSpikeWave, tall and thin).
//   Leaping smash — from mid range it jumps at you and slams down where it lands.
//   Steadfast — never flinches; takes reduced damage from the front while charging.
// Telegraphs: body flashes gold while charging.
public partial class Paladin : Enemy
{
	[ExportGroup("Smite")]
	[Export] public float SmiteRange = 75f;
	[Export] public float AttackCooldown = 2f;
	[Export] public float SmiteCharge = 0.6f;
	[Export] public float SlamDamageMultiplier = 1.1f;
	[Export] public float PillarDamageMultiplier = 1.4f;
	// Measured off attack frames 25-29 (scale 1.8, sprite at 5.4,31): the mace hits the floor just in
	// front; the pillar rises ~61px in front, up to well above the head.
	[Export] public Vector2 SlamHitboxCenter = new(24f, 18f);
	[Export] public Vector2 SlamHitboxSize = new(40f, 30f);
	[Export] public Vector2 PillarHitboxCenter = new(61f, -18f);
	[Export] public Vector2 PillarHitboxSize = new(30f, 98f);

	[ExportGroup("Judgement")]
	[Export] public float JudgementMinDistance = 95f;
	[Export] public float JudgementMaxDistance = 340f;
	[Export] public float JudgementChance = 0.45f;
	[Export] public float JudgementCooldown = 6f;
	[Export] public float JudgementRecharge = 0.7f;
	[Export] public int PillarCount = 6;

	[ExportGroup("Leap")]
	[Export] public float LeapMinDistance = 90f;
	[Export] public float LeapMaxDistance = 200f;
	[Export] public float LeapChance = 0.3f;
	[Export] public float LeapCooldown = 5f;
	[Export] public float LeapVelocityY = -380f;

	[ExportGroup("Steadfast")]
	[Export] public int ChargeFrontArmor = 4;

	[ExportGroup("Look")]
	[Export] public Color HolyColor = new(1f, 0.85f, 0.4f, 1f);
	[Export] public Color FlashTint = new(2.4f, 2.1f, 1.2f, 1f);
	[Export] public float DecisionInterval = 0.4f;

	private const float Fps = 20f;

	private static readonly RandomNumberGenerator Rng = new();

	private Tween _flashTween;
	private bool _busy;
	private bool _charging;
	private bool _hopping;
	private int _baseDefense;
	private float _decisionTimer;
	private float _attackCooldownTimer;
	private float _judgementCooldownTimer;
	private float _leapCooldownTimer;

	static Paladin()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_baseDefense = Stats.Defense;
		_attackCooldownTimer = Rng.RandfRange(0.5f, 1f);
		_judgementCooldownTimer = Rng.RandfRange(1.5f, 3f);
		_leapCooldownTimer = Rng.RandfRange(2f, 3f);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;
		float dt = (float)delta;
		_decisionTimer -= dt;
		_attackCooldownTimer -= dt;
		_judgementCooldownTimer -= dt;
		_leapCooldownTimer -= dt;
		var player = PlayerReads.Find(this);
		// Braced while charging: frontal hits lose ChargeFrontArmor.
		bool front = player is not null && (player.GlobalPosition.X >= GlobalPosition.X) == FacingRight;
		Stats.Defense = _charging && front ? _baseDefense + ChargeFrontArmor : _baseDefense;

		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval)
			return;
		if (_hopping && IsOnFloor() && Velocity.Y >= 0f)
			_hopping = false;
		if (player is null || !PlayerDetected || _busy || _hopping || !IsOnFloor())
			return;

		float absDistance = Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X);
		if (absDistance <= SmiteRange && _attackCooldownTimer <= 0f)
		{
			_ = Smite();
			return;
		}
		if (_decisionTimer > 0f || _attackCooldownTimer > AttackCooldown * 0.85f)
			return;
		_decisionTimer = DecisionInterval * Rng.RandfRange(0.8f, 1.3f);
		if (absDistance >= JudgementMinDistance && absDistance <= JudgementMaxDistance && _judgementCooldownTimer <= 0f
			&& Rng.Randf() < JudgementChance)
			_ = Judgement();
		else if (absDistance >= LeapMinDistance && absDistance <= LeapMaxDistance && _leapCooldownTimer <= 0f
			&& player.IsOnFloor() && Rng.Randf() < LeapChance)
			_ = LeapSmash(player);
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_hopping)
			return currentVelocityX;
		if (_busy)
			return 0f;
		float toward = Mathf.Sign(distanceX);
		if (Mathf.Abs(distanceX) > SmiteRange * 0.8f && CanStepTo(toward))
			return toward * MoveSpeed;
		return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
	}

	protected override bool CanTurnToFacePlayer => !_busy && !_hopping;

	// ── Smite ───────────────────────────────────────────────────────────────────────────────

	private async Task Smite()
	{
		if (!Begin())
			return;
		try
		{
			if (!await Charge("attack", 0, 23, SmiteCharge))
				return;
			await SlamAndPillar(24);
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * Rng.RandfRange(0.85f, 1.2f);
	}

	// Plays `from..to` of `anim` squeezed into `duration`, flashing gold, braced.
	private async Task<bool> Charge(string anim, int from, int to, float duration)
	{
		_charging = true;
		Sprite.Play(anim);
		Sprite.Pause();
		FlashBody(duration);
		int frames = to - from + 1;
		for (int f = from; f <= to; f++)
		{
			Sprite.Frame = f;
			if (!await Wait(duration / frames))
			{
				_charging = false;
				return false;
			}
		}
		_charging = false;
		return true;
	}

	// attack frames 24..40: swing up, slam (24/25), pillar up front, recovery.
	private async Task SlamAndPillar(int swingFrame)
	{
		Sprite.Play("attack");
		Sprite.Pause();
		Sprite.Frame = swingFrame;
		if (!await Wait(0.08f))
			return;
		Sprite.Frame = swingFrame + 1;
		StrikeBox(SlamHitboxCenter, SlamHitboxSize, SlamDamageMultiplier, 240f);
		Shake(2.5f, 0.15f);
		bool pillarHit = false;
		for (int f = swingFrame + 2; f <= 40; f++)
		{
			Sprite.Frame = f;
			if (!pillarHit && f <= swingFrame + 6)
				pillarHit = StrikeBox(PillarHitboxCenter, PillarHitboxSize, PillarDamageMultiplier, 300f, launch: true);
			if (!await Wait(1f / Fps))
				return;
		}
	}

	// ── Judgement ───────────────────────────────────────────────────────────────────────────

	private async Task Judgement()
	{
		if (!Begin())
			return;
		_judgementCooldownTimer = JudgementCooldown;
		try
		{
			if (!await Charge("attack2", 0, 12, SmiteCharge))
				return;
			// Slam: the wave races along the floor.
			Sprite.Frame = 13;
			StrikeBox(SlamHitboxCenter, SlamHitboxSize, SlamDamageMultiplier, 240f);
			Shake(2f, 0.12f);
			float forward = FacingRight ? 1f : -1f;
			if (GetTree().CurrentScene is Node scene)
			{
				var wave = new ElfSwordWave { Direction = forward, ThrowerStats = Stats, GlowColor = HolyColor, DamageMultiplier = 0.8f };
				scene.AddChild(wave);
				wave.GlobalPosition = GlobalPosition + new Vector2(forward * 30f, BodyBottom);
			}
			for (int f = 14; f <= 26; f++)
			{
				Sprite.Frame = f;
				if (!await Wait(1f / Fps))
					return;
			}
			// Gathering again, then the row of pillars.
			if (!await Charge("attack2", 27, 48, JudgementRecharge))
				return;
			FacePlayer();
			SpawnPillars(FacingRight ? 1f : -1f);
			for (int f = 49; f <= 62; f++)
			{
				Sprite.Frame = f;
				if (!await Wait(1f / Fps))
					return;
			}
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * 0.7f;
	}

	private void SpawnPillars(float forward)
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		var pillars = new SoulSpikeWave
		{
			Direction = forward,
			ThrowerStats = Stats,
			DamageMultiplier = 1.1f,
			GlowColor = HolyColor,
			Count = PillarCount,
			Spacing = 44f,
			Interval = 0.11f,
			SpikeHeight = 96f,
			SpikeWidth = 12f,
		};
		scene.AddChild(pillars);
		pillars.GlobalPosition = GlobalPosition + new Vector2(forward * 20f, BodyBottom);
	}

	// ── Leaping smash ───────────────────────────────────────────────────────────────────────

	private async Task LeapSmash(Metroidvania.Player.Player player)
	{
		float airTime = 2f * -LeapVelocityY / Mathf.Max(1f, Gravity);
		float dx = player.GlobalPosition.X - GlobalPosition.X;
		var launch = new Vector2(Mathf.Clamp(dx / airTime, -260f, 260f), LeapVelocityY);
		if (!LandsSafely(launch, 40f) || !Begin())
			return;
		_leapCooldownTimer = LeapCooldown;
		try
		{
			FlashBody(0.25f);
			Sprite.Play("jump");
			if (!await Wait(0.15f))
				return;
			Velocity = launch;
			_hopping = true;
			float t = 0f;
			while (_hopping && t < airTime + 0.6f)
			{
				await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
				if (!IsInstanceValid(this) || IsQueuedForRemoval)
					return;
				t += (float)GetPhysicsProcessDeltaTime();
			}
			FacePlayer();
			await SlamAndPillar(24);
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * 0.7f;
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
		_charging = false;
		ClearFlash();
		if (HoldingAttackSlot)
		{
			EnemyCombatCoordinator.ReleaseAttackSlot();
			HoldingAttackSlot = false;
		}
	}

	private bool StrikeBox(Vector2 center, Vector2 size, float damageMultiplier, float knockback, bool launch = false)
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
				float side = Mathf.Sign(player.GlobalPosition.X - GlobalPosition.X);
				player.ApplyKnockback((launch ? new Vector2(side * 0.4f, -1f) : new Vector2(side, -0.3f)).Normalized(), knockback);
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

	private void Shake(float strength, float duration)
	{
		if (PlayerReads.Find(this) is { } player && player.GlobalPosition.DistanceTo(GlobalPosition) < 700f)
			player.ShakeCamera(strength, duration);
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
		Stats.Defense = _baseDefense;
		ClearFlash();
		base.OnDefeated();
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null || _busy || _hopping)
			return;
		string anim = !IsOnFloor() ? "jump" : Mathf.Abs(velocity.X) > 5f ? "walk" : "idle";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	protected override bool ContactDamageEnabled => false;
}
