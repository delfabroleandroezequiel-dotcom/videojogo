using Godot;
using Metroidvania.Player;
using Metroidvania.Quests;

namespace Metroidvania.Save;

public partial class LevelBootstrap : Node
{
	[Export] public string ScenePath = "res://scenes/world/TestLevel.tscn";

	private Player.Player _player;

	public override void _Ready()
	{
		_player = GetNode<Player.Player>("Player");

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
		};
		data.UnlockedAbilities.AddRange(abilities.GetUnlocked());
		data.DefeatedBosses.AddRange(SaveManager.Instance.GetDefeatedBosses());
		data.LitSavePoints.AddRange(SaveManager.Instance.GetLitSavePoints());
		data.ActiveQuests.AddRange(QuestManager.Instance.GetActiveQuestIds());
		data.CompletedQuests.AddRange(QuestManager.Instance.GetCompletedQuestIds());
		foreach (System.Collections.Generic.KeyValuePair<string, int> entry in QuestManager.Instance.SnapshotProgress())
			data.QuestProgress[entry.Key] = entry.Value;
		data.CollectedItems.AddRange(SaveManager.Instance.GetCollectedItems());
		data.EquippedRings.AddRange(SaveManager.Instance.GetEquippedRings());

		SaveManager.Instance.SaveGame(SaveManager.Instance.CurrentSlot, data);
		SaveManager.Instance.ClearCommonEnemyDefeats();
		SaveManager.Instance.LoadGame(SaveManager.Instance.CurrentSlot);
		GetTree().CallDeferred(SceneTree.MethodName.ReloadCurrentScene);
	}

	public void RespawnPlayer()
	{
		int slot = SaveManager.Instance.CurrentSlot;
		if (SaveManager.Instance.HasSaveFile(slot))
			SaveManager.Instance.PeekSave(slot);
		else
			SaveManager.Instance.ClearPendingLoad();

		SaveManager.Instance.ClearCommonEnemyDefeats();
		GetTree().CallDeferred(SceneTree.MethodName.ReloadCurrentScene);
	}

	private void ApplySave(SaveData data)
	{
		_player.GlobalPosition = new Vector2(data.PositionX, data.PositionY);
		_player.GetNode<Camera2D>("Camera2D").ResetSmoothing();

		PlayerAbilities abilities = _player.GetNode<PlayerAbilities>("Abilities");
		foreach (string abilityId in data.UnlockedAbilities)
			abilities.Unlock(abilityId);
	}
}
