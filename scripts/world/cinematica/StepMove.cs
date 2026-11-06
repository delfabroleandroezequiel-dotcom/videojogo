using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// Walks an actor to a target (a node, e.g. a Marker2D, or a world X) playing Animation, then
// EndAnimation. NPCs walk with their own physics; the player (empty Actor) walks with its own
// physics and run/idle animations while still held (Player.CutsceneWalk) — Animation and
// EndAnimation don't apply to it; anything else is slid along.
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
		if (actor is null)
			return;
		Task walk = Walk(cinematic, actor, Goal(cinematic));
		if (WaitUntilArrived)
			await walk;
	}

	// Skipped: straight onto the mark, standing.
	public override void Skip(Cinematic cinematic)
	{
		Node2D actor = cinematic.Resolve(Actor);
		if (actor is null)
			return;
		actor.GlobalPosition = new Vector2(Goal(cinematic), actor.GlobalPosition.Y);
		if (actor is Metroidvania.Player.Player player)
			player.CutsceneWalk(0f);
		else
		{
			if (actor is Npc npc)
				npc.CinematicVelocityX = 0f;
			Cinematic.SpriteOf(actor)?.Play(EndAnimation);
		}
	}

	private float Goal(Cinematic cinematic) =>
		Target is null || Target.IsEmpty ? TargetX : cinematic.GetNodeOrNull<Node2D>(Target)?.GlobalPosition.X ?? TargetX;

	private async Task Walk(Cinematic cinematic, Node2D actor, float goal)
	{
		float dir = Mathf.Sign(goal - actor.GlobalPosition.X);
		if (dir == 0f)
			return;
		var player = actor as Metroidvania.Player.Player;
		var npc = actor as Npc;
		if (player is not null)
			player.CutsceneWalk(dir * Speed);
		else
		{
			Cinematic.FaceActor(actor, dir);
			Cinematic.SpriteOf(actor)?.Play(Animation);
		}
		if (npc is not null)
		{
			cinematic.TakeControl(npc);
			npc.CinematicVelocityX = dir * Speed;
		}
		float timeout = Mathf.Abs(goal - actor.GlobalPosition.X) / Mathf.Max(1f, Speed) + 2f;
		while (timeout > 0f && !cinematic.Skipping && GodotObject.IsInstanceValid(actor) && (goal - actor.GlobalPosition.X) * dir > 2f)
		{
			float dt = (float)cinematic.GetPhysicsProcessDeltaTime();
			if (npc is null && player is null)
				actor.GlobalPosition += new Vector2(dir * Speed * dt, 0f);
			timeout -= dt;
			await cinematic.WaitPhysicsFrame();
		}
		if (!GodotObject.IsInstanceValid(actor) || cinematic.Skipping)
			return;
		if (player is not null)
		{
			player.CutsceneWalk(0f);
			return;
		}
		if (npc is not null)
			npc.CinematicVelocityX = 0f;
		Cinematic.SpriteOf(actor)?.Play(EndAnimation);
	}
}
