using Godot;
using System.Threading.Tasks;

namespace Metroidvania.World;

// Gives the small spiders (SpiderEnemy/SpiderEnemyDark, both instance this scene) two things
// MeleeEnemy's generic Attack() doesn't fit well enough to reuse as-is, so this overrides it
// completely instead of composing with base.Attack() (compare OrcEnemy's windup, which DOES
// still call base.Attack() — that works there because it only delays the *start* of an otherwise
// unchanged sequence; here the hitbox timing itself is what's different):
//
// 1. The bite only actually connects once the animation reaches HitStartFrame — MeleeEnemy.Attack
//    activates the hitbox from frame 0, which reads as the spider hitting during its windup/lunge
//    pose instead of at the bite itself.
// 2. A poison spit fires the moment the "attack" clip reaches its own last frame. This uses
//    FrameChanged (checking Sprite.Frame against the clip's last index), not AnimationFinished:
//    the clip is 14 frames at 18fps (~0.78s) but AttackAnimDuration is 0.75s, so the old
//    base.Attack()-based version swapped back to "idle" about a frame early and AnimationFinished
//    never fired at all. Watching the frame index directly still catches it regardless of exactly
//    when that swap happens.
public partial class SpiderEnemy : MeleeEnemy
{
	[Export] public PackedScene PoisonSpitScene;
	[Export] public float PoisonSpitHorizontalSpeed = 200f;
	// Minimum horizontal distance used for the launch-velocity solve below — guards against the
	// player standing almost directly under/over the spider, where the real distance would blow
	// up the required vertical speed into an unreasonable spike.
	[Export] public float PoisonSpitMinDistance = 60f;
	// Which frame of "attack" the bite actually connects on — the sheet's lunge doesn't land until
	// well into the clip (frames 0-9 are windup), not frame 0 like MeleeEnemy assumes by default.
	[Export] public int HitStartFrame = 10;

	// ── Telegraph ──
	// Neon-green blinking during every wind-up (bite, pounce) — the small dark sprite read poorly
	// before it struck. Done on the sprite's own SelfModulate plus an additive glow behind it, so
	// it never overwrites Visual.Modulate (SpiderEnemyDark's darker tint lives there).
	[ExportGroup("Telegraph")]
	[Export] public Color TelegraphTint = new(0.55f, 3.2f, 0.75f, 1f);
	[Export] public Color TelegraphGlowColor = new(0.3f, 1f, 0.4f, 0.9f);
	[Export] public float TelegraphGlowScale = 0.9f;
	[Export] public int TelegraphBlinks = 3;

	// ── Ambusher AI ──
	// Small, fast and poisonous: pounces onto the player from mid range (a real jump arc, only if it
	// lands on ground, biting on contact mid-air), spits poison from range instead of only after a
	// bite, skitters back after attacking or when the player swings at it, and pounces on whiffed
	// swings / dash endings. Never walks or hops off a ledge.
	[ExportGroup("AI")]
	[Export] public float PounceMinDistance = 70f;
	[Export] public float PounceMaxDistance = 175f;
	[Export] public float PounceCooldown = 3.5f;
	[Export] public float PounceChance = 0.45f;
	[Export] public float PounceTelegraph = 0.35f;
	[Export] public float PounceVelocityY = -300f;
	[Export] public float PounceMaxSpeedX = 330f;
	[Export] public float SpitMinDistance = 100f;
	[Export] public float SpitMaxDistance = 240f;
	[Export] public float SpitCooldown = 4f;
	[Export] public float SpitChance = 0.5f;
	[Export] public float DecisionInterval = 0.4f;
	[Export] public float SkitterChance = 0.5f;
	[Export] public float EvadeChance = 0.35f;
	[Export] public Vector2 SkitterHop = new(170f, -200f);

	private bool _poisonSpitFiredThisAttack;
	private Sprite2D _telegraphGlow;
	private Tween _telegraphTween;
	private bool _pouncing;
	private float _pounceCooldownTimer;
	private float _spitCooldownTimer;
	private float _decisionTimer;
	private bool _wasAttacking;	private static readonly RandomNumberGenerator SpiderJitterRng = new();

	static SpiderEnemy()
	{
		SpiderJitterRng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;

		_telegraphGlow = new Sprite2D
		{
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			Modulate = TelegraphGlowColor with { A = 0f },
			Scale = Vector2.One * TelegraphGlowScale,
			ShowBehindParent = true,
		};
		Visual.AddChild(_telegraphGlow);
		_pounceCooldownTimer = SpiderJitterRng.RandfRange(0.5f, 1.5f);
	}

	// Blinks the neon telegraph TelegraphBlinks times across duration.
	private void Telegraph(float duration)
	{
		if (Sprite is null || duration <= 0f)
			return;
		_telegraphTween?.Kill();
		_telegraphTween = CreateTween();
		float half = duration / Mathf.Max(1, TelegraphBlinks) * 0.5f;
		for (int i = 0; i < TelegraphBlinks; i++)
		{
			_telegraphTween.TweenProperty(Sprite, "self_modulate", TelegraphTint, half);
			_telegraphTween.Parallel().TweenProperty(_telegraphGlow, "modulate:a", TelegraphGlowColor.A, half);
			_telegraphTween.TweenProperty(Sprite, "self_modulate", Colors.White, half);
			_telegraphTween.Parallel().TweenProperty(_telegraphGlow, "modulate:a", 0f, half);
		}
	}

	private void ClearTelegraph()
	{
		_telegraphTween?.Kill();
		if (Sprite is not null)
			Sprite.SelfModulate = Colors.White;
		if (_telegraphGlow is not null)
			_telegraphGlow.Modulate = TelegraphGlowColor with { A = 0f };
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval)
			return;
		Think((float)delta);
	}

	private void Think(float dt)
	{
		_pounceCooldownTimer -= dt;
		_spitCooldownTimer -= dt;
		_decisionTimer -= dt;
		bool justFinishedAttack = _wasAttacking && !Attacking;
		_wasAttacking = Attacking;

		var player = PlayerRef;
		if (player is null || !PlayerDetected)
			return;

		float distanceX = player.GlobalPosition.X - GlobalPosition.X;
		float absDistance = Mathf.Abs(distanceX);
		float away = -Mathf.Sign(distanceX);

		if (justFinishedAttack && SpiderJitterRng.Randf() < SkitterChance)
			TryHop(new Vector2(away * SkitterHop.X, SkitterHop.Y));

		if (Attacking || Hopping || !IsOnFloor())
			return;

		// Skitter out of an incoming swing.
		if (Whiff.PlayerStartedSwing && absDistance <= AttackRange * 1.6f && SpiderJitterRng.Randf() < EvadeChance)
		{
			TryHop(new Vector2(away * SkitterHop.X, SkitterHop.Y));
			return;
		}

		bool opening = Whiff.JustWhiffed || Dash.SinceDashEnded < 0.1f;
		bool pounceRange = absDistance >= PounceMinDistance && absDistance <= PounceMaxDistance && player.IsOnFloor();
		if (pounceRange && _pounceCooldownTimer <= 0f && (opening || (_decisionTimer <= 0f && SpiderJitterRng.Randf() < PounceChance)))
		{
			Whiff.Consume();
			TryPounce(player);
			return;
		}

		if (_decisionTimer <= 0f)
		{
			_decisionTimer = DecisionInterval;
			// Ranged spit: the full attack clip still ends in the spit even if the bite is out of reach.
			if (absDistance >= SpitMinDistance && absDistance <= SpitMaxDistance && _spitCooldownTimer <= 0f
				&& SpiderJitterRng.Randf() < SpitChance && TryAttackNow())
				_spitCooldownTimer = SpitCooldown;
		}
	}

	private void TryPounce(Metroidvania.Player.Player player)
	{
		if (Attacking || !CanAttack || !EnemyCombatCoordinator.TryAcquireAttackSlot())
			return;
		HoldingAttackSlot = true;
		_ = Pounce(player);
	}

	private async Task Pounce(Metroidvania.Player.Player player)
	{
		Attacking = true;
		CanAttack = false;
		_pouncing = true;
		_pounceCooldownTimer = PounceCooldown;
		FacingRight = player.GlobalPosition.X >= GlobalPosition.X;
		Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);

		Sprite.Play("lunge");
		Sprite.Pause();
		Sprite.Frame = 0;
		Telegraph(PounceTelegraph);

		try
		{
			await ToSignal(GetTree().CreateTimer(PounceTelegraph), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;

			// Aim at where the player is now; airtime of the arc = 2·|vy|/g.
			float airTime = 2f * -PounceVelocityY / Mathf.Max(1f, Gravity);
			float dx = player.GlobalPosition.X - GlobalPosition.X;
			var launch = new Vector2(Mathf.Clamp(dx / airTime, -PounceMaxSpeedX, PounceMaxSpeedX), PounceVelocityY);
			// No safe landing (pit/ledge) → the pounce just fizzles; cooldowns below still run.
			if (TryHop(launch))
			{
				Sprite.Play("lunge");
				AttackHitbox.Position = new Vector2(FacingRight ? AttackHitboxReach * 0.6f : -AttackHitboxReach * 0.6f, 0);
				AttackHitbox.Activate(Stats);

				float elapsed = 0f;
				while (Hopping && elapsed < airTime + 0.4f)
				{
					await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
					if (!IsInstanceValid(this) || IsQueuedForRemoval)
						return;
					elapsed += (float)GetPhysicsProcessDeltaTime();
				}
			}
		}
		finally
		{
			if (IsInstanceValid(this))
			{
				AttackHitbox.Deactivate();
				ClearTelegraph();
				_pouncing = false;
				Attacking = false;
				// OnDefeated may already have released it (death mid-pounce).
				if (HoldingAttackSlot)
				{
					EnemyCombatCoordinator.ReleaseAttackSlot();
					HoldingAttackSlot = false;
				}
			}
		}

		float jitter = 1f + SpiderJitterRng.RandfRange(-AttackCooldownJitter, AttackCooldownJitter);
		await ToSignal(GetTree().CreateTimer(AttackCooldown * jitter), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			CanAttack = true;
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta) =>
		SpacingMoveX(distanceX, currentVelocityX, StopDistance, 0f);

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (_pouncing)
			return;
		base.UpdateAnimation(velocity);
	}

	protected override async Task Attack()
	{
		Attacking = true;
		CanAttack = false;
		_poisonSpitFiredThisAttack = false;
		// The wind-up is the clip's own lead-in (bite lands on HitStartFrame), so it isn't scaled.
		WindupScale = 1f;
		Sprite.Position = new Vector2(Sprite.Position.X, AttackSpriteYOffset);
		Sprite.Play("attack");

		void OnFrameChanged()
		{
			if (_poisonSpitFiredThisAttack || Sprite.Animation != "attack")
				return;
			if (Sprite.Frame != Sprite.SpriteFrames.GetFrameCount("attack") - 1)
				return;

			_poisonSpitFiredThisAttack = true;
			FirePoisonSpit();
		}
		Sprite.FrameChanged += OnFrameChanged;

		try
		{
			float animSpeed = (float)Sprite.SpriteFrames.GetAnimationSpeed("attack");
			float windupDuration = animSpeed > 0f ? HitStartFrame / animSpeed : 0f;
			Telegraph(windupDuration);

			await ToSignal(GetTree().CreateTimer(windupDuration), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;

			AttackHitbox.Position = new Vector2(FacingRight ? AttackHitboxReach : -AttackHitboxReach, 0);
			AttackHitbox.Activate(Stats);

			await ToSignal(GetTree().CreateTimer(AttackDuration), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;
			AttackHitbox.Deactivate();

			float remainingAnimTime = Mathf.Max(0f, AttackAnimDuration - windupDuration - AttackDuration);
			await ToSignal(GetTree().CreateTimer(remainingAnimTime), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;

			Attacking = false;
			Sprite.Position = new Vector2(Sprite.Position.X, 0f);
		}
		finally
		{
			if (IsInstanceValid(Sprite))
				Sprite.FrameChanged -= OnFrameChanged;
			if (IsInstanceValid(this))
				ClearTelegraph();
			EnemyCombatCoordinator.ReleaseAttackSlot();
			HoldingAttackSlot = false;
		}

		float jitter = 1f + SpiderJitterRng.RandfRange(-AttackCooldownJitter, AttackCooldownJitter);
		await ToSignal(GetTree().CreateTimer(AttackCooldown * jitter), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			CanAttack = true;
	}

	private void FirePoisonSpit()
	{
		if (PoisonSpitScene is null || !IsInstanceValid(this))
			return;

		Node2D playerNode = GetTree().GetFirstNodeInGroup("player") as Node2D;
		if (playerNode is null)
			return;

		Vector2 to = playerNode.GlobalPosition - GlobalPosition;
		float dx = Mathf.Sign(to.X == 0f ? (FacingRight ? 1f : -1f) : to.X) * Mathf.Max(Mathf.Abs(to.X), PoisonSpitMinDistance);
		float vx = Mathf.Sign(dx) * PoisonSpitHorizontalSpeed;
		float time = Mathf.Abs(dx) / PoisonSpitHorizontalSpeed;

		var spit = PoisonSpitScene.Instantiate<PoisonSpit>();
		GetTree().CurrentScene.AddChild(spit);
		spit.GlobalPosition = GlobalPosition;

		// Solves for the vertical launch speed that actually lands on the player's current height
		// given ArcGravity and the flight time implied by the horizontal speed above, instead of a
		// fixed arc that only happens to look right at one specific range.
		float vy = (to.Y - 0.5f * spit.ArcGravity * time * time) / time;
		spit.Launch(new Vector2(vx, vy), Stats);
	}
}
