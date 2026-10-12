using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Caballero del escudo / Shield Knight (Knight pack, faces right). A disciplined sword-and-shield
// knight: guards, rolls past you, and presses forward with a stepping three-cut combo.
// This sheet is awkward: each clip uses its own cell width (idle 64, run/shield/death 96,
// attack/jump 144, roll 180) and the figure sits at a different spot in each, so every clip gets
// its own Sprite.Offset (ClipAnchor) to keep the body on the origin; on top of that attack and roll
// carry the figure across the cell (root motion), cancelled frame by frame while the body moves.
// Sheet beats used:
//   attack (22, 18fps): 0-4 squared up, 5-8 a first cut, 9 drawn back, 10-12 a second cut, 13-16
//     stepping in, 17 a long lunging slash (~28px of drawn travel), 18-21 recovery.
//   shield (7): 3-6 the shield brought round in front.
//   roll (15, 18fps): a forward roll covering ~81px of the cell.
// Moves:
//   Guard — a swing starting in front of him raises the shield: frontal hits are blocked. A block
//     is answered with a quick cut.
//   Advancing combo — two cuts and a lunging slash, stepping forward through it.
//   Roll past — when you attack and the guard is on cooldown (or you turtle), he rolls straight
//     through you (untouchable while rolling), comes up behind you and closes straight back in.
// Telegraphs: body flashes white before the combo; steel-blue neon trails.
public partial class ShieldKnight : Enemy
{
	[ExportGroup("Combo")]
	[Export] public float ComboRange = 60f;
	[Export] public float AttackCooldown = 1.4f;
	[Export] public float ComboTelegraph = 0.3f;
	[Export] public float CutDamageMultiplier = 1f;
	[Export] public float LungeDamageMultiplier = 1.3f;
	// Measured (scale 2): cuts 1-2 reach ~56px in front over the whole body; the lunging slash ~77px.
	[Export] public Vector2 CutHitboxCenter = new(30f, -4f);
	[Export] public Vector2 CutHitboxSize = new(56f, 56f);
	[Export] public Vector2 LungeHitboxCenter = new(40f, -2f);
	[Export] public Vector2 LungeHitboxSize = new(76f, 50f);

	[ExportGroup("Guard")]
	[Export] public float GuardRange = 100f;
	[Export] public float GuardChance = 0.6f;
	[Export] public float GuardDuration = 1f;
	[Export] public float GuardCooldown = 2.5f;

	[ExportGroup("Roll")]
	[Export] public float RollTrigger = 70f;
	[Export] public float RollChance = 0.5f;
	[Export] public float RollCooldown = 4f;

	[ExportGroup("Look")]
	[Export] public Color TrailGlowColor = new(0.45f, 0.65f, 1f, 1f);
	[Export] public Color FlashTint = new(2.2f, 2.2f, 2.2f, 1f);

	private const float SpriteScale = 2f;

	// Per-clip offset (texture px) that puts each clip's figure where idle's is.
	private static readonly Dictionary<string, float> ClipAnchor = new()
	{
		["idle"] = 0f, ["run"] = -3.5f, ["shield"] = 1f, ["attack"] = 8f, ["roll"] = 26f, ["jump"] = 8f, ["death"] = 1f,
	};
	// Drawn forward travel per frame (texture px), measured off the sheet.
	private static readonly float[] AttackDrift = { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 4f, 11f, 11f, 16f, 28f, 28f, 28f, 28f, 28f };
	private static readonly float[] RollDrift = { 0f, 1f, 6f, 7f, 10f, 29f, 45f, 60f, 69f, 76f, 80f, 81f, 81f, 81f, 81f };

	private static readonly RandomNumberGenerator Rng = new();
	private readonly WhiffWatcher _whiff = new();

	private EnemySlashTrail _trail;
	private Tween _flashTween;
	private bool _busy;
	private bool _guarding;
	private bool _blocked;
	private bool _rolling;
	private float[] _drift;
	private float _appliedDrift;
	private float _attackCooldownTimer;
	private float _guardCooldownTimer;
	private float _rollCooldownTimer;

	static ShieldKnight()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_attackCooldownTimer = Rng.RandfRange(0.4f, 0.9f);
		_rollCooldownTimer = Rng.RandfRange(1f, 2f);
		_trail = new EnemySlashTrail
		{
			CoreWidth = 2.5f,
			GlowWidth = 9f,
			GlowColor = TrailGlowColor,
			CoreColor = new Color(0.9f, 0.95f, 1f, 1f),
			MinPointSpacing = 1.5f,
			SparkAmount = 10,
		};
		Visual.AddChild(_trail);
		Sprite.AnimationChanged += () => { _drift = null; _appliedDrift = 0f; ApplyOffset(); };
		Sprite.FrameChanged += ApplyRootMotion;
		ApplyOffset();
		Stats.IncomingHitInterceptor = TryBlock;
		Stats.HitTaken += _ => _whiff.NotifyHitTaken();
	}

	// ── Offsets & root motion ───────────────────────────────────────────────────────────────

	private void ApplyOffset()
	{
		float anchor = ClipAnchor.GetValueOrDefault(Sprite.Animation, 0f);
		Sprite.Offset = new Vector2(anchor - _appliedDrift, 0f);
	}

	private void ApplyRootMotion()
	{
		if (_drift is null)
			return;
		float drift = _drift[Mathf.Clamp(Sprite.Frame, 0, _drift.Length - 1)];
		float step = (drift - _appliedDrift) * SpriteScale;
		_appliedDrift = drift;
		ApplyOffset();
		if (Mathf.Abs(step) > 0.01f)
		{
			float dir = FacingRight ? 1f : -1f;
			if (!TestMove(GlobalTransform, new Vector2(dir * step, 0f)) && HasGroundAhead(dir))
				MoveAndCollide(new Vector2(dir * step, 0f));
		}
	}

	// Plays a clip paused at frame 0 with root motion from `drift`.
	private void PlayWithDrift(string anim, float[] drift)
	{
		Sprite.Play(anim);
		Sprite.Pause();
		_drift = drift;
		_appliedDrift = 0f;
		Sprite.Frame = 0;
		ApplyOffset();
	}

	// ── Guard ───────────────────────────────────────────────────────────────────────────────

	private bool PlayerInFront(Node2D player) => (player.GlobalPosition.X >= GlobalPosition.X) == FacingRight;

	private bool TryBlock()
	{
		// Untouchable mid-roll.
		if (_rolling)
			return true;
		if (!_guarding || PlayerReads.Find(this) is not { } player || !PlayerInFront(player))
			return false;
		_blocked = true;
		ImpactEffect.SpawnAt(this, GlobalPosition + new Vector2(FacingRight ? 16f : -16f, -8f));
		Sfx.PlayAt(this, "Combat/Sword", "Sword Blocked");
		return true;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;
		float dt = (float)delta;
		_attackCooldownTimer -= dt;
		_guardCooldownTimer -= dt;
		_rollCooldownTimer -= dt;
		var player = PlayerReads.Find(this);
		_whiff.Update(player, dt);
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval || player is null || !PlayerDetected || _busy || !IsOnFloor())
			return;

		float distanceX = player.GlobalPosition.X - GlobalPosition.X;
		float absDistance = Mathf.Abs(distanceX);
		bool turtling = PlayerReads.IsBlockingToward(player, GlobalPosition.X);

		if (_whiff.PlayerStartedSwing && PlayerInFront(player) && absDistance <= GuardRange)
		{
			if (_guardCooldownTimer <= 0f && Rng.Randf() < GuardChance)
			{
				_ = Guard();
				return;
			}
			if (absDistance <= RollTrigger && _rollCooldownTimer <= 0f && Rng.Randf() < RollChance)
			{
				_ = RollPast(Mathf.Sign(distanceX));
				return;
			}
		}
		if (turtling && absDistance <= RollTrigger && _rollCooldownTimer <= 0f)
		{
			_ = RollPast(Mathf.Sign(distanceX));
			return;
		}
		if (absDistance <= ComboRange && _attackCooldownTimer <= 0f)
			_ = Combo(ComboTelegraph);
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_busy)
			return 0f;
		float toward = Mathf.Sign(distanceX);
		if (Mathf.Abs(distanceX) > ComboRange * 0.8f && CanStepTo(toward))
			return toward * MoveSpeed;
		return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
	}

	protected override bool CanTurnToFacePlayer => !_busy;

	private async Task Guard()
	{
		_busy = true;
		_guarding = true;
		_blocked = false;
		_guardCooldownTimer = GuardCooldown;
		FacePlayer();
		try
		{
			Sprite.Play("shield");
			Sprite.Pause();
			for (int f = 0; f <= 4; f++)
			{
				Sprite.Frame = f;
				if (!await Wait(0.03f))
					return;
			}
			float t = 0f;
			while (t < GuardDuration && !_blocked)
			{
				await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
				if (!IsInstanceValid(this) || IsQueuedForRemoval)
					return;
				t += (float)GetPhysicsProcessDeltaTime();
			}
		}
		finally
		{
			if (IsInstanceValid(this))
			{
				_guarding = false;
				_busy = false;
			}
		}
		if (_blocked)
		{
			_attackCooldownTimer = 0f;
			_ = Combo(0.12f, cuts: 1);
		}
	}

	// ── Combo ───────────────────────────────────────────────────────────────────────────────

	private async Task Combo(float telegraph, int cuts = 3)
	{
		if (!Begin())
			return;
		try
		{
			PlayWithDrift("attack", AttackDrift);
			if (!await Frames(1, 3, 1f / 18f))
				return;
			Sprite.Frame = 4;
			FlashBody(telegraph);
			if (!await Wait(telegraph))
				return;
			Sprite.Frame = 5;
			_trail.Play(new Vector2(2f, -6f), 44f, -100f, 50f, 0.07f);
			Strike(CutHitboxCenter, CutHitboxSize, CutDamageMultiplier, 180f);
			if (!await Frames(6, 8, 1f / 18f))
				return;
			if (cuts >= 2 && Rng.Randf() < 0.75f)
			{
				FacePlayer();
				if (!await Frames(9, 9, 1f / 18f))
					return;
				Sprite.Frame = 10;
				_trail.Play(new Vector2(2f, -2f), 44f, 70f, -60f, 0.07f);
				Strike(CutHitboxCenter, CutHitboxSize, CutDamageMultiplier, 200f);
				if (!await Frames(11, 16, 1f / 18f))
					return;
				if (cuts >= 3 && Rng.Randf() < 0.7f)
				{
					Sprite.Frame = 17;
					_trail.PlayThrust(new Vector2(0f, -2f), new Vector2(LungeHitboxCenter.X + LungeHitboxSize.X * 0.5f, -2f), 0.05f, 0.08f);
					Strike(LungeHitboxCenter, LungeHitboxSize, LungeDamageMultiplier, 300f);
					if (!await Frames(18, 21, 1f / 18f))
						return;
				}
			}
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * Rng.RandfRange(0.85f, 1.2f);
	}

	private async Task<bool> Frames(int from, int to, float each)
	{
		for (int f = from; f <= to; f++)
		{
			Sprite.Frame = f;
			if (!await Wait(each))
				return false;
		}
		return true;
	}

	// ── Roll past ───────────────────────────────────────────────────────────────────────────

	private async Task RollPast(float direction)
	{
		if (!Begin())
			return;
		_rollCooldownTimer = RollCooldown;
		try
		{
			FaceTowards(GlobalPosition.X + direction);
			_rolling = true;
			PlayWithDrift("roll", RollDrift);
			if (!await Frames(1, 14, 1f / 18f))
				return;
			_rolling = false;
		}
		finally
		{
			if (IsInstanceValid(this))
				_rolling = false;
			Finish();
		}
		// Came up past them: turn and go straight back in (the roll overshoots ~100px, so the cut
		// comes from the normal approach rather than a swing at thin air).
		FacePlayer();
		_attackCooldownTimer = 0f;
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
		_drift = null;
		_appliedDrift = 0f;
		ClearFlash();
		if (HoldingAttackSlot)
		{
			EnemyCombatCoordinator.ReleaseAttackSlot();
			HoldingAttackSlot = false;
		}
	}

	private bool Strike(Vector2 center, Vector2 size, float damageMultiplier, float knockback)
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
				player.ApplyKnockback(new Vector2(Mathf.Sign(player.GlobalPosition.X - GlobalPosition.X), -0.3f).Normalized(), knockback);
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
		_drift = null;
		_appliedDrift = 0f;
		base.OnDefeated();
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null || _busy)
			return;
		string anim = Mathf.Abs(velocity.X) > 5f ? "run" : "idle";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	protected override bool ContactDamageEnabled => false;
}
