using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// The Bandits2 Hound only ships one real attack — a pounce-bite where the dog physically lunges
// forward as it bites (confirmed by cropping attack1's own frames), not a stationary swing like a
// sword enemy. This makes that lunge an actual position change instead of just playing the
// animation in place, so it reads as a pounce. Same finding as Bandido1: "block" in this sheet is
// a mislabeled duplicate of the run cycle (checked every row on the sheet, not just this one, since
// getting burned once on that already) — not used here.
//
// Hunting-dog AI on top (same "read the player" toolkit as the elves/bandits): it stalks at mid
// range with feints instead of walking straight in, leaps at the player's throat from mid range,
// rushes in on a whiffed swing or a dash ending, sometimes hops back out after biting, and vaults
// over a player who turtles behind their block to bite from behind. Every bite flashes the body first.
public partial class Perrito1 : MeleeEnemy
{
	// Short — a dog doesn't wind up long, but it still crouches for a beat before pouncing so the
	// bite isn't instant the moment it's in range.
	[Export] public float WindupDuration = 0.3f;
	[Export] public int AttackTelegraphFrame = 0;
	[Export] public float LungeSpeed = 260f;
	[Export] public float LungeDuration = 0.25f;

	// Two-speed approach: trots over on noticing the player from afar, then breaks into a sprint
	// once close enough that a charge actually reads as one. RunSpeed reuses the profile's
	// MoveSpeed (already tuned for chase feel); WalkSpeed is deliberately its own slower value.
	// The sheet's own "walk"/"run" animation names were swapped when checked — HoundSpriteFrames.tres
	// has been fixed to match the real per-frame stride, not the original (wrong) label.
	[Export] public float WalkSpeed = 90f;
	[Export] public float RunTriggerDistance = 200f;

	// ── Telegraph ──
	// The body itself flashes bright during every wind-up (ground bite, pounce) — no colored glow,
	// just the sprite's own SelfModulate pushed over 1, so Visual.Modulate stays free.
	[ExportGroup("Telegraph")]
	[Export] public Color TelegraphTint = new(2.2f, 2.2f, 2.2f, 1f);
	[Export] public int TelegraphBlinks = 2;

	// ── Bite trail ──
	// Amber (matches the hound's yellow eyes, so it isn't one more bandit red): a ribbon that follows
	// the mouth while it lunges/leaps, and two short fang arcs snapping shut at full reach.
	[ExportGroup("Trail")]
	[Export] public Color TrailGlowColor = new(1f, 0.5f, 0.04f, 1f);
	[Export] public Color TrailCoreColor = new(1f, 0.92f, 0.6f, 1f);
	[Export] public float TrailLifetime = 0.16f;
	[Export] public float TrailCoreWidth = 2f;
	[Export] public float TrailGlowWidth = 7f;
	// attack1 frame where the jaws are fully extended (rightmost mouth pixel of the clip).
	[Export] public int JawSnapFrame = 3;
	[Export] public float JawSnapRadius = 11f;
	[Export] public float JawSnapDuration = 0.07f;
	[Export] public float JawFlashSize = 0.5f;

	// ── AI ──
	[ExportGroup("AI")]
	[Export] public float DecisionInterval = 0.35f;
	// Inside this distance it stops approaching straight and stalks: feints in, backs off, holds.
	[Export] public float StalkMaxDistance = 170f;
	[Export] public float StalkSpeed = 80f;
	[Export] public float FeintInTime = 0.25f;
	[Export] public float FeintOutTime = 0.3f;
	// Chance per decision to commit to a sprint-and-bite instead of another feint.
	[Export] public float RushChance = 0.35f;
	[Export] public float RushMaxTime = 1f;
	// Starts the ground bite when this close during a rush (the bite's own lunge covers the rest).
	[Export] public float RushBiteDistance = 70f;
	// Leap at the throat: a real jump arc aimed at the player, bite active the whole flight. Only
	// if it lands on ground.
	[Export] public float PounceMinDistance = 80f;
	[Export] public float PounceMaxDistance = 180f;
	[Export] public float PounceChance = 0.35f;
	[Export] public float PounceCooldown = 3f;
	[Export] public float PounceTelegraph = 0.32f;
	[Export] public float PounceVelocityY = -280f;
	[Export] public float PounceMaxSpeedX = 320f;
	// Punish: a whiffed swing or a dash ending near it → rush in with a much shorter wind-up.
	[Export] public float PunishRange = 160f;
	[Export] public float PunishWindupScale = 0.4f;
	// Hit and run: after a bite, sometimes hops back out of reach.
	[Export] public float HitAndRunChance = 0.5f;
	[Export] public Vector2 RetreatHop = new(200f, -220f);
	// Flank: the player whiffs a parry toward it → vault over during the recovery and bite from behind.
	[Export] public Vector2 VaultHop = new(240f, -360f);

	// Rightmost (mouth) pixel of each attack1 frame on the 110x64 sheet cell, measured from the
	// sheet; MouthDefault is the run/jump head. Converted through the sprite's transform, so the
	// scale, the attack Y offset and the facing flip all apply for free.
	private static readonly Vector2[] AttackMouthPixels =
	{
		new(71, 34), new(79, 28), new(90, 32), new(95, 34), new(92, 30), new(84, 32), new(76, 35),
	};
	private static readonly Vector2 MouthDefault = new(73, 31);
	private static readonly Vector2 CellCenter = new(55, 32);

	private bool _isLunging;
	private float _lungeDirection;
	private bool _isRunningSpeed;
	private float _runSpeed;

	private bool _pouncing;
	private float _pounceCooldownTimer;
	private float _decisionTimer;
	private int _stalkMove;
	private float _stalkTimer;
	private bool _feintReturning;
	private bool _rushing;
	private float _rushTimer;
	private float _rushWindupScale = 1f;
	private bool _counterPending;
	private bool _wasAttacking;

	private Tween _telegraphTween;
	private Line2D _ribbonGlow;
	private Line2D _ribbonCore;
	private readonly List<Vector2> _ribbonPoints = new();
	private readonly List<float> _ribbonAges = new();
	private bool _ribbonEmitting;
	private EnemySlashTrail _upperFang;
	private EnemySlashTrail _lowerFang;
	private bool _jawSnapped;

	private static readonly RandomNumberGenerator Rng = new();

	static Perrito1()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_runSpeed = MoveSpeed;
		_pounceCooldownTimer = Rng.RandfRange(0.5f, 1.5f);

		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		_ribbonGlow = MakeRibbonLine(TrailGlowWidth, TrailGlowColor, additive);
		_ribbonCore = MakeRibbonLine(TrailCoreWidth, TrailCoreColor, additive);

		_upperFang = MakeFangTrail();
		_lowerFang = MakeFangTrail();
		Sprite.FrameChanged += OnSpriteFrameChanged;
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
			// World-space points, so the ribbon stays where the mouth passed instead of riding along.
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

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;

		if (_isLunging)
		{
			Vector2 velocity = Velocity;
			velocity.Y = IsOnFloor() ? 0f : velocity.Y + Gravity * (float)delta;
			velocity.X = _lungeDirection * LungeSpeed;
			Velocity = velocity;
			MoveAndSlide();
			UpdateRibbon((float)delta);
			return;
		}

		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval)
			return;

		Think((float)delta);
		UpdateRibbon((float)delta);
	}

	private void Think(float dt)
	{
		_pounceCooldownTimer -= dt;
		_decisionTimer -= dt;
		_stalkTimer -= dt;
		bool justFinishedAttack = _wasAttacking && !Attacking;
		_wasAttacking = Attacking;

		var player = PlayerRef;
		if (player is null || !PlayerDetected)
		{
			_rushing = false;
			_stalkMove = 0;
			return;
		}

		float distanceX = player.GlobalPosition.X - GlobalPosition.X;
		float absDistance = Mathf.Abs(distanceX);
		float toward = Mathf.Sign(distanceX);

		// Hit and run: bite, then hop back out of reach.
		if (justFinishedAttack && Rng.Randf() < HitAndRunChance)
			TryHop(new Vector2(-toward * RetreatHop.X, RetreatHop.Y));

		if (Attacking || Hopping || !IsOnFloor())
			return;

		// Landed from a vault behind a blocking player → bite right away.
		if (_counterPending)
		{
			_counterPending = false;
			StartRush(PunishWindupScale);
		}

		// Punish a whiffed swing or a dash ending close by.
		if ((Whiff.JustWhiffed || Dash.SinceDashEnded < 0.1f) && absDistance <= PunishRange)
		{
			Whiff.Consume();
			StartRush(PunishWindupScale);
		}

		// Flank a player who just whiffed a parry.
		if (PlayerReads.WhiffedParryToward(player, GlobalPosition.X) && absDistance <= 110f
			&& TryHop(new Vector2(toward * VaultHop.X, VaultHop.Y)))
		{
			_rushing = false;
			_counterPending = true;
			return;
		}

		if (_rushing)
		{
			_rushTimer -= dt;
			if (absDistance <= RushBiteDistance)
			{
				_rushing = false;
				TryAttackNow(_rushWindupScale);
			}
			else if (_rushTimer <= 0f)
			{
				_rushing = false;
			}
			return;
		}

		// Feint: step in, then back out.
		if (_feintReturning && _stalkTimer <= 0f)
		{
			_feintReturning = false;
			_stalkMove = -1;
			_stalkTimer = FeintOutTime;
		}
		else if (_stalkTimer <= 0f)
		{
			_stalkMove = 0;
		}

		if (absDistance > StalkMaxDistance || _decisionTimer > 0f)
			return;
		_decisionTimer = DecisionInterval * Rng.RandfRange(0.8f, 1.3f);

		bool pounceRange = absDistance >= PounceMinDistance && absDistance <= PounceMaxDistance && player.IsOnFloor();
		if (pounceRange && _pounceCooldownTimer <= 0f && Rng.Randf() < PounceChance)
		{
			TryPounce(player);
			return;
		}

		float roll = Rng.Randf();
		if (roll < RushChance)
		{
			StartRush(1f);
		}
		else if (roll < RushChance + 0.3f)
		{
			_stalkMove = 1;
			_stalkTimer = FeintInTime;
			_feintReturning = true;
		}
		else if (roll < RushChance + 0.5f)
		{
			_stalkMove = -1;
			_stalkTimer = FeintOutTime;
		}
		else
		{
			_stalkMove = 0;
			_stalkTimer = DecisionInterval;
		}
	}

	private void StartRush(float windupScale)
	{
		_rushing = true;
		_rushTimer = RushMaxTime;
		_rushWindupScale = windupScale;
		_feintReturning = false;
		_stalkMove = 0;
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (Hopping)
			return currentVelocityX;
		if (Attacking || !CanChase)
			return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);

		float absDistance = Mathf.Abs(distanceX);
		float toward = Mathf.Sign(distanceX);

		if (_rushing)
		{
			_isRunningSpeed = true;
			return CanStepTo(toward) ? toward * _runSpeed : 0f;
		}

		if (absDistance > StalkMaxDistance)
		{
			// Two-speed approach from afar.
			_isRunningSpeed = absDistance <= RunTriggerDistance;
			return CanStepTo(toward) ? toward * (_isRunningSpeed ? _runSpeed : WalkSpeed) : 0f;
		}

		// Stalking band: feints and back-offs at a prowling pace. Still closes in if the player is
		// right on top of it so the normal in-range bite can trigger.
		_isRunningSpeed = false;
		float move = _stalkMove;
		if (absDistance <= AttackRange)
			move = 0f;
		float direction = move * toward;
		return move != 0f && CanStepTo(direction) ? direction * StalkSpeed : Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null || _pouncing)
			return;
		if (Hopping && PlayAirAnimation(velocity))
			return;

		string anim = Attacking ? "attack1"
			: Mathf.Abs(velocity.X) <= 5f ? "idle"
			: _isRunningSpeed ? "run" : "walk";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	// ── Telegraph ──

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
			_telegraphTween.TweenProperty(Sprite, "self_modulate", Colors.White, half);
		}
	}

	private void ClearTelegraph()
	{
		_telegraphTween?.Kill();
		if (Sprite is not null)
			Sprite.SelfModulate = Colors.White;
	}

	// ── Trail ──

	private Vector2 MouthGlobal()
	{
		Vector2 pixel = Sprite.Animation == "attack1" && Sprite.Frame < AttackMouthPixels.Length
			? AttackMouthPixels[Sprite.Frame]
			: MouthDefault;
		return Sprite.ToGlobal(pixel - CellCenter);
	}

	private Vector2 MouthInVisual() => Visual.ToLocal(MouthGlobal());

	private void UpdateRibbon(float dt)
	{
		for (int i = _ribbonAges.Count - 1; i >= 0; i--)
		{
			_ribbonAges[i] += dt;
			if (_ribbonAges[i] > TrailLifetime)
			{
				_ribbonAges.RemoveAt(i);
				_ribbonPoints.RemoveAt(i);
			}
		}

		if (_ribbonEmitting && Sprite is not null)
		{
			Vector2 mouth = MouthGlobal();
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

	// Jaws snapping shut at full reach: an upper and a lower fang arc closing onto the mouth tip.
	private void OnSpriteFrameChanged()
	{
		if (_jawSnapped || !Attacking || Sprite.Animation != "attack1" || Sprite.Frame != JawSnapFrame)
			return;
		_jawSnapped = true;

		Vector2 mouth = MouthInVisual();
		Vector2 center = mouth - new Vector2(JawSnapRadius, 0f);
		_upperFang.Play(center, JawSnapRadius, -110f, -5f, JawSnapDuration);
		_lowerFang.Play(center, JawSnapRadius, 110f, 5f, JawSnapDuration);
		SpawnJawFlash(mouth);
	}

	private void SpawnJawFlash(Vector2 localPosition)
	{
		var flash = new Sprite2D
		{
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			Modulate = TrailCoreColor,
			Position = localPosition,
			Scale = Vector2.One * JawFlashSize * 0.3f,
		};
		Visual.AddChild(flash);

		Tween tween = flash.CreateTween();
		tween.TweenProperty(flash, "scale", Vector2.One * JawFlashSize, 0.08f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
		tween.Parallel().TweenProperty(flash, "modulate", new Color(TrailGlowColor, 0f), 0.14f);
		tween.TweenCallback(Callable.From(flash.QueueFree));
	}

	// ── Pounce ──

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
		_jawSnapped = false;
		_pounceCooldownTimer = PounceCooldown;
		FacingRight = player.GlobalPosition.X >= GlobalPosition.X;
		Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);

		HoldTelegraphFrame("attack1");
		Sprite.Frame = AttackTelegraphFrame;
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
			if (TryAttackHop(launch))
			{
				Sprite.Play("attack1");
				_ribbonEmitting = true;
				AttackHitbox.Position = new Vector2(FacingRight ? AttackHitboxReach : -AttackHitboxReach, 0);
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
				_ribbonEmitting = false;
				_pouncing = false;
				Attacking = false;
				Sprite.Position = new Vector2(Sprite.Position.X, 0f);
				// OnDefeated may already have released it (death mid-pounce).
				if (HoldingAttackSlot)
				{
					EnemyCombatCoordinator.ReleaseAttackSlot();
					HoldingAttackSlot = false;
				}
			}
		}

		float jitter = 1f + (Rng.Randf() * 2f - 1f) * AttackCooldownJitter;
		await ToSignal(GetTree().CreateTimer(AttackCooldown * jitter), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			CanAttack = true;
	}

	// Full replacement of MeleeEnemy.Attack (not a call to base) since the lunge needs exclusive
	// control of Velocity for its duration — replicates the same attack-slot/cooldown bookkeeping
	// base.Attack() would have done, just around a moving swing instead of a stationary one.
	protected override async Task Attack()
	{
		Attacking = true;
		CanAttack = false;
		_rushing = false;
		_jawSnapped = false;

		float windup = WindupDuration * WindupScale;
		WindupScale = 1f;
		HoldTelegraphFrame("attack1");
		Sprite.Frame = AttackTelegraphFrame;
		Telegraph(windup);
		await ToSignal(GetTree().CreateTimer(windup), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		ClearTelegraph();
		Sprite.Play("attack1");
		_lungeDirection = FacingRight ? 1f : -1f;
		_isLunging = true;
		_ribbonEmitting = true;

		AttackHitbox.Position = new Vector2(FacingRight ? AttackHitboxReach : -AttackHitboxReach, 0);
		AttackHitbox.Activate(Stats);

		try
		{
			await ToSignal(GetTree().CreateTimer(LungeDuration), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;
			_isLunging = false;
			_ribbonEmitting = false;
			AttackHitbox.Deactivate();

			float remainingAnimTime = Mathf.Max(0f, AttackAnimDuration - windup - LungeDuration);
			await ToSignal(GetTree().CreateTimer(remainingAnimTime), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;
			Attacking = false;
			Sprite.Position = new Vector2(Sprite.Position.X, 0f);
		}
		finally
		{
			_isLunging = false;
			_ribbonEmitting = false;
			if (HoldingAttackSlot)
			{
				EnemyCombatCoordinator.ReleaseAttackSlot();
				HoldingAttackSlot = false;
			}
		}

		float jitter = 1f + (Rng.Randf() * 2f - 1f) * AttackCooldownJitter;
		await ToSignal(GetTree().CreateTimer(AttackCooldown * jitter), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			CanAttack = true;
	}
}
