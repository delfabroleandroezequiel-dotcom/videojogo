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

		// World map (MapaPanel): built here instead of in PauseMenu.tscn, second in the list.
		_map = new MapaPanel { Name = "Mapa" };
		_panel.AddChild(_map);
		// Opened with M: backing out of the map goes straight back to the game.
		_map.Closed += () => { if (_mapDirect) TogglePause(); else _mainView.Visible = true; };
		var mapButton = new Button { Name = "BotonMapa", Text = "UI_PAUSE_MAP", CustomMinimumSize = new Vector2(280f, 54f) };
		_mainView.AddChild(mapButton);
		_mainView.MoveChild(mapButton, 1);
		mapButton.Pressed += OnMapPressed;
	}

	private MapaPanel _map;
	private bool _mapDirect;

	private void ToggleMapShortcut()
	{
		if (_panel.Visible && _map.Visible)
		{
			GetViewport().SetInputAsHandled();
			TogglePause(); // closes the map and unpauses
			return;
		}
		// Only from gameplay: not over another overlay (quest log, inventory, dialogue) or a menu.
		if (GetTree().Paused || GetTree().CurrentScene is not LevelBootstrap)
			return;
		GetViewport().SetInputAsHandled();
		TogglePause();
		OpenMap(direct: true);
	}

	private void OnMapPressed() => OpenMap(direct: false);

	private void OpenMap(bool direct)
	{
		_mapDirect = direct;
		_mainView.Visible = false;
		_map.Open();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		// "map" (M): straight to the map and back to the game, no stop in the pause list.
		if (@event.IsActionPressed("map"))
		{
			ToggleMapShortcut();
			return;
		}

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
		if (_panel.Visible && (_settings.Back() || _map.Back()))
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
			_map.HideNow();
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
