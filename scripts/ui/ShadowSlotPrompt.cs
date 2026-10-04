using System;
using Godot;
using Metroidvania.Save;

namespace Metroidvania.UI;

// Bastón slot picker (pauses the game): lists the 5 summon slots and lets the player discard one.
// Used when all slots are full and a new enemy's essence is taken (discard one to make room for it),
// and for discarding a slot by hand. Built in code and freed when closed — not an autoload.
public partial class ShadowSlotPrompt : CanvasLayer
{
	private string _title = "";
	private string _cancelText = "";
	private Action<string> _onChosen; // discarded key, or null if cancelled

	public static void Open(Node context, string title, string cancelText, Action<string> onChosen)
	{
		var prompt = new ShadowSlotPrompt { _title = title, _cancelText = cancelText, _onChosen = onChosen };
		context.GetTree().Root.AddChild(prompt);
	}

	public override void _Ready()
	{
		Layer = 20;
		ProcessMode = ProcessModeEnum.Always;
		GetTree().Paused = true;

		var dim = new ColorRect { Color = new Color(0f, 0f, 0.03f, 0.6f), MouseFilter = Control.MouseFilterEnum.Stop };
		dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(dim);

		var box = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		box.SetAnchorsPreset(Control.LayoutPreset.Center);
		box.AddThemeConstantOverride("separation", 8);
		box.GrowHorizontal = Control.GrowDirection.Both;
		box.GrowVertical = Control.GrowDirection.Both;
		AddChild(box);

		box.AddChild(new Label { Text = _title, HorizontalAlignment = HorizontalAlignment.Center });
		Button first = null;
		foreach (string key in SaveManager.Instance.GetShadowSlotKeys())
		{
			var button = new Button
			{
				Text = string.Format(TranslationServer.Translate("UI_SHADOW_SLOT_ENTRY"), ShadowNames.DisplayName(key), SaveManager.Instance.GetShadowUses(key)),
				CustomMinimumSize = new Vector2(260f, 34f),
			};
			button.Pressed += () => Close(key);
			box.AddChild(button);
			first ??= button;
		}
		var cancel = new Button { Text = _cancelText, CustomMinimumSize = new Vector2(260f, 34f) };
		cancel.Pressed += () => Close(null);
		box.AddChild(cancel);
		(first ?? cancel).CallDeferred(Control.MethodName.GrabFocus);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_cancel"))
		{
			GetViewport().SetInputAsHandled();
			Close(null);
		}
	}

	private void Close(string discardedKey)
	{
		GetTree().Paused = false;
		if (discardedKey is not null)
			SaveManager.Instance.DiscardShadowSlot(discardedKey);
		_onChosen?.Invoke(discardedKey);
		QueueFree();
	}
}

// Display names for Bastón slots: an "ENEMY_<SCENE NAME>" translation key if one exists, else the
// scene's file name.
public static class ShadowNames
{
	public static string DisplayName(string enemyScenePath)
	{
		string baseName = enemyScenePath.GetFile().GetBaseName();
		string key = "ENEMY_" + baseName.ToUpperInvariant();
		string translated = TranslationServer.Translate(key);
		return translated != key ? translated : baseName;
	}
}
