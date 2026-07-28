using Godot;

namespace Metroidvania.World;

// Swimmable water zone: same "Area2D detects the player, calls a public Enter/Exit method on
// Player" pattern Ladder.cs and Rope.cs already use for their own traversal zones — Player owns
// the actual swim state/physics, this just reports when the player is inside.
// CreateArea is a static factory (same idea as Hazard.CreateArea) since this is meant to be
// spawned by a water piece's own Rebuild() to exactly match its visual bounds (see
// ProceduralWater.IsSwimmable), not hand-placed/sized independently the way Ladder is.
// Detects Player.SwimSensor (a small Area2D near the player's vertical center) instead of the
// player's full CharacterBody2D shape — that shape's bottom edge is at the player's feet, so
// body-based detection triggered swimming the instant a toe touched the surface, freezing the
// player's fall right there (swim movement overrides gravity) and reading as walking on water.
// Since Water has no solid collision, the player free-falls through it under gravity until the
// higher sensor reaches the surface, so they're already half-submerged by the time swim engages.
public partial class Water : Area2D
{
	public static Water CreateArea(Node2D parent, Vector2 size)
	{
		var water = new Water
		{
			Name = "WaterZone",
			CollisionLayer = 0,
			CollisionMask = 2,
		};
		parent.AddChild(water);
		water.Owner = parent;

		var shape = new CollisionShape2D
		{
			Name = "CollisionShape2D",
			Position = size / 2f,
			Shape = new RectangleShape2D { Size = size },
		};
		water.AddChild(shape);
		shape.Owner = parent;

		water.AreaEntered += water.OnAreaEntered;
		water.AreaExited += water.OnAreaExited;
		return water;
	}

	private void OnAreaEntered(Area2D area)
	{
		if (area.GetParent() is Metroidvania.Player.Player player)
		{
			player.EnterWater(this);
			WaterSplash.SpawnAt(this, area.GlobalPosition);
		}
	}

	private void OnAreaExited(Area2D area)
	{
		if (area.GetParent() is Metroidvania.Player.Player player)
			player.ExitWater(this);
	}
}
