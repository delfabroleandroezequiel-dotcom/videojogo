using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Buitre / Vulture (Desert enemy pack, 160x96 cells, faces LEFT — mirrored on the scene). A
// carrion bird: it waits perched, circles high over you, and drops in dive-bombs — more eagerly the
// weaker you get.
// Sheet beats used:
//   idle: perched on the ground. take_off: launching into the air. fly: the flapping loop.
//   dive_start: tucking in (the wind-up). dive: the streamlined plunge, head first.
//   dive_end: pulling out of the dive, wings flared.
//   death_start → death_fall_loop → death_landing: shot down, tumbling, hitting the ground.
// Moves:
//   Perch — sits on the ground until you come near, then takes off.
//   Circle — wheels above you at CircleHeight, drifting from side to side out of sword reach.
//   Dive-bomb — tucks in (flashing), then plunges at where you're heading, talons first, and pulls
//     up past you. Flies into walls/floor → it pulls up early.
//   Carrion scent — below CarrionHealthRatio of your health it dives twice as often.
//   Shot down — killed in the air, it tumbles down and crashes onto the floor.
// Telegraphs: body flashes white during the tuck.
public partial class Vulture : Enemy
{
	[ExportGroup("Flight")]
	[Export] public float CircleHeight = 115f;
	[Export] public float CircleSwing = 90f;
	[Export] public float CircleSwingSpeed = 0.9f;
	[Export] public float FlyAcceleration = 300f;
	[Export] public float PerchedVisualLift = 15f;

	[ExportGroup("Dive")]
	[Export] public float DiveCooldown = 3.2f;
	[Export] public float DiveTuck = 0.45f;
	[Export] public float DiveSpeed = 360f;
	[Export] public float DiveMaxTime = 0.9f;
	[Export] public float DiveLead = 0.35f;
	[Export] public float DiveDamageMultiplier = 1.2f;
	[Export] public float DiveKnockback = 280f;
	[Export] public Vector2 TalonHitboxSize = new(36f, 30f);

	[ExportGroup("Carrion")]
	[Export] public float CarrionHealthRatio = 0.35f;

	[ExportGroup("Look")]
	[Export] public Color FlashTint = new(2.2f, 2.2f, 2.2f, 1f);

	private const float Fps = 12f;

	private static readonly RandomNumberGenerator Rng = new();

	private enum State { Perched, TakingOff, Circling, Diving }

	private State _state = State.Perched;
	private Tween _flashTween;
	private float _time;
	private float _diveCooldownTimer;

	static Vulture()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_time = Rng.Randf() * Mathf.Tau;
		_diveCooldownTimer = Rng.RandfRange(1.2f, 2f);
		Visual.Position = new Vector2(0f, -PerchedVisualLift);
		// Its own copy of the frames with a "death" clip (= death_start), so Enemy's normal death
		// handling (save, loot, essence, collisions) runs and Shot down only adds the fall.
		if (Sprite?.SpriteFrames is { } frames && !frames.HasAnimation("death"))
		{
			var copy = (SpriteFrames)frames.Duplicate();
			copy.AddAnimation("death");
			copy.SetAnimationLoop("death", false);
			copy.SetAnimationSpeed("death", frames.GetAnimationSpeed("death_start"));
			for (int i = 0; i < frames.GetFrameCount("death_start"); i++)
				copy.AddFrame("death", frames.GetFrameTexture("death_start", i));
			Sprite.SpriteFrames = copy;
		}
		Sprite?.Play("idle");
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;
		float dt = (float)delta;
		_time += dt;
		_diveCooldownTimer -= dt;
		var player = PlayerReads.Find(this);
		PlayerDetected = player is not null && GlobalPosition.DistanceTo(player.GlobalPosition) <= DetectionRange;

		switch (_state)
		{
			case State.Perched:
				// Sits on the floor (gravity) until the player comes near.
				Velocity = new Vector2(0f, IsOnFloor() ? 0f : Velocity.Y + Gravity * dt);
				MoveAndSlide();
				if (PlayerDetected)
					_ = TakeOff();
				break;
			case State.Circling:
				Circle(player, dt);
				if (player is not null && PlayerDetected && _diveCooldownTimer <= 0f)
					_ = Dive(player);
				break;
		}
	}

	// ── Flight ──────────────────────────────────────────────────────────────────────────────

	private async Task TakeOff()
	{
		_state = State.TakingOff;
		Sprite.Play("take_off");
		Visual.Position = Vector2.Zero;
		float t = 0f;
		while (t < 7f / Fps)
		{
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;
			float dt = (float)GetPhysicsProcessDeltaTime();
			t += dt;
			Velocity = new Vector2(0f, -140f);
			MoveAndSlide();
		}
		Sprite.Play("fly");
		_state = State.Circling;
	}

	private void Circle(Metroidvania.Player.Player player, float dt)
	{
		Vector2 target = GlobalPosition;
		if (player is not null && PlayerDetected)
			target = player.GlobalPosition + new Vector2(Mathf.Sin(_time * CircleSwingSpeed) * CircleSwing, -CircleHeight);
		Vector2 toTarget = target - GlobalPosition;
		Vector2 desired = toTarget.Length() > 8f ? toTarget.Normalized() * MoveSpeed : Vector2.Zero;
		Velocity = Velocity.MoveToward(desired, FlyAcceleration * dt);
		MoveAndSlide();
		if (Mathf.Abs(Velocity.X) > 10f)
			FaceTowards(GlobalPosition.X + Velocity.X);
		if (Sprite.Animation != "fly")
			Sprite.Play("fly");
	}

	// ── Dive ────────────────────────────────────────────────────────────────────────────────

	private async Task Dive(Metroidvania.Player.Player player)
	{
		if (!EnemyCombatCoordinator.TryAcquireAttackSlot())
		{
			_diveCooldownTimer = 0.4f;
			return;
		}
		HoldingAttackSlot = true;
		_state = State.Diving;
		bool carrion = player.GetNodeOrNull<Stats>("Stats") is { } ps && ps.CurrentHealth <= ps.MaxHealth * CarrionHealthRatio;
		_diveCooldownTimer = DiveCooldown * (carrion ? 0.5f : 1f) * Rng.RandfRange(0.85f, 1.2f);
		try
		{
			// Tuck in, hovering, flashing.
			FaceTowards(player.GlobalPosition.X);
			Sprite.Play("dive_start");
			FlashBody(DiveTuck);
			float t = 0f;
			while (t < DiveTuck)
			{
				await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
				if (!Alive())
					return;
				float dt = (float)GetPhysicsProcessDeltaTime();
				t += dt;
				Velocity = Velocity.MoveToward(new Vector2(0f, -30f), 600f * dt);
				MoveAndSlide();
			}

			// Plunge at where the player is heading.
			Vector2 aim = player.GlobalPosition + new Vector2(player.Velocity.X * DiveLead, 0f);
			Vector2 direction = (aim - GlobalPosition).Normalized();
			FaceTowards(GlobalPosition.X + direction.X);
			Sprite.Play("dive");
			bool hit = false;
			t = 0f;
			float passedAt = -1f;
			while (t < DiveMaxTime)
			{
				await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
				if (!Alive())
					return;
				float dt = (float)GetPhysicsProcessDeltaTime();
				t += dt;
				Velocity = direction * DiveSpeed;
				MoveAndSlide();
				if (!hit)
					hit = Talons();
				// Into the floor or a wall → pull up early.
				if (GetSlideCollisionCount() > 0)
					break;
				// Shoot past the player a little, then pull up.
				if (passedAt < 0f && (GlobalPosition - aim).Dot(direction) > 0f)
					passedAt = t;
				if (passedAt >= 0f && t - passedAt > 0.15f)
					break;
			}

			// Pull up, wings flared.
			Sprite.Play("dive_end");
			t = 0f;
			while (t < 8f / Fps)
			{
				await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
				if (!Alive())
					return;
				float dt = (float)GetPhysicsProcessDeltaTime();
				t += dt;
				Velocity = Velocity.MoveToward(new Vector2(direction.X * 120f, -160f), 900f * dt);
				MoveAndSlide();
				if (!hit)
					hit = Talons();
			}
		}
		finally
		{
			if (IsInstanceValid(this))
			{
				ClearFlash();
				if (HoldingAttackSlot)
				{
					EnemyCombatCoordinator.ReleaseAttackSlot();
					HoldingAttackSlot = false;
				}
				if (!IsQueuedForRemoval)
					_state = State.Circling;
			}
		}
	}

	private bool Alive() => IsInstanceValid(this) && !IsQueuedForRemoval;

	// Talons: a box around the head/feet, leading the way.
	private bool Talons()
	{
		Vector2 at = GlobalPosition + new Vector2(FacingRight ? 10f : -10f, 6f);
		var query = new PhysicsShapeQueryParameters2D
		{
			Shape = new RectangleShape2D { Size = TalonHitboxSize },
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
			stats.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(Stats.AttackPower * DiveDamageMultiplier)));
			if (stats.CurrentHealth < before)
			{
				player.ApplyKnockback(new Vector2(FacingRight ? 1f : -1f, -0.3f).Normalized(), DiveKnockback);
				BloodEffect.SpawnAt(this, player.GlobalPosition);
			}
			return true;
		}
		return false;
	}

	// ── Shot down ───────────────────────────────────────────────────────────────────────────

	protected override void OnDefeated()
	{
		ClearFlash();
		bool airborne = _state != State.Perched;
		base.OnDefeated();
		if (airborne)
			_ = FallDead();
		else
		{
			// Killed on its perch: straight to lying on the floor.
			Sprite.Play("death_landing");
			Sprite.Frame = Sprite.SpriteFrames.GetFrameCount("death_landing") - 1;
		}
	}

	private async Task FallDead()
	{
		await ToSignal(GetTree().CreateTimer(7f / Fps), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this))
			return;
		Sprite.Play("death_fall_loop");
		float vy = 0f;
		for (float t = 0f; t < 3f; t += (float)GetPhysicsProcessDeltaTime())
		{
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			if (!IsInstanceValid(this))
				return;
			float dt = (float)GetPhysicsProcessDeltaTime();
			vy = Mathf.Min(vy + Gravity * dt, 600f);
			Vector2 next = GlobalPosition + new Vector2(0f, vy * dt);
			var query = PhysicsRayQueryParameters2D.Create(GlobalPosition, next + new Vector2(0f, 14f), 1u);
			var floor = GetWorld2D().DirectSpaceState.IntersectRay(query);
			if (floor.Count > 0)
			{
				GlobalPosition = (Vector2)floor["position"] + new Vector2(0f, -14f);
				break;
			}
			GlobalPosition = next;
		}
		Visual.Position = new Vector2(0f, -PerchedVisualLift);
		Sprite.Play("death_landing");
	}

	// ── Shared ──────────────────────────────────────────────────────────────────────────────

	private void FaceTowards(float targetX)
	{
		FacingRight = targetX >= GlobalPosition.X;
		Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);
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

	// Flying: a hit just bumps it.
	public override void ApplyKnockback(Vector2 direction, float force)
	{
		if (_state == State.Circling)
			Velocity += direction * force * 0.5f;
	}

	protected override bool ContactDamageEnabled => false;
}
