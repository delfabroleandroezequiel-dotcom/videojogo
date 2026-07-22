using Godot;

namespace Metroidvania.World;

// Runtime helper shared by Elevator and MovingPlatform: both let a Switches export spawn an
// interactive Lever as a sibling (not a child, so it doesn't ride along with whatever it's
// controlling), already wired to a no-argument callback — no manual signal-connecting in the
// editor needed.
public static class LeverSpawner
{
	private const string LeverScenePath = "res://scenes/world/Lever.tscn";

	public static void SpawnAt(Node parent, Vector2 position, Lever.ActivatedEventHandler onActivated)
	{
		PackedScene leverScene = GD.Load<PackedScene>(LeverScenePath);
		Lever lever = leverScene.Instantiate<Lever>();
		lever.Position = position;
		parent.AddChild(lever);
		lever.Activated += onActivated;
	}
}
