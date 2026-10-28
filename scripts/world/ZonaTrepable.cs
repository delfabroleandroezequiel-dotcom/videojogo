using Godot;

namespace Metroidvania.World;

// A climbable surface on a moving body (a boss's torso, arm, head…): Shadow of the Colossus-style
// grip. Make it a child of the part it covers and give it CollisionPolygon2D children outlining the
// climbable area in that part's own pixels — it then moves, rotates and scales with the part, so
// the same zone works at any boss size.
//
// The player grabs it by holding up while overlapping it, then moves freely over it (see Player's
// grip state). The grip position is stored in this zone's local space each frame, so whatever the
// part does (a bending elbow, breathing, a shake) carries the player along without physics — a
// rotating physics body would launch them instead.
public partial class ZonaTrepable : Area2D
{
	// Extra stamina drain per second on top of the player's own grip cost (e.g. a slimy or
	// shaking part). Negative makes a part a resting spot.
	[Export] public float ExtraStaminaPerSecond;
	// Extra drain per second only while the player holds still on it — set at runtime by a boss
	// shaking you off, so climbing on is the way to hang on.
	[Export] public float ExtraStaminaWhenStill;
	// Multiplier on the player's climb speed while on this zone.
	[Export] public float ClimbSpeedMultiplier = 1f;

	public override void _Ready()
	{
		CollisionLayer = 0;
		CollisionMask = 2;
		Monitorable = false;
		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
	}

	// Whether a world point lies on this zone's climbable surface.
	public bool ContainsGlobalPoint(Vector2 globalPoint)
	{
		foreach (Node child in GetChildren())
		{
			if (child is not CollisionPolygon2D poly || poly.Disabled)
				continue;
			Vector2 local = poly.GlobalTransform.AffineInverse() * globalPoint;
			if (Geometry2D.IsPointInPolygon(local, poly.Polygon))
				return true;
		}
		return false;
	}

	private void OnBodyEntered(Node2D body)
	{
		if (body is Metroidvania.Player.Player player)
			player.EnterClimbZone(this);
	}

	private void OnBodyExited(Node2D body)
	{
		if (body is Metroidvania.Player.Player player)
			player.ExitClimbZone(this);
	}
}
