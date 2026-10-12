using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Fantasma / Ghost (Undead pack, 110x80 cells, faces right). A drifting spirit: it floats freely
// (no gravity), passes through walls and floors, and keeps slipping out of reach.
// Sheet beats used:
//   attack (8.2fps): 3 arm raised (the wind-up), 4-5 a spectral claw sweeping down the front.
//   spell: 3-7 stretched out flying forward — the swoop.
//   exile: 0-4 settling, 5-15 melting into a puddle on the floor and fading away (played backwards
//     it rises out of the puddle).
// Moves:
//   Hover — drifts to a spot above and in front of the player, bobbing, straight through terrain.
//   Fade — every few seconds it turns translucent for a moment: hits pass right through it. It
//     can't attack while faded, so wait for it to come back.
//   Claw — the spectral swipe; chills (Ice damage).
//   Swoop — from mid range it rears back (flashing) and flies straight through where you stood.
//   Exile — melts into a puddle and vanishes, then rises out of the floor behind you and claws.
// Telegraphs: the body flashes white before every attack; ghostly-teal afterimages on the swoop.
public partial class Ghost : Enemy
{
	[ExportGroup("Hover")]
	[Export] public float HoverDistance = 70f;
	[Export] public float HoverHeight = 30f;
	[Export] public float DriftAcceleration = 260f;
	[Export] public float BobAmplitude = 5f;
	[Export] public float BobSpeed = 2.4f;

	[ExportGroup("Fade")]
	[Export] public float FadeIntervalMin = 3.5f;
	[Export] public float FadeIntervalMax = 5.5f;
	[Export] public float FadeDuration = 1.4f;
	[Export] public float FadedAlpha = 0.22f;

	[ExportGroup("Claw")]
	[Export] public float ClawRange = 48f;
	[Export] public float AttackCooldown = 1.4f;
	[Export] public float ClawTelegraph = 0.35f;
	[Export] public float ClawDamageMultiplier = 1f;
	// Measured off attack frame 4 (scale 1.5, sprite at -2,0): the claw reaches ~37px in front and
	// covers the whole height of the body.
	[Export] public Vector2 ClawHitboxCenter = new(24f, 0f);
	[Export] public Vector2 ClawHitboxSize = new(36f, 58f);

	[ExportGroup("Swoop")]
	[Export] public float SwoopMinDistance = 70f;
	[Export] public float SwoopMaxDistance = 260f;
	[Export] public float SwoopChance = 0.55f;
	[Export] public float SwoopCooldown = 4f;
	[Export] public float SwoopTelegraph = 0.5f;
	[Export] public float SwoopSpeed = 430f;
	[Export] public float SwoopMaxTime = 0.6f;
	[Export] public float SwoopDamageMultiplier = 1.1f;

	[ExportGroup("Exile")]
	[Export] public float ExileCooldown = 7f;
	[Export] public float ExileChance = 0.35f;
	[Export] public float ExileHiddenTime = 0.6f;
	[Export] public float ExileBehindDistance = 46f;

	[ExportGroup("Look")]
	[Export] public Color GlowColor = new(0.35f, 1f, 0.9f, 1f);
	[Export] public Color FlashTint = new(2.2f, 2.2f, 2.2f, 1f);
	[Export] public float DecisionInterval = 0.35f;

	private static readonly RandomNumberGenerator Rng = new();

	private Tween _flashTween;
	private bool _busy;
	private bool _faded;
	private bool _hidden;
	private Vector2 _drift;
	private float _time;
	private float _decisionTimer;
	private float _attackCooldownTimer;
	private float _swoopCooldownTimer;
	private float _exileCooldownTimer;
	private float _fadeTimer;
	private float _afterimageTimer;

	static Ghost()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		// Drifts through terrain.
		CollisionMask = 0;
		_time = Rng.Randf() * Mathf.Tau;
		_attackCooldownTimer = Rng.RandfRange(0.6f, 1.2f);
		_swoopCooldownTimer = Rng.RandfRange(1f, 2f);
		_exileCooldownTimer = Rng.RandfRange(3f, 5f);
		_fadeTimer = Rng.RandfRange(FadeIntervalMin, FadeIntervalMax);
		// Faded or hidden in the floor, hits pass straight through.
		Stats.IncomingHitInterceptor = () => _faded || _hidden;
	}

	// ── Main loop (replaces Enemy's walk-and-gravity loop: this one flies) ──────────────────

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;
		float dt = (float)delta;
		_time += dt;
		_decisionTimer -= dt;
		_attackCooldownTimer -= dt;
		_swoopCooldownTimer -= dt;
		_exileCooldownTimer -= dt;

		var player = PlayerReads.Find(this);
		PlayerDetected = player is not null && GlobalPosition.DistanceTo(player.GlobalPosition) <= DetectionRange;

		if (!_busy)
		{
			Vector2 target = GlobalPosition;
			if (PlayerDetected)
			{
				float side = GlobalPosition.X >= player.GlobalPosition.X ? 1f : -1f;
				target = player.GlobalPosition + new Vector2(side * HoverDistance, -HoverHeight);
				FaceTowards(player.GlobalPosition.X);
			}
			Vector2 toTarget = target - GlobalPosition;
			Vector2 desired = toTarget.Length() > 6f ? toTarget.Normalized() * MoveSpeed : Vector2.Zero;
			_drift = _drift.MoveToward(desired, DriftAcceleration * dt);
			GlobalPosition += _drift * dt + new Vector2(0f, Mathf.Sin(_time * BobSpeed) * BobAmplitude * dt);
			UpdateFade(dt);
			UpdateIdleAnimation();
			if (PlayerDetected && !_faded)
				Think(player);
		}
	}

	private void UpdateFade(float dt)
	{
		_fadeTimer -= dt;
		if (_fadeTimer > 0f)
			return;
		_faded = !_faded;
		_fadeTimer = _faded ? FadeDuration : Rng.RandfRange(FadeIntervalMin, FadeIntervalMax);
		Tween tween = CreateTween();
		tween.TweenProperty(Sprite, "modulate:a", _faded ? FadedAlpha : 1f, 0.25f);
	}

	private void SetFaded(bool faded)
	{
		_faded = faded;
		if (Sprite is not null)
			Sprite.Modulate = new Color(1f, 1f, 1f, faded ? FadedAlpha : 1f);
	}

	// ── Decisions ───────────────────────────────────────────────────────────────────────────

	private void Think(Metroidvania.Player.Player player)
	{
		Vector2 offset = player.GlobalPosition - GlobalPosition;
		float absX = Mathf.Abs(offset.X);

		if (absX <= ClawRange && Mathf.Abs(offset.Y) < 50f && _attackCooldownTimer <= 0f)
		{
			_ = Claw(ClawTelegraph);
			return;
		}

		if (_decisionTimer > 0f || _attackCooldownTimer > AttackCooldown * 0.5f)
			return;
		_decisionTimer = DecisionInterval * Rng.RandfRange(0.8f, 1.3f);
		float distance = offset.Length();

		if (_exileCooldownTimer <= 0f && player.IsOnFloor() && Rng.Randf() < ExileChance)
		{
			_ = Exile(player);
			return;
		}
		if (distance >= SwoopMinDistance && distance <= SwoopMaxDistance && _swoopCooldownTimer <= 0f
			&& Rng.Randf() < SwoopChance)
			_ = Swoop(player);
		else if (absX > ClawRange)
		{
			// Drift in for the claw: pull the hover spot right next to the player for a moment.
			_drift = (player.GlobalPosition + new Vector2(Mathf.Sign(-offset.X) * 30f, -10f) - GlobalPosition).Normalized() * MoveSpeed * 1.4f;
		}
	}

	// ── Claw ────────────────────────────────────────────────────────────────────────────────

	private async Task Claw(float telegraph)
	{
		if (!Begin())
			return;
		try
		{
			Sprite.Play("attack");
			Sprite.Pause();
			Sprite.Frame = 3;
			FlashBody(telegraph);
			if (!await Wait(telegraph))
				return;
			// Lunges a little into the swipe.
			GlobalPosition += new Vector2((FacingRight ? 1f : -1f) * 10f, 0f);
			Sprite.Frame = 4;
			Strike(ClawHitboxCenter, ClawHitboxSize, ClawDamageMultiplier, 200f);
			for (int f = 5; f <= 8; f++)
			{
				if (!await Wait(1f / 8.2f))
					return;
				Sprite.Frame = f;
			}
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * Rng.RandfRange(0.85f, 1.2f);
	}

	// ── Swoop ───────────────────────────────────────────────────────────────────────────────

	private async Task Swoop(Metroidvania.Player.Player player)
	{
		if (!Begin())
			return;
		_swoopCooldownTimer = SwoopCooldown;
		try
		{
			Sprite.Play("spell");
			Sprite.Pause();
			Sprite.Frame = 2;
			// Rears back a little while flashing.
			Vector2 away = (GlobalPosition - player.GlobalPosition).Normalized();
			FlashBody(SwoopTelegraph);
			float t = 0f;
			while (t < SwoopTelegraph)
			{
				await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
				if (!IsInstanceValid(this) || IsQueuedForRemoval)
					return;
				float dt = (float)GetPhysicsProcessDeltaTime();
				t += dt;
				GlobalPosition += away * 40f * dt;
			}

			// Straight through where the player is now (aimed a touch low, at the body).
			Vector2 direction = (player.GlobalPosition + new Vector2(0f, -4f) - GlobalPosition).Normalized();
			FaceTowards(GlobalPosition.X + direction.X);
			Sprite.Frame = 5;
			bool hit = false;
			t = 0f;
			while (t < SwoopMaxTime)
			{
				await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
				if (!IsInstanceValid(this) || IsQueuedForRemoval)
					return;
				float dt = (float)GetPhysicsProcessDeltaTime();
				t += dt;
				GlobalPosition += direction * SwoopSpeed * dt;
				Afterimage(dt);
				if (!hit)
					hit = Strike(new Vector2(6f, 0f), new Vector2(34f, 40f), SwoopDamageMultiplier, 260f);
			}
			_drift = direction * MoveSpeed;
			Sprite.Frame = 8;
			if (!await Wait(0.25f))
				return;
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * 0.7f;
	}

	// ── Exile ───────────────────────────────────────────────────────────────────────────────

	private async Task Exile(Metroidvania.Player.Player player)
	{
		if (!Begin())
			return;
		_exileCooldownTimer = ExileCooldown;
		try
		{
			// Melts away (played double speed so it isn't a free 2 seconds).
			Sprite.SpeedScale = 2f;
			Sprite.Play("exile");
			_hidden = true;
			if (!await Wait(16f / 8f / 2f))
				return;
			Sprite.Visible = false;
			if (!await Wait(ExileHiddenTime))
				return;
			if (!IsInstanceValid(player))
				return;

			// Rises out of the floor right behind the player.
			float behind = player.IsFacingRight ? -1f : 1f;
			GlobalPosition = player.GlobalPosition + new Vector2(behind * ExileBehindDistance, -4f);
			FaceTowards(player.GlobalPosition.X);
			Sprite.Visible = true;
			Sprite.PlayBackwards("exile");
			if (!await Wait(16f / 8f / 2f * 0.6f))
				return;
			Sprite.SpeedScale = 1f;
			_hidden = false;
		}
		finally
		{
			if (IsInstanceValid(this))
			{
				Sprite.SpeedScale = 1f;
				Sprite.Visible = true;
				_hidden = false;
			}
			Finish();
		}
		// Straight into a quick claw.
		_attackCooldownTimer = 0f;
		_ = Claw(0.18f);
	}

	// ── Shared ──────────────────────────────────────────────────────────────────────────────

	private bool Begin()
	{
		if (!EnemyCombatCoordinator.TryAcquireAttackSlot())
		{
			_attackCooldownTimer = 0.3f;
			return false;
		}
		HoldingAttackSlot = true;
		_busy = true;
		// Attacks always come out solid.
		SetFaded(false);
		_fadeTimer = Mathf.Max(_fadeTimer, 1.2f);
		if (PlayerReads.Find(this) is { } player)
			FaceTowards(player.GlobalPosition.X);
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

	private bool Strike(Vector2 center, Vector2 size, float damageMultiplier, float knockback)
	{
		Vector2 at = GlobalPosition + new Vector2(FacingRight ? center.X : -center.X, center.Y);
		var query = new PhysicsShapeQueryParameters2D
		{
			Shape = new RectangleShape2D { Size = size },
			Transform = new Transform2D(0f, at),
			CollisionMask = 2,
			CollideWithBodies = true,
		};
		foreach (var hit in GetWorld2D().DirectSpaceState.IntersectShape(query, 8))
		{
			if (hit["collider"].AsGodotObject() is not Node2D body || !body.IsInGroup("player"))
				continue;
			if (body.GetNodeOrNull<Stats>("Stats") is not { } stats || stats.IsInvulnerable)
				continue;
			int before = stats.CurrentHealth;
			stats.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(Stats.AttackPower * damageMultiplier)), element: DamageElement.Ice);
			if (stats.CurrentHealth < before && body.HasMethod("ApplyKnockback"))
				body.Call("ApplyKnockback", new Vector2(Mathf.Sign(body.GlobalPosition.X - GlobalPosition.X), -0.3f).Normalized(), knockback);
			return true;
		}
		return false;
	}

	private void Afterimage(float dt)
	{
		_afterimageTimer -= dt;
		if (_afterimageTimer > 0f)
			return;
		_afterimageTimer = 0.035f;
		Texture2D frame = Sprite?.SpriteFrames?.GetFrameTexture(Sprite.Animation, Sprite.Frame);
		if (frame is null || GetTree().CurrentScene is not Node scene)
			return;
		var ghost = new Sprite2D
		{
			Texture = frame,
			Centered = Sprite.Centered,
			Offset = Sprite.Offset,
			FlipH = Sprite.FlipH,
			Material = Metroidvania.Player.NeonSilhouette.CreateMaterial(GlowColor, 1.4f),
			Modulate = new Color(1f, 1f, 1f, 0.5f),
			ZIndex = ZIndex - 1,
		};
		scene.AddChild(ghost);
		ghost.GlobalTransform = Sprite.GlobalTransform;
		Tween fade = ghost.CreateTween();
		fade.TweenProperty(ghost, "modulate:a", 0f, 0.3f);
		fade.TweenCallback(Callable.From(ghost.QueueFree));
	}

	private async Task<bool> Wait(float seconds)
	{
		await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
		return IsInstanceValid(this) && !IsQueuedForRemoval;
	}

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

	// Flies: a shove just nudges it.
	public override void ApplyKnockback(Vector2 direction, float force)
	{
		if (!_busy)
			_drift += direction * force * 0.5f;
	}

	protected override void OnDefeated()
	{
		ClearFlash();
		SetFaded(false);
		if (Sprite is not null)
		{
			Sprite.Visible = true;
			Sprite.SpeedScale = 1f;
		}
		base.OnDefeated();
	}

	private void UpdateIdleAnimation()
	{
		if (Sprite is null)
			return;
		string anim = _drift.Length() > MoveSpeed * 0.8f ? "move_fast" : _drift.Length() > 15f ? "move_slow" : "idle";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	protected override bool ContactDamageEnabled => false;
}
