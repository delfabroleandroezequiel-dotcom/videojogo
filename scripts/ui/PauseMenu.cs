using Godot;
using Metroidvania.Save;

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
		GetTree().ChangeSceneToFile("res://scenes/ui/MainMenu.tscn");
	}
}
