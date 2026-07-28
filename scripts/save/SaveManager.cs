using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using Metroidvania.Quests;
using Metroidvania.Shared;

namespace Metroidvania.Save;

public partial class SaveManager : Node
{
	public const int SlotCount = 6;

	// Bump this whenever a saved field's meaning changes, and add the corresponding
	// upgrade step to MigrateSaveData. Legacy saves written before this existed load as 0.
	public const int CurrentSaveVersion = 1;

	public static SaveManager Instance { get; private set; }
	public int CurrentSlot { get; set; }
	public string CurrentCharacterName { get; set; } = "Héroe";
	public SaveData PendingLoad { get; private set; }
	public Vector2? PendingSpawnPosition { get; set; }
	public Vector2? PendingReturnPosition { get; set; }

	// Current HP/heal-charge count carried between scenes within the same play session.
	// Null means "no cached value" — Stats/HealFlask keep their fresh full-refill default.
	// Cleared on rest and on respawn, since those are the only moments meant to fully refill.
	public int? SessionCurrentHealth { get; set; }
	public int? SessionHealCharges { get; set; }

	// Same idea, for the fire transformation and the torch — both are pure Player-instance
	// state today (no signal to piggyback on like Health/HealCharges), so Player writes
	// these directly wherever CycleElement/the torch toggle mutate them.
	public DamageElement? SessionElement { get; set; }
	public bool? SessionTorchEquipped { get; set; }

	private readonly HashSet<string> _defeatedBosses = new();
	private readonly HashSet<string> _defeatedCommonEnemies = new();
	private readonly HashSet<string> _litSavePoints = new();
	private readonly HashSet<string> _unlockedAbilities = new();
	private readonly HashSet<string> _collectedItems = new();
	private readonly HashSet<string> _collectedPickups = new();
	private readonly HashSet<string> _playedCutscenes = new();
	private readonly List<string> _equippedRings = new();

	[Signal] public delegate void GoldChangedEventHandler(int gold);

	public int Gold { get; private set; }

	// Decided once when the player creates a New Game (see MainMenu.OnNameConfirmed) and fixed
	// for that save's whole lifetime — reloading the same save always reproduces the same
	// enemy shuffle, but every new save gets its own fresh seed.
	public bool EnemyRandomizerEnabled { get; set; }
	public int RandomizerSeed { get; private set; }

	public void RollNewRandomizerSeed()
	{
		RandomizerSeed = (int)GD.Randi();
	}

	public void AddGold(int amount)
	{
		if (amount <= 0)
			return;

		Gold += amount;
		EmitSignal(SignalName.GoldChanged, Gold);
	}

	public bool SpendGold(int amount)
	{
		if (amount <= 0 || Gold < amount)
			return false;

		Gold -= amount;
		EmitSignal(SignalName.GoldChanged, Gold);
		return true;
	}

	public bool IsPickupCollected(string id) => _collectedPickups.Contains(id);
	public void MarkPickupCollected(string id) => _collectedPickups.Add(id);
	public IReadOnlyCollection<string> GetCollectedPickups() => _collectedPickups;

	public const int MaxHealChargeCap = 5;
	private int _maxHealCharges = 1;

	public int StoryStage { get; private set; }

	public void SetStoryStage(int stage)
	{
		if (stage <= StoryStage)
			return;

		StoryStage = stage;
		GameConfig.Instance.Achievements.CheckTriggers();
	}

	public int Reputation { get; private set; }

	public void AddReputation(int amount)
	{
		Reputation = Mathf.Clamp(Reputation + amount, GameConfig.ReputationMin, GameConfig.ReputationMax);
	}

	public ReputationTier GetReputationTier()
	{
		if (Reputation >= GameConfig.ReputationLoveThreshold)
			return ReputationTier.Love;

		return Reputation <= GameConfig.ReputationHateThreshold ? ReputationTier.Hate : ReputationTier.Normal;
	}

	public override void _Ready()
	{
		Instance = this;
	}

	public bool IsCommonEnemyDefeated(string id) => _defeatedCommonEnemies.Contains(id);
	public void MarkCommonEnemyDefeated(string id) => _defeatedCommonEnemies.Add(id);
	public void ClearCommonEnemyDefeats() => _defeatedCommonEnemies.Clear();
	public int GetDefeatedCommonEnemyCount() => _defeatedCommonEnemies.Count;

	public bool IsBossDefeated(string id) => _defeatedBosses.Contains(id);
	public IReadOnlyCollection<string> GetDefeatedBosses() => _defeatedBosses;

	public void MarkBossDefeated(string id)
	{
		_defeatedBosses.Add(id);
		GameConfig.Instance.Achievements.CheckTriggers();
	}

	public bool IsSavePointLit(string id) => _litSavePoints.Contains(id);
	public void MarkSavePointLit(string id) => _litSavePoints.Add(id);
	public IReadOnlyCollection<string> GetLitSavePoints() => _litSavePoints;

	// Permanent, unlike common-enemy defeats — once a cutscene plays it never plays again for
	// this save, even after resting/reloading (same lifetime as LitSavePoints/DefeatedBosses).
	public bool IsCutscenePlayed(string id) => _playedCutscenes.Contains(id);
	public void MarkCutscenePlayed(string id) => _playedCutscenes.Add(id);
	public IReadOnlyCollection<string> GetPlayedCutscenes() => _playedCutscenes;

	public bool HasAbility(string id) => _unlockedAbilities.Contains(id);
	public void UnlockAbility(string id) => _unlockedAbilities.Add(id);
	public IReadOnlyCollection<string> GetUnlockedAbilities() => _unlockedAbilities;

	public const int MaxEquippedRings = 2;

	public bool HasItem(string id) => _collectedItems.Contains(id);
	public IReadOnlyCollection<string> GetCollectedItems() => _collectedItems;

	public void CollectItem(string id)
	{
		_collectedItems.Add(id);
		GameConfig.Instance.Achievements.CheckTriggers();
	}

	public IReadOnlyList<string> GetEquippedRings() => _equippedRings;

	public bool EquipRing(string id)
	{
		if (_equippedRings.Contains(id) || _equippedRings.Count >= MaxEquippedRings)
			return false;

		_equippedRings.Add(id);
		return true;
	}

	public void UnequipRing(string id) => _equippedRings.Remove(id);

	public int GetMaxHealCharges() => _maxHealCharges;

	public bool UnlockHealCharge()
	{
		if (_maxHealCharges >= MaxHealChargeCap)
			return false;

		_maxHealCharges++;
		return true;
	}

	public void ResetProgressState()
	{
		_defeatedBosses.Clear();
		_defeatedCommonEnemies.Clear();
		_litSavePoints.Clear();
		_unlockedAbilities.Clear();
		_collectedItems.Clear();
		_equippedRings.Clear();
		_collectedPickups.Clear();
		_playedCutscenes.Clear();
		_maxHealCharges = 1;
		StoryStage = 0;
		Gold = 0;
		Reputation = 0;
		SessionCurrentHealth = null;
		SessionHealCharges = null;
		EnemyRandomizerEnabled = false;
		RollNewRandomizerSeed();
		QuestManager.Instance.ResetProgressState();
	}

	// Upgrades an older SaveData in place, one version step at a time, before it's used.
	// No steps exist yet — this is the hook for the day a saved field's meaning changes.
	// Example once needed:
	// if (data.SaveVersion < 2) { ... adjust data for the v1 -> v2 change ... }
	private static void MigrateSaveData(SaveData data)
	{
		data.SaveVersion = CurrentSaveVersion;
	}

	private static string GetPath(int slot) => $"user://savegame_slot_{slot}.json";

	public bool HasSaveFile(int slot) => FileAccess.FileExists(GetPath(slot));

	// A corrupted or partially-written save file (crash mid-write, disk issue, hand-edited
	// JSON) should never crash the game on load — treated the same as "no save file" by every
	// caller instead of letting the exception propagate.
	private static SaveData TryReadSaveFile(int slot)
	{
		try
		{
			using FileAccess file = FileAccess.Open(GetPath(slot), FileAccess.ModeFlags.Read);
			return JsonSerializer.Deserialize<SaveData>(file.GetAsText());
		}
		catch (JsonException)
		{
			GD.PushError($"Save slot {slot} is corrupted — ignoring it.");
			return null;
		}
	}

	public void SaveGame(int slot, SaveData data)
	{
		data.SaveVersion = CurrentSaveVersion;
		string json = JsonSerializer.Serialize(data);
		using FileAccess file = FileAccess.Open(GetPath(slot), FileAccess.ModeFlags.Write);
		file.StoreString(json);
	}

	public SaveData LoadGame(int slot)
	{
		if (!HasSaveFile(slot))
			return null;

		SaveData data = TryReadSaveFile(slot);
		if (data is null)
			return null;

		PendingLoad = data;
		MigrateSaveData(PendingLoad);
		CurrentCharacterName = PendingLoad.CharacterName;

		_defeatedCommonEnemies.Clear();
		_defeatedBosses.Clear();
		foreach (string bossId in PendingLoad.DefeatedBosses)
			_defeatedBosses.Add(bossId);

		_litSavePoints.Clear();
		foreach (string savePointId in PendingLoad.LitSavePoints)
			_litSavePoints.Add(savePointId);

		_playedCutscenes.Clear();
		foreach (string cutsceneId in PendingLoad.PlayedCutscenes)
			_playedCutscenes.Add(cutsceneId);

		_unlockedAbilities.Clear();
		foreach (string abilityId in PendingLoad.UnlockedAbilities)
			_unlockedAbilities.Add(abilityId);

		QuestManager.Instance.RestoreState(PendingLoad.ActiveQuests, PendingLoad.CompletedQuests, PendingLoad.QuestProgress);

		_collectedItems.Clear();
		foreach (string itemId in PendingLoad.CollectedItems)
			_collectedItems.Add(itemId);

		_equippedRings.Clear();
		_equippedRings.AddRange(PendingLoad.EquippedRings);

		_collectedPickups.Clear();
		foreach (string pickupId in PendingLoad.CollectedPickups)
			_collectedPickups.Add(pickupId);

		_maxHealCharges = Mathf.Clamp(PendingLoad.MaxHealCharges, 1, MaxHealChargeCap);
		StoryStage = PendingLoad.StoryStage;
		Gold = PendingLoad.Gold;
		Reputation = PendingLoad.Reputation;
		EmitSignal(SignalName.GoldChanged, Gold);
		EnemyRandomizerEnabled = PendingLoad.EnemyRandomizerEnabled;
		RandomizerSeed = PendingLoad.RandomizerSeed;

		return PendingLoad;
	}

	public SaveData PeekSave(int slot)
	{
		if (!HasSaveFile(slot))
			return null;

		PendingLoad = TryReadSaveFile(slot);
		return PendingLoad;
	}

	public string GetCharacterName(int slot)
	{
		if (!HasSaveFile(slot))
			return null;

		return TryReadSaveFile(slot)?.CharacterName;
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
