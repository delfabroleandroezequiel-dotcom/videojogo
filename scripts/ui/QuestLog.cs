using Godot;
using Metroidvania.Quests;

namespace Metroidvania.UI;

public partial class QuestLog : CanvasLayer
{
	private Control _panel;
	private VBoxContainer _activeList;
	private VBoxContainer _completedList;
	private bool _isOpen;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;

		_panel = GetNode<Control>("Panel");
		_activeList = GetNode<VBoxContainer>("Panel/VBox/ActiveList");
		_completedList = GetNode<VBoxContainer>("Panel/VBox/CompletedList");

		_panel.Visible = false;

		QuestManager.Instance.QuestStarted += _ => RefreshIfOpen();
		QuestManager.Instance.QuestProgressUpdated += (_, _, _) => RefreshIfOpen();
		QuestManager.Instance.QuestCompleted += _ => RefreshIfOpen();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("quest_log") && !DialogueBox.Instance.IsOpen)
		{
			Toggle();
			GetViewport().SetInputAsHandled();
		}
	}

	private void Toggle()
	{
		_isOpen = !_isOpen;
		_panel.Visible = _isOpen;
		GetTree().Paused = _isOpen;

		if (_isOpen)
			Refresh();
	}

	private void RefreshIfOpen()
	{
		if (_isOpen)
			Refresh();
	}

	private void Refresh()
	{
		ClearChildren(_activeList);
		ClearChildren(_completedList);

		foreach (string questId in QuestManager.Instance.GetActiveQuestIds())
		{
			Quest quest = QuestManager.Instance.GetQuestInfo(questId);
			if (quest is null)
				continue;

			string prefix = quest.IsMainQuest ? "[Principal] " : "[Secundaria] ";
			int current = QuestManager.Instance.GetDisplayProgress(quest);
			string progress = $" ({current}/{quest.ObjectiveTarget})";

			Label label = new() { Text = $"{prefix}{quest.Title}{progress}\n  {quest.Description}", AutowrapMode = TextServer.AutowrapMode.Word };
			_activeList.AddChild(label);
		}

		foreach (string questId in QuestManager.Instance.GetCompletedQuestIds())
		{
			Quest quest = QuestManager.Instance.GetQuestInfo(questId);
			if (quest is null)
				continue;

			Label label = new() { Text = $"✓ {quest.Title}" };
			_completedList.AddChild(label);
		}
	}

	private static void ClearChildren(Node container)
	{
		foreach (Node child in container.GetChildren())
			child.QueueFree();
	}
}
