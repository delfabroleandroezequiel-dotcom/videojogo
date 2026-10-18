using Godot;
using Metroidvania.Player;

namespace Metroidvania.World;

// Ghost Warrior 1 — the Oscuros' reaper: a floating spectre with a long glaive that harasses from
// the air instead of brawling.
//  * Holds a perch beside and above the player (HoverOffset) rather than ramming into them.
//  * Estocada: holds the raised-glaive frame blinking cyan, then lunges at where the player is,
//    glaive levelled (frames 3-6), leaving cyan afterimages.
//  * Media luna: if the lunge ends close to the player it chains straight into the sheet's huge
//    crescent sweep (frames 7-8), traced with a cyan neon arc.
//  * Reads the player: rises out of a swing that starts close in front of it, and punishes a
//    whiffed parry with a quick lunge.
public partial class GhostWarrior1 : FlyingEnemy
{
	[ExportGroup("Hover")]
	// Where it likes to float relative to the player (X mirrored to whichever side it's on).
	[Export] public Vector2 HoverOffset = new(150f, -80f);
	[Export] public float BobAmplitude = 8f;
	[Export] public float BobSpeed = 2.4f;

	[ExportGroup("Estocada")]
	[Export] public float ThrustRange = 280f;
	[Export] public float ThrustWindup = 0.45f;
	[Export] public float QuickWindupScale = 0.4f;
	[Export] public float ThrustSpeed = 540f;
	[Export] public float ThrustTime = 0.42f;
	[Export] public float ThrustCooldown = 1.7f;
	[Export] public Vector2 ThrustHitboxCenter = new(78f, -6f);
	[Export] public Vector2 ThrustHitboxSize = new(110f, 26f);

	[ExportGroup("Media luna")]
	[Export] public float CrescentRange = 115f;
	[Export] public Vector2 CrescentHitboxCenter = new(30f, -10f);
	[Export] public Vector2 CrescentHitboxSize = new(170f, 160f);
	[Export] public Vector2 CrescentTrailCenter = new(10f, -10f);
	[Export] public float CrescentTrailRadius = 90f;

	[ExportGroup("Frames")]
	[Export] public int TelegraphFrame = 2;
	[Export] public int ThrustFrame = 3;
	[Export] public int CrescentFrame = 7;
	[Export] public float AttackFps = 14f;

	[ExportGroup("AI")]
	[Export] public float EvadeRange = 120f;
	[Export] public float EvadeChance = 0.6f;
	[Export] public float EvadeLift = 110f;
	[Export] public float EvadeCooldown = 2f;
	[Export] public float HurtDuration = 0.25f;

	[ExportGroup("Look")]
	[Export] public Color TelegraphTint = new(0.6f, 2.4f, 3f, 1f);
	[Export] public Color NeonColor = new(0.15f, 0.9f, 1f, 1f);
	[Export] public float AfterimageInterval = 0.035f;
	[Export] public float AfterimageLifetime = 0.25f;

	private enum Phase { Hover, Windup, Thrust, Crescent, Evade }

	private static readonly RandomNumberGenerator Rng = new();

	private Phase _phase = Phase.Hover;
	private float _phaseTimer;
	private float _time;
	private float _thrustCooldownTimer;
	private float _evadeCooldownTimer;
	private float _hurtTimer;
	private float _afterimageTimer;
	private Vector2 _thrustDirection;
	private float _side = 1f;
	private bool _quickWindup;
	private Hitbox _hitbox;
	private CollisionShape2D _hitboxShape;
	private EnemySlashTrail _trail;
	private Tween _telegraphTween;
	private readonly WhiffWatcher _whiff = new();

	static GhostWarrior1()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;

		_hitboxShape = new CollisionShape2D { Name = "CollisionShape2D", Disabled = true };
		_hitbox = new Hitbox { Name = "AttackHitbox", CollisionLayer = 0, CollisionMask = 2 };
		_hitbox.AddChild(_hitboxShape);
		AddChild(_hitbox);

		_trail = new EnemySlashTrail { GlowColor = NeonColor, CoreColor = NeonColor.Lightened(0.7f), CoreWidth = 5f, GlowWidth = 16f };
		Visual.AddChild(_trail);

		Stats.HitTaken += _ =>
		{
			_whiff.NotifyHitTaken();
			if (_phase == Phase.Hover)
				_hurtTimer = HurtDuration;
		};
		_thrustCooldownTimer = Rng.RandfRange(0.6f, 1.4f);
		_time = Rng.Randf() * Mathf.Tau;
	}

	protected override bool OverrideSteering(double delta)
	{
		float dt = (float)delta;
		_time += dt;
		_thrustCooldownTimer -= dt;
		_evadeCooldownTimer -= dt;
		_hurtTimer -= dt;
		var player = Player as Metroidvania.Player.Player;
		_whiff.Update(player, dt);

		switch (_phase)
		{
			case Phase.Windup:
				_phaseTimer -= dt;
				Glide(Vector2.Zero, dt);
				if (_phaseTimer <= 0f)
					StartThrust(player);
				break;
			case Phase.Thrust:
				RunThrust(player, dt);
				break;
			case Phase.Crescent:
				_phaseTimer -= dt;
				Glide(Vector2.Zero, dt);
				if (_phaseTimer <= 0f)
					EndAttack();
				break;
			case Phase.Evade:
				_phaseTimer -= dt;
				Glide(new Vector2(0f, -EvadeLift / 0.2f), dt);
				if (_phaseTimer <= 0f)
					_phase = Phase.Hover;
				break;
			default:
				Hover(player, dt);
				break;
		}
		return true;
	}

	// ── Hover / decisions ──

	private void Hover(Metroidvania.Player.Player player, float dt)
	{
		if (player is null || GlobalPosition.DistanceTo(player.GlobalPosition) > DetectionRange)
		{
			Hunting = false;
			Glide(Vector2.Zero, dt);
			PlayLoop("idle");
			return;
		}
		Hunting = true;
		PlayerDetected = true;
		float distanceX = player.GlobalPosition.X - GlobalPosition.X;
		if (Mathf.Abs(distanceX) > 30f)
			_side = -Mathf.Sign(distanceX);
		Face(distanceX >= 0f);

		Vector2 perch = player.GlobalPosition + new Vector2(_side * HoverOffset.X, HoverOffset.Y);
		Vector2 toPerch = perch - Anchor;
		Glide(toPerch.Length() > 6f ? toPerch.Normalized() * Mathf.Min(ChaseSpeed, toPerch.Length() * 4f) : Vector2.Zero, dt);
		PlayLoop(_hurtTimer > 0f ? "hurt" : FlightVelocity.Length() > 25f ? "fly" : "idle");

		float distance = GlobalPosition.DistanceTo(player.GlobalPosition);
		bool playerFacesMe = player.IsFacingRight == (GlobalPosition.X > player.GlobalPosition.X);
		if (_whiff.PlayerStartedSwing && distance <= EvadeRange && playerFacesMe && _evadeCooldownTimer <= 0f
			&& Rng.Randf() < EvadeChance)
		{
			_evadeCooldownTimer = EvadeCooldown;
			_phase = Phase.Evade;
			_phaseTimer = 0.2f;
			return;
		}

		bool punish = PlayerReads.WhiffedParryToward(player, GlobalPosition.X) || _whiff.JustWhiffed;
		if (distance <= ThrustRange && (_thrustCooldownTimer <= 0f || punish) && _hurtTimer <= 0f)
		{
			_whiff.Consume();
			StartWindup(punish);
		}
	}

	// Steers the flight anchor toward `desired` velocity and applies the bob as a pure offset.
	private void Glide(Vector2 desired, float dt)
	{
		FlightVelocity = FlightVelocity.MoveToward(desired, ChaseAcceleration * 3f * dt);
		Anchor += FlightVelocity * dt;
		float bob = _phase == Phase.Hover ? Mathf.Sin(_time * BobSpeed) * BobAmplitude : 0f;
		GlobalPosition = Anchor + new Vector2(0f, bob);
	}

	private void Face(bool right)
	{
		FacingRight = right;
		Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);
	}

	private void PlayLoop(string animation)
	{
		if (Sprite is not null && Sprite.Animation != animation && Sprite.SpriteFrames.HasAnimation(animation))
			Sprite.Play(animation);
	}

	// ── Estocada ──

	private void StartWindup(bool quick)
	{
		_phase = Phase.Windup;
		_quickWindup = quick;
		_phaseTimer = ThrustWindup * (quick ? QuickWindupScale : 1f);
		Sprite.Play("attack");
		Sprite.Stop();
		Sprite.Frame = TelegraphFrame;
		Telegraph(_phaseTimer);
	}

	private void StartThrust(Metroidvania.Player.Player player)
	{
		_telegraphTween?.Kill();
		Sprite.SelfModulate = Colors.White;
		Vector2 target = player is null ? GlobalPosition + new Vector2(FacingRight ? 1f : -1f, 0f) : player.GlobalPosition + new Vector2(0f, -10f);
		_thrustDirection = (target - GlobalPosition).Normalized();
		if (_thrustDirection == Vector2.Zero)
			_thrustDirection = new Vector2(FacingRight ? 1f : -1f, 0f);
		Face(_thrustDirection.X >= 0f);
		// The glaive is levelled horizontally on the sheet: keep the lunge mostly flat so it reads.
		_thrustDirection = new Vector2(_thrustDirection.X, Mathf.Clamp(_thrustDirection.Y, -0.45f, 0.45f)).Normalized();
		_phase = Phase.Thrust;
		_phaseTimer = ThrustTime;
		_afterimageTimer = 0f;
		_thrustCooldownTimer = ThrustCooldown * Rng.RandfRange(0.85f, 1.15f);
		Sprite.Play("attack");
		Sprite.Stop();
		Sprite.Frame = ThrustFrame;
		ActivateHitbox(ThrustHitboxCenter, ThrustHitboxSize);
	}

	private void RunThrust(Metroidvania.Player.Player player, float dt)
	{
		_phaseTimer -= dt;
		FlightVelocity = _thrustDirection * ThrustSpeed;
		Anchor += FlightVelocity * dt;
		GlobalPosition = Anchor;
		// Glaive-levelled frames 3-6 across the lunge.
		float t = 1f - Mathf.Clamp(_phaseTimer / ThrustTime, 0f, 1f);
		Sprite.Frame = ThrustFrame + Mathf.Min(3, (int)(t * 4f));

		_afterimageTimer -= dt;
		if (_afterimageTimer <= 0f)
		{
			_afterimageTimer = AfterimageInterval;
			SpawnAfterimage();
		}

		bool hitWall = GetWorld2D().DirectSpaceState.IntersectRay(
			PhysicsRayQueryParameters2D.Create(GlobalPosition, GlobalPosition + _thrustDirection * 24f, 1u)).Count > 0;
		if (_phaseTimer > 0f && !hitWall)
			return;

		_hitbox.Deactivate();
		FlightVelocity = Vector2.Zero;
		if (player is not null && GlobalPosition.DistanceTo(player.GlobalPosition) <= CrescentRange)
			StartCrescent(player);
		else
			EndAttack();
	}

	// ── Media luna ──

	private async void StartCrescent(Metroidvania.Player.Player player)
	{
		Face(player.GlobalPosition.X >= GlobalPosition.X);
		_phase = Phase.Crescent;
		float clip = (Sprite.SpriteFrames.GetFrameCount("attack") - CrescentFrame) / AttackFps;
		_phaseTimer = clip;
		Sprite.Play("attack");
		Sprite.Frame = CrescentFrame;
		_trail.Play(CrescentTrailCenter, CrescentTrailRadius, -150f, 70f, 0.18f);
		ActivateHitbox(CrescentHitboxCenter, CrescentHitboxSize);
		await ToSignal(GetTree().CreateTimer(2f / AttackFps), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			_hitbox.Deactivate();
	}

	private void EndAttack()
	{
		_hitbox.Deactivate();
		_phase = Phase.Hover;
		Sprite.Play("fly");
	}

	// ── Helpers ──

	// A fresh shape every time (the shape is per-instance anyway, but this keeps the attacks from
	// ever sharing one).
	private void ActivateHitbox(Vector2 center, Vector2 size)
	{
		_hitboxShape.Shape = new RectangleShape2D { Size = size };
		_hitbox.Position = new Vector2(FacingRight ? center.X : -center.X, center.Y);
		_hitbox.Activate(Stats);
	}

	private void Telegraph(float duration)
	{
		if (duration <= 0f)
			return;
		_telegraphTween?.Kill();
		_telegraphTween = CreateTween();
		float half = duration / 4f;
		for (int i = 0; i < 2; i++)
		{
			_telegraphTween.TweenProperty(Sprite, "self_modulate", TelegraphTint, half);
			_telegraphTween.TweenProperty(Sprite, "self_modulate", Colors.White, half);
		}
	}

	private void SpawnAfterimage()
	{
		Texture2D frame = Sprite?.SpriteFrames?.GetFrameTexture(Sprite.Animation, Sprite.Frame);
		if (frame is null || GetTree().CurrentScene is not Node scene)
			return;
		var ghost = new Sprite2D
		{
			Texture = frame,
			Centered = Sprite.Centered,
			Offset = Sprite.Offset,
			Material = NeonSilhouette.CreateMaterial(NeonColor, 1.8f),
			Modulate = new Color(1f, 1f, 1f, 0.75f),
			ZIndex = ZIndex - 1,
		};
		scene.AddChild(ghost);
		ghost.GlobalTransform = Sprite.GlobalTransform;
		Tween fade = ghost.CreateTween();
		fade.TweenProperty(ghost, "modulate:a", 0f, AfterimageLifetime);
		fade.TweenCallback(Callable.From(ghost.QueueFree));
	}
}
