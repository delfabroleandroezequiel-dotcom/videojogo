using System.Collections.Generic;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// A harder, independent boss for a dedicated arena — reuses SpiderBoss's scene/web-spit plumbing
// as a starting point, but is its own fight: no melee, no lunge (SpiderBossArena.tscn sets
// AttackRange/LungeChance to 0 on its own now, not inherited — SpiderBoss.cs itself stays the
// original CuevaBosqueLobo1 encounter and must not be touched for this). What makes THIS fight
// distinct: a permanent spread of falling spiderling reinforcements that gets topped back up the
// instant one dies, and a telegraphed poison burst under the player that's active from phase 1
// onward. Both, plus the inherited web spit, scale up (shorter cooldowns, more reinforcements) as
// HP drops through 3 phases, so the fight visibly ramps up instead of staying flat.
public partial class SpiderBossArena : SpiderBoss
{
	[Export] public PackedScene FallingSpiderScene;
	// Index 0/1/2 = phase 1/2/3's target reinforcement count kept alive at once — not a one-time
	// spawn, every death gets replaced, so this is a floor, not a cap.
	[Export] public int[] PhaseSpiderTargetCount = { 1, 2, 2 };
	[Export] public float FallingSpiderSpawnHeight = 500f;
	[Export] public float FallingSpiderSpawnRadiusX = 50f;
	[Export] public float FallingSpiderInitialDelay = 1f;
	// Deliberately short — the fall time from FallingSpiderSpawnHeight already buys the player a
	// beat before the replacement becomes a threat, so this just covers the "another one's coming"
	// telegraph, not a full breather. Combat should stay relentless: killing one never earns silence.
	[Export] public float FallingSpiderRespawnDelay = 0.4f;
	// Safety net, not a tuning knob for normal play: if a falling spider misses the arena's floor
	// (e.g. spawned past the edge of a narrow room) it never lands, never dies, and would otherwise
	// sit in _activeFallingSpiders forever — silently blocking the "always respawn on kill" promise
	// since the tracked count never drops. Comfortably longer than any real fall from
	// FallingSpiderSpawnHeight should ever take.
	[Export] public float FallingSpiderLostTimeout = 6f;

	// Index 0/1/2 = phase 1/2/3's cooldown multiplier for the inherited web spit — spitting faster
	// as the fight escalates. SpiderBoss.cs exposes WebSpitCooldownMultiplier specifically so this
	// can hook in without touching the base CuevaBosqueLobo1 encounter's timing at all.
	[Export] public float[] PhaseWebSpitCooldownMultiplier = { 1f, 0.75f, 0.55f };

	[Export] public float[] PhaseExplosionInterval = { 3.5f, 2.2f, 1.3f };
	[Export] public float ExplosionTelegraphDuration = 0.85f;
	[Export] public float ExplosionActiveDuration = 0.35f;
	[Export] public int ExplosionDamage = 35;
	[Export] public float ExplosionRadius = 70f;
	[Export] public string MarkerFramesPath = "res://resources/sprites/Pj3MagicOrbLargeGreenSpriteFrames.tres";
	[Export] public string ExplosionFramesPath = "res://resources/sprites/SmokePoisonSpriteFrames.tres";
	[Export] public string ExplosionAnimation = "smoke_poison";

	// The arena runs dark (Visual.modulate tints this spider near-black), so its own bolts/bursts
	// need to actually throw light — same PointLightGradient texture as everything else's Glow.
	[Export] public string LightTexturePath = "res://resources/lighting/PointLightGradient.tres";
	[Export] public Color MarkerLightColor = new(0.6f, 1f, 0.55f, 1f);
	[Export] public float MarkerLightEnergy = 0.9f;
	[Export] public float MarkerLightScale = 1.5f;
	[Export] public Color ExplosionLightColor = new(0.55f, 0.9f, 0.5f, 1f);
	[Export] public float ExplosionFlashEnergy = 1.8f;
	[Export] public float ExplosionFlashScale = 3f;
	[Export] public float ExplosionFlashDuration = 0.3f;

	private int _currentPhase = 1;
	private bool _explosionLoopStarted;
	private readonly List<Node2D> _activeFallingSpiders = new();
	// GD only exposes RandRange (float); RandiRange lives on RandomNumberGenerator itself, and
	// Boss.cs's own _rng is private, so this needs its own instance rather than reusing that one.
	private readonly RandomNumberGenerator _rng = new();

	// This arena's only offense is the web spit / poison burst / falling spiderlings, so unlike
	// RatEnemy (whose only offense IS contact), a player brushing against the spider's body
	// shouldn't also chip damage on top of all that.
	protected override bool ContactDamageEnabled => false;

	protected override float WebSpitCooldownMultiplier => _currentPhase >= 1 && _currentPhase <= PhaseWebSpitCooldownMultiplier.Length
		? PhaseWebSpitCooldownMultiplier[_currentPhase - 1]
		: 1f;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;

		_rng.Randomize();
		LockFacingLeft();
		InitFallingSpiders();
		RunExplosions();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;

		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval)
			return;

		// Simulates being chained/trapped facing one direction — Boss.cs's own physics step just
		// turned FacingRight/Visual.Scale toward wherever the player is, so this has to re-lock it
		// every frame (not just once in _Ready) to actually hold.
		LockFacingLeft();

		UpdatePhase();
	}

	private void LockFacingLeft()
	{
		FacingRight = false;
		Visual.Scale = new Vector2(-1, 1);
	}

	private void UpdatePhase()
	{
		float ratio = Stats.MaxHealth > 0 ? (float)Stats.CurrentHealth / Stats.MaxHealth : 0f;
		int newPhase = ratio > 0.66f ? 1 : ratio > 0.33f ? 2 : 3;
		if (newPhase == _currentPhase)
			return;

		_currentPhase = newPhase;
		TopUpFallingSpiders();
	}

	private float CurrentExplosionInterval => _currentPhase >= 1 && _currentPhase <= PhaseExplosionInterval.Length
		? PhaseExplosionInterval[_currentPhase - 1]
		: PhaseExplosionInterval[^1];

	private int CurrentSpiderTarget => _currentPhase >= 1 && _currentPhase <= PhaseSpiderTargetCount.Length
		? PhaseSpiderTargetCount[_currentPhase - 1]
		: 0;

	private async void InitFallingSpiders()
	{
		if (FallingSpiderInitialDelay > 0f)
			await ToSignal(GetTree().CreateTimer(FallingSpiderInitialDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		TopUpFallingSpiders();
	}

	private void TopUpFallingSpiders()
	{
		if (FallingSpiderScene is null)
			return;

		_activeFallingSpiders.RemoveAll(spider => !IsInstanceValid(spider));
		int target = CurrentSpiderTarget;
		int slot = _activeFallingSpiders.Count;
		while (_activeFallingSpiders.Count < target)
			SpawnFallingSpider(slot++);
	}

	private void SpawnFallingSpider(int slot)
	{
		Node instance = FallingSpiderScene.Instantiate();
		GetTree().CurrentScene.AddChild(instance);
		if (instance is not Node2D spider2D)
			return;

		// Anchored on the player, not the boss — dropped near the boss's own (fixed) position they'd
		// often land well outside the player's DetectionRange and just sit there doing nothing.
		Node2D player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		Vector2 anchor = player?.GlobalPosition ?? GlobalPosition;

		// Alternating side + growing spread per slot so a batch topped up together doesn't all land
		// on top of each other and read as "only one spider" on screen.
		float side = slot % 2 == 0 ? 1f : -1f;
		float offsetX = side * FallingSpiderSpawnRadiusX * (1 + slot / 2);
		spider2D.GlobalPosition = anchor + new Vector2(offsetX, -FallingSpiderSpawnHeight);
		_activeFallingSpiders.Add(spider2D);

		Stats spiderStats = spider2D.GetNodeOrNull<Stats>("Stats");
		if (spiderStats is not null)
			spiderStats.Died += () => OnFallingSpiderDied(spider2D);

		WatchdogFallingSpider(spider2D);
	}

	private async void WatchdogFallingSpider(Node2D spider)
	{
		await ToSignal(GetTree().CreateTimer(FallingSpiderLostTimeout), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;
		if (!IsInstanceValid(spider) || !_activeFallingSpiders.Contains(spider))
			return; // already died normally and was already handled by OnFallingSpiderDied

		// A spider that landed and is just alive and fighting is NOT lost — only one still mid-air
		// after this long (fell past the floor, out of bounds, etc.) actually is.
		if (spider is CharacterBody2D body && body.IsOnFloor())
			return;

		spider.QueueFree();
		_activeFallingSpiders.Remove(spider);
		TopUpFallingSpiders();
	}

	private async void OnFallingSpiderDied(Node2D spider)
	{
		_activeFallingSpiders.Remove(spider);

		if (FallingSpiderRespawnDelay > 0f)
			await ToSignal(GetTree().CreateTimer(FallingSpiderRespawnDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		TopUpFallingSpiders();
	}

	// Runs for the boss's whole lifetime once started in _Ready — CurrentExplosionInterval just
	// reads shorter as _currentPhase climbs, so the loop itself never needs restarting per phase.
	private async void RunExplosions()
	{
		if (_explosionLoopStarted)
			return;
		_explosionLoopStarted = true;

		while (IsInstanceValid(this) && !IsQueuedForRemoval)
		{
			await ToSignal(GetTree().CreateTimer(CurrentExplosionInterval), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;

			if (!PlayerDetected)
				continue;

			Node2D player = GetTree().GetFirstNodeInGroup("player") as Node2D;
			if (player is null)
				continue;

			Vector2 markPosition = player.GlobalPosition;

			// Held manually (not via VfxSpawner's play-then-self-free helper) because the telegraph
			// window and the marker animation's own length aren't the same thing.
			var marker = new AnimatedSprite2D
			{
				SpriteFrames = GD.Load<SpriteFrames>(MarkerFramesPath),
				Animation = "marker",
			};
			GetTree().CurrentScene.AddChild(marker);
			marker.GlobalPosition = markPosition;
			marker.Play("marker");

			// Child of the marker so it's freed automatically with marker.QueueFree() below.
			marker.AddChild(new PointLight2D
			{
				Color = MarkerLightColor,
				Energy = MarkerLightEnergy,
				Texture = GD.Load<Texture2D>(LightTexturePath),
				TextureScale = MarkerLightScale,
			});

			await ToSignal(GetTree().CreateTimer(ExplosionTelegraphDuration), SceneTreeTimer.SignalName.Timeout);
			marker.QueueFree();
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;

			VfxSpawner.SpawnAt(this, markPosition, ExplosionFramesPath, ExplosionAnimation);
			SpawnExplosionFlash(markPosition);

			Hazard hazard = Hazard.CreateArea(this, instantKill: false, damage: ExplosionDamage, knockbackForce: 260f);
			hazard.GlobalPosition = markPosition;
			hazard.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = ExplosionRadius } });

			await ToSignal(GetTree().CreateTimer(ExplosionActiveDuration), SceneTreeTimer.SignalName.Timeout);
			if (IsInstanceValid(hazard))
				hazard.QueueFree();
		}
	}

	private async void SpawnExplosionFlash(Vector2 position)
	{
		var light = new PointLight2D
		{
			Color = ExplosionLightColor,
			Energy = ExplosionFlashEnergy,
			Texture = GD.Load<Texture2D>(LightTexturePath),
			TextureScale = ExplosionFlashScale,
		};
		GetTree().CurrentScene.AddChild(light);
		light.GlobalPosition = position;

		await ToSignal(GetTree().CreateTimer(ExplosionFlashDuration), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(light))
			light.QueueFree();
	}
}
