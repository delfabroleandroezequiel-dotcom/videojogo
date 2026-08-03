using Godot;

namespace Metroidvania.World;

public static class VfxSpawner
{
	public static void SpawnAt(Node context, Vector2 globalPosition, string framesPath, string animation, Vector2 offset = default, float scale = 1f, Color? modulate = null)
	{
		var sprite = new AnimatedSprite2D
		{
			SpriteFrames = GD.Load<SpriteFrames>(framesPath),
			Animation = animation,
			Scale = Vector2.One * scale,
		};
		if (modulate.HasValue)
			sprite.Modulate = modulate.Value;

		context.GetTree().CurrentScene.AddChild(sprite);
		sprite.GlobalPosition = globalPosition + offset;
		sprite.AnimationFinished += sprite.QueueFree;
		sprite.Play(animation);
	}
}
