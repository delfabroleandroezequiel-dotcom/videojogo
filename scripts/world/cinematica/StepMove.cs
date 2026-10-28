using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// Walks an actor to a target (a node, e.g. a Marker2D, or a world X) playing Animation, then
// EndAnimation. NPCs walk with their own physics; anything else is slid along.
[GlobalClass]
public partial class StepMove : CinematicStep
{
	[Export] public NodePath Actor;
	[Export] public NodePath Target;
	// Used when Target is empty.
	[Export] public float TargetX;
	[Export] public float Speed = 80f;
	[Export] public string Animation = "walk";
	[Export] public string EndAnimation = "idle";
	// false = start walking and let the next steps run meanwhile.
	[Export] public bool WaitUntilArrived = true;

	public override async Task Run(Cinematic cinematic)
	{
		Node2D actor = cinematic.Resolve(Actor);
		if (actor is null || actor == cinematic.Player)
		{
			GD.PushWarning("StepMove: the player isn't moved by cinematics (it's held); pick an NPC.");
			return;
		}
		float goal = Target is null || Target.IsEmpty ? TargetX : cinematic.GetNodeOrNull<Node2D>(Target)?.GlobalPosition.X ?? TargetX;
		Task walk = Walk(cinematic, actor, goal);
		if (WaitUntilArrived)
			await walk;
	}

	private async Task Walk(Cinematic cinematic, Node2D actor, float goal)
	{
		float dir = Mathf.Sign(goal - actor.GlobalPosition.X);
		if (dir == 0f)
			return;
		Cinematic.FaceActor(actor, dir);
		Cinematic.SpriteOf(actor)?.Play(Animation);
		var npc = actor as Npc;
		if (npc is not null)
		{
			cinematic.TakeControl(npc);
			npc.CinematicVelocityX = dir * Speed;
		}
		float timeout = Mathf.Abs(goal - actor.GlobalPosition.X) / Mathf.Max(1f, Speed) + 2f;
		while (timeout > 0f && GodotObject.IsInstanceValid(actor) && (goal - actor.GlobalPosition.X) * dir > 2f)
		{
			float dt = (float)cinematic.GetPhysicsProcessDeltaTime();
			if (npc is null)
				actor.GlobalPosition += new Vector2(dir * Speed * dt, 0f);
			timeout -= dt;
			await cinematic.WaitPhysicsFrame();
		}
		if (!GodotObject.IsInstanceValid(actor))
			return;
		if (npc is not null)
			npc.CinematicVelocityX = 0f;
		Cinematic.SpriteOf(actor)?.Play(EndAnimation);
	}
}
