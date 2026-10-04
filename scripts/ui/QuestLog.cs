using Godot;
using Metroidvania.Quests;
using Metroidvania.Shared;

namespace Metroidvania.UI;

public partial class QuestLog : CanvasLayer
{
	private Control _panel;
	private VBoxContainer _activeList;
	private VBoxContainer _completedList;
	private Label _completedTitle;
	private Label _closeHint;
	private bool _isOpen;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;

		_panel = GetNode<Control>("Panel");
		_activeList = GetNode<VBoxContainer>("Panel/VBox/ActiveList");
		_completedList = GetNode<VBoxContainer>("Panel/VBox/CompletedList");
		_completedTitle = GetNode<Label>("Panel/VBox/CompletedTitle");
		_closeHint = GetNode<Label>("Panel/VBox/CloseHint");

		// Jekyria window: vine frame, gold title, and the lists in a scroll so a long log stays inside it.
		var box = GetNode<VBoxContainer>("Panel/VBox");
		Label activeTitle = GetNode<Label>("Panel/VBox/ActiveTitle");
		JekyriaFrame.Dress(_panel, box, GetNode<Label>("Panel/VBox/Title"), new Vector2(600f, 440f));
		JekyriaFrame.StyleHeader(activeTitle);
		JekyriaFrame.StyleHeader(_completedTitle);
		JekyriaFrame.WrapInScroll(box, 1, activeTitle, _activeList, _completedTitle, _completedList);

		_panel.Visible = false;

		QuestManager.Instance.QuestStarted += _ => RefreshIfOpen();
		QuestManager.Instance.QuestProgressUpdated += (_, _, _) => RefreshIfOpen();
		QuestManager.Instance.QuestCompleted += _ => RefreshIfOpen();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		// Opens only during free gameplay: never on top of the pause menu/settings (e.g. while rebinding
		// keys), a dialogue or another overlay — they all pause the tree — nor outside a level. Closing
		// is always allowed.
		if (!_isOpen && (GetTree().Paused || GetTree().CurrentScene is not Metroidvania.Save.LevelBootstrap))
			return;
		// Escape also closes it (the pause menu stays out of the way while it's open).
		if (_isOpen && @event.IsActionPressed("ui_cancel"))
		{
			Toggle();
			GetViewport().SetInputAsHandled();
			return;
		}
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
		// The hint shows the key actually bound to the log (it can be remapped).
		_closeHint.Text = string.Format(TranslationServer.Translate("UI_QUESTLOG_CLOSE_HINT"),
			OS.GetKeycodeString(GameConfig.Instance.GetBinding("quest_log")));

		foreach (string questId in QuestManager.Instance.GetActiveQuestIds())
		{
			Quest quest = QuestManager.Instance.GetQuestInfo(questId);
			if (quest is null)
				continue;

			string prefix = TranslationServer.Translate(quest.IsMainQuest ? "UI_QUESTLOG_MAIN_PREFIX" : "UI_QUESTLOG_SIDE_PREFIX");
			int current = QuestManager.Instance.GetDisplayProgress(quest);
			string progress = string.Format(TranslationServer.Translate("UI_QUESTLOG_PROGRESS"), current, quest.ObjectiveTarget);
			string title = TranslationServer.Translate(quest.Title);
			string description = TranslationServer.Translate(quest.Description);

			Label label = new() { Text = $"{prefix}{title}{progress}\n  {description}", AutowrapMode = TextServer.AutowrapMode.Word };
			_activeList.AddChild(label);
		}

		foreach (string questId in QuestManager.Instance.GetCompletedQuestIds())
		{
			Quest quest = QuestManager.Instance.GetQuestInfo(questId);
			if (quest is null)
				continue;

			Label label = new() { Text = string.Format(TranslationServer.Translate("UI_QUESTLOG_COMPLETED_ENTRY"), TranslationServer.Translate(quest.Title)) };
			_completedList.AddChild(label);
		}

		// QueueFree'd rows are still children this frame, so count the live ones.
		if (CountLive(_activeList) == 0)
			_activeList.AddChild(new Label { Text = "UI_QUESTLOG_EMPTY", Modulate = new Color(0.7f, 0.7f, 0.7f) });
		_completedTitle.Visible = CountLive(_completedList) > 0;
	}

	private static int CountLive(Node container)
	{
		int count = 0;
		foreach (Node child in container.GetChildren())
			if (!child.IsQueuedForDeletion())
				count++;
		return count;
	}

	private static void ClearChildren(Node container)
	{
		foreach (Node child in container.GetChildren())
			child.QueueFree();
	}
}
