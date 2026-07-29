using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Reusable greybox building block: like CorridorBlock but stripped down to a single collision
// side — Lateral is a floor strip (walkable top surface at the origin, extends right over Length,
// down by Thickness), Vertical is a standalone wall/pillar strip (extends down over Length,
// straddling the origin horizontally by Thickness). No floor/ceiling/wall toggles because there's
// only ever one side; use CorridorBlock instead when a piece needs more than one.
// Meant to be chained by MiniCorridorAccordion, which spaces a row of these by a jump-gap
// distance — but also usable standalone, hand-placed, like any other reusable piece.
// [Tool] so the shape/fill preview updates live in the editor, before ever pressing Play.
[Tool]
public partial class MiniCorridor : Node2D
{
	// World only, by default — matches the previous unconditional behavior so existing pieces
	// don't suddenly become graspable on load.
	private const uint SolidLayer = 1u;
	private const uint ClimbableLayer = 1u | PhysicsLayers.ClimbableWalls;

	private CorridorOrientation _orientation = CorridorOrientation.Lateral;

	[Export]
	public CorridorOrientation Orientation
	{
		get => _orientation;
		set { _orientation = value; Rebuild(); }
	}

	private float _length = 96f;

	[Export]
	public float Length
	{
		get => _length;
		set { _length = value; Rebuild(); }
	}

	private float _thickness = 32f;

	[Export]
	public float Thickness
	{
		get => _thickness;
		set { _thickness = value; Rebuild(); }
	}

	[Export] public Color FillColor = new(0.5f, 0.5f, 0.55f, 0.6f);

	private bool _showFill = true;

	[Export]
	public bool ShowFill
	{
		get => _showFill;
		set { _showFill = value; Rebuild(); }
	}

	private bool _climbable;

	// Tags the collision on the ClimbableWalls physics layer so Player's existing wall-climb
	// (Vertical piece) or ledge-grab (Lateral piece) already catches it — same layer MiniPlataforma
	// and MiniPared bake in unconditionally, but here it's optional since a plain CorridorBlock-
	// style wall/floor isn't always meant to be climbable/graspable.
	[Export]
	public bool Climbable
	{
		get => _climbable;
		set { _climbable = value; Rebuild(); }
	}

	private bool _oneWay;

	// Jump/pass through from the open side, land/block from the other — same idea as
	// MiniPlataforma's OneWay, off by default here since MiniCorridor doubles as a solid wall too.
	[Export]
	public bool OneWay
	{
		get => _oneWay;
		set { _oneWay = value; Rebuild(); }
	}

	public override void _Ready() => Rebuild();

	private void Rebuild()
	{
		if (!IsInsideTree())
			return;

		bool lateral = _orientation == CorridorOrientation.Lateral;
		Vector2 size = lateral ? new Vector2(_length, _thickness) : new Vector2(_thickness, _length);
		Vector2 center = lateral ? new Vector2(_length / 2f, _thickness / 2f) : new Vector2(0f, _length / 2f);

		if (GetNodeOrNull<StaticBody2D>("Body") is StaticBody2D body)
		{
			body.Position = center;
			// OneWayPlatforms tags this for Player.TryDropThroughOneWayPlatform, which filters by
			// that layer specifically — OneWayCollision alone (set below) only gives the engine's
			// jump-through-from-below behavior, not the down+jump drop-through game mechanic.
			uint layer = _climbable ? ClimbableLayer : SolidLayer;
			if (_oneWay)
				layer |= PhysicsLayers.OneWayPlatforms;
			body.CollisionLayer = layer;

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
				fill.Visible = _showFill;
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

		if (GetNodeOrNull<Marker2D>("Exit") is Marker2D exit)
			exit.Position = lateral ? new Vector2(_length, 0f) : new Vector2(0f, _length);
	}
}
