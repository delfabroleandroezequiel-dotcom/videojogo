using Godot;
using Metroidvania.Player;
using Metroidvania.Quests;
using Metroidvania.Shared;
using Metroidvania.UI;
using Metroidvania.World;

namespace Metroidvania.Save;

public partial class LevelBootstrap : Node
{
	[Export] public string ScenePath = "res://scenes/world/Mapas/Casa1.tscn";
	[Export] public string ZoneName = "";
	[Export] public CameraProfile CameraProfile;
	[Export] public int CameraLimitLeft = -10000000;
	[Export] public int CameraLimitRight = 10000000;
	[Export] public int CameraLimitTop = -10000000;
	[Export] public int CameraLimitBottom = 10000000;

	// Player.FallDeathY defaults to a flat 700 that has nothing to do with any particular map's
	// actual depth — several interiors/caves already sit deeper than that and needed a manual
	// per-scene FallDeathY override just to stop the player dying on their own floor. CameraLimitBottom
	// is already authored per map for the camera clamp, so it's a ready-made per-map depth reference:
	// only ever raises FallDeathY (never lowers it below whatever the scene explicitly set), so an
	// existing manual override still wins whenever it's already deeper than this margin.
	[Export] public float FallDeathMargin = 300f;

	// Video/showcase maps (e.g. MidirArena): hide the player's whole HUD layer, and let F cycle the
	// blade's element for free (no resins, no timer) the way it worked before the quick bar.
	[Export] public bool HideHud;
	[Export] public bool FreeElementCycle;

	private Player.Player _player;

	public override void _Ready()
	{
		EnemyCombatCoordinator.Reset();

		// Guards against a level reload/death happening mid-hit-stop, which would otherwise
		// orphan that coroutine and leave the whole game permanently in slow motion.
		Engine.TimeScale = 1.0;

		_player = GetNode<Player.Player>("Player");
		ApplyCameraConfig();
		_player.GetNode<CanvasLayer>("HUD").Visible = !HideHud;
		_player.FreeElementCycle = FreeElementCycle;

		if (!string.IsNullOrEmpty(ZoneName))
			ZoneTitle.Instance.Show(ZoneName);

		if (SaveManager.Instance.PendingSpawnPosition.HasValue)
		{
			_player.GlobalPosition = SaveManager.Instance.PendingSpawnPosition.Value;
			_player.GetNode<Camera2D>("Camera2D").ResetSmoothing();
			SaveManager.Instance.PendingSpawnPosition = null;
			if (SaveManager.Instance.PendingSpawnOnLadder)
				_player.LatchLadderOnSpawn();
			SaveManager.Instance.PendingSpawnOnLadder = false;
		}
		else
		{
			SaveManager.Instance.PendingSpawnOnLadder = false;
			SaveData pending = SaveManager.Instance.PendingLoad;
			if (pending is not null)
				ApplySave(pending);

			SaveManager.Instance.ClearPendingLoad();
		}

		SpawnCompanionIfRecruited();
		SpawnEcosRemnant();
		RandomizeEnemies();
	}

	// The Ecos the last death left in this map, waiting where the player last stood.
	private void SpawnEcosRemnant()
	{
		SaveManager save = SaveManager.Instance;
		if (save.DroppedEcos <= 0 || save.DroppedEcosScene != ScenePath)
			return;
		var remnant = new EcosRemnant { Name = "EcosRemnant" };
		AddChild(remnant);
		remnant.GlobalPosition = save.DroppedEcosPosition;
	}

	// Enemy randomizer: maps place their enemies directly (nobody authors EnemySpawnPoints), so the
	// randomizer works on what's already there — every common enemy is swapped for a pick from
	// GameConfig's ground/flying pool. Runs from the root's _Ready, i.e. after every enemy's own
	// _Ready (already-defeated ones have queued themselves out). Bosses and enemies wired into
	// scripted events (DefeatWall, RockslideBlocker, CaveCollapseOverlay hold them by path) stay.
	// The replacement keeps the original's parent, name and position, so its PersistenceId (= node
	// path) is the same and "defeated" flags carry over between randomizer on/off.
	private void RandomizeEnemies()
	{
		if (!SaveManager.Instance.EnemyRandomizerEnabled)
			return;

		var scripted = CollectScriptedEnemies();
		foreach (Node node in GetTree().GetNodesInGroup("enemy"))
		{
			if (node is not Enemy enemy || enemy.IsQueuedForDeletion() || !IsAncestorOf(enemy))
				continue;
			if (IsBoss(enemy) || scripted.Contains(enemy))
				continue;

			PackedScene pick = PickRandomEnemy(enemy);
			if (pick is null || pick.ResourcePath == enemy.SceneFilePath)
				continue;
			ReplaceEnemy(enemy, pick);
		}
	}

	private static bool IsBoss(Enemy enemy) =>
		enemy is Boss or MushroomBoss or LargeSkullBoss || enemy.IsInGroup("boss");

	private System.Collections.Generic.HashSet<Node> CollectScriptedEnemies()
	{
		var result = new System.Collections.Generic.HashSet<Node>();
		var stack = new System.Collections.Generic.Stack<Node>();
		stack.Push(this);
		while (stack.Count > 0)
		{
			Node node = stack.Pop();
			foreach (Node child in node.GetChildren())
				stack.Push(child);

			switch (node)
			{
				case DefeatWall wall:
					foreach (NodePath path in wall.Enemies)
						AddResolved(result, wall, path);
					break;
				case RockslideBlocker blocker:
					AddResolved(result, blocker, blocker.TriggerEnemyPath);
					break;
				case CaveCollapseOverlay overlay:
					AddResolved(result, overlay, overlay.TriggerEnemyPath);
					break;
			}
		}
		return result;
	}

	private static void AddResolved(System.Collections.Generic.HashSet<Node> set, Node from, NodePath path)
	{
		if (path is not null && !path.IsEmpty && from.GetNodeOrNull(path) is Node target)
			set.Add(target);
	}

	// Same seeding as EnemySpawnPoint: save seed XOR the enemy's stable path, so a given save always
	// reproduces the same shuffle and each spot rolls independently.
	private static PackedScene PickRandomEnemy(Enemy enemy)
	{
		PackedScene[] pool = enemy is FlyingEnemy
			? GameConfig.Instance.FlyingEnemyPool
			: GameConfig.Instance.GroundEnemyPool;
		if (pool.Length == 0)
			return null;

		uint seed = (uint)SaveManager.Instance.RandomizerSeed ^ (uint)enemy.GetPath().ToString().Hash();
		var rng = new RandomNumberGenerator { Seed = seed };
		return pool[rng.RandiRange(0, pool.Length - 1)];
	}

	private static void ReplaceEnemy(Enemy original, PackedScene scene)
	{
		Node parent = original.GetParent();
		int index = original.GetIndex();
		StringName name = original.Name;
		Vector2 position = original.Position;

		parent.RemoveChild(original);
		original.QueueFree();

		Node replacement = scene.Instantiate();
		replacement.Name = name;
		if (replacement is Enemy replacementEnemy)
			replacementEnemy.CustomPersistenceId = original.CustomPersistenceId;
		if (replacement is Node2D replacement2D)
			replacement2D.Position = position;
		parent.AddChild(replacement);
		parent.MoveChild(replacement, index);
	}

	private const string CompanionDogScenePath = "res://scenes/world/CompanionDog.tscn";

	// The dalmatian companion is shelved for now (art no longer fits) — kept intact, just never
	// spawned. Flip back to true to bring it back.
	private static readonly bool CompanionDogEnabled = false;

	// Re-spawns the dog companion fresh in every map instead of it surviving as an autoload —
	// matches how every other per-map persistent bit of state here (opened gates, lit save
	// points...) is just a SaveManager flag re-applied on load, not a node that lives forever.
	private void SpawnCompanionIfRecruited()
	{
		if (!CompanionDogEnabled || !SaveManager.Instance.CompanionRecruited)
			return;

		PackedScene companionScene = GD.Load<PackedScene>(CompanionDogScenePath);
		Node companion = companionScene.Instantiate();
		// CurrentScene, not GetParent() — LevelBootstrap sits on the map's root, so GetParent()
		// is the engine's real /root, which ChangeSceneToFile/ToPacked never touches. A child
		// added there survives every future scene change as a leftover, and each new map's
		// LevelBootstrap would then spawn another one alongside it.
		GetTree().CurrentScene.AddChild(companion);
		float behindSide = _player.IsFacingRight ? -1f : 1f;
		((Node2D)companion).GlobalPosition = _player.GlobalPosition + new Vector2(behindSide * 40f, 0f);
	}

	public void SaveAtCheckpoint(Vector2 checkpointPosition)
	{
		PlayerAbilities abilities = _player.GetNode<PlayerAbilities>("Abilities");

		SaveData data = new()
		{
			CharacterName = SaveManager.Instance.CurrentCharacterName,
			ScenePath = ScenePath,
			PositionX = checkpointPosition.X,
			PositionY = checkpointPosition.Y,
			StoryStage = SaveManager.Instance.StoryStage,
			Reputation = SaveManager.Instance.Reputation,
		};
		data.UnlockedAbilities.AddRange(abilities.GetUnlocked());
		data.DefeatedBosses.AddRange(SaveManager.Instance.GetDefeatedBosses());
		data.LitSavePoints.AddRange(SaveManager.Instance.GetLitSavePoints());
		data.PlayedCutscenes.AddRange(SaveManager.Instance.GetPlayedCutscenes());
		data.OpenedGates.AddRange(SaveManager.Instance.GetOpenedGates());
		data.BrokenWalls.AddRange(SaveManager.Instance.GetBrokenWalls());
		foreach (System.Collections.Generic.KeyValuePair<string, int> entry in SaveManager.Instance.GetItemCounts())
			data.ItemCounts[entry.Key] = entry.Value;
		foreach (System.Collections.Generic.KeyValuePair<string, int> entry in SaveManager.Instance.GetShopPurchases())
			data.ShopPurchases[entry.Key] = entry.Value;
		data.CompanionRecruited = SaveManager.Instance.CompanionRecruited;
		data.ActiveQuests.AddRange(QuestManager.Instance.GetActiveQuestIds());
		data.CompletedQuests.AddRange(QuestManager.Instance.GetCompletedQuestIds());
		foreach (System.Collections.Generic.KeyValuePair<string, int> entry in QuestManager.Instance.SnapshotProgress())
			data.QuestProgress[entry.Key] = entry.Value;
		data.CollectedItems.AddRange(SaveManager.Instance.GetCollectedItems());
		data.EquippedRings.AddRange(SaveManager.Instance.GetEquippedRings());
		data.CollectedPickups.AddRange(SaveManager.Instance.GetCollectedPickups());
		data.MaxHealCharges = SaveManager.Instance.GetMaxHealCharges();
		data.Gold = SaveManager.Instance.Gold;
		data.Ecos = SaveManager.Instance.Ecos;
		foreach (System.Collections.Generic.KeyValuePair<string, int> entry in SaveManager.Instance.GetStatLevels())
			data.StatLevels[entry.Key] = entry.Value;
		data.DroppedEcos = SaveManager.Instance.DroppedEcos;
		data.DroppedEcosScene = SaveManager.Instance.DroppedEcosScene;
		data.DroppedEcosX = SaveManager.Instance.DroppedEcosPosition.X;
		data.DroppedEcosY = SaveManager.Instance.DroppedEcosPosition.Y;
		for (int i = 0; i < SaveManager.QuickSlotCount; i++)
			data.QuickSlots.Add(SaveManager.Instance.GetQuickSlot(i));
		data.SelectedQuickSlot = SaveManager.Instance.SelectedQuickSlot;
		// In slot order, so each type keeps its HUD square after loading.
		foreach (string shadowKey in SaveManager.Instance.GetShadowSlotKeys())
			data.ShadowUses[shadowKey] = SaveManager.Instance.GetShadowUses(shadowKey);
		data.EnemyRandomizerEnabled = SaveManager.Instance.EnemyRandomizerEnabled;
		data.RandomizerSeed = SaveManager.Instance.RandomizerSeed;

		SaveManager.Instance.SaveGame(SaveManager.Instance.CurrentSlot, data);
		SaveManager.Instance.ClearCommonEnemyDefeats();
		SaveManager.Instance.LoadGame(SaveManager.Instance.CurrentSlot);
		SaveManager.Instance.SessionCurrentHealth = null;
		SaveManager.Instance.SessionHealCharges = null;
		SaveManager.Instance.SessionElement = null;
		SaveManager.Instance.SessionTorchEquipped = null;
		SaveManager.Instance.SessionShadowEssence = null;
		GetTree().CallDeferred(SceneTree.MethodName.ReloadCurrentScene);
	}

	public void RespawnPlayer()
	{
		int slot = SaveManager.Instance.CurrentSlot;
		string targetScenePath;

		if (SaveManager.Instance.HasSaveFile(slot))
		{
			SaveData data = SaveManager.Instance.PeekSave(slot);
			targetScenePath = data.ScenePath;
		}
		else
		{
			SaveManager.Instance.ClearPendingLoad();
			targetScenePath = GameConfig.Instance.DefaultStartScenePath;
		}

		SaveManager.Instance.ClearCommonEnemyDefeats();
		SaveManager.Instance.SessionCurrentHealth = null;
		SaveManager.Instance.SessionHealCharges = null;
		SaveManager.Instance.SessionElement = null;
		SaveManager.Instance.SessionTorchEquipped = null;
		SaveManager.Instance.SessionShadowEssence = null;
		// A "return to where you came from" LevelTransition (UseStoredReturnPosition) reads this and
		// never clears it on its own — it stays pointing at wherever the player entered a building
		// from, indefinitely, across deaths. Dying and respawning should mean starting fresh, not
		// getting walked back into whatever was near that old spot in Nix (or wherever) when it was
		// recorded; without this, respawning at home and walking back out could drop the player right
		// on top of a hazard/mob that has nothing to do with where they actually died.
		SaveManager.Instance.ReturnPositions.Clear();

		if (targetScenePath == ScenePath)
			GetTree().CallDeferred(SceneTree.MethodName.ReloadCurrentScene);
		else
			GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, targetScenePath);
	}

	private void ApplySave(SaveData data)
	{
		_player.GlobalPosition = new Vector2(data.PositionX, data.PositionY);
		_player.GetNode<Camera2D>("Camera2D").ResetSmoothing();

		PlayerAbilities abilities = _player.GetNode<PlayerAbilities>("Abilities");
		foreach (string abilityId in data.UnlockedAbilities)
			abilities.Unlock(abilityId);
	}

	private void ApplyCameraConfig()
	{
		Camera2D camera = _player.GetNode<Camera2D>("Camera2D");
		camera.LimitLeft = CameraLimitLeft;
		camera.LimitRight = CameraLimitRight;
		camera.LimitTop = CameraLimitTop;
		camera.LimitBottom = CameraLimitBottom;
		_player.FallDeathY = Mathf.Max(_player.FallDeathY, CameraLimitBottom + FallDeathMargin);

		if (CameraProfile is null)
			return;

		camera.Zoom = new Vector2(CameraProfile.Zoom, CameraProfile.Zoom);
		camera.PositionSmoothingSpeed = CameraProfile.SmoothingSpeed;
		_player.ProfileCameraOffsetY = CameraProfile.OffsetY;
		_player.ProfileZoom = CameraProfile.Zoom;
	}
}
