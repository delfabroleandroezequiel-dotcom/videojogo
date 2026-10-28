using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// Plays an animation on an actor; optionally waits for a non-looping one to finish.
[GlobalClass]
public partial class StepAnimate : CinematicStep
{
	[Export] public NodePath Actor;
	[Export] public string Animation = "idle";
	[Export] public bool WaitForEnd;
	// Animation to fall back to once a waited-for animation ends ("" = stay on its last frame).
	[Export] public string ThenAnimation = "idle";

	public override async Task Run(Cinematic cinematic)
	{
		Node2D actor = cinematic.Resolve(Actor);
		if (actor is Npc npc)
			cinematic.TakeControl(npc);
		AnimatedSprite2D sprite = Cinematic.SpriteOf(actor);
		if (sprite?.SpriteFrames is null || !sprite.SpriteFrames.HasAnimation(Animation))
			return;
		sprite.Play(Animation);
		if (!WaitForEnd || sprite.SpriteFrames.GetAnimationLoop(Animation))
			return;
		await cinematic.ToSignal(sprite, AnimatedSprite2D.SignalName.AnimationFinished);
		if (GodotObject.IsInstanceValid(sprite) && !string.IsNullOrEmpty(ThenAnimation))
			sprite.Play(ThenAnimation);
	}
}
