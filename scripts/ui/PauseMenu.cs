using Godot;
using Metroidvania.Save;
using Metroidvania.Shared;

namespace Metroidvania.UI;

public partial class PauseMenu : CanvasLayer
{
	private Control _panel;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		_panel = GetNode<Control>("Panel");
		_panel.Visible = false;

		GetNode<Button>("Panel/VBox/ResumeButton").Pressed += TogglePause;
		GetNode<Button>("Panel/VBox/MainMenuButton").Pressed += OnMainMenuPressed;

		GetNode<Button>("Panel/VBox/LanguageRow/EsButton").Pressed += () => LocaleManager.Instance.SetLocale("es");
		GetNode<Button>("Panel/VBox/LanguageRow/EnButton").Pressed += () => LocaleManager.Instance.SetLocale("en");
		GetNode<Button>("Panel/VBox/LanguageRow/PtButton").Pressed += () => LocaleManager.Instance.SetLocale("pt");
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
	}

	private void OnMainMenuPressed()
	{
		GetTree().Paused = false;
		_panel.Visible = false;
		GetTree().ChangeSceneToFile(GameConfig.Instance.MainMenuScenePath);
	}
}
