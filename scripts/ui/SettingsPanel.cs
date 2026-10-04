using Godot;
using Metroidvania.Save;
using Metroidvania.Shared;
using System.Collections.Generic;

namespace Metroidvania.UI;

// Controls/display/language settings, shared by MainMenu and PauseMenu — one place to edit
// this UI instead of two copies drifting apart. Host just calls Open()/Close() and listens
// for Closed to know when to bring its own view back.
public partial class SettingsPanel : Control
{
	// Row label = "UI_ACTION_" + the action name in caps (e.g. spell_tornado → UI_ACTION_SPELL_TORNADO), so a new
	// remappable action only needs its entry in GameConfig.RemappableActions plus that string key.
	private static string ActionLabelKey(string action) => "UI_ACTION_" + action.ToUpperInvariant();

	[Signal] public delegate void ClosedEventHandler();

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
		Visible = false;
		_mainView = GetNode<Control>("MainView");

		GetNode<Button>("MainView/ControlsButton").Pressed += OpenControlsPanel;
		GetNode<Button>("MainView/DisplayButton").Pressed += OpenDisplayPanel;
		GetNode<Button>("MainView/CloseButton").Pressed += Close;

		GetNode<Button>("MainView/LanguageRow/EsButton").Pressed += () => LocaleManager.Instance.SetLocale("es");
		GetNode<Button>("MainView/LanguageRow/EnButton").Pressed += () => LocaleManager.Instance.SetLocale("en");
		GetNode<Button>("MainView/LanguageRow/PtButton").Pressed += () => LocaleManager.Instance.SetLocale("pt");

		_controlsPanel = GetNode<Control>("ControlsPanel");
		_bindingList = GetNode<VBoxContainer>("ControlsPanel/CVBox/ListScroll/List");
		GetNode<Button>("ControlsPanel/CVBox/ResetButton").Pressed += OnResetBindings;
		GetNode<Button>("ControlsPanel/CVBox/BackButton").Pressed += CloseSubPanels;

		_displayPanel = GetNode<Control>("DisplayPanel");
		_resolutionList = GetNode<VBoxContainer>("DisplayPanel/DVBox/ResolutionList");
		_fullscreenButton = GetNode<Button>("DisplayPanel/DVBox/FullscreenButton");
		_fullscreenButton.Pressed += OnFullscreenToggled;
		GetNode<Button>("DisplayPanel/DVBox/BackButton").Pressed += CloseSubPanels;

		BuildBindingRows();
		BuildResolutionRows();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_listeningAction is null || !Visible)
			return;

		if (@event is InputEventKey { Pressed: true, Echo: false } keyEvent)
		{
			GetViewport().SetInputAsHandled();
			if (keyEvent.PhysicalKeycode != Key.Escape)
				GameConfig.Instance.SetBinding(_listeningAction, keyEvent.PhysicalKeycode);

			StopListening();
			RefreshBindingLabels();
		}
	}

	public void Open()
	{
		Visible = true;
		CloseSubPanels();
	}

	private void Close()
	{
		Visible = false;
		EmitSignal(SignalName.Closed);
	}

	// Escape/back: a sub-panel (Controls, Display) goes back to the settings list, the list closes the
	// panel. Returns false if the panel wasn't open (the caller handles the key itself).
	public bool Back()
	{
		if (!Visible)
			return false;
		if (_controlsPanel.Visible || _displayPanel.Visible)
			CloseSubPanels();
		else
			Close();
		return true;
	}

	// Closes everything without signalling (e.g. the pause menu being dismissed while it's open).
	public void HideNow()
	{
		CloseSubPanels();
		Visible = false;
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
				Text = ActionLabelKey(action),
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
