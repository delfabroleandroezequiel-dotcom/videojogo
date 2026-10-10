using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Asesina/AssassinBandit (Bandits3 pack) — a knife-thrower skirmisher built on the ElfArcher
// "read the player and score the options" approach. The sheet faces right like the rest of the pack
// and has no jump, so all her evasion is on the ground: the Dodge row is a crouch, not a hop.
//   Spacing: holds a mid-range band, backs off (still facing you) when you close in, never off a
//   ledge. Leads its throws, and holds a throw while you dash or block, releasing as that ends.
//   Throws (all from the same Attack row: frames 1-4 draw the arm back — 4 is the held wind-up — and the
//     blade leaves on frame 5):
//     Single    — one spinning blade; sticks in walls, or in the floor as a 2 s caltrop.
//     Fan       — three at once, vs a jumping or far player and when single throws keep missing.
//     Boomerang — out, stops, comes back to her; the return hits from behind and ignores a block.
//     Rain      — blades lobbed high onto marked spots around a player who stands still / turtles.
//   Duck & counter: pressed point-blank (you swing or dash in), she ducks back under it (brief
//   i-frames) and answers with a low blade as she rises.
//   Smoke blink: cornered against a wall/ledge, she vanishes in smoke and reappears on your other
//   side, leaving neon afterimages, and throws straight away.
// Telegraphs: the body flashes white before Fan / Boomerang / Rain (same as Perrito1).
public partial class Bandido6 : Enemy
{
	[ExportGroup("Spacing")]
	[Export] public float PreferredMinDistance = 160f;
	[Export] public float PreferredMaxDistance = 300f;
	[Export] public float RetreatOvershoot = 45f;
	[Export] public float RetreatSpeedMultiplier = 1.05f;
	[Export] public float ThrowRange = 440f;

	[ExportGroup("Throw")]
	// Where the blade leaves her hand, facing right: just in front of the chest — measured off
	// Attack.png (front edge 71,48 of the 128x96 cell, sprite scale 1.75, position 2.6,-24.25).
	[Export] public Vector2 ReleaseOffset = new(18f, -20f);
	[Export] public float BladeSpeed = 430f;
	[Export] public Vector2 PlayerAimOffset = new(0f, -8f);
	[Export] public float LeadAccuracy = 0.8f;
	[Export] public float VerticalLeadFactor = 0.3f;
	[Export] public float MaxAimAngleDegrees = 55f;
	[Export] public float MinAimHold = 0.12f;
	[Export] public float MaxAimHold = 0.55f;
	[Export] public float PostDashRelease = 0.06f;
	[Export] public int WindupFrame = 4;
	[Export] public int ReleaseFrame = 5;
	[Export] public float ThrowCooldown = 1.1f;
	[Export] public Color FlashTint = new(2.2f, 2.2f, 2.2f, 1f);
	[Export] public Color BladeTrailColor = new(1f, 0.08f, 0.2f, 1f);

	[ExportGroup("Fan")]
	[Export] public float FanCooldown = 3.5f;
	[Export] public float FanSpreadDegrees = 13f;
	[Export] public float FanDamageMultiplier = 0.75f;
	[Export] public float FanTelegraph = 0.25f;

	[ExportGroup("Boomerang")]
	[Export] public float BoomerangCooldown = 4.5f;
	[Export] public float BoomerangOutDistance = 280f;
	[Export] public float BoomerangSpeed = 470f;
	[Export] public float BoomerangDamageMultiplier = 0.9f;
	[Export] public float BoomerangTelegraph = 0.3f;

	[ExportGroup("Rain")]
	[Export] public float RainCooldown = 6.5f;
	[Export] public int RainBlades = 5;
	[Export] public float RainSpread = 56f;
	[Export] public float RainFlightTime = 0.95f;
	[Export] public float RainGravity = 900f;
	[Export] public float RainDamageMultiplier = 0.7f;
	[Export] public float RainTelegraph = 0.45f;
	[Export] public float RainMinDistance = 110f;
	[Export] public float RainMaxDistance = 380f;
	// Player standing (nearly) still this long makes the rain likely.
	[Export] public float CampingTime = 0.9f;

	[ExportGroup("Duck & counter")]
	[Export] public float DuckTriggerDistance = 80f;
	[Export] public float DuckCooldown = 2.4f;
	[Export] public float DuckSlideSpeed = 240f;
	// Dodge row plays at 16fps: crouched on frames 2-6.
	[Export] public float DuckInvulnerableFrom = 2f / 16f;
	[Export] public float DuckInvulnerableTo = 7f / 16f;
	[Export] public Vector2 CounterReleaseOffset = new(16f, -4f);

	[ExportGroup("Smoke blink")]
	[Export] public float BlinkCooldown = 5f;
	[Export] public float BlinkTriggerDistance = 110f;
	[Export] public float BlinkDistance = 130f;
	[Export] public float BlinkHiddenTime = 0.2f;
	[Export] public Color SmokeColor = new(0.32f, 0.28f, 0.38f, 0.85f);
	[Export] public Color AfterimageColor = new(1f, 0.1f, 0.25f, 1f);

	[Export] public float HurtDuration = 0.3f;

	private enum Throw { Single, Fan, Boomerang, Rain }

	private static readonly RandomNumberGenerator Rng = new();

	private bool _busy;
	private bool _cancelThrow;
	private bool _uncancellable;
	private bool _retreating;
	private bool _wantsEvade;
	private float _hurtTimer;
	private float _throwCooldownTimer;
	private float _fanCooldownTimer;
	private float _boomerangCooldownTimer;
	private float _rainCooldownTimer;
	private float _duckCooldownTimer;
	private float _blinkCooldownTimer;
	private float _sincePlayerDashEnded = 999f;
	private bool _playerWasDashing;
	private float _playerStillTime;
	private float _missPressure;
	private float _slideVelocity;
	private Tween _flashTween;

	static Bandido6()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_throwCooldownTimer = Rng.RandfRange(0.4f, 1.1f);
		_rainCooldownTimer = Rng.RandfRange(2f, 4f);
		Stats.HitTaken += _ => OnHurt();
	}

	private void OnHurt()
	{
		_hurtTimer = HurtDuration;
		if (_busy && !_uncancellable)
			_cancelThrow = true;
		_wantsEvade = true;
	}

	private Metroidvania.Player.Player FindPlayer() => PlayerReads.Find(this);

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;

		float dt = (float)delta;
		_hurtTimer -= dt;
		_throwCooldownTimer -= dt;
		_fanCooldownTimer -= dt;
		_boomerangCooldownTimer -= dt;
		_rainCooldownTimer -= dt;
		_duckCooldownTimer -= dt;
		_blinkCooldownTimer -= dt;
		_sincePlayerDashEnded += dt;

		var player = FindPlayer();
		if (player is not null)
		{
			if (_playerWasDashing && !player.IsDashing)
				_sincePlayerDashEnded = 0f;
			_playerWasDashing = player.IsDashing;
			_playerStillTime = Mathf.Abs(player.Velocity.X) < 30f && player.IsOnFloor() ? _playerStillTime + dt : 0f;
		}

		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval || player is null || !PlayerDetected)
			return;

		if (!_busy && IsOnFloor())
		{
			if (TryReact(player))
				return;
			TryStartThrow(player);
		}
	}

	// ── Movement ────────────────────────────────────────────────────────────────────────────

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_busy)
		{
			_retreating = false;
			if (_slideVelocity != 0f)
			{
				_slideVelocity = Mathf.MoveToward(_slideVelocity, 0f, DuckSlideSpeed * 2.2f * (float)delta);
				return CanStepTo(Mathf.Sign(_slideVelocity)) ? _slideVelocity : 0f;
			}
			return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
		}
		if (_hurtTimer > 0f)
			return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
		return KitingMoveX(distanceX, currentVelocityX, PreferredMinDistance, PreferredMaxDistance, RetreatOvershoot,
			RetreatSpeedMultiplier, ref _retreating);
	}

	// Always faces the player — she backs off with her blades still pointed at you.
	protected override bool DesiredFacingRight(Node2D player, float distanceX) => distanceX >= 0;

	protected override bool CanTurnToFacePlayer => !_busy;

	// ── Reactions ───────────────────────────────────────────────────────────────────────────

	private bool TryReact(Metroidvania.Player.Player player)
	{
		float distanceX = player.GlobalPosition.X - GlobalPosition.X;
		float absDistance = Mathf.Abs(distanceX);
		float away = -Mathf.Sign(distanceX);
		bool pressing = player.IsAttacking || (player.IsDashing && PlayerReads.IsClosingIn(player, GlobalPosition.X, 0f)) || _wantsEvade;
		_wantsEvade = false;

		// Cornered → smoke blink to their other side.
		if (absDistance <= BlinkTriggerDistance && !CanStepTo(away) && _blinkCooldownTimer <= 0f)
			return StartBusy(Blink(player));

		// Pressed point-blank → duck back under it and counter.
		if (absDistance <= DuckTriggerDistance && pressing && _duckCooldownTimer <= 0f)
			return StartBusy(DuckAndCounter(player));

		return false;
	}

	private bool StartBusy(Task task)
	{
		_ = task;
		return true;
	}

	// ── Throw selection ─────────────────────────────────────────────────────────────────────

	private void TryStartThrow(Metroidvania.Player.Player player)
	{
		if (_hurtTimer > 0f || _throwCooldownTimer > 0f)
			return;
		float distanceX = player.GlobalPosition.X - GlobalPosition.X;
		float absDistance = Mathf.Abs(distanceX);
		if (absDistance > ThrowRange)
			return;
		// While backing off, only stop to throw once the gap is comfortable again.
		if (_retreating && absDistance < PreferredMinDistance)
			return;

		Throw kind = ChooseThrow(player, absDistance);
		_ = RunThrow(kind, 1f);
	}

	private Throw ChooseThrow(Metroidvania.Player.Player player, float absDistance)
	{
		bool airborne = !player.IsOnFloor();
		bool turtling = PlayerReads.IsBlockingToward(player, GlobalPosition.X);
		bool camping = _playerStillTime >= CampingTime;

		float single = 1f;
		float fan = _fanCooldownTimer > 0f ? 0f
			: 0.35f + (airborne ? 1.2f : 0f) + (absDistance > 230f ? 0.4f : 0f) + 0.35f * _missPressure;
		float boomerang = _boomerangCooldownTimer > 0f ? 0f
			: 0.3f + (turtling ? 1.6f : 0f) + (camping ? 0.3f : 0f);
		float rain = _rainCooldownTimer > 0f || absDistance < RainMinDistance || absDistance > RainMaxDistance ? 0f
			: 0.15f + (camping ? 1.2f : 0f) + (turtling ? 0.7f : 0f) + 0.5f * Mathf.Max(0f, _missPressure - 1f);

		float roll = Rng.Randf() * (single + fan + boomerang + rain);
		if ((roll -= rain) < 0f) return Throw.Rain;
		if ((roll -= boomerang) < 0f) return Throw.Boomerang;
		if ((roll -= fan) < 0f) return Throw.Fan;
		return Throw.Single;
	}

	// ── Throwing ────────────────────────────────────────────────────────────────────────────

	private async Task RunThrow(Throw kind, float holdScale)
	{
		if (!EnemyCombatCoordinator.TryAcquireAttackSlot())
		{
			_throwCooldownTimer = 0.25f;
			return;
		}
		HoldingAttackSlot = true;
		_busy = true;
		_cancelThrow = false;

		var player = FindPlayer();
		if (player is not null)
			FaceTowards(player.GlobalPosition.X);

		float fps = (float)Sprite.SpriteFrames.GetAnimationSpeed("attack");
		Sprite.Play("attack");
		Sprite.Pause();
		Sprite.Frame = WindupFrame;

		float telegraph = kind switch
		{
			Throw.Fan => FanTelegraph,
			Throw.Boomerang => BoomerangTelegraph,
			Throw.Rain => RainTelegraph,
			_ => 0f,
		};
		if (telegraph > 0f)
			FlashBody(telegraph);

		// Rain marks its landing spots during the wind-up.
		List<Vector2> rainTargets = null;
		List<Node2D> rainMarks = null;
		if (kind == Throw.Rain && player is not null)
		{
			rainTargets = PickRainTargets(player);
			rainMarks = new List<Node2D>();
			foreach (Vector2 target in rainTargets)
				rainMarks.Add(SpawnRainMark(target, telegraph + RainFlightTime));
		}

		// Aim: hold until a good moment (not while they dash or block toward her), within limits.
		float held = 0f;
		float minHold = Mathf.Max(MinAimHold * holdScale, telegraph);
		float maxHold = Mathf.Max(MaxAimHold * holdScale, telegraph);
		while (true)
		{
			if (!await NextFrame())
			{
				FreeMarks(rainMarks);
				return;
			}
			held += (float)GetPhysicsProcessDeltaTime();
			player = FindPlayer();
			if (player is null)
			{
				FreeMarks(rainMarks);
				EndThrow(0.3f);
				return;
			}
			if (kind != Throw.Rain)
				FaceTowards(player.GlobalPosition.X);
			if (held < minHold)
				continue;
			// Rain and boomerang go regardless — they're the answers to dodging/blocking.
			if (held >= maxHold || kind == Throw.Rain || kind == Throw.Boomerang || GoodMomentToRelease(player))
				break;
		}

		Sprite.Play("attack");
		await ToSignal(GetTree().CreateTimer(Mathf.Max(0, ReleaseFrame - WindupFrame) / fps), SceneTreeTimer.SignalName.Timeout);
		if (!StillValid())
		{
			FreeMarks(rainMarks);
			return;
		}

		player = FindPlayer();
		if (player is not null)
			Release(kind, player, rainTargets);
		Sfx.PlayAt(this, "Combat/Sword", "Sword Attack");

		int framesLeft = Sprite.SpriteFrames.GetFrameCount("attack") - ReleaseFrame;
		await ToSignal(GetTree().CreateTimer(Mathf.Max(0, framesLeft) / fps), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		switch (kind)
		{
			case Throw.Fan: _fanCooldownTimer = FanCooldown; break;
			case Throw.Boomerang: _boomerangCooldownTimer = BoomerangCooldown; break;
			case Throw.Rain: _rainCooldownTimer = RainCooldown; break;
		}
		EndThrow(ThrowCooldown * Rng.RandfRange(0.85f, 1.2f));
	}

	private void Release(Throw kind, Metroidvania.Player.Player player, List<Vector2> rainTargets)
	{
		Vector2 origin = HandPosition(ReleaseOffset);
		switch (kind)
		{
			case Throw.Single:
				SpawnBlade(origin, PredictAim(player, origin) * BladeSpeed, AssassinBlade.Mode.Straight, 1f);
				break;
			case Throw.Fan:
			{
				Vector2 aim = PredictAim(player, origin);
				for (int i = -1; i <= 1; i++)
					SpawnBlade(origin, aim.Rotated(Mathf.DegToRad(FanSpreadDegrees * i)) * BladeSpeed, AssassinBlade.Mode.Straight, FanDamageMultiplier, 3);
				break;
			}
			case Throw.Boomerang:
			{
				// Flat and straight at their chest — the return is the point.
				Vector2 aim = new(FacingRight ? 1f : -1f, 0f);
				AssassinBlade blade = SpawnBlade(origin, aim * BoomerangSpeed, AssassinBlade.Mode.Boomerang, BoomerangDamageMultiplier);
				blade.OutDistance = BoomerangOutDistance;
				break;
			}
			case Throw.Rain:
				if (rainTargets is null)
					break;
				for (int i = 0; i < rainTargets.Count; i++)
				{
					// Ballistic: reach the target in RainFlightTime (staggered a hair per blade).
					float t = RainFlightTime + i * 0.06f;
					Vector2 d = rainTargets[i] - origin;
					var velocity = new Vector2(d.X / t, (d.Y - 0.5f * RainGravity * t * t) / t);
					AssassinBlade blade = SpawnBlade(origin, velocity, AssassinBlade.Mode.Lob, RainDamageMultiplier, rainTargets.Count);
					blade.Gravity = RainGravity;
				}
				break;
		}
	}

	private AssassinBlade SpawnBlade(Vector2 origin, Vector2 velocity, AssassinBlade.Mode mode, float damageMultiplier, int bladesInThrow = 1)
	{
		var blade = new AssassinBlade
		{
			FlightMode = mode,
			Velocity = velocity,
			DamageMultiplier = damageMultiplier,
			Thrower = this,
			ThrowerStats = Stats,
			TrailColor = BladeTrailColor,
		};
		blade.Resolved += hit =>
		{
			if (!IsInstanceValid(this))
				return;
			_missPressure = hit ? 0f : _missPressure + 1f / bladesInThrow;
		};
		GetTree().CurrentScene.AddChild(blade);
		blade.GlobalPosition = origin;
		return blade;
	}

	private Vector2 HandPosition(Vector2 offset) =>
		GlobalPosition + new Vector2(FacingRight ? offset.X : -offset.X, offset.Y);

	// Leads the player (two refinement passes of distance / blade speed), clamped to a forward cone.
	private Vector2 PredictAim(Metroidvania.Player.Player player, Vector2 origin)
	{
		Vector2 direction = PlayerReads.PredictAimDirection(origin, player, BladeSpeed, LeadAccuracy, VerticalLeadFactor, PlayerAimOffset);
		float forward = FacingRight ? 1f : -1f;
		float angle = Mathf.Atan2(direction.Y, direction.X * forward);
		float limit = Mathf.DegToRad(MaxAimAngleDegrees);
		angle = Mathf.Clamp(angle, -limit, limit);
		return new Vector2(Mathf.Cos(angle) * forward, Mathf.Sin(angle));
	}

	private bool GoodMomentToRelease(Metroidvania.Player.Player player) =>
		!player.IsDashing && _sincePlayerDashEnded >= PostDashRelease && !PlayerReads.IsBlockingToward(player, GlobalPosition.X);

	// Landing spots around the player (and a bit ahead of where they're heading), snapped to the floor.
	private List<Vector2> PickRainTargets(Metroidvania.Player.Player player)
	{
		var targets = new List<Vector2>();
		float lead = Mathf.Clamp(player.Velocity.X * 0.4f, -60f, 60f);
		for (int i = 0; i < RainBlades; i++)
		{
			float offset = (i - (RainBlades - 1) * 0.5f) * RainSpread + Rng.RandfRange(-10f, 10f);
			float x = player.GlobalPosition.X + lead + offset;
			var from = new Vector2(x, player.GlobalPosition.Y - 40f);
			var query = PhysicsRayQueryParameters2D.Create(from, from + Vector2.Down * 260f,
				1u | Metroidvania.Shared.PhysicsLayers.ClimbableWalls | Metroidvania.Shared.PhysicsLayers.OneWayPlatforms);
			var hit = GetWorld2D().DirectSpaceState.IntersectRay(query);
			if (hit.Count > 0)
				targets.Add((Vector2)hit["position"]);
		}
		return targets;
	}

	// A small crimson X blinking on the floor where a rain blade will land.
	private Node2D SpawnRainMark(Vector2 ground, float lifetime)
	{
		var mark = new Node2D { ZIndex = 1 };
		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		mark.AddChild(new Line2D { Points = new[] { new Vector2(-7f, -5f), new Vector2(7f, 1f) }, Width = 2f, DefaultColor = BladeTrailColor, Material = additive });
		mark.AddChild(new Line2D { Points = new[] { new Vector2(-7f, 1f), new Vector2(7f, -5f) }, Width = 2f, DefaultColor = BladeTrailColor, Material = additive });
		GetTree().CurrentScene.AddChild(mark);
		mark.GlobalPosition = ground;

		Tween blink = mark.CreateTween().SetLoops();
		blink.TweenProperty(mark, "modulate:a", 0.25f, 0.12f);
		blink.TweenProperty(mark, "modulate:a", 1f, 0.12f);
		mark.GetTree().CreateTimer(lifetime + 0.1f).Timeout += () =>
		{
			if (IsInstanceValid(mark))
				mark.QueueFree();
		};
		return mark;
	}

	private static void FreeMarks(List<Node2D> marks)
	{
		if (marks is null)
			return;
		foreach (Node2D mark in marks)
		{
			if (IsInstanceValid(mark))
				mark.QueueFree();
		}
	}

	// ── Duck & counter ──────────────────────────────────────────────────────────────────────

	private async Task DuckAndCounter(Metroidvania.Player.Player player)
	{
		if (!EnemyCombatCoordinator.TryAcquireAttackSlot())
		{
			_duckCooldownTimer = 0.4f;
			return;
		}
		HoldingAttackSlot = true;
		_busy = true;
		_uncancellable = true;
		_duckCooldownTimer = DuckCooldown;
		FaceTowards(player.GlobalPosition.X);

		float away = FacingRight ? -1f : 1f;
		_slideVelocity = away * DuckSlideSpeed;
		Sprite.Play("dodge");

		try
		{
			await ToSignal(GetTree().CreateTimer(DuckInvulnerableFrom), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;
			Stats.ExternalInvulnerable = true;
			await ToSignal(GetTree().CreateTimer(DuckInvulnerableTo - DuckInvulnerableFrom), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;
			Stats.ExternalInvulnerable = false;
			_slideVelocity = 0f;

			// Rising out of the crouch: a quick low blade (attack row from the held wind-up frame).
			player = FindPlayer();
			if (player is not null)
			{
				FaceTowards(player.GlobalPosition.X);
				Sprite.Play("attack");
				Sprite.Frame = WindupFrame;
				await ToSignal(GetTree().CreateTimer(1f / 14f), SceneTreeTimer.SignalName.Timeout);
				if (!IsInstanceValid(this) || IsQueuedForRemoval)
					return;
				Vector2 origin = HandPosition(CounterReleaseOffset);
				SpawnBlade(origin, new Vector2(FacingRight ? 1f : -1f, 0f) * BladeSpeed * 1.1f, AssassinBlade.Mode.Straight, 1f);
				Sfx.PlayAt(this, "Combat/Sword", "Sword Attack");
				await ToSignal(GetTree().CreateTimer(0.3f), SceneTreeTimer.SignalName.Timeout);
				if (!IsInstanceValid(this) || IsQueuedForRemoval)
					return;
			}
		}
		finally
		{
			if (IsInstanceValid(this))
			{
				Stats.ExternalInvulnerable = false;
				_slideVelocity = 0f;
				_uncancellable = false;
			}
		}
		EndThrow(ThrowCooldown * 0.8f);
	}

	// ── Smoke blink ─────────────────────────────────────────────────────────────────────────

	private async Task Blink(Metroidvania.Player.Player player)
	{
		_busy = true;
		_uncancellable = true;
		_blinkCooldownTimer = BlinkCooldown;
		FlashBody(0.18f);

		try
		{
			await ToSignal(GetTree().CreateTimer(0.18f), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;

			Vector2 from = GlobalPosition;
			Vector2? landing = FindBlinkSpot(player);
			SmokeBurst(from);
			Visual.Visible = false;
			Stats.ExternalInvulnerable = true;
			await ToSignal(GetTree().CreateTimer(BlinkHiddenTime), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;

			if (landing is { } spot)
			{
				GlobalPosition = spot;
				Velocity = Vector2.Zero;
				Afterimages(from, spot);
			}
			SmokeBurst(GlobalPosition);
			Visual.Visible = true;
			Stats.ExternalInvulnerable = false;
			player = FindPlayer();
			if (player is not null)
				FaceTowards(player.GlobalPosition.X);
		}
		finally
		{
			if (IsInstanceValid(this))
			{
				Visual.Visible = true;
				Stats.ExternalInvulnerable = false;
				_uncancellable = false;
			}
		}

		// Straight into a quick throw from the new side.
		_busy = false;
		_throwCooldownTimer = 0f;
		_ = RunThrow(Throw.Single, 0.4f);
	}

	// The far side of the player (BlinkDistance past them), on floor and not inside a wall; a
	// shorter hop if that fails; null = stay put.
	private Vector2? FindBlinkSpot(Metroidvania.Player.Player player)
	{
		float side = Mathf.Sign(player.GlobalPosition.X - GlobalPosition.X);
		if (side == 0f)
			side = 1f;
		var space = GetWorld2D().DirectSpaceState;
		uint wallMask = 1u | Metroidvania.Shared.PhysicsLayers.ClimbableWalls;
		uint floorMask = wallMask | Metroidvania.Shared.PhysicsLayers.OneWayPlatforms;
		foreach (float distance in new[] { BlinkDistance, BlinkDistance * 0.6f })
		{
			float x = player.GlobalPosition.X + side * distance;
			var mid = new Vector2(player.GlobalPosition.X, player.GlobalPosition.Y - 10f);
			// No wall between the player and the spot.
			var wallQuery = PhysicsRayQueryParameters2D.Create(mid, new Vector2(x + side * BodyHalfWidth, mid.Y), wallMask,
				new Godot.Collections.Array<Rid> { GetRid() });
			if (space.IntersectRay(wallQuery).Count > 0)
				continue;
			var floorQuery = PhysicsRayQueryParameters2D.Create(new Vector2(x, mid.Y), new Vector2(x, mid.Y + 160f), floorMask,
				new Godot.Collections.Array<Rid> { GetRid() });
			var hit = space.IntersectRay(floorQuery);
			if (hit.Count == 0)
				continue;
			Vector2 floor = (Vector2)hit["position"];
			return new Vector2(x, floor.Y - BodyBottom - 1f);
		}
		return null;
	}

	private void SmokeBurst(Vector2 at)
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		var ramp = new Gradient();
		ramp.SetColor(0, SmokeColor);
		ramp.SetColor(1, SmokeColor with { A = 0f });
		var grow = new Curve();
		grow.AddPoint(new Vector2(0f, 0.4f));
		grow.AddPoint(new Vector2(1f, 1f));
		var smoke = new GpuParticles2D
		{
			Amount = 22,
			Lifetime = 0.7,
			OneShot = true,
			Explosiveness = 0.95f,
			Randomness = 0.5f,
			LocalCoords = false,
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			ProcessMaterial = new ParticleProcessMaterial
			{
				Direction = new Vector3(0f, -1f, 0f),
				Spread = 180f,
				InitialVelocityMin = 40f,
				InitialVelocityMax = 120f,
				Gravity = new Vector3(0f, -40f, 0f),
				DampingMin = 80f,
				DampingMax = 140f,
				ScaleMin = 0.35f,
				ScaleMax = 0.75f,
				ScaleCurve = new CurveTexture { Curve = grow },
				ColorRamp = new GradientTexture1D { Gradient = ramp },
				EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
				EmissionBoxExtents = new Vector3(14f, 26f, 0f),
			},
			ZIndex = 2,
		};
		scene.AddChild(smoke);
		smoke.GlobalPosition = at + new Vector2(0f, -6f);
		smoke.Emitting = true;
		smoke.GetTree().CreateTimer(1.2).Timeout += () =>
		{
			if (IsInstanceValid(smoke))
				smoke.QueueFree();
		};

		// A thin crimson ring snapping outward.
		const int segments = 24;
		var points = new Vector2[segments + 1];
		for (int i = 0; i <= segments; i++)
		{
			float a = Mathf.Tau * i / segments;
			points[i] = new Vector2(Mathf.Cos(a) * 30f, Mathf.Sin(a) * 30f);
		}
		var ring = new Line2D
		{
			Points = points,
			Width = 2f,
			DefaultColor = AfterimageColor,
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			Scale = Vector2.One * 0.3f,
			ZIndex = 2,
		};
		scene.AddChild(ring);
		ring.GlobalPosition = at + new Vector2(0f, -6f);
		Tween tween = ring.CreateTween();
		tween.TweenProperty(ring, "scale", Vector2.One, 0.22f).SetEase(Tween.EaseType.Out);
		tween.Parallel().TweenProperty(ring, "modulate:a", 0f, 0.25f);
		tween.TweenCallback(Callable.From(ring.QueueFree));
	}

	// Neon ghosts of her along the line she blinked through.
	private void Afterimages(Vector2 from, Vector2 to)
	{
		Texture2D frame = Sprite?.SpriteFrames?.GetFrameTexture(Sprite.Animation, Sprite.Frame);
		if (frame is null || GetTree().CurrentScene is not Node scene)
			return;
		const int ghosts = 4;
		for (int i = 0; i < ghosts; i++)
		{
			float t = (i + 1f) / (ghosts + 1f);
			var ghost = new Sprite2D
			{
				Texture = frame,
				Centered = Sprite.Centered,
				Offset = Sprite.Offset,
				FlipH = Sprite.FlipH,
				Material = Metroidvania.Player.NeonSilhouette.CreateMaterial(AfterimageColor, 1.6f),
				Modulate = new Color(1f, 1f, 1f, 0.6f * (1f - t * 0.5f)),
			};
			scene.AddChild(ghost);
			Transform2D xform = Sprite.GlobalTransform;
			xform.Origin += from.Lerp(to, t) - GlobalPosition;
			ghost.GlobalTransform = xform;
			Tween fade = ghost.CreateTween();
			fade.TweenProperty(ghost, "modulate:a", 0f, 0.3f + 0.05f * i);
			fade.TweenCallback(Callable.From(ghost.QueueFree));
		}
	}

	// ── Shared ──────────────────────────────────────────────────────────────────────────────

	private async Task<bool> NextFrame()
	{
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		return StillValid();
	}

	private bool StillValid()
	{
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return false;
		if (!_cancelThrow)
			return true;
		// Hit mid wind-up: the throw is dropped.
		EndThrow(ThrowCooldown * 0.6f);
		return false;
	}

	private void EndThrow(float cooldown)
	{
		_busy = false;
		_cancelThrow = false;
		_throwCooldownTimer = cooldown;
		ClearFlash();
		if (HoldingAttackSlot)
		{
			EnemyCombatCoordinator.ReleaseAttackSlot();
			HoldingAttackSlot = false;
		}
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

	// Killed mid-blink: make sure the body is visible for the death animation.
	protected override void OnDefeated()
	{
		Visual.Visible = true;
		Stats.ExternalInvulnerable = false;
		ClearFlash();
		base.OnDefeated();
	}

	// ── Animation ───────────────────────────────────────────────────────────────────────────

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null || _busy)
			return;
		string anim = _hurtTimer > 0f ? "hit" : Mathf.Abs(velocity.X) > 5f ? "run" : "idle";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	// Her offense is the blades; brushing against her shouldn't hurt.
	protected override bool ContactDamageEnabled => false;
}
