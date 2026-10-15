using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Corpulento/HeavyBandit — an executioner with a serrated greatsword, played as slow artillery that
// punishes every lazy answer to it. Its sheet's attack (24 frames, 14fps) has two usable beats:
//   frame 3  — a quick low scoop (sword swung up from behind/below to the front),
//   frames 4-8 sword raised overhead, frame 9 the smash into the floor, 10-19 sword stuck in the
//   ground, 20-23 the pull-out.
// Moves built on that:
//   - Smash (in reach, or from range when the player is on the ground in its lane): the body
//     flashes while the sword is up, the blade comes down with a neon streak, the floor bursts at
//     the tip, and a glowing fissure races forward along the floor (a moving hitbox) to erupt in a
//     cluster of rock spikes at its end. It stops at a pit or a wall. It won't waste a smash on a
//     jumping player — it waits and smashes the moment they land.
//   - Scoop (only frames 0-3, fast): flings a spray of gravel that arcs at the player. Its answer
//     to point-blank pressure, to whiffs/dash endings, and to a player hopping over its fissures.
//   - Backswing: get behind it and it spins round into a scoop instead of letting you hit its back.
// All FX are hand-built (GroundSlamFx + EnemySlashTrail). See Bandido5 for the leaping cousin.
public partial class BandidoCorpulento : MeleeEnemy
{
	private const float SheetFps = 14f;
	private const int PullOutFrame = 20;
	private const float PullOutDuration = 4f / SheetFps;

	// ── Smash ──
	// Frame 9 is where the blade hits the floor.
	[Export] public float WindupDuration = 9f / 14f;
	[Export] public float SmashHitWindow = 0.16f;
	[Export] public float SmashImpactOffsetX = 70f;
	[Export] public float SmashShake = 4f;
	// Body flash while the sword is raised (frames 4-8).
	[Export] public Color FlashTint = new(2.2f, 2.2f, 2.2f, 1f);
	[Export] public int FlashBlinks = 2;

	// Neon streak swept out ahead of the blade (trails go past the weapon, to its reach): from the
	// overhead pose behind its head down to the floor in front, where frame 10 plants the tip.
	[ExportGroup("Trails")]
	[Export] public Vector2 SmashTrailCenter = new(8f, -10f);
	[Export] public float SmashTrailRadius = 72f;
	[Export] public float SmashTrailStartAngle = -120f;
	[Export] public float SmashTrailEndAngle = 35f;
	[Export] public float SmashTrailDuration = 0.09f;
	// Scoop: from low behind, under, up to the front (frame 3's crescent).
	[Export] public Vector2 ScoopTrailCenter = new(0f, 0f);
	[Export] public float ScoopTrailRadius = 54f;
	[Export] public float ScoopTrailStartAngle = 140f;
	[Export] public float ScoopTrailEndAngle = -25f;
	[Export] public float ScoopTrailDuration = 0.08f;
	[Export] public Color TrailGlowColor = new(1f, 0.05f, 0.12f, 1f);
	[Export] public Color TrailCoreColor = new(1f, 0.6f, 0.62f, 1f);

	// ── Fissure ──
	[ExportGroup("Fissure")]
	[Export] public float FissureSpeed = 430f;
	[Export] public float FissureLength = 230f;
	[Export] public Vector2 FissureHitboxSize = new(34f, 26f);
	[Export] public int FissureDamage = 14;
	[Export] public float FissureChipEvery = 24f;
	[Export] public Vector2 EruptionSize = new(60f, 48f);
	[Export] public int EruptionDamage = 18;
	[Export] public float EruptionHold = 0.3f;
	[Export] public float EruptionShake = 3f;

	// ── Scoop + gravel ──
	[ExportGroup("Scoop")]
	[Export] public int ScoopHitFrame = 3;
	[Export] public float ScoopFlashDuration = 0.18f;
	[Export] public float ScoopHitWindow = 0.12f;
	[Export] public float ScoopRecovery = 0.3f;
	[Export] public int GravelCount = 4;
	[Export] public int GravelDamage = 6;
	[Export] public float GravelGravity = 900f;
	[Export] public float GravelLifetime = 2.5f;

	// ── AI ──
	[ExportGroup("AI")]
	[Export] public float LaneHeightTolerance = 40f;
	[Export] public float DecisionInterval = 0.45f;
	[Export] public float ArtilleryMinDistance = 90f;
	[Export] public float ArtilleryMaxDistance = 290f;
	[Export] public float ArtilleryChance = 0.55f;
	[Export] public float CloseScoopChance = 0.45f;
	[Export] public float PunishRange = 75f;
	[Export] public float BehindRange = 95f;
	[Export] public float BackswingCooldown = 1.8f;
	[Export] public float GravelMinDistance = 70f;
	[Export] public float GravelMaxDistance = 210f;
	[Export] public float GravelCooldown = 3.2f;
	// Chance per decision to toss gravel at a player who's in the air (hopping over fissures).
	[Export] public float GravelVsAirborneChance = 0.7f;

	private static readonly GroundSlamFx.Palette Palette = new()
	{
		Hot = new Color(1f, 0.6f, 0.2f, 1f),
		Core = new Color(1f, 0.95f, 0.8f, 1f),
		Rock = new Color(0.4f, 0.37f, 0.34f, 1f),
		Dust = new Color(0.58f, 0.53f, 0.48f, 0.6f),
		Light = new Color(1f, 0.7f, 0.4f, 1f),
	};

	private EnemySlashTrail _trail;
	private Tween _flashTween;
	private float _decisionTimer;
	private float _backswingCooldownTimer;
	private float _gravelCooldownTimer;
	private bool _playerWasAirborne;
	private bool _scooping;
	private static readonly RandomNumberGenerator Rng = new();

	static BandidoCorpulento()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_trail = new EnemySlashTrail
		{
			CoreWidth = 3f,
			GlowWidth = 10f,
			GlowColor = TrailGlowColor,
			CoreColor = TrailCoreColor,
			MinPointSpacing = 1.5f,
		};
		Visual.AddChild(_trail);
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval)
			return;
		Think((float)delta);
	}

	// ── Decisions ───────────────────────────────────────────────────────────────────────────────

	private void Think(float dt)
	{
		_decisionTimer -= dt;
		_backswingCooldownTimer -= dt;
		_gravelCooldownTimer -= dt;

		var player = PlayerRef;
		bool airborne = player is not null && !player.IsOnFloor();
		bool justLanded = _playerWasAirborne && !airborne;
		_playerWasAirborne = airborne;
		if (player is null || !PlayerDetected || Attacking || Hopping || !IsOnFloor())
			return;

		float distanceX = player.GlobalPosition.X - GlobalPosition.X;
		float absDistance = Mathf.Abs(distanceX);
		bool inLane = Mathf.Abs(player.GlobalPosition.Y - GlobalPosition.Y) <= LaneHeightTolerance;
		bool facingPlayer = FacingRight == (distanceX >= 0f);

		// Got behind it → spin round into a scoop.
		if (!facingPlayer && absDistance <= BehindRange && _backswingCooldownTimer <= 0f)
		{
			_backswingCooldownTimer = BackswingCooldown;
			FacingRight = distanceX >= 0f;
			Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);
			TryScoop(0.6f);
			return;
		}

		// Landing in its lane after hopping a fissure → smash right then.
		if (justLanded && inLane && facingPlayer && absDistance <= ArtilleryMaxDistance)
		{
			TryAttackNow();
			return;
		}

		// Whiff / dash ending point-blank → fast scoop.
		if ((Whiff.JustWhiffed || Dash.SinceDashEnded < 0.1f) && absDistance <= PunishRange && facingPlayer)
		{
			Whiff.Consume();
			TryScoop(0.6f);
			return;
		}

		if (_decisionTimer > 0f)
			return;
		_decisionTimer = DecisionInterval * Rng.RandfRange(0.8f, 1.25f);

		// Hopping over its fissures → gravel to catch them in the air.
		bool gravelRange = absDistance >= GravelMinDistance && absDistance <= GravelMaxDistance;
		if (gravelRange && airborne && facingPlayer && _gravelCooldownTimer <= 0f && Rng.Randf() < GravelVsAirborneChance)
		{
			TryScoop(1f);
			return;
		}

		// Point-blank: mix the quick scoop into the slow smash so it can't be face-tanked.
		if (absDistance <= AttackRange && facingPlayer && Rng.Randf() < CloseScoopChance)
		{
			TryScoop(1f);
			return;
		}

		// Artillery: smash from range down the lane at a grounded player.
		if (inLane && !airborne && facingPlayer && absDistance >= ArtilleryMinDistance && absDistance <= ArtilleryMaxDistance
			&& Rng.Randf() < ArtilleryChance)
		{
			TryAttackNow();
			return;
		}

		if (gravelRange && facingPlayer && _gravelCooldownTimer <= 0f && Rng.Randf() < 0.25f)
			TryScoop(1f);
	}

	// Don't smash at a player who's in the air right over the lane — wait for the landing.
	protected override bool ReadyToCommitAttack() => PlayerRef is null || PlayerRef.IsOnFloor();

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta) =>
		SpacingMoveX(distanceX, currentVelocityX, StopDistance, 0f);

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (_scooping)
			return;
		base.UpdateAnimation(velocity);
	}

	// ── Flash ───────────────────────────────────────────────────────────────────────────────────

	private void FlashBody(float delay, float duration)
	{
		if (Sprite is null || duration <= 0f)
			return;
		_flashTween?.Kill();
		_flashTween = CreateTween();
		if (delay > 0f)
			_flashTween.TweenInterval(delay);
		float half = duration / Mathf.Max(1, FlashBlinks) * 0.5f;
		for (int i = 0; i < FlashBlinks; i++)
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

	// ── Smash ───────────────────────────────────────────────────────────────────────────────────

	// Full replacement of MeleeEnemy.Attack (the sword lying in the floor for frames 10-19 must not
	// keep hurting, so the hit window is its own), with the same slot/cooldown bookkeeping.
	protected override async Task Attack()
	{
		Attacking = true;
		CanAttack = false;
		WindupScale = 1f;
		Sprite.Play("attack");
		// Sword up over frames 4-8.
		FlashBody(4f / SheetFps, 5f / SheetFps);

		try
		{
			await ToSignal(GetTree().CreateTimer(WindupDuration), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;
			ClearFlash();

			float sign = FacingRight ? 1f : -1f;
			_trail.Play(SmashTrailCenter, SmashTrailRadius, SmashTrailStartAngle, SmashTrailEndAngle, SmashTrailDuration);
			if (TryFindGround(GlobalPosition.X + sign * SmashImpactOffsetX, out Vector2 impact))
			{
				GroundSlamFx.Impact(this, impact, 0.9f, Palette);
				RunFissure(sign, impact);
			}
			ShakePlayerCamera(SmashShake, 0.22f);

			AttackHitbox.Position = new Vector2(sign * AttackHitboxReach, 0f);
			AttackHitbox.Activate(Stats);
			await ToSignal(GetTree().CreateTimer(SmashHitWindow), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;
			AttackHitbox.Deactivate();

			float remaining = Mathf.Max(0f, AttackAnimDuration - WindupDuration - SmashHitWindow);
			await ToSignal(GetTree().CreateTimer(remaining), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;

			// Pull the sword out (frames 20-23) instead of snapping straight back to idle.
			Sprite.Frame = PullOutFrame;
			await ToSignal(GetTree().CreateTimer(PullOutDuration), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;
		}
		finally
		{
			if (IsInstanceValid(this))
			{
				AttackHitbox.Deactivate();
				ClearFlash();
				Attacking = false;
				// OnDefeated may already have released it (death mid-swing).
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

	// A glowing crack racing forward along the floor from the blade's impact, carrying a hitbox at
	// its front, chipping the floor as it goes and erupting in rock spikes where it ends. Stops at
	// a pit or a wall. Runs on its own, so it finishes even if the attacker dies mid-way.
	private async void RunFissure(float sign, Vector2 start)
	{
		Node scene = GetTree().CurrentScene;
		var fissure = GroundSlamFx.BeginFissure(this, Palette);
		Hazard hazard = Hazard.CreateArea(scene, instantKill: false, damage: FissureDamage, knockbackForce: 200f);
		hazard.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = FissureHitboxSize } });
		hazard.GlobalPosition = start - new Vector2(0f, FissureHitboxSize.Y * 0.5f);
		fissure?.AddPoint(start);

		SceneTree tree = GetTree();
		PhysicsDirectSpaceState2D space = GetWorld2D().DirectSpaceState;
		Rid self = GetRid();
		float bodyTop = GlobalPosition.Y - 10f;
		float bodyFeetSearch = GlobalPosition.Y + BodyBottom + 60f;
		float midHeight = start.Y - FissureHitboxSize.Y * 0.6f;

		Vector2 front = start;
		float travelled = 0f;
		float sinceChip = 0f;
		try
		{
			while (travelled < FissureLength)
			{
				await hazard.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
				if (!IsInstanceValid(hazard))
					return;
				float step = FissureSpeed * (float)(1.0 / Engine.PhysicsTicksPerSecond);
				float nextX = front.X + sign * step;

				// Wall in the way → stop here.
				var wallQuery = PhysicsRayQueryParameters2D.Create(new Vector2(front.X, midHeight), new Vector2(nextX, midHeight),
					WallMask, new Godot.Collections.Array<Rid> { self });
				if (space.IntersectRay(wallQuery).Count > 0)
					break;
				// No floor (pit/ledge) → stop here.
				var floorQuery = PhysicsRayQueryParameters2D.Create(new Vector2(nextX, bodyTop), new Vector2(nextX, bodyFeetSearch),
					WorldMask, new Godot.Collections.Array<Rid> { self });
				var hit = space.IntersectRay(floorQuery);
				if (hit.Count == 0)
					break;

				Vector2 ground = (Vector2)hit["position"];
				travelled += Mathf.Abs(nextX - front.X);
				sinceChip += step;
				front = ground;
				fissure?.AddPoint(ground);
				hazard.GlobalPosition = ground - new Vector2(0f, FissureHitboxSize.Y * 0.5f);
				if (sinceChip >= FissureChipEvery)
				{
					sinceChip = 0f;
					GroundSlamFx.Chip(hazard, ground, 0.45f, Palette);
				}
			}
		}
		finally
		{
			fissure?.Finish();
			if (IsInstanceValid(hazard))
				hazard.QueueFree();
		}

		// Eruption at the end of the crack.
		if (scene is null || !IsInstanceValid(scene))
			return;
		GroundSlamFx.EarthSpikes(scene, front, EruptionSize, EruptionHold, Palette);
		GroundSlamFx.Impact(scene, front, 0.6f, Palette);
		if (IsInstanceValid(this) && !IsQueuedForRemoval)
			ShakePlayerCamera(EruptionShake, 0.18f);

		Hazard eruption = Hazard.CreateArea(scene, instantKill: false, damage: EruptionDamage, knockbackForce: 280f);
		eruption.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = EruptionSize } });
		eruption.GlobalPosition = front - new Vector2(0f, EruptionSize.Y * 0.5f);
		await eruption.ToSignal(tree.CreateTimer(0.25f), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(eruption))
			eruption.QueueFree();
	}

	// ── Scoop + gravel ──────────────────────────────────────────────────────────────────────────

	private void TryScoop(float flashScale)
	{
		if (Attacking || !CanAttack || !IsOnFloor() || !EnemyCombatCoordinator.TryAcquireAttackSlot())
			return;
		HoldingAttackSlot = true;
		_ = Scoop(flashScale);
	}

	// Frames 0-3 only: the low sword swings up to the front, flinging gravel, then it recovers
	// without going into the overhead smash.
	private async Task Scoop(float flashScale)
	{
		Attacking = true;
		CanAttack = false;
		_scooping = true;
		_gravelCooldownTimer = GravelCooldown;
		float flash = ScoopFlashDuration * flashScale;

		Sprite.Play("attack");
		Sprite.Pause();
		Sprite.Frame = 1;
		FlashBody(0f, flash);

		try
		{
			await ToSignal(GetTree().CreateTimer(flash), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;
			ClearFlash();

			Sprite.Frame = ScoopHitFrame;
			float sign = FacingRight ? 1f : -1f;
			_trail.Play(ScoopTrailCenter, ScoopTrailRadius, ScoopTrailStartAngle, ScoopTrailEndAngle, ScoopTrailDuration);
			if (TryFindGround(GlobalPosition.X + sign * 30f, out Vector2 scraped))
				GroundSlamFx.Chip(this, scraped, 0.7f, Palette);
			FlingGravel(sign);
			ShakePlayerCamera(1.5f, 0.1f);

			AttackHitbox.Position = new Vector2(sign * AttackHitboxReach, 0f);
			AttackHitbox.Activate(Stats);
			await ToSignal(GetTree().CreateTimer(ScoopHitWindow), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;
			AttackHitbox.Deactivate();

			await ToSignal(GetTree().CreateTimer(ScoopRecovery), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;
		}
		finally
		{
			if (IsInstanceValid(this))
			{
				AttackHitbox.Deactivate();
				ClearFlash();
				_scooping = false;
				Attacking = false;
				if (HoldingAttackSlot)
				{
					EnemyCombatCoordinator.ReleaseAttackSlot();
					HoldingAttackSlot = false;
				}
			}
		}

		float jitter = 1f + Rng.RandfRange(-AttackCooldownJitter, AttackCooldownJitter);
		await ToSignal(GetTree().CreateTimer(AttackCooldown * 0.6f * jitter), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			CanAttack = true;
	}

	// A spray of rocks lobbed forward on spread-out arcs that roughly bracket the player's spot.
	private void FlingGravel(float sign)
	{
		Node scene = GetTree().CurrentScene;
		if (scene is null)
			return;
		Vector2 origin = GlobalPosition + new Vector2(sign * 34f, -6f);
		float targetDx = PlayerRef is { } player ? Mathf.Abs(player.GlobalPosition.X - origin.X) : 120f;
		for (int i = 0; i < GravelCount; i++)
		{
			float vy = -Rng.RandfRange(230f, 360f);
			float airTime = 2f * -vy / GravelGravity;
			float reach = Mathf.Clamp(targetDx, 60f, GravelMaxDistance) * Rng.RandfRange(0.7f, 1.25f);
			var velocity = new Vector2(sign * reach / airTime, vy);

			var rock = new Area2D { CollisionLayer = 0, CollisionMask = WorldMask | 2u };
			rock.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 4f } });
			rock.AddChild(GroundSlamFx.MakeRock(Rng.RandfRange(3.5f, 5.5f), Palette));
			scene.AddChild(rock);
			rock.GlobalPosition = origin + new Vector2(Rng.RandfRange(-4f, 4f), Rng.RandfRange(-4f, 4f));
			FlyGravel(rock, velocity, GravelGravity, GravelLifetime, GravelDamage);
		}
	}

	private static async void FlyGravel(Area2D rock, Vector2 velocity, float gravity, float lifetime, int damage)
	{
		bool done = false;
		float spin = Rng.RandfRange(-14f, 14f);
		void Land(bool chip)
		{
			if (done || !IsInstanceValid(rock))
				return;
			done = true;
			if (chip)
				GroundSlamFx.Chip(rock, rock.GlobalPosition, 0.3f, Palette);
			rock.QueueFree();
		}

		rock.BodyEntered += body =>
		{
			if (done)
				return;
			if (body.IsInGroup("player"))
			{
				if (body.GetNodeOrNull<Stats>("Stats") is { IsInvulnerable: false } stats)
					stats.TakeDamage(damage, isProjectile: true);
				Land(true);
			}
			else if (velocity.Y > 0f)
			{
				Land(true);
			}
		};

		SceneTree tree = rock.GetTree();
		float age = 0f;
		while (!done && IsInstanceValid(rock))
		{
			await rock.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
			if (done || !IsInstanceValid(rock))
				return;
			float dt = (float)(1.0 / Engine.PhysicsTicksPerSecond);
			age += dt;
			velocity.Y += gravity * dt;
			rock.GlobalPosition += velocity * dt;
			rock.Rotation += spin * dt;
			if (age >= lifetime)
				Land(false);
		}
	}

	// ── World queries ───────────────────────────────────────────────────────────────────────────

	private static uint WorldMask => 1u | Metroidvania.Shared.PhysicsLayers.ClimbableWalls | Metroidvania.Shared.PhysicsLayers.OneWayPlatforms;
	// Walls only: a one-way platform at the crack's height must not stop it.
	private static uint WallMask => 1u | Metroidvania.Shared.PhysicsLayers.ClimbableWalls;

	private bool TryFindGround(float x, out Vector2 point)
	{
		point = default;
		var query = PhysicsRayQueryParameters2D.Create(new Vector2(x, GlobalPosition.Y - 10f), new Vector2(x, GlobalPosition.Y + BodyBottom + 60f),
			WorldMask, new Godot.Collections.Array<Rid> { GetRid() });
		var hit = GetWorld2D().DirectSpaceState.IntersectRay(query);
		if (hit.Count == 0)
			return false;
		point = (Vector2)hit["position"];
		return true;
	}

	private void ShakePlayerCamera(float strength, float duration)
	{
		if (PlayerReads.Find(this) is { } player && player.GlobalPosition.DistanceTo(GlobalPosition) < 700f)
			player.ShakeCamera(strength, duration);
	}
}
