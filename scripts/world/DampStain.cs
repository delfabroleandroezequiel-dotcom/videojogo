using Godot;

namespace Metroidvania.World;

// Procedural damp stain — several jittered concentric polygons layered at decreasing opacity, so
// the edge fades instead of cutting off hard (no shader/blur available from a plain
// DrawColoredPolygon). Purely cosmetic, no collision.
[Tool]
public partial class DampStain : Node2D
{
	private float _radius = 40f;

	[Export(PropertyHint.Range, "6,300,1")]
	public float Radius
	{
		get => _radius;
		set { _radius = Mathf.Max(6f, value); QueueRedraw(); }
	}

	private int _pointCount = 10;

	[Export(PropertyHint.Range, "5,24,1")]
	public int PointCount
	{
		get => _pointCount;
		set { _pointCount = Mathf.Max(5, value); QueueRedraw(); }
	}

	private int _layerCount = 3;

	// More layers = softer-looking fade toward the edge.
	[Export(PropertyHint.Range, "1,6,1")]
	public int LayerCount
	{
		get => _layerCount;
		set { _layerCount = Mathf.Max(1, value); QueueRedraw(); }
	}

	private float _jitterAmount = 0.25f;

	[Export(PropertyHint.Range, "0,0.6,0.01")]
	public float JitterAmount
	{
		get => _jitterAmount;
		set { _jitterAmount = Mathf.Clamp(value, 0f, 0.6f); QueueRedraw(); }
	}

	private int _seed = 1;

	[Export]
	public int Seed
	{
		get => _seed;
		set { _seed = value; QueueRedraw(); }
	}

	private Color _stainColor = new(0.1f, 0.12f, 0.1f, 0.35f);

	// Alpha is per-layer — the innermost, most-opaque layer ends up roughly LayerCount times this.
	[Export]
	public Color StainColor
	{
		get => _stainColor;
		set { _stainColor = value; QueueRedraw(); }
	}

	public override void _Ready() => QueueRedraw();

	public override void _Draw()
	{
		var rng = new RandomNumberGenerator { Seed = (ulong)_seed };
		for (int layer = 0; layer < _layerCount; layer++)
		{
			float layerRadius = _radius * (1f - (float)layer / _layerCount * 0.5f);
			var points = new Vector2[_pointCount];
			for (int i = 0; i < _pointCount; i++)
			{
				float angle = Mathf.Tau * i / _pointCount;
				float r = layerRadius * (1f + rng.RandfRange(-_jitterAmount, _jitterAmount));
				points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
			}
			DrawColoredPolygon(points, _stainColor);
		}
	}
}
