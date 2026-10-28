using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Guerrero rojo / Red Warrior (170x96 cells, faces right). A berserker with a broadsword: relentless
// pressure, a whirlwind, and a roar that sends him into a frenzy.
// Sheet beats used:
//   attack (30): 0-4 the sword drawn back, 5-9 a low slash out front, 11-13 pulled back up,
//     14 the hammer coming down, 15-18 it hitting the ground, 23-25 a flourish, 26-29 recovery.
//   dash (8): a slide with the weapons lowered — the figure itself travels ~59px across the cell
//     (root motion). Used as a dodge, not an attack.
//   spin_attack (30): 4-8 winding up, 9-23 a spinning whirlwind that drifts ~34px forward
//     (root motion), 24-29 recovery.
//   taunt (18): 7-15 arms raised in a roar.
// Root motion: for dash and spin_attack the sprite's Offset cancels the drawn drift frame by frame
// while the body moves by the same amount, so he really travels and nothing pops back afterwards.
// Moves:
//   Slash + slam — low slash, then (often) a hammer slam into the ground (rock burst, harder hit).
//   Dodge — when the player starts a swing close by, turns and slides away untouchable (neon ghosts).
//   Whirlwind — spins forward, hitting on both sides several times. Hard to approach; back off.
//   Frenzy — below FrenzyHealthRatio he roars (taunt) once: faster and harder-hitting from then on.
//   Punish — a whiffed swing near him gets a quick low slash.
// Telegraphs: body flashes white before each attack; crimson neon trails.
public partial class RedWarrior : Enemy
{
	[ExportGroup("Double slash")]
	[Export] public float SlashRange = 80f;
	[Export] public float AttackCooldown = 1.3f;
	[Export] public float SlashTelegraph = 0.3f;
	[Export] public float SecondChance = 0.6f;
	[Export] public float LowDamageMultiplier = 1f;
	[Export] public float HighDamageMultiplier = 1.25f;

	[ExportGroup("Hammer slam")]
	[Export] public float SlamImpactScale = 0.8f;
	[Export] public float SlamShake = 3f;

	[ExportGroup("Dodge")]
	// The dash sheet is a slide with the weapons lowered, not a thrust: he turns his back and slides
	// away when the player starts a swing close to him, untouchable for the slide.
	[Export] public float DodgeTriggerRange = 120f;
	[Export] public float DodgeChance = 0.35f;
	[Export] public float DodgeCooldown = 2.5f;
	[Export] public float DodgeFrameTime = 0.045f;
	[Export] public Color DodgeGhostColor = new(0.15f, 0.65f, 1f, 1f);
	[Export] public float DodgeGhostIntensity = 1.8f;
	[Export] public float DodgeGhostAlpha = 0.85f;
	[Export] public float DodgeGhostLifetime = 0.22f;

	[ExportGroup("Whirlwind")]
	[Export] public float WhirlRange = 90f;
	[Export] public float WhirlChance = 0.25f;
	[Export] public float WhirlCooldown = 6f;
	[Export] public float WhirlHitInterval = 0.2f;
	[Export] public float WhirlDamageMultiplier = 0.8f;
	[Export] public Vector2 WhirlHitboxCenter = new(0f, -4f);
	[Export] public Vector2 WhirlHitboxSize = new(112f, 60f);

	[ExportGroup("Frenzy")]
	[Export] public float FrenzyHealthRatio = 0.5f;
	[Export] public float FrenzySpeedScale = 1.3f;
	[Export] public float FrenzyDamageBonus = 0.25f;

	[ExportGroup("Look")]
	// Camera shake on every swing he throws (slash, whirlwind start); 0 = none.
	[Export] public float SwingShake = 0f;
	[Export] public float RoarShake = 3f;
	[Export] public float RoarShakeDuration = 0.3f;
	[Export] public Color TrailGlowColor = new(1f, 0.1f, 0.12f, 1f);
	[Export] public Color FlashTint = new(2.2f, 2.2f, 2.2f, 1f);
	[Export] public float DecisionInterval = 0.35f;

	// Drawn drift of the figure per frame (cell px, forward), measured off the sheet.
	private static readonly float[] DashDrift = { 0f, 5f, 17f, 17f, 17f, 17f, 59f, 59f };
	private static readonly float[] SpinDrift =
	{
		0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 4f, 6f, 9f, 11f, 13f, 15f, 17f, 19f, 21f, 23f, 26f, 28f, 30f,
		32f, 33f, 34f, 34f, 34f, 34f, 34f,
	};
	private const float SpriteScale = 1.8f;

	// Hit windows measured off the sheet (alpha bbox per 170x96 cell; idle body spans x 63-97, feet at
	// y 75): the frontmost drawn pixel (blade tip) and the top of the drawing on each active frame.
	// Damage is only checked on these frames, against a box reaching exactly that far, so it never
	// lands on the wind-up. Lunge reaches already have the drawn drift subtracted (root motion).
	private static readonly float[] LowSlashReach = { 126f, 119f, 118f, 118f };   // attack 5-8
	private static readonly float[] LowSlashTop = { 46f, 44f, 45f, 45f };
	private static readonly float[] HighSlashReach = { 125f, 128f, 130f, 131f };  // attack 15-18
	private static readonly float[] HighSlashTop = { 35f, 35f, 47f, 47f };
	// Hammer head touches the ground on attack frame 15 at cell x ~111 (light pixels at feet level).
	private const float SlamImpactCellX = 111f;
	private const float CellCenterX = 85f;
	private const float CellCenterY = 48f;
	private const float CellFeetY = 75f;
	private const float StrikeBackEdge = 7f;
	private static readonly Vector2 SpriteOffset = new(9f, -19.4f);

	protected enum Move { Slash, SecondSlash, Punish, Dodge, Whirlwind, Frenzy }

	protected static readonly GroundSlamFx.Palette SlamPalette = new()
	{
		Hot = new Color(1f, 0.6f, 0.2f, 1f),
		Core = new Color(1f, 0.95f, 0.8f, 1f),
		Rock = new Color(0.4f, 0.37f, 0.34f, 1f),
		Dust = new Color(0.58f, 0.53f, 0.48f, 0.6f),
		Light = new Color(1f, 0.7f, 0.4f, 1f),
	};

	// Hooks for scripted variants (ArthurEntrenamiento): gate moves and slow/soften the kit.
	protected virtual bool MoveAllowed(Move move) => true;
	protected virtual float TelegraphScale => 1f;
	protected virtual float CooldownScale => 1f;
	protected virtual float DamageScale => 1f;
	protected virtual float SpeedBonus => 1f;
	// Third hit after the slam: the big overhead arc (attack 23-25) the base kit skips.
	protected virtual bool OverheadFinisher => false;
	// Called every decision tick before the default choices; true = handled (a smarter brain).
	protected virtual bool Decide(Metroidvania.Player.Player player, float distanceX, float absDistance) => false;
	// Keep the wind-up pose (attack frame 4) while this is true, up to MaxTelegraphHold — the archer's
	// "hold the drawn bow" read, for a slash.
	protected virtual bool HoldTelegraph() => false;
	protected virtual float MaxTelegraphHold => 0f;
	protected bool IsBusy => _busy;
	protected WhiffWatcher Whiff => _whiff;
	protected EnemySlashTrail Trail => _trail;
	protected static RandomNumberGenerator Random => Rng;
	protected float AttackCooldownTimer { get => _attackCooldownTimer; set => _attackCooldownTimer = value; }
	protected float DodgeCooldownTimer { get => _dodgeCooldownTimer; set => _dodgeCooldownTimer = value; }
	protected float WhirlCooldownTimer { get => _whirlCooldownTimer; set => _whirlCooldownTimer = value; }

	private static readonly RandomNumberGenerator Rng = new();
	private readonly WhiffWatcher _whiff = new();

	private EnemySlashTrail _trail;
	private Tween _flashTween;
	private bool _busy;
	private bool _frenzied;
	private float _baseMoveSpeed;
	private float _decisionTimer;
	private float _attackCooldownTimer;
	private float _dodgeCooldownTimer;
	private float _whirlCooldownTimer;
	private float[] _drift;
	private StringName _driftAnimation;
	private Vector2 _driftOffset;
	private float _driftScale = 1f;
	private float _appliedDrift;
	private Vector2 _baseOffset;

	static RedWarrior()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_baseMoveSpeed = MoveSpeed;
		_baseOffset = Sprite.Offset;
		_attackCooldownTimer = Rng.RandfRange(0.4f, 0.9f);
		_dodgeCooldownTimer = Rng.RandfRange(0.8f, 1.6f);
		_whirlCooldownTimer = Rng.RandfRange(2f, 3f);
		_trail = new EnemySlashTrail
		{
			CoreWidth = 3f,
			GlowWidth = 10f,
			GlowColor = TrailGlowColor,
			CoreColor = new Color(1f, 0.65f, 0.6f, 1f),
			MinPointSpacing = 1.5f,
			SparkAmount = 12,
		};
		Visual.AddChild(_trail);
		Sprite.FrameChanged += ApplyRootMotion;
		Stats.HitTaken += _ => _whiff.NotifyHitTaken();
	}

	// ── Root motion ─────────────────────────────────────────────────────────────────────────

	// animOffset: for sheets whose cell isn't 170x96, the shift that puts the frame-0 figure where the
	// idle figure stands. scale: the body travels drift * scale (aimed jumps); the figure stays on it.
	protected void BeginRootMotion(float[] drift, Vector2 animOffset = default, float scale = 1f)
	{
		_drift = drift;
		_driftAnimation = Sprite.Animation;
		_driftOffset = _baseOffset + animOffset;
		_driftScale = scale;
		_appliedDrift = 0f;
		Sprite.Offset = _driftOffset;
	}

	// Each frame: hide the drawn drift with the offset, and move the body by the newly drawn amount.
	// Only for the animation it was started on: a stun switching to idle mid-lunge must not read the
	// idle's frame numbers as drift (that walked the body backwards).
	private void ApplyRootMotion()
	{
		if (_drift is null || Sprite.Animation != _driftAnimation)
			return;
		float drift = _drift[Mathf.Clamp(Sprite.Frame, 0, _drift.Length - 1)];
		float step = (drift - _appliedDrift) * SpriteScale * _driftScale;
		_appliedDrift = drift;
		Sprite.Offset = _driftOffset + new Vector2(-drift, 0f);
		if (Mathf.Abs(step) > 0.01f)
		{
			float dir = FacingRight ? 1f : -1f;
			// Stop at walls/ledges: the figure then just stays put (offset still hides the drift).
			if (!TestMove(GlobalTransform, new Vector2(dir * step, 0f)) && HasGroundAhead(dir))
				MoveAndCollide(new Vector2(dir * step, 0f));
		}
	}

	protected void EndRootMotion()
	{
		_drift = null;
		_appliedDrift = 0f;
		if (Sprite is null)
			return;
		// The last drifted frame is drawn far ahead of the cell centre; dropping the offset while it's
		// still on screen flashed the figure ~100px forward for a frame. Switch to the idle first.
		if (Sprite.Animation == _driftAnimation)
			Sprite.Play("idle");
		Sprite.Offset = _baseOffset;
	}

	// ── Main loop ───────────────────────────────────────────────────────────────────────────

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;
		float dt = (float)delta;
		_decisionTimer -= dt;
		_attackCooldownTimer -= dt * Speed() / CooldownScale;
		_dodgeCooldownTimer -= dt / CooldownScale;
		_whirlCooldownTimer -= dt / CooldownScale;
		var player = PlayerReads.Find(this);
		_whiff.Update(player, dt);
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval || player is null || !PlayerDetected || _busy || !IsOnFloor())
			return;

		float distanceX = player.GlobalPosition.X - GlobalPosition.X;
		float absDistance = Mathf.Abs(distanceX);

		if (!_frenzied && MoveAllowed(Move.Frenzy) && Stats.CurrentHealth <= Stats.MaxHealth * FrenzyHealthRatio)
		{
			_ = Roar();
			return;
		}
		if (Decide(player, distanceX, absDistance))
			return;
		if (MoveAllowed(Move.Dodge) && _whiff.PlayerStartedSwing && absDistance <= DodgeTriggerRange
			&& _dodgeCooldownTimer <= 0f && Rng.Randf() < DodgeChance && HasRoomToDodge(-Mathf.Sign(distanceX)))
		{
			_ = Dodge(-Mathf.Sign(distanceX));
			return;
		}
		if (MoveAllowed(Move.Punish) && _whiff.JustWhiffed && absDistance <= SlashRange + 10f && _attackCooldownTimer <= AttackCooldown * 0.5f)
		{
			_whiff.Consume();
			_ = DoubleSlash(0.12f, allowSecond: false);
			return;
		}
		if (MoveAllowed(Move.Whirlwind) && absDistance <= WhirlRange && _whirlCooldownTimer <= 0f && _attackCooldownTimer <= 0f && Rng.Randf() < WhirlChance)
		{
			_ = Whirlwind();
			return;
		}
		if (MoveAllowed(Move.Slash) && absDistance <= SlashRange && _attackCooldownTimer <= 0f)
		{
			_ = DoubleSlash(SlashTelegraph * TelegraphScale, allowSecond: MoveAllowed(Move.SecondSlash));
			return;
		}
	}

	protected float Speed() => (_frenzied ? FrenzySpeedScale : 1f) * SpeedBonus;
	protected float Damage(float multiplier) => (multiplier + (_frenzied ? FrenzyDamageBonus : 0f)) * DamageScale;

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_busy)
			return 0f;
		float toward = Mathf.Sign(distanceX);
		if (Mathf.Abs(distanceX) > SlashRange * 0.8f && CanStepTo(toward))
			return toward * MoveSpeed;
		return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
	}

	protected override bool CanTurnToFacePlayer => !_busy;

	// ── Double slash ────────────────────────────────────────────────────────────────────────

	protected async Task DoubleSlash(float telegraph, bool allowSecond)
	{
		if (!Begin())
			return;
		try
		{
			Sprite.Play("attack");
			Sprite.Pause();
			if (!await Frames(0, 3, 0.04f))
				return;
			Sprite.Frame = 4;
			FlashBody(telegraph);
			if (!await Wait(telegraph))
				return;
			for (float held = 0f; held < MaxTelegraphHold && HoldTelegraph(); held += 0.05f)
			{
				if (!await Wait(0.05f))
					return;
			}
			FacePlayer();
			_trail.Play(new Vector2(0f, 4f), 60f, -40f, 60f, 0.08f, 34f);
			Shake(SwingShake, 0.15f);
			if (!await StrikeFrames(5, LowSlashReach, LowSlashTop, Damage(LowDamageMultiplier), 200f, 0.05f))
				return;
			if (!await Frames(9, 10, 0.05f))
				return;
			if (allowSecond && Rng.Randf() < SecondChance)
			{
				FacePlayer();
				if (!await Frames(11, 14, 0.05f))
					return;
				// Frame 15 is the hammer meeting the ground: rock burst where the head lands.
				_trail.Play(new Vector2(0f, -10f), 64f, -110f, 30f, 0.08f);
				float slamX = (SlamImpactCellX - CellCenterX) * SpriteScale + SpriteOffset.X;
				float feetY = (CellFeetY - CellCenterY) * SpriteScale + SpriteOffset.Y;
				GroundSlamFx.Impact(this, GlobalPosition + new Vector2(FacingRight ? slamX : -slamX, feetY), SlamImpactScale, SlamPalette);
				Shake(SlamShake, 0.25f);
				if (!await StrikeFrames(15, HighSlashReach, HighSlashTop, Damage(HighDamageMultiplier), 280f, 0.045f))
					return;
				if (!await Frames(19, 22, 0.045f))
					return;
				if (OverheadFinisher)
				{
					// Attack 23-25: the hammer swung in a big arc over his head, hitting all round him
					// (alpha bbox x 57-116, top y ~27-34 down to the waist).
					FacePlayer();
					FlashBody(0.12f);
					_trail.Play(new Vector2(0f, -30f), 58f, -170f, -10f, 0.1f);
					Shake(SlamShake, 0.25f);
					bool hit = false;
					for (int f = 23; f <= 25; f++)
					{
						Sprite.Frame = f;
						if (!hit)
							hit = Strike(new Vector2(12f, -11f), new Vector2(106f, 81f), Damage(HighDamageMultiplier * 1.15f), 320f, ignoreFacing: true);
						if (!await Wait(0.06f / Speed()))
							return;
					}
				}
			}
			if (!await Frames(26, 29, 0.05f))
				return;
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * Rng.RandfRange(0.85f, 1.2f);
	}

	protected async Task<bool> Frames(int from, int to, float each)
	{
		for (int f = from; f <= to; f++)
		{
			Sprite.Frame = f;
			if (!await Wait(each / Speed()))
				return false;
		}
		return true;
	}

	// Shows each active frame for `each` seconds and checks damage on it (once per swing) with a box
	// that reaches exactly as far as the blade drawn on that frame.
	private async Task<bool> StrikeFrames(int firstFrame, float[] reach, float[] top, float damageMultiplier, float knockback, float each)
	{
		bool hit = false;
		for (int i = 0; i < reach.Length; i++)
		{
			Sprite.Frame = firstFrame + i;
			if (!hit)
				hit = StrikeToReach(reach[i], top[i], damageMultiplier, knockback);
			if (!await Wait(each / Speed()))
				return false;
		}
		return true;
	}

	// Box from just in front of the body out to the drawn tip (cell px), from the drawing's top to the feet.
	private bool StrikeToReach(float reachX, float topY, float damageMultiplier, float knockback)
	{
		float front = (reachX - CellCenterX) * SpriteScale + SpriteOffset.X;
		float top = (topY - CellCenterY) * SpriteScale + SpriteOffset.Y;
		float feet = (CellFeetY - CellCenterY) * SpriteScale + SpriteOffset.Y;
		var center = new Vector2((StrikeBackEdge + front) * 0.5f, (top + feet) * 0.5f);
		var size = new Vector2(front - StrikeBackEdge, feet - top);
		return Strike(center, size, damageMultiplier, knockback);
	}

	// ── Dodge ───────────────────────────────────────────────────────────────────────────────

	protected bool HasRoomToDodge(float away) =>
		!TestMove(GlobalTransform, new Vector2(away * 70f, 0f)) && CanStepTo(away);

	// Back to the player, slide away (the dash sheet's drawn travel, as root motion), untouchable,
	// leaving the same neon afterimages as the player's dash. Then square up again, ready to answer.
	protected async Task Dodge(float away)
	{
		_busy = true;
		_dodgeCooldownTimer = DodgeCooldown;
		ClearFlash();
		FacingRight = away > 0f;
		Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);
		Stats.ExternalInvulnerable = true;
		try
		{
			Sprite.Play("dash");
			Sprite.Pause();
			BeginRootMotion(DashDrift);
			Sprite.Frame = 0;
			for (int f = 1; f <= 7; f++)
			{
				Sprite.Frame = f;
				SpawnDodgeGhost();
				if (!await Wait(DodgeFrameTime * 0.5f / Speed()))
					return;
				SpawnDodgeGhost();
				if (!await Wait(DodgeFrameTime * 0.5f / Speed()))
					return;
			}
		}
		finally
		{
			if (IsInstanceValid(this))
			{
				Stats.ExternalInvulnerable = false;
				EndRootMotion();
				FacePlayer();
				_busy = false;
			}
		}
		// Squared up out of range: ready to step back in and swing almost at once.
		_attackCooldownTimer = Mathf.Min(_attackCooldownTimer, 0.2f);
	}

	private void SpawnDodgeGhost()
	{
		Texture2D frame = Sprite.SpriteFrames?.GetFrameTexture(Sprite.Animation, Sprite.Frame);
		if (frame is null || GetTree().CurrentScene is not Node scene)
			return;
		var ghost = new Sprite2D
		{
			Texture = frame,
			Centered = Sprite.Centered,
			Offset = Sprite.Offset,
			Material = Metroidvania.Player.NeonSilhouette.CreateMaterial(DodgeGhostColor, DodgeGhostIntensity),
			Modulate = new Color(1f, 1f, 1f, DodgeGhostAlpha),
			ZIndex = ZIndex - 1,
		};
		scene.AddChild(ghost);
		ghost.GlobalTransform = Sprite.GlobalTransform;
		Tween fade = ghost.CreateTween();
		fade.TweenProperty(ghost, "modulate:a", 0f, DodgeGhostLifetime).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
		fade.TweenCallback(Callable.From(ghost.QueueFree));
	}

	// ── Whirlwind ───────────────────────────────────────────────────────────────────────────

	protected async Task Whirlwind()
	{
		if (!Begin())
			return;
		_whirlCooldownTimer = WhirlCooldown;
		try
		{
			Sprite.Play("spin_attack");
			Sprite.Pause();
			BeginRootMotion(SpinDrift);
			if (!await Frames(0, 3, 0.04f))
				return;
			FlashBody(0.35f);
			Shake(SwingShake, 0.2f);
			if (!await Frames(4, 8, 0.07f))
				return;
			float hitTimer = 0f;
			for (int f = 9; f <= 23; f++)
			{
				Sprite.Frame = f;
				hitTimer -= 0.055f;
				if (hitTimer <= 0f)
				{
					hitTimer = WhirlHitInterval;
					Strike(WhirlHitboxCenter, WhirlHitboxSize, Damage(WhirlDamageMultiplier), 180f, ignoreFacing: true);
				}
				if (!await Wait(0.055f / Speed()))
					return;
			}
			if (!await Frames(24, 29, 0.05f))
				return;
		}
		finally
		{
			EndRootMotion();
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * 0.8f;
	}

	// ── Frenzy ──────────────────────────────────────────────────────────────────────────────

	protected async Task Roar()
	{
		_frenzied = true;
		_busy = true;
		try
		{
			Sprite.Play("taunt");
			Sprite.Pause();
			for (int f = 0; f <= 17; f++)
			{
				Sprite.Frame = f;
				if (f == 7)
				{
					Shake(RoarShake, RoarShakeDuration);
					Sprite.SelfModulate = new Color(1.8f, 0.6f, 0.6f, 1f);
					CreateTween().TweenProperty(Sprite, "self_modulate", Colors.White, 0.8f);
				}
				if (!await Wait(1f / 15f))
					return;
			}
			MoveSpeed = _baseMoveSpeed * FrenzySpeedScale;
			Sprite.SpeedScale = FrenzySpeedScale;
		}
		finally
		{
			if (IsInstanceValid(this))
				_busy = false;
		}
	}

	// ── Shared ──────────────────────────────────────────────────────────────────────────────

	protected bool Begin()
	{
		if (!EnemyCombatCoordinator.TryAcquireAttackSlot())
		{
			_attackCooldownTimer = 0.25f;
			return false;
		}
		HoldingAttackSlot = true;
		_busy = true;
		FacePlayer();
		return true;
	}

	protected void Finish()
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

	// ignoreFacing: the box is centred on him (the whirlwind), so either side counts.
	protected bool Strike(Vector2 center, Vector2 size, float damageMultiplier, float knockback, bool ignoreFacing = false)
	{
		if (IsStunned)
			return false;
		Vector2 at = GlobalPosition + new Vector2(FacingRight || ignoreFacing ? center.X : -center.X, center.Y);
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
				player.ApplyKnockback(new Vector2(Mathf.Sign(player.GlobalPosition.X - GlobalPosition.X), -0.3f).Normalized(), knockback);
				BloodEffect.SpawnAt(this, player.GlobalPosition);
			}
			return true;
		}
		return false;
	}

	protected async Task<bool> Wait(float seconds)
	{
		await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
		// The tree timer keeps ticking through a stun/execution freeze, so the attack has to notice it
		// and stop — otherwise the swing that got parried into a stun carried on and still hit.
		return IsInstanceValid(this) && !IsQueuedForRemoval && !IsStunned && ProcessMode != ProcessModeEnum.Disabled;
	}

	protected void Shake(float strength, float duration)
	{
		if (strength <= 0f)
			return;
		if (PlayerReads.Find(this) is { } player && player.GlobalPosition.DistanceTo(GlobalPosition) < 700f)
			player.ShakeCamera(strength, duration);
	}

	protected void FacePlayer()
	{
		if (PlayerReads.Find(this) is { } player)
		{
			FacingRight = player.GlobalPosition.X >= GlobalPosition.X;
			Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);
		}
	}

	protected void FlashBody(float duration)
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

	protected void ClearFlash()
	{
		_flashTween?.Kill();
		if (Sprite is not null)
			Sprite.SelfModulate = Colors.White;
	}

	protected override void OnDefeated()
	{
		ClearFlash();
		EndRootMotion();
		if (Sprite is not null)
			Sprite.SpeedScale = 1f;
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
