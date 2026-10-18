using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// Ghost Warrior 3 — an Oscura witch with a ghost-fire flail. Fights at mid range and punishes the
// usual ways of getting at a caster:
//  * Latigazo (attack1): the flail lashes a spectral cloud out in front — her mid-range poke.
//  * Remolino (attack2): she spins inside a burst of smoke that covers both sides — the answer to
//    a player who closes in or slips behind her (so a backstab has to be earned).
//  * Erupción (attack3): a long, blinking charge, then a row of cyan eruptions marching away along
//    the floor (OscuroEruption) — jump or roll clear.
// Keeps her distance (walks back when crowded), never steps off a ledge. Every move is announced
// with a cyan blink on its wind-up frame.
public partial class GhostWarrior3 : MeleeEnemy
{
	[ExportGroup("Spacing")]
	[Export] public float PreferredDistance = 120f;
	[Export] public float TooCloseDistance = 80f;
	[Export] public float DecisionInterval = 0.35f;

	[ExportGroup("Latigazo")]
	[Export] public float LashRange = 140f;
	[Export] public float LashWindup = 0.4f;
	[Export] public int LashHoldFrame = 3;
	[Export] public int LashHitFrame = 4;
	[Export] public Vector2 LashHitboxCenter = new(58f, -14f);
	[Export] public Vector2 LashHitboxSize = new(96f, 80f);

	[ExportGroup("Remolino")]
	// Player this close, or anywhere close behind her → spin.
	[Export] public float SpinTriggerRange = 75f;
	[Export] public float SpinBehindRange = 115f;
	[Export] public float SpinWindup = 0.3f;
	[Export] public int SpinHoldFrame = 3;
	[Export] public int SpinHitFrame = 4;
	[Export] public Vector2 SpinHitboxCenter = new(4f, -12f);
	[Export] public Vector2 SpinHitboxSize = new(150f, 84f);

	[ExportGroup("Erupción")]
	[Export] public float EruptionMinRange = 110f;
	[Export] public float EruptionMaxRange = 340f;
	[Export] public float EruptionCooldown = 6f;
	[Export] public float EruptionChance = 0.45f;
	[Export] public int EruptionCastFrame = 13;
	[Export] public int EruptionCount = 5;
	[Export] public float EruptionStartOffset = 60f;
	[Export] public float EruptionSpacing = 55f;
	[Export] public float EruptionInterval = 0.12f;
	[Export] public float EruptionWarning = 0.35f;
	[Export] public Vector2 EruptionTileSize = new(50f, 62f);
	[Export] public float EruptionDamageMultiplier = 1.3f;

	[ExportGroup("Look")]
	[Export] public float AttackFps = 14f;
	[Export] public float HurtDuration = 0.3f;
	[Export] public Color TelegraphTint = new(0.6f, 2.4f, 3f, 1f);

	private enum Move { Lash, Spin, Eruption }

	private static readonly RandomNumberGenerator Rng = new();

	private Move _nextMove;
	private float _decisionTimer;
	private float _eruptionCooldownTimer;
	private float _hurtTimer;
	private Tween _telegraphTween;
	private CollisionShape2D _hitboxShape;

	static GhostWarrior3()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_hitboxShape = AttackHitbox.GetNode<CollisionShape2D>("CollisionShape2D");
		_eruptionCooldownTimer = Rng.RandfRange(2f, 4f);
		Stats.HitTaken += _ =>
		{
			if (!Attacking)
				_hurtTimer = HurtDuration;
		};
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;
		float dt = (float)delta;
		_hurtTimer -= dt;
		_decisionTimer -= dt;
		_eruptionCooldownTimer -= dt;
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval)
			return;
		Think();
	}

	// All her attacks are chosen here; MeleeEnemy's plain in-range swing never fires.
	protected override bool ReadyToCommitAttack() => false;

	private void Think()
	{
		var player = PlayerRef;
		if (player is null || !PlayerDetected || Attacking || !CanAttack || _hurtTimer > 0f || !IsOnFloor())
			return;

		float distanceX = player.GlobalPosition.X - GlobalPosition.X;
		float absDistance = Mathf.Abs(distanceX);
		bool behind = (distanceX > 0f) != FacingRight;

		if (absDistance <= SpinTriggerRange || (behind && absDistance <= SpinBehindRange))
		{
			Commit(Move.Spin, 1f);
			return;
		}
		// A whiffed swing or parry in reach → a quick lash.
		if ((Whiff.JustWhiffed || PlayerReads.WhiffedParryToward(player, GlobalPosition.X)) && absDistance <= LashRange)
		{
			Whiff.Consume();
			Commit(Move.Lash, 0.5f);
			return;
		}
		if (_decisionTimer > 0f)
			return;
		_decisionTimer = DecisionInterval;

		if (_eruptionCooldownTimer <= 0f && absDistance >= EruptionMinRange && absDistance <= EruptionMaxRange
			&& Rng.Randf() < EruptionChance)
		{
			_eruptionCooldownTimer = EruptionCooldown;
			Commit(Move.Eruption, 1f);
			return;
		}
		if (absDistance <= LashRange)
			Commit(Move.Lash, 1f);
	}

	private void Commit(Move move, float windupScale)
	{
		_nextMove = move;
		TryAttackNow(windupScale);
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_hurtTimer > 0f)
			return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
		return SpacingMoveX(distanceX, currentVelocityX, PreferredDistance, TooCloseDistance);
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null || Attacking)
			return;
		string anim = _hurtTimer > 0f ? "hurt" : Mathf.Abs(velocity.X) > 5f ? "walk" : "idle";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	// ── Moves ──

	protected override async Task Attack()
	{
		Attacking = true;
		CanAttack = false;
		_hurtTimer = 0f;
		float windupScale = WindupScale;
		WindupScale = 1f;
		try
		{
			switch (_nextMove)
			{
				case Move.Spin:
					await Swing("attack2", SpinHoldFrame, SpinHitFrame, SpinWindup * windupScale, SpinHitboxCenter, SpinHitboxSize);
					break;
				case Move.Eruption:
					await Eruption();
					break;
				default:
					await Swing("attack1", LashHoldFrame, LashHitFrame, LashWindup * windupScale, LashHitboxCenter, LashHitboxSize);
					break;
			}
		}
		finally
		{
			if (IsInstanceValid(this))
			{
				AttackHitbox.Deactivate();
				Attacking = false;
				if (HoldingAttackSlot)
				{
					EnemyCombatCoordinator.ReleaseAttackSlot();
					HoldingAttackSlot = false;
				}
			}
		}
		if (!IsInstanceValid(this))
			return;
		await ToSignal(GetTree().CreateTimer(AttackCooldown * Rng.RandfRange(0.85f, 1.15f)), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			CanAttack = true;
	}

	// Hold the wind-up frame blinking, play on, hit for two frames from hitFrame, then let the clip end.
	private async Task Swing(string animation, int holdFrame, int hitFrame, float windup, Vector2 hitboxCenter, Vector2 hitboxSize)
	{
		Sprite.Play(animation);
		Sprite.Stop();
		Sprite.Frame = holdFrame;
		Telegraph(windup);
		if (!await Wait(windup))
			return;
		ClearTelegraph();
		Sprite.Play(animation);
		Sprite.Frame = holdFrame;
		if (!await Wait((hitFrame - holdFrame) / AttackFps))
			return;
		SetHitbox(hitboxCenter, hitboxSize);
		AttackHitbox.Activate(Stats);
		if (!await Wait(2f / AttackFps))
			return;
		AttackHitbox.Deactivate();
		await Wait(RemainingClip(animation, hitFrame + 2));
	}

	private async Task Eruption()
	{
		Sprite.Play("attack3");
		float charge = EruptionCastFrame / AttackFps;
		Telegraph(charge);
		if (!await Wait(charge))
			return;
		ClearTelegraph();
		float direction = FacingRight ? 1f : -1f;
		Vector2 ground = GlobalPosition + new Vector2(direction * EruptionStartOffset, BodyBottom);
		int damage = Mathf.Max(1, Mathf.RoundToInt(Stats.AttackPower * EruptionDamageMultiplier));
		_ = OscuroEruption.Run(this, Stats, ground, direction, EruptionCount, EruptionSpacing, EruptionInterval,
			EruptionTileSize, EruptionWarning, damage);
		await Wait(RemainingClip("attack3", EruptionCastFrame));
	}

	// ── Helpers ──

	private async Task<bool> Wait(float seconds)
	{
		if (seconds > 0f)
			await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
		return IsInstanceValid(this) && !IsQueuedForRemoval && IsInsideTree();
	}

	private float RemainingClip(string animation, int fromFrame) =>
		Mathf.Max(0f, (Sprite.SpriteFrames.GetFrameCount(animation) - fromFrame) / AttackFps);

	// A fresh shape per move: the scene's shape is shared by every instance of it.
	private void SetHitbox(Vector2 center, Vector2 size)
	{
		_hitboxShape.Shape = new RectangleShape2D { Size = size };
		_hitboxShape.Position = Vector2.Zero;
		AttackHitbox.Position = new Vector2(FacingRight ? center.X : -center.X, center.Y);
	}

	private void Telegraph(float duration)
	{
		if (duration <= 0f)
			return;
		_telegraphTween?.Kill();
		_telegraphTween = CreateTween();
		int blinks = Mathf.Max(2, Mathf.RoundToInt(duration / 0.2f));
		float half = duration / blinks * 0.5f;
		for (int i = 0; i < blinks; i++)
		{
			_telegraphTween.TweenProperty(Sprite, "self_modulate", TelegraphTint, half);
			_telegraphTween.TweenProperty(Sprite, "self_modulate", Colors.White, half);
		}
	}

	private void ClearTelegraph()
	{
		_telegraphTween?.Kill();
		Sprite.SelfModulate = Colors.White;
	}
}
