using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Shared plumbing for the soldiers of Enterbill (blue) and Radem (red) — PixelLab sprites with
// attack/attack2 strips drawn at 14fps. This class owns NO moves: every soldier decides and plays
// its own kit in Kit() (its own .cs). What lives here is only what every kit needs the same way:
// - the attack lifecycle (attack slot, cooldown, clean abort when stunned/flinched/executed),
// - frame-driven strikes (hitbox switched on only on drawn frames, sized per strike),
// - telegraph flash, neon trail in the kingdom's colour, lunges, camera shake,
// - voice barks: "Alerta" the moment it spots the player, "Ataque" on some swings (VoiceCategory).
public abstract partial class GuerreroReino : MeleeEnemy
{
	[ExportGroup("Guerrero")]
	[Export] public Color TrailGlowColor = new(0.15f, 0.45f, 1f, 1f);
	[Export] public Color TrailCoreColor = new(0.7f, 0.85f, 1f, 1f);
	// Telegraph tint (self_modulate, HDR): bright white-blue reads as "it's coming".
	[Export] public Color TellColor = new(2.2f, 2.2f, 2.6f, 1f);
	[Export] public float AttackFps = 14f;
	[Export] public float AttackShoutChance = 0.35f;
	// Extra Defense while ShieldUp (shield bearers only; damage = attack - Defense, min 1).
	[Export] public int FrontShieldDefense = 0;

	protected EnemySlashTrail Trail;
	private RectangleShape2D _hitShape;
	private CollisionShape2D _hitCollision;
	private Tween _flashTween;
	private float _lungeTimer;
	private float _lungeSpeed;
	private bool _wasDetected;
	private ulong _lostSinceMsec;
	private bool _lostOnce;
	// One alert bark at a time across every soldier on screen, so a squad doesn't shout in chorus.
	private static ulong s_nextBarkMsec;
	private static readonly RandomNumberGenerator Rng = new();

	static GuerreroReino()
	{
		Rng.Randomize();
	}

	protected float Forward => FacingRight ? 1f : -1f;
	// The player, or the nearest soldier of the rival kingdom when one is closer (Enterbill vs Radem).
	protected CharacterBody2D Target => IsInstanceValid(_rival) ? _rival : AliadoDelJugador ? null : PlayerRef;

	// ── Kingdom rivalry ──
	private const string GroupName = "guerreros_reino";
	private const uint EnemyLayer = 1u << 2;
	public bool EsEnterbill => GetType().Name.StartsWith("GuerreroEnterbill");
	// On the player's side: never targets or hurts the player, only fights the rival kingdom
	// (and stands guard while none is around). Per instance, so a map can field an allied squad.
	[Export] public bool AliadoDelJugador;
	private GuerreroReino _rival;

	private bool IsAliveRival(GuerreroReino other) =>
		IsInstanceValid(other) && other != this && other.IsInsideTree() && other.EsEnterbill != EsEnterbill
		&& other.Stats is not null && other.Stats.CurrentHealth > 0;

	private void PickRival()
	{
		_rival = null;
		Node2D player = AliadoDelJugador ? null : base.CurrentTarget();
		float best = player is null ? DetectionRange : Mathf.Min(DetectionRange, player.GlobalPosition.DistanceTo(GlobalPosition));
		foreach (Node node in GetTree().GetNodesInGroup(GroupName))
		{
			if (node is not GuerreroReino other || !IsAliveRival(other))
				continue;
			float d = other.GlobalPosition.DistanceTo(GlobalPosition);
			if (d < best)
			{
				best = d;
				_rival = other;
			}
		}
	}

	protected override Node2D CurrentTarget()
	{
		PickRival();
		if (IsInstanceValid(_rival))
			return _rival;
		return AliadoDelJugador ? null : base.CurrentTarget();
	}
	protected float TargetDistanceX => Target is null ? float.MaxValue : Mathf.Abs(Target.GlobalPosition.X - GlobalPosition.X);
	protected bool TargetInFront => Target is not null && Mathf.Sign(Target.GlobalPosition.X - GlobalPosition.X) == Forward;
	protected static float Roll() => Rng.Randf();

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		Trail = new EnemySlashTrail { CoreWidth = 3f, GlowWidth = 10f, GlowColor = TrailGlowColor, CoreColor = TrailCoreColor, SparkAmount = 18 };
		Visual.AddChild(Trail);
		// Own copy of the hitbox shape: strikes resize it, and the .tscn sub-resource is shared by
		// every instance of the scene.
		_hitCollision = AttackHitbox.GetNode<CollisionShape2D>("CollisionShape2D");
		_hitShape = (RectangleShape2D)_hitCollision.Shape.Duplicate();
		_hitCollision.Shape = _hitShape;
		_baseDefense = Stats.Defense;
		// Rival kingdoms fight each other: the blade also reaches enemies, but only the other crown's.
		AddToGroup(GroupName);
		AttackHitbox.CollisionMask |= EnemyLayer;
		AttackHitbox.TargetFilter = body => (!AliadoDelJugador && body.IsInGroup("player")) || (body is GuerreroReino other && IsAliveRival(other));
	}

	private int _baseDefense;

	// Shield bearers: true while the shield covers the player's side (not mid-swing).
	protected virtual bool ShieldUp => false;

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval)
			return;
		if (_lungeTimer > 0f)
			_lungeTimer -= (float)delta;
		Stats.Defense = ShieldUp ? _baseDefense + FrontShieldDefense : _baseDefense;
		UpdateBarks();
	}

	private void UpdateBarks()
	{
		ulong now = Time.GetTicksMsec();
		if (PlayerDetected && !_wasDetected && (!_lostOnce || now - _lostSinceMsec > 4000))
		{
			if (now >= s_nextBarkMsec)
			{
				s_nextBarkMsec = now + 1500;
				Bark("Alerta", 2.2f);
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

	// Hook: the first frame it spots the player (after the bark). The captain rallies here.
	protected virtual void OnSpottedPlayer() { }

	protected void Bark(string key, float maxSeconds = 1.2f)
	{
		if (!string.IsNullOrEmpty(VoiceCategory))
			Sfx.PlayIfExistsAt(this, VoiceCategory, key, maxSeconds: maxSeconds);
	}

	protected void MaybeShout()
	{
		if (Roll() < AttackShoutChance)
			Bark("Ataque", 0.8f);
	}

	// ── Movement ────────────────────────────────────────────────────────────────────────────

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
		return MoveX(player, distanceX, currentVelocityX, delta);
	}

	// Per-soldier footwork; default = MeleeEnemy's approach to attack range.
	protected virtual float MoveX(Node2D player, float distanceX, float currentVelocityX, double delta) =>
		Hopping ? currentVelocityX : Attacking ? Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed) : DefaultMoveX(player, distanceX, currentVelocityX, delta);

	private float DefaultMoveX(Node2D player, float distanceX, float currentVelocityX, double delta) =>
		SpacingMoveX(distanceX, currentVelocityX, StopDistance, 0f);

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null || Attacking || IsFlinching)
			return;
		bool backingOff = Mathf.Abs(velocity.X) > 5f && Mathf.Sign(velocity.X) != Forward;
		string anim = Mathf.Abs(velocity.X) <= 5f ? "idle" : backingOff || Mathf.Abs(velocity.X) < MoveSpeed * 0.7f ? "walk" : "run";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	// ── Attack lifecycle ────────────────────────────────────────────────────────────────────

	// The soldier's whole decision + moves. Return false when it bailed out (Wait() said stop).
	protected abstract Task<bool> Kit();

	// Extra time added to the next cooldown by the kit (a heavy finisher, a reload...).
	protected float ExtraCooldown;

	protected sealed override async Task Attack()
	{
		if (AttackWasInterrupted)
			return;
		Attacking = true;
		CanAttack = false;
		ExtraCooldown = 0f;
		try
		{
			await Kit();
		}
		finally
		{
			if (IsInstanceValid(this) && !AttackWasInterrupted)
				EndAttackState();
		}

		if (!IsInstanceValid(this) || IsQueuedForRemoval || AttackWasInterrupted)
			return;
		float jitter = 1f + Rng.RandfRange(-AttackCooldownJitter, AttackCooldownJitter);
		await ToSignal(GetTree().CreateTimer(AttackCooldown * jitter + ExtraCooldown), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this) && !AttackWasInterrupted)
			CanAttack = true;
	}

	private void EndAttackState()
	{
		Attacking = false;
		StopLunge();
		AttackHitbox.Deactivate();
		ClearFlash();
		if (HoldingAttackSlot)
		{
			EnemyCombatCoordinator.ReleaseAttackSlot();
			HoldingAttackSlot = false;
		}
	}

	// False = stop the kit now (dead, stunned, frozen for an execution, flinched out of the swing).
	protected async Task<bool> Wait(float seconds)
	{
		if (seconds > 0f)
			await ToSignal(GetTree().CreateTimer(seconds, processAlways: false), SceneTreeTimer.SignalName.Timeout);
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

	// Holds the wind-up frame, flashing, for `seconds`.
	protected async Task<bool> Telegraph(string anim, int frame, float seconds)
	{
		Pose(anim, frame);
		FlashBody(seconds);
		return await Wait(seconds);
	}

	// Plays frames [from, to] of `anim` one by one at AttackFps (or `fps`). On `hitFrame` the
	// hitbox goes live for `activeFrames` frames, `reach` px in front, sized `size`, centred `y`
	// px below the body centre. The trail (if `trail` is set) starts one frame before the hit, so
	// it sweeps out to the hitbox's far edge as the blade lands.
	protected async Task<bool> Strike(string anim, int from, int to, int hitFrame, float reach, Vector2 size,
		float y = 0f, int activeFrames = 2, float damage = 1f, TrailArc? trail = null, float fps = 0f, float poiseMul = 1f)
	{
		float step = 1f / (fps > 0f ? fps : AttackFps);
		for (int f = from; f <= to; f++)
		{
			Pose(anim, f);
			if (trail is { } arc && f == Mathf.Max(from, hitFrame - 1))
				Trail.Play(arc.Center, arc.Radius, arc.Start, arc.End, arc.Duration, arc.RadiusY);
			if (f == hitFrame)
				HitOn(reach, size, y, damage);
			if (f == hitFrame + activeFrames)
				AttackHitbox.Deactivate();
			if (!await Wait(step))
				return false;
		}
		AttackHitbox.Deactivate();
		return true;
	}

	protected void HitOn(float reach, Vector2 size, float y = 0f, float damage = 1f)
	{
		if (AttackWasInterrupted)
			return;
		_hitShape.Size = size;
		AttackHitbox.Position = new Vector2(Forward * reach, y);
		AttackHitbox.Activate(Stats, damageMultiplier: damage);
	}

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

	protected override void OnDefeated()
	{
		ClearFlash();
		base.OnDefeated();
	}

	// Angles: 0° = front, -90° = up, +90° = down (EnemySlashTrail convention, mirrored by Visual).
	public readonly record struct TrailArc(Vector2 Center, float Radius, float Start, float End, float Duration = 0.1f, float RadiusY = -1f);
}
