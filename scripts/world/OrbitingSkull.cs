using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Circles around whatever it's parented to (the boss) at a fixed radius, forming a ring the player
// physically can't walk through to reach the center. No AI, no player-seeking, no explosion —
// unlike BurningSkull, these never detonate, they're just a moving wall — but they do have their
// own Stats (see the .tscn) so the player can actually whittle the barrier down: a plain
// Hitbox.OnBodyEntered hit already finds that Stats child node and damages it same as any enemy,
// no special-casing needed here beyond queueing free on Stats.Died. AnimatableBody2D (not
// StaticBody2D) because it's repositioned every frame and still needs to correctly push a
// CharacterBody2D out of the way — a StaticBody2D moved via script doesn't interact with physics
// the same way. Moves via local Position (relative to its parent, so it automatically follows the
// boss without any explicit "track the center" code) and never touches Rotation itself, same
// reasoning as the AnimatableBody2D platform gotcha elsewhere in this project: only reposition,
// never rotate the body a character can be pushed against.
public partial class OrbitingSkull : AnimatableBody2D
{
	[Export] public float OrbitRadius = 100f;
	[Export] public float OrbitSpeed = 0.6f;
	[Export] public float StartAngle;

	private float _angle;

	public override void _Ready()
	{
		_angle = StartAngle;
		Position = new Vector2(Mathf.Cos(_angle), Mathf.Sin(_angle)) * OrbitRadius;

		Stats stats = GetNodeOrNull<Stats>("Stats");
		if (stats is not null)
			stats.Died += QueueFree;
	}

	public override void _PhysicsProcess(double delta)
	{
		_angle += OrbitSpeed * (float)delta;
		Position = new Vector2(Mathf.Cos(_angle), Mathf.Sin(_angle)) * OrbitRadius;
	}
}
