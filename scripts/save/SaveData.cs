using System.Collections.Generic;

namespace Metroidvania.Save;

public class SaveData
{
	public string CharacterName { get; set; } = "Héroe";
	public string ScenePath { get; set; } = "res://scenes/world/TestLevel.tscn";
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
}
