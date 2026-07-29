using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Reusable greybox platform: a single one-way collision strip (jump through from below, land on
// top), pre-tagged on the ClimbableWalls physics layer so Player's existing generic ledge-grab
// (TryDetectLedge in Player.cs) already catches its edges — no special "grab marker" needed, same
// as any hand-tuned floor edge in the levels (see CuevaBosqueLobo1.tscn's collision_layer=17
// floors). Meant to be chained by MiniPlataformaAccordion, but usable standalone too.
// [Tool] so the shape/fill preview updates live in the editor, before ever pressing Play.
[Tool]
public partial class MiniPlataforma : Node2D
{
	// World + ClimbableWalls — matches the collision_layer = 17 convention already used on
	// hand-placed graspable floor edges.
	private const uint GraspableLayer = 1u | PhysicsLayers.ClimbableWalls;

	private float _length = 96f;

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

	private bool _oneWay = true;

	[Export]
	public bool OneWay
	{
		get => _oneWay;
		set { _oneWay = value; Rebuild(); }
	}

	[Export] public Color FillColor = new(0.5f, 0.5f, 0.55f, 0.6f);

	public override void _Ready() => Rebuild();

	private void Rebuild()
	{
		if (!IsInsideTree())
			return;

		Vector2 size = new(_length, _thickness);

		if (GetNodeOrNull<StaticBody2D>("Body") is StaticBody2D body)
		{
			body.Position = new Vector2(_length / 2f, _thickness / 2f);
			// OneWayPlatforms tags this for Player.TryDropThroughOneWayPlatform, which filters by
			// that layer specifically — OneWayCollision alone (set below) only gives the engine's
			// jump-through-from-below behavior, not the down+jump drop-through game mechanic.
			body.CollisionLayer = _oneWay ? GraspableLayer | PhysicsLayers.OneWayPlatforms : GraspableLayer;

			if (body.GetNodeOrNull<CollisionShape2D>("CollisionShape2D") is CollisionShape2D collision)
			{
				// Always assign a fresh shape instead of mutating collision.Shape in place — the
				// RectangleShape2D from the .tscn is a single shared resource, so every instance
				// would otherwise resize the same object and collisions would jump between pieces.
				collision.Shape = new RectangleShape2D { Size = size };
				collision.OneWayCollision = _oneWay;
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
