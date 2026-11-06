using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// Turns an actor to look at a target (empty Actor or Target = the player).
[GlobalClass]
public partial class StepFace : CinematicStep
{
	[Export] public NodePath Actor;
	[Export] public NodePath Target;

	public override Task Run(Cinematic cinematic)
	{
		Node2D actor = cinematic.Resolve(Actor);
		Node2D target = cinematic.Resolve(Target);
		if (actor is null || target is null || actor == target)
			return Task.CompletedTask;
		float direction = target.GlobalPosition.X - actor.GlobalPosition.X;
		if (actor is Metroidvania.Player.Player player)
			player.CutsceneFace(direction);
		else
		{
			if (actor is Npc npc)
				cinematic.TakeControl(npc);
			Cinematic.FaceActor(actor, direction);
		}
		return Task.CompletedTask;
	}

	public override void Skip(Cinematic cinematic) => Run(cinematic);
}
