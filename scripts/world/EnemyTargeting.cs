using Godot;

namespace Metroidvania.World;

// Who enemy AI goes after. Normally the player's body (the "player" group), but while the player is
// steering a shadow (the berserker or a stolen shadow) that shadow is the target instead — the world
// otherwise still only sees the body ("player" group untouched: transitions, triggers, chests, NPCs
// keep reacting to the real player only). Player sets/clears ControlledShadow.
public static class EnemyTargeting
{
	public static Node2D ControlledShadow;

	public static Node2D Current(SceneTree tree)
	{
		if (ControlledShadow is not null && GodotObject.IsInstanceValid(ControlledShadow) && ControlledShadow.IsInsideTree()
			&& !ControlledShadow.IsQueuedForDeletion())
			return ControlledShadow;
		return tree.GetFirstNodeInGroup("player") as Node2D;
	}
}
