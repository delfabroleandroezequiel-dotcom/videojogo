using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// Points the held player's camera at a target (blend Weight: 1 = centred on it) with a zoom;
// an empty Target brings it back to the player.
[GlobalClass]
public partial class StepCamera : CinematicStep
{
	[Export] public NodePath Target;
	[Export] public float Weight = 1f;
	[Export] public float Zoom = 1.2f;
	// Lets the camera travel before the next step.
	[Export] public float WaitSeconds = 0.6f;

	public override async Task Run(Cinematic cinematic)
	{
		if (cinematic.Player is not { } player)
			return;
		Node2D target = Target is null || Target.IsEmpty ? null : cinematic.GetNodeOrNull<Node2D>(Target);
		player.FocusCameraDuringCutscene(target?.GlobalPosition, Weight, target is null ? 1f : Zoom);
		await cinematic.WaitSeconds(WaitSeconds);
	}
}
