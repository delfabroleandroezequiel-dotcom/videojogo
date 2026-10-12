using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Limo ácido / Acid Slime (Monsters Creatures Fantasy 2, 156x156 cells, faces right). A slow puddle
// of acid with a skull floating inside it: it surges out in a long wave, spits acid that pools on
// the floor, and splits in two when it dies.
// Sheet beats used:
//   attack (19 frames): 3-8 rearing up into a tall blob (the wind-up), 9 lunging, 10-14 stretched
//     out in a long skull-faced wave spitting acid droplets, 15-18 pulling back together.
//   hurt: wobble. walk: oozing along.
// Moves:
//   Surge — rears up (flashing), then stretches into a wave that reaches far in front (~110px) and
//     spits acid globs ahead that leave poison puddles (AcidGlob → PoisonPuddle).
//   Gelatinous — knockback barely moves it.
//   Split — the big one bursts into two small slimes when it dies (they don't split again, drop
//     nothing and leave no save mark — same idea as the Necromancer's summons).
// Telegraphs: body flashes white while it rears up.
public partial class AcidSlime : Enemy
{
	[ExportGroup("Surge")]
	[Export] public float SurgeRange = 100f;
	[Export] public float AttackCooldown = 2f;
	[Export] public float RearTime = 0.55f;
	[Export] public float SurgeDamageMultiplier = 1f;
	[Export] public float SurgeKnockback = 220f;
	// Measured off attack frames 11-14 (scale 1.5, sprite at 0.75,0): the wave reaches ~114px in
	// front, from the floor up to ~26px.
	[Export] public Vector2 SurgeHitboxCenter = new(66f, -6f);
	[Export] public Vector2 SurgeHitboxSize = new(96f, 40f);
	[Export] public PackedScene PuddleScene;
	[Export] public int Globs = 3;

	[ExportGroup("Split")]
	[Export] public int SplitCount = 2;
	[Export] public float SmallScale = 0.6f;
	[Export] public int SmallHealth = 10;

	[ExportGroup("Look")]
	[Export] public Color FlashTint = new(2.2f, 2.2f, 2.2f, 1f);

	// Set on the halves spawned by Split (before adding them to the tree).
	public bool IsSmall;
	public const string SplitPersistenceId = "acid_slime_split";

	private static readonly RandomNumberGenerator Rng = new();

	private Tween _flashTween;
	private bool _busy;
	private float _attackCooldownTimer;

	static AcidSlime()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_attackCooldownTimer = Rng.RandfRange(0.5f, 1.2f);
		if (IsSmall)
		{
			LootTable = System.Array.Empty<LootEntry>();
			DropsShadowEssence = false;
			Visual.Scale = new Vector2(SmallScale, SmallScale);
			MoveSpeed *= 1.4f;
			Stats.MaxHealth = SmallHealth;
			Stats.ResetToFull();
			// A fresh, smaller shape — never resize the one shared with every other slime instance.
			foreach (var path in new[] { "CollisionShape2D", "ContactArea/CollisionShape2D" })
				if (GetNode<CollisionShape2D>(path) is { Shape: RectangleShape2D rect } shape)
					shape.Shape = new RectangleShape2D { Size = rect.Size * SmallScale };
		}
	}

	protected override bool IsDefeated() => !IsSmall && base.IsDefeated();

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;
		_attackCooldownTimer -= (float)delta;
		var player = PlayerReads.Find(this);
		base._PhysicsProcess(delta);
		// Small halves keep their shrunken visual (Enemy re-applies Visual.Scale for facing).
		if (IsSmall)
			Visual.Scale = new Vector2((FacingRight ? 1f : -1f) * SmallScale, SmallScale);
		if (IsQueuedForRemoval || player is null || !PlayerDetected || _busy || !IsOnFloor())
			return;
		float absDistance = Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X);
		if (absDistance <= SurgeRange * Reach() && _attackCooldownTimer <= 0f)
			_ = Surge();
	}

	private float Reach() => IsSmall ? SmallScale : 1f;

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_busy)
			return 0f;
		float toward = Mathf.Sign(distanceX);
		if (Mathf.Abs(distanceX) > SurgeRange * Reach() * 0.7f && CanStepTo(toward))
			return toward * MoveSpeed;
		return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
	}

	protected override bool CanTurnToFacePlayer => !_busy;

	// Gelatinous: shoves barely move it.
	public override void ApplyKnockback(Vector2 direction, float force) => base.ApplyKnockback(direction, force * 0.25f);

	// ── Surge ───────────────────────────────────────────────────────────────────────────────

	private async Task Surge()
	{
		if (!EnemyCombatCoordinator.TryAcquireAttackSlot())
		{
			_attackCooldownTimer = 0.3f;
			return;
		}
		HoldingAttackSlot = true;
		_busy = true;
		try
		{
			if (PlayerReads.Find(this) is { } player)
			{
				FacingRight = player.GlobalPosition.X >= GlobalPosition.X;
				Visual.Scale = new Vector2((FacingRight ? 1f : -1f) * Reach(), Reach());
			}
			Sprite.Play("attack");
			Sprite.Pause();
			FlashBody(RearTime);
			for (int f = 1; f <= 8; f++)
			{
				Sprite.Frame = f;
				if (!await Wait(RearTime / 8f))
					return;
			}
			bool hit = false;
			for (int f = 9; f <= 18; f++)
			{
				Sprite.Frame = f;
				if (f >= 10 && f <= 14 && !hit)
					hit = Strike();
				if (f == 11)
					SpitGlobs();
				if (!await Wait(1f / 12f))
					return;
			}
		}
		finally
		{
			if (IsInstanceValid(this))
			{
				_busy = false;
				ClearFlash();
				if (HoldingAttackSlot)
				{
					EnemyCombatCoordinator.ReleaseAttackSlot();
					HoldingAttackSlot = false;
				}
			}
		}
		_attackCooldownTimer = AttackCooldown * Rng.RandfRange(0.85f, 1.25f);
	}

	private bool Strike()
	{
		float r = Reach();
		Vector2 center = SurgeHitboxCenter * r;
		Vector2 at = GlobalPosition + new Vector2(FacingRight ? center.X : -center.X, center.Y);
		var query = new PhysicsShapeQueryParameters2D
		{
			Shape = new RectangleShape2D { Size = SurgeHitboxSize * r },
			Transform = new Transform2D(0f, at),
			CollisionMask = 2,
			CollideWithBodies = true,
		};
		foreach (var hit in GetWorld2D().DirectSpaceState.IntersectShape(query, 8))
		{
			if (hit["collider"].AsGodotObject() is not Metroidvania.Player.Player player)
				continue;
			if (player.GetNodeOrNull<Stats>("Stats") is not { } stats || stats.IsInvulnerable)
				continue;
			int before = stats.CurrentHealth;
			stats.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(Stats.AttackPower * SurgeDamageMultiplier)), element: DamageElement.Poison);
			if (stats.CurrentHealth < before)
				player.ApplyKnockback(new Vector2(FacingRight ? 1f : -1f, -0.3f).Normalized(), SurgeKnockback);
			return true;
		}
		return false;
	}

	private void SpitGlobs()
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		int count = IsSmall ? 1 : Globs;
		Vector2 mouth = GlobalPosition + new Vector2((FacingRight ? 1f : -1f) * 100f * Reach(), -14f * Reach());
		for (int i = 0; i < count; i++)
		{
			float speed = Rng.RandfRange(120f, 240f);
			var glob = new AcidGlob
			{
				Velocity = new Vector2((FacingRight ? 1f : -1f) * speed, Rng.RandfRange(-260f, -160f)),
				ThrowerStats = Stats,
				PuddleScene = PuddleScene,
			};
			scene.AddChild(glob);
			glob.GlobalPosition = mouth;
		}
	}

	// ── Split ───────────────────────────────────────────────────────────────────────────────

	protected override void OnDefeated()
	{
		ClearFlash();
		if (!IsSmall)
			CallDeferred(MethodName.Split);
		base.OnDefeated();
	}

	private void Split()
	{
		// Its own scene, loaded by path: a scene can't reference itself as an ExtResource.
		if (string.IsNullOrEmpty(SceneFilePath) || GetTree()?.CurrentScene is not Node scene
			|| GD.Load<PackedScene>(SceneFilePath) is not { } splitScene)
			return;
		for (int i = 0; i < SplitCount; i++)
		{
			if (splitScene.Instantiate() is not AcidSlime half)
				continue;
			half.IsSmall = true;
			half.CustomPersistenceId = SplitPersistenceId;
			scene.AddChild(half);
			float side = i % 2 == 0 ? -1f : 1f;
			half.GlobalPosition = GlobalPosition + new Vector2(side * 18f, -6f);
			half.Velocity = new Vector2(side * 140f, -220f);
		}
	}

	// ── Shared ──────────────────────────────────────────────────────────────────────────────

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

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null || _busy)
			return;
		string anim = Mathf.Abs(velocity.X) > 5f ? "walk" : "idle";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	protected override bool ContactDamageEnabled => false;
}
