using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// A row of spectral eruptions marching along the floor away from an Oscuro (GhostWarrior3's
// charged erupción, GhostWarrior4's crescent follow-up): each tile flickers a warning crack, then
// bursts into cyan spikes that hurt the player standing in them. Drawn with GroundSlamFx in the
// faction's cyan, like the rest of the neon kit. Ground-bound, so jumping or rolling clear is the
// answer; the damage still goes through Stats, so a timed parry stops a tile like any hit.
public static class OscuroEruption
{
	public static readonly GroundSlamFx.Palette Palette = new()
	{
		Hot = new Color(0.2f, 0.95f, 1f, 1f),
		Core = new Color(0.85f, 1f, 1f, 1f),
		Rock = new Color(0.05f, 0.18f, 0.22f, 1f),
		Dust = new Color(0.3f, 0.8f, 0.9f, 0.45f),
		Light = new Color(0.25f, 0.9f, 1f, 1f),
	};

	// firstGround: the floor point of the first tile; direction: ±1 along X.
	public static async Task Run(Node2D owner, Stats ownerStats, Vector2 firstGround, float direction, int count,
		float spacing, float interval, Vector2 tileSize, float warning, int damage)
	{
		for (int i = 0; i < count; i++)
		{
			if (!GodotObject.IsInstanceValid(owner) || !owner.IsInsideTree())
				return;
			Vector2 ground = firstGround + new Vector2(direction * spacing * i, 0f);
			_ = Tile(owner, ownerStats, ground, tileSize, warning, damage);
			await owner.ToSignal(owner.GetTree().CreateTimer(interval), SceneTreeTimer.SignalName.Timeout);
		}
	}

	private static async Task Tile(Node2D owner, Stats ownerStats, Vector2 ground, Vector2 size, float warning, int damage)
	{
		GroundSlamFx.WarningCrack(owner, ground, size.X, warning, Palette);
		await owner.ToSignal(owner.GetTree().CreateTimer(warning), SceneTreeTimer.SignalName.Timeout);
		if (!GodotObject.IsInstanceValid(owner) || !owner.IsInsideTree() || ownerStats.CurrentHealth <= 0)
			return;
		GroundSlamFx.EarthSpikes(owner, ground, size, 0.25f, Palette);

		var query = new PhysicsShapeQueryParameters2D
		{
			Shape = new RectangleShape2D { Size = size },
			Transform = new Transform2D(0f, ground - new Vector2(0f, size.Y * 0.5f)),
			CollisionMask = 2,
			CollideWithBodies = true,
		};
		foreach (var hit in owner.GetWorld2D().DirectSpaceState.IntersectShape(query, 4))
		{
			if (hit["collider"].AsGodotObject() is Node2D body && body.IsInGroup("player")
				&& body.GetNodeOrNull<Stats>("Stats") is { } stats)
				stats.TakeDamage(damage);
		}
	}
}
