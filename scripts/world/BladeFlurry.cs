using System.Collections.Generic;
using Godot;

namespace Metroidvania.World;

// A burst of neon blade cuts around a point — the mushroom boss's "dance" flurry (see MushroomBoss).
// Each burst draws a set of short arcs (own centres) and straight crossing cuts, staggered a few
// hundredths apart so it reads as a rapid "tra-tra-tra" of blades, each a glow line + near-white
// core that grows along its path, then fades. Coordinates are local and assume the owner faces
// RIGHT; put it under the owner's Visual so the facing flip mirrors it.
public partial class BladeFlurry : Node2D
{
	// arc: (cx, cy, startDeg, endDeg, radius) — cut: (x0, y0, x1, y1). Approved prototype (2026-09-24),
	// mirrored to face right.
	private static readonly float[][] DefaultBlades =
	{
		new[] { 0f, -25f, -20f, -5f, -135f, 60f },
		new[] { 1f, 50f, -62f, -85f, 50f },
		new[] { 0f, -40f, 20f, -55f, 155f, 52f },
		new[] { 1f, 45f, 56f, -98f, -56f },
		new[] { 0f, 10f, 5f, 20f, -85f, 68f },
		new[] { 0f, -55f, -5f, -85f, 105f, 42f },
		new[] { 1f, 5f, -84f, -28f, 77f },
	};

	public Color GlowColor = new(0.25f, 1f, 0.4f, 0.9f);
	public Color CoreColor = new(0.85f, 1f, 0.9f, 1f);
	public float GlowWidth = 12f;
	public float CoreWidth = 3.5f;
	public float Stagger = 0.03f;
	public float GrowDuration = 0.09f;
	public float FadeDuration = 0.14f;

	private sealed class Blade
	{
		public float[] Shape;
		public float Age;
		public Line2D Glow;
		public Line2D Core;
	}

	private readonly List<Blade> _live = new();
	private CanvasItemMaterial _additive;
	private Curve _taper;
	private Gradient _fade;

	public override void _Ready()
	{
		_additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		_taper = new Curve();
		_taper.AddPoint(new Vector2(0f, 0.1f));
		_taper.AddPoint(new Vector2(0.7f, 1f));
		_taper.AddPoint(new Vector2(1f, 0.3f));
		_fade = new Gradient();
		_fade.SetColor(0, new Color(1f, 1f, 1f, 0f));
		_fade.SetColor(1, new Color(1f, 1f, 1f, 1f));
	}

	// One burst: every blade, staggered.
	public void Play()
	{
		for (int i = 0; i < DefaultBlades.Length; i++)
			_live.Add(new Blade { Shape = DefaultBlades[i], Age = -i * Stagger });
	}

	public override void _Process(double delta)
	{
		for (int i = _live.Count - 1; i >= 0; i--)
		{
			Blade blade = _live[i];
			blade.Age += (float)delta;
			if (blade.Age < 0f)
				continue;

			if (blade.Glow is null)
			{
				blade.Glow = MakeLine(GlowWidth);
				blade.Core = MakeLine(CoreWidth);
			}

			float grow = Mathf.Clamp(blade.Age / GrowDuration, 0f, 1f);
			Vector2[] points = BladePoints(blade.Shape, grow);
			blade.Glow.Points = points;
			blade.Core.Points = points;

			float fade = Mathf.Clamp((blade.Age - GrowDuration) / FadeDuration, 0f, 1f);
			blade.Glow.Modulate = GlowColor with { A = GlowColor.A * (1f - fade) };
			blade.Core.Modulate = CoreColor with { A = CoreColor.A * (1f - fade) };

			if (fade >= 1f)
			{
				blade.Glow.QueueFree();
				blade.Core.QueueFree();
				_live.RemoveAt(i);
			}
		}
	}

	private static Vector2[] BladePoints(float[] shape, float grow)
	{
		const int segments = 14;
		var points = new Vector2[segments + 1];
		for (int i = 0; i <= segments; i++)
		{
			float f = i / (float)segments * grow;
			if (shape[0] == 0f)
			{
				var centre = new Vector2(shape[1], shape[2]);
				float angle = Mathf.DegToRad(Mathf.Lerp(shape[3], shape[4], f));
				points[i] = centre + Vector2.Right.Rotated(angle) * shape[5];
			}
			else
			{
				points[i] = new Vector2(shape[1], shape[2]).Lerp(new Vector2(shape[3], shape[4]), f);
			}
		}
		return points;
	}

	private Line2D MakeLine(float width)
	{
		var line = new Line2D
		{
			Width = width,
			WidthCurve = _taper,
			Gradient = _fade,
			Material = _additive,
			BeginCapMode = Line2D.LineCapMode.Round,
			EndCapMode = Line2D.LineCapMode.Round,
		};
		AddChild(line);
		return line;
	}
}
