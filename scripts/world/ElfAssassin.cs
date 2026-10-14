using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Elf assassin (Elves pack) — a close-quarters ambusher, the melee counterpart to the elf archer and
// the knife-throwing bandit. Same "read the player, score the options" approach. Its sheet:
//   attack1 (12fps): 0 blade low, 1 a big rising cut up the front, 2 follow-through, 3 blade up —
//   which is exactly attack2's frame 0, so the two chain: attack2 1 is a wide horizontal cut that
//   sweeps behind AND in front of it, 2-3 follow-through. throw: 0-1 arm back, dagger leaves on 2.
//   Faces right; has jump/fall.
// Moves:
//   Camouflage — fades almost invisible (a faint shimmer gives it away) and circles to the
//     player's BACK, then flashes into view and strikes. Getting hit breaks it.
//   Rising cut → spin — the 2-hit combo; cuts that land from behind go through the player's block.
//   Cross-up — turtle behind your block toward it and it vaults over you and spins on landing.
//   Pounce — from mid range it leaps at you and spins on the way down.
//   Poison dagger — a quick throw at a kiting / airborne player (poisons on hit).
//   Evade — starts a swing close in front of it and it backflips out, often into camouflage.
//   Punish — a whiffed swing or a dash ending nearby gets rushed.
// Telegraph: the body flashes white before every strike. Elf-green neon trails reach out to each
// cut's real reach.
public partial class ElfAssassin : Enemy
{
	[ExportGroup("Combat")]
	[Export] public float ComboRange = 62f;
	[Export] public float ComboCooldown = 1.6f;
	[Export] public float ChainChance = 0.65f;
	[Export] public float ComboTelegraph = 0.28f;
	[Export] public float RisingDamageMultiplier = 1f;
	[Export] public float SpinDamageMultiplier = 1.1f;
	[Export] public float CutKnockback = 220f;
	// Hit boxes measured off the attack rows (110x80 cells, scale 1.4, sprite at 4,-2.2):
	// the rising cut reaches ~43px in front from the feet to over the head; the spin covers
	// ~36px to either side at waist height.
	[Export] public Vector2 RisingHitboxCenter = new(26f, -10f);
	[Export] public Vector2 RisingHitboxSize = new(46f, 58f);
	[Export] public Vector2 SpinHitboxCenter = new(0f, 4f);
	[Export] public Vector2 SpinHitboxSize = new(88f, 36f);
	[Export] public Color TrailColor = new(0.35f, 1f, 0.45f, 1f);
	[Export] public Color FlashTint = new(2.2f, 2.2f, 2.2f, 1f);

	[ExportGroup("Camouflage")]
	[Export] public float CamoAlpha = 0.1f;
	[Export] public float CamoCooldown = 6f;
	[Export] public float CamoMaxTime = 4.5f;
	[Export] public float CamoMinDistance = 170f;
	[Export] public float StalkSpeedMultiplier = 1.35f;
	// How far behind the player it tries to end up before striking.
	[Export] public float BehindOffset = 40f;
	[Export] public float AmbushTelegraph = 0.18f;

	[ExportGroup("Pounce & vault")]
	[Export] public float PounceMinDistance = 120f;
	[Export] public float PounceMaxDistance = 230f;
	[Export] public float PounceCooldown = 4f;
	[Export] public float PounceChance = 0.35f;
	[Export] public float PounceVelocityY = -380f;
	[Export] public float PounceMaxSpeedX = 330f;
	[Export] public float VaultSpeedX = 260f;
	[Export] public float VaultVelocityY = -420f;
	[Export] public float MaxSafeHopDrop = 40f;

	[ExportGroup("Dagger")]
	[Export] public float ThrowMinDistance = 110f;
	[Export] public float ThrowMaxDistance = 300f;
	[Export] public float ThrowCooldown = 3.5f;
	[Export] public float ThrowChance = 0.45f;
	[Export] public float DaggerSpeed = 430f;
	[Export] public Vector2 DaggerOffset = new(30f, -12f);
	[Export] public float ThrowTelegraph = 0.2f;

	[ExportGroup("Evade")]
	[Export] public float EvadeRange = 80f;
	[Export] public float EvadeChance = 0.45f;
	[Export] public float EvadeCooldown = 2.5f;
	[Export] public Vector2 BackflipHop = new(240f, -300f);
	[Export] public float PunishRange = 160f;
	[Export] public float RushSpeed = 300f;
	[Export] public float DecisionInterval = 0.35f;
	[Export] public float HurtDuration = 0.25f;

	private const float AttackFps = 12f;

	private static readonly RandomNumberGenerator Rng = new();
	private readonly DashWatcher _dash = new();
	private readonly WhiffWatcher _whiff = new();

	private EnemySlashTrail _trail;
	private Tween _flashTween;
	private Tween _camoTween;
	private bool _busy;
	private bool _uncancellable;
	private bool _cancel;
	private bool _hopping;
	private bool _camouflaged;
	private float _camoTimer;
	private float _shimmerTimer;
	private bool _rushing;
	private float _rushTimer;
	private float _rushWindup = 1f;
	private float _hurtTimer;
	private float _decisionTimer;
	private float _comboCooldownTimer;
	private float _throwCooldownTimer;
	private float _camoCooldownTimer;
	private float _pounceCooldownTimer;
	private float _evadeCooldownTimer;
	private bool _camoAfterLanding;

	static ElfAssassin()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_camoCooldownTimer = Rng.RandfRange(0.5f, 2f);
		_pounceCooldownTimer = Rng.RandfRange(1f, 2.5f);
		_trail = new EnemySlashTrail
		{
			CoreWidth = 2f,
			GlowWidth = 7f,
			GlowColor = TrailColor,
			CoreColor = new Color(0.85f, 1f, 0.88f, 1f),
			MinPointSpacing = 1.5f,
			SparkAmount = 10,
		};
		Visual.AddChild(_trail);
		Stats.HitTaken += _ => OnHurt();
		Stats.HitTaken += _ => _whiff.NotifyHitTaken();
	}

	private void OnHurt()
	{
		_hurtTimer = HurtDuration;
		if (_camouflaged)
			SetCamouflage(false);
		if (_busy && !_uncancellable)
			_cancel = true;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;
		float dt = (float)delta;
		_hurtTimer -= dt;
		_decisionTimer -= dt;
		_comboCooldownTimer -= dt;
		_throwCooldownTimer -= dt;
		_camoCooldownTimer -= dt;
		_pounceCooldownTimer -= dt;
		_evadeCooldownTimer -= dt;
		_rushTimer -= dt;

		var player = PlayerReads.Find(this);
		_dash.Update(player, dt);
		_whiff.Update(player, dt);
		UpdateCamouflage(dt);

		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval)
			return;

		if (_hopping && IsOnFloor() && Velocity.Y >= 0f)
		{
			_hopping = false;
			if (_camoAfterLanding)
			{
				_camoAfterLanding = false;
				if (_camoCooldownTimer <= 0f)
					SetCamouflage(true);
			}
		}

		if (player is null || !PlayerDetected || _busy || _hopping || !IsOnFloor())
			return;
		Think(player, dt);
	}

	// ── Decisions ───────────────────────────────────────────────────────────────────────────

	private void Think(Metroidvania.Player.Player player, float dt)
	{
		float distanceX = player.GlobalPosition.X - GlobalPosition.X;
		float absDistance = Mathf.Abs(distanceX);
		float toward = Mathf.Sign(distanceX);
		bool playerFacesMe = player.IsFacingRight == (GlobalPosition.X > player.GlobalPosition.X);
		bool airborne = !player.IsOnFloor();

		// Stalking unseen: once at their back (or simply close), flash in and strike.
		if (_camouflaged)
		{
			if (absDistance <= ComboRange - 8f && _comboCooldownTimer <= 0f)
			{
				SetCamouflage(false);
				_ = Combo(player, AmbushTelegraph, chain: true);
			}
			return;
		}

		// Evade an incoming swing: backflip out, likely vanishing on landing.
		if (_whiff.PlayerStartedSwing && absDistance <= EvadeRange && playerFacesMe && _evadeCooldownTimer <= 0f
			&& Rng.Randf() < EvadeChance && TryHop(new Vector2(-toward * BackflipHop.X, BackflipHop.Y)))
		{
			_evadeCooldownTimer = EvadeCooldown;
			_camoAfterLanding = true;
			return;
		}

		// Punish a whiff or a dash ending nearby: rush in with a shorter wind-up.
		if ((_whiff.JustWhiffed || _dash.SinceDashEnded < 0.1f) && absDistance <= PunishRange)
		{
			_whiff.Consume();
			_rushing = true;
			_rushTimer = 0.6f;
			_rushWindup = 0.5f;
		}

		// A whiffed parry toward it → vault over during the recovery and spin from behind.
		if (PlayerReads.WhiffedParryToward(player, GlobalPosition.X) && absDistance <= 110f)
		{
			if (TryStartBusyHop(new Vector2(toward * VaultSpeedX, VaultVelocityY), out _))
			{
				_ = AerialSpin(player, spinOnLanding: true);
				return;
			}
		}

		if (_rushing)
		{
			if (absDistance <= ComboRange && _comboCooldownTimer <= 0f)
			{
				_rushing = false;
				_ = Combo(player, ComboTelegraph * _rushWindup, chain: Rng.Randf() < ChainChance);
			}
			else if (_rushTimer <= 0f)
			{
				_rushing = false;
			}
			return;
		}

		if (absDistance <= ComboRange && _comboCooldownTimer <= 0f)
		{
			_ = Combo(player, ComboTelegraph, chain: Rng.Randf() < ChainChance);
			return;
		}

		if (_decisionTimer > 0f)
			return;
		_decisionTimer = DecisionInterval * Rng.RandfRange(0.8f, 1.3f);

		bool kitingAway = player.Velocity.X * Mathf.Sign(distanceX) > 60f;
		if (absDistance >= ThrowMinDistance && absDistance <= ThrowMaxDistance && _throwCooldownTimer <= 0f
			&& Rng.Randf() < ThrowChance + (airborne ? 0.3f : 0f) + (kitingAway ? 0.3f : 0f))
		{
			_ = Throw(player);
			return;
		}

		if (absDistance >= PounceMinDistance && absDistance <= PounceMaxDistance && !airborne && _pounceCooldownTimer <= 0f
			&& Rng.Randf() < PounceChance)
		{
			float airTime = 2f * -PounceVelocityY / Mathf.Max(1f, Gravity);
			var launch = new Vector2(Mathf.Clamp(distanceX / airTime, -PounceMaxSpeedX, PounceMaxSpeedX), PounceVelocityY);
			if (TryStartBusyHop(launch, out _))
			{
				_pounceCooldownTimer = PounceCooldown;
				_ = AerialSpin(player, spinOnLanding: false);
				return;
			}
		}

		if (absDistance >= CamoMinDistance && _camoCooldownTimer <= 0f)
			SetCamouflage(true);
	}

	// ── Movement ────────────────────────────────────────────────────────────────────────────

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_hopping)
			return currentVelocityX;
		if (_busy || _hurtTimer > 0f)
			return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);

		float toward = Mathf.Sign(distanceX);
		if (_rushing)
			return CanStepTo(toward) ? toward * RushSpeed : 0f;

		if (_camouflaged && player is Metroidvania.Player.Player p)
		{
			// Circle to the player's back.
			float behindX = p.GlobalPosition.X + (p.IsFacingRight ? -BehindOffset : BehindOffset);
			float dx = behindX - GlobalPosition.X;
			if (Mathf.Abs(dx) < 10f)
				return 0f;
			float dir = Mathf.Sign(dx);
			return CanStepTo(dir) ? dir * MoveSpeed * StalkSpeedMultiplier : 0f;
		}

		if (Mathf.Abs(distanceX) > ComboRange * 0.8f && CanStepTo(toward))
			return toward * MoveSpeed;
		return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
	}

	protected override bool DesiredFacingRight(Node2D player, float distanceX) => distanceX >= 0;

	protected override bool CanTurnToFacePlayer => !_busy;

	private bool TryStartBusyHop(Vector2 launch, out bool started)
	{
		started = false;
		if (!IsOnFloor() || !LandsSafely(launch, MaxSafeHopDrop))
			return false;
		Velocity = launch;
		_hopping = true;
		started = true;
		return true;
	}

	private bool TryHop(Vector2 launch)
	{
		if (!TryStartBusyHop(launch, out _))
			return false;
		return true;
	}

	// ── Combo ───────────────────────────────────────────────────────────────────────────────

	private async Task Combo(Metroidvania.Player.Player player, float telegraph, bool chain)
	{
		if (!EnemyCombatCoordinator.TryAcquireAttackSlot())
		{
			_comboCooldownTimer = 0.3f;
			return;
		}
		HoldingAttackSlot = true;
		_busy = true;
		_cancel = false;
		FaceTowards(player.GlobalPosition.X);

		Sprite.Play("attack1");
		Sprite.Pause();
		Sprite.Frame = 0;
		FlashBody(telegraph);
		if (!await WaitChecked(telegraph))
			return;

		_uncancellable = true;
		try
		{
			// Rising cut (attack1 frame 1).
			Sprite.Frame = 1;
			_trail.Play(new Vector2(6f, -6f), 48f, 55f, -105f, 0.08f);
			Strike(RisingHitboxCenter, RisingHitboxSize, RisingDamageMultiplier);
			if (!await Wait(1f / AttackFps))
				return;
			Sprite.Frame = 2;
			if (!await Wait(1f / AttackFps))
				return;
			Sprite.Frame = 3;
			if (!await Wait(1f / AttackFps))
				return;

			if (chain)
			{
				// attack2 starts from the same raised pose → the wide spin (frame 1).
				player = PlayerReads.Find(this);
				if (player is not null)
					FaceTowards(player.GlobalPosition.X);
				Sprite.Play("attack2");
				Sprite.Pause();
				Sprite.Frame = 0;
				if (!await Wait(1f / AttackFps))
					return;
				Spin();
				if (!await Wait(1f / AttackFps))
					return;
				Sprite.Frame = 2;
				if (!await Wait(1f / AttackFps))
					return;
				Sprite.Frame = 3;
				if (!await Wait(1f / AttackFps))
					return;
			}
			if (!await Wait(0.2f))
				return;
		}
		finally
		{
			if (IsInstanceValid(this))
				_uncancellable = false;
		}
		_comboCooldownTimer = ComboCooldown;
		EndAction();
	}

	// The wide horizontal cut on attack2 frame 1: an oval sweep from high front, down through the
	// front and round behind, hitting both sides.
	private void Spin()
	{
		Sprite.Play("attack2");
		Sprite.Pause();
		Sprite.Frame = 1;
		_trail.Play(new Vector2(0f, 4f), 52f, -55f, 190f, 0.1f, 22f);
		Strike(SpinHitboxCenter, SpinHitboxSize, SpinDamageMultiplier);
	}

	// Leap (pounce or vault): jump/fall frames in the air; the pounce spins on the way down when
	// near the player, the vault spins on landing behind them.
	private async Task AerialSpin(Metroidvania.Player.Player player, bool spinOnLanding)
	{
		if (!EnemyCombatCoordinator.TryAcquireAttackSlot())
		{
			_busy = false;
			return;
		}
		HoldingAttackSlot = true;
		_busy = true;
		_uncancellable = true;
		if (_camouflaged)
			SetCamouflage(false);
		FlashBody(0.25f);
		bool spun = false;

		try
		{
			float elapsed = 0f;
			while (_hopping && elapsed < 2f)
			{
				await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
				if (!IsInstanceValid(this) || IsQueuedForRemoval)
					return;
				elapsed += (float)GetPhysicsProcessDeltaTime();

				if (!spun && !spinOnLanding && Velocity.Y > 0f && IsInstanceValid(player)
					&& GlobalPosition.DistanceTo(player.GlobalPosition) < 75f)
				{
					spun = true;
					Spin();
				}
				else if (!spun)
				{
					string anim = Velocity.Y < 0f ? "jump" : "fall";
					if (Sprite.Animation != anim)
						Sprite.Play(anim);
				}
			}

			if (!spun)
			{
				// Landed (the vault, or a pounce that missed the window): spin now.
				if (IsInstanceValid(player))
					FaceTowards(player.GlobalPosition.X);
				Spin();
			}
			if (!await Wait(3f / AttackFps + 0.15f))
				return;
		}
		finally
		{
			if (IsInstanceValid(this))
				_uncancellable = false;
		}
		_comboCooldownTimer = ComboCooldown * 0.6f;
		EndAction();
	}

	// One damage check over a box in front of it (mirrored by facing). Cuts that land from behind
	// the player ignore their block (Player's block is otherwise omnidirectional).
	private void Strike(Vector2 center, Vector2 size, float damageMultiplier)
	{
		Vector2 at = GlobalPosition + new Vector2(FacingRight ? center.X : -center.X, center.Y);
		var query = new PhysicsShapeQueryParameters2D
		{
			Shape = new RectangleShape2D { Size = size },
			Transform = new Transform2D(0f, at),
			CollisionMask = 2,
			CollideWithAreas = false,
			CollideWithBodies = true,
		};
		foreach (var hit in GetWorld2D().DirectSpaceState.IntersectShape(query, 8))
		{
			if (hit["collider"].AsGodotObject() is not Node2D body || !body.IsInGroup("player"))
				continue;
			if (body.GetNodeOrNull<Stats>("Stats") is not { } stats || stats.IsInvulnerable)
				continue;

			int damage = Mathf.Max(1, Mathf.RoundToInt(Stats.AttackPower * damageMultiplier));
			var player = body as Metroidvania.Player.Player;
			bool fromBehind = player is not null
				&& (player.IsFacingRight ? GlobalPosition.X < player.GlobalPosition.X : GlobalPosition.X > player.GlobalPosition.X);
			if (fromBehind && player.IsBlocking)
			{
				System.Func<bool> interceptor = stats.IncomingHitInterceptor;
				stats.IncomingHitInterceptor = null;
				stats.TakeDamage(damage);
				stats.IncomingHitInterceptor = interceptor;
			}
			else
			{
				stats.TakeDamage(damage);
			}
			if (body.HasMethod("ApplyKnockback"))
				body.Call("ApplyKnockback", new Vector2(Mathf.Sign(body.GlobalPosition.X - GlobalPosition.X), -0.2f).Normalized(), CutKnockback);
			BloodEffect.SpawnAt(this, body.GlobalPosition);
			break;
		}
	}

	// ── Dagger ──────────────────────────────────────────────────────────────────────────────

	private async Task Throw(Metroidvania.Player.Player player)
	{
		if (!EnemyCombatCoordinator.TryAcquireAttackSlot())
		{
			_throwCooldownTimer = 0.3f;
			return;
		}
		HoldingAttackSlot = true;
		_busy = true;
		_cancel = false;
		_throwCooldownTimer = ThrowCooldown;
		FaceTowards(player.GlobalPosition.X);

		Sprite.Play("throw");
		Sprite.Pause();
		Sprite.Frame = 1;
		FlashBody(ThrowTelegraph);
		if (!await WaitChecked(ThrowTelegraph))
			return;

		Sprite.Play("throw");
		Sprite.Frame = 2;
		player = PlayerReads.Find(this);
		if (player is not null)
		{
			Vector2 origin = GlobalPosition + new Vector2(FacingRight ? DaggerOffset.X : -DaggerOffset.X, DaggerOffset.Y);
			Vector2 aim = PlayerReads.PredictAimDirection(origin, player, DaggerSpeed, 0.8f, 0.3f, new Vector2(0f, -8f));
			float forward = FacingRight ? 1f : -1f;
			float angle = Mathf.Clamp(Mathf.Atan2(aim.Y, aim.X * forward), Mathf.DegToRad(-35f), Mathf.DegToRad(35f));
			var dagger = new ElfDagger
			{
				Direction = new Vector2(Mathf.Cos(angle) * forward, Mathf.Sin(angle)),
				Speed = DaggerSpeed,
				ThrowerStats = Stats,
				TrailColor = TrailColor,
			};
			GetTree().CurrentScene.AddChild(dagger);
			dagger.GlobalPosition = origin;
			Sfx.PlayAt(this, "Combat/Sword", "Sword Attack");
		}
		if (!await Wait(4f / AttackFps))
			return;
		EndAction();
	}

	// ── Camouflage ──────────────────────────────────────────────────────────────────────────

	private void SetCamouflage(bool on)
	{
		if (_camouflaged == on || Sprite is null)
			return;
		_camouflaged = on;
		_camoTimer = CamoMaxTime;
		_shimmerTimer = 0.4f;
		_camoTween?.Kill();
		_camoTween = CreateTween();
		float alpha = on ? CamoAlpha : 1f;
		float time = on ? 0.35f : 0.12f;
		_camoTween.TweenProperty(Sprite, "modulate:a", alpha, time);
		foreach (string bar in new[] { "HealthBar", "StaminaBar" })
		{
			if (GetNodeOrNull<CanvasItem>(bar) is { } item)
				_camoTween.Parallel().TweenProperty(item, "modulate:a", on ? 0f : 1f, time);
		}
		if (!on)
			_camoCooldownTimer = CamoCooldown;
	}

	// While hidden, a faint shimmer every so often keeps it trackable; it can't stay hidden forever.
	private void UpdateCamouflage(float dt)
	{
		if (!_camouflaged)
			return;
		_camoTimer -= dt;
		if (_camoTimer <= 0f)
		{
			SetCamouflage(false);
			return;
		}
		_shimmerTimer -= dt;
		if (_shimmerTimer > 0f || (_camoTween is not null && _camoTween.IsRunning()))
			return;
		_shimmerTimer = Rng.RandfRange(0.45f, 0.8f);
		Tween shimmer = CreateTween();
		shimmer.TweenProperty(Sprite, "modulate:a", CamoAlpha + 0.25f, 0.08f);
		shimmer.TweenProperty(Sprite, "modulate:a", CamoAlpha, 0.18f);
	}

	// ── Shared ──────────────────────────────────────────────────────────────────────────────

	private async Task<bool> Wait(float seconds)
	{
		await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
		return IsInstanceValid(this) && !IsQueuedForRemoval;
	}

	// Like Wait, but a hit during it cancels the action (wind-ups only).
	private async Task<bool> WaitChecked(float seconds)
	{
		float t = 0f;
		while (t < seconds)
		{
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return false;
			if (_cancel)
			{
				EndAction();
				return false;
			}
			t += (float)GetPhysicsProcessDeltaTime();
		}
		return true;
	}

	private void EndAction()
	{
		_busy = false;
		_cancel = false;
		ClearFlash();
		if (HoldingAttackSlot)
		{
			EnemyCombatCoordinator.ReleaseAttackSlot();
			HoldingAttackSlot = false;
		}
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
		_camoTween?.Kill();
		if (Sprite is not null)
			Sprite.Modulate = Colors.White;
		ClearFlash();
		base.OnDefeated();
	}

	// ── Animation ───────────────────────────────────────────────────────────────────────────

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null || _busy)
			return;
		string anim;
		if (!IsOnFloor())
			anim = velocity.Y < 0f ? "jump" : "fall";
		else if (_hurtTimer > 0f)
			anim = "hitnoeffect";
		else if (Mathf.Abs(velocity.X) > MoveSpeed * 1.1f)
			anim = "run";
		else if (Mathf.Abs(velocity.X) > 5f)
			anim = "walk";
		else
			anim = "idle";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	protected override bool ContactDamageEnabled => false;
}
