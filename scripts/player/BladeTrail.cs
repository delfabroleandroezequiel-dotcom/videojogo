using System.Collections.Generic;
using Godot;

namespace Metroidvania.Player;

// A sword trail drawn the way the player's own SwordArc is (smooth curve through the blade tip's
// measured path, revealed over the swing's draw time), but built for excess: three stacked Line2Ds —
// a black ink smear (normal blend), a wide red glow and a white-hot core (additive) — each with a
// soft-edged gradient texture across its width so the edges bloom instead of cutting hard, a bright
// head fading to nothing at the tail, and a taper toward the tail. After the reveal it lingers, then
// thins and fades. Lives under the shadow's Visual node, so facing flips it for free.
public partial class BladeTrail : Node2D
{
	public Vector2[] Curve = System.Array.Empty<Vector2>();
	public float DrawTime = 0.1f;
	// Seconds to wait before the reveal, to line up with the frame where the sheet's swing happens.
	public float Delay;
	public float Width = 18f;
	public Color Glow = new(1f, 0.1f, 0.15f, 1f);
	public Color Core = new(1f, 0.85f, 0.8f, 1f);
	public Color Ink = new(0.03f, 0f, 0.02f, 1f);
	public float Linger = 0.08f;
	public float FadeTime = 0.22f;
	// Tiny red humanities (MicroHumanity) shed off the leading edge while the cut is drawn, flung
	// out to the outside of the swing. 0 = none.
	public int HumanityCount;
	public Color HumanityColor = new(1f, 0.15f, 0.2f, 1f);
	public float HumanitySize = 0.4f;
	private int _humanitiesShed;

	private Line2D _ink;
	private Line2D _glow;
	private Line2D _core;
	private float _age;

	private static Texture2D _softEdge;
	public static Texture2D SoftEdgeTexture => SoftEdge();

	// Transparent at both edges, solid in the middle — across the line's width (TextureMode.Stretch
	// maps V across the width).
	private static Texture2D SoftEdge()
	{
		if (_softEdge is not null)
			return _softEdge;
		var gradient = new Gradient();
		gradient.SetOffset(0, 0f);
		gradient.SetColor(0, new Color(1f, 1f, 1f, 0f));
		gradient.SetOffset(1, 1f);
		gradient.SetColor(1, new Color(1f, 1f, 1f, 0f));
		gradient.AddPoint(0.5f, Colors.White);
		gradient.AddPoint(0.3f, new Color(1f, 1f, 1f, 0.6f));
		gradient.AddPoint(0.7f, new Color(1f, 1f, 1f, 0.6f));
		_softEdge = new GradientTexture2D { Gradient = gradient, Width = 4, Height = 64, FillFrom = new Vector2(0f, 0f), FillTo = new Vector2(0f, 1f) };
		return _softEdge;
	}

	private Line2D Layer(Color color, float width, bool additive, float headAlpha)
	{
		var fade = new Gradient();
		fade.SetColor(0, color with { A = 0f });
		fade.SetColor(1, color with { A = headAlpha });
		fade.AddPoint(0.6f, color with { A = headAlpha * 0.7f });
		var taper = new Curve();
		taper.AddPoint(new Vector2(0f, 0.15f));
		taper.AddPoint(new Vector2(0.75f, 1f));
		taper.AddPoint(new Vector2(1f, 0.7f));
		var line = new Line2D
		{
			Width = width,
			WidthCurve = taper,
			Gradient = fade,
			Texture = SoftEdge(),
			TextureMode = Line2D.LineTextureMode.Stretch,
			JointMode = Line2D.LineJointMode.Round,
			BeginCapMode = Line2D.LineCapMode.Round,
			EndCapMode = Line2D.LineCapMode.Round,
			Antialiased = true,
		};
		if (additive)
			line.Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		AddChild(line);
		return line;
	}

	public override void _Ready()
	{
		_ink = Layer(Ink, Width * 1.45f, false, 0.85f);
		_glow = Layer(Glow, Width * 1.15f, true, 0.9f);
		_core = Layer(Core, Width * 0.32f, true, 1f);
	}

	public override void _Process(double delta)
	{
		_age += (float)delta;
		if (Curve.Length < 2)
		{
			QueueFree();
			return;
		}
		if (_age < Delay)
			return;
		float age = _age - Delay;
		float progress = Mathf.Clamp(age / Mathf.Max(DrawTime, 0.01f), 0f, 1f);
		int count = Mathf.Max(2, Mathf.CeilToInt(Curve.Length * progress));
		var shown = new Vector2[count];
		System.Array.Copy(Curve, shown, count);
		_ink.Points = shown;
		_glow.Points = shown;
		_core.Points = shown;
		ShedHumanities(shown, progress);

		float fadeStart = DrawTime + Linger;
		if (age > fadeStart)
		{
			float f = 1f - Mathf.Clamp((age - fadeStart) / FadeTime, 0f, 1f);
			Modulate = new Color(1f, 1f, 1f, f);
			// Thins out as it dies, like the smear drying.
			_glow.Width = Width * 1.15f * (0.5f + 0.5f * f);
			_core.Width = Width * 0.32f * f;
			if (f <= 0f)
				QueueFree();
		}
	}

	// Keeps the shed count in step with the reveal, each one popping off the current head of the cut
	// and flung along the swing's outward normal (in world space, so a flipped Visual is handled).
	private void ShedHumanities(Vector2[] shown, float progress)
	{
		int due = Mathf.RoundToInt(HumanityCount * progress);
		while (_humanitiesShed < due && shown.Length >= 2)
		{
			_humanitiesShed++;
			Vector2 head = ToGlobal(shown[^1]);
			Vector2 prev = ToGlobal(shown[^2]);
			Vector2 tangent = (head - prev).Normalized();
			Vector2 center = ToGlobal(Vector2.Zero);
			Vector2 normal = tangent.Orthogonal();
			if (normal.Dot(head - center) < 0f)
				normal = -normal;
			Vector2 velocity = (normal * (float)GD.RandRange(60.0, 160.0) + tangent * (float)GD.RandRange(40.0, 120.0))
				.Rotated((float)GD.RandRange(-0.5, 0.5));
			Metroidvania.World.MicroHumanity.SpawnAt(this, head, velocity, HumanitySize * (float)GD.RandRange(0.75, 1.25), HumanityColor);
		}
	}

	// Catmull-Rom-ish smoothing of a sparse tip path, same as Player.RunPathNeonTrail.
	public static Vector2[] Smooth(IReadOnlyList<Vector2> path, int samplesPerSegment = 8)
	{
		var curve = new List<Vector2>();
		for (int i = 0; i < path.Count - 1; i++)
		{
			Vector2 pre = path[Mathf.Max(i - 1, 0)];
			Vector2 a = path[i];
			Vector2 b = path[i + 1];
			Vector2 post = path[Mathf.Min(i + 2, path.Count - 1)];
			for (int s = 0; s < samplesPerSegment; s++)
				curve.Add(a.CubicInterpolate(b, pre, post, s / (float)samplesPerSegment));
		}
		curve.Add(path[^1]);
		return curve.ToArray();
	}
}
