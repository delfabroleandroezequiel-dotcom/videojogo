using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Reusable greybox wall pillar: a single vertical collision strip tagged on the ClimbableWalls
// physics layer, so Player's existing WallClimb/wall-jump detection (IsTouchingClimbableWall in
// Player.cs) already catches it — no special setup needed, same idea as MiniPlataforma for
// ledge-grab. Meant to be chained by MiniParedAccordion, but usable standalone too.
// [Tool] so the shape/fill preview updates live in the editor, before ever pressing Play.
[Tool]
public partial class MiniPared : Node2D
{
	// World + ClimbableWalls — matches the collision_layer = 17 convention already used on
	// hand-placed graspable wall/floor edges.
	private const uint ClimbableLayer = 1u | PhysicsLayers.ClimbableWalls;

	private float _length = 160f;

	[Export]
	public float Length
	{
		get => _length;
		set { _length = value; Rebuild(); }
	}

	private float _thickness = 24f;

	[Export]
	public float Thickness
	{
		get => _thickness;
		set { _thickness = value; Rebuild(); }
	}

	[Export] public Color FillColor = new(0.5f, 0.5f, 0.55f, 0.6f);

	public override void _Ready() => Rebuild();

	private void Rebuild()
	{
		if (!IsInsideTree())
			return;

		Vector2 size = new(_thickness, _length);

		if (GetNodeOrNull<StaticBody2D>("Body") is StaticBody2D body)
		{
			body.Position = new Vector2(0f, _length / 2f);
			body.CollisionLayer = ClimbableLayer;

			if (body.GetNodeOrNull<CollisionShape2D>("CollisionShape2D") is CollisionShape2D collision)
			{
				// Always assign a fresh shape instead of mutating collision.Shape in place — the
				// RectangleShape2D from the .tscn is a single shared resource, so every instance
				// would otherwise resize the same object and collisions would jump between pieces.
				collision.Shape = new RectangleShape2D { Size = size };
			}

			if (body.GetNodeOrNull<Polygon2D>("Fill") is Polygon2D fill)
			{
				fill.Color = FillColor;
				Vector2 half = size / 2f;
				fill.Polygon = new[]
				{
					new Vector2(-half.X, -half.Y),
					new Vector2(half.X, -half.Y),
					new Vector2(half.X, half.Y),
					new Vector2(-half.X, half.Y),
				};
			}
		}
	}
}
