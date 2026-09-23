using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Elf archer — the "smarter enemy" test bed. Instead of a fixed pattern it re-reads the player
// every decision and scores its options:
//  * Spacing (unless HoldPosition): keeps a preferred distance band, kites away when the player
//    closes in, walks up when they're too far, never backs off a ledge or into a wall. When the
//    player gets on top of it, it hops away — or vaults over them if it's cornered — but only if
//    the hop's simulated arc lands on ground (never into a pit or off a ledge).
//  * Aim: leads the player (predicts where they'll be when the arrow arrives), holds the drawn bow
//    while the player is dashing or blocking toward it and releases right as that ends — dodging
//    on reflex gets punished, reading the tell doesn't.
//  * Attack choice: normal shot / 3-arrow spread / 6-arrow fan, weighted by the situation (jumping
//    player -> spread, close or cornered -> fan) and by how many recent arrows missed.
//  * Getting hit mid-draw cancels the shot; getting hit at all makes it want to disengage.
// The two big volleys have a visible green glow telegraph so they stay fair.
public partial class ElfArcher : Enemy
{
	// Plant in place (only turn and shoot) instead of moving — per placement, same idea as
	// Hechicero/MagoCamino's HoldPosition.
	[Export] public bool HoldPosition = false;

	[ExportGroup("Spacing")]
	[Export] public float PreferredMinDistance = 170f;
	[Export] public float PreferredMaxDistance = 320f;
	// Once kiting, keeps backing off until this much past PreferredMinDistance (hysteresis), so it
	// opens a real gap instead of stuttering right at the threshold.
	[Export] public float RetreatOvershoot = 50f;
	[Export] public float RetreatSpeedMultiplier = 1.1f;
	[Export] public float ShootRange = 460f;

	[ExportGroup("Evasive hop")]
	[Export] public float HopTriggerDistance = 85f;
	[Export] public float HopCooldown = 2.2f;
	[Export] public float HopSpeedX = 230f;
	[Export] public float HopVelocityY = -320f;
	[Export] public float VaultSpeedX = 300f;
	[Export] public float VaultVelocityY = -420f;
	// A hop is only taken if its simulated arc lands on ground no more than this far below its
	// current feet — never hops off a ledge or into a pit. If neither the backward hop nor the vault
	// lands safely, it stands its ground (and the cornered volley becomes likely).
	[Export] public float MaxSafeHopDrop = 40f;

	[ExportGroup("Aim")]
	[Export] public PackedScene ArrowScene;
	[Export] public float ArrowSpeed = 420f;
	// Where the arrow leaves the bow, relative to the body when facing right (measured off
	// Elf_Archer_Attack4.png's nocked arrow, sprite scale 1.4).
	[Export] public Vector2 ArrowSpawnOffset = new(18f, -13f);
	// Aim point on the player relative to their origin (chest height).
	[Export] public Vector2 PlayerAimOffset = new(0f, -6f);
	// 1 = perfect horizontal lead, 0 = shoots where the player is right now.
	[Export] public float LeadAccuracy = 0.85f;
	// Vertical lead is damped — a jumping player's velocity flips under gravity, so fully leading
	// it overshoots.
	[Export] public float VerticalLeadFactor = 0.35f;
	[Export] public float MaxAimAngleDegrees = 65f;
	[Export] public float MinAimHold = 0.15f;
	[Export] public float MaxAimHold = 0.7f;
	// Grace after the player's dash ends before releasing (their dash recovery).
	[Export] public float PostDashRelease = 0.06f;

	[ExportGroup("Attacks")]
	// Attack sheet frames: 0-2 nock, 3-6 full draw, 7 release.
	[Export] public int DrawHoldFrame = 5;
	[Export] public int ReleaseFrame = 7;
	[Export] public float ShotCooldown = 1.1f;
	[Export] public float TripleCooldown = 3.5f;
	[Export] public float TripleSpreadDegrees = 14f;
	[Export] public float TripleDamageMultiplier = 0.8f;
	[Export] public float VolleyCooldown = 7f;
	[Export] public int VolleyArrowCount = 6;
	[Export] public float VolleyFanDegrees = 70f;
	[Export] public float VolleyDamageMultiplier = 0.6f;
	[Export] public float TripleTelegraph = 0.2f;
	[Export] public float VolleyTelegraph = 0.45f;
	[Export] public Color TelegraphColor = new(0.55f, 1.6f, 0.65f, 1f);
	[Export] public float HurtDuration = 0.3f;

	private enum Shot { Normal, Triple, Volley }

	private static readonly RandomNumberGenerator Rng = new();

	private bool _busy;
	private bool _cancelShot;
	private bool _retreating;
	private bool _hopping;
	private bool _wantsEvade;
	private float _hopCooldownTimer;
	private float _hurtTimer;
	private float _shotCooldownTimer;
	private float _tripleCooldownTimer;
	private float _volleyCooldownTimer;
	private float _sincePlayerDashEnded = 999f;
	private bool _playerWasDashing;
	// Rises as arrows expire without hitting, drops back to 0 on a hit — pushes it toward the
	// wider volleys when the player keeps dodging single shots.
	private float _missPressure;

	static ElfArcher()
	{
		Rng.Randomize();
	}

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;

		// Stagger the first shot a bit so several archers placed together don't volley in sync.
		_shotCooldownTimer = Rng.RandfRange(0.3f, 1f);
		Stats.HitTaken += _ => OnHurt();
	}

	private void OnHurt()
	{
		_hurtTimer = HurtDuration;
		if (_busy)
			_cancelShot = true;
		_wantsEvade = true;
	}

	private Metroidvania.Player.Player FindPlayer() =>
		GetTree().GetFirstNodeInGroup("player") as Metroidvania.Player.Player;

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;

		float dt = (float)delta;
		_hopCooldownTimer -= dt;
		_hurtTimer -= dt;
		_shotCooldownTimer -= dt;
		_tripleCooldownTimer -= dt;
		_volleyCooldownTimer -= dt;
		_sincePlayerDashEnded += dt;

		var player = FindPlayer();
		if (player is not null)
		{
			if (_playerWasDashing && !player.IsDashing)
				_sincePlayerDashEnded = 0f;
			_playerWasDashing = player.IsDashing;

			TryStartHop(player);
		}

		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval)
			return;

		if (_hopping && IsOnFloor() && Velocity.Y >= 0f)
			_hopping = false;

		if (player is not null)
			TryStartAttack(player);
	}

	// ── Movement ────────────────────────────────────────────────────────────────────────────

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_hopping)
			return currentVelocityX;

		if (HoldPosition || _busy || _hurtTimer > 0f)
		{
			_retreating = false;
			return Mathf.MoveToward(currentVelocityX, 0f, MoveSpeed);
		}

		return KitingMoveX(distanceX, currentVelocityX, PreferredMinDistance, PreferredMaxDistance, RetreatOvershoot,
			RetreatSpeedMultiplier, ref _retreating);
	}

	// Runs away facing where it's going (reads as fleeing, not moonwalking); otherwise faces the
	// player so it's always ready to draw.
	protected override bool DesiredFacingRight(Node2D player, float distanceX) =>
		_retreating ? distanceX < 0 : distanceX >= 0;

	protected override bool CanTurnToFacePlayer => !_busy;

	private void TryStartHop(Metroidvania.Player.Player player)
	{
		if (HoldPosition || _hopping || _busy || _hopCooldownTimer > 0f || !IsOnFloor())
			return;

		float distanceX = player.GlobalPosition.X - GlobalPosition.X;
		float absDistance = Mathf.Abs(distanceX);
		bool playerSwingingAtMe = player.IsAttacking && absDistance < PreferredMinDistance * 0.6f;
		bool evadeAfterHit = _wantsEvade && absDistance < PreferredMinDistance;
		_wantsEvade = false;

		if (absDistance > HopTriggerDistance && !playerSwingingAtMe && !evadeAfterHit)
			return;

		float away = -Mathf.Sign(distanceX);
		if (away == 0f)
			away = FacingRight ? -1f : 1f;

		// Backward hop if it lands on ground; otherwise vault over the player to the open side; if
		// neither lands safely (pit/ledge both ways), don't jump at all.
		Vector2 backHop = new(away * HopSpeedX, HopVelocityY);
		Vector2 vault = new(-away * VaultSpeedX, VaultVelocityY);
		if (LandsSafely(backHop, MaxSafeHopDrop))
			Velocity = backHop;
		else if (LandsSafely(vault, MaxSafeHopDrop))
			Velocity = vault;
		else
		{
			_hopCooldownTimer = 0.4f;
			return;
		}
		_hopping = true;
		_retreating = false;
		_hopCooldownTimer = HopCooldown;
	}

	// ── Attack selection ────────────────────────────────────────────────────────────────────

	private void TryStartAttack(Metroidvania.Player.Player player)
	{
		if (_busy || _hopping || _hurtTimer > 0f || _shotCooldownTimer > 0f || !IsOnFloor() || !PlayerDetected)
			return;

		float distanceX = player.GlobalPosition.X - GlobalPosition.X;
		float absDistance = Mathf.Abs(distanceX);
		if (absDistance > ShootRange)
			return;

		// While kiting, only stop to shoot once the gap is back to a comfortable size.
		if (_retreating && absDistance < PreferredMinDistance)
			return;

		if (!EnemyCombatCoordinator.TryAcquireAttackSlot())
		{
			_shotCooldownTimer = 0.25f;
			return;
		}
		HoldingAttackSlot = true;

		_ = RunAttack(ChooseShot(player, absDistance, distanceX));
	}

	private Shot ChooseShot(Metroidvania.Player.Player player, float absDistance, float distanceX)
	{
		bool airborne = !player.IsOnFloor();
		bool closingIn = player.Velocity.X * Mathf.Sign(-distanceX) > 60f;
		bool tooClose = absDistance < PreferredMinDistance;
		bool cornered = tooClose && !CanStepTo(-Mathf.Sign(distanceX));

		float normal = 1f;
		float triple = _tripleCooldownTimer > 0f ? 0f
			: 0.5f + (airborne ? 1.2f : 0f) + (absDistance > 220f ? 0.4f : 0f) + 0.35f * _missPressure;
		float volley = _volleyCooldownTimer > 0f ? 0f
			: 0.15f + (tooClose ? 1.1f : 0f) + (cornered || (HoldPosition && tooClose) ? 1.4f : 0f)
				+ (closingIn ? 0.4f : 0f) + 0.5f * Mathf.Max(0f, _missPressure - 1f);

		float roll = Rng.Randf() * (normal + triple + volley);
		if (roll < volley)
			return Shot.Volley;
		if (roll < volley + triple)
			return Shot.Triple;
		return Shot.Normal;
	}

	// ── Shooting ────────────────────────────────────────────────────────────────────────────

	private async Task RunAttack(Shot shot)
	{
		_busy = true;
		_cancelShot = false;
		var player = FindPlayer();
		if (player is not null)
			FaceTowards(player.GlobalPosition.X);

		float fps = (float)Sprite.SpriteFrames.GetAnimationSpeed("attack");
		Sprite.Play("attack");

		// Nock and draw, then hold the drawn bow while aiming.
		while (Sprite.Frame < DrawHoldFrame && Sprite.IsPlaying())
		{
			if (!await NextFrame())
				return;
		}
		Sprite.Pause();

		float telegraph = shot switch { Shot.Volley => VolleyTelegraph, Shot.Triple => TripleTelegraph, _ => 0f };
		if (telegraph > 0f)
			Glow(telegraph);

		float held = 0f;
		while (true)
		{
			if (!await NextFrame())
				return;
			held += (float)GetPhysicsProcessDeltaTime();
			player = FindPlayer();
			if (player is null)
			{
				EndAttack(0f);
				return;
			}

			FaceTowards(player.GlobalPosition.X);
			if (held < Mathf.Max(MinAimHold, telegraph))
				continue;
			if (held >= MaxAimHold || GoodMomentToRelease(player))
				break;
		}

		Sprite.Play("attack");
		float releaseDelay = Mathf.Max(0, ReleaseFrame - DrawHoldFrame) / fps;
		await ToSignal(GetTree().CreateTimer(releaseDelay), SceneTreeTimer.SignalName.Timeout);
		if (!StillValid())
			return;

		player = FindPlayer();
		if (player is not null)
			Fire(shot, player);

		int framesLeft = Sprite.SpriteFrames.GetFrameCount("attack") - ReleaseFrame;
		await ToSignal(GetTree().CreateTimer(Mathf.Max(0, framesLeft) / fps), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		if (shot == Shot.Triple)
			_tripleCooldownTimer = TripleCooldown;
		else if (shot == Shot.Volley)
			_volleyCooldownTimer = VolleyCooldown;
		EndAttack(ShotCooldown * Rng.RandfRange(0.85f, 1.2f));
	}

	// Awaits one physics frame; false (after cleaning up) if the shot got cancelled or it died.
	private async Task<bool> NextFrame()
	{
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		return StillValid();
	}

	private bool StillValid()
	{
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return false;
		if (!_cancelShot)
			return true;

		// Hit mid-draw: drop the shot, short cooldown so it doesn't just re-draw instantly.
		EndAttack(ShotCooldown * 0.6f);
		return false;
	}

	private void EndAttack(float cooldown)
	{
		_busy = false;
		_cancelShot = false;
		_shotCooldownTimer = cooldown;
		Visual.Modulate = Colors.White;
		if (HoldingAttackSlot)
		{
			EnemyCombatCoordinator.ReleaseAttackSlot();
			HoldingAttackSlot = false;
		}
	}

	// Holding while the player dashes (release right after it ends) or blocks facing us.
	private bool GoodMomentToRelease(Metroidvania.Player.Player player)
	{
		if (player.IsDashing)
			return false;
		if (_sincePlayerDashEnded < PostDashRelease)
			return false;

		bool playerFacesMe = player.IsFacingRight == (GlobalPosition.X > player.GlobalPosition.X);
		return !(player.IsBlocking && playerFacesMe);
	}

	private void FaceTowards(float targetX)
	{
		FacingRight = targetX >= GlobalPosition.X;
		Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);
	}

	private void Glow(float duration)
	{
		Tween glowTween = CreateTween();
		glowTween.TweenProperty(Visual, "modulate", TelegraphColor, duration * 0.5f);
		glowTween.TweenProperty(Visual, "modulate", Colors.White, duration * 0.5f);
	}

	private Vector2 ArrowOrigin() =>
		GlobalPosition + new Vector2(FacingRight ? ArrowSpawnOffset.X : -ArrowSpawnOffset.X, ArrowSpawnOffset.Y);

	// Predicts where the player will be when the arrow gets there (two refinement passes of
	// distance / arrow speed), leading horizontally by LeadAccuracy and vertically only a little.
	private Vector2 PredictAimDirection(Metroidvania.Player.Player player, Vector2 origin)
	{
		Vector2 target = player.GlobalPosition + PlayerAimOffset;
		Vector2 playerVelocity = player.IsDashing ? Vector2.Zero : player.Velocity;
		Vector2 predicted = target;
		for (int i = 0; i < 2; i++)
		{
			float travelTime = origin.DistanceTo(predicted) / ArrowSpeed;
			predicted = target + new Vector2(playerVelocity.X * travelTime * LeadAccuracy,
				playerVelocity.Y * travelTime * VerticalLeadFactor);
		}

		Vector2 direction = (predicted - origin).Normalized();
		float forward = FacingRight ? 1f : -1f;
		float angle = Mathf.Atan2(direction.Y, direction.X * forward);
		float limit = Mathf.DegToRad(MaxAimAngleDegrees);
		angle = Mathf.Clamp(angle, -limit, limit);
		return new Vector2(Mathf.Cos(angle) * forward, Mathf.Sin(angle));
	}

	private void Fire(Shot shot, Metroidvania.Player.Player player)
	{
		if (ArrowScene is null)
			return;

		Vector2 origin = ArrowOrigin();
		Vector2 aim = PredictAimDirection(player, origin);

		switch (shot)
		{
			case Shot.Normal:
				SpawnArrow(origin, aim, 1f, 1);
				break;
			case Shot.Triple:
				for (int i = -1; i <= 1; i++)
					SpawnArrow(origin, aim.Rotated(Mathf.DegToRad(TripleSpreadDegrees * i)), TripleDamageMultiplier, 3);
				break;
			case Shot.Volley:
				int count = Mathf.Max(2, VolleyArrowCount);
				for (int i = 0; i < count; i++)
				{
					float t = i / (float)(count - 1) - 0.5f;
					SpawnArrow(origin, aim.Rotated(Mathf.DegToRad(VolleyFanDegrees * t)), VolleyDamageMultiplier, count);
				}
				break;
		}

		Sfx.PlayAt(this, "Combat/Bow", "Bow Attack");
	}

	private void SpawnArrow(Vector2 origin, Vector2 direction, float damageMultiplier, int arrowsInShot)
	{
		var arrow = ArrowScene.Instantiate<ElfArrow>();
		arrow.Speed = ArrowSpeed;
		arrow.DamageMultiplier = damageMultiplier;
		arrow.Resolved += hit =>
		{
			if (!IsInstanceValid(this))
				return;
			_missPressure = hit ? 0f : _missPressure + 1f / arrowsInShot;
		};
		GetTree().CurrentScene.AddChild(arrow);
		arrow.GlobalPosition = origin;
		arrow.Launch(direction, Stats);
	}

	// ── Animation ───────────────────────────────────────────────────────────────────────────

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null || _busy)
			return;

		string anim;
		if (_hurtTimer > 0f)
			anim = "hit";
		else if (!IsOnFloor())
			anim = velocity.Y < 0f ? "jump" : "fall";
		else
			anim = Mathf.Abs(velocity.X) > 5f ? "run" : "idle";

		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	// Its offense is the bow; brushing against it shouldn't hurt.
	protected override bool ContactDamageEnabled => false;
}
