using System.Collections.Generic;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// A harder, rooted-in-place variant of SpiderBoss for a dedicated arena — none of this touches
// SpiderBoss.cs itself, since that script is still fighting live in CuevaBosqueLobo1. "Stationary"
// needs no code at all: MoveSpeed/LungeChance/RetreatChance = 0 on the instance already fully
// roots a Boss (see MushroomEnemy's header comment for the same trick). This class only adds what
// makes THIS fight distinct: a phase counter driving how many spiders fall from above, and a
// phase-3 telegraphed ground explosion on the player's position.
public partial class SpiderBossArena : SpiderBoss
{
	[Export] public PackedScene FallingSpiderScene;
	// Index 0/1/2 = phase 1/2/3's target alive count. Phase 3 keeps the same 2 spiders as phase 2
	// on top of its own explosion chain, not instead of them — the two pressures stack.
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

	[Export] public float Phase3ExplosionInterval = 1f;
	[Export] public float Phase3TelegraphDuration = 0.85f;
	[Export] public float Phase3ExplosionActiveDuration = 0.35f;
	[Export] public int Phase3ExplosionDamage = 35;
	[Export] public float Phase3ExplosionRadius = 70f;
	[Export] public string MarkerFramesPath = "res://resources/sprites/Pj3MagicOrbLargeGreenSpriteFrames.tres";
	[Export] public string ExplosionFramesPath = "res://resources/sprites/SmokePoisonSpriteFrames.tres";
	[Export] public string ExplosionAnimation = "smoke_poison";
	// Separate from ExplosionFramesPath on purpose — that one's animation is now "smoke_poison",
	// but Hitbox.cs always plays "impact" for a custom impactFramesPath, so the melee spark needs
	// its own resource (the poison pack's "impact" anim) rather than sharing the phase-3 one.
	[Export] public string MeleeImpactFramesPath = "res://resources/sprites/FanfxPoisonLargeGreenSpriteFrames.tres";

	// Boss.cs's own melee follow-up is a per-swing ComboChance coin flip, which is why this whole
	// Attack() is a full replacement rather than a call to base. MeleeBurstMinHits/MaxHits can widen
	// this into a multi-swing burst, but default to a single swing — the "attack" SpriteFrames
	// animation isn't built to repeat cleanly back to back, so 2-3 in a row just looked like one
	// stretched-out, broken animation rather than distinct strikes.
	[Export] public int MeleeBurstMinHits = 1;
	[Export] public int MeleeBurstMaxHits = 1;
	[Export] public float MeleeBurstGapDuration = 0.25f;

	private int _currentPhase = 1;
	private bool _phase3Running;
	private readonly List<Node2D> _activeFallingSpiders = new();
	// GD only exposes RandRange (float); RandiRange lives on RandomNumberGenerator itself, and
	// Boss.cs's own _rng is private, so this needs its own instance rather than reusing that one.
	private readonly RandomNumberGenerator _burstRng = new();

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;

		_burstRng.Randomize();
		LockFacingLeft();
		InitFallingSpiders();
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
		// every frame (not just once in _Ready) to actually hold, including the attack hitbox's
		// side (it reads FacingRight at swing time), which should also always land to the left.
		LockFacingLeft();

		UpdatePhase();
	}

	private void LockFacingLeft()
	{
		FacingRight = false;
		Visual.Scale = new Vector2(-1, 1);
	}

	// Full replacement of Boss.Attack (not a call to base) — swings MeleeBurstGapDuration apart,
	// burst length picked once at the start, then AttackCooldown as the real rest before the next
	// burst can begin. IsEnraged's cooldown multiplier is skipped on purpose: this arena instance
	// sets EnrageHealthPercent=0, so it'd never apply anyway.
	protected override async void Attack(bool isCombo = false)
	{
		int burstCount = _burstRng.RandiRange(MeleeBurstMinHits, MeleeBurstMaxHits);

		for (int i = 0; i < burstCount; i++)
		{
			_attacking = true;
			_canAttack = false;
			_attackAnimation = DefaultAttackAnimation;
			Sprite?.Play(_attackAnimation);

			if (AttackHitboxDelay > 0f)
				await ToSignal(GetTree().CreateTimer(AttackHitboxDelay), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this))
				return;

			// Custom (dark poison) impact spark instead of Hitbox's default bright white-ish flash —
			// the default reads as too flashy/clashing against this boss's darker recolor. Also
			// darkened further via impactModulate — the source frames alone still read too bright.
			_hitbox.Position = new Vector2(FacingRight ? AttackHitboxReach : -AttackHitboxReach, 0);
			_hitbox.Activate(Stats, impactFramesPath: MeleeImpactFramesPath, impactModulate: new Color(0.35f, 0.35f, 0.35f, 1f));

			await ToSignal(GetTree().CreateTimer(AttackDuration), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this))
				return;
			_hitbox.Deactivate();

			float remainingAnimTime = Mathf.Max(0f, AttackAnimDuration - AttackHitboxDelay - AttackDuration);
			await ToSignal(GetTree().CreateTimer(remainingAnimTime), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this))
				return;
			_attacking = false;

			if (i < burstCount - 1 && MeleeBurstGapDuration > 0f)
			{
				await ToSignal(GetTree().CreateTimer(MeleeBurstGapDuration), SceneTreeTimer.SignalName.Timeout);
				if (!IsInstanceValid(this))
					return;
			}
		}

		await ToSignal(GetTree().CreateTimer(AttackCooldown), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			_canAttack = true;
	}

	private void UpdatePhase()
	{
		float ratio = Stats.MaxHealth > 0 ? (float)Stats.CurrentHealth / Stats.MaxHealth : 0f;
		int newPhase = ratio > 0.66f ? 1 : ratio > 0.33f ? 2 : 3;
		if (newPhase == _currentPhase)
			return;

		_currentPhase = newPhase;
		TopUpFallingSpiders();

		if (_currentPhase >= 3 && !_phase3Running)
		{
			_phase3Running = true;
			RunPhase3Explosions();
		}
	}

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

		// Alternating side + growing spread per slot (same idea as SpiderBoss.SummonSpiderlings) —
		// two spiders topped up in the same batch used to both roll an independent random offset
		// and could land right on top of each other, reading as "only one spider" on screen even
		// though the count was correct. This guarantees real separation instead.
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

		// A spider that landed and is just alive and fighting is NOT lost — only one still
		// mid-air after this long (fell past the floor, out of bounds, etc.) actually is. Without
		// this check every spider that simply survives past the timeout got yanked out from under
		// the player, which is the "disappearing spiders" bug.
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

	private async void RunPhase3Explosions()
	{
		while (IsInstanceValid(this) && !IsQueuedForRemoval && _currentPhase >= 3)
		{
			await ToSignal(GetTree().CreateTimer(Phase3ExplosionInterval), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval || _currentPhase < 3)
				break;

			Node2D player = GetTree().GetFirstNodeInGroup("player") as Node2D;
			if (player is null)
				continue;

			Vector2 markPosition = player.GlobalPosition;

			// Held manually (not via VfxSpawner's play-then-self-free helper) because the telegraph
			// window and the marker animation's own length aren't the same thing — the marker needs
			// to sit there for exactly Phase3TelegraphDuration regardless of how long its loop is.
			var marker = new AnimatedSprite2D
			{
				SpriteFrames = GD.Load<SpriteFrames>(MarkerFramesPath),
				Animation = "marker",
			};
			GetTree().CurrentScene.AddChild(marker);
			marker.GlobalPosition = markPosition;
			marker.Play("marker");

			await ToSignal(GetTree().CreateTimer(Phase3TelegraphDuration), SceneTreeTimer.SignalName.Timeout);
			marker.QueueFree();
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;

			VfxSpawner.SpawnAt(this, markPosition, ExplosionFramesPath, ExplosionAnimation);

			Hazard hazard = Hazard.CreateArea(this, instantKill: false, damage: Phase3ExplosionDamage, knockbackForce: 260f);
			hazard.GlobalPosition = markPosition;
			hazard.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = Phase3ExplosionRadius } });

			await ToSignal(GetTree().CreateTimer(Phase3ExplosionActiveDuration), SceneTreeTimer.SignalName.Timeout);
			if (IsInstanceValid(hazard))
				hazard.QueueFree();
		}
	}
}
