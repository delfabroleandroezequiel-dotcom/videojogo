using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// Moves an actor's drawing (its Visual node) away from its body, without touching the physics:
// lying on a bed the body can't stand on, sitting on a ledge, a small hop down from it.
// Offset (0, 0) brings the drawing back onto the body. Duration 0 = instant.
[GlobalClass]
public partial class StepActorOffset : CinematicStep
{
	[Export] public NodePath Actor;
	[Export] public Vector2 Offset;
	[Export] public float Duration;
	[Export] public bool WaitForEnd = true;
	// The player's cloth cape hangs straight down from a lying body: hide it while posed.
	[Export] public bool HideCape;

	public override async Task Run(Cinematic cinematic)
	{
		Node2D actor = cinematic.Resolve(Actor);
		if (actor?.GetNodeOrNull<Node2D>("Visual") is not { } visual)
			return;
		if (actor.GetNodeOrNull("CharacterFlair") is { } flair && flair.Get("CapeEnabled").VariantType != Variant.Type.Nil)
			flair.Set("CapeEnabled", !HideCape);
		if (Duration <= 0f)
		{
			visual.Position = Offset;
			return;
		}
		Tween tween = visual.CreateTween().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
		tween.TweenProperty(visual, "position", Offset, Duration);
		if (WaitForEnd)
			await cinematic.ToSignal(tween, Tween.SignalName.Finished);
	}

	public override void Skip(Cinematic cinematic)
	{
		Node2D actor = cinematic.Resolve(Actor);
		if (actor?.GetNodeOrNull<Node2D>("Visual") is not { } visual)
			return;
		if (actor.GetNodeOrNull("CharacterFlair") is { } flair && flair.Get("CapeEnabled").VariantType != Variant.Type.Nil)
			flair.Set("CapeEnabled", !HideCape);
		visual.Position = Offset;
	}
}
