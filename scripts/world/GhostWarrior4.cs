using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// Ghost Warrior 4 — the Oscuros' champion: an armoured knight with a spectral greatsword. Slow,
// heavy, hard to stagger (3 parries to stun, no knockback), and a natural guardian:
//  * Dormant (StartDormant): kneels with the sword planted (the sheet's "Stick in floor" end
//    pose) until the player comes within WakeRange or hits it, then draws the sword ("Take sword
//    off") and fights.
//  * Barrido (attack1): a low sweep along the floor — jump it. Sometimes chains into the tajo.
//  * Tajo (attack2): an overhead cut down across the front; also its punish for a whiffed parry.
//  * Media luna (attack3): a slow charge, a huge crescent, then spectral eruptions marching on
//    from where the blade lands (OscuroEruption).
// Every move is announced with a cyan blink on its wind-up frame.
public partial class GhostWarrior4 : MeleeEnemy
{
	[ExportGroup("Guardian")]
	[Export] public bool StartDormant = true;
	[Export] public float WakeRange = 200f;

	[ExportGroup("Barrido")]
	[Export] public float SweepWindup = 0.45f;
	[Export] public int SweepHoldFrame = 4;
	[Export] public int SweepHitFrame = 6;
	[Export] public Vector2 SweepHitboxCenter = new(58f, 14f);
	[Export] public Vector2 SweepHitboxSize = new(118f, 46f);
	[Export] public float ChainChance = 0.5f;

	[ExportGroup("Tajo")]
	[Export] public float CutWindup = 0.5f;
	[Export] public float QuickWindupScale = 0.45f;
	[Export] public int CutHoldFrame = 5;
	[Export] public int CutHitFrame = 7;
	[Export] public Vector2 CutHitboxCenter = new(45f, -2f);
	[Export] public Vector2 CutHitboxSize = new(132f, 96f);

	[ExportGroup("Media luna")]
	[Export] public float CrescentRange = 170f;
	[Export] public float CrescentCooldown = 5f;
	[Export] public float CrescentChance = 0.4f;
	[Export] public int CrescentHitFrame = 9;
	[Export] public Vector2 CrescentHitboxCenter = new(60f, -32f);
	[Export] public Vector2 CrescentHitboxSize = new(170f, 150f);
	[Export] public int FlameCount = 3;
	[Export] public float FlameStartOffset = 150f;
	[Export] public float FlameSpacing = 55f;
	[Export] public float FlameInterval = 0.1f;
	[Export] public float FlameWarning = 0.3f;
	[Export] public Vector2 FlameTileSize = new(52f, 70f);
	[Export] public float FlameDamageMultiplier = 1.2f;

	[ExportGroup("Look")]
	[Export] public float AttackFps = 14f;
	[Export] public float HurtDuration = 0.25f;
	[Export] public Color TelegraphTint = new(0.6f, 2.4f, 3f, 1f);

	private enum Move { Sweep, Cut, Crescent }
	private enum State { Dormant, Rising, Awake }

	private static readonly RandomNumberGenerator Rng = new();

	private State _state = State.Awake;
	private Move _nextMove;
	private float _crescentCooldownTimer;
	private float _hurtTimer;
	private Tween _telegraphTween;
	private CollisionShape2D _hitboxShape;

	static GhostWarrior4()
	{
		Rng.Randomize();
	}

	// The champion is a heavy: tanks hits through its own swings and takes a long string to break.
	public GhostWarrior4()
	{
		HyperArmorWhileAttacking = true;
		MaxPoise = 80f;
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_hitboxShape = AttackHitbox.GetNode<CollisionShape2D>("CollisionShape2D");
		_crescentCooldownTimer = Rng.RandfRange(1.5f, 3f);
		Stats.HitTaken += _ =>
		{
			if (_state == State.Dormant)
				Wake();
			else if (!Attacking && _state == State.Awake)
				_hurtTimer = HurtDuration;
		};
		if (StartDormant)
		{
			_state = State.Dormant;
			Sprite.Play("kneel");
			Sprite.Stop();
			Sprite.Frame = Sprite.SpriteFrames.GetFrameCount("kneel") - 1;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;
		if (_state != State.Awake)
		{
			// Kneeling or drawing the sword: no AI, just stay put on the floor.
			PlayerDetected = false;
			Velocity = new Vector2(0f, IsOnFloor() ? 0f : Velocity.Y + Gravity * (float)delta);
			MoveAndSlide();
			if (_state == State.Dormant && PlayerReads.Find(this) is { } sleeper
				&& sleeper.GlobalPosition.DistanceTo(GlobalPosition) <= WakeRange)
				Wake();
			return;
		}

		float dt = (float)delta;
		_hurtTimer -= dt;
		_crescentCooldownTimer -= dt;
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval)
			return;
		Think();
	}

	private async void Wake()
	{
		if (_state != State.Dormant)
			return;
		_state = State.Rising;
		Sprite.Play("rise");
		await ToSignal(Sprite, AnimatedSprite2D.SignalName.AnimationFinished);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;
		_state = State.Awake;
		Sprite.Play("idle");
	}

	// All its attacks are chosen here; MeleeEnemy's plain in-range swing never fires.
	protected override bool ReadyToCommitAttack() => false;

	private void Think()
	{
		var player = PlayerRef;
		if (player is null || !PlayerDetected || Attacking || !CanAttack || _hurtTimer > 0f || !IsOnFloor())
			return;
		float absDistance = Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X);

		if (PlayerReads.WhiffedParryToward(player, GlobalPosition.X) && absDistance <= AttackRange * 1.2f)
		{
			Commit(Move.Cut, QuickWindupScale);
			return;
		}
		if (_crescentCooldownTimer <= 0f && absDistance <= CrescentRange && absDistance > AttackRange * 0.6f
			&& Rng.Randf() < CrescentChance)
		{
			_crescentCooldownTimer = CrescentCooldown;
			Commit(Move.Crescent, 1f);
			return;
		}
		if (absDistance <= AttackRange)
			Commit(Rng.Randf() < 0.55f ? Move.Sweep : Move.Cut, 1f);
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
		return base.ComputeMoveX(player, distanceX, currentVelocityX, delta);
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null || Attacking || _state != State.Awake)
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
				case Move.Sweep:
					if (await Swing("attack1", SweepHoldFrame, SweepHitFrame, SweepWindup * windupScale, SweepHitboxCenter, SweepHitboxSize)
						&& PlayerInReach() && Rng.Randf() < ChainChance)
						await Swing("attack2", CutHoldFrame, CutHitFrame, CutWindup * 0.5f, CutHitboxCenter, CutHitboxSize);
					break;
				case Move.Crescent:
					await Crescent();
					break;
				default:
					await Swing("attack2", CutHoldFrame, CutHitFrame, CutWindup * windupScale, CutHitboxCenter, CutHitboxSize);
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

	private bool PlayerInReach() =>
		PlayerRef is { } player && Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X) <= AttackRange * 1.1f;

	// Hold the wind-up frame blinking, play on, hit for two frames from hitFrame, then let the clip
	// end. False when the enemy went away mid-swing.
	private async Task<bool> Swing(string animation, int holdFrame, int hitFrame, float windup, Vector2 hitboxCenter, Vector2 hitboxSize)
	{
		Sprite.Play(animation);
		Sprite.Stop();
		Sprite.Frame = holdFrame;
		Telegraph(windup);
		if (!await Wait(windup))
			return false;
		ClearTelegraph();
		Sprite.Play(animation);
		Sprite.Frame = holdFrame;
		if (!await Wait((hitFrame - holdFrame) / AttackFps))
			return false;
		SetHitbox(hitboxCenter, hitboxSize);
		AttackHitbox.Activate(Stats);
		if (!await Wait(2f / AttackFps))
			return false;
		AttackHitbox.Deactivate();
		return await Wait(RemainingClip(animation, hitFrame + 2));
	}

	private async Task Crescent()
	{
		Sprite.Play("attack3");
		float charge = CrescentHitFrame / AttackFps;
		Telegraph(charge);
		if (!await Wait(charge))
			return;
		ClearTelegraph();
		SetHitbox(CrescentHitboxCenter, CrescentHitboxSize);
		AttackHitbox.Activate(Stats);
		float direction = FacingRight ? 1f : -1f;
		Vector2 ground = GlobalPosition + new Vector2(direction * FlameStartOffset, BodyBottom);
		int damage = Mathf.Max(1, Mathf.RoundToInt(Stats.AttackPower * FlameDamageMultiplier));
		_ = OscuroEruption.Run(this, Stats, ground, direction, FlameCount, FlameSpacing, FlameInterval, FlameTileSize,
			FlameWarning, damage);
		if (!await Wait(2f / AttackFps))
			return;
		AttackHitbox.Deactivate();
		await Wait(RemainingClip("attack3", CrescentHitFrame + 2));
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
