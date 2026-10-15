using System.Collections.Generic;

namespace Metroidvania.Save;

public class SaveData
{
	// 0 = legacy saves written before versioning existed. Bump SaveManager.CurrentSaveVersion
	// and add a step to SaveManager.MigrateSaveData whenever a saved field's meaning changes.
	public int SaveVersion { get; set; }
	public string CharacterName { get; set; } = "Héroe";
	public string ScenePath { get; set; } = "res://scenes/world/Mapas/Casa1.tscn";
	public float PositionX { get; set; }
	public float PositionY { get; set; }
	public List<string> UnlockedAbilities { get; set; } = new();
	public List<string> DefeatedBosses { get; set; } = new();
	public List<string> LitSavePoints { get; set; } = new();
	public List<string> ActiveQuests { get; set; } = new();
	public List<string> CompletedQuests { get; set; } = new();
	public Dictionary<string, int> QuestProgress { get; set; } = new();
	public List<string> CollectedItems { get; set; } = new();
	public List<string> EquippedRings { get; set; } = new();
	public List<string> CollectedPickups { get; set; } = new();
	public List<string> PlayedCutscenes { get; set; } = new();
	public List<string> OpenedGates { get; set; } = new();
	// Additive: saves written before this field existed simply deserialize to an empty list.
	public List<string> BrokenWalls { get; set; } = new();
	// Additive, same as BrokenWalls: stackable item id → how many are held, and
	// "shopId|itemId" → how many of a limited shop stock have been bought.
	public Dictionary<string, int> ItemCounts { get; set; } = new();
	public Dictionary<string, int> ShopPurchases { get; set; } = new();
	public bool CompanionRecruited { get; set; }
	public int MaxHealCharges { get; set; } = 1;
	public int StoryStage { get; set; }
	public int Gold { get; set; }
	// Bastón: summon slots (max 5) — enemy scene path → stored uses (unlimited, farmable).
	public Dictionary<string, int> ShadowUses { get; set; } = new();
	public int Reputation { get; set; }
	// Additive (older saves load as 0 / empty). Ecos = the stat-leveling currency; StatLevels =
	// StatIds → levels bought. DroppedEcos* = what the last death left behind, if not picked up yet.
	public int Ecos { get; set; }
	public Dictionary<string, int> StatLevels { get; set; } = new();
	public int DroppedEcos { get; set; }
	public string DroppedEcosScene { get; set; } = "";
	public float DroppedEcosX { get; set; }
	public float DroppedEcosY { get; set; }
	// Additive: the quick bar's item ids, one per slot ("" = empty), and which slot is selected.
	public List<string> QuickSlots { get; set; } = new();
	public int SelectedQuickSlot { get; set; }

	// Decided once at New Game creation and never changed mid-save — see SaveManager.
	public bool EnemyRandomizerEnabled { get; set; }
	public int RandomizerSeed { get; set; }
}
