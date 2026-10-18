using Godot;

namespace Metroidvania.World;

// Procedural spike row for Spikes.tscn: iron stakes with lit/shadow faces, rust creeping up from
// the sockets, dried blood running down from the tips, a dimmer back row for depth and a cracked
// stone plate underneath. Everything is drawn in _Draw from a seeded RNG, so two rows with
// different Seed values never repeat the same silhouette, and the same Seed always redraws
// identically (editor and game match).
//
// Local origin = bottom-centre of the stone plate. Rotate the parent Spikes instance for
// ceiling/wall spikes — the drawing and the hazard rectangle rotate with it.
//
// When HazardShape is set, keeps that CollisionShape2D (the Hazard area's shape) sized to the spikes.
// Always assigns a fresh RectangleShape2D instead of resizing the existing one, so instances
// never share/mutate the same sub-resource.
[Tool]
public partial class PinchosFila : Node2D
{
	private float _width = 32f;
	[Export(PropertyHint.Range, "8,512,1")]
	public float Width { get => _width; set { _width = Mathf.Max(8f, value); Refresh(); } }

	private float _height = 26f;
	// Tallest spike height, measured from the top of the stone plate.
	[Export(PropertyHint.Range, "6,128,1")]
	public float Height { get => _height; set { _height = Mathf.Max(6f, value); Refresh(); } }

	private int _count;
	// 0 = automatic (about one spike every 7 px of Width).
	[Export(PropertyHint.Range, "0,64,1")]
	public int Count { get => _count; set { _count = Mathf.Max(0, value); Refresh(); } }

	private int _seed = 1;
	[Export]
	public int Seed { get => _seed; set { _seed = value; Refresh(); } }

	private bool _backRow = true;
	[Export]
	public bool BackRow { get => _backRow; set { _backRow = value; Refresh(); } }

	private float _rust = 0.45f;
	[Export(PropertyHint.Range, "0,1,0.05")]
	public float Rust { get => _rust; set { _rust = value; Refresh(); } }

	private float _blood = 0.5f;
	[Export(PropertyHint.Range, "0,1,0.05")]
	public float Blood { get => _blood; set { _blood = value; Refresh(); } }

	private bool _hasBase = true;
	// Off for spikes that grow straight out of an existing floor/wall tile.
	[Export]
	public bool HasBase { get => _hasBase; set { _hasBase = value; Refresh(); } }

	private Color _metal = new(0.56f, 0.58f, 0.62f);
	[Export]
	public Color MetalColor { get => _metal; set { _metal = value; Refresh(); } }

	private Color _stone = new(0.30f, 0.29f, 0.32f);
	[Export]
	public Color StoneColor { get => _stone; set { _stone = value; Refresh(); } }

	private bool _tipGlow;
	// Faint glow on the tips so the danger still reads in near-black rooms (CanvasModulate ~0.05).
	[Export]
	public bool TipGlow { get => _tipGlow; set { _tipGlow = value; Refresh(); } }

	private Color _glowColor = new(0.4f, 1f, 0.55f);
	[Export]
	public Color GlowColor { get => _glowColor; set { _glowColor = value; Refresh(); } }

	private float _extension = 1f;
	// 0 = fully retracted (only the slots show), 1 = fully out. SpikeTrap drives this every frame,
	// so the setter only redraws.
	[Export(PropertyHint.Range, "0,1,0.01")]
	public float Extension { get => _extension; set { _extension = Mathf.Clamp(value, 0f, 1f); QueueRedraw(); } }

	private float _spikeInset;
	// Plate margin on each side with no spikes (lets the plate be wider than the deadly part).
	[Export(PropertyHint.Range, "0,64,1")]
	public float SpikeInset { get => _spikeInset; set { _spikeInset = Mathf.Max(0f, value); Refresh(); } }

	private NodePath _hazardShape = new();
	// CollisionShape2D to keep sized to the spikes. Empty = leave collision alone (SpikeTrap keeps
	// its own hand-tuned hazard rectangle).
	[Export]
	public NodePath HazardShape { get => _hazardShape; set { _hazardShape = value; Refresh(); } }

	private const float BaseHeight = 8f;
	private const float SocketDepth = 2f;

	private static readonly Color Outline = new(0.06f, 0.05f, 0.07f);
	private static readonly Color RustColor = new(0.45f, 0.22f, 0.10f);
	private static readonly Color BloodColor = new(0.38f, 0.03f, 0.04f);

	private float PlateTop => _hasBase ? -BaseHeight : 0f;

	public override void _Ready()
	{
		UpdateCollision();
		QueueRedraw();
	}

	private void Refresh()
	{
		QueueRedraw();
		if (IsInsideTree())
			UpdateCollision();
	}

	public override void _Draw()
	{
		// Taller spikes get proportionally thicker so a 60px stake isn't a needle.
		float thick = Mathf.Max(1f, _height / 26f);
		float span = Mathf.Max(4f, _width - _spikeInset * 2f);
		int count = _count > 0 ? _count : Mathf.Max(1, Mathf.RoundToInt(span / (7f * thick)));
		float step = span / count;
		float left = -span / 2f;

		// Each spike has its own RNG (seed + index), so the silhouette doesn't depend on draw order
		// or on how far the row is extended.
		if (_backRow)
		{
			for (int i = 0; i < count - 1; i++)
			{
				var rng = SpikeRng(1000 + i);
				float x = left + step * (i + 1) + rng.RandfRange(-step * 0.15f, step * 0.15f);
				float h = _height * rng.RandfRange(0.5f, 0.7f);
				float w = Mathf.Min(step * 0.8f, rng.RandfRange(4f, 6f) * thick);
				DrawSpike(rng, x, w, h, 0.45f, back: true);
			}
		}

		if (_hasBase)
			DrawPlate(SpikeRng(-1));

		for (int i = 0; i < count; i++)
		{
			var rng = SpikeRng(i);
			float x = left + step * (i + 0.5f) + rng.RandfRange(-step * 0.1f, step * 0.1f);
			// Every other spike is noticeably shorter so the row reads as a jagged crown, not a comb.
			float h = _height * (i % 2 == 0 ? rng.RandfRange(0.82f, 1f) : rng.RandfRange(0.6f, 0.82f));
			float w = Mathf.Min(step * 0.95f, rng.RandfRange(5f, 7.5f) * thick);
			DrawSpike(rng, x, w, h, 1f, back: false);
		}
	}

	private RandomNumberGenerator SpikeRng(int index) =>
		new() { Seed = (ulong)(uint)_seed * 7919UL + (ulong)(uint)(index + 1) * 104729UL };

	private void DrawSpike(RandomNumberGenerator rng, float x, float w, float h, float light, bool back)
	{
		// Grows out of the socket: below ~1px there's nothing to draw except the socket itself.
		h *= _extension;
		if (h < 1f)
		{
			if (!back && _hasBase)
				DrawSocket(x, w);
			return;
		}

		float baseY = PlateTop + (back ? 0f : SocketDepth);
		float lean = rng.RandfRange(-1.5f, 1.5f);
		var bl = Snap(new Vector2(x - w / 2f, baseY));
		var br = Snap(new Vector2(x + w / 2f, baseY));
		var bm = Snap(new Vector2(x - w * 0.1f, baseY));
		var tip = Snap(new Vector2(x + lean, PlateTop - h));
		// A slight shoulder 35% up gives the stake a forged, faceted look instead of a flat triangle.
		var shoulderL = Snap(bl.Lerp(tip, 0.35f) + new Vector2(-0.5f, 0f));
		var shoulderR = Snap(br.Lerp(tip, 0.35f) + new Vector2(0.5f, 0f));

		Color lit = (_metal * 1.25f).Clamp() * light;
		Color shade = _metal.Darkened(0.45f) * light;
		lit.A = shade.A = 1f;
		Color rustLit = lit.Lerp(RustColor * light, _rust);
		Color rustShade = shade.Lerp(RustColor.Darkened(0.4f) * light, _rust);
		rustLit.A = rustShade.A = 1f;

		DrawPolyline(new[] { bl, shoulderL, tip, shoulderR, br, bl }, Outline, 2f);

		// Lit (left) face and shadow (right) face, rust at the bottom fading into clean steel.
		DrawPolygon(new[] { bl, shoulderL, tip, bm },
			new[] { rustLit, lit.Lerp(rustLit, 0.3f), lit, rustLit });
		DrawPolygon(new[] { bm, tip, shoulderR, br },
			new[] { rustShade, shade, shade.Lerp(rustShade, 0.3f), rustShade });

		if (!back)
		{
			// Specular edge along the lit side, stopping short of the tip.
			var hiStart = Snap(bl.Lerp(tip, 0.15f) + new Vector2(1f, 0f));
			var hiEnd = Snap(bl.Lerp(tip, 0.8f) + new Vector2(0.5f, 0f));
			DrawLine(hiStart, hiEnd, new Color(1f, 1f, 1f, 0.55f), 1f);
			DrawRect(new Rect2(tip - new Vector2(0.5f, 0.5f), Vector2.One), new Color(1f, 1f, 1f, 0.3f));

			if (_blood > 0f && rng.Randf() < 0.35f + _blood * 0.6f)
				DrawBlood(rng, tip, bl, br);

			// Socket the stake is driven into.
			if (_hasBase)
				DrawSocket(x, w);
		}

		if (_tipGlow && !back)
		{
			// Neon rim on both edges of the tip, fading out down the stake.
			var hot = _glowColor;
			var cold = _glowColor;
			cold.A = 0f;
			var leftEnd = Snap(tip.Lerp(bl, 0.4f));
			var rightEnd = Snap(tip.Lerp(br, 0.4f));
			DrawPolyline(new[] { leftEnd, tip, rightEnd }, new Color(hot, 0.25f), 3f);
			DrawPolylineColors(new[] { leftEnd, tip, rightEnd }, new[] { cold, hot, cold }, 1f);
		}
	}

	private void DrawBlood(RandomNumberGenerator rng, Vector2 tip, Vector2 bl, Vector2 br)
	{
		// Thin coat on the upper part of the stake, plus one or two drips running further down.
		float coat = rng.RandfRange(0.25f, 0.45f) * (0.5f + _blood);
		var a = Snap(tip.Lerp(bl, coat));
		var b = Snap(tip.Lerp(br, coat));
		var edge = BloodColor;
		edge.A = 0f;
		var solid = BloodColor;
		solid.A = 0.9f;
		DrawPolygon(new[] { tip, b, a }, new[] { solid, edge.Lerp(solid, 0.8f), edge.Lerp(solid, 0.8f) });

		int drips = rng.RandiRange(1, 2);
		for (int i = 0; i < drips; i++)
		{
			float t = rng.RandfRange(0.15f, 0.85f);
			var start = Snap(a.Lerp(b, t));
			float len = rng.RandfRange(1.5f, 3.5f) * (0.5f + _blood);
			var end = Snap(start + (bl.Lerp(br, t) - start).Normalized() * len);
			DrawLine(start, end, edge.Lerp(solid, 0.75f), 1f);
			DrawRect(new Rect2(end - new Vector2(0.5f, 0f), Vector2.One), solid);
		}
	}

	private void DrawPlate(RandomNumberGenerator rng)
	{
		float left = -_width / 2f;
		var plate = new Rect2(left, -BaseHeight, _width, BaseHeight);
		DrawRect(plate.Grow(1f), Outline);
		DrawRect(plate, _stone);
		// Bevel: lit top edge, dark bottom edge.
		DrawRect(new Rect2(left, -BaseHeight, _width, 1f), _stone.Lightened(0.3f));
		DrawRect(new Rect2(left, -2f, _width, 2f), _stone.Darkened(0.35f));

		// Two staggered courses of blocks: each block gets its own slight tone, a dark seam on
		// its left and a 1px catch-light right after it.
		float mid = -BaseHeight / 2f;
		DrawLine(new Vector2(left, mid), new Vector2(left + _width, mid), _stone.Darkened(0.5f), 1f);
		for (int course = 0; course < 2; course++)
		{
			float top = course == 0 ? -BaseHeight + 1f : mid + 0.5f;
			float bottom = course == 0 ? mid - 0.5f : -2f;
			float x = left + (course == 0 ? 0f : rng.RandfRange(3f, 7f));
			while (x < left + _width - 3f)
			{
				float next = Mathf.Min(left + _width, x + rng.RandfRange(8f, 14f));
				var tone = rng.Randf() < 0.5f ? _stone.Lightened(rng.RandfRange(0f, 0.08f)) : _stone.Darkened(rng.RandfRange(0f, 0.12f));
				DrawRect(new Rect2(x + 1f, top, next - x - 1f, bottom - top), tone);
				if (x > left)
				{
					float sx = Mathf.Round(x);
					DrawLine(new Vector2(sx, top), new Vector2(sx, bottom), _stone.Darkened(0.55f), 1f);
					DrawLine(new Vector2(sx + 1f, top), new Vector2(sx + 1f, bottom), _stone.Lightened(0.15f), 1f);
				}
				x = next;
			}
		}

		// A few hairline cracks.
		int cracks = Mathf.Max(1, Mathf.RoundToInt(_width / 24f));
		for (int i = 0; i < cracks; i++)
		{
			var p = Snap(new Vector2(rng.RandfRange(left + 2f, left + _width - 4f), -BaseHeight + 2f));
			var q = Snap(p + new Vector2(rng.RandfRange(-2f, 2f), 2f));
			var r = Snap(q + new Vector2(rng.RandfRange(-2f, 3f), 1f));
			DrawPolyline(new[] { p, q, r }, _stone.Darkened(0.5f), 1f);
		}

		if (_blood > 0.3f)
		{
			// Dried splatter on the plate under the stakes.
			int spots = Mathf.RoundToInt(_width / 20f * _blood);
			var c = BloodColor;
			c.A = 0.7f;
			for (int i = 0; i < spots; i++)
			{
				var p = Snap(new Vector2(rng.RandfRange(left + 2f, left + _width - 3f), -BaseHeight + rng.RandfRange(0f, 1f)));
				DrawRect(new Rect2(p, new Vector2(rng.RandiRange(1, 3), 1f)), c);
			}
		}
	}

	private void DrawSocket(float x, float w)
	{
		float l = Mathf.Round(x - w / 2f) - 1f;
		float r = Mathf.Round(x + w / 2f) + 1f;
		DrawRect(new Rect2(l, PlateTop, r - l, SocketDepth + 1f), Outline);
		DrawLine(new Vector2(l, PlateTop + SocketDepth + 1f), new Vector2(r, PlateTop + SocketDepth + 1f),
			_stone.Lightened(0.25f), 1f);
	}

	private void UpdateCollision()
	{
		if (_hazardShape.IsEmpty)
			return;
		var shapeNode = GetNodeOrNull<CollisionShape2D>(_hazardShape);
		if (shapeNode is null)
			return;

		// Slightly forgiving: a bit narrower than the row and only ~80% of the tallest spike, so
		// grazing a tip pixel doesn't count as a hit.
		var size = new Vector2(Mathf.Max(4f, _width - _spikeInset * 2f - 4f), _height * 0.8f);
		var center = Position + new Vector2(0f, PlateTop - size.Y / 2f);
		if (shapeNode.Shape is RectangleShape2D current && current.Size.IsEqualApprox(size)
			&& shapeNode.Position.IsEqualApprox(center))
			return;

		shapeNode.Shape = new RectangleShape2D { Size = size };
		shapeNode.Position = center;
	}

	private static Vector2 Snap(Vector2 v) => new(Mathf.Round(v.X * 2f) / 2f, Mathf.Round(v.Y * 2f) / 2f);
}
