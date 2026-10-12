using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Piromante / Pyromancer (Evil Wizard, 150x150 cells, faces right). A fire-robed sorcerer: a roaring
// flame stream up close, fireballs at range, and a trail of burning ground when he runs.
// Sheet beats used:
//   attack (8, looping): the flamethrower — a jet of fire from his hand, held as long as he wants.
//   run: fleeing / repositioning.
// Moves:
//   Flame stream — holds the jet on you for StreamTime: small Fire ticks (no i-frames) and the
//     tip sets the floor ablaze (GroundFire).
//   Fireball — at range he flicks the same pose into a fireball (ProtaFireball) that bursts into
//     fire on the ground.
//   Burning retreat — crowd him and he runs off, leaving patches of fire behind him.
// Telegraphs: body flashes orange before casting.
public partial class Pyromancer : Enemy
{
	[ExportGroup("Spacing")]
	[Export] public float MinDistance = 90f;
	[Export] public float MaxDistance = 230f;

	[ExportGroup("Flame stream")]
	[Export] public float StreamRange = 75f;
	[Export] public float AttackCooldown = 2.2f;
	[Export] public float StreamTelegraph = 0.35f;
	[Export] public float StreamTime = 1.2f;
	[Export] public float TickInterval = 0.2f;
	[Export] public float TickDamageMultiplier = 0.4f;
	// attack frames (scale 1.1, sprite at 4.4,1.3): the jet reaches ~75px in front at chest height.
	[Export] public Vector2 StreamHitboxCenter = new(48f, -8f);
	[Export] public Vector2 StreamHitboxSize = new(60f, 34f);

	[ExportGroup("Fireball")]
	[Export] public PackedScene GroundFireScene;
	[Export] public float FireballMinDistance = 120f;
	[Export] public float FireballMaxDistance = 360f;
	[Export] public float FireballCooldown = 2.5f;
	[Export] public Vector2 HandOffset = new(24f, -10f);

	[ExportGroup("Burning retreat")]
	[Export] public float RetreatTrigger = 55f;
	[Export] public float RetreatCooldown = 4f;
	[Export] public float RetreatTime = 0.7f;
	[Export] public float RetreatSpeedMultiplier = 1.6f;

	[ExportGroup("Look")]
	[Export] public Color FlashTint = new(2.4f, 1.5f, 0.8f, 1f);
	[Export] public float DecisionInterval = 0.35f;

	private static readonly RandomNumberGenerator Rng = new();

	private Tween _flashTween;
	private bool _busy;
	private bool _retreating;
	private float _moveOverride;
	private float _decisionTimer;
	private float _attackCooldownTimer;
	private float _fireballCooldownTimer;
	private float _retreatCooldownTimer;

	static Pyromancer()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_attackCooldownTimer = Rng.RandfRange(0.5f, 1f);
		_fireballCooldownTimer = Rng.RandfRange(0.5f, 1.2f);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;
		float dt = (float)delta;
		_decisionTimer -= dt;
		_attackCooldownTimer -= dt;
		_fireballCooldownTimer -= dt;
		_retreatCooldownTimer -= dt;
		var player = PlayerReads.Find(this);
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval || player is null || !PlayerDetected || _busy || !IsOnFloor())
			return;

		float absDistance = Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X);
		if (absDistance <= RetreatTrigger && _retreatCooldownTimer <= 0f)
		{
			_ = BurningRetreat(Mathf.Sign(GlobalPosition.X - player.GlobalPosition.X));
			return;
		}
		if (absDistance <= StreamRange && _attackCooldownTimer <= 0f)
		{
			_ = FlameStream();
			return;
		}
		if (_decisionTimer > 0f)
			return;
		_decisionTimer = DecisionInterval;
		if (absDistance >= FireballMinDistance && absDistance <= FireballMaxDistance && _fireballCooldownTimer <= 0f)
			_ = Fireball();
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_busy)
			return _moveOverride == 0f || CanStepTo(Mathf.Sign(_moveOverride)) ? _moveOverride : 0f;
		// Walks into stream range when the stream is ready, otherwise keeps fireball range.
		if (_attackCooldownTimer <= 0f)
		{
			float toward = Mathf.Sign(distanceX);
			if (Mathf.Abs(distanceX) > StreamRange * 0.8f && CanStepTo(toward))
				return toward * MoveSpeed;
			return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
		}
		return KitingMoveX(distanceX, currentVelocityX, MinDistance, MaxDistance, 30f, 1.1f, ref _retreating);
	}

	protected override bool DesiredFacingRight(Node2D player, float distanceX) => _retreating ? distanceX < 0 : distanceX >= 0;

	protected override bool CanTurnToFacePlayer => !_busy;

	// ── Flame stream ────────────────────────────────────────────────────────────────────────

	private async Task FlameStream()
	{
		if (!Begin())
			return;
		try
		{
			Sprite.Play("idle");
			FlashBody(StreamTelegraph);
			if (!await Wait(StreamTelegraph))
				return;
			Sprite.Play("attack");
			float t = 0f;
			float tick = 0f;
			bool lit = false;
			while (t < StreamTime)
			{
				await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
				if (!IsInstanceValid(this) || IsQueuedForRemoval)
					return;
				float dt = (float)GetPhysicsProcessDeltaTime();
				t += dt;
				tick -= dt;
				if (tick <= 0f)
				{
					tick = TickInterval;
					BurnTick();
				}
				// Sets the floor at the jet's tip on fire, once.
				if (!lit && t > StreamTime * 0.4f)
				{
					lit = true;
					SpawnGroundFire(GlobalPosition.X + (FacingRight ? 1f : -1f) * (StreamHitboxCenter.X + StreamHitboxSize.X * 0.4f));
				}
			}
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * Rng.RandfRange(0.85f, 1.25f);
	}

	private void BurnTick()
	{
		Vector2 at = GlobalPosition + new Vector2(FacingRight ? StreamHitboxCenter.X : -StreamHitboxCenter.X, StreamHitboxCenter.Y);
		var query = new PhysicsShapeQueryParameters2D
		{
			Shape = new RectangleShape2D { Size = StreamHitboxSize },
			Transform = new Transform2D(0f, at),
			CollisionMask = 2,
			CollideWithBodies = true,
		};
		foreach (var hit in GetWorld2D().DirectSpaceState.IntersectShape(query, 8))
		{
			if (hit["collider"].AsGodotObject() is not Metroidvania.Player.Player player)
				continue;
			player.GetNodeOrNull<Stats>("Stats")?.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(Stats.AttackPower * TickDamageMultiplier)),
				element: DamageElement.Fire, ignoreInvulnerability: true, armInvulnerability: false);
			return;
		}
	}

	private void SpawnGroundFire(float x)
	{
		if (GroundFireScene is null || GetTree().CurrentScene is not Node scene)
			return;
		var query = PhysicsRayQueryParameters2D.Create(new Vector2(x, GlobalPosition.Y - 10f), new Vector2(x, GlobalPosition.Y + 120f), 1u);
		var hit = GetWorld2D().DirectSpaceState.IntersectRay(query);
		if (hit.Count == 0)
			return;
		var fire = GroundFireScene.Instantiate<Node2D>();
		scene.AddChild(fire);
		fire.GlobalPosition = (Vector2)hit["position"];
	}

	// ── Fireball ────────────────────────────────────────────────────────────────────────────

	private async Task Fireball()
	{
		if (!Begin())
			return;
		_fireballCooldownTimer = FireballCooldown;
		try
		{
			FlashBody(0.3f);
			if (!await Wait(0.3f))
				return;
			Sprite.Play("attack");
			if (GetTree().CurrentScene is Node scene && PlayerReads.Find(this) is { } player)
			{
				Vector2 origin = GlobalPosition + new Vector2(FacingRight ? HandOffset.X : -HandOffset.X, HandOffset.Y);
				Vector2 dir = (player.GlobalPosition - origin).Normalized();
				if (Mathf.Sign(dir.X) != (FacingRight ? 1f : -1f))
					dir = new Vector2(FacingRight ? 1f : -1f, 0f);
				var ball = new ProtaFireball { Direction = dir, ThrowerStats = Stats, GroundFireScene = GroundFireScene, DamageMultiplier = 0.9f, Speed = 280f };
				scene.AddChild(ball);
				ball.GlobalPosition = origin;
			}
			if (!await Wait(0.35f))
				return;
		}
		finally
		{
			Finish();
		}
	}

	// ── Burning retreat ─────────────────────────────────────────────────────────────────────

	private async Task BurningRetreat(float away)
	{
		_busy = true;
		_retreatCooldownTimer = RetreatCooldown;
		try
		{
			FacingRight = away > 0f;
			Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);
			Sprite.Play("run");
			_moveOverride = away * MoveSpeed * RetreatSpeedMultiplier;
			float t = 0f;
			float dropTimer = 0f;
			while (t < RetreatTime)
			{
				await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
				if (!IsInstanceValid(this) || IsQueuedForRemoval)
					return;
				float dt = (float)GetPhysicsProcessDeltaTime();
				t += dt;
				dropTimer -= dt;
				if (dropTimer <= 0f)
				{
					dropTimer = 0.25f;
					SpawnGroundFire(GlobalPosition.X - away * 18f);
				}
			}
		}
		finally
		{
			if (IsInstanceValid(this))
			{
				_moveOverride = 0f;
				_busy = false;
			}
		}
	}

	// ── Shared ──────────────────────────────────────────────────────────────────────────────

	private bool Begin()
	{
		if (!EnemyCombatCoordinator.TryAcquireAttackSlot())
		{
			_attackCooldownTimer = Mathf.Max(_attackCooldownTimer, 0.3f);
			return false;
		}
		HoldingAttackSlot = true;
		_busy = true;
		if (PlayerReads.Find(this) is { } player)
		{
			FacingRight = player.GlobalPosition.X >= GlobalPosition.X;
			Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);
		}
		return true;
	}

	private void Finish()
	{
		if (!IsInstanceValid(this))
			return;
		_busy = false;
		ClearFlash();
		if (HoldingAttackSlot)
		{
			EnemyCombatCoordinator.ReleaseAttackSlot();
			HoldingAttackSlot = false;
		}
	}

	private async Task<bool> Wait(float seconds)
	{
		await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
		return IsInstanceValid(this) && !IsQueuedForRemoval;
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
