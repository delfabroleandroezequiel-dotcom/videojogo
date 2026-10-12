using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Espadachín errante / Wandering Swordsman (Martial Hero 3, 126x126 cells, faces right). A lone
// duelist: fast, aggressive, and his three cuts flow into each other.
// Sheet beats used:
//   attack1 (7): 0-3 the sword drawn up and back (wind-up), 4 a great overhead crescent, 5-6 the
//     follow-through low — which is where attack2 starts.
//   attack2 (6): 0-2 crouched low, 3 a rising cut sweeping up over his head and behind him, 4-5 the
//     sword carried back high — which is where attack3 starts.
//   attack3 (9): 0-5 the sword drawn back level (the wind-up), 6 a horizontal slash lunging forward,
//     7-8 recovery.
//   jump / fall: the vault over a cornering player.
// Moves:
//   Flowing combo — 1 to 3 cuts (attack1 → attack2 → attack3), each one faster than the last; he
//     cuts it off at different points so it can't be memorised.
//   Iai dash — from mid range he dashes in (afterimages) into the horizontal slash.
//   Vault — cornered against a wall with you close, he leaps over you and cuts on landing.
//   Punish — a whiffed swing or a dash ending near him: instant iai dash.
// Telegraphs: body flashes white before the first cut and the dash; white neon trails.
public partial class WanderingSwordsman : Enemy
{
	[ExportGroup("Combo")]
	[Export] public float ComboRange = 70f;
	[Export] public float AttackCooldown = 1.3f;
	[Export] public float FirstTelegraph = 0.3f;
	[Export] public float ComboDamageMultiplier = 1f;
	[Export] public float FinisherDamageMultiplier = 1.3f;
	// Measured off the cut frames (scale 1.6, sprite at -7.2,0):
	//   attack1 frame 4: an overhead crescent reaching ~90px in front, from high over the head.
	//   attack2 frame 3: a rising cut sweeping ~90px in front and ~70px behind.
	//   attack3 frame 6: a horizontal slash ~90px in front at waist height.
	[Export] public Vector2 Cut1Center = new(45f, -25f);
	[Export] public Vector2 Cut1Size = new(76f, 96f);
	[Export] public Vector2 Cut2Center = new(10f, -22f);
	[Export] public Vector2 Cut2Size = new(150f, 86f);
	[Export] public Vector2 Cut3Center = new(50f, 4f);
	[Export] public Vector2 Cut3Size = new(84f, 40f);

	[ExportGroup("Iai dash")]
	[Export] public float DashMinDistance = 110f;
	[Export] public float DashMaxDistance = 230f;
	[Export] public float DashChance = 0.45f;
	[Export] public float DashCooldown = 3.5f;
	[Export] public float DashTelegraph = 0.3f;
	[Export] public float DashSpeed = 520f;

	[ExportGroup("Vault")]
	[Export] public Vector2 VaultVelocity = new(230f, -360f);
	[Export] public float VaultCooldown = 4f;

	[ExportGroup("Look")]
	[Export] public Color TrailGlowColor = new(0.75f, 0.85f, 1f, 1f);
	[Export] public Color AfterimageColor = new(0.7f, 0.85f, 1f, 1f);
	[Export] public Color FlashTint = new(2.2f, 2.2f, 2.2f, 1f);
	[Export] public float DecisionInterval = 0.3f;

	private static readonly RandomNumberGenerator Rng = new();
	private readonly DashWatcher _dash = new();
	private readonly WhiffWatcher _whiff = new();

	private EnemySlashTrail _trail;
	private Tween _flashTween;
	private bool _busy;
	private bool _hopping;
	private float _moveOverride;
	private float _decisionTimer;
	private float _attackCooldownTimer;
	private float _dashCooldownTimer;
	private float _vaultCooldownTimer;
	private float _afterimageTimer;

	static WanderingSwordsman()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_attackCooldownTimer = Rng.RandfRange(0.3f, 0.8f);
		_dashCooldownTimer = Rng.RandfRange(0.8f, 1.5f);
		_trail = new EnemySlashTrail
		{
			CoreWidth = 2.5f,
			GlowWidth = 9f,
			GlowColor = TrailGlowColor,
			CoreColor = new Color(1f, 1f, 1f, 1f),
			MinPointSpacing = 1.5f,
			SparkAmount = 10,
		};
		Visual.AddChild(_trail);
		Stats.HitTaken += _ => _whiff.NotifyHitTaken();
		// The sheet draws jump/fall 14px lower in the cell than everything else (feet at y95, not 81).
		Sprite.AnimationChanged += () =>
			Sprite.Offset = Sprite.Animation == "jump" || Sprite.Animation == "fall" ? new Vector2(0f, -14f) : Vector2.Zero;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;
		float dt = (float)delta;
		_decisionTimer -= dt;
		_attackCooldownTimer -= dt;
		_dashCooldownTimer -= dt;
		_vaultCooldownTimer -= dt;
		var player = PlayerReads.Find(this);
		_dash.Update(player, dt);
		_whiff.Update(player, dt);
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval)
			return;
		if (_hopping && IsOnFloor() && Velocity.Y >= 0f)
			_hopping = false;
		if (player is null || !PlayerDetected || _busy || _hopping || !IsOnFloor())
			return;

		float distanceX = player.GlobalPosition.X - GlobalPosition.X;
		float absDistance = Mathf.Abs(distanceX);
		float toward = Mathf.Sign(distanceX);

		if ((_whiff.JustWhiffed || _dash.SinceDashEnded < 0.1f) && absDistance <= DashMaxDistance && _attackCooldownTimer <= AttackCooldown * 0.5f)
		{
			_whiff.Consume();
			_ = IaiDash(toward, 0.08f);
			return;
		}
		// Back to the wall with the player close → vault over.
		if (absDistance <= ComboRange && _vaultCooldownTimer <= 0f && TestMove(GlobalTransform, new Vector2(-toward * 20f, 0f)))
		{
			_ = Vault(toward);
			return;
		}
		if (absDistance <= ComboRange && _attackCooldownTimer <= 0f)
		{
			_ = Combo(FirstTelegraph);
			return;
		}
		if (_decisionTimer > 0f || _attackCooldownTimer > AttackCooldown * 0.6f)
			return;
		_decisionTimer = DecisionInterval * Rng.RandfRange(0.8f, 1.3f);
		if (absDistance >= DashMinDistance && absDistance <= DashMaxDistance && _dashCooldownTimer <= 0f && Rng.Randf() < DashChance)
			_ = IaiDash(toward, DashTelegraph);
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_hopping)
			return currentVelocityX;
		if (_busy)
			return _moveOverride == 0f || CanStepTo(Mathf.Sign(_moveOverride)) ? _moveOverride : 0f;
		float toward = Mathf.Sign(distanceX);
		if (Mathf.Abs(distanceX) > ComboRange * 0.8f && CanStepTo(toward))
			return toward * MoveSpeed;
		return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
	}

	protected override bool CanTurnToFacePlayer => !_busy && !_hopping;

	// ── Combo ───────────────────────────────────────────────────────────────────────────────

	private async Task Combo(float telegraph)
	{
		if (!Begin())
			return;
		try
		{
			int length = Rng.Randf() switch { < 0.3f => 1, < 0.65f => 2, _ => 3 };
			// Cut 1: overhead crescent.
			Sprite.Play("attack1");
			Sprite.Pause();
			for (int f = 0; f <= 3; f++)
			{
				Sprite.Frame = f;
				if (f == 3)
					FlashBody(telegraph);
				if (!await Wait(f == 3 ? telegraph : 0.05f))
					return;
			}
			Sprite.Frame = 4;
			_trail.Play(new Vector2(0f, -20f), 60f, -150f, 40f, 0.08f);
			Strike(Cut1Center, Cut1Size, ComboDamageMultiplier, 160f);
			if (!await Frames(5, 6, 0.06f))
				return;
			if (length >= 2)
			{
				// Cut 2: rising, from low front up over and behind.
				FacePlayer();
				Sprite.Play("attack2");
				Sprite.Pause();
				if (!await Frames(0, 2, 0.045f))
					return;
				Sprite.Frame = 3;
				_trail.Play(new Vector2(0f, -10f), 62f, 60f, -200f, 0.08f);
				Strike(Cut2Center, Cut2Size, ComboDamageMultiplier, 180f);
				if (!await Frames(4, 5, 0.05f))
					return;
			}
			if (length >= 3)
			{
				// Cut 3: the horizontal finisher, lunging.
				FacePlayer();
				Sprite.Play("attack3");
				Sprite.Pause();
				if (!await Frames(0, 5, 0.035f))
					return;
				Sprite.Frame = 6;
				_moveOverride = (FacingRight ? 1f : -1f) * 260f;
				_trail.PlayThrust(new Vector2(4f, 2f), new Vector2(Cut3Center.X + Cut3Size.X * 0.5f, 2f), 0.05f, 0.08f);
				Strike(Cut3Center, Cut3Size, FinisherDamageMultiplier, 300f);
				if (!await Wait(0.08f))
					return;
				_moveOverride = 0f;
				if (!await Frames(7, 8, 0.07f))
					return;
			}
			if (!await Wait(0.2f))
				return;
		}
		finally
		{
			if (IsInstanceValid(this))
				_moveOverride = 0f;
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * Rng.RandfRange(0.85f, 1.2f);
	}

	private async Task<bool> Frames(int from, int to, float each)
	{
		for (int f = from; f <= to; f++)
		{
			Sprite.Frame = f;
			if (!await Wait(each))
				return false;
		}
		return true;
	}

	// ── Iai dash ────────────────────────────────────────────────────────────────────────────

	private async Task IaiDash(float direction, float telegraph)
	{
		if (!Begin())
			return;
		_dashCooldownTimer = DashCooldown;
		try
		{
			FaceTowards(GlobalPosition.X + direction);
			Sprite.Play("attack3");
			Sprite.Pause();
			Sprite.Frame = 3;
			FlashBody(telegraph);
			if (!await Wait(telegraph))
				return;
			// Dash until in cutting range (or blocked), leaving afterimages.
			_moveOverride = direction * DashSpeed;
			float t = 0f;
			while (t < 0.5f)
			{
				await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
				if (!IsInstanceValid(this) || IsQueuedForRemoval)
					return;
				float dt = (float)GetPhysicsProcessDeltaTime();
				t += dt;
				Afterimage(dt);
				if (PlayerReads.Find(this) is { } p && Mathf.Abs(p.GlobalPosition.X - GlobalPosition.X) <= Cut3Center.X)
					break;
				if (TestMove(GlobalTransform, new Vector2(direction * 8f, 0f)) || !HasGroundAhead(direction))
					break;
			}
			Sprite.Frame = 6;
			_trail.PlayThrust(new Vector2(-30f, 2f), new Vector2(Cut3Center.X + Cut3Size.X * 0.5f, 2f), 0.05f, 0.1f);
			Strike(Cut3Center, Cut3Size, FinisherDamageMultiplier, 300f);
			_moveOverride = direction * 120f;
			if (!await Wait(0.08f))
				return;
			_moveOverride = 0f;
			if (!await Frames(7, 8, 0.08f))
				return;
		}
		finally
		{
			if (IsInstanceValid(this))
				_moveOverride = 0f;
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * 0.8f;
	}

	private void Afterimage(float dt)
	{
		_afterimageTimer -= dt;
		if (_afterimageTimer > 0f)
			return;
		_afterimageTimer = 0.03f;
		Texture2D frame = Sprite?.SpriteFrames?.GetFrameTexture(Sprite.Animation, Sprite.Frame);
		if (frame is null || GetTree().CurrentScene is not Node scene)
			return;
		var ghost = new Sprite2D
		{
			Texture = frame,
			Centered = Sprite.Centered,
			Offset = Sprite.Offset,
			FlipH = Sprite.FlipH,
			Material = Metroidvania.Player.NeonSilhouette.CreateMaterial(AfterimageColor, 1.4f),
			Modulate = new Color(1f, 1f, 1f, 0.5f),
			ZIndex = ZIndex - 1,
		};
		scene.AddChild(ghost);
		ghost.GlobalTransform = Sprite.GlobalTransform;
		Tween fade = ghost.CreateTween();
		fade.TweenProperty(ghost, "modulate:a", 0f, 0.22f);
		fade.TweenCallback(Callable.From(ghost.QueueFree));
	}

	// ── Vault ───────────────────────────────────────────────────────────────────────────────

	private async Task Vault(float toward)
	{
		var launch = new Vector2(toward * VaultVelocity.X, VaultVelocity.Y);
		if (!LandsSafely(launch, 40f))
		{
			_vaultCooldownTimer = 1f;
			return;
		}
		if (!Begin())
			return;
		_vaultCooldownTimer = VaultCooldown;
		try
		{
			Velocity = launch;
			_hopping = true;
			Sprite.Play("jump");
			float t = 0f;
			while (_hopping && t < 1.4f)
			{
				await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
				if (!IsInstanceValid(this) || IsQueuedForRemoval)
					return;
				t += (float)GetPhysicsProcessDeltaTime();
				if (Velocity.Y > 0f && Sprite.Animation != "fall")
					Sprite.Play("fall");
			}
			// Lands behind them and turns into the rising cut.
			FacePlayer();
			Sprite.Play("attack2");
			Sprite.Pause();
			Sprite.Frame = 2;
			if (!await Wait(0.06f))
				return;
			Sprite.Frame = 3;
			_trail.Play(new Vector2(0f, -10f), 62f, 60f, -200f, 0.08f);
			Strike(Cut2Center, Cut2Size, ComboDamageMultiplier, 200f);
			if (!await Frames(4, 5, 0.07f))
				return;
		}
		finally
		{
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * 0.7f;
	}

	// ── Shared ──────────────────────────────────────────────────────────────────────────────

	private bool Begin()
	{
		if (!EnemyCombatCoordinator.TryAcquireAttackSlot())
		{
			_attackCooldownTimer = 0.25f;
			return false;
		}
		HoldingAttackSlot = true;
		_busy = true;
		FacePlayer();
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
			if (hit["collider"].AsGodotObject() is not Metroidvania.Player.Player player)
				continue;
			if (player.GetNodeOrNull<Stats>("Stats") is not { } stats || stats.IsInvulnerable)
				continue;
			int before = stats.CurrentHealth;
			stats.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(Stats.AttackPower * damageMultiplier)));
			if (stats.CurrentHealth < before)
			{
				player.ApplyKnockback(new Vector2(Mathf.Sign(player.GlobalPosition.X - GlobalPosition.X), -0.3f).Normalized(), knockback);
				BloodEffect.SpawnAt(this, player.GlobalPosition);
			}
			return true;
		}
		return false;
	}

	private async Task<bool> Wait(float seconds)
	{
		await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
		return IsInstanceValid(this) && !IsQueuedForRemoval;
	}

	private void FacePlayer()
	{
		if (PlayerReads.Find(this) is { } player)
			FaceTowards(player.GlobalPosition.X);
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

	protected override void OnDefeated()
	{
		ClearFlash();
		base.OnDefeated();
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null || _busy || _hopping)
			return;
		string anim = !IsOnFloor() ? (velocity.Y < 0f ? "jump" : "fall") : Mathf.Abs(velocity.X) > 5f ? "run" : "idle";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	protected override bool ContactDamageEnabled => false;
}
