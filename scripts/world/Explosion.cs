using Godot;

namespace Metroidvania.World;

public partial class Explosion : Node2D
{
	[Export] public float Duration = 0.35f;
	[Export] public Color ExplosionColor = new(1f, 0.6f, 0.1f, 1f);

	public float TargetScale = 1f;

	public override void _Ready()
	{
		Polygon2D shape = GetNode<Polygon2D>("Shape");
		shape.Color = ExplosionColor;
		Scale = Vector2.Zero;

		Tween tween = CreateTween();
		tween.TweenProperty(this, "scale", Vector2.One * TargetScale, Duration)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		tween.Parallel().TweenProperty(shape, "color:a", 0f, Duration);
		tween.TweenCallback(Callable.From(QueueFree));
	}
}
