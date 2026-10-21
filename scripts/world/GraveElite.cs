using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Esqueleto élite / Grave Elite (KBPixelArt Skeleton Enemy Pack, 128x128 cells, faces right). The
// armoured grave skeleton with a two-handed great blade: slow, heavy, and hard to stagger.
// Sheet beats used:
//   attack (19): 1-5 the blade hauled up and back (wind-up), 6 brought down into the floor, 7-16
//     dragged along the ground throwing sparks, 17-18 lifted back up.
//   slide_start (7) dropping low, slide_loop (4) sliding on one knee, slide_end (12) getting up.
//   digout (27): bursts out of the grave (GraveRise). idle / walk / hurt / death.
// Moves:
//   Grave cleaver — wind-up (green blinks), slam with a ground burst, then the dragged blade
//     tears a fissure that runs forward along the floor (jump it).
//   Bone slide — from mid range it drops and slides at you low and fast; jump over it. Getting
//     up afterwards is its slowest recovery.
//   Heavy — only flinches now and then, and never out of an attack.
//   Buried start — like the other grave skeletons.
// Telegraphs: green blinks on both wind-ups; the dropping crouch before the slide.
public partial class GraveElite : Enemy
{
	[ExportGroup("Grave")]
	[Export] public bool StartBuried = true;
	[Export] public float WakeDistance = 170f;
	[Export] public float WakeHeight = 90f;
	[Export] public float RiseTime = 1.6f;

	[ExportGroup("Grave cleaver")]
	[Export] public float CleaverTrigger = 80f;
	[Export] public float CleaverWindup = 0.6f;
	[Export] public float CleaverDamageMultiplier = 1.5f;
	[Export] public float AttackCooldown = 1.8f;
	// Frame 6: the blade comes down ~20-75px in front.
	[Export] public Vector2 CleaverHitboxCenter = new(46f, -4f);
	[Export] public Vector2 CleaverHitboxSize = new(62f, 60f);
	[Export] public float ImpactOffsetX = 66f;
	[Export] public float ImpactScale = 0.6f;

	[ExportGroup("Fissure")]
	[Export] public int FissureSteps = 5;
	[Export] public float FissureStepLength = 28f;
	[Export] public float FissureStepTime = 0.07f;
	[Export] public float FissureDamageMultiplier = 0.8f;
	[Export] public Vector2 FissureHitboxSize = new(26f, 22f);

	[ExportGroup("Bone slide")]
	[Export] public float SlideMinDistance = 110f;
	[Export] public float SlideMaxDistance = 230f;
	[Export] public float SlideCooldown = 4f;
	[Export] public float SlideWindup = 0.45f;
	[Export] public float SlideSpeed = 320f;
	[Export] public float SlideOvershoot = 60f;
	[Export] public float SlideDamageMultiplier = 1.1f;
	// slide_loop: a low wedge from its knee to the blade tip, ankle height.
	[Export] public Vector2 SlideHitboxCenter = new(18f, 16f);
	[Export] public Vector2 SlideHitboxSize = new(52f, 22f);

	[ExportGroup("Look")]
	[Export] public float FlinchChance = 0.3f;
	[Export] public Color FlashTint = new(1.4f, 2.6f, 1.5f, 1f);
	[Export] public Color TrailGlowColor = new(1f, 0.1f, 0.2f, 1f);

	private static readonly RandomNumberGenerator Rng = new();
	private static readonly GroundSlamFx.Palette FissurePalette = new()
	{
		Hot = new Color(1f, 0.15f, 0.25f, 1f),
		Core = new Color(1f, 0.85f, 0.85f, 1f),
		Rock = new Color(0.36f, 0.32f, 0.33f, 1f),
		Dust = new Color(0.55f, 0.5f, 0.5f, 0.6f),
		Light = new Color(1f, 0.25f, 0.3f, 1f),
	};
	private readonly GraveRise _grave = new();

	private EnemySlashTrail _trail;
	private Tween _flashTween;
	private bool _busy;
	private float _moveOverride;
	private float _attackCooldownTimer;
	private float _slideCooldownTimer;

	static GraveElite()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_attackCooldownTimer = Rng.RandfRange(0.3f, 0.8f);
		_slideCooldownTimer = Rng.RandfRange(1f, 2f);
		_trail = new EnemySlashTrail
		{
			CoreWidth = 3f,
			GlowWidth = 11f,
			GlowColor = TrailGlowColor,
			CoreColor = new Color(1f, 0.75f, 0.75f, 1f),
			SparkAmount = 14,
		};
		Visual.AddChild(_trail);
		Stats.IncomingHitInterceptor = () => _grave.Busy;
		Stats.HitTaken += _ => OnHit();
		if (StartBuried)
			_grave.Bury(Sprite);
	}

	private void OnHit()
	{
		if (!_busy && Rng.Randf() < FlinchChance)
			_ = Flinch();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;
		if (_grave.Busy)
		{
			_grave.WatchForPlayer(this, Sprite, WakeDistance, WakeHeight, RiseTime);
			Velocity = Vector2.Zero;
			return;
		}
		float dt = (float)delta;
		_attackCooldownTimer -= dt;
		_slideCooldownTimer -= dt;
		var player = PlayerReads.Find(this);
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval || player is null || !PlayerDetected || _busy || !IsOnFloor())
			return;

		float absDx = Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X);
		float absDy = Mathf.Abs(player.GlobalPosition.Y - GlobalPosition.Y);
		if (absDy > 50f || _attackCooldownTimer > 0f)
			return;
		if (absDx <= CleaverTrigger)
			_ = Cleaver();
		else if (absDx >= SlideMinDistance && absDx <= SlideMaxDistance && _slideCooldownTimer <= 0f)
			_ = BoneSlide(player);
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_busy)
			return _moveOverride == 0f || CanStepTo(Mathf.Sign(_moveOverride)) ? _moveOverride : 0f;
		return base.ComputeMoveX(player, distanceX, currentVelocityX, delta);
	}

	protected override bool CanTurnToFacePlayer => !_busy;

	// ── Grave cleaver + fissure ──────────────────────────────────────────────────────────────────

	private async Task Cleaver()
	{
		if (!Begin())
			return;
		try
		{
			Sprite.Play("attack");
			Sprite.Pause();
			FlashBody(CleaverWindup);
			for (int f = 0; f <= 5; f++)
			{
				Sprite.Frame = f;
				if (!await Wait(CleaverWindup / 6f))
					return;
			}
			ClearFlash();

			Sfx.PlayAt(this, "Combat/Sword", "Sword Attack");
			_trail.Play(new Vector2(10f, -6f), 44f, -110f, 40f, 0.09f, 38f);
			Sprite.Frame = 6;
			Strike(CleaverHitboxCenter, CleaverHitboxSize, CleaverDamageMultiplier, 260f);
			float forward = FacingRight ? 1f : -1f;
			Vector2 ground = GlobalPosition + new Vector2(forward * ImpactOffsetX, BodyBottom);
			GroundSlamFx.Impact(this, ground, ImpactScale, FissurePalette);
			_ = Fissure(forward, ground);

			// The blade dragged along the floor while the fissure runs ahead.
			for (int f = 7; f <= 18; f++)
			{
				Sprite.Frame = f;
				if (!await Wait(f >= 17 ? 0.1f : 0.06f))
					return;
			}
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * Rng.RandfRange(0.85f, 1.25f);
	}

	// Runs forward from the impact along the floor; stops at a wall or a drop. Independent of the
	// attack coroutine so it finishes even if it's hit or killed meanwhile.
	private async Task Fissure(float forward, Vector2 start)
	{
		var trail = GroundSlamFx.BeginFissure(this, FissurePalette);
		trail?.AddPoint(start);
		Vector2 at = start;
		bool hit = false;
		for (int i = 0; i < FissureSteps; i++)
		{
			await ToSignal(GetTree().CreateTimer(FissureStepTime), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this))
				return;
			Vector2 next = at + new Vector2(forward * FissureStepLength, 0f);
			var wall = PhysicsRayQueryParameters2D.Create(at + new Vector2(0f, -8f), next + new Vector2(0f, -8f), 1u);
			if (GetWorld2D().DirectSpaceState.IntersectRay(wall).Count > 0)
				break;
			var floor = PhysicsRayQueryParameters2D.Create(next + new Vector2(0f, -10f), next + new Vector2(0f, 14f), 1u);
			var ground = GetWorld2D().DirectSpaceState.IntersectRay(floor);
			if (ground.Count == 0)
				break;
			at = (Vector2)ground["position"];
			trail?.AddPoint(at);
			GroundSlamFx.Chip(this, at, 0.45f, FissurePalette);
			if (!hit)
				hit = StrikeAt(at + new Vector2(0f, -FissureHitboxSize.Y * 0.5f), FissureHitboxSize, FissureDamageMultiplier, 180f);
		}
		trail?.Finish();
	}

	// ── Bone slide ───────────────────────────────────────────────────────────────────────────────

	private async Task BoneSlide(Metroidvania.Player.Player player)
	{
		if (!Begin())
			return;
		_slideCooldownTimer = SlideCooldown * Rng.RandfRange(0.85f, 1.2f);
		try
		{
			Sprite.Play("slide_start");
			Sprite.Pause();
			FlashBody(SlideWindup);
			int startFrames = Sprite.SpriteFrames.GetFrameCount("slide_start");
			for (int f = 0; f < startFrames; f++)
			{
				Sprite.Frame = f;
				if (!await Wait(SlideWindup / startFrames))
					return;
			}
			ClearFlash();

			float forward = FacingRight ? 1f : -1f;
			float distance = IsInstanceValid(player)
				? Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X) + SlideOvershoot
				: SlideMaxDistance;
			Sprite.Play("slide_loop");
			Sfx.PlayAt(this, "Combat/Sword", "Sword Attack");
			_moveOverride = forward * SlideSpeed;
			float startX = GlobalPosition.X;
			float elapsed = 0f;
			bool hit = false;
			while (Mathf.Abs(GlobalPosition.X - startX) < distance && elapsed < distance / SlideSpeed + 0.3f)
			{
				await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
				if (!IsInstanceValid(this) || IsQueuedForRemoval)
					return;
				elapsed += (float)GetPhysicsProcessDeltaTime();
				if (elapsed > 0.1f && Mathf.Abs(Velocity.X) < 5f)
					break; // a wall or a ledge stopped it
				if (!hit)
					hit = Strike(SlideHitboxCenter, SlideHitboxSize, SlideDamageMultiplier, 240f);
				if ((int)(elapsed / 0.07f) != (int)((elapsed - (float)GetPhysicsProcessDeltaTime()) / 0.07f))
					GroundSlamFx.Chip(this, GlobalPosition + new Vector2(0f, BodyBottom), 0.25f, FissurePalette);
			}
			_moveOverride = 0f;

			// Getting back up — the opening.
			Sprite.Play("slide_end");
			int endFrames = Sprite.SpriteFrames.GetFrameCount("slide_end");
			if (!await Wait(endFrames / (float)Sprite.SpriteFrames.GetAnimationSpeed("slide_end")))
				return;
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * 0.5f;
	}

	// ── Shared ───────────────────────────────────────────────────────────────────────────────────

	private bool Strike(Vector2 center, Vector2 size, float damageMultiplier, float knockback) =>
		StrikeAt(GlobalPosition + new Vector2(FacingRight ? center.X : -center.X, center.Y), size, damageMultiplier, knockback);

	private bool StrikeAt(Vector2 at, Vector2 size, float damageMultiplier, float knockback)
	{
		var query = new PhysicsShapeQueryParameters2D
		{
			Shape = new RectangleShape2D { Size = size },
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
			stats.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(Stats.AttackPower * damageMultiplier)));
			if (stats.CurrentHealth < before)
			{
				float away = Mathf.Sign(player.GlobalPosition.X - GlobalPosition.X);
				player.ApplyKnockback(new Vector2(away == 0f ? 1f : away, -0.35f).Normalized(), knockback);
				BloodEffect.SpawnAt(this, player.GlobalPosition);
			}
			return true;
		}
		return false;
	}

	private async Task Flinch()
	{
		_busy = true;
		try
		{
			Sprite.Play("hurt");
			await ToSignal(GetTree().CreateTimer(0.25f), SceneTreeTimer.SignalName.Timeout);
		}
		finally
		{
			if (IsInstanceValid(this))
				_busy = false;
		}
	}

	private bool Begin()
	{
		if (!EnemyCombatCoordinator.TryAcquireAttackSlot())
		{
			_attackCooldownTimer = 0.3f;
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
		_moveOverride = 0f;
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

	// Too heavy to be shoved around — not by hits, not by the player's body.
	public override bool BodyPushable => false;

	public override void ApplyKnockback(Vector2 direction, float force)
	{
	}

	protected override void OnDefeated()
	{
		ClearFlash();
		base.OnDefeated();
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null || _busy || _grave.Busy)
			return;
		string anim = Mathf.Abs(velocity.X) > 5f ? "walk" : "idle";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	protected override bool ContactDamageEnabled => false;
}
