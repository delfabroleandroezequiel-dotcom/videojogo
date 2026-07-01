using Godot;

namespace Metroidvania.Quests;

public enum QuestObjectiveKind
{
	Manual,
	DefeatBoss,
	CollectItem,
	DefeatEnemyCount,
}

[GlobalClass]
public partial class Quest : Resource
{
	[Export] public string Id = "";
	[Export] public string Title = "";
	[Export] public string Description = "";
	[Export] public bool IsMainQuest;
	[Export] public int ObjectiveTarget = 1;
	[Export] public string ObjectiveLabel = "";
	[Export] public string RewardText = "";

	[Export] public QuestObjectiveKind ObjectiveKind = QuestObjectiveKind.Manual;
	[Export] public string RequiredBossId = "";
	[Export] public string RequiredItemId = "";
}
