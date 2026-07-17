using System.Collections.Generic;

namespace Metroidvania.Save;

public class SaveData
{
	// 0 = legacy saves written before versioning existed. Bump SaveManager.CurrentSaveVersion
	// and add a step to SaveManager.MigrateSaveData whenever a saved field's meaning changes.
	public int SaveVersion { get; set; }
	public string CharacterName { get; set; } = "Héroe";
	public string ScenePath { get; set; } = "res://scenes/world/Casa1.tscn";
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
	public int MaxHealCharges { get; set; } = 1;
	public int StoryStage { get; set; }
	public int Gold { get; set; }

	// Decided once at New Game creation and never changed mid-save — see SaveManager.
	public bool EnemyRandomizerEnabled { get; set; }
	public int RandomizerSeed { get; set; }
}
