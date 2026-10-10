using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Ranger archer (red hood, teal tunic — the spr_Archer*_strip sheets) on the ElfArcher "read the
// player and score the options" approach, with its own kit built from its sheet:
//   Spacing: holds a band, backs off as you close in, never off a ledge. Leads its shots and holds
//   the drawn bow while you dash or block, releasing as that ends.
//   Shots (Attack row, 14fps: draw 0-4, held on 4, arrow leaves on 5):
//     Single   — fast arrow with a teal neon streak.
//     Triple   — 3-arrow spread vs a jumping / far player, or when single shots keep missing.
//     Homing   — slower arrow that follows you for ~2 s with a capped turn rate; a dash shakes it.
//     Sniper   — a thin laser sight tracks you, then freezes and turns white (your cue) before a
//                very fast arrow that pierces through.
//   Up close (Melee row, 18fps): short combo = sweeping cut (frames 3-4) + bow-thrust (5-8), or the
//   full combo that adds the flurry of cuts (14-21) and leaves it open afterwards (22-27).
//   Evasion: the Dash row is an in-place roll — it rolls backward under your swing (brief i-frames)
//   and fires a quick shot as it gets up; cornered, it vaults over you with the jump row (only if
//   the arc lands on ground).
// Telegraphs: the body flashes white before Homing, Sniper and the combos. Neon trails on every cut
// reach out to the hit's real reach.
public partial class RangerArcher : Enemy
{
	[ExportGroup("Spacing")]
	[Export] public float PreferredMinDistance = 170f;
	[Export] public float PreferredMaxDistance = 320f;
	[Export] public float RetreatOvershoot = 50f;
	[Export] public float RetreatSpeedMultiplier = 1.1f;
	[Export] public float ShootRange = 480f;

	[ExportGroup("Aim")]
	// Bow position on Attack frame 5 facing right (sheet bow ~75,55 of the 128 cell, sprite scale
	// 1.71, position 3.4,11.2).
	[Export] public Vector2 ArrowSpawnOffset = new(22f, -4f);
	[Export] public Vector2 PlayerAimOffset = new(0f, -8f);
	[Export] public float ArrowSpeed = 460f;
	[Export] public float LeadAccuracy = 0.85f;
	[Export] public float VerticalLeadFactor = 0.35f;
	[Export] public float MaxAimAngleDegrees = 60f;
	[Export] public float MinAimHold = 0.15f;
	[Export] public float MaxAimHold = 0.7f;
	[Export] public float PostDashRelease = 0.06f;
	[Export] public int DrawHoldFrame = 4;
	[Export] public int ReleaseFrame = 5;
	[Export] public float ShotCooldown = 1.1f;
	[Export] public Color ArrowColor = new(0.2f, 1f, 0.85f, 1f);
	[Export] public Color FlashTint = new(2.2f, 2.2f, 2.2f, 1f);

	[ExportGroup("Triple")]
	[Export] public float TripleCooldown = 3.5f;
	[Export] public float TripleSpreadDegrees = 13f;
	[Export] public float TripleDamageMultiplier = 0.8f;
	[Export] public float TripleTelegraph = 0.2f;

	[ExportGroup("Homing")]
	[Export] public float HomingCooldown = 5f;
	[Export] public float HomingSpeed = 270f;
	[Export] public float HomingDuration = 2.2f;
	[Export] public float HomingTurnRate = 150f;
	[Export] public float HomingTelegraph = 0.35f;
	[Export] public float HomingDamageMultiplier = 0.9f;

	[ExportGroup("Sniper")]
	[Export] public float SniperCooldown = 7f;
	[Export] public float SniperTrackTime = 0.75f;
	[Export] public float SniperLockTime = 0.25f;
	[Export] public float SniperSpeed = 950f;
	[Export] public float SniperDamageMultiplier = 1.4f;
	[Export] public float SniperMinDistance = 200f;

	[ExportGroup("Melee")]
	[Export] public float MeleeRange = 70f;
	[Export] public float MeleeCooldown = 2.2f;
	[Export] public float FullComboChance = 0.4f;
	[Export] public float MeleeTelegraph = 0.18f;
	// Hit boxes measured off the Melee row (scale 1.71): the sweep, the thrust, the flurry.
	[Export] public Vector2 SweepHitboxCenter = new(38f, -3f);
	[Export] public Vector2 SweepHitboxSize = new(55f, 49f);
	[Export] public Vector2 ThrustHitboxCenter = new(40f, -5f);
	[Export] public Vector2 ThrustHitboxSize = new(61f, 21f);
	[Export] public Vector2 FlurryHitboxCenter = new(44f, -5f);
	[Export] public Vector2 FlurryHitboxSize = new(63f, 55f);

	[ExportGroup("Evasion")]
	[Export] public float RollTriggerDistance = 85f;
	[Export] public float RollCooldown = 2.4f;
	[Export] public float RollSpeed = 250f;
	[Export] public float VaultSpeedX = 300f;
	[Export] public float VaultVelocityY = -420f;
	[Export] public float VaultCooldown = 3f;
	[Export] public float MaxSafeHopDrop = 40f;
	[Export] public float HurtDuration = 0.3f;

	private enum Shot { Single, Triple, Homing, Sniper }

	private const float MeleeFps = 18f;
	private const float RollFps = 18f;

	private static readonly RandomNumberGenerator Rng = new();

	private EnemySlashTrail _trail;
	private Metroidvania.Player.Hitbox _hitbox;
	private Tween _flashTween;
	private bool _busy;
	private bool _uncancellable;
	private bool _cancelShot;
	private bool _retreating;
	private bool _vaulting;
	private bool _wantsEvade;
	private float _rollVelocity;
	private float _hurtTimer;
	private float _shotCooldownTimer;
	private float _tripleCooldownTimer;
	private float _homingCooldownTimer;
	private float _sniperCooldownTimer;
	private float _meleeCooldownTimer;
	private float _rollCooldownTimer;
	private float _vaultCooldownTimer;
	private float _sincePlayerDashEnded = 999f;
	private bool _playerWasDashing;
	private float _missPressure;

	static RangerArcher()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_shotCooldownTimer = Rng.RandfRange(0.3f, 1f);
		_sniperCooldownTimer = Rng.RandfRange(2.5f, 4f);
		_hitbox = GetNode<Metroidvania.Player.Hitbox>("AttackHitbox");
		_trail = new EnemySlashTrail
		{
			CoreWidth = 2f,
			GlowWidth = 7f,
			GlowColor = ArrowColor,
			CoreColor = new Color(0.85f, 1f, 0.97f, 1f),
			MinPointSpacing = 1.5f,
			SparkAmount = 10,
		};
		Visual.AddChild(_trail);
		Stats.HitTaken += _ => OnHurt();
	}

	private void OnHurt()
	{
		_hurtTimer = HurtDuration;
		if (_busy && !_uncancellable)
			_cancelShot = true;
		_wantsEvade = true;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;
		float dt = (float)delta;
		_hurtTimer -= dt;
		_shotCooldownTimer -= dt;
		_tripleCooldownTimer -= dt;
		_homingCooldownTimer -= dt;
		_sniperCooldownTimer -= dt;
		_meleeCooldownTimer -= dt;
		_rollCooldownTimer -= dt;
		_vaultCooldownTimer -= dt;
		_sincePlayerDashEnded += dt;

		var player = PlayerReads.Find(this);
		if (player is not null)
		{
			if (_playerWasDashing && !player.IsDashing)
				_sincePlayerDashEnded = 0f;
			_playerWasDashing = player.IsDashing;
		}

		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval)
			return;
		if (_vaulting && IsOnFloor() && Velocity.Y >= 0f)
			_vaulting = false;

		if (player is null || !PlayerDetected || _busy || _vaulting || !IsOnFloor())
			return;
		if (TryReact(player))
			return;
		TryStartShot(player);
	}

	// ── Movement ────────────────────────────────────────────────────────────────────────────

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_vaulting)
			return currentVelocityX;
		if (_busy)
		{
			_retreating = false;
			if (_rollVelocity != 0f)
				return CanStepTo(Mathf.Sign(_rollVelocity)) ? _rollVelocity : 0f;
			return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
		}
		if (_hurtTimer > 0f)
			return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
		return KitingMoveX(distanceX, currentVelocityX, PreferredMinDistance, PreferredMaxDistance, RetreatOvershoot,
			RetreatSpeedMultiplier, ref _retreating);
	}

	// Runs away facing where it's going; otherwise faces the player, ready to draw.
	protected override bool DesiredFacingRight(Node2D player, float distanceX) =>
		_retreating ? distanceX < 0 : distanceX >= 0;

	protected override bool CanTurnToFacePlayer => !_busy && !_vaulting;

	// ── Reactions (melee / roll / vault) ────────────────────────────────────────────────────

	private bool TryReact(Metroidvania.Player.Player player)
	{
		float distanceX = player.GlobalPosition.X - GlobalPosition.X;
		float absDistance = Mathf.Abs(distanceX);
		float away = -Mathf.Sign(distanceX);
		bool pressing = player.IsAttacking || _wantsEvade
			|| (player.IsDashing && PlayerReads.IsClosingIn(player, GlobalPosition.X, 0f));
		_wantsEvade = false;
		bool cornered = absDistance <= RollTriggerDistance && !CanStepTo(away);

		// Cornered: vault over to the open side (only if it lands), else fight it out.
		if (cornered && _vaultCooldownTimer <= 0f)
		{
			var vault = new Vector2(-away * VaultSpeedX, VaultVelocityY);
			if (LandsSafely(vault, MaxSafeHopDrop))
			{
				Velocity = vault;
				_vaulting = true;
				_retreating = false;
				_vaultCooldownTimer = VaultCooldown;
				Sprite.Play("jump");
				return true;
			}
		}

		// Swung at / dashed into up close → roll back under it.
		if (absDistance <= RollTriggerDistance && pressing && !cornered && _rollCooldownTimer <= 0f)
		{
			_ = RollAndShoot(player, away);
			return true;
		}

		// Standing in its face → the bow combo.
		if (absDistance <= MeleeRange && _meleeCooldownTimer <= 0f)
		{
			_ = MeleeCombo(player, cornered || Rng.Randf() < FullComboChance);
			return true;
		}
		return false;
	}

	// ── Shot selection ──────────────────────────────────────────────────────────────────────

	private void TryStartShot(Metroidvania.Player.Player player)
	{
		if (_hurtTimer > 0f || _shotCooldownTimer > 0f)
			return;
		float distanceX = player.GlobalPosition.X - GlobalPosition.X;
		float absDistance = Mathf.Abs(distanceX);
		if (absDistance > ShootRange)
			return;
		if (_retreating && absDistance < PreferredMinDistance)
			return;
		_ = RunShot(ChooseShot(player, absDistance), 1f);
	}

	private Shot ChooseShot(Metroidvania.Player.Player player, float absDistance)
	{
		bool airborne = !player.IsOnFloor();
		bool turtling = PlayerReads.IsBlockingToward(player, GlobalPosition.X);

		float single = 1f;
		float triple = _tripleCooldownTimer > 0f ? 0f
			: 0.45f + (airborne ? 1.2f : 0f) + (absDistance > 230f ? 0.4f : 0f) + 0.35f * _missPressure;
		// The follower punishes a player who keeps dodging straight shots or hides behind a block.
		float homing = _homingCooldownTimer > 0f ? 0f
			: 0.6f + 0.45f * _missPressure + (turtling ? 0.6f : 0f) + (airborne ? 0.3f : 0f) + (absDistance > 250f ? 0.3f : 0f);
		float sniper = _sniperCooldownTimer > 0f || absDistance < SniperMinDistance ? 0f
			: 0.35f + (absDistance > 280f ? 0.6f : 0f) + (player.IsOnFloor() ? 0.2f : 0f);

		float roll = Rng.Randf() * (single + triple + homing + sniper);
		if ((roll -= sniper) < 0f) return Shot.Sniper;
		if ((roll -= homing) < 0f) return Shot.Homing;
		if ((roll -= triple) < 0f) return Shot.Triple;
		return Shot.Single;
	}

	// ── Shooting ────────────────────────────────────────────────────────────────────────────

	private async Task RunShot(Shot shot, float holdScale)
	{
		if (!EnemyCombatCoordinator.TryAcquireAttackSlot())
		{
			_shotCooldownTimer = 0.25f;
			return;
		}
		HoldingAttackSlot = true;
		_busy = true;
		_cancelShot = false;

		var player = PlayerReads.Find(this);
		if (player is not null)
			FaceTowards(player.GlobalPosition.X);

		float fps = (float)Sprite.SpriteFrames.GetAnimationSpeed("attack");
		Sprite.Play("attack");
		while (Sprite.Frame < DrawHoldFrame && Sprite.IsPlaying())
		{
			if (!await NextFrame())
				return;
		}
		Sprite.Pause();
		Sprite.Frame = DrawHoldFrame;

		Line2D laser = null;
		float telegraph = shot switch
		{
			Shot.Triple => TripleTelegraph,
			Shot.Homing => HomingTelegraph,
			_ => 0f,
		};
		if (telegraph > 0f)
			FlashBody(telegraph);

		Vector2 sniperDirection = Vector2.Right;
		if (shot == Shot.Sniper)
		{
			// Laser sight: tracks the player, then freezes and goes white just before the shot.
			FlashBody(SniperTrackTime + SniperLockTime);
			laser = MakeLaser();
			float t = 0f;
			while (t < SniperTrackTime + SniperLockTime)
			{
				if (!await NextFrame())
				{
					laser.QueueFree();
					return;
				}
				t += (float)GetPhysicsProcessDeltaTime();
				player = PlayerReads.Find(this);
				Vector2 origin = ArrowOrigin();
				if (t < SniperTrackTime && player is not null)
				{
					FaceTowards(player.GlobalPosition.X);
					sniperDirection = Aim(player, origin, SniperSpeed);
					laser.DefaultColor = ArrowColor with { A = 0.35f + 0.25f * Mathf.Sin(t * 40f) };
					laser.Width = 1.5f;
				}
				else
				{
					laser.DefaultColor = new Color(1f, 1f, 1f, 0.9f);
					laser.Width = 2.5f;
				}
				laser.Points = new[] { origin, origin + sniperDirection * 900f };
			}
			laser.QueueFree();
		}
		else
		{
			float held = 0f;
			float minHold = Mathf.Max(MinAimHold * holdScale, telegraph);
			float maxHold = Mathf.Max(MaxAimHold * holdScale, telegraph);
			while (true)
			{
				if (!await NextFrame())
					return;
				held += (float)GetPhysicsProcessDeltaTime();
				player = PlayerReads.Find(this);
				if (player is null)
				{
					EndAction(0.3f);
					return;
				}
				FaceTowards(player.GlobalPosition.X);
				if (held < minHold)
					continue;
				// The homing arrow doesn't need a clean window — it chases.
				if (held >= maxHold || shot == Shot.Homing || GoodMomentToRelease(player))
					break;
			}
		}

		Sprite.Play("attack");
		await ToSignal(GetTree().CreateTimer(Mathf.Max(0, ReleaseFrame - DrawHoldFrame) / fps), SceneTreeTimer.SignalName.Timeout);
		if (!StillValid())
			return;

		player = PlayerReads.Find(this);
		if (player is not null)
			Fire(shot, player, sniperDirection);
		Sfx.PlayAt(this, "Combat/Bow", "Bow Attack");

		int framesLeft = Sprite.SpriteFrames.GetFrameCount("attack") - ReleaseFrame;
		await ToSignal(GetTree().CreateTimer(Mathf.Max(0, framesLeft) / fps), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		switch (shot)
		{
			case Shot.Triple: _tripleCooldownTimer = TripleCooldown; break;
			case Shot.Homing: _homingCooldownTimer = HomingCooldown; break;
			case Shot.Sniper: _sniperCooldownTimer = SniperCooldown; break;
		}
		EndAction(ShotCooldown * Rng.RandfRange(0.85f, 1.2f));
	}

	private void Fire(Shot shot, Metroidvania.Player.Player player, Vector2 sniperDirection)
	{
		Vector2 origin = ArrowOrigin();
		switch (shot)
		{
			case Shot.Single:
				SpawnArrow(origin, Aim(player, origin, ArrowSpeed), ArrowSpeed, RangerArrow.Mode.Straight, 1f);
				break;
			case Shot.Triple:
			{
				Vector2 aim = Aim(player, origin, ArrowSpeed);
				for (int i = -1; i <= 1; i++)
					SpawnArrow(origin, aim.Rotated(Mathf.DegToRad(TripleSpreadDegrees * i)), ArrowSpeed, RangerArrow.Mode.Straight, TripleDamageMultiplier, 3);
				break;
			}
			case Shot.Homing:
			{
				// Launched a little upward so its curve toward the player is visible.
				Vector2 aim = Aim(player, origin, HomingSpeed).Rotated(Mathf.DegToRad(FacingRight ? -20f : 20f));
				RangerArrow arrow = SpawnArrow(origin, aim, HomingSpeed, RangerArrow.Mode.Homing, HomingDamageMultiplier);
				arrow.HomingDuration = HomingDuration;
				arrow.TurnRateDegrees = HomingTurnRate;
				arrow.Lifetime = HomingDuration + 1.2f;
				break;
			}
			case Shot.Sniper:
				SpawnArrow(origin, sniperDirection, SniperSpeed, RangerArrow.Mode.Piercing, SniperDamageMultiplier).Lifetime = 1.2f;
				break;
		}
	}

	private RangerArrow SpawnArrow(Vector2 origin, Vector2 direction, float speed, RangerArrow.Mode mode, float damageMultiplier, int arrowsInShot = 1)
	{
		var arrow = new RangerArrow
		{
			FlightMode = mode,
			Direction = direction,
			Speed = speed,
			DamageMultiplier = damageMultiplier,
			ShooterStats = Stats,
			GlowColor = ArrowColor,
		};
		arrow.Resolved += hit =>
		{
			if (!IsInstanceValid(this))
				return;
			_missPressure = hit ? 0f : _missPressure + 1f / arrowsInShot;
		};
		GetTree().CurrentScene.AddChild(arrow);
		arrow.GlobalPosition = origin;
		return arrow;
	}

	private Vector2 ArrowOrigin() =>
		GlobalPosition + new Vector2(FacingRight ? ArrowSpawnOffset.X : -ArrowSpawnOffset.X, ArrowSpawnOffset.Y);

	// Leads the player, clamped to a forward cone.
	private Vector2 Aim(Metroidvania.Player.Player player, Vector2 origin, float speed)
	{
		Vector2 direction = PlayerReads.PredictAimDirection(origin, player, speed, LeadAccuracy, VerticalLeadFactor, PlayerAimOffset);
		float forward = FacingRight ? 1f : -1f;
		float angle = Mathf.Atan2(direction.Y, direction.X * forward);
		float limit = Mathf.DegToRad(MaxAimAngleDegrees);
		angle = Mathf.Clamp(angle, -limit, limit);
		return new Vector2(Mathf.Cos(angle) * forward, Mathf.Sin(angle));
	}

	private bool GoodMomentToRelease(Metroidvania.Player.Player player) =>
		!player.IsDashing && _sincePlayerDashEnded >= PostDashRelease && !PlayerReads.IsBlockingToward(player, GlobalPosition.X);

	private Line2D MakeLaser()
	{
		var laser = new Line2D
		{
			TopLevel = true,
			Width = 1.5f,
			DefaultColor = ArrowColor with { A = 0.4f },
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			ZIndex = 2,
		};
		AddChild(laser);
		return laser;
	}

	// ── Melee combo ─────────────────────────────────────────────────────────────────────────

	private async Task MeleeCombo(Metroidvania.Player.Player player, bool full)
	{
		if (!EnemyCombatCoordinator.TryAcquireAttackSlot())
		{
			_meleeCooldownTimer = 0.4f;
			return;
		}
		HoldingAttackSlot = true;
		_busy = true;
		_uncancellable = true;
		_meleeCooldownTimer = MeleeCooldown;
		FaceTowards(player.GlobalPosition.X);

		try
		{
			// Wind-up held on frame 2 while the body flashes.
			Sprite.Play("melee");
			Sprite.Pause();
			Sprite.Frame = 2;
			FlashBody(MeleeTelegraph);
			if (!await Wait(MeleeTelegraph))
				return;
			Sprite.Play("melee");

			// Sweep (frames 3-4): a cut from overhead down the front.
			if (!await WaitFrame(3))
				return;
			_trail.Play(new Vector2(4f, -5f), 63f, -100f, 40f, 0.1f);
			Strike(SweepHitboxCenter, SweepHitboxSize);
			if (!await WaitFrame(5))
				return;

			// Thrust (frames 5-8): the bow driven straight forward.
			_trail.PlayThrust(new Vector2(11f, -5f), new Vector2(72f, -5f), 0.06f, 0.12f);
			Strike(ThrustHitboxCenter, ThrustHitboxSize);
			if (!await WaitFrame(9))
				return;
			_hitbox.Deactivate();

			if (full)
			{
				// Flurry (frames 14-21): four quick alternating cuts.
				if (!await WaitFrame(14))
					return;
				for (int cut = 0; cut < 4; cut++)
				{
					bool down = cut % 2 == 0;
					_trail.Play(new Vector2(8f, -5f), 66f, down ? -60f : 60f, down ? 60f : -60f, 0.08f);
					Strike(FlurryHitboxCenter, FlurryHitboxSize);
					if (!await WaitFrame(16 + cut * 2))
						return;
					_hitbox.Deactivate();
				}
				// Left open on the recovery frames (22-27).
				if (!await WaitFrame(27))
					return;
			}
			else
			{
				// Short version: straight into the recovery frames.
				Sprite.Frame = 22;
				if (!await Wait(5f / MeleeFps))
					return;
			}
		}
		finally
		{
			if (IsInstanceValid(this))
			{
				_hitbox.Deactivate();
				_uncancellable = false;
			}
		}
		EndAction(ShotCooldown * 0.7f);
	}

	private void Strike(Vector2 center, Vector2 size)
	{
		_hitbox.Deactivate();
		// A fresh shape each time — never resize a shared shape resource in place.
		var shape = _hitbox.GetNode<CollisionShape2D>("CollisionShape2D");
		shape.Shape = new RectangleShape2D { Size = size };
		_hitbox.Position = new Vector2(FacingRight ? center.X : -center.X, center.Y);
		_hitbox.Activate(Stats);
	}

	private async Task<bool> WaitFrame(int frame)
	{
		while (Sprite.Animation == "melee" && Sprite.Frame < frame && Sprite.IsPlaying())
		{
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return false;
		}
		return true;
	}

	private async Task<bool> Wait(float seconds)
	{
		await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
		return IsInstanceValid(this) && !IsQueuedForRemoval;
	}

	// ── Roll ────────────────────────────────────────────────────────────────────────────────

	private async Task RollAndShoot(Metroidvania.Player.Player player, float away)
	{
		if (!EnemyCombatCoordinator.TryAcquireAttackSlot())
		{
			_rollCooldownTimer = 0.4f;
			return;
		}
		HoldingAttackSlot = true;
		_busy = true;
		_uncancellable = true;
		_rollCooldownTimer = RollCooldown;
		FaceTowards(player.GlobalPosition.X);
		Sprite.Play("dash");
		_rollVelocity = away * RollSpeed;

		try
		{
			// Rolling under it: frames 2-7 are the low part, invulnerable.
			if (!await Wait(2f / RollFps))
				return;
			Stats.ExternalInvulnerable = true;
			if (!await Wait(6f / RollFps))
				return;
			Stats.ExternalInvulnerable = false;
			_rollVelocity = 0f;
			if (!await Wait(2f / RollFps))
				return;
		}
		finally
		{
			if (IsInstanceValid(this))
			{
				Stats.ExternalInvulnerable = false;
				_rollVelocity = 0f;
				_uncancellable = false;
			}
		}

		// Up and straight into a quick shot.
		_busy = false;
		if (HoldingAttackSlot)
		{
			EnemyCombatCoordinator.ReleaseAttackSlot();
			HoldingAttackSlot = false;
		}
		_shotCooldownTimer = 0f;
		_ = RunShot(Shot.Single, 0.35f);
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
		if (!_cancelShot)
			return true;
		// Hit mid-draw: the shot is dropped.
		EndAction(ShotCooldown * 0.6f);
		return false;
	}

	private void EndAction(float cooldown)
	{
		_busy = false;
		_cancelShot = false;
		_shotCooldownTimer = cooldown;
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

	protected override void OnDefeated()
	{
		Stats.ExternalInvulnerable = false;
		ClearFlash();
		_hitbox?.Deactivate();
		base.OnDefeated();
	}

	// ── Animation ───────────────────────────────────────────────────────────────────────────

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null || _busy)
			return;
		string anim;
		if (!IsOnFloor())
			anim = "jump";
		else if (_hurtTimer > 0f)
			anim = "idle";
		else
			anim = Mathf.Abs(velocity.X) > 5f ? "run" : "idle";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	// Its offense is the bow; brushing against it shouldn't hurt.
	protected override bool ContactDamageEnabled => false;
}
