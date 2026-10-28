using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// Turns an actor to look at a target (empty Target = the player).
[GlobalClass]
public partial class StepFace : CinematicStep
{
	[Export] public NodePath Actor;
	[Export] public NodePath Target;

	public override Task Run(Cinematic cinematic)
	{
		Node2D actor = cinematic.Resolve(Actor);
		Node2D target = cinematic.Resolve(Target);
		if (actor is null || target is null || actor == cinematic.Player)
			return Task.CompletedTask;
		if (actor is Npc npc)
			cinematic.TakeControl(npc);
		Cinematic.FaceActor(actor, target.GlobalPosition.X - actor.GlobalPosition.X);
		return Task.CompletedTask;
	}
}
