using Godot;

namespace Metroidvania.World;

// Floor that rides along with a rocking Node2D (e.g. a ship with RotationSway) WITHOUT rotating:
// a body the player stands on must never rotate (it launches them), so instead this only slides
// vertically by how high/low the rocking node's deck would be right under the player's feet —
// (player X - pivot X) * sin(tilt). The floor stays flat and continuous, the movement is slow and
// small (a few dozen px at the far ends of a wide ship, ~0 at its center), and it tracks the
// visual sway of RockingNodePath exactly because it reads that node's own Rotation every frame.
//
// Needs to be an AnimatableBody2D (not StaticBody2D) so the character actually rides the motion.
// Moves via local Position, never GlobalPosition. Assumes the parent isn't rotated/scaled.
// Only tracks the player: anything else standing on this floor away from the player won't line up
// with the deck.
public partial class RockingFloor : AnimatableBody2D
{
	// The node that rocks (its Rotation is what's read) — typically the ship sprite.
	[Export] public NodePath RockingNodePath;

	private Node2D _rocking;
	private Vector2 _basePosition;
	private float _baseRotation;

	public override void _Ready()
	{
		_rocking = RockingNodePath is null || RockingNodePath.IsEmpty ? null : GetNodeOrNull<Node2D>(RockingNodePath);
		if (_rocking is null)
		{
			GD.PushWarning($"{Name}: RockingNodePath doesn't point to a Node2D; the floor won't move.");
			SetPhysicsProcess(false);
			return;
		}

		_basePosition = Position;
		_baseRotation = _rocking.Rotation;
	}

	public override void _PhysicsProcess(double delta)
	{
		float tilt = _rocking.Rotation - _baseRotation;
		float pivotX = _rocking.GlobalPosition.X;
		float feetX = GetTree().GetFirstNodeInGroup("player") is Node2D player ? player.GlobalPosition.X : pivotX;

		// Positive rotation is clockwise on screen (Y down), so a point to the right of the pivot
		// moves down as tilt grows.
		Position = _basePosition + new Vector2(0f, (feetX - pivotX) * Mathf.Sin(tilt));
	}
}
