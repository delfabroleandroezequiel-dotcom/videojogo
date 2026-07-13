using Godot;

namespace Metroidvania.World;

public partial class Explosion : Node2D
{
	[Export] public float BaseVisualScale = 1.53f;
	[Export] public float VerticalOffset = -14f;

	public float TargetScale = 1f;

	public override void _Ready()
	{
		var sprite = GetNode<AnimatedSprite2D>("Sprite");
		Scale = Vector2.One * TargetScale * BaseVisualScale;
		sprite.Position = new Vector2(0f, VerticalOffset);
		sprite.AnimationFinished += QueueFree;
		sprite.Play("explode");
	}
}
