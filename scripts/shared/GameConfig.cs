using Godot;

namespace Metroidvania.Shared;

public partial class GameConfig : Node
{
	public static GameConfig Instance { get; private set; }

	[Export] public string DefaultStartScenePath = "res://scenes/world/MainLevel.tscn";
	[Export] public string MainMenuScenePath = "res://scenes/ui/MainMenu.tscn";

	public override void _Ready()
	{
		Instance = this;
	}
}
