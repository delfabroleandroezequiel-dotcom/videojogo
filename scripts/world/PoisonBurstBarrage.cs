using System.Collections.Generic;
using Godot;
using Metroidvania.Quests;
using Metroidvania.Save;

namespace Metroidvania.World;

// A relentless, room-wide version of SpiderBossArena's telegraphed poison burst, for a room that's
// off-limits until some boss is dead (e.g. HabitacionRecompensaSpider reachable by a shortcut
// while its spider is still alive). SpiderBossArena's own burst is one marker at a time under the
// player; this fires a whole wave at once -- one right under the player, one where the player's
// current velocity says they'll be when it lands, and a carpet of flanking ones on the ground to
// either side -- so simply running in a straight line just runs into the next circle. The only
// real counter is a dash's i-frames, which is the point.
//
// Deliberately its own component rather than a refactor of SpiderBossArena.RunExplosions: that
// loop is tuned around a single boss and is left untouched.
//
// Everything is driven from _PhysicsProcess (no SceneTreeTimer awaits, which keep running while
// the tree is paused) so opening a dialogue or menu freezes markers and hazards together.
public partial class PoisonBurstBarrage : Node2D
{
	// SaveManager boss persistence id (Enemy.PersistenceId / CustomPersistenceId). The barrage only
	// exists while that boss is alive; empty = no boss condition.
	[Export] public string ActiveWhileBossAliveId = "";
	// Optional second off-switch: never active once this quest is completed. Empty = ignore.
	[Export] public string DisabledWhenQuestCompletedId = "";

	[Export] public float StartDelay = 1f;
	[Export] public float WaveInterval = 0.6f;
	[Export] public float TelegraphDuration = 0.6f;
	[Export] public float ActiveDuration = 0.35f;
	[Export] public int Damage = 20;
	[Export] public float KnockbackForce = 220f;
	[Export] public float Radius = 70f;

	// Where the player's current horizontal velocity will carry them by detonation time. 0 disables
	// the lead burst.
	[Export] public float LeadFactor = 1f;
	// Extra bursts on the ground to each side of the player: FlankPairs * 2 total, FlankSpacing apart.
	[Export] public int FlankPairs = 2;
	[Export] public float FlankSpacing = 100f;
	[Export(PropertyHint.Layers2DPhysics)] public uint GroundMask = 1;
	[Export] public float GroundProbeDepth = 600f;
	// Ground-snapped bursts are centered on the player's body, not the floor line, so the circle
	// actually reaches someone standing there.
	[Export] public float GroundLift = 28f;

	[Export] public string MarkerFramesPath = "res://resources/sprites/Pj3MagicOrbLargeGreenSpriteFrames.tres";
	[Export] public string ExplosionFramesPath = "res://resources/sprites/SmokePoisonSpriteFrames.tres";
	[Export] public string ExplosionAnimation = "smoke_poison";
	[Export] public string LightTexturePath = "res://resources/lighting/PointLightGradient.tres";
	[Export] public Color MarkerLightColor = new(0.6f, 1f, 0.55f, 1f);
	[Export] public float MarkerLightEnergy = 0.9f;
	[Export] public float MarkerLightScale = 1.5f;
	[Export] public Color ExplosionLightColor = new(0.55f, 0.9f, 0.5f, 1f);
	[Export] public float ExplosionFlashEnergy = 1.8f;
	[Export] public float ExplosionFlashScale = 3f;
	[Export] public float ExplosionFlashDuration = 0.3f;

	private sealed class PendingBurst
	{
		public Vector2 Position;
		public float TimeLeft;
		public AnimatedSprite2D Marker;
	}

	private sealed class TimedNode
	{
		public Node Node;
		public float TimeLeft;
	}

	private readonly List<PendingBurst> _pending = new();
	private readonly List<TimedNode> _timed = new();
	private SpriteFrames _markerFrames;
	private Texture2D _lightTexture;
	private float _waveTimer;

	public override void _Ready()
	{
		bool bossDead = !string.IsNullOrEmpty(ActiveWhileBossAliveId)
			&& SaveManager.Instance.IsBossDefeated(ActiveWhileBossAliveId);
		bool questDone = !string.IsNullOrEmpty(DisabledWhenQuestCompletedId)
			&& QuestManager.Instance.IsCompleted(DisabledWhenQuestCompletedId);
		if (bossDead || questDone)
		{
			QueueFree();
			return;
		}

		_markerFrames = GD.Load<SpriteFrames>(MarkerFramesPath);
		_lightTexture = GD.Load<Texture2D>(LightTexturePath);
		_waveTimer = StartDelay;
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		TickBursts(dt);
		TickTimedNodes(dt);

		_waveTimer -= dt;
		if (_waveTimer > 0f)
			return;

		_waveTimer = WaveInterval;
		SpawnWave();
	}

	private void SpawnWave()
	{
		if (GetTree().GetFirstNodeInGroup("player") is not Node2D player)
			return;

		Vector2 origin = player.GlobalPosition;
		QueueBurst(origin);

		float velocityX = player is CharacterBody2D body ? body.Velocity.X : 0f;
		float leadOffset = velocityX * TelegraphDuration * LeadFactor;
		if (LeadFactor > 0f && Mathf.Abs(leadOffset) > Radius * 0.5f)
			QueueBurst(SnapToGround(origin + new Vector2(leadOffset, 0f)));

		for (int i = 1; i <= FlankPairs; i++)
		{
			QueueBurst(SnapToGround(origin + new Vector2(i * FlankSpacing, 0f)));
			QueueBurst(SnapToGround(origin - new Vector2(i * FlankSpacing, 0f)));
		}
	}

	private Vector2 SnapToGround(Vector2 point)
	{
		var query = PhysicsRayQueryParameters2D.Create(point, point + Vector2.Down * GroundProbeDepth, GroundMask);
		var hit = GetWorld2D().DirectSpaceState.IntersectRay(query);
		return hit.Count > 0
			? (Vector2)hit["position"] - new Vector2(0f, GroundLift)
			: point;
	}

	private void QueueBurst(Vector2 position)
	{
		var marker = new AnimatedSprite2D
		{
			SpriteFrames = _markerFrames,
			Animation = "marker",
		};
		AddChild(marker);
		marker.GlobalPosition = position;
		marker.Play("marker");

		// Child of the marker so it goes away with it when the burst detonates.
		marker.AddChild(new PointLight2D
		{
			Color = MarkerLightColor,
			Energy = MarkerLightEnergy,
			Texture = _lightTexture,
			TextureScale = MarkerLightScale,
		});

		_pending.Add(new PendingBurst { Position = position, TimeLeft = TelegraphDuration, Marker = marker });
	}

	private void TickBursts(float dt)
	{
		for (int i = _pending.Count - 1; i >= 0; i--)
		{
			PendingBurst burst = _pending[i];
			burst.TimeLeft -= dt;
			if (burst.TimeLeft > 0f)
				continue;

			_pending.RemoveAt(i);
			Detonate(burst);
		}
	}

	private void Detonate(PendingBurst burst)
	{
		burst.Marker.QueueFree();
		VfxSpawner.SpawnAt(this, burst.Position, ExplosionFramesPath, ExplosionAnimation);

		var flash = new PointLight2D
		{
			Color = ExplosionLightColor,
			Energy = ExplosionFlashEnergy,
			Texture = _lightTexture,
			TextureScale = ExplosionFlashScale,
		};
		AddChild(flash);
		flash.GlobalPosition = burst.Position;
		_timed.Add(new TimedNode { Node = flash, TimeLeft = ExplosionFlashDuration });

		Hazard hazard = Hazard.CreateArea(this, instantKill: false, damage: Damage, knockbackForce: KnockbackForce);
		hazard.GlobalPosition = burst.Position;
		hazard.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = Radius } });
		_timed.Add(new TimedNode { Node = hazard, TimeLeft = ActiveDuration });
	}

	private void TickTimedNodes(float dt)
	{
		for (int i = _timed.Count - 1; i >= 0; i--)
		{
			TimedNode timed = _timed[i];
			timed.TimeLeft -= dt;
			if (timed.TimeLeft > 0f)
				continue;

			_timed.RemoveAt(i);
			if (IsInstanceValid(timed.Node))
				timed.Node.QueueFree();
		}
	}
}
