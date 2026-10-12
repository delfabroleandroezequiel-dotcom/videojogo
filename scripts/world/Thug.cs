using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Matón / Thug (Bandits 2 pack, 110x80 cells, faces right). A bandit gunslinger: a flintlock in the
// lead hand, a knife in the other. He keeps you at pistol range and makes you pay for closing in.
// Sheet beats used (ThugSpriteFrames — note its clip names are off):
//   attack1 (7): 1-2 the pistol raised and aimed (the wind-up), 3 the shot (muzzle flash), 4-5 the
//     recoil kicking the gun up, 6 lowered. (The .tres listed 13 frames; the last 6 were past the
//     edge of the sheet and were trimmed.)
//   fall (6): actually the knife lunge — 2-4 a low, long thrust, 5 recovering.
//   hurt: flinch.
// Moves:
//   Pistol shot — aims (flashing, a red sight line), then fires a near-instant bullet (blockable).
//     Two shots, then he has to reload for ReloadTime — that's the window to rush him.
//   Knife lunge — get close and he lunges low with the knife, then backs off to shooting range.
//   Spacing — backs away to PreferredMin..PreferredMax and shoots from there.
// Telegraphs: body flashes white before each attack; a thin red aim line before the shot; bandit
// red trail on the lunge.
public partial class Thug : Enemy
{
	[ExportGroup("Spacing")]
	[Export] public float PreferredMin = 110f;
	[Export] public float PreferredMax = 220f;

	[ExportGroup("Pistol")]
	[Export] public float ShotRange = 360f;
	[Export] public float AimTime = 0.45f;
	[Export] public int ShotsPerReload = 2;
	[Export] public float ShotInterval = 0.9f;
	[Export] public float ReloadTime = 2.2f;
	[Export] public float ShotDamageMultiplier = 1.1f;
	// The muzzle, measured off attack1 frame 3 (scale 1.45, sprite at -2.9,-4.8).
	[Export] public Vector2 MuzzleOffset = new(49f, -13f);

	[ExportGroup("Knife lunge")]
	[Export] public float LungeTrigger = 70f;
	[Export] public float LungeCooldown = 1.8f;
	[Export] public float LungeTelegraph = 0.25f;
	[Export] public float LungeSpeed = 240f;
	[Export] public float LungeDamageMultiplier = 1.2f;
	// fall frames 2-4: the knife reaches ~52px in front at waist height.
	[Export] public Vector2 LungeHitboxCenter = new(32f, -4f);
	[Export] public Vector2 LungeHitboxSize = new(46f, 26f);

	[ExportGroup("Look")]
	[Export] public Color TrailGlowColor = new(1f, 0.08f, 0.12f, 1f);
	[Export] public Color FlashTint = new(2.2f, 2.2f, 2.2f, 1f);
	[Export] public float DecisionInterval = 0.3f;

	private static readonly RandomNumberGenerator Rng = new();
	private readonly DashWatcher _dash = new();

	private EnemySlashTrail _trail;
	private Tween _flashTween;
	private bool _busy;
	private bool _retreating;
	private float _moveOverride;
	private int _shotsLeft;
	private float _decisionTimer;
	private float _shotTimer;
	private float _reloadTimer;
	private float _lungeCooldownTimer;

	static Thug()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_shotsLeft = ShotsPerReload;
		_shotTimer = Rng.RandfRange(0.4f, 0.9f);
		_trail = new EnemySlashTrail
		{
			CoreWidth = 2f,
			GlowWidth = 7f,
			GlowColor = TrailGlowColor,
			CoreColor = new Color(1f, 0.7f, 0.7f, 1f),
			MinPointSpacing = 1.5f,
			SparkAmount = 8,
		};
		Visual.AddChild(_trail);
		Stats.HitTaken += _ => OnHit();
	}

	private void OnHit()
	{
		if (!_busy)
			_ = Flinch();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;
		float dt = (float)delta;
		_decisionTimer -= dt;
		_shotTimer -= dt;
		_lungeCooldownTimer -= dt;
		if (_reloadTimer > 0f)
		{
			_reloadTimer -= dt;
			if (_reloadTimer <= 0f)
				_shotsLeft = ShotsPerReload;
		}
		var player = PlayerReads.Find(this);
		_dash.Update(player, dt);
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval || player is null || !PlayerDetected || _busy || !IsOnFloor())
			return;

		float absDistance = Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X);
		if (absDistance <= LungeTrigger && _lungeCooldownTimer <= 0f)
		{
			_ = KnifeLunge();
			return;
		}
		if (_decisionTimer > 0f)
			return;
		_decisionTimer = DecisionInterval;
		if (_shotsLeft > 0 && _shotTimer <= 0f && absDistance <= ShotRange && absDistance > LungeTrigger * 0.8f
			&& _dash.GoodMomentToRelease(player, GlobalPosition.X, 0.1f))
			_ = Shoot();
	}

	// Keeps pistol range; while reloading he backs off further.
	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_busy)
			return _moveOverride == 0f || CanStepTo(Mathf.Sign(_moveOverride)) ? _moveOverride : 0f;
		float min = _reloadTimer > 0f ? PreferredMin + 40f : PreferredMin;
		return KitingMoveX(distanceX, currentVelocityX, min, PreferredMax, 30f, 1.1f, ref _retreating);
	}

	protected override bool DesiredFacingRight(Node2D player, float distanceX) => _retreating ? distanceX < 0 : distanceX >= 0;

	protected override bool CanTurnToFacePlayer => !_busy;

	// ── Pistol ──────────────────────────────────────────────────────────────────────────────

	private async Task Shoot()
	{
		if (!Begin())
			return;
		Line2D sight = null;
		try
		{
			Sprite.Play("attack1");
			Sprite.Pause();
			Sprite.Frame = 1;
			if (!await Wait(0.06f))
				return;
			Sprite.Frame = 2;
			FlashBody(AimTime);
			// A thin red sight line tracking the player while he aims.
			sight = new Line2D
			{
				Width = 1f,
				DefaultColor = new Color(1f, 0.15f, 0.15f, 0.6f),
				Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
				TopLevel = true,
			};
			AddChild(sight);
			float t = 0f;
			Vector2 aim = Vector2.Right;
			while (t < AimTime)
			{
				await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
				if (!IsInstanceValid(this) || IsQueuedForRemoval)
					return;
				t += (float)GetPhysicsProcessDeltaTime();
				aim = AimDirection();
				Vector2 muzzle = Muzzle();
				sight.Points = new[] { muzzle, muzzle + aim * ShotRange };
			}
			sight.QueueFree();
			sight = null;
			Sprite.Frame = 3;
			FireBullet(aim);
			if (!await Wait(0.07f))
				return;
			Sprite.Frame = 4;
			if (!await Wait(0.08f))
				return;
			Sprite.Frame = 5;
			if (!await Wait(0.08f))
				return;
			Sprite.Frame = 6;
			if (!await Wait(0.1f))
				return;
		}
		finally
		{
			if (IsInstanceValid(sight))
				sight.QueueFree();
			Finish();
		}
		_shotsLeft--;
		_shotTimer = ShotInterval;
		if (_shotsLeft <= 0)
			_reloadTimer = ReloadTime;
	}

	private Vector2 Muzzle() => GlobalPosition + new Vector2(FacingRight ? MuzzleOffset.X : -MuzzleOffset.X, MuzzleOffset.Y);

	// Straight at the player's chest, but never behind him.
	private Vector2 AimDirection()
	{
		if (PlayerReads.Find(this) is not { } player)
			return new Vector2(FacingRight ? 1f : -1f, 0f);
		Vector2 dir = (player.GlobalPosition + new Vector2(0f, -8f) - Muzzle()).Normalized();
		if (Mathf.Sign(dir.X) != (FacingRight ? 1f : -1f))
			dir = new Vector2(FacingRight ? 1f : -1f, 0f);
		return dir;
	}

	// Near-instant bullet: a ray from the muzzle (terrain stops it), a fading tracer, damage on the player.
	private void FireBullet(Vector2 direction)
	{
		Vector2 from = Muzzle();
		Vector2 to = from + direction * ShotRange;
		var query = PhysicsRayQueryParameters2D.Create(from, to, 1u | 2u);
		query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
		var hit = GetWorld2D().DirectSpaceState.IntersectRay(query);
		if (hit.Count > 0)
		{
			to = (Vector2)hit["position"];
			if (hit["collider"].AsGodotObject() is Metroidvania.Player.Player player
				&& player.GetNodeOrNull<Stats>("Stats") is { IsInvulnerable: false } stats)
			{
				int before = stats.CurrentHealth;
				stats.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(Stats.AttackPower * ShotDamageMultiplier)), isProjectile: true);
				if (stats.CurrentHealth < before)
				{
					player.ApplyKnockback(new Vector2(Mathf.Sign(direction.X), -0.2f).Normalized(), 200f);
					BloodEffect.SpawnAt(this, player.GlobalPosition);
				}
			}
			else
				ImpactEffect.SpawnAt(this, to);
		}
		if (GetTree().CurrentScene is Node scene)
		{
			var tracer = new Line2D
			{
				Points = new[] { from, to },
				Width = 2f,
				DefaultColor = new Color(1f, 0.9f, 0.55f, 1f),
				Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			};
			scene.AddChild(tracer);
			Tween fade = tracer.CreateTween();
			fade.TweenProperty(tracer, "modulate:a", 0f, 0.15f);
			fade.TweenCallback(Callable.From(tracer.QueueFree));
		}
		Sfx.PlayAt(this, "Combat/Bow", "Bow Attack");
	}

	// ── Knife lunge ─────────────────────────────────────────────────────────────────────────

	private async Task KnifeLunge()
	{
		if (!Begin())
			return;
		_lungeCooldownTimer = LungeCooldown;
		try
		{
			Sprite.Play("fall");
			Sprite.Pause();
			Sprite.Frame = 1;
			FlashBody(LungeTelegraph);
			if (!await Wait(LungeTelegraph))
				return;
			float forward = FacingRight ? 1f : -1f;
			_moveOverride = forward * LungeSpeed;
			bool hit = false;
			for (int f = 2; f <= 4; f++)
			{
				Sprite.Frame = f;
				if (f == 2)
					_trail.PlayThrust(new Vector2(4f, -4f), new Vector2(LungeHitboxCenter.X + LungeHitboxSize.X * 0.5f, -4f), 0.04f, 0.08f);
				if (!hit)
					hit = Strike();
				if (!await Wait(0.06f))
					return;
			}
			_moveOverride = 0f;
			Sprite.Frame = 5;
			if (!await Wait(0.12f))
				return;
			// Back off to shooting range.
			_moveOverride = -forward * MoveSpeed * 1.4f;
			Sprite.Play("run");
			if (!await Wait(0.35f))
				return;
		}
		finally
		{
			if (IsInstanceValid(this))
				_moveOverride = 0f;
			Finish();
		}
		_shotTimer = Mathf.Min(_shotTimer, 0.3f);
	}

	private bool Strike()
	{
		Vector2 at = GlobalPosition + new Vector2(FacingRight ? LungeHitboxCenter.X : -LungeHitboxCenter.X, LungeHitboxCenter.Y);
		var query = new PhysicsShapeQueryParameters2D
		{
			Shape = new RectangleShape2D { Size = LungeHitboxSize },
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
			stats.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(Stats.AttackPower * LungeDamageMultiplier)));
			if (stats.CurrentHealth < before)
			{
				player.ApplyKnockback(new Vector2(Mathf.Sign(player.GlobalPosition.X - GlobalPosition.X), -0.3f).Normalized(), 220f);
				BloodEffect.SpawnAt(this, player.GlobalPosition);
			}
			return true;
		}
		return false;
	}

	// ── Flinch ──────────────────────────────────────────────────────────────────────────────

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

	// ── Shared ──────────────────────────────────────────────────────────────────────────────

	private bool Begin()
	{
		if (!EnemyCombatCoordinator.TryAcquireAttackSlot())
		{
			_shotTimer = 0.3f;
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
		string anim = Mathf.Abs(velocity.X) > MoveSpeed * 1.05f ? "run" : Mathf.Abs(velocity.X) > 5f ? "walk" : "idle";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	protected override bool ContactDamageEnabled => false;
}
