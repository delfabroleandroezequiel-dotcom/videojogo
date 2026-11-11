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

		BuildToast();
		QuestManager.Instance.QuestStarted += id => Toast("QUEST_TOAST_NEW", id);
		QuestManager.Instance.QuestProgressUpdated += (id, current, target) =>
		{
			// The last step is announced by QuestCompleted, right after this.
			if (current < target)
				Toast("QUEST_TOAST_PROGRESS", id, current, target);
		};
		QuestManager.Instance.QuestCompleted += id => Toast("QUEST_TOAST_DONE", id);
	}

	// ---- Toast: a short line at the top of the screen when a quest starts, advances or ends, so the
	// player knows the errand counted without opening the log. Queued, one at a time.
	private Label _toast;
	private readonly System.Collections.Generic.Queue<string> _toastQueue = new();
	private bool _toastBusy;

	private void BuildToast()
	{
		_toast = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			AnchorLeft = 0f, AnchorRight = 1f, OffsetTop = 64f, OffsetBottom = 110f,
			Modulate = new Color(1f, 1f, 1f, 0f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_toast.AddThemeFontOverride("font", GD.Load<Font>("res://resources/ui/JekyriaFont.res"));
		_toast.AddThemeFontSizeOverride("font_size", 22);
		_toast.AddThemeColorOverride("font_color", new Color(0.95f, 0.82f, 0.45f));
		_toast.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.9f));
		_toast.AddThemeConstantOverride("outline_size", 6);
		AddChild(_toast);
	}

	private void Toast(string formatKey, string questId, int current = 0, int target = 0)
	{
		Quest quest = QuestManager.Instance.GetQuestInfo(questId);
		if (quest is null || string.IsNullOrEmpty(quest.Title))
			return;
		string title = TranslationServer.Translate(quest.Title);
		_toastQueue.Enqueue(string.Format(TranslationServer.Translate(formatKey), title, current, target));
		if (!_toastBusy)
			_ = PlayToasts();
	}

	private async System.Threading.Tasks.Task PlayToasts()
	{
		_toastBusy = true;
		while (_toastQueue.Count > 0 && IsInsideTree())
		{
			_toast.Text = _toastQueue.Dequeue();
			Tween tween = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
			tween.TweenProperty(_toast, "modulate:a", 1f, 0.35f);
			tween.TweenInterval(2.4f);
			tween.TweenProperty(_toast, "modulate:a", 0f, 0.6f);
			await ToSignal(tween, Tween.SignalName.Finished);
		}
		_toastBusy = false;
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
