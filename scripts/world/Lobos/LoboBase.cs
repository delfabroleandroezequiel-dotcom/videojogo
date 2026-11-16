using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Shared plumbing for the PixelLab wolves (112x64 cells, facing right). Like GuerreroReino, this
// class owns NO kit: every wolf decides and plays its own moves in Kit() (its own .cs). What lives
// here is only what every kit needs the same way:
// - the attack lifecycle (attack slot, cooldown, clean abort when stunned/flinched/executed),
// - the two moves every wolf's sheet has: the lunging bite and the pounce (leap at the throat),
//   both flashing the body first, with an amber mouth ribbon and fangs snapping at the hit frame,
// - prowling footwork between attacks (feints in and out instead of walking straight in),
// - the pack: wolves find each other (group "lobos"), can be hastened (Gris's howl, the Alfa's
//   growl) or scared off (the Alfa dying),
// - hiding (burrowed / turned to smoke: invisible, untouchable) and safe teleports on the floor,
// - sounds: VoiceCategory "Gruñido" on spotting the player and before a pounce, "Ataque" on some
//   bites, and the jaw clack (Combat/Mordida) on every bite's hit frame.
public abstract partial class LoboBase : MeleeEnemy
{
	[ExportGroup("Lobo")]
	[Export] public Color TrailGlowColor = new(1f, 0.5f, 0.04f, 1f);
	[Export] public Color TrailCoreColor = new(1f, 0.92f, 0.6f, 1f);
	// Telegraph tint (self_modulate, HDR): the body flashes bright before every attack.
	[Export] public Color TellColor = new(2.2f, 2.2f, 2.2f, 1f);
	[Export] public float AttackFps = 14f;
	[Export(PropertyHint.Range, "0,1")] public float AttackBarkChance = 0.4f;

	// Bite, measured per sprite from the "bite" frames: the jaws reach full extension on
	// BiteHitFrame; BiteReach/BiteY place the hitbox's centre at the mouth (body coords, facing right).
	[ExportGroup("Mordida")]
	[Export] public int BiteHitFrame = 4;
	[Export] public float BiteReach = 34f;
	[Export] public float BiteY = -4f;
	[Export] public Vector2 BiteSize = new(30f, 24f);
	// Bites without rushing in when the player is this close.
	[Export] public float BiteRange = 70f;
	[Export] public float BiteWindup = 0.32f;
	[Export] public float BiteLungeSpeed = 240f;

	[ExportGroup("Salto")]
	[Export] public float PounceTelegraph = 0.34f;
	[Export] public float PounceVelocityY = -280f;
	[Export] public float PounceMaxSpeedX = 330f;

	// Between attacks it hovers at about this distance, feinting in and out.
	// Las Raíces: wolves that fled into the roots. While Neutral they never attack: when the player
	// comes within NeutralFearRadius they growl and back away. Hitting one (or a packmate within
	// NeutralAlarmRadius) turns them hostile for good.
	[ExportGroup("Neutral")]
	[Export] public bool Neutral;
	[Export] public float NeutralFearRadius = 230f;
	[Export] public float NeutralAlarmRadius = 520f;

	[ExportGroup("Merodeo")]
	[Export] public float ProwlDistance = 130f;
	[Export] public float ProwlSpeedFactor = 0.45f;

	// Bite clack: a shared combat folder, not the voice (every canine's jaws sound the same).
	private const string BiteSoundCategory = "Combat/Mordida";
	private const string BiteSoundKey = "Mordida";
	private const string PackGroup = "lobos";

	protected EnemySlashTrail Trail;
	private EnemySlashTrail _upperFang;
	private EnemySlashTrail _lowerFang;
	private RectangleShape2D _hitShape;
	private Tween _flashTween;

	private float _lungeTimer;
	private float _lungeSpeed;
	private float _rushTimer;
	private float _scaredTimer;
	private float _prowlTimer;
	private int _prowlMove;

	private float _hasteTimer;
	private float _hasteCooldownMul = 1f;
	private float _hasteSpeedMul = 1f;

	private bool _wasDetected;
	private ulong _lostSinceMsec;
	private bool _lostOnce;
	private static ulong s_nextGrowlMsec;

	private Line2D _ribbonGlow;
	private Line2D _ribbonCore;
	private readonly List<Vector2> _ribbonPoints = new();
	private readonly List<float> _ribbonAges = new();
	private bool _ribbonEmitting;
	private const float RibbonLifetime = 0.16f;

	protected bool Hidden { get; private set; }
	private uint _bodyLayer;

	private static readonly RandomNumberGenerator Rng = new();

	static LoboBase()
	{
		Rng.Randomize();
	}

	protected float Forward => FacingRight ? 1f : -1f;
	protected Metroidvania.Player.Player Target => PlayerRef;
	protected float TargetDistanceX => Target is null ? float.MaxValue : Mathf.Abs(Target.GlobalPosition.X - GlobalPosition.X);
	protected float TowardTarget => Target is null ? Forward : Mathf.Sign(Target.GlobalPosition.X - GlobalPosition.X);
	protected static float Roll() => Rng.Randf();
	protected static float RandRange(float a, float b) => Rng.RandfRange(a, b);
	protected float SpeedMul => _hasteTimer > 0f ? _hasteSpeedMul : 1f;
	protected bool Hastened => _hasteTimer > 0f;
	protected bool Scared => _scaredTimer > 0f;

	// True when the player has his back to this wolf.
	protected bool TargetBackTurned => Target is { } p && (p.IsFacingRight ? GlobalPosition.X < p.GlobalPosition.X : GlobalPosition.X > p.GlobalPosition.X);

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		AddToGroup(PackGroup);
		_bodyLayer = CollisionLayer;

		Trail = new EnemySlashTrail { CoreWidth = 2.5f, GlowWidth = 8f, GlowColor = TrailGlowColor, CoreColor = TrailCoreColor, SparkAmount = 10 };
		Visual.AddChild(Trail);
		_upperFang = MakeFangTrail();
		_lowerFang = MakeFangTrail();

		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		_ribbonGlow = MakeRibbonLine(7f, TrailGlowColor, additive);
		_ribbonCore = MakeRibbonLine(2f, TrailCoreColor, additive);

		// Own copy of the hitbox shape: strikes resize it, and the .tscn sub-resource is shared by
		// every instance of the scene.
		var hitCollision = AttackHitbox.GetNode<CollisionShape2D>("CollisionShape2D");
		_hitShape = (RectangleShape2D)hitCollision.Shape.Duplicate();
		hitCollision.Shape = _hitShape;

		Stats.HitTaken += _ => BecomeHostile();
	}

	// Ends the neutral truce for this wolf and every neutral packmate close enough to see it.
	public void BecomeHostile()
	{
		if (!Neutral || IsQueuedForRemoval)
			return;
		Neutral = false;
		CanAttack = true;
		Bark("Gruñido", 1.2f);
		foreach (LoboBase mate in PackMates(NeutralAlarmRadius))
			mate.BecomeHostile();
	}

	private float _neutralGrowlTimer;

	private void UpdateNeutral(float dt)
	{
		CanAttack = false;
		if (_neutralGrowlTimer > 0f)
			_neutralGrowlTimer -= dt;
		if (EnemyTargeting.Current(GetTree()) is not Node2D player
			|| GlobalPosition.DistanceTo(player.GlobalPosition) > NeutralFearRadius)
			return;
		Scare(0.4f);
		if (_neutralGrowlTimer <= 0f)
		{
			_neutralGrowlTimer = RandRange(2.5f, 4f);
			Bark("Gruñido", 1.2f);
		}
	}

	private EnemySlashTrail MakeFangTrail()
	{
		var trail = new EnemySlashTrail
		{
			CoreWidth = 1.5f,
			GlowWidth = 4.5f,
			GlowColor = TrailGlowColor,
			CoreColor = TrailCoreColor,
			MinPointSpacing = 1f,
			SparkAmount = 6,
			SparkLifetime = 0.2,
		};
		Visual.AddChild(trail);
		return trail;
	}

	private Line2D MakeRibbonLine(float width, Color color, Material material)
	{
		var fade = new Gradient();
		fade.SetColor(0, new Color(1f, 1f, 1f, 0f));
		fade.SetColor(1, new Color(1f, 1f, 1f, 1f));
		var taper = new Curve();
		taper.AddPoint(new Vector2(0f, 0.1f));
		taper.AddPoint(new Vector2(1f, 1f));
		var line = new Line2D
		{
			TopLevel = true,
			Material = material,
			Width = width,
			WidthCurve = taper,
			Gradient = fade,
			BeginCapMode = Line2D.LineCapMode.Round,
			EndCapMode = Line2D.LineCapMode.Round,
			Modulate = color,
		};
		AddChild(line);
		return line;
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval)
			return;
		float dt = (float)delta;
		if (_lungeTimer > 0f)
			_lungeTimer -= dt;
		if (_rushTimer > 0f)
			_rushTimer -= dt;
		if (_scaredTimer > 0f)
			_scaredTimer -= dt;
		if (_hasteTimer > 0f)
			_hasteTimer -= dt;
		if (Neutral)
		{
			UpdateNeutral(dt);
			UpdateRibbon(dt);
			return;
		}
		UpdateSpotted();
		UpdateRibbon(dt);
		if (!Attacking && !Hopping && !IsFlinching && !IsStunned && PlayerDetected)
			Think(dt);
	}

	// Per-wolf reactions outside its attacks (punishing a whiff, etc). Runs every physics frame
	// while it's hunting and not mid-attack.
	protected virtual void Think(float dt) { }

	private void UpdateSpotted()
	{
		ulong now = Time.GetTicksMsec();
		if (PlayerDetected && !_wasDetected && (!_lostOnce || now - _lostSinceMsec > 4000))
		{
			// One growl at a time across the whole pack, so five wolves don't growl in chorus.
			if (now >= s_nextGrowlMsec)
			{
				s_nextGrowlMsec = now + 1500;
				Bark("Gruñido", 1.2f);
			}
			OnSpottedPlayer();
		}
		if (!PlayerDetected && _wasDetected)
		{
			_lostSinceMsec = now;
			_lostOnce = true;
		}
		_wasDetected = PlayerDetected;
	}

	// Hook: the first frame it spots the player.
	protected virtual void OnSpottedPlayer() { }

	protected void Bark(string key, float maxSeconds = 1.2f)
	{
		if (!string.IsNullOrEmpty(VoiceCategory))
			Sfx.PlayIfExistsAt(this, VoiceCategory, key, maxSeconds: maxSeconds);
	}

	// ── Pack ────────────────────────────────────────────────────────────────────────────────

	protected IEnumerable<LoboBase> PackMates(float radius)
	{
		foreach (Node node in GetTree().GetNodesInGroup(PackGroup))
		{
			if (node is LoboBase mate && mate != this && IsInstanceValid(mate) && !mate.IsQueuedForRemoval
				&& mate.Stats is { CurrentHealth: > 0 } && mate.GlobalPosition.DistanceTo(GlobalPosition) <= radius)
				yield return mate;
		}
	}

	protected int CountPackMates(float radius)
	{
		int count = 0;
		foreach (LoboBase _ in PackMates(radius))
			count++;
		return count;
	}

	// Faster attacks (cooldown × cooldownMul) and footwork (speed × speedMul) for `seconds`, with a
	// coloured pulse on the body so the player sees who got the buff.
	public void Hasten(float cooldownMul, float speedMul, float seconds, Color pulse)
	{
		if (IsQueuedForRemoval)
			return;
		_hasteCooldownMul = cooldownMul;
		_hasteSpeedMul = speedMul;
		_hasteTimer = Mathf.Max(_hasteTimer, seconds);
		Alert(seconds);
		Tween tween = CreateTween();
		tween.TweenProperty(Visual, "modulate", pulse, 0.12f);
		tween.TweenProperty(Visual, "modulate", Colors.White, 0.35f);
	}

	// Backs off away from the player for `seconds` (the pack losing its alpha).
	public void Scare(float seconds)
	{
		if (IsQueuedForRemoval)
			return;
		_scaredTimer = Mathf.Max(_scaredTimer, seconds);
		_hasteTimer = 0f;
	}

	// ── Movement ────────────────────────────────────────────────────────────────────────────

	// Moves `speed` px/s along the facing direction for `seconds` (negative speed = backwards).
	protected void Lunge(float speed, float seconds)
	{
		_lungeSpeed = speed;
		_lungeTimer = seconds;
	}

	protected void StopLunge() => _lungeTimer = 0f;

	protected sealed override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_lungeTimer > 0f)
		{
			float dir = Mathf.Sign(_lungeSpeed) * Forward;
			return CanStepTo(dir) ? Mathf.Abs(_lungeSpeed) * dir : 0f;
		}
		if (Hopping)
			return currentVelocityX;
		float toward = Mathf.Sign(distanceX);
		if (_rushTimer > 0f)
			return CanStepTo(toward) ? toward * MoveSpeed * 1.15f * SpeedMul : 0f;
		if (Attacking || Hidden)
			return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
		if (_scaredTimer > 0f)
			return CanStepTo(-toward) ? -toward * MoveSpeed * 0.8f : Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
		// A neutral wolf never walks toward the player, it just holds its ground until it's scared off.
		if (Neutral)
			return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
		return MoveX(player, distanceX, currentVelocityX, delta);
	}

	// A wolf that moves away from the player turns tail and runs that way instead of sliding
	// backwards facing him (it looked like a moonwalk). Attacks, lunges, hops and flinches keep
	// facing the player as before. Velocity is last frame's, which is plenty for a facing choice.
	protected override bool DesiredFacingRight(Node2D player, float distanceX)
	{
		float vx = Velocity.X;
		bool movingAway = Mathf.Abs(vx) > 5f && Mathf.Sign(vx) != Mathf.Sign(distanceX);
		if (movingAway && !Attacking && _lungeTimer <= 0f && !Hopping && !IsFlinching)
			return vx > 0f;
		return distanceX >= 0;
	}

	// Per-wolf footwork; default = run in from afar, then prowl around ProwlDistance with feints.
	protected virtual float MoveX(Node2D player, float distanceX, float currentVelocityX, double delta) =>
		ProwlMoveX(distanceX, currentVelocityX, (float)delta, ProwlDistance);

	protected float ProwlMoveX(float distanceX, float currentVelocityX, float dt, float prowlDistance)
	{
		float abs = Mathf.Abs(distanceX);
		float toward = Mathf.Sign(distanceX);
		float speed = MoveSpeed * SpeedMul;
		if (abs > prowlDistance + 25f)
			return CanStepTo(toward) ? toward * speed : 0f;

		_prowlTimer -= dt;
		if (_prowlTimer <= 0f)
		{
			float roll = Roll();
			_prowlMove = roll < 0.3f ? 1 : roll < 0.6f ? -1 : 0;
			// Backing off is a real turn-and-trot (see DesiredFacingRight), so it lasts long enough
			// to read instead of a quick flip back and forth.
			_prowlTimer = _prowlMove < 0 ? RandRange(0.55f, 0.9f) : RandRange(0.25f, 0.55f);
		}
		if (abs < BiteRange * 0.7f && _prowlMove > 0)
			_prowlMove = 0;
		float dir = _prowlMove * toward;
		return _prowlMove != 0 && CanStepTo(dir) ? dir * speed * ProwlSpeedFactor : Mathf.MoveToward(currentVelocityX, 0f, speed);
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null || Attacking || IsFlinching || Hidden)
			return;
		if (Hopping && !IsOnFloor())
		{
			if (Sprite.Animation != "pounce")
				Pose("pounce", 6);
			return;
		}
		float speed = Mathf.Abs(velocity.X);
		string anim = speed <= 5f ? "idle" : MoveAnimation(speed);
		Sprite.SpeedScale = anim == "idle" ? 1f : Mathf.Clamp(speed / Mathf.Max(1f, MoveSpeed), 0.55f, 1.3f);
		if (Sprite.Animation != anim || !Sprite.IsPlaying())
			Sprite.Play(anim);
	}

	// Which loop plays while moving at `speed` (Negro creeps with its own "stalk").
	protected virtual string MoveAnimation(float speed) => "run";

	// ── Attack lifecycle ────────────────────────────────────────────────────────────────────

	// The wolf's whole decision + moves. Return false when it bailed out (Wait() said stop).
	protected abstract Task<bool> Kit();

	// Extra time added to the next cooldown by the kit; a negative NextCooldown overrides the whole
	// cooldown (a wolf that only repositioned shouldn't wait a full attack cooldown).
	protected float ExtraCooldown;
	protected float NextCooldown = -1f;
	// Wind-up multiplier for this attack (punishes come out faster): MeleeEnemy's WindupScale,
	// latched at the start of the kit.
	protected float KitWindupScale = 1f;

	protected sealed override async Task Attack()
	{
		if (AttackWasInterrupted)
			return;
		Attacking = true;
		CanAttack = false;
		ExtraCooldown = 0f;
		NextCooldown = -1f;
		KitWindupScale = WindupScale;
		WindupScale = 1f;
		if (Sprite is not null)
			Sprite.SpeedScale = 1f;
		try
		{
			await Kit();
		}
		finally
		{
			if (IsInstanceValid(this))
			{
				if (Hidden)
					SetHidden(false);
				if (!AttackWasInterrupted)
					EndAttackState();
			}
		}

		if (!IsInstanceValid(this) || IsQueuedForRemoval || AttackWasInterrupted)
			return;
		float jitter = 1f + Rng.RandfRange(-AttackCooldownJitter, AttackCooldownJitter);
		float cooldown = NextCooldown >= 0f ? NextCooldown : AttackCooldown * jitter * (Hastened ? _hasteCooldownMul : 1f) + ExtraCooldown;
		await ToSignal(GetTree().CreateTimer(cooldown), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this) && !AttackWasInterrupted)
			CanAttack = true;
	}

	private void EndAttackState()
	{
		Attacking = false;
		StopLunge();
		_rushTimer = 0f;
		_ribbonEmitting = false;
		AttackHitbox.Deactivate();
		ClearFlash();
		if (HoldingAttackSlot)
		{
			EnemyCombatCoordinator.ReleaseAttackSlot();
			HoldingAttackSlot = false;
		}
	}

	// False = stop the kit now (dead, stunned, frozen for an execution, flinched out of the move).
	protected async Task<bool> Wait(float seconds)
	{
		if (seconds > 0f)
			await ToSignal(GetTree().CreateTimer(seconds, processAlways: false), SceneTreeTimer.SignalName.Timeout);
		return StillGoing;
	}

	protected async Task<bool> WaitPhysicsFrame()
	{
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		return StillGoing;
	}

	protected bool StillGoing => IsInstanceValid(this) && !IsQueuedForRemoval && !AttackWasInterrupted && !IsStunned
		&& ProcessMode != ProcessModeEnum.Disabled && Stats.CurrentHealth > 0;

	protected void Pose(string anim, int frame)
	{
		Sprite.Play(anim);
		Sprite.Stop();
		Sprite.Frame = frame;
	}

	protected void TurnToTarget()
	{
		if (Target is null)
			return;
		FacingRight = Target.GlobalPosition.X >= GlobalPosition.X;
		Visual.Scale = new Vector2(FacingRight ? 1 : -1, Visual.Scale.Y);
	}

	// Holds `frame` of `anim`, flashing, for `seconds`.
	protected async Task<bool> Telegraph(string anim, int frame, float seconds)
	{
		Pose(anim, frame);
		FlashBody(seconds);
		return await Wait(seconds);
	}

	// Plays frames [from, to] of `anim` at AttackFps (or `fps`); `onFrame` runs as each one shows.
	protected async Task<bool> PlayFrames(string anim, int from, int to, float fps = 0f, System.Action<int> onFrame = null)
	{
		float step = 1f / (fps > 0f ? fps : AttackFps);
		int dir = to >= from ? 1 : -1;
		for (int f = from; f != to + dir; f += dir)
		{
			Pose(anim, f);
			onFrame?.Invoke(f);
			if (!await Wait(step))
				return false;
		}
		return true;
	}

	protected void HitOn(float reach, Vector2 size, float y = 0f, float damage = 1f, System.Action<Vector2> onHit = null)
	{
		if (AttackWasInterrupted)
			return;
		_hitShape.Size = size;
		AttackHitbox.Position = new Vector2(Forward * reach, y);
		AttackHitbox.Activate(Stats, damageMultiplier: damage, customImpactEffect: onHit);
	}

	// Sprint toward the player (ComputeMoveX) for up to `seconds`, playing the run loop.
	protected void StartRush(float seconds)
	{
		_rushTimer = seconds;
		Sprite.SpeedScale = 1.2f;
		Sprite.Play("run");
	}

	protected void StopRush()
	{
		_rushTimer = 0f;
		if (Sprite is not null)
			Sprite.SpeedScale = 1f;
	}

	protected bool Rushing => _rushTimer > 0f;

	// Runs toward the player at sprint speed until within `distance` (true) or `maxTime` runs out,
	// a wall/ledge stops it, or the player gets out of reach (false).
	protected async Task<bool> RushIn(float distance, float maxTime)
	{
		StartRush(maxTime);
		while (_rushTimer > 0f)
		{
			TurnToTarget();
			if (TargetDistanceX <= distance)
			{
				_rushTimer = 0f;
				Sprite.SpeedScale = 1f;
				return true;
			}
			if (!CanStepTo(TowardTarget) || !await WaitPhysicsFrame())
				break;
		}
		_rushTimer = 0f;
		if (Sprite is not null)
			Sprite.SpeedScale = 1f;
		return false;
	}

	// ── The two moves every wolf has ────────────────────────────────────────────────────────

	// The lunging bite: crouch back (bite frames up to BiteHitFrame-1, held there and flashing), then lunge forward
	// with the jaws, hitbox live on BiteHitFrame and the frame after. `onHit` runs when it connects.
	protected async Task<bool> Bite(float windupScale = 1f, float damage = 1f, System.Action<Vector2> onHit = null)
	{
		TurnToTarget();
		float windup = BiteWindup * windupScale * KitWindupScale;
		if (!await PlayFrames("bite", 1, Mathf.Max(1, BiteHitFrame - 2)))
			return false;
		if (!await Telegraph("bite", BiteHitFrame - 1, windup))
			return false;
		ClearFlash();
		if (Roll() < AttackBarkChance)
			Bark("Ataque", 0.7f);

		Lunge(BiteLungeSpeed, 3f / AttackFps);
		_ribbonEmitting = true;
		int last = Sprite.SpriteFrames.GetFrameCount("bite") - 1;
		bool ok = await PlayFrames("bite", BiteHitFrame, last, onFrame: f =>
		{
			if (f == BiteHitFrame)
			{
				HitOn(BiteReach, BiteSize, BiteY, damage, onHit);
				SnapJaws();
			}
			else if (f == BiteHitFrame + 2)
			{
				AttackHitbox.Deactivate();
				_ribbonEmitting = false;
			}
		});
		AttackHitbox.Deactivate();
		_ribbonEmitting = false;
		StopLunge();
		return ok;
	}

	protected Vector2 MouthLocal => new(BiteReach + BiteSize.X * 0.35f, BiteY);

	private void SnapJaws()
	{
		Sfx.PlayIfExistsAt(this, BiteSoundCategory, BiteSoundKey, maxSeconds: 0.45f);
		const float radius = 11f;
		Vector2 mouth = MouthLocal;
		Vector2 center = mouth - new Vector2(radius, 0f);
		_upperFang.Play(center, radius, -110f, -5f, 0.07f);
		_lowerFang.Play(center, radius, 110f, 5f, 0.07f);
		SpawnGlow(Visual.ToGlobal(mouth), TrailCoreColor, 0.5f, 0.14f);
	}

	// Leap at the throat: crouch (pounce frames 1-3, flashing, with a growl), then a real jump arc
	// aimed at where the player stands, jaws live the whole flight. Fizzles if the arc has no safe
	// landing (pit/ledge).
	protected async Task<bool> Pounce(float damage = 1f, System.Action<Vector2> onHit = null)
	{
		var target = Target;
		if (target is null)
			return true;
		TurnToTarget();
		if (!await PlayFrames("pounce", 1, 2))
			return false;
		Bark("Gruñido", PounceTelegraph + 0.3f);
		if (!await Telegraph("pounce", 3, PounceTelegraph * KitWindupScale))
			return false;
		ClearFlash();

		float airTime = 2f * -PounceVelocityY / Mathf.Max(1f, Gravity);
		float dx = target.GlobalPosition.X - GlobalPosition.X;
		var launch = new Vector2(Mathf.Clamp(dx / airTime, -PounceMaxSpeedX, PounceMaxSpeedX), PounceVelocityY);
		if (!TryAttackHop(launch))
			return true;

		Pose("pounce", 5);
		HitOn(BiteReach * 0.85f, BiteSize + new Vector2(6f, 6f), BiteY, damage, onHit);
		_ribbonEmitting = true;
		float elapsed = 0f;
		bool snapped = false;
		while (Hopping && elapsed < airTime + 0.4f)
		{
			if (!await WaitPhysicsFrame())
				return false;
			elapsed += (float)GetPhysicsProcessDeltaTime();
			int frame = 5 + Mathf.Clamp((int)(elapsed / airTime * 4f), 0, 3);
			if (Sprite.Frame != frame)
				Pose("pounce", frame);
			if (!snapped && elapsed >= airTime * 0.45f)
			{
				snapped = true;
				SnapJaws();
			}
		}
		AttackHitbox.Deactivate();
		_ribbonEmitting = false;
		return await PlayFrames("pounce", 9, Sprite.SpriteFrames.GetFrameCount("pounce") - 1);
	}

	// Hops backwards out of reach (true if it found ground to land on).
	protected bool HopBack(float speedX = 200f, float speedY = -220f) =>
		TryAttackHop(new Vector2(-Forward * speedX, speedY));

	protected async Task<bool> WaitLanded(float maxSeconds = 1.2f)
	{
		float elapsed = 0f;
		while (Hopping && elapsed < maxSeconds)
		{
			if (!await WaitPhysicsFrame())
				return false;
			elapsed += (float)GetPhysicsProcessDeltaTime();
		}
		return true;
	}

	// ── Hiding and teleports (burrow, smoke) ────────────────────────────────────────────────

	protected void SetHidden(bool hidden)
	{
		Hidden = hidden;
		Visual.Visible = !hidden;
		Stats.ExternalInvulnerable = hidden;
		CollisionLayer = hidden ? 0u : _bodyLayer;
		foreach (string bar in new[] { "HealthBar", "StaminaBar" })
			if (GetNodeOrNull<CanvasItem>(bar) is { } node)
				node.Visible = !hidden;
		if (hidden)
		{
			AttackHitbox.Deactivate();
			_ribbonEmitting = false;
		}
	}

	// The floor's Y under world X `x`, searched from a bit above this wolf's feet down to `maxDrop`
	// below them; null if there's none (pit).
	protected float? FloorAt(float x, float maxDrop = 40f)
	{
		float feet = GlobalPosition.Y + BodyBottom;
		var query = PhysicsRayQueryParameters2D.Create(new Vector2(x, feet - 30f), new Vector2(x, feet + maxDrop), 1u);
		query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
		var hit = GetWorld2D().DirectSpaceState.IntersectRay(query);
		return hit.Count == 0 ? null : ((Vector2)hit["position"]).Y;
	}

	// No wall between this wolf and world X `x` at body height.
	protected bool ClearPathTo(float x)
	{
		var from = GlobalPosition;
		var query = PhysicsRayQueryParameters2D.Create(from, new Vector2(x, from.Y), 1u);
		query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
		return GetWorld2D().DirectSpaceState.IntersectRay(query).Count == 0;
	}

	// Moves the wolf to stand on the floor at world X `x` if that spot is reachable at about the
	// same height (no walls in between, no pit, no ledge more than `maxStep` up or down).
	protected bool TryTeleportTo(float x, float maxStep = 28f)
	{
		if (!ClearPathTo(x) || FloorAt(x, maxStep) is not float floorY)
			return false;
		float feet = GlobalPosition.Y + BodyBottom;
		if (Mathf.Abs(floorY - feet) > maxStep)
			return false;
		GlobalPosition = new Vector2(x, floorY - BodyBottom);
		Velocity = Vector2.Zero;
		return true;
	}

	// ── Visuals ─────────────────────────────────────────────────────────────────────────────

	protected void FlashBody(float duration)
	{
		if (Sprite is null || duration <= 0f)
			return;
		_flashTween?.Kill();
		_flashTween = CreateTween();
		float quarter = duration / 4f;
		for (int i = 0; i < 2; i++)
		{
			_flashTween.TweenProperty(Sprite, "self_modulate", TellColor, quarter);
			_flashTween.TweenProperty(Sprite, "self_modulate", Colors.White, quarter);
		}
	}

	protected void ClearFlash()
	{
		_flashTween?.Kill();
		if (Sprite is not null)
			Sprite.SelfModulate = Colors.White;
	}

	protected void Shake(float strength, float duration)
	{
		if (PlayerRef is { } player && player.GlobalPosition.DistanceTo(GlobalPosition) < 700f)
			player.ShakeCamera(strength, duration);
	}

	protected Vector2 GroundPoint(float aheadX) => GlobalPosition + new Vector2(Forward * aheadX, BodyBottom);

	private static Texture2D _dot;
	protected static Texture2D Dot => _dot ??= GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres");
	private static CanvasItemMaterial _additive;
	protected static CanvasItemMaterial Additive => _additive ??= new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };

	// A soft additive glow that pops and fades at a world point.
	protected void SpawnGlow(Vector2 globalPosition, Color color, float size, float duration)
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		var glow = new Sprite2D { Texture = Dot, Material = Additive, Modulate = color, GlobalPosition = globalPosition, Scale = Vector2.One * size * 0.3f, ZIndex = 5 };
		scene.AddChild(glow);
		glow.GlobalPosition = globalPosition;
		Tween tween = glow.CreateTween();
		tween.TweenProperty(glow, "scale", Vector2.One * size, duration * 0.5f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
		tween.Parallel().TweenProperty(glow, "modulate", new Color(color, 0f), duration);
		tween.TweenCallback(Callable.From(glow.QueueFree));
	}

	// `count` soft dots flung from `origin` (world) with random velocities around `direction`,
	// slowing and fading — spores, frost, smoke, sand. Purely visual.
	protected void SpawnPuffs(Vector2 origin, int count, Vector2 direction, float spreadDeg, float speedMin, float speedMax,
		Color color, float size, float lifetime, bool additive = true, float gravity = 0f)
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		for (int i = 0; i < count; i++)
		{
			float angle = Mathf.DegToRad(RandRange(-spreadDeg, spreadDeg));
			Vector2 velocity = direction.Normalized().Rotated(angle) * RandRange(speedMin, speedMax);
			float s = size * RandRange(0.6f, 1.3f);
			var puff = new Sprite2D
			{
				Texture = Dot,
				Material = additive ? Additive : null,
				Modulate = color,
				Scale = Vector2.One * s * 0.5f,
				ZIndex = 4,
			};
			scene.AddChild(puff);
			puff.GlobalPosition = origin + new Vector2(RandRange(-4f, 4f), RandRange(-4f, 4f));
			float life = lifetime * RandRange(0.7f, 1.2f);
			Vector2 end = puff.GlobalPosition + velocity * life + new Vector2(0f, 0.5f * gravity * life * life);
			Tween tween = puff.CreateTween();
			tween.TweenProperty(puff, "global_position", end, life).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
			tween.Parallel().TweenProperty(puff, "scale", Vector2.One * s, life);
			tween.Parallel().TweenProperty(puff, "modulate", new Color(color, 0f), life).SetEase(Tween.EaseType.In);
			tween.TweenCallback(Callable.From(puff.QueueFree));
		}
	}

	// A ring that expands from this wolf (howl, growl): purely visual.
	protected void SpawnRing(Vector2 localCenter, Color color, float radius, float duration)
	{
		// Points rebuilt each step (scaling the Line2D would fatten the stroke with the radius).
		var ring = new Line2D { Width = 2.5f, DefaultColor = color, Material = Additive, Closed = true, Position = localCenter, ZIndex = 4 };
		Visual.AddChild(ring);
		var points = new Vector2[32];
		void Grow(float t)
		{
			if (!IsInstanceValid(ring))
				return;
			float eased = 1f - Mathf.Pow(1f - t, 3f);
			float r = Mathf.Lerp(radius * 0.15f, radius, eased);
			for (int i = 0; i < points.Length; i++)
				points[i] = Vector2.FromAngle(Mathf.Tau * i / points.Length) * r;
			ring.Points = points;
			ring.Width = Mathf.Lerp(2.5f, 0.8f, t);
			ring.Modulate = new Color(1f, 1f, 1f, 1f - t);
		}
		Grow(0f);
		Tween tween = ring.CreateTween();
		tween.TweenMethod(Callable.From<float>(Grow), 0f, 1f, duration);
		tween.TweenCallback(Callable.From(ring.QueueFree));
	}

	// ── Mouth ribbon ────────────────────────────────────────────────────────────────────────

	private void UpdateRibbon(float dt)
	{
		for (int i = _ribbonAges.Count - 1; i >= 0; i--)
		{
			_ribbonAges[i] += dt;
			if (_ribbonAges[i] > RibbonLifetime)
			{
				_ribbonAges.RemoveAt(i);
				_ribbonPoints.RemoveAt(i);
			}
		}

		if (_ribbonEmitting && !Hidden)
		{
			Vector2 mouth = Visual.ToGlobal(MouthLocal);
			if (_ribbonPoints.Count == 0 || _ribbonPoints[^1].DistanceSquaredTo(mouth) > 1.5f * 1.5f)
			{
				_ribbonPoints.Add(mouth);
				_ribbonAges.Add(0f);
			}
		}

		Vector2[] points = _ribbonPoints.Count >= 2 ? _ribbonPoints.ToArray() : System.Array.Empty<Vector2>();
		_ribbonGlow.Points = points;
		_ribbonCore.Points = points;
	}

	protected override void OnDefeated()
	{
		ClearFlash();
		if (Hidden)
			SetHidden(false);
		base.OnDefeated();
	}
}
