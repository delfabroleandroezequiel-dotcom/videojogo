using System.Collections.Generic;
using Godot;

namespace Metroidvania.World;

// Reusable solid shadow: a plain rectangle you size with Width/Height, so a wide bar, a tall
// column or a big block are all the same scene with different numbers. Per-side Feather turns any
// edge into a soft fade instead of a hard cut, which is what makes different silhouettes possible
// from one rectangle (fade only the top for a shadow that dissolves upward, only the right for one
// that bleeds into a corridor, etc). Anchor picks which point of the rectangle sits on the node's
// position, so it can grow from a doorway or a floor line instead of always from its center.
// More complex shapes (L, T, stairs) come from overlapping several instances.
//
// Drawn with _Draw() rather than child Polygon2D nodes: nothing to rebuild/free, and nothing gets
// serialized into the maps that instance it. [Tool] so it previews live in the editor.
[Tool]
public partial class ShadowBlock : Node2D
{
	private float _width = 400f;

	[Export(PropertyHint.Range, "1,4000,1,or_greater")]
	public float Width
	{
		get => _width;
		set { _width = Mathf.Max(1f, value); QueueRedraw(); }
	}

	private float _height = 300f;

	[Export(PropertyHint.Range, "1,4000,1,or_greater")]
	public float Height
	{
		get => _height;
		set { _height = Mathf.Max(1f, value); QueueRedraw(); }
	}

	private Vector2 _anchor = new(0.5f, 0.5f);

	// Which point of the rectangle sits on this node's position: (0,0) top-left, (0.5,0.5) center,
	// (0.5,1) bottom-center, (1,0.5) middle of the right edge, etc.
	[Export]
	public Vector2 Anchor
	{
		get => _anchor;
		set { _anchor = new Vector2(Mathf.Clamp(value.X, 0f, 1f), Mathf.Clamp(value.Y, 0f, 1f)); QueueRedraw(); }
	}

	private Color _shadowColor = Colors.Black;

	[Export]
	public Color ShadowColor
	{
		get => _shadowColor;
		set { _shadowColor = value; QueueRedraw(); }
	}

	private float _opacity = 1f;

	[Export(PropertyHint.Range, "0,1,0.01")]
	public float Opacity
	{
		get => _opacity;
		set { _opacity = Mathf.Clamp(value, 0f, 1f); QueueRedraw(); }
	}

	private float _featherLeft;
	private float _featherTop;
	private float _featherRight;
	private float _featherBottom;

	// Width in pixels of the fade-out on each edge; 0 = hard edge. If two opposite feathers add up
	// to more than the rectangle's size they're scaled down proportionally to fit.
	[ExportGroup("Feather")]
	[Export(PropertyHint.Range, "0,2000,1,or_greater")]
	public float FeatherLeft
	{
		get => _featherLeft;
		set { _featherLeft = Mathf.Max(0f, value); QueueRedraw(); }
	}

	[Export(PropertyHint.Range, "0,2000,1,or_greater")]
	public float FeatherTop
	{
		get => _featherTop;
		set { _featherTop = Mathf.Max(0f, value); QueueRedraw(); }
	}

	[Export(PropertyHint.Range, "0,2000,1,or_greater")]
	public float FeatherRight
	{
		get => _featherRight;
		set { _featherRight = Mathf.Max(0f, value); QueueRedraw(); }
	}

	[Export(PropertyHint.Range, "0,2000,1,or_greater")]
	public float FeatherBottom
	{
		get => _featherBottom;
		set { _featherBottom = Mathf.Max(0f, value); QueueRedraw(); }
	}

	public override void _Draw()
	{
		(float[] xs, float[] xAlpha) = BuildAxis(_width, _featherLeft, _featherRight);
		(float[] ys, float[] yAlpha) = BuildAxis(_height, _featherTop, _featherBottom);
		Vector2 origin = -new Vector2(_width * _anchor.X, _height * _anchor.Y);

		Color ColorAt(int x, int y) => new(
			_shadowColor.R,
			_shadowColor.G,
			_shadowColor.B,
			_shadowColor.A * _opacity * xAlpha[x] * yAlpha[y]);

		// The rectangle is split into a grid along the feather boundaries, one quad per cell, with
		// alpha 0 only on the outer edge of a feathered side — so each cell's vertex colors
		// interpolate to a straight fade and unfeathered sides stay fully solid.
		var points = new Vector2[4];
		var colors = new Color[4];
		for (int y = 0; y < ys.Length - 1; y++)
		{
			for (int x = 0; x < xs.Length - 1; x++)
			{
				points[0] = origin + new Vector2(xs[x], ys[y]);
				points[1] = origin + new Vector2(xs[x + 1], ys[y]);
				points[2] = origin + new Vector2(xs[x + 1], ys[y + 1]);
				points[3] = origin + new Vector2(xs[x], ys[y + 1]);
				colors[0] = ColorAt(x, y);
				colors[1] = ColorAt(x + 1, y);
				colors[2] = ColorAt(x + 1, y + 1);
				colors[3] = ColorAt(x, y + 1);
				DrawPolygon(points, colors);
			}
		}
	}

	// Grid lines and per-line alpha along one axis: the outer ends fade to 0 only if that side has
	// a feather, and the lines just inside them (where each feather ends) are fully opaque.
	private static (float[] Positions, float[] Alphas) BuildAxis(float size, float startFeather, float endFeather)
	{
		startFeather = Mathf.Min(startFeather, size);
		endFeather = Mathf.Min(endFeather, size);
		if (startFeather + endFeather > size)
		{
			float fit = size / (startFeather + endFeather);
			startFeather *= fit;
			endFeather *= fit;
		}

		var positions = new List<float> { 0f };
		var alphas = new List<float> { startFeather > 0f ? 0f : 1f };

		if (startFeather > 0f && startFeather < size)
		{
			positions.Add(startFeather);
			alphas.Add(1f);
		}

		float endInner = size - endFeather;
		if (endFeather > 0f && endInner > positions[^1] + 0.001f)
		{
			positions.Add(endInner);
			alphas.Add(1f);
		}

		positions.Add(size);
		alphas.Add(endFeather > 0f ? 0f : 1f);
		return (positions.ToArray(), alphas.ToArray());
	}
}
