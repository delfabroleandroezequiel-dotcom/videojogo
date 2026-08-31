using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// Three independent attacks, each on its own cooldown:
// - Ground-fire: her only tell is coming down to ground level before casting, plus a warning
//   marker (reusing SkeletonKingBoss's ThunderMarkerSpriteFrames "marker" + light, until this boss
//   gets its own) held for the same window as the fire building up. Once fully erupted, it summons
//   a BurningDamned at her own position — no ground-tracking for the summon itself, normal gravity
//   carries it down to whatever floor is actually below, same as any other grounded enemy.
//   "recover" (fire dying down) plays after, then she resumes the normal FlyingEnemy hunt.
// - Missile volley: launches her orbiting-skull barrier at the player one at a time (see
//   RunMissileVolley/OrbitingSkull.LaunchAttack) — doesn't freeze her steering since it's the
//   skulls attacking, not her.
// - Rain: ported straight from SkeletonKingBoss (the desert boss) — a room-wide volley of strikes
//   that all telegraph together, leaving a couple of safe gaps to stand in. Reuses his rain bolt
//   (ThunderRainStrikeSpriteFrames) and marker as a deliberate placeholder (explicitly asked to
//   keep the same effect for now, just retinted red instead of his ice-blue), swap RainFramesPath/
//   the marker for something of her own once she gets bespoke art for this attack.
// The sheet's 4th row (smoke-vanish/teleport) is still real but unused — no teleport behavior yet.
public partial class LargeSkullBoss : FlyingEnemy
{
	[Export] public PackedScene BurningDamnedScene;
	[Export] public float AttackGroundY;
	[Export] public float AttackCooldown = 5f;
	// How far into the "attack" animation (10 frames @ 8fps = 1.25s) the fire is fully erupted and
	// the minion actually spawns — measured against the sheet's own frames, not guessed: the flame
	// is still building through frame ~6 and is fully engulfing the base by frame ~7.
	[Export] public float FireEruptDelay = 0.875f;
	[Export] public float GroundProximityThreshold = 20f;
	[Export] public float DescendSpeed = 140f;

	[Export] public string MarkerFramesPath = "res://resources/sprites/ThunderMarkerSpriteFrames.tres";
	[Export] public string LightTexturePath = "res://resources/lighting/PointLightGradient.tres";
	[Export] public Color MarkerLightColor = new(1f, 0.2f, 0.15f, 1f);
	[Export] public float MarkerLightEnergy = 0.9f;
	[Export] public float MarkerLightScale = 1.3f;

	// Missile volley: sends her orbiting-skull barrier at the player one at a time, each one
	// homing in and detonating on its own (see OrbitingSkull.LaunchAttack). Independent cooldown
	// from the ground-fire attack — doesn't freeze her steering, since it's the skulls doing the
	// attacking, not her casting something with a windup animation. Permanently consumes whichever
	// skulls it launches, so using this thins out her own barrier — that trade-off is deliberate.
	[Export] public float MissileVolleyCooldown = 8f;
	[Export] public float MissileLaunchStagger = 0.25f;

	// Whether they were killed by the player or consumed by a missile volley, once the barrier is
	// completely empty it refills back to a full ring — checked every physics frame (cheap: just a
	// child count) rather than tracked through every individual death/launch path.
	[Export] public PackedScene OrbitingSkullScene;
	[Export] public int SkullBarrierCount = 8;

	// Red instead of his ice-blue, per request — applied to the bolt sprite's modulate and both
	// lights so the whole effect reads as her fire rather than his lightning.
	[Export] public Color BoltModulate = new(1f, 0.3f, 0.25f, 1f);
	[Export] public Color LightningLightColor = new(1f, 0.25f, 0.2f, 1f);
	[Export] public float LightningFlashEnergy = 2.2f;
	[Export] public float LightningFlashScale = 4f;
	[Export] public float LightningFlashDuration = 0.25f;
	[Export] public float MapMinX = 0f;
	[Export] public float MapMaxX = 1774f;

	[Export] public string RainFramesPath = "res://resources/sprites/ThunderRainStrikeSpriteFrames.tres";
	[Export] public Vector2 RainVfxOffset = new(0f, -563.2f);
	[Export] public float RainVfxScale = 2.2f;
	[Export] public float RainCooldown = 9f;
	[Export] public int RainStrikeCount = 14;
	[Export] public int RainSafeGapCount = 1;
	[Export] public float RainTelegraphDuration = 1.1f;
	[Export] public float RainActiveDuration = 0.3f;
	[Export] public int RainDamage = 20;
	[Export] public Vector2 RainHitboxSize = new(70f, 100f);

	private bool _isAttacking;
	private bool _isDescending;
	private float _attackTimer;
	private float _missileTimer;
	private float _rainTimer;
	private readonly RandomNumberGenerator _rng = new();

	// Skulls launched as missiles get reparented out of SkullBarrier (see OrbitingSkull.LaunchAttack)
	// while still very much alive and chasing the player, so "the barrier container is empty" isn't
	// the right question — this counts every skull this boss currently owns, wherever it is, and only
	// refills once all of them are actually gone (see OrbitingSkull.SkullDestroyed).
	private int _aliveSkullCount;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;

		_rng.Randomize();
		_attackTimer = AttackCooldown;
		_missileTimer = MissileVolleyCooldown;
		_rainTimer = RainCooldown;

		if (GetNodeOrNull("SkullBarrier") is Node skullBarrier)
		{
			foreach (Node child in skullBarrier.GetChildren())
			{
				if (child is OrbitingSkull skull)
					TrackSkull(skull);
			}
		}
	}

	private void TrackSkull(OrbitingSkull skull)
	{
		_aliveSkullCount++;
		skull.SkullDestroyed += () => _aliveSkullCount--;
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval)
			return;

		RefillSkullBarrierIfEmpty();
	}

	private void RefillSkullBarrierIfEmpty()
	{
		if (_aliveSkullCount > 0 || GetNodeOrNull("SkullBarrier") is not Node skullBarrier || OrbitingSkullScene is null)
			return;

		for (int i = 0; i < SkullBarrierCount; i++)
		{
			var skull = (OrbitingSkull)OrbitingSkullScene.Instantiate();
			skull.StartAngle = Mathf.Tau / SkullBarrierCount * i;
			skullBarrier.AddChild(skull);
			TrackSkull(skull);
		}
	}

	protected override bool OverrideSteering(double delta)
	{
		if (_isAttacking)
			return true;

		if (_isDescending)
		{
			if (Mathf.Abs(GlobalPosition.Y - AttackGroundY) <= GroundProximityThreshold)
			{
				_isDescending = false;
				_ = RunFireAttack();
				return true;
			}

			Anchor += new Vector2(0f, Mathf.Sign(AttackGroundY - GlobalPosition.Y) * DescendSpeed * (float)delta);
			GlobalPosition = Anchor;
			return true;
		}

		if (Hunting)
		{
			_attackTimer -= (float)delta;
			if (_attackTimer <= 0f)
			{
				_attackTimer = AttackCooldown;
				if (Mathf.Abs(GlobalPosition.Y - AttackGroundY) <= GroundProximityThreshold)
					_ = RunFireAttack();
				else
					_isDescending = true;
			}

			_missileTimer -= (float)delta;
			if (_missileTimer <= 0f)
			{
				_missileTimer = MissileVolleyCooldown;
				_ = RunMissileVolley();
			}

			_rainTimer -= (float)delta;
			if (_rainTimer <= 0f)
			{
				_rainTimer = RainCooldown;
				_ = RunRainAttack();
			}
		}

		return false;
	}

	// RainSafeGapCount of the RainStrikeCount points are skipped entirely (both telegraph and
	// strike) — those are the "eggs" the player needs to be standing on when the rest land. Fires
	// every strike's own task up front (not awaited in sequence) so they all telegraph together and
	// land together, instead of one at a time.
	private async Task RunRainAttack()
	{
		if (RainStrikeCount <= 0)
			return;

		_isAttacking = true;

		bool[] isSafe = new bool[RainStrikeCount];
		int safeCount = Mathf.Clamp(RainSafeGapCount, 0, RainStrikeCount);
		while (safeCount > 0)
		{
			int index = _rng.RandiRange(0, RainStrikeCount - 1);
			if (isSafe[index])
				continue;
			isSafe[index] = true;
			safeCount--;
		}

		float step = RainStrikeCount > 1 ? (MapMaxX - MapMinX) / (RainStrikeCount - 1) : 0f;
		for (int i = 0; i < RainStrikeCount; i++)
		{
			if (isSafe[i])
				continue;

			float x = MapMinX + step * i;
			StrikeAt(new Vector2(x, AttackGroundY), RainTelegraphDuration, RainActiveDuration, RainDamage,
				new RectangleShape2D { Size = RainHitboxSize }, RainFramesPath, RainVfxOffset, RainVfxScale);
		}

		await ToSignal(GetTree().CreateTimer(RainTelegraphDuration + RainActiveDuration), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			_isAttacking = false;
	}

	// Ported from SkeletonKingBoss.StrikeAt — marker held for telegraphDuration, then the bolt plays
	// and a hazard sits active for activeDuration.
	private async void StrikeAt(Vector2 position, float telegraphDuration, float activeDuration, int damage,
		Shape2D hazardShape, string framesPath, Vector2 vfxOffset, float vfxScale)
	{
		var marker = new AnimatedSprite2D
		{
			SpriteFrames = GD.Load<SpriteFrames>(MarkerFramesPath),
			Animation = "marker",
			Modulate = BoltModulate,
		};
		GetTree().CurrentScene.AddChild(marker);
		marker.GlobalPosition = position;
		marker.Play("marker");
		marker.AddChild(new PointLight2D
		{
			Color = MarkerLightColor,
			Energy = MarkerLightEnergy,
			Texture = GD.Load<Texture2D>(LightTexturePath),
			TextureScale = MarkerLightScale,
		});

		await ToSignal(GetTree().CreateTimer(telegraphDuration), SceneTreeTimer.SignalName.Timeout);
		marker.QueueFree();
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		VfxSpawner.SpawnAt(this, position, framesPath, "strike", vfxOffset, vfxScale, BoltModulate);
		SpawnLightFlash(position, LightningLightColor, LightningFlashEnergy, LightningFlashScale, LightningFlashDuration);

		Hazard hazard = Hazard.CreateArea(this, instantKill: false, damage: damage, knockbackForce: 220f);
		hazard.GlobalPosition = position;
		hazard.AddChild(new CollisionShape2D { Shape = hazardShape });

		await ToSignal(GetTree().CreateTimer(activeDuration), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(hazard))
			hazard.QueueFree();
	}

	private async void SpawnLightFlash(Vector2 position, Color color, float energy, float textureScale, float duration)
	{
		var light = new PointLight2D
		{
			Color = color,
			Energy = energy,
			Texture = GD.Load<Texture2D>(LightTexturePath),
			TextureScale = textureScale,
		};
		GetTree().CurrentScene.AddChild(light);
		light.GlobalPosition = position;

		await ToSignal(GetTree().CreateTimer(duration), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(light))
			light.QueueFree();
	}

	private async Task RunMissileVolley()
	{
		Node skullBarrier = GetNodeOrNull("SkullBarrier");
		if (skullBarrier is null)
			return;

		// Snapshot first — launching a skull reparents it out of SkullBarrier, which would mutate
		// GetChildren() mid-iteration otherwise.
		var skulls = new List<OrbitingSkull>();
		foreach (Node child in skullBarrier.GetChildren())
		{
			if (child is OrbitingSkull skull)
				skulls.Add(skull);
		}

		foreach (OrbitingSkull skull in skulls)
		{
			if (!IsInstanceValid(skull))
				continue;

			skull.LaunchAttack();
			await ToSignal(GetTree().CreateTimer(MissileLaunchStagger), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;
		}
	}

	private async Task RunFireAttack()
	{
		_isAttacking = true;
		Sprite.Play("attack");

		var marker = new AnimatedSprite2D
		{
			SpriteFrames = GD.Load<SpriteFrames>(MarkerFramesPath),
			Animation = "marker",
		};
		GetTree().CurrentScene.AddChild(marker);
		marker.GlobalPosition = GlobalPosition;
		marker.Play("marker");
		marker.AddChild(new PointLight2D
		{
			Color = MarkerLightColor,
			Energy = MarkerLightEnergy,
			Texture = GD.Load<Texture2D>(LightTexturePath),
			TextureScale = MarkerLightScale,
		});

		await ToSignal(GetTree().CreateTimer(FireEruptDelay), SceneTreeTimer.SignalName.Timeout);
		marker.QueueFree();
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		SpawnBurningDamned();

		await ToSignal(Sprite, AnimatedSprite2D.SignalName.AnimationFinished);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		Sprite.Play("recover");
		await ToSignal(Sprite, AnimatedSprite2D.SignalName.AnimationFinished);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		Sprite.Play("fly");
		_isAttacking = false;
	}

	private void SpawnBurningDamned()
	{
		if (BurningDamnedScene is not PackedScene scene)
			return;

		var damnedNode = (Node2D)scene.Instantiate();
		damnedNode.GlobalPosition = GlobalPosition;
		GetTree().CurrentScene.AddChild(damnedNode);

		// She's solid to everything else, but a minion she just summoned right on top of her
		// shouldn't physically shove against her — let it pass through her specifically.
		if (damnedNode is CharacterBody2D damnedBody)
			damnedBody.AddCollisionExceptionWith(this);
	}
}
