using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// A FlickerLight that only switches on in dark maps (SceneDarkness), for glows that would wash out
// a daylight map — e.g. CookingArea's wide campfire light, placed both in caves and in Nix.
public partial class DarkSceneFlickerLight : FlickerLight
{
	public override void _Ready()
	{
		base._Ready();
		Visible = false;
		// Deferred: the map's CanvasModulate may sit after this node in the tree.
		Callable.From(() => Visible = SceneDarkness.IsDark(this)).CallDeferred();
	}
}
