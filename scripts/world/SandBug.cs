using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Bicho de arena / Sand Bug (Desert enemy pack, 160x64 cells, faces LEFT — mirrored on the scene).
// A desert larva: it lies half-buried in the sand, springs on whoever walks by and latches on, and
// curls into an armoured ball — which it can also roll at you.
// Sheet beats used:
//   digging_idle: half-buried, waiting. move: crawling.
//   attack_jump_start: rearing up, jaws open (the wind-up). attack_jump_static: mid-leap.
//   catch: upright, latched onto its prey. catch_endd: dropping back down.
//   miss: a missed leap — it ends up standing upright, back turned, stuck for a moment.
//   takeguard: 1 curling up, 2-11 a rolled-up armoured ball, 12-15 uncurling.
//   takeblock: hit while curled.
// Moves:
//   Ambush — waits buried (barely visible); when you come near it leaps out at you.
//   Latch — a leap that connects grabs on: it rides you, chomping a few times. Dash/roll, or turn
//     around and hit it, to shake it off.
//   Miss — a leap that misses leaves it stuck upright and wide open for a moment.
//   Curl — a swing coming at it head-on can make it curl up: hits bounce off the ball.
//   Roll — from mid range it curls up and rolls at you, bouncing off walls.
// Telegraphs: body flashes white before the leap and the roll.
public partial class SandBug : Enemy
{
	[ExportGroup("Ambush")]
	[Export] public float AmbushRange = 110f;
	[Export] public float BuriedAlpha = 0.55f;

	[ExportGroup("Leap & latch")]
	[Export] public float LeapRange = 130f;
	[Export] public float LeapCooldown = 2.2f;
	[Export] public float LeapTelegraph = 0.3f;
	[Export] public float LeapVelocityY = -260f;
	[Export] public float LeapDamageMultiplier = 1f;
	[Export] public int LatchBites = 3;
	[Export] public float LatchBiteInterval = 0.35f;
	[Export] public float BiteDamageMultiplier = 0.5f;
	[Export] public float MissStunTime = 0.9f;

	[ExportGroup("Curl & roll")]
	[Export] public float CurlChance = 0.5f;
	[Export] public float CurlTime = 1f;
	[Export] public float CurlCooldown = 3f;
	[Export] public float RollMinDistance = 140f;
	[Export] public float RollMaxDistance = 300f;
	[Export] public float RollChance = 0.35f;
	[Export] public float RollCooldown = 5f;
	[Export] public float RollSpeed = 260f;
	[Export] public float RollTime = 1.6f;
	[Export] public float RollDamageMultiplier = 0.9f;

	[ExportGroup("Look")]
	[Export] public Color FlashTint = new(2.2f, 2.2f, 2.2f, 1f);

	private const float Fps = 12f;

	private static readonly RandomNumberGenerator Rng = new();
	private readonly WhiffWatcher _whiff = new();

	private Tween _flashTween;
	private bool _buried = true;
	private bool _busy;
	private bool _curled;
	private bool _leaping;
	private bool _latched;
	private bool _shakeOff;
	private float _moveOverride;
	private float _leapCooldownTimer;
	private float _curlCooldownTimer;
	private float _rollCooldownTimer;

	static SandBug()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_rollCooldownTimer = Rng.RandfRange(1.5f, 3f);
		Sprite.Play("digging_idle");
		Sprite.Modulate = new Color(1f, 1f, 1f, BuriedAlpha);
		Stats.IncomingHitInterceptor = TryBounce;
		Stats.HitTaken += _ => OnHit();
		Stats.HitTaken += _ => _whiff.NotifyHitTaken();
	}

	// Curled up: hits bounce off the shell.
	private bool TryBounce()
	{
		if (!_curled)
			return false;
		ImpactEffect.SpawnAt(this, GlobalPosition + new Vector2(0f, -8f));
		Sfx.PlayAt(this, "Combat/Sword", "Sword Blocked");
		if (!_busy || Sprite.Animation == "takeguard")
			Sprite.Play("takeblock");
		return true;
	}

	private void OnHit()
	{
		if (_latched)
			_shakeOff = true;
		if (_buried)
			Surface();
	}

	private void Surface()
	{
		_buried = false;
		CreateTween().TweenProperty(Sprite, "modulate:a", 1f, 0.2f);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;
		float dt = (float)delta;
		_leapCooldownTimer -= dt;
		_curlCooldownTimer -= dt;
		_rollCooldownTimer -= dt;
		var player = PlayerReads.Find(this);
		_whiff.Update(player, dt);

		if (_latched)
			return; // Riding the player: Latch() moves it.
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval || player is null || _busy || _leaping || !IsOnFloor())
			return;

		float distanceX = player.GlobalPosition.X - GlobalPosition.X;
		float absDistance = Mathf.Abs(distanceX);
		if (_buried)
		{
			if (absDistance <= AmbushRange && Mathf.Abs(player.GlobalPosition.Y - GlobalPosition.Y) < 80f)
			{
				Surface();
				_ = Leap(player, 0.12f);
			}
			return;
		}
		if (!PlayerDetected)
			return;

		bool frontal = (player.GlobalPosition.X >= GlobalPosition.X) == FacingRight;
		if (_whiff.PlayerStartedSwing && frontal && absDistance <= 90f && _curlCooldownTimer <= 0f && Rng.Randf() < CurlChance)
		{
			_ = Curl();
			return;
		}
		if (absDistance <= LeapRange && absDistance >= 36f && _leapCooldownTimer <= 0f && player.IsOnFloor())
		{
			_ = Leap(player, LeapTelegraph);
			return;
		}
		if (absDistance >= RollMinDistance && absDistance <= RollMaxDistance && _rollCooldownTimer <= 0f && Rng.Randf() < RollChance * 0.05f)
			_ = Roll(Mathf.Sign(distanceX));
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_leaping)
			return currentVelocityX;
		if (_buried)
			return 0f;
		if (_busy)
			return _moveOverride;
		float toward = Mathf.Sign(distanceX);
		if (Mathf.Abs(distanceX) > 60f && CanStepTo(toward))
			return toward * MoveSpeed;
		return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
	}

	protected override bool CanTurnToFacePlayer => !_busy && !_leaping && !_buried;

	// ── Leap & latch ────────────────────────────────────────────────────────────────────────

	private async Task Leap(Metroidvania.Player.Player player, float telegraph)
	{
		if (!Begin())
			return;
		_leapCooldownTimer = LeapCooldown;
		bool latched = false;
		try
		{
			FacePlayer();
			Sprite.Play("attack_jump_start");
			FlashBody(telegraph);
			if (!await Wait(Mathf.Max(telegraph, 5f / Fps)))
				return;

			float airTime = 2f * -LeapVelocityY / Mathf.Max(1f, Gravity);
			float dx = player.GlobalPosition.X - GlobalPosition.X;
			Velocity = new Vector2(Mathf.Clamp(dx / airTime, -260f, 260f), LeapVelocityY);
			_leaping = true;
			Sprite.Play("attack_jump_static");
			float t = 0f;
			while (t < airTime + 0.5f)
			{
				await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
				if (!Alive())
					return;
				t += (float)GetPhysicsProcessDeltaTime();
				if (TouchesPlayer(out var victim))
				{
					latched = true;
					_leaping = false;
					await Latch(victim);
					return;
				}
				if (t > 0.1f && IsOnFloor())
					break;
			}
			_leaping = false;

			// Missed: stuck upright for a moment, open.
			Sprite.Play("miss");
			if (!await Wait(MissStunTime))
				return;
		}
		finally
		{
			if (IsInstanceValid(this))
			{
				_leaping = false;
				if (!latched)
					Finish();
			}
		}
	}

	private async Task Latch(Metroidvania.Player.Player player)
	{
		_latched = true;
		_shakeOff = false;
		var playerStats = player.GetNodeOrNull<Stats>("Stats");
		if (playerStats is not null && !playerStats.IsInvulnerable)
			playerStats.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(Stats.AttackPower * LeapDamageMultiplier)));
		Sprite.Play("catch");
		try
		{
			int bites = 0;
			float biteTimer = LatchBiteInterval;
			float t = 0f;
			while (bites < LatchBites)
			{
				await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
				if (!Alive() || !IsInstanceValid(player))
					return;
				float dt = (float)GetPhysicsProcessDeltaTime();
				t += dt;
				// Rides the player's back.
				GlobalPosition = player.GlobalPosition + new Vector2(player.IsFacingRight ? -10f : 10f, -6f);
				FaceTowards(player.GlobalPosition.X + (player.IsFacingRight ? 1f : -1f));
				if (_shakeOff || player.IsDashing || player.IsRolling)
					break;
				biteTimer -= dt;
				if (biteTimer <= 0f)
				{
					biteTimer = LatchBiteInterval;
					bites++;
					// Small chomps that don't grant i-frames (like a damage-over-time tick).
					playerStats?.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(Stats.AttackPower * BiteDamageMultiplier)),
						ignoreInvulnerability: true, armInvulnerability: false);
					BloodEffect.SpawnAt(this, player.GlobalPosition);
				}
			}
			// Drops off behind the player.
			Sprite.Play("catch_endd");
			Velocity = new Vector2(player.IsFacingRight ? -120f : 120f, -120f);
		}
		finally
		{
			if (IsInstanceValid(this))
			{
				_latched = false;
			}
		}
		// Scuttles back off before it can leap again.
		_leapCooldownTimer = LeapCooldown * 1.6f;
		_moveOverride = Mathf.Sign(GlobalPosition.X - player.GlobalPosition.X) * MoveSpeed * 2f;
		if (!await Wait(6f / Fps))
			return;
		Sprite.Play("move");
		if (!await Wait(0.35f))
			return;
		_moveOverride = 0f;
		Finish();
	}

	private bool TouchesPlayer(out Metroidvania.Player.Player player)
	{
		player = null;
		var query = new PhysicsShapeQueryParameters2D
		{
			Shape = new RectangleShape2D { Size = new Vector2(46f, 40f) },
			Transform = new Transform2D(0f, GlobalPosition + new Vector2(0f, -8f)),
			CollisionMask = 2,
			CollideWithBodies = true,
		};
		foreach (var hit in GetWorld2D().DirectSpaceState.IntersectShape(query, 8))
		{
			if (hit["collider"].AsGodotObject() is Metroidvania.Player.Player p && p.GetNodeOrNull<Stats>("Stats") is { IsInvulnerable: false })
			{
				// A raised guard stops the leap cold (no latch).
				if (PlayerReads.IsBlockingToward(p, GlobalPosition.X))
					return false;
				player = p;
				return true;
			}
		}
		return false;
	}

	// ── Curl & roll ─────────────────────────────────────────────────────────────────────────

	private async Task Curl()
	{
		_busy = true;
		_curled = true;
		_curlCooldownTimer = CurlCooldown;
		try
		{
			Sprite.Play("takeguard");
			Sprite.Pause();
			Sprite.Frame = 1;
			if (!await Wait(1f / Fps))
				return;
			float t = 0f;
			int frame = 2;
			while (t < CurlTime)
			{
				if (Sprite.Animation == "takeguard")
				{
					Sprite.Frame = frame;
					frame = frame >= 11 ? 2 : frame + 1;
				}
				else if (!Sprite.IsPlaying())
				{
					Sprite.Play("takeguard");
					Sprite.Pause();
				}
				if (!await Wait(1f / Fps))
					return;
				t += 1f / Fps;
			}
			_curled = false;
			Sprite.Play("takeguard");
			Sprite.Pause();
			for (int f = 12; f <= 15; f++)
			{
				Sprite.Frame = f;
				if (!await Wait(1f / Fps))
					return;
			}
		}
		finally
		{
			if (IsInstanceValid(this))
			{
				_curled = false;
				_busy = false;
			}
		}
	}

	private async Task Roll(float direction)
	{
		if (!Begin())
			return;
		_rollCooldownTimer = RollCooldown;
		_curled = true;
		try
		{
			FaceTowards(GlobalPosition.X + direction);
			Sprite.Play("takeguard");
			Sprite.Pause();
			Sprite.Frame = 1;
			FlashBody(0.35f);
			if (!await Wait(0.35f))
				return;
			bool hit = false;
			float t = 0f;
			int frame = 2;
			float frameTimer = 0f;
			int bounces = 0;
			_moveOverride = direction * RollSpeed;
			while (t < RollTime)
			{
				await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
				if (!Alive())
					return;
				float dt = (float)GetPhysicsProcessDeltaTime();
				t += dt;
				frameTimer += dt;
				if (frameTimer >= 1f / 18f)
				{
					frameTimer = 0f;
					frame = frame >= 11 ? 2 : frame + 1;
					Sprite.Frame = frame;
				}
				if (!hit && TouchesPlayer(out var victim))
				{
					hit = true;
					var stats = victim.GetNodeOrNull<Stats>("Stats");
					stats?.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(Stats.AttackPower * RollDamageMultiplier)));
					victim.ApplyKnockback(new Vector2(Mathf.Sign(_moveOverride), -0.5f).Normalized(), 300f);
				}
				// Bounces off walls (twice at most) and stops at ledges.
				float sign = Mathf.Sign(_moveOverride);
				if (TestMove(GlobalTransform, new Vector2(sign * 6f, 0f)))
				{
					if (++bounces > 2)
						break;
					_moveOverride = -_moveOverride;
					ImpactEffect.SpawnAt(this, GlobalPosition + new Vector2(sign * 20f, 0f));
				}
				else if (!HasGroundAhead(sign))
					break;
			}
			_moveOverride = 0f;
			_curled = false;
			for (int f = 12; f <= 15; f++)
			{
				Sprite.Frame = f;
				if (!await Wait(1f / Fps))
					return;
			}
		}
		finally
		{
			if (IsInstanceValid(this))
			{
				_moveOverride = 0f;
				_curled = false;
			}
			Finish();
		}
	}

	// ── Shared ──────────────────────────────────────────────────────────────────────────────

	private bool Alive() => IsInstanceValid(this) && !IsQueuedForRemoval;

	private bool Begin()
	{
		if (!EnemyCombatCoordinator.TryAcquireAttackSlot())
		{
			_leapCooldownTimer = Mathf.Max(_leapCooldownTimer, 0.3f);
			return false;
		}
		HoldingAttackSlot = true;
		_busy = true;
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
		return Alive();
	}

	private void FacePlayer()
	{
		if (PlayerReads.Find(this) is { } player)
			FaceTowards(player.GlobalPosition.X);
	}

	private void FaceTowards(float targetX)
	{
		FacingRight = targetX >= GlobalPosition.X;
		Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);
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
		_latched = false;
		if (Sprite is not null)
			Sprite.Modulate = Colors.White;
		base.OnDefeated();
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null || _busy || _leaping || _buried)
			return;
		string anim = Mathf.Abs(velocity.X) > 5f ? "move" : "digging_idle";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	protected override bool ContactDamageEnabled => false;
}
