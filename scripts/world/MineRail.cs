using Godot;

namespace Metroidvania.World;

// Shared helper for everything that runs on mine rails (MineCart = the ride, LooseMineCart = the
// runaway hazard): a rail is just a polyline of points in the owner's parent space, walked by
// distance along it. Also draws the greybox rails (two steel lines + wooden sleepers) so both carts
// show exactly the track they follow — the rail is the visible "why" of the motion, never a hidden
// path.
public static class MineRail
{
	public static float Length(Vector2[] points)
	{
		float total = 0f;
		for (int i = 1; i < points.Length; i++)
			total += points[i - 1].DistanceTo(points[i]);
		return total;
	}

	// Point and unit tangent at `distance` along the rail (clamped to both ends).
	public static Vector2 Sample(Vector2[] points, float distance, out Vector2 tangent)
	{
		tangent = Vector2.Right;
		if (points.Length == 0)
			return Vector2.Zero;
		if (points.Length == 1)
			return points[0];

		float remaining = Mathf.Max(0f, distance);
		for (int i = 1; i < points.Length; i++)
		{
			Vector2 a = points[i - 1];
			Vector2 b = points[i];
			float segment = a.DistanceTo(b);
			if (segment < 0.001f)
				continue;
			tangent = (b - a) / segment;
			if (remaining <= segment || i == points.Length - 1)
				return a + tangent * Mathf.Min(remaining, segment);
			remaining -= segment;
		}
		return points[^1];
	}

	// Two rail lines a hair apart (side view: the near and the far rail) plus sleepers every
	// SleeperSpacing px, all as plain Line2D children of `parent`. Points are in parent space.
	public static Node2D BuildVisual(Node parent, Vector2[] points, string name = "Rieles")
	{
		var root = new Node2D { Name = name, ZIndex = -2 };
		parent.AddChild(root);
		if (points.Length < 2)
			return root;

		var steel = new Color(0.55f, 0.56f, 0.6f, 1f);
		var wood = new Color(0.36f, 0.24f, 0.14f, 1f);

		float length = Length(points);
		const float sleeperSpacing = 22f;
		for (float d = 6f; d < length; d += sleeperSpacing)
		{
			Vector2 p = Sample(points, d, out Vector2 t);
			Vector2 n = new Vector2(-t.Y, t.X); // points "down" for a left-to-right rail
			root.AddChild(new Line2D
			{
				Points = new[] { p - t * 5f + n * 2f, p + t * 5f + n * 2f },
				Width = 4f,
				DefaultColor = wood,
			});
		}
		root.AddChild(new Line2D { Points = points, Width = 2f, DefaultColor = steel.Darkened(0.35f), Position = new Vector2(0f, 2f) });
		root.AddChild(new Line2D { Points = points, Width = 2f, DefaultColor = steel });
		return root;
	}
}
