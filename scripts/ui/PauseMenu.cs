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
		if (!@event.IsActionPressed("ui_cancel"))
			return;

		bool inLevel = GetTree().CurrentScene is LevelBootstrap;
		if (!inLevel && !GetTree().Paused)
			return;

		TogglePause();
		GetViewport().SetInputAsHandled();
	}

	private void TogglePause()
	{
		bool paused = !GetTree().Paused;
		GetTree().Paused = paused;
		_panel.Visible = paused;
		if (!paused)
			_mainView.Visible = true;
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
