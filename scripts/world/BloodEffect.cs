using Godot;

namespace Metroidvania.World;

public static class BloodEffect
{
	private const string FramesPath = "res://resources/sprites/BloodSpriteFrames.tres";

	public static void SpawnAt(Node context, Vector2 globalPosition)
	{
		var sprite = new AnimatedSprite2D
		{
			SpriteFrames = GD.Load<SpriteFrames>(FramesPath),
			Animation = "splat",
		};

		context.GetTree().CurrentScene.AddChild(sprite);
		sprite.GlobalPosition = globalPosition;
		sprite.AnimationFinished += sprite.QueueFree;
		sprite.Play("splat");
	}
}
