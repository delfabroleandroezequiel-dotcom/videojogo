using Godot;

namespace Metroidvania.World;

// One-shot "arrow stuck in the wall/target" burst — same helper shape as ImpactEffect.cs, just
// pointed at the arrow pack's own impact animation instead of the generic hit burst, and rotated
// to match the arrow's flight angle so it doesn't look pasted on sideways.
public static class ArrowImpactEffect
{
	private const string FramesPath = "res://resources/sprites/ArrowImpactSpriteFrames.tres";

	public static void SpawnAt(Node context, Vector2 globalPosition, float rotation)
	{
		var sprite = new AnimatedSprite2D
		{
			SpriteFrames = GD.Load<SpriteFrames>(FramesPath),
			Animation = "impact",
			Rotation = rotation,
		};

		context.GetTree().CurrentScene.AddChild(sprite);
		sprite.GlobalPosition = globalPosition;
		sprite.AnimationFinished += sprite.QueueFree;
		sprite.Play("impact");
	}
}
