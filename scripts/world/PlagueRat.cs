using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Rata de la plaga / Plague Rat (Monsters Creatures Fantasy 2, 70x70 cells, faces right). A big
// sewer rat that hunts in packs: quick, flinchy, and its bite carries disease.
// Sheet beats used:
//   attack_bite (12 frames): 2-6 rearing up on its hind legs (the wind-up), 7-9 lunging forward
//     jaws open, 10-11 settling.
//   hurt: flinch. run: scurrying.
// Moves:
//   Lunge bite — rears up (flashing) and springs forward a short way, biting; poisons on a hit.
//   Skitter back — after a bite it darts back out, then comes again.
//   Flinch — a hit interrupts its wind-up and knocks it back a little (small and light).
//   Pack alarm — when one gets hit, every other plague rat nearby hears the squeal and joins in
//     (wider detection for a while).
// Telegraphs: the body flashes white while it rears up.
public partial class PlagueRat : Enemy
{
	[ExportGroup("Bite")]
	[Export] public float BiteRange = 70f;
	[Export] public float AttackCooldown = 1.2f;
	[Export] public float RearTime = 0.35f;
	[Export] public float LungeSpeed = 260f;
	[Export] public float LungeTime = 0.18f;
	[Export] public float BiteDamageMultiplier = 1f;
	// Measured off attack_bite frames 7-9 (scale 1.5, sprite at 0.75,-3): the jaws reach ~38px in
	// front, at about the rat's own head height.
	[Export] public Vector2 BiteHitboxCenter = new(26f, 0f);
	[Export] public Vector2 BiteHitboxSize = new(30f, 24f);
	[Export] public int PoisonTickDamage = 1;
	[Export] public float PoisonTickInterval = 0.5f;
	[Export] public float PoisonDuration = 3f;

	[ExportGroup("Skitter & flinch")]
	[Export] public float SkitterSpeed = 190f;
	[Export] public float SkitterTime = 0.35f;
	[Export] public float FlinchTime = 0.25f;
	[Export] public float FlinchPush = 140f;

	[ExportGroup("Pack")]
	[Export] public float AlarmRadius = 320f;
	[Export] public float AlarmedDetectionRange = 600f;
	[Export] public float AlarmDuration = 6f;

	[ExportGroup("Look")]
	[Export] public Color FlashTint = new(2.2f, 2.2f, 2.2f, 1f);

	private static readonly RandomNumberGenerator Rng = new();

	private Tween _flashTween;
	private bool _busy;
	private bool _flinch;
	private float _moveOverride;
	private float _attackCooldownTimer;
	private float _alarmTimer;
	private float _baseDetection;

	static PlagueRat()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_baseDetection = DetectionRange;
		_attackCooldownTimer = Rng.RandfRange(0.2f, 0.9f);
		Stats.HitTaken += _ => OnHit();
	}

	private void OnHit()
	{
		_flinch = true;
		RaiseAlarm();
		if (!_busy)
			_ = Flinch();
	}

	// Squeal: every plague rat in earshot (this one included) hunts from much further away.
	private void RaiseAlarm()
	{
		foreach (Node node in GetTree().GetNodesInGroup("enemy"))
		{
			if (node is PlagueRat rat && rat.GlobalPosition.DistanceTo(GlobalPosition) <= AlarmRadius)
				rat.Alarm();
		}
	}

	public void Alarm()
	{
		_alarmTimer = AlarmDuration;
		DetectionRange = AlarmedDetectionRange;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;
		float dt = (float)delta;
		_attackCooldownTimer -= dt;
		if (_alarmTimer > 0f)
		{
			_alarmTimer -= dt;
			if (_alarmTimer <= 0f)
				DetectionRange = _baseDetection;
		}
		var player = PlayerReads.Find(this);
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval || player is null || !PlayerDetected || _busy || !IsOnFloor())
			return;
		float absDistance = Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X);
		if (absDistance <= BiteRange && _attackCooldownTimer <= 0f && player.GlobalPosition.Y > GlobalPosition.Y - 60f)
			_ = LungeBite();
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_busy)
			return _moveOverride == 0f || CanStepTo(Mathf.Sign(_moveOverride)) ? _moveOverride : 0f;
		float toward = Mathf.Sign(distanceX);
		float absDistance = Mathf.Abs(distanceX);
		// Circles at the edge of its lunge while the bite recharges.
		float wanted = _attackCooldownTimer > 0.15f ? BiteRange + 30f : BiteRange * 0.6f;
		if (absDistance > wanted && CanStepTo(toward))
			return toward * MoveSpeed;
		if (absDistance < wanted - 30f && CanStepTo(-toward))
			return -toward * MoveSpeed * 0.8f;
		return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
	}

	protected override bool CanTurnToFacePlayer => !_busy;

	// ── Bite ────────────────────────────────────────────────────────────────────────────────

	private async Task LungeBite()
	{
		if (!Begin())
			return;
		_flinch = false;
		try
		{
			FacePlayer();
			Sprite.Play("attack_bite");
			Sprite.Pause();
			FlashBody(RearTime);
			for (int f = 2; f <= 6; f++)
			{
				Sprite.Frame = f;
				if (!await Wait(RearTime / 5f))
					return;
				// Hit while rearing → the bite is cut short.
				if (_flinch)
				{
					Finish();
					_ = Flinch();
					return;
				}
			}
			float forward = FacingRight ? 1f : -1f;
			_moveOverride = forward * LungeSpeed;
			bool hit = false;
			for (int f = 7; f <= 9; f++)
			{
				Sprite.Frame = f;
				if (!hit)
					hit = Strike();
				if (!await Wait(LungeTime / 3f))
					return;
				if (!hit)
					hit = Strike();
			}
			_moveOverride = 0f;
			Sprite.Frame = 10;
			if (!await Wait(0.08f))
				return;
			// Skitter back out.
			Sprite.Play("run");
			_moveOverride = -forward * SkitterSpeed;
			if (!await Wait(SkitterTime))
				return;
			_moveOverride = 0f;
		}
		finally
		{
			if (IsInstanceValid(this))
				_moveOverride = 0f;
			Finish();
		}
		_attackCooldownTimer = AttackCooldown * Rng.RandfRange(0.8f, 1.3f);
	}

	private bool Strike()
	{
		Vector2 at = GlobalPosition + new Vector2(FacingRight ? BiteHitboxCenter.X : -BiteHitboxCenter.X, BiteHitboxCenter.Y);
		var query = new PhysicsShapeQueryParameters2D
		{
			Shape = new RectangleShape2D { Size = BiteHitboxSize },
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
			stats.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(Stats.AttackPower * BiteDamageMultiplier)), element: DamageElement.Poison);
			if (stats.CurrentHealth < before)
			{
				player.ApplyPoison(PoisonTickDamage, PoisonTickInterval, PoisonDuration);
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
		_flinch = false;
		try
		{
			Sprite.Play("hurt");
			float away = 1f;
			if (PlayerReads.Find(this) is { } player)
				away = Mathf.Sign(GlobalPosition.X - player.GlobalPosition.X);
			_moveOverride = away * FlinchPush;
			if (!await Wait(FlinchTime))
				return;
			_moveOverride = 0f;
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
			_attackCooldownTimer = 0.25f;
			return false;
		}
		HoldingAttackSlot = true;
		_busy = true;
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

	private void FacePlayer()
	{
		if (PlayerReads.Find(this) is { } player)
		{
			FacingRight = player.GlobalPosition.X >= GlobalPosition.X;
			Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);
		}
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
