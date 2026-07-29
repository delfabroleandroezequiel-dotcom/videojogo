using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Reusable greybox ramp: a diagonal walkable surface instead of MiniCorridor's flat one — a solid
// wedge (CollisionPolygon2D, so the polygon can be rewritten directly without the "fresh Shape2D
// resource" dance CorridorBlock/MiniCorridor need for a shared RectangleShape2D). Walkable for
// free: CharacterBody2D's default floor_max_angle is 45°, which Player never overrides, so any
// AngleDegrees up to 45° already registers as floor and MoveAndSlide handles it — no ramp-specific
// code needed in Player.cs. Steeper than that and it stops counting as floor.
// Sized by Length (the actual slope surface length) + AngleDegrees instead of separate
// horizontal/vertical components — matches how you'd actually spec a ramp ("a 200px ramp at 30°"),
// with Run/Rise derived from those. AngleDegrees is signed like MiniEscaleraAccordion's
// StepHeight — negative climbs (goes up to the right), positive descends.
// [Tool] so the shape/fill preview updates live in the editor, before ever pressing Play.
[Tool]
public partial class MiniRampa : Node2D
{
	private float _length = 144f;

	[Export]
	public float Length
	{
		get => _length;
		set { _length = Mathf.Max(1f, value); Rebuild(); }
	}

	private float _angleDegrees = -25f;

	[Export(PropertyHint.Range, "-45,45,0.5")]
	public float AngleDegrees
	{
		get => _angleDegrees;
		set { _angleDegrees = Mathf.Clamp(value, -45f, 45f); Rebuild(); }
	}

	private float _thickness = 32f;

	[Export]
	public float Thickness
	{
		get => _thickness;
		set { _thickness = Mathf.Max(4f, value); Rebuild(); }
	}

	[Export] public Color FillColor = new(0.5f, 0.5f, 0.55f, 0.6f);

	private bool _showFill = true;

	[Export]
	public bool ShowFill
	{
		get => _showFill;
		set { _showFill = value; Rebuild(); }
	}

	private bool _oneWay;

	// Jump/pass through from underneath, land/block from above — same idea as MiniPlataforma's
	// OneWay, off by default since a ramp usually doubles as a solid wall/floor join.
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

		float rad = Mathf.DegToRad(_angleDegrees);
		Vector2 low = Vector2.Zero;
		Vector2 high = new(_length * Mathf.Cos(rad), _length * Mathf.Sin(rad));
		Vector2 lowBack = low + new Vector2(0f, _thickness);
		Vector2 highBack = high + new Vector2(0f, _thickness);
		Vector2[] points = { low, high, highBack, lowBack };

		if (GetNodeOrNull<StaticBody2D>("Body") is StaticBody2D body)
		{
			// OneWayPlatforms tags this for Player.TryDropThroughOneWayPlatform, which filters by
			// that layer specifically — OneWayCollision alone (below) only gives the engine's
			// jump-through-from-below behavior, not the down+jump drop-through game mechanic.
			body.CollisionLayer = _oneWay ? 1u | PhysicsLayers.OneWayPlatforms : 1u;

			if (body.GetNodeOrNull<CollisionPolygon2D>("CollisionPolygon2D") is CollisionPolygon2D collision)
			{
				collision.Polygon = points;
				collision.OneWayCollision = _oneWay;
			}

			if (body.GetNodeOrNull<Polygon2D>("Fill") is Polygon2D fill)
			{
				fill.Visible = _showFill;
				fill.Color = FillColor;
				fill.Polygon = points;
			}
		}

		if (GetNodeOrNull<Marker2D>("Exit") is Marker2D exit)
			exit.Position = high;
	}
}
