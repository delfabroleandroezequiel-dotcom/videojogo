using Godot;
using System.Threading.Tasks;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Incendiario/TorcherBandit: settle-then-telegraph-then-swing torch attack that leaves fire on
// the ground (see GroundFire), played as a zoner — see the AI block below.
public partial class Bandido4 : MeleeEnemy
{
	[Export] public float SettleDelay = 0.3f;
	[Export] public float SettleSpeedThreshold = 10f;
	[Export] public float WindupDuration = 0.5f;

	// Attack.png plays at 14fps (see TorcherBanditSpriteFrames.tres) — frame 7 of the resumed
	// swing (after the telegraph hold below) is where the torch actually connects.
	[Export] public float HitFrameDelay = 7f / 14f;

	// Fire trail following the torch's swing, drawn by EnemySlashTrail. Measured off
	// TorcherBandit/Attack.png (flame-pixel centroid per frame, sprite scale 1.6192 + offset):
	// frames 0-6 hold the torch behind at about (-49,-18), frame 7 snaps it over the head down to
	// the front at chest height (42,-14) — the frame even has its own baked swoosh along that
	// path. Pivoting around the shoulder (-6,-32), that's ~162° -> ~20° going over the top, so the
	// arc runs -198° -> 20° (-90° = straight up). The swing is a single-frame snap in the art, so
	// the trail starts one frame before it (frame 6; play resumes from TelegraphFrame 1) and
	// sweeps fast.
	[Export] public Vector2 FireTrailCenter = new(-6f, -32f);
	[Export] public float FireTrailRadius = 50f;
	[Export] public float FireTrailStartAngle = -198f;
	[Export] public float FireTrailEndAngle = 20f;
	[Export] public float FireTrailStartDelay = 5f / 14f;
	[Export] public float FireTrailDuration = 0.12f;
	[Export] public float FireTrailCoreWidth = 4f;
	[Export] public float FireTrailGlowWidth = 12f;
	[Export] public Color FireTrailCoreColor = new(1f, 0.9f, 0.5f, 1f);
	// Glow color along the streak, tail -> tip: cooling deep red at the tail, orange at the tip.
	[Export] public Gradient FireTrailGlowRamp;
	[Export] public ParticleProcessMaterial FireTrailEmberProcess;

	// Small burning patch left on the ground under where the swing ends (see GroundFire). Found
	// by a short raycast straight down from the trail's end point; if there's no ground within
	// GroundFireMaxDrop (swinging off a ledge), no fire is spawned.
	[Export] public PackedScene GroundFireScene;
	[Export] public float GroundFireMaxDrop = 120f;

	// ── Zoner AI ──
	// After a swing leaves fire on the ground it backs off so the fire sits between it and the
	// player, making them cross the flames to reach it. A player standing in fire (or ending a
	// dash in reach) is the opening it steps in to punish. Never backs off a ledge.
	[ExportGroup("AI")]
	[Export] public float RetreatAfterFireTime = 1.4f;
	[Export] public float RetreatDistance = 150f;
	[Export] public float PunishWindupScale = 0.6f;

	private EnemySlashTrail _fireTrail;
	private float _retreatTimer;
	private float _settleTimer;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;

		_fireTrail = new EnemySlashTrail
		{
			CoreWidth = FireTrailCoreWidth,
			GlowWidth = FireTrailGlowWidth,
			GlowColor = Colors.White,
			CoreColor = FireTrailCoreColor,
			GlowRamp = FireTrailGlowRamp ?? DefaultFireRamp(),
			SparkProcess = FireTrailEmberProcess,
			SparkColor = new Color(1f, 0.75f, 0.35f, 1f),
			SparkAmount = 24,
			SparkLifetime = 0.9,
		};
		Visual.AddChild(_fireTrail);
	}

	private static Gradient DefaultFireRamp()
	{
		var ramp = new Gradient();
		ramp.Offsets = new[] { 0f, 0.5f, 1f };
		ramp.Colors = new[]
		{
			new Color(0.45f, 0.03f, 0f, 0f),
			new Color(1f, 0.25f, 0.02f, 0.75f),
			new Color(1f, 0.6f, 0.1f, 1f),
		};
		return ramp;
	}

	private async void RunFireSwing()
	{
		await ToSignal(GetTree().CreateTimer(FireTrailStartDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		_fireTrail.Play(FireTrailCenter, FireTrailRadius, FireTrailStartAngle, FireTrailEndAngle, FireTrailDuration);

		// The fire only lands once the torch's own hit window (HitFrameDelay + AttackDuration from
		// the swing's start) has closed — spawned any earlier, its first tick would reach the player
		// before the torch and the torch hit would then be the one that misses.
		float untilHitWindowCloses = Mathf.Max(FireTrailDuration, HitFrameDelay + AttackDuration - FireTrailStartDelay);
		await ToSignal(GetTree().CreateTimer(untilHitWindowCloses), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		SpawnGroundFire();
	}

	private void SpawnGroundFire()
	{
		if (GroundFireScene is null)
			return;

		Vector2 localTip = FireTrailCenter + Vector2.Right.Rotated(Mathf.DegToRad(FireTrailEndAngle)) * FireTrailRadius;
		Vector2 from = Visual.ToGlobal(localTip);
		var query = PhysicsRayQueryParameters2D.Create(from, from + Vector2.Down * GroundFireMaxDrop,
			1u | PhysicsLayers.ClimbableWalls | PhysicsLayers.OneWayPlatforms, new Godot.Collections.Array<Rid> { GetRid() });
		var hit = GetWorld2D().DirectSpaceState.IntersectRay(query);
		if (hit.Count == 0)
			return;

		// Deferred: adding a collision-carrying Area2D mid physics step can hit "Can't change this
		// state while flushing queries" (same reason PoisonSpit defers its PoisonPuddle).
		Vector2 groundPoint = (Vector2)hit["position"];
		_retreatTimer = RetreatAfterFireTime;
		Node currentScene = GetTree().CurrentScene;
		Callable.From(() =>
		{
			var fire = GroundFireScene.Instantiate<Node2D>();
			currentScene.AddChild(fire);
			fire.GlobalPosition = groundPoint;
		}).CallDeferred();
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);

		if (IsQueuedForRemoval)
			return;
		Think((float)delta);

		if (Mathf.Abs(Velocity.X) > SettleSpeedThreshold)
			_settleTimer = 0f;
		else
			_settleTimer += (float)delta;
	}

	protected override bool ReadyToCommitAttack() => _settleTimer >= SettleDelay && _retreatTimer <= 0f;

	private void Think(float dt)
	{
		_retreatTimer -= dt;
		var player = PlayerRef;
		if (player is null || Attacking || !PlayerDetected)
			return;

		float absDistance = Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X);
		bool playerInFire = PlayerStandingInFire(player);
		if (playerInFire)
			_retreatTimer = 0f;

		if ((playerInFire || Dash.SinceDashEnded < 0.1f) && absDistance <= AttackRange * 1.1f)
			TryAttackNow(PunishWindupScale);
	}

	private bool PlayerStandingInFire(Node2D player)
	{
		foreach (Node node in GetTree().GetNodesInGroup(GroundFire.GroupName))
		{
			if (node is GroundFire fire && fire.IsBurning && fire.OverlapsBody(player))
				return true;
		}
		return false;
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (Hopping)
			return currentVelocityX;
		// Backing off behind its fire: never approach, keep RetreatDistance.
		if (_retreatTimer > 0f)
			return SpacingMoveX(distanceX, currentVelocityX, float.MaxValue, RetreatDistance);
		return SpacingMoveX(distanceX, currentVelocityX, StopDistance, 0f);
	}

	protected override async Task Attack()
	{
		Attacking = true;
		CanAttack = false;

		HoldTelegraphFrame("attack");
		float windup = WindupDuration * WindupScale;
		WindupScale = 1f;
		await ToSignal(GetTree().CreateTimer(windup), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		Sprite.Play("attack");
		RunFireSwing();
		await ToSignal(GetTree().CreateTimer(HitFrameDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		await base.Attack();
	}
}
