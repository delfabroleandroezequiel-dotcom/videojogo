using Godot;
using Metroidvania.Save;
using Metroidvania.Shared;

namespace Metroidvania.UI;

public partial class PauseMenu : CanvasLayer
{
	private Control _panel;
	private Control _mainView;
	private SettingsPanel _settings;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		_panel = GetNode<Control>("Panel");
		_panel.Visible = false;
		_mainView = GetNode<Control>("Panel/VBox");

		GetNode<Button>("Panel/VBox/ResumeButton").Pressed += TogglePause;
		GetNode<Button>("Panel/VBox/MainMenuButton").Pressed += OnMainMenuPressed;

		_settings = GetNode<SettingsPanel>("Panel/Settings");
		GetNode<Button>("Panel/VBox/SettingsButton").Pressed += OnSettingsPressed;
		_settings.Closed += () => _mainView.Visible = true;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		// "pause" (Escape / Start) opens and closes it; ui_cancel only backs out once it's open,
		// since on a gamepad ui_cancel is B, which is also roll.
		bool pausePressed = @event.IsActionPressed("pause");
		if (!pausePressed && !(_panel.Visible && @event.IsActionPressed("ui_cancel")))
			return;

		bool inLevel = GetTree().CurrentScene is LevelBootstrap;
		if (!inLevel && !GetTree().Paused)
			return;
		// Paused by someone else (quest log, inventory, dialogue, prompts): that overlay owns the screen,
		// so the pause menu doesn't open on top of it.
		if (GetTree().Paused && !_panel.Visible)
			return;

		GetViewport().SetInputAsHandled();
		// Settings open: Escape steps back inside it instead of closing the whole pause menu.
		if (_panel.Visible && _settings.Back())
			return;
		TogglePause();
	}

	private void TogglePause()
	{
		bool paused = !GetTree().Paused;
		GetTree().Paused = paused;
		_panel.Visible = paused;
		if (!paused)
		{
			// Never leave settings open underneath — reopening the pause menu must show only its list.
			_settings.HideNow();
			_mainView.Visible = true;
		}
	}

	private void OnMainMenuPressed()
	{
		GetTree().Paused = false;
		_panel.Visible = false;
		GetTree().ChangeSceneToFile(GameConfig.Instance.MainMenuScenePath);
	}

	private void OnSettingsPressed()
	{
		_mainView.Visible = false;
		_settings.Open();
	}
}
