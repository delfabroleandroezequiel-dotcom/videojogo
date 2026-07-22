using Godot;
using Metroidvania.Player;
using Metroidvania.Quests;
using Metroidvania.Shared;
using Metroidvania.UI;
using Metroidvania.World;

namespace Metroidvania.Save;

public partial class LevelBootstrap : Node
{
	[Export] public string ScenePath = "res://scenes/world/Casa1.tscn";
	[Export] public string ZoneName = "";
	[Export] public CameraProfile CameraProfile;
	[Export] public int CameraLimitLeft = -10000000;
	[Export] public int CameraLimitRight = 10000000;
	[Export] public int CameraLimitTop = -10000000;
	[Export] public int CameraLimitBottom = 10000000;

	private Player.Player _player;

	public override void _Ready()
	{
		EnemyCombatCoordinator.Reset();

		// Guards against a level reload/death happening mid-hit-stop, which would otherwise
		// orphan that coroutine and leave the whole game permanently in slow motion.
		Engine.TimeScale = 1.0;

		_player = GetNode<Player.Player>("Player");
		ApplyCameraConfig();

		if (!string.IsNullOrEmpty(ZoneName))
			ZoneTitle.Instance.Show(ZoneName);

		if (SaveManager.Instance.PendingSpawnPosition.HasValue)
		{
			_player.GlobalPosition = SaveManager.Instance.PendingSpawnPosition.Value;
			_player.GetNode<Camera2D>("Camera2D").ResetSmoothing();
			SaveManager.Instance.PendingSpawnPosition = null;
			return;
		}

		SaveData pending = SaveManager.Instance.PendingLoad;
		if (pending is not null)
			ApplySave(pending);

		SaveManager.Instance.ClearPendingLoad();
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
		data.ActiveQuests.AddRange(QuestManager.Instance.GetActiveQuestIds());
		data.CompletedQuests.AddRange(QuestManager.Instance.GetCompletedQuestIds());
		foreach (System.Collections.Generic.KeyValuePair<string, int> entry in QuestManager.Instance.SnapshotProgress())
			data.QuestProgress[entry.Key] = entry.Value;
		data.CollectedItems.AddRange(SaveManager.Instance.GetCollectedItems());
		data.EquippedRings.AddRange(SaveManager.Instance.GetEquippedRings());
		data.CollectedPickups.AddRange(SaveManager.Instance.GetCollectedPickups());
		data.MaxHealCharges = SaveManager.Instance.GetMaxHealCharges();
		data.Gold = SaveManager.Instance.Gold;
		data.EnemyRandomizerEnabled = SaveManager.Instance.EnemyRandomizerEnabled;
		data.RandomizerSeed = SaveManager.Instance.RandomizerSeed;

		SaveManager.Instance.SaveGame(SaveManager.Instance.CurrentSlot, data);
		SaveManager.Instance.ClearCommonEnemyDefeats();
		SaveManager.Instance.LoadGame(SaveManager.Instance.CurrentSlot);
		SaveManager.Instance.SessionCurrentHealth = null;
		SaveManager.Instance.SessionHealCharges = null;
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

		if (CameraProfile is null)
			return;

		camera.Zoom = new Vector2(CameraProfile.Zoom, CameraProfile.Zoom);
		camera.PositionSmoothingSpeed = CameraProfile.SmoothingSpeed;
		_player.ProfileCameraOffsetY = CameraProfile.OffsetY;
		_player.ProfileZoom = CameraProfile.Zoom;
	}
}
