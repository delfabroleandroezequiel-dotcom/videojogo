using Godot;

namespace Metroidvania.Shared;

// Whether the current map is one of the dark ones (caves): it has a visible CanvasModulate dimmer
// than DarkThreshold (see the lighting pattern — cave-dark ≈ 0.05, dim rooms ≈ 0.5). Lights that only
// make sense in the dark (the player's aura, a campfire's wide glow) ask this instead of each map
// toggling them by hand.
public static class SceneDarkness
{
	public const float DarkThreshold = 0.45f;

	public static bool IsDark(Node context)
	{
		if (context.GetTree()?.CurrentScene is not Node scene)
			return false;
		foreach (Node node in scene.FindChildren("*", "CanvasModulate", true, false))
		{
			if (node is CanvasModulate modulate && modulate.Visible && modulate.Color.Luminance < DarkThreshold)
				return true;
		}
		return false;
	}
}
