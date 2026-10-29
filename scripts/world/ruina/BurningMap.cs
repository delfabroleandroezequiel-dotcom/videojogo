using Godot;
using Metroidvania.Player;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Turns an existing map into its burning "near future" version (the opening's escape run) without
// copying it: the Llamas_* scenes inherit the original map and add this node. On start it
//   * empties the map of life (NPCs, animals, enemies, coins, triggers),
//   * points the exits of the escape route at their burning versions and closes the rest,
//   * paints the disaster: red light, ash in the air, fires along the ground, a rumbling camera
//     and a meteor/rock shower aimed at where the player is heading,
//   * gives future Delfrad his double jump and dash (scene only, not saved),
//   * and turns death into "back to the start of this map" (held at 1 HP, fade, reload).
// The look itself (desaturated, ember-tinted) comes from the "Llamas_" lacquer preset.
public partial class BurningMap : Node
{
	[ExportGroup("Route")]
	// Pairs "original scene file -> burning scene file" for the exits that stay open.
	[Export] public Godot.Collections.Dictionary<string, string> ExitRemap = new()
	{
		{ "res://scenes/world/Mapas/BosqueLobos2.tscn", "res://scenes/world/Mapas/Llamas/Llamas_BosqueLobos2.tscn" },
		{ "res://scenes/world/Mapas/BosqueLobos1.tscn", "res://scenes/world/Mapas/Llamas/Llamas_BosqueLobos1.tscn" },
		{ "res://scenes/world/Mapas/Nix.tscn", "res://scenes/world/Mapas/Llamas/Llamas_Nix.tscn" },
	};
	// Exits left as they are (e.g. the house door, where the memory begins).
	[Export] public string[] KeepExits = { "res://scenes/world/Mapas/Casa1.tscn" };
	// Instanced scenes removed from the map: the living world that's gone.
	[Export] public string[] RemoveScenes =
	{
		"res://scenes/world/Npc.tscn", "res://scenes/world/Coin.tscn", "res://scenes/world/Crow.tscn",
		"res://scenes/world/Gaviota.tscn", "res://scenes/world/GaviotaVoladora.tscn",
		"res://scenes/world/DialogueTrigger.tscn", "res://scenes/world/TrainingDummy.tscn",
		"res://scenes/world/Rehusables/Akita.tscn", "res://scenes/world/Rehusables/GoldenRetriever.tscn",
		"res://scenes/world/Rehusables/Schnauzer.tscn", "res://scenes/world/Rehusables/SiberianHusky.tscn",
		"res://scenes/world/Rehusables/CatOrange.tscn", "res://scenes/world/WeatherOverlay.tscn",
		"res://scenes/world/MosquitoEnemy.tscn", "res://scenes/world/RangedEnemy.tscn",
	};

	[ExportGroup("Look")]
	[Export] public Color LightTint = new(1f, 0.62f, 0.5f, 1f);
	[Export] public PackedScene WeatherScene;
	// WeatherOverlay's index for drifting ash.
	[Export] public int AshWeatherIndex = 3;
	[Export] public PackedScene FireScene;
	[Export] public float FireSpacing = 230f;
	[Export] public Vector2 FireScale = new(1.7f, 1.7f);

	[ExportGroup("Disaster")]
	[Export] public float MeteorIntervalMin = 1.3f;
	[Export] public float MeteorIntervalMax = 2.6f;
	// Share of the falls that are plain rocks (smaller, no fire left behind).
	[Export] public float RockChance = 0.35f;
	[Export] public int MeteorDamage = 18;
	[Export] public float RumbleIntervalMin = 2.5f;
	[Export] public float RumbleIntervalMax = 5.5f;
	[Export] public bool GrantFutureKit = true;

	private static readonly RandomNumberGenerator Rng = new();
	private Metroidvania.Player.Player _player;
	private float _meteorTimer = 2f;
	private float _rumbleTimer = 1.5f;
	private bool _reloading;

	static BurningMap() => Rng.Randomize();

	public override void _Ready() => Callable.From(Setup).CallDeferred();

	private Node Map => GetParent();

	private void Setup()
	{
		_player = GetTree().GetFirstNodeInGroup("player") as Metroidvania.Player.Player;
		EmptyTheMap(Map);
		foreach (Node enemy in GetTree().GetNodesInGroup("enemy"))
			enemy.QueueFree();
		foreach (Node npc in GetTree().GetNodesInGroup("npc"))
			npc.QueueFree();
		RewireExits(Map);

		// Recursive: some maps keep it inside a shared atmosphere scene (AtmosferaBosqueLobos).
		if (Map.FindChild("CanvasModulate", true, false) is CanvasModulate modulate)
			modulate.Color = LightTint;
		else
			Map.AddChild(new CanvasModulate { Color = LightTint });

		if (WeatherScene?.Instantiate() is Node weather)
		{
			weather.Set("ForceWeatherIndex", AshWeatherIndex);
			Map.AddChild(weather);
		}
		LightFiresWhenPhysicsIsUp();

		if (_player is not null)
		{
			if (GrantFutureKit)
			{
				var abilities = _player.GetNode<PlayerAbilities>("Abilities");
				abilities.GrantForScene(PlayerAbilities.DoubleJump);
				abilities.GrantForScene(PlayerAbilities.Dash);
			}
			var stats = _player.GetNode<Stats>("Stats");
			stats.MinHealth = 1;
			stats.HealthChanged += (current, _) =>
			{
				if (current <= 1)
					RestartMap();
			};
		}
	}

	private void EmptyTheMap(Node node)
	{
		foreach (Node child in node.GetChildren())
		{
			if (child == this)
				continue;
			if (System.Array.IndexOf(RemoveScenes, child.SceneFilePath) >= 0)
			{
				child.QueueFree();
				continue;
			}
			EmptyTheMap(child);
		}
	}

	private void RewireExits(Node node)
	{
		foreach (Node child in node.GetChildren())
		{
			if (child is LevelTransition exit)
			{
				if (ExitRemap.TryGetValue(exit.TargetScenePath, out string burning))
					exit.TargetScenePath = burning;
				else if (System.Array.IndexOf(KeepExits, exit.TargetScenePath) < 0)
					exit.QueueFree();
				continue;
			}
			RewireExits(child);
		}
	}

	// The floor bodies only answer ray casts after the first physics step.
	private async void LightFiresWhenPhysicsIsUp()
	{
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		if (IsInstanceValid(this))
			LightFires();
	}

	// Fires along the ground every FireSpacing (found by casting down), harmless set dressing.
	private void LightFires()
	{
		// The map root's C# class (LevelBootstrap) isn't a Node2D type even on a Node2D, so the
		// player stands in for the 2D world when casting rays.
		if (FireScene is null || _player is null || Map.Get("CameraLimitLeft").VariantType == Variant.Type.Nil)
			return;
		Node2D world = _player;
		float left = Map.Get("CameraLimitLeft").AsInt32();
		float right = Map.Get("CameraLimitRight").AsInt32();
		float top = Map.Get("CameraLimitTop").AsInt32();
		if (top < -100000)
			top = -400f;
		for (float x = left + FireSpacing * 0.5f; x < right; x += FireSpacing * Rng.RandfRange(0.7f, 1.3f))
		{
			Vector2? ground = GroundBelow(world, new Vector2(x, top));
			if (ground is null || FireScene.Instantiate() is not Node2D fire)
				continue;
			fire.Set("TickDamage", 0);
			fire.Set("HoldDuration", 99999f);
			fire.Scale = FireScale * Rng.RandfRange(0.8f, 1.25f);
			Map.AddChild(fire);
			fire.GlobalPosition = ground.Value;
		}
	}

	public static Vector2? GroundBelow(Node2D context, Vector2 from, float depth = 2500f)
	{
		var query = PhysicsRayQueryParameters2D.Create(from, from + new Vector2(0f, depth), 1);
		var hit = context.GetWorld2D().DirectSpaceState.IntersectRay(query);
		return hit.Count > 0 ? hit["position"].AsVector2() : null;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_player is null || !IsInstanceValid(_player) || Cinematic.Playing)
			return;
		float dt = (float)delta;
		_rumbleTimer -= dt;
		if (_rumbleTimer <= 0f)
		{
			_rumbleTimer = Rng.RandfRange(RumbleIntervalMin, RumbleIntervalMax);
			_player.ShakeCamera(Rng.RandfRange(1.5f, 3.5f), Rng.RandfRange(0.4f, 0.9f));
		}
		_meteorTimer -= dt;
		if (_meteorTimer <= 0f)
		{
			_meteorTimer = Rng.RandfRange(MeteorIntervalMin, MeteorIntervalMax);
			DropMeteor();
		}
	}

	// Aimed a little ahead of where the player is going, so running doesn't outpace it and
	// standing still doesn't make it trivial; sometimes right on top of them.
	private void DropMeteor()
	{
		float facing = Mathf.Abs(_player.Velocity.X) > 20f ? Mathf.Sign(_player.Velocity.X) : (_player.IsFacingRight ? 1f : -1f);
		float x = _player.GlobalPosition.X + _player.Velocity.X * 0.9f + facing * Rng.RandfRange(-60f, 200f);
		Vector2? ground = GroundBelow(_player, new Vector2(x, _player.GlobalPosition.Y - 500f));
		if (ground is null)
			return;
		bool rock = Rng.Randf() < RockChance;
		var meteor = new Meteor
		{
			Target = ground.Value,
			IsRock = rock,
			Damage = rock ? MeteorDamage / 2 : MeteorDamage,
			FireScene = rock ? null : FireScene,
			FromRight = Rng.Randf() < 0.5f,
		};
		Map.AddChild(meteor);
	}

	private async void RestartMap()
	{
		if (_reloading)
			return;
		_reloading = true;
		_player.GetNode<Stats>("Stats").GodMode = true;
		_player.HoldForCutscene(true);
		var layer = new CanvasLayer { Layer = 60 };
		var black = new ColorRect { Color = new Color(0f, 0f, 0f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
		black.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		layer.AddChild(black);
		AddChild(layer);
		Tween tween = black.CreateTween();
		tween.TweenProperty(black, "color:a", 1f, 0.8f);
		await ToSignal(tween, Tween.SignalName.Finished);
		GetTree().ReloadCurrentScene();
	}
}
