using Godot;
using Metroidvania.Save;
using Metroidvania.Shared;
using System.Collections.Generic;

namespace Metroidvania.UI;

public partial class PauseMenu : CanvasLayer
{
	private static readonly Dictionary<string, string> ActionLabelKeys = new()
	{
		{ "move_left", "UI_ACTION_MOVE_LEFT" },
		{ "move_right", "UI_ACTION_MOVE_RIGHT" },
		{ "move_up", "UI_ACTION_MOVE_UP" },
		{ "move_down", "UI_ACTION_MOVE_DOWN" },
		{ "jump", "UI_ACTION_JUMP" },
		{ "attack", "UI_ACTION_ATTACK" },
		{ "dash", "UI_ACTION_DASH" },
		{ "sprint", "UI_ACTION_SPRINT" },
		{ "interact", "UI_ACTION_INTERACT" },
		{ "quest_log", "UI_ACTION_QUEST_LOG" },
		{ "inventory", "UI_ACTION_INVENTORY" },
		{ "heal", "UI_ACTION_HEAL" },
		{ "companion_stance", "UI_ACTION_COMPANION_STANCE" },
		{ "block", "UI_ACTION_BLOCK" },
		{ "charged_attack", "UI_ACTION_CHARGED_ATTACK" },
	};

	private Control _panel;
	private Control _mainView;
	private Control _controlsPanel;
	private VBoxContainer _bindingList;
	private readonly Dictionary<string, Button> _bindingButtons = new();
	private string _listeningAction;

	private Control _displayPanel;
	private VBoxContainer _resolutionList;
	private Button _fullscreenButton;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		_panel = GetNode<Control>("Panel");
		_panel.Visible = false;
		_mainView = GetNode<Control>("Panel/VBox");

		GetNode<Button>("Panel/VBox/ResumeButton").Pressed += TogglePause;
		GetNode<Button>("Panel/VBox/MainMenuButton").Pressed += OnMainMenuPressed;
		GetNode<Button>("Panel/VBox/ControlsButton").Pressed += OpenControlsPanel;
		GetNode<Button>("Panel/VBox/DisplayButton").Pressed += OpenDisplayPanel;

		GetNode<Button>("Panel/VBox/LanguageRow/EsButton").Pressed += () => LocaleManager.Instance.SetLocale("es");
		GetNode<Button>("Panel/VBox/LanguageRow/EnButton").Pressed += () => LocaleManager.Instance.SetLocale("en");
		GetNode<Button>("Panel/VBox/LanguageRow/PtButton").Pressed += () => LocaleManager.Instance.SetLocale("pt");

		_controlsPanel = GetNode<Control>("Panel/ControlsPanel");
		_bindingList = GetNode<VBoxContainer>("Panel/ControlsPanel/CVBox/List");
		GetNode<Button>("Panel/ControlsPanel/CVBox/ResetButton").Pressed += OnResetBindings;
		GetNode<Button>("Panel/ControlsPanel/CVBox/BackButton").Pressed += CloseSubPanels;

		_displayPanel = GetNode<Control>("Panel/DisplayPanel");
		_resolutionList = GetNode<VBoxContainer>("Panel/DisplayPanel/DVBox/ResolutionList");
		_fullscreenButton = GetNode<Button>("Panel/DisplayPanel/DVBox/FullscreenButton");
		_fullscreenButton.Pressed += OnFullscreenToggled;
		GetNode<Button>("Panel/DisplayPanel/DVBox/BackButton").Pressed += CloseSubPanels;

		BuildBindingRows();
		BuildResolutionRows();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_listeningAction is not null)
		{
			if (@event is InputEventKey { Pressed: true, Echo: false } keyEvent)
			{
				GetViewport().SetInputAsHandled();
				if (keyEvent.PhysicalKeycode != Key.Escape)
					GameConfig.Instance.SetBinding(_listeningAction, keyEvent.PhysicalKeycode);

				StopListening();
				RefreshBindingLabels();
			}
			return;
		}

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
			CloseSubPanels();
	}

	private void OnMainMenuPressed()
	{
		GetTree().Paused = false;
		_panel.Visible = false;
		GetTree().ChangeSceneToFile(GameConfig.Instance.MainMenuScenePath);
	}

	private void OpenControlsPanel()
	{
		_mainView.Visible = false;
		_controlsPanel.Visible = true;
		RefreshBindingLabels();
	}

	private void OpenDisplayPanel()
	{
		_mainView.Visible = false;
		_displayPanel.Visible = true;
		RefreshFullscreenLabel();
	}

	private void CloseSubPanels()
	{
		StopListening();
		_controlsPanel.Visible = false;
		_displayPanel.Visible = false;
		_mainView.Visible = true;
	}

	private void OnResetBindings()
	{
		GameConfig.Instance.ResetBindingsToDefault();
		RefreshBindingLabels();
	}

	private void OnFullscreenToggled()
	{
		GameConfig.Instance.SetFullscreen(!GameConfig.Instance.IsFullscreen);
		RefreshFullscreenLabel();
	}

	private void RefreshFullscreenLabel()
	{
		_fullscreenButton.Text = TranslationServer.Translate(
			GameConfig.Instance.IsFullscreen ? "UI_DISPLAY_FULLSCREEN_ON" : "UI_DISPLAY_FULLSCREEN_OFF");
	}

	private void BuildResolutionRows()
	{
		foreach (Vector2I resolution in GameConfig.ResolutionPresets)
		{
			var button = new Button
			{
				Text = $"{resolution.X} x {resolution.Y}",
				CustomMinimumSize = new Vector2(0, 28),
			};
			button.Pressed += () => GameConfig.Instance.SetResolution(resolution);
			_resolutionList.AddChild(button);
		}
	}

	private void BuildBindingRows()
	{
		foreach (string action in GameConfig.RemappableActions)
		{
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 12);

			var label = new Label
			{
				Text = ActionLabelKeys[action],
				CustomMinimumSize = new Vector2(140, 0),
				AutowrapMode = TextServer.AutowrapMode.Off,
			};

			var button = new Button { CustomMinimumSize = new Vector2(90, 28) };
			button.Pressed += () => StartListening(action, button);

			row.AddChild(label);
			row.AddChild(button);
			_bindingList.AddChild(row);
			_bindingButtons[action] = button;
		}
	}

	private void StartListening(string action, Button button)
	{
		StopListening();
		RefreshBindingLabels();
		_listeningAction = action;
		button.Text = TranslationServer.Translate("UI_CONTROLS_PRESS_KEY");
	}

	private void StopListening()
	{
		_listeningAction = null;
	}

	private void RefreshBindingLabels()
	{
		foreach (KeyValuePair<string, Button> entry in _bindingButtons)
			entry.Value.Text = OS.GetKeycodeString(GameConfig.Instance.GetBinding(entry.Key));
	}
}
