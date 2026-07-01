using System.Collections.Generic;
using Godot;
using Metroidvania.Save;

namespace Metroidvania.Quests;

public partial class QuestManager : Node
{
	public static QuestManager Instance { get; private set; }

	[Signal] public delegate void QuestStartedEventHandler(string questId);
	[Signal] public delegate void QuestProgressUpdatedEventHandler(string questId, int current, int target);
	[Signal] public delegate void QuestCompletedEventHandler(string questId);

	private readonly Dictionary<string, Quest> _registry = new();
	private readonly HashSet<string> _activeQuests = new();
	private readonly HashSet<string> _completedQuests = new();
	private readonly Dictionary<string, int> _progress = new();

	public override void _Ready()
	{
		Instance = this;
	}

	public void RegisterQuest(Quest quest)
	{
		if (quest is null || string.IsNullOrEmpty(quest.Id))
			return;

		_registry[quest.Id] = quest;
	}

	public bool IsActive(string questId) => _activeQuests.Contains(questId);
	public bool IsCompleted(string questId) => _completedQuests.Contains(questId);
	public int GetProgress(string questId) => _progress.GetValueOrDefault(questId, 0);
	public Quest GetQuestInfo(string questId) => _registry.GetValueOrDefault(questId);

	public IReadOnlyCollection<string> GetActiveQuestIds() => _activeQuests;
	public IReadOnlyCollection<string> GetCompletedQuestIds() => _completedQuests;

	public void StartQuest(Quest quest)
	{
		if (quest is null || string.IsNullOrEmpty(quest.Id))
			return;

		RegisterQuest(quest);
		if (_activeQuests.Contains(quest.Id) || _completedQuests.Contains(quest.Id))
			return;

		_activeQuests.Add(quest.Id);
		_progress[quest.Id] = 0;
		EmitSignal(SignalName.QuestStarted, quest.Id);
	}

	public void AddProgress(string questId, int amount = 1)
	{
		if (!_activeQuests.Contains(questId))
			return;

		Quest quest = _registry.GetValueOrDefault(questId);
		int target = quest?.ObjectiveTarget ?? 1;
		int current = Mathf.Min(target, GetProgress(questId) + amount);
		_progress[questId] = current;
		EmitSignal(SignalName.QuestProgressUpdated, questId, current, target);

		if (current >= target)
			CompleteQuest(questId);
	}

	public bool IsObjectiveMet(Quest quest)
	{
		if (quest is null)
			return false;

		return quest.ObjectiveKind switch
		{
			QuestObjectiveKind.DefeatBoss => SaveManager.Instance.IsBossDefeated(quest.RequiredBossId),
			QuestObjectiveKind.CollectItem => SaveManager.Instance.HasItem(quest.RequiredItemId),
			QuestObjectiveKind.DefeatEnemyCount => SaveManager.Instance.GetDefeatedCommonEnemyCount() >= quest.ObjectiveTarget,
			_ => GetProgress(quest.Id) >= quest.ObjectiveTarget,
		};
	}

	public int GetDisplayProgress(Quest quest)
	{
		if (quest is null)
			return 0;

		return quest.ObjectiveKind switch
		{
			QuestObjectiveKind.DefeatBoss => SaveManager.Instance.IsBossDefeated(quest.RequiredBossId) ? quest.ObjectiveTarget : 0,
			QuestObjectiveKind.CollectItem => SaveManager.Instance.HasItem(quest.RequiredItemId) ? quest.ObjectiveTarget : 0,
			QuestObjectiveKind.DefeatEnemyCount => Mathf.Min(quest.ObjectiveTarget, SaveManager.Instance.GetDefeatedCommonEnemyCount()),
			_ => GetProgress(quest.Id),
		};
	}

	public void CompleteQuest(string questId)
	{
		if (!_activeQuests.Contains(questId) || _completedQuests.Contains(questId))
			return;

		_activeQuests.Remove(questId);
		_completedQuests.Add(questId);
		EmitSignal(SignalName.QuestCompleted, questId);
	}

	public void RestoreState(IEnumerable<string> activeIds, IEnumerable<string> completedIds, Dictionary<string, int> progress)
	{
		_activeQuests.Clear();
		_completedQuests.Clear();
		_progress.Clear();

		foreach (string id in activeIds)
			_activeQuests.Add(id);
		foreach (string id in completedIds)
			_completedQuests.Add(id);
		foreach (KeyValuePair<string, int> entry in progress)
			_progress[entry.Key] = entry.Value;
	}

	public Dictionary<string, int> SnapshotProgress() => new(_progress);

	public void ResetProgressState()
	{
		_activeQuests.Clear();
		_completedQuests.Clear();
		_progress.Clear();
	}
}
