using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// Calls a method (no arguments) on a node — for a character's own scripted beat that the generic
// steps can't express, e.g. Arthur turning his back and walking off. Doesn't wait.
[GlobalClass]
public partial class StepCall : CinematicStep
{
	[Export] public NodePath Target;
	[Export] public string Method = "";

	public override Task Run(Cinematic cinematic)
	{
		Node target = cinematic.Resolve(Target);
		if (target is null || !target.HasMethod(Method))
			GD.PushWarning($"StepCall: \"{Target}\" has no method \"{Method}\".");
		else
			target.Call(Method);
		return Task.CompletedTask;
	}
}
