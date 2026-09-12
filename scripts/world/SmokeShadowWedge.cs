using System.Collections.Generic;
using Godot;

namespace Metroidvania.World;

// Reusable ground shadow shaped like an inverted triangle — a solid dark body across most of its
// depth that only starts fading in the last stretch before the point, instead of thinning out
// evenly the whole way down. A single antialiased Polygon2D with per-vertex alpha, not overlapping
// sprites, so the fill reads as one smooth, homogeneous shape. Meant as a generic "something
// connects here" ground marker — pair with LevelTransition's Iluminado light for an entrance/return
// point, or use alone to draw the eye toward any spot.
// [Tool] so it previews live in the editor.
[Tool]
public partial class SmokeShadowWedge : Node2D
{
	private float _width = 500f;

	// How wide the shadow is at its top (open) edge.
	[Export(PropertyHint.Range, "20,2000,1")]
	public float Width
	{
		get => _width;
		set { _width = Mathf.Max(20f, value); Rebuild(); }
	}

	private float _depth = 180f;

	// How far down the point reaches from the top edge.
	[Export(PropertyHint.Range, "10,1000,1")]
	public float Depth
	{
		get => _depth;
		set { _depth = Mathf.Max(10f, value); Rebuild(); }
	}

	private float _maxOpacity = 0.45f;

	[Export(PropertyHint.Range, "0,1,0.01")]
	public float MaxOpacity
	{
		get => _maxOpacity;
		set { _maxOpacity = Mathf.Clamp(value, 0f, 1f); Rebuild(); }
	}

	private Color _smokeColor = new(0.05f, 0.05f, 0.07f);

	[Export]
	public Color SmokeColor
	{
		get => _smokeColor;
		set { _smokeColor = value; Rebuild(); }
	}

	private float _solidRatio = 0.55f;

	// Fraction of the depth (from the top) that stays fully opaque before the fade to the point
	// begins — 0.55 means the top 55% is a flat solid body and only the last 45% tapers to nothing.
	[Export(PropertyHint.Range, "0,0.95,0.01")]
	public float SolidRatio
	{
		get => _solidRatio;
		set { _solidRatio = Mathf.Clamp(value, 0f, 0.95f); Rebuild(); }
	}

	private int _subdivisions = 12;

	// How many steps make up the taper — only affects how smooth the fade gradient looks, not the
	// (always straight) silhouette edges.
	[Export(PropertyHint.Range, "2,40,1")]
	public int Subdivisions
	{
		get => _subdivisions;
		set { _subdivisions = Mathf.Max(2, value); Rebuild(); }
	}

	public override void _Ready() => Rebuild();

	private void Rebuild()
	{
		if (!IsInsideTree())
			return;

		Scale = Vector2.One;

		foreach (Node child in GetChildren())
			child.Free();

		float halfWidth = _width / 2f;
		var points = new List<Vector2>();
		var colors = new List<Color>();

		Color ColorAt(float t)
		{
			float alpha = t <= _solidRatio
				? _maxOpacity
				: Mathf.Lerp(_maxOpacity, 0f, (t - _solidRatio) / (1f - _solidRatio));
			return new Color(_smokeColor.R, _smokeColor.G, _smokeColor.B, alpha);
		}

		// Top edge, then down the right slanted side to the apex, then back up the left side.
		points.Add(new Vector2(-halfWidth, 0f));
		colors.Add(ColorAt(0f));
		points.Add(new Vector2(halfWidth, 0f));
		colors.Add(ColorAt(0f));

		for (int i = 1; i <= _subdivisions; i++)
		{
			float t = i / (float)_subdivisions;
			points.Add(new Vector2(halfWidth * (1f - t), _depth * t));
			colors.Add(ColorAt(t));
		}

		for (int i = _subdivisions - 1; i >= 1; i--)
		{
			float t = i / (float)_subdivisions;
			points.Add(new Vector2(-halfWidth * (1f - t), _depth * t));
			colors.Add(ColorAt(t));
		}

		var polygon = new Polygon2D
		{
			Antialiased = true,
			Polygon = points.ToArray(),
			VertexColors = colors.ToArray(),
		};
		AddChild(polygon);
		polygon.Owner = this;
	}
}
