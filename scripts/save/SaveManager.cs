using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Metroidvania.Save;

public partial class SaveManager : Node
{
	public const int SlotCount = 6;

	public static SaveManager Instance { get; private set; }
	public int CurrentSlot { get; set; }
	public string CurrentCharacterName { get; set; } = "Héroe";
	public SaveData PendingLoad { get; private set; }

	private readonly HashSet<string> _defeatedBosses = new();
	private readonly HashSet<string> _defeatedCommonEnemies = new();
	private readonly HashSet<string> _litSavePoints = new();

	public override void _Ready()
	{
		Instance = this;
	}

	public bool IsCommonEnemyDefeated(string id) => _defeatedCommonEnemies.Contains(id);
	public void MarkCommonEnemyDefeated(string id) => _defeatedCommonEnemies.Add(id);
	public void ClearCommonEnemyDefeats() => _defeatedCommonEnemies.Clear();
	public int GetDefeatedCommonEnemyCount() => _defeatedCommonEnemies.Count;

	public bool IsBossDefeated(string id) => _defeatedBosses.Contains(id);
	public void MarkBossDefeated(string id) => _defeatedBosses.Add(id);
	public IReadOnlyCollection<string> GetDefeatedBosses() => _defeatedBosses;

	public bool IsSavePointLit(string id) => _litSavePoints.Contains(id);
	public void MarkSavePointLit(string id) => _litSavePoints.Add(id);
	public IReadOnlyCollection<string> GetLitSavePoints() => _litSavePoints;

	public void ResetProgressState()
	{
		_defeatedBosses.Clear();
		_defeatedCommonEnemies.Clear();
		_litSavePoints.Clear();
	}

	private static string GetPath(int slot) => $"user://savegame_slot_{slot}.json";

	public bool HasSaveFile(int slot) => FileAccess.FileExists(GetPath(slot));

	public void SaveGame(int slot, SaveData data)
	{
		string json = JsonSerializer.Serialize(data);
		using FileAccess file = FileAccess.Open(GetPath(slot), FileAccess.ModeFlags.Write);
		file.StoreString(json);
	}

	public SaveData LoadGame(int slot)
	{
		if (!HasSaveFile(slot))
			return null;

		using FileAccess file = FileAccess.Open(GetPath(slot), FileAccess.ModeFlags.Read);
		string json = file.GetAsText();
		PendingLoad = JsonSerializer.Deserialize<SaveData>(json);
		CurrentCharacterName = PendingLoad.CharacterName;

		_defeatedCommonEnemies.Clear();
		_defeatedBosses.Clear();
		foreach (string bossId in PendingLoad.DefeatedBosses)
			_defeatedBosses.Add(bossId);

		_litSavePoints.Clear();
		foreach (string savePointId in PendingLoad.LitSavePoints)
			_litSavePoints.Add(savePointId);

		return PendingLoad;
	}

	public SaveData PeekSave(int slot)
	{
		if (!HasSaveFile(slot))
			return null;

		using FileAccess file = FileAccess.Open(GetPath(slot), FileAccess.ModeFlags.Read);
		PendingLoad = JsonSerializer.Deserialize<SaveData>(file.GetAsText());
		return PendingLoad;
	}

	public string GetCharacterName(int slot)
	{
		if (!HasSaveFile(slot))
			return null;

		using FileAccess file = FileAccess.Open(GetPath(slot), FileAccess.ModeFlags.Read);
		SaveData data = JsonSerializer.Deserialize<SaveData>(file.GetAsText());
		return data?.CharacterName;
	}

	public void DeleteSave(int slot)
	{
		if (HasSaveFile(slot))
			DirAccess.RemoveAbsolute(GetPath(slot));
	}

	public void ClearPendingLoad() => PendingLoad = null;

	public int FindMostRecentSlot()
	{
		int bestSlot = -1;
		DateTime bestTime = DateTime.MinValue;

		for (int slot = 0; slot < SlotCount; slot++)
		{
			if (!HasSaveFile(slot))
				continue;

			string globalPath = ProjectSettings.GlobalizePath(GetPath(slot));
			DateTime modifiedTime = System.IO.File.GetLastWriteTimeUtc(globalPath);
			if (modifiedTime > bestTime)
			{
				bestTime = modifiedTime;
				bestSlot = slot;
			}
		}

		return bestSlot;
	}
}
