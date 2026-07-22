using Godot;
using System.Collections.Generic;
using Metroidvania.Achievements;

namespace Metroidvania.Shared;

public partial class GameConfig : Node
{
	public static GameConfig Instance { get; private set; }

	[Export] public string DefaultStartScenePath = "res://scenes/world/Casa1.tscn";
	[Export] public string MainMenuScenePath = "res://scenes/ui/MainMenu.tscn";

	[Export] public PackedScene[] GroundEnemyPool = System.Array.Empty<PackedScene>();
	[Export] public PackedScene[] FlyingEnemyPool = System.Array.Empty<PackedScene>();

	public AchievementTracker Achievements { get; private set; }

	public static readonly string[] RemappableActions =
	{
		"move_left", "move_right", "move_up", "move_down", "jump", "attack", "dash", "sprint", "interact", "quest_log",
		"inventory", "heal", "companion_stance", "block", "charged_attack",
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
		{ "block", Key.K },
		{ "charged_attack", Key.H },
	};

	public static readonly Vector2I[] ResolutionPresets =
	{
		new(1152, 648),
		new(1280, 720),
		new(1600, 900),
		new(1920, 1080),
	};

	// Default cross-axis size for CorridorBlock (corridor height when Lateral, width when
	// Vertical) — a sane starting point, not a lock; CrossSize/CrossPreset override it per
	// instance for narrow squeezes, wide boss arenas, etc.
	public const float CorridorStandardCrossSize = 288f;

	// Small/Medium/Large presets for CorridorBlock.Length, and Narrow/Standard/Wide presets for
	// CorridorBlock.CrossSize — picked from a dropdown instead of guessing a pixel count each
	// time; Custom still allows a free value on both.
	public static readonly float[] CorridorLengthPresets = { 384f, 768f, 1152f };
	public static readonly float[] CorridorCrossSizePresets = { 96f, 288f, 576f };

	// World reputation ("Amor/Normal/Odio"): completing a quest nudges it up, killing an
	// ambient (non-quest) NPC nudges it down — see QuestManager.CompleteQuest and Npc.OnDied.
	// Clamped in SaveManager.AddReputation so it can't run away in either direction over a long
	// playthrough; the thresholds below decide which of an ambient NPC's Love/Hate dialogue
	// lines (if any) it uses instead of its normal one.
	public const int ReputationPerQuestCompleted = 1;
	public const int ReputationPerNpcKilled = 1;
	public const int ReputationMin = -10;
	public const int ReputationMax = 10;
	public const int ReputationLoveThreshold = 3;
	public const int ReputationHateThreshold = -3;

	private const string ConfigPath = "user://settings.cfg";
	private const string ConfigSection = "input";
	private const string DisplaySection = "display";

	public override void _Ready()
	{
		Instance = this;
		Achievements = GetNode<AchievementTracker>("Achievements");
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

	// Only the keyboard binding is user-remappable here — erasing every event for the action
	// would also wipe the gamepad button/axis bindings set up in project.godot's input map, so
	// those get collected first and re-added after the reset.
	private static void ApplyBinding(string action, Key keycode)
	{
		List<InputEvent> nonKeyEvents = new();
		foreach (InputEvent inputEvent in InputMap.ActionGetEvents(action))
			if (inputEvent is not InputEventKey)
				nonKeyEvents.Add(inputEvent);

		InputMap.ActionEraseEvents(action);
		InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = keycode });
		foreach (InputEvent preserved in nonKeyEvents)
			InputMap.ActionAddEvent(action, preserved);
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
