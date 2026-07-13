using Godot;

namespace Metroidvania.Achievements;

public enum AchievementTrigger
{
	Manual,
	DefeatBoss,
	CollectItem,
	StoryStage,
}

[GlobalClass]
public partial class Achievement : Resource
{
	[Export] public string Id = "";
	[Export] public string Title = "";
	[Export] public string Description = "";
	[Export] public Texture2D Icon;
	[Export] public bool Hidden;

	// Matches the API name configured for this achievement in the Steamworks partner dashboard.
	// Unused until Steam integration (GodotSteam or similar) is added, but every achievement
	// needs one before that day, so it lives on the definition from the start.
	[Export] public string SteamApiName = "";

	[Export] public AchievementTrigger Trigger = AchievementTrigger.Manual;
	[Export] public string RequiredBossId = "";
	[Export] public string RequiredItemId = "";
	[Export] public int RequiredStoryStage;
}
