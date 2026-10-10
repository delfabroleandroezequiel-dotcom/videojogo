using Godot;
using System.Threading.Tasks;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Incendiario/TorcherBandit: settle-then-telegraph-then-swing torch attack that leaves fire on
// the ground (see GroundFire), played as a zoner — see the AI block below.
public partial class Bandido4 : MeleeEnemy
{
	[Export] public float SettleDelay = 0.3f;
	[Export] public float SettleSpeedThreshold = 10f;
	[Export] public float WindupDuration = 0.5f;

	// Attack.png plays at 14fps (see TorcherBanditSpriteFrames.tres) — frame 7 of the resumed
	// swing (after the telegraph hold below) is where the torch actually connects.
	[Export] public float HitFrameDelay = 7f / 14f;

	// Fire trail following the torch's swing, drawn by EnemySlashTrail. Measured off
	// TorcherBandit/Attack.png (flame-pixel centroid per frame, sprite scale 1.6192 + offset):
	// frames 0-6 hold the torch behind at about (-49,-18), frame 7 snaps it over the head down to
	// the front at chest height (42,-14) — the frame even has its own baked swoosh along that
	// path. Pivoting around the shoulder (-6,-32), that's ~162° -> ~20° going over the top, so the
	// arc runs -198° -> 20° (-90° = straight up). The swing is a single-frame snap in the art, so
	// the trail starts one frame before it (frame 6; play resumes from TelegraphFrame 1) and
	// sweeps fast.
	[Export] public Vector2 FireTrailCenter = new(-6f, -32f);
	[Export] public float FireTrailRadius = 50f;
	[Export] public float FireTrailStartAngle = -198f;
	[Export] public float FireTrailEndAngle = 20f;
	[Export] public float FireTrailStartDelay = 5f / 14f;
	[Export] public float FireTrailDuration = 0.12f;
	[Export] public float FireTrailCoreWidth = 4f;
	[Export] public float FireTrailGlowWidth = 12f;
	[Export] public Color FireTrailCoreColor = new(1f, 0.9f, 0.5f, 1f);
	// Glow color along the streak, tail -> tip: cooling deep red at the tail, orange at the tip.
	[Export] public Gradient FireTrailGlowRamp;
	[Export] public ParticleProcessMaterial FireTrailEmberProcess;

	// Small burning patch left on the ground under where the swing ends (see GroundFire). Found
	// by a short raycast straight down from the trail's end point; if there's no ground within
	// GroundFireMaxDrop (swinging off a ledge), no fire is spawned.
	[Export] public PackedScene GroundFireScene;
	[Export] public float GroundFireMaxDrop = 120f;

	// ── Zoner AI ──
	// After a swing leaves fire on the ground it backs off so the fire sits between it and the
	// player, making them cross the flames to reach it — but it isn't a free target while doing so:
	// if the player follows it into reach, or it runs out of room (wall/ledge behind), it turns and
	// swings back with a short wind-up. A player standing in fire, whiffing a swing or ending a dash
	// nearby is the opening it steps in to punish. From mid range it lobs a fan of embers instead
	// of walking over, each one leaving a GroundFire patch where it lands. Never backs off a ledge.
	[ExportGroup("AI")]
	[Export] public float RetreatAfterFireTime = 1.4f;
	[Export] public float RetreatDistance = 150f;
	[Export] public float PunishWindupScale = 0.6f;
	// Pressed while retreating (player inside AttackRange) → counter-swing with this wind-up scale.
	[Export] public float CounterWindupScale = 0.4f;
	// Punish openings within this distance: steps in at StepInSpeed and swings once in reach.
	[Export] public float PunishRange = 110f;
	[Export] public float StepInSpeed = 190f;
	[Export] public float StepInMaxTime = 0.45f;

	// ── Ember lob ──
	// Same over-the-head torch swing, released as EmberCount embers on parabolas spread around the
	// player's position (EmberSpread px apart), so jumping forward or back both risk landing in fire.
	// The body flashes during the wind-up (the melee swing doesn't), which is how the throw reads.
	[ExportGroup("Ember Lob")]
	[Export] public float ThrowMinDistance = 120f;
	[Export] public float ThrowMaxDistance = 280f;
	[Export] public float ThrowCooldown = 4.5f;
	[Export] public float ThrowChance = 0.5f;
	[Export] public float ThrowDecisionInterval = 0.5f;
	[Export] public float ThrowWindup = 0.45f;
	[Export] public Color ThrowFlashTint = new(2.2f, 2.2f, 2.2f, 1f);
	[Export] public int ThrowFlashBlinks = 2;
	[Export] public int EmberCount = 3;
	[Export] public float EmberSpread = 55f;
	// Release point: where the torch ends the swing (front, chest height — see the trail comment).
	[Export] public Vector2 EmberReleaseOffset = new(42f, -14f);
	[Export] public float EmberLaunchSpeedY = 330f;
	// Each successive ember goes a bit higher so the three don't fly as one blob.
	[Export] public float EmberLaunchSpeedYStep = 35f;
	[Export] public float EmberGravity = 750f;
	[Export] public float EmberMaxSpeedX = 420f;
	[Export] public int EmberDamage = 8;
	[Export] public float EmberLifetime = 3f;
	[Export] public float EmberSize = 0.32f;
	[Export] public Color EmberColor = new(1f, 0.55f, 0.1f, 1f);

	private EnemySlashTrail _fireTrail;
	private float _retreatTimer;
	private float _settleTimer;
	private bool _steppingIn;
	private float _stepInTimer;
	private float _throwCooldownTimer;
	private float _throwDecisionTimer;
	private Tween _flashTween;
	private static readonly RandomNumberGenerator Rng = new();

	static Bandido4()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;

		_fireTrail = new EnemySlashTrail
		{
			CoreWidth = FireTrailCoreWidth,
			GlowWidth = FireTrailGlowWidth,
			GlowColor = Colors.White,
			CoreColor = FireTrailCoreColor,
			GlowRamp = FireTrailGlowRamp ?? DefaultFireRamp(),
			SparkProcess = FireTrailEmberProcess,
			SparkColor = new Color(1f, 0.75f, 0.35f, 1f),
			SparkAmount = 24,
			SparkLifetime = 0.9,
		};
		Visual.AddChild(_fireTrail);
	}

	private static Gradient DefaultFireRamp()
	{
		var ramp = new Gradient();
		ramp.Offsets = new[] { 0f, 0.5f, 1f };
		ramp.Colors = new[]
		{
			new Color(0.45f, 0.03f, 0f, 0f),
			new Color(1f, 0.25f, 0.02f, 0.75f),
			new Color(1f, 0.6f, 0.1f, 1f),
		};
		return ramp;
	}

	private async void RunFireSwing()
	{
		await ToSignal(GetTree().CreateTimer(FireTrailStartDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		_fireTrail.Play(FireTrailCenter, FireTrailRadius, FireTrailStartAngle, FireTrailEndAngle, FireTrailDuration);

		// The fire only lands once the torch's own hit window (HitFrameDelay + AttackDuration from
		// the swing's start) has closed — spawned any earlier, its first tick would reach the player
		// before the torch and the torch hit would then be the one that misses.
		float untilHitWindowCloses = Mathf.Max(FireTrailDuration, HitFrameDelay + AttackDuration - FireTrailStartDelay);
		await ToSignal(GetTree().CreateTimer(untilHitWindowCloses), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		SpawnGroundFire();
	}

	private void SpawnGroundFire()
	{
		Vector2 localTip = FireTrailCenter + Vector2.Right.Rotated(Mathf.DegToRad(FireTrailEndAngle)) * FireTrailRadius;
		if (SpawnGroundFireBelow(this, GroundFireScene, Visual.ToGlobal(localTip), GroundFireMaxDrop, GetRid()))
			_retreatTimer = RetreatAfterFireTime;
	}

	// Raycasts straight down from `from` and drops a GroundFire on the first ground within maxDrop.
	// Static (context = any node in the world) so an ember still in flight after its thrower died
	// can use it too.
	private static bool SpawnGroundFireBelow(Node2D context, PackedScene fireScene, Vector2 from, float maxDrop, Rid exclude)
	{
		if (fireScene is null || !IsInstanceValid(context) || !context.IsInsideTree())
			return false;

		var query = PhysicsRayQueryParameters2D.Create(from, from + Vector2.Down * maxDrop,
			1u | PhysicsLayers.ClimbableWalls | PhysicsLayers.OneWayPlatforms, new Godot.Collections.Array<Rid> { exclude });
		var hit = context.GetWorld2D().DirectSpaceState.IntersectRay(query);
		if (hit.Count == 0)
			return false;

		// Deferred: adding a collision-carrying Area2D mid physics step can hit "Can't change this
		// state while flushing queries" (same reason PoisonSpit defers its PoisonPuddle).
		Vector2 groundPoint = (Vector2)hit["position"];
		Node currentScene = context.GetTree().CurrentScene;
		Callable.From(() =>
		{
			var fire = fireScene.Instantiate<Node2D>();
			currentScene.AddChild(fire);
			fire.GlobalPosition = groundPoint;
		}).CallDeferred();
		return true;
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);

		if (IsQueuedForRemoval)
			return;
		Think((float)delta);

		if (Mathf.Abs(Velocity.X) > SettleSpeedThreshold)
			_settleTimer = 0f;
		else
			_settleTimer += (float)delta;
	}

	protected override bool ReadyToCommitAttack() => _settleTimer >= SettleDelay && _retreatTimer <= 0f;

	private void Think(float dt)
	{
		_retreatTimer -= dt;
		_throwCooldownTimer -= dt;
		_throwDecisionTimer -= dt;
		var player = PlayerRef;
		if (player is null || Attacking || !PlayerDetected)
		{
			_steppingIn = false;
			return;
		}

		float distanceX = player.GlobalPosition.X - GlobalPosition.X;
		float absDistance = Mathf.Abs(distanceX);
		float away = -Mathf.Sign(distanceX);
		bool playerInFire = PlayerStandingInFire(player);
		if (playerInFire)
			_retreatTimer = 0f;

		if (_retreatTimer > 0f)
		{
			// Followed into reach while backing off → turn and swing back.
			if (absDistance <= AttackRange && TryAttackNow(CounterWindupScale))
				return;
			// Out of room behind it → stop running and fight normally.
			if (!CanStepTo(away))
				_retreatTimer = 0f;
		}

		// Punish openings: player in the fire, a whiffed swing, a dash ending nearby.
		if ((playerInFire || Whiff.JustWhiffed || Dash.SinceDashEnded < 0.1f) && absDistance <= PunishRange)
		{
			Whiff.Consume();
			_retreatTimer = 0f;
			_steppingIn = true;
			_stepInTimer = StepInMaxTime;
		}

		if (_steppingIn)
		{
			_stepInTimer -= dt;
			if (absDistance <= AttackRange)
			{
				_steppingIn = false;
				TryAttackNow(PunishWindupScale);
			}
			else if (_stepInTimer <= 0f)
			{
				_steppingIn = false;
			}
			return;
		}

		// Ember lob from mid range.
		if (_throwDecisionTimer <= 0f && !Hopping && IsOnFloor())
		{
			_throwDecisionTimer = ThrowDecisionInterval;
			if (absDistance >= ThrowMinDistance && absDistance <= ThrowMaxDistance && _throwCooldownTimer <= 0f
				&& Rng.Randf() < ThrowChance)
				TryThrow(player);
		}
	}

	private bool PlayerStandingInFire(Node2D player)
	{
		foreach (Node node in GetTree().GetNodesInGroup(GroundFire.GroupName))
		{
			if (node is GroundFire fire && fire.IsBurning && fire.OverlapsBody(player))
				return true;
		}
		return false;
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (Hopping)
			return currentVelocityX;
		if (_steppingIn && !Attacking)
		{
			float toward = Mathf.Sign(distanceX);
			return CanStepTo(toward) ? toward * StepInSpeed : 0f;
		}
		// Backing off behind its fire: never approach, keep RetreatDistance.
		if (_retreatTimer > 0f)
			return SpacingMoveX(distanceX, currentVelocityX, float.MaxValue, RetreatDistance);
		return SpacingMoveX(distanceX, currentVelocityX, StopDistance, 0f);
	}

	// ── Ember lob ──

	private void TryThrow(Metroidvania.Player.Player player)
	{
		if (Attacking || !CanAttack || !EnemyCombatCoordinator.TryAcquireAttackSlot())
			return;
		HoldingAttackSlot = true;
		_ = Throw(player);
	}

	private async Task Throw(Metroidvania.Player.Player player)
	{
		Attacking = true;
		CanAttack = false;
		_throwCooldownTimer = ThrowCooldown * Rng.RandfRange(0.85f, 1.2f);
		FacingRight = player.GlobalPosition.X >= GlobalPosition.X;
		Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);

		HoldTelegraphFrame("attack");
		FlashBody(ThrowWindup);

		try
		{
			await ToSignal(GetTree().CreateTimer(ThrowWindup), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;
			ClearFlash();

			Sprite.Play("attack");
			await ToSignal(GetTree().CreateTimer(FireTrailStartDelay), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;
			_fireTrail.Play(FireTrailCenter, FireTrailRadius, FireTrailStartAngle, FireTrailEndAngle, FireTrailDuration);

			// Release on the frame the torch comes over the top (same hit frame as the melee swing).
			await ToSignal(GetTree().CreateTimer(Mathf.Max(0f, HitFrameDelay - FireTrailStartDelay)), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;
			if (IsInstanceValid(player))
				ReleaseEmbers(player);

			await ToSignal(GetTree().CreateTimer(Mathf.Max(0f, AttackAnimDuration - HitFrameDelay)), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;
		}
		finally
		{
			if (IsInstanceValid(this))
			{
				ClearFlash();
				Attacking = false;
				// OnDefeated may already have released it (death mid-throw).
				if (HoldingAttackSlot)
				{
					EnemyCombatCoordinator.ReleaseAttackSlot();
					HoldingAttackSlot = false;
				}
			}
		}

		float jitter = 1f + Rng.RandfRange(-AttackCooldownJitter, AttackCooldownJitter);
		await ToSignal(GetTree().CreateTimer(AttackCooldown * jitter), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			CanAttack = true;
	}

	private void FlashBody(float duration)
	{
		_flashTween?.Kill();
		_flashTween = CreateTween();
		float half = duration / Mathf.Max(1, ThrowFlashBlinks) * 0.5f;
		for (int i = 0; i < ThrowFlashBlinks; i++)
		{
			_flashTween.TweenProperty(Sprite, "self_modulate", ThrowFlashTint, half);
			_flashTween.TweenProperty(Sprite, "self_modulate", Colors.White, half);
		}
	}

	private void ClearFlash()
	{
		_flashTween?.Kill();
		if (Sprite is not null)
			Sprite.SelfModulate = Colors.White;
	}

	// Aims each ember at the player's X plus its spread offset: picks the launch vy, then solves the
	// flight time to the target's height under EmberGravity, and vx = dx / time.
	private void ReleaseEmbers(Node2D player)
	{
		Vector2 origin = Visual.ToGlobal(EmberReleaseOffset);
		Node scene = GetTree().CurrentScene;
		if (scene is null)
			return;

		for (int i = 0; i < EmberCount; i++)
		{
			float offset = (i - (EmberCount - 1) * 0.5f) * EmberSpread;
			Vector2 target = new(player.GlobalPosition.X + offset, player.GlobalPosition.Y);
			float vy = -(EmberLaunchSpeedY + i * EmberLaunchSpeedYStep);
			float dy = target.Y - origin.Y;
			float discriminant = vy * vy + 2f * EmberGravity * dy;
			float time = discriminant > 0f ? (-vy + Mathf.Sqrt(discriminant)) / EmberGravity : 2f * -vy / EmberGravity;
			float vx = Mathf.Clamp((target.X - origin.X) / Mathf.Max(0.1f, time), -EmberMaxSpeedX, EmberMaxSpeedX);
			LaunchEmber(scene, origin, new Vector2(vx, vy));
		}
	}

	// A small burning blob on a parabola: burns the player on contact (Fire-typed, like GroundFire),
	// and on touching ground leaves a GroundFire patch. Built in code (glow + core + embers + light)
	// and flown by its own loop that never touches this enemy again, so it keeps going if the
	// thrower dies mid-flight.
	private void LaunchEmber(Node scene, Vector2 origin, Vector2 velocity)
	{
		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		var dot = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres");
		var ember = new Area2D
		{
			CollisionLayer = 0,
			CollisionMask = 1u | 2u | PhysicsLayers.ClimbableWalls,
		};
		ember.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 5f } });
		ember.AddChild(new Sprite2D { Texture = dot, Material = additive, Modulate = EmberColor, Scale = Vector2.One * EmberSize });
		ember.AddChild(new Sprite2D { Texture = dot, Material = additive, Modulate = FireTrailCoreColor, Scale = Vector2.One * EmberSize * 0.45f });
		ember.AddChild(new GpuParticles2D
		{
			Material = additive,
			Amount = 14,
			Lifetime = 0.45,
			Randomness = 0.5f,
			LocalCoords = false,
			ProcessMaterial = FireTrailEmberProcess ?? GD.Load<ParticleProcessMaterial>("res://resources/particles/FlameEmberProcess.tres"),
			Texture = dot,
			Scale = new Vector2(0.35f, 0.35f),
			Modulate = new Color(1f, 0.7f, 0.3f, 1f),
		});
		ember.AddChild(new PointLight2D
		{
			Texture = GD.Load<Texture2D>("res://resources/lighting/PointLightGradient.tres"),
			Color = EmberColor,
			Energy = 0.8f,
			TextureScale = 0.35f,
		});
		scene.AddChild(ember);
		ember.GlobalPosition = origin;

		FlyEmber(ember, velocity, EmberGravity, EmberLifetime, EmberDamage, GroundFireScene, GroundFireMaxDrop);
	}

	private static async void FlyEmber(Area2D ember, Vector2 velocity, float gravity, float lifetime, int damage,
		PackedScene fireScene, float maxDrop)
	{
		bool done = false;
		void Burst(bool leaveFire)
		{
			if (done || !IsInstanceValid(ember))
				return;
			done = true;
			if (leaveFire)
				SpawnGroundFireBelow(ember, fireScene, ember.GlobalPosition + Vector2.Up * 8f, maxDrop, ember.GetRid());
			ember.QueueFree();
		}

		ember.BodyEntered += body =>
		{
			if (done)
				return;
			if (body.IsInGroup("player"))
			{
				if (body.GetNodeOrNull<Stats>("Stats") is { IsInvulnerable: false } stats)
					stats.TakeDamage(damage, isProjectile: true, element: DamageElement.Fire);
				Burst(true);
			}
			else if (velocity.Y > 0f)
			{
				// Only while falling: a wall or ceiling clipped on the way up shouldn't snuff it.
				Burst(true);
			}
		};

		SceneTree tree = ember.GetTree();
		float age = 0f;
		while (!done && IsInstanceValid(ember))
		{
			await ember.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
			if (done || !IsInstanceValid(ember))
				return;
			float dt = (float)ember.GetPhysicsProcessDeltaTime();
			age += dt;
			velocity.Y += gravity * dt;
			ember.GlobalPosition += velocity * dt;
			if (age >= lifetime)
				Burst(false);
		}
	}

	protected override async Task Attack()
	{
		Attacking = true;
		CanAttack = false;
		_steppingIn = false;

		HoldTelegraphFrame("attack");
		float windup = WindupDuration * WindupScale;
		WindupScale = 1f;
		await ToSignal(GetTree().CreateTimer(windup), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		Sprite.Play("attack");
		RunFireSwing();
		await ToSignal(GetTree().CreateTimer(HitFrameDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		await base.Attack();
	}
}
