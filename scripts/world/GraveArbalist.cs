using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Esqueleto ballestero / Grave Arbalist (KBPixelArt Skeleton Enemy Pack, 128x128 cells, faces
// right). A grave skeleton with a heavy crossbow: one devastating bolt, then a long reload.
// Sheet beats used:
//   shot_start (4) raising the crossbow, shot (4) the bolt leaving (frame 0), shot_end (4) lowering,
//   reload (10) cranking the string back. digout (19) (GraveRise). idle / walk / hurt / death.
//   Bolt: the pack's own arrow sheets (GraveBolt).
// Moves:
//   Aimed bolt — raises the crossbow and tracks you with a thin green sight line, LOCKS the aim
//     a beat before firing (that's your cue to move), then a fast bolt that stops on walls.
//   Point-blank — rushed shot with a short aim when you're right on top of it, big knockback.
//   Reload — after every shot it stands still cranking for ReloadTime. Hitting it during the reload
//     makes it start over: rush it.
//   Spacing — backs off to MinDistance..MaxDistance (never off a ledge) while loaded.
//   Buried start — like the other grave skeletons, but wakes from further away.
public partial class GraveArbalist : Enemy
{
	[ExportGroup("Grave")]
	[Export] public bool StartBuried = true;
	[Export] public float WakeDistance = 230f;
	[Export] public float WakeHeight = 100f;
	[Export] public float RiseTime = 1.2f;

	[ExportGroup("Spacing")]
	[Export] public float MinDistance = 130f;
	[Export] public float MaxDistance = 260f;

	[ExportGroup("Bolt")]
	[Export] public float ShotRange = 380f;
	[Export] public float AimTime = 0.6f;
	[Export] public float AimLockTime = 0.15f;
	[Export] public float MaxAimAngle = 35f;
	[Export] public float BoltSpeed = 470f;
	[Export] public float BoltDamageMultiplier = 1.4f;
	[Export] public float ShotCooldown = 0.5f;
	// The crossbow's mouth on shot_start frame 3 (scale 1.6, sprite at 7,-21.6).
	[Export] public Vector2 BoltOrigin = new(34f, -7f);

	[ExportGroup("Point-blank")]
	[Export] public float PointBlankTrigger = 60f;
	[Export] public float PointBlankAim = 0.25f;
	[Export] public float PointBlankKnockback = 340f;

	[ExportGroup("Reload")]
	[Export] public float ReloadTime = 1.5f;

	[ExportGroup("Look")]
	[Export] public Color FlashTint = new(1.4f, 2.6f, 1.5f, 1f);
	[Export] public Color SightColor = new(0.35f, 1f, 0.45f, 0.55f);

	private static readonly RandomNumberGenerator Rng = new();
	private readonly GraveRise _grave = new();

	private Tween _flashTween;
	private bool _busy;
	private bool _loaded = true;
	private bool _reloading;
	private bool _reloadInterrupted;
	private bool _retreating;
	private float _shotCooldownTimer;

	static GraveArbalist()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_shotCooldownTimer = Rng.RandfRange(0.3f, 0.8f);
		Stats.IncomingHitInterceptor = () => _grave.Busy;
		Stats.HitTaken += _ => OnHit();
		if (StartBuried)
			_grave.Bury(Sprite);
	}

	private void OnHit()
	{
		if (_reloading)
		{
			_reloadInterrupted = true;
			return;
		}
		if (!_busy)
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
		_shotCooldownTimer -= (float)delta;
		var player = PlayerReads.Find(this);
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval || player is null || !PlayerDetected || _busy || !IsOnFloor())
			return;

		if (!_loaded)
		{
			_ = Reload();
			return;
		}
		float distance = GlobalPosition.DistanceTo(player.GlobalPosition);
		if (_shotCooldownTimer > 0f || distance > ShotRange)
			return;
		_ = Shoot(Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X) <= PointBlankTrigger);
	}

	// Keeps its distance while loaded; stands its ground (cranking) otherwise.
	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_busy || !_loaded)
			return 0f;
		return KitingMoveX(distanceX, currentVelocityX, MinDistance, MaxDistance, 30f, 1.1f, ref _retreating);
	}

	protected override bool DesiredFacingRight(Node2D player, float distanceX) => _retreating ? distanceX < 0 : distanceX >= 0;

	protected override bool CanTurnToFacePlayer => !_busy;

	// ── Shot ─────────────────────────────────────────────────────────────────────────────────────

	private async Task Shoot(bool pointBlank)
	{
		if (!Begin())
			return;
		Line2D sight = null;
		try
		{
			float aimTime = pointBlank ? PointBlankAim : AimTime;
			Sprite.Play("shot_start");
			Sprite.Pause();
			for (int f = 0; f <= 3; f++)
			{
				Sprite.Frame = f;
				if (!await Wait(0.05f))
					return;
			}
			FlashBody(aimTime);
			sight = new Line2D
			{
				Width = 1f,
				DefaultColor = SightColor,
				Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
				TopLevel = true,
			};
			AddChild(sight);
			Vector2 aim = AimDirection();
			float t = 0f;
			while (t < aimTime)
			{
				await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
				if (!IsInstanceValid(this) || IsQueuedForRemoval)
					return;
				t += (float)GetPhysicsProcessDeltaTime();
				// Tracks, then locks for the last AimLockTime (the sight line brightens).
				if (t < aimTime - AimLockTime)
					aim = AimDirection();
				else
					sight.DefaultColor = new Color(SightColor, 1f);
				sight.Points = new[] { Mouth(), Mouth() + aim * ShotRange };
			}
			sight.QueueFree();
			sight = null;

			Sprite.Play("shot");
			Sprite.Pause();
			Sprite.Frame = 0;
			FireBolt(aim, pointBlank);
			for (int f = 1; f <= 3; f++)
			{
				if (!await Wait(0.06f))
					return;
				Sprite.Frame = f;
			}
			Sprite.Play("shot_end");
			if (!await Wait(0.3f))
				return;
		}
		finally
		{
			if (IsInstanceValid(sight))
				sight.QueueFree();
			Finish();
		}
		_shotCooldownTimer = ShotCooldown;
	}

	private Vector2 Mouth() => GlobalPosition + new Vector2(FacingRight ? BoltOrigin.X : -BoltOrigin.X, BoltOrigin.Y);

	// At the player's chest, within MaxAimAngle of straight ahead.
	private Vector2 AimDirection()
	{
		float forward = FacingRight ? 1f : -1f;
		if (PlayerReads.Find(this) is not { } player)
			return new Vector2(forward, 0f);
		Vector2 dir = player.GlobalPosition + new Vector2(0f, -8f) - Mouth();
		float angle = Mathf.Atan2(dir.Y, Mathf.Abs(dir.X));
		angle = Mathf.Clamp(angle, -Mathf.DegToRad(MaxAimAngle), Mathf.DegToRad(MaxAimAngle));
		return new Vector2(forward * Mathf.Cos(angle), Mathf.Sin(angle));
	}

	private void FireBolt(Vector2 direction, bool pointBlank)
	{
		_loaded = false;
		if (GetTree().CurrentScene is not Node scene)
			return;
		var bolt = new GraveBolt
		{
			Direction = direction,
			Speed = BoltSpeed,
			DamageMultiplier = BoltDamageMultiplier,
			KnockbackForce = pointBlank ? PointBlankKnockback : 180f,
			ShooterStats = Stats,
		};
		scene.AddChild(bolt);
		bolt.GlobalPosition = Mouth();
		Sfx.PlayAt(this, "Combat/Bow", "Bow Attack");
	}

	// ── Reload ───────────────────────────────────────────────────────────────────────────────────

	private async Task Reload()
	{
		_busy = true;
		_reloading = true;
		try
		{
			int frames = Sprite.SpriteFrames.GetFrameCount("reload");
			Sprite.Play("reload");
			Sprite.Pause();
			int f = 0;
			while (f < frames)
			{
				if (_reloadInterrupted)
				{
					// Knocked off the crank: back to the start.
					_reloadInterrupted = false;
					f = 0;
					Sprite.Play("hurt");
					if (!await Wait(0.25f))
						return;
					Sprite.Play("reload");
					Sprite.Pause();
				}
				Sprite.Frame = f;
				if (!await Wait(ReloadTime / frames))
					return;
				f++;
			}
			_loaded = true;
		}
		finally
		{
			if (IsInstanceValid(this))
			{
				_reloading = false;
				_reloadInterrupted = false;
				_busy = false;
			}
		}
	}

	// ── Shared ───────────────────────────────────────────────────────────────────────────────────

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
			_shotCooldownTimer = 0.3f;
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
		if (Sprite is null || _busy || _grave.Busy)
			return;
		string anim = Mathf.Abs(velocity.X) > 5f ? "walk" : "idle";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	protected override bool ContactDamageEnabled => false;
}
