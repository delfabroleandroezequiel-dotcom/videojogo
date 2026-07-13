using System.Collections.Generic;
using System.Text.Json;
using Godot;
using Metroidvania.Save;

namespace Metroidvania.Achievements;

// Achievements are account-wide, not tied to a save slot — like Steam's own model, defeating a
// boss in one save file shouldn't un-happen when you start a fresh slot. So unlocks persist to
// their own file instead of going through SaveManager's per-slot save data.
public partial class AchievementTracker : Node
{
	private const string AchievementsFolder = "res://resources/achievements/";
	private const string UnlockedPath = "user://achievements.json";

	private readonly Dictionary<string, Achievement> _definitions = new();
	private readonly HashSet<string> _unlocked = new();

	[Signal] public delegate void AchievementUnlockedEventHandler(string achievementId);

	public override void _Ready()
	{
		LoadDefinitions();
		LoadUnlocked();
	}

	public bool IsUnlocked(string id) => _unlocked.Contains(id);
	public Achievement Get(string id) => _definitions.GetValueOrDefault(id);
	public IReadOnlyCollection<Achievement> GetAll() => _definitions.Values;
	public int UnlockedCount => _unlocked.Count;
	public int TotalCount => _definitions.Count;

	public void Unlock(string id)
	{
		if (string.IsNullOrEmpty(id) || _unlocked.Contains(id) || !_definitions.ContainsKey(id))
			return;

		_unlocked.Add(id);
		SaveUnlocked();
		EmitSignal(SignalName.AchievementUnlocked, id);
	}

	// Boss/item/story achievements aren't polled every frame — call this right after the
	// underlying state actually changes (see the SaveManager call sites), so it's a cheap
	// scan over just the still-locked definitions.
	public void CheckTriggers()
	{
		foreach (Achievement achievement in _definitions.Values)
		{
			if (IsUnlocked(achievement.Id))
				continue;

			bool met = achievement.Trigger switch
			{
				AchievementTrigger.DefeatBoss => SaveManager.Instance.IsBossDefeated(achievement.RequiredBossId),
				AchievementTrigger.CollectItem => SaveManager.Instance.HasItem(achievement.RequiredItemId),
				AchievementTrigger.StoryStage => SaveManager.Instance.StoryStage >= achievement.RequiredStoryStage,
				_ => false,
			};

			if (met)
				Unlock(achievement.Id);
		}
	}

	private void LoadDefinitions()
	{
		using DirAccess dir = DirAccess.Open(AchievementsFolder);
		if (dir is null)
			return;

		dir.ListDirBegin();
		string fileName = dir.GetNext();
		while (fileName != "")
		{
			if (fileName.EndsWith(".tres"))
			{
				Achievement achievement = GD.Load<Achievement>(AchievementsFolder + fileName);
				if (achievement is not null && !string.IsNullOrEmpty(achievement.Id))
					_definitions[achievement.Id] = achievement;
			}

			fileName = dir.GetNext();
		}

		dir.ListDirEnd();
	}

	private void SaveUnlocked()
	{
		using FileAccess file = FileAccess.Open(UnlockedPath, FileAccess.ModeFlags.Write);
		file.StoreString(JsonSerializer.Serialize(_unlocked));
	}

	private void LoadUnlocked()
	{
		if (!FileAccess.FileExists(UnlockedPath))
			return;

		using FileAccess file = FileAccess.Open(UnlockedPath, FileAccess.ModeFlags.Read);
		string[] ids = JsonSerializer.Deserialize<string[]>(file.GetAsText());
		foreach (string id in ids)
			_unlocked.Add(id);
	}
}
