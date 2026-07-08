using Godot;
using System.Collections.Generic;

namespace Metroidvania.Shared;

public partial class GameConfig : Node
{
	public static GameConfig Instance { get; private set; }

	[Export] public string DefaultStartScenePath = "res://scenes/world/Casa1.tscn";
	[Export] public string MainMenuScenePath = "res://scenes/ui/MainMenu.tscn";

	public static readonly string[] RemappableActions =
	{
		"move_left", "move_right", "move_up", "move_down", "jump", "attack", "dash", "sprint", "interact", "quest_log",
		"inventory", "heal", "companion_stance",
	};

	private static readonly Dictionary<string, Key> DefaultBindings = new()
	{
		{ "move_left", Key.A },
		{ "move_right", Key.D },
		{ "move_up", Key.W },
		{ "move_down", Key.S },
		{ "jump", Key.Space },
		{ "attack", Key.J },
		{ "dash", Key.Shift },
		{ "sprint", Key.Shift },
		{ "interact", Key.E },
		{ "quest_log", Key.L },
		{ "inventory", Key.I },
		{ "heal", Key.R },
		{ "companion_stance", Key.P },
	};

	public static readonly Vector2I[] ResolutionPresets =
	{
		new(1152, 648),
		new(1280, 720),
		new(1600, 900),
		new(1920, 1080),
	};

	private const string ConfigPath = "user://settings.cfg";
	private const string ConfigSection = "input";
	private const string DisplaySection = "display";

	public override void _Ready()
	{
		Instance = this;
		LoadBindings();
		LoadDisplaySettings();
	}

	public bool IsFullscreen => DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen;

	public Vector2I GetWindowSize() => DisplayServer.WindowGetSize();

	public void SetFullscreen(bool enabled)
	{
		DisplayServer.WindowSetMode(enabled ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);

		ConfigFile config = new();
		config.Load(ConfigPath);
		config.SetValue(DisplaySection, "fullscreen", enabled);
		config.Save(ConfigPath);
	}

	public void SetResolution(Vector2I size)
	{
		if (IsFullscreen)
			SetFullscreen(false);

		DisplayServer.WindowSetSize(size);
		CenterWindow();

		ConfigFile config = new();
		config.Load(ConfigPath);
		config.SetValue(DisplaySection, "width", size.X);
		config.SetValue(DisplaySection, "height", size.Y);
		config.Save(ConfigPath);
	}

	private static void CenterWindow()
	{
		Vector2I screenSize = DisplayServer.ScreenGetSize();
		Vector2I windowSize = DisplayServer.WindowGetSize();
		DisplayServer.WindowSetPosition((screenSize - windowSize) / 2);
	}

	private void LoadDisplaySettings()
	{
		ConfigFile config = new();
		if (config.Load(ConfigPath) != Error.Ok)
			return;

		int width = config.GetValue(DisplaySection, "width", ResolutionPresets[0].X).AsInt32();
		int height = config.GetValue(DisplaySection, "height", ResolutionPresets[0].Y).AsInt32();
		DisplayServer.WindowSetSize(new Vector2I(width, height));
		CenterWindow();

		if (config.GetValue(DisplaySection, "fullscreen", false).AsBool())
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
	}

	// project.godot binds actions by physical_keycode (layout-independent), so remaps
	// have to write the same field or a rebound key silently stops matching keypresses.
	public Key GetBinding(string action)
	{
		foreach (InputEvent inputEvent in InputMap.ActionGetEvents(action))
			if (inputEvent is InputEventKey key)
				return key.PhysicalKeycode;

		return Key.None;
	}

	public void SetBinding(string action, Key keycode)
	{
		ApplyBinding(action, keycode);

		ConfigFile config = new();
		config.Load(ConfigPath);
		config.SetValue(ConfigSection, action, (int)keycode);
		config.Save(ConfigPath);
	}

	public void ResetBindingsToDefault()
	{
		ConfigFile config = new();
		config.Load(ConfigPath);

		foreach (string action in RemappableActions)
		{
			Key keycode = DefaultBindings[action];
			ApplyBinding(action, keycode);
			config.SetValue(ConfigSection, action, (int)keycode);
		}

		config.Save(ConfigPath);
	}

	private static void ApplyBinding(string action, Key keycode)
	{
		InputMap.ActionEraseEvents(action);
		InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = keycode });
	}

	private void LoadBindings()
	{
		ConfigFile config = new();
		if (config.Load(ConfigPath) != Error.Ok)
			return;

		foreach (string action in RemappableActions)
		{
			if (!config.HasSectionKey(ConfigSection, action))
				continue;

			int keycode = config.GetValue(ConfigSection, action).AsInt32();
			ApplyBinding(action, (Key)keycode);
		}
	}
}
