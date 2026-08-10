using Godot;

namespace Metroidvania.World;

// Procedural moss clump — a scatter of overlapping small circles instead of a single blob, so the
// edge reads as clumpy/organic rather than a smooth stain. Purely cosmetic, no collision.
[Tool]
public partial class MossPatch : Node2D
{
	private float _radius = 24f;

	[Export(PropertyHint.Range, "4,150,1")]
	public float Radius
	{
		get => _radius;
		set { _radius = Mathf.Max(4f, value); QueueRedraw(); }
	}

	private int _clumpCount = 14;

	[Export(PropertyHint.Range, "2,60,1")]
	public int ClumpCount
	{
		get => _clumpCount;
		set { _clumpCount = Mathf.Max(2, value); QueueRedraw(); }
	}

	private float _minCircleRadius = 3f;

	[Export(PropertyHint.Range, "1,40,1")]
	public float MinCircleRadius
	{
		get => _minCircleRadius;
		set { _minCircleRadius = Mathf.Max(1f, value); QueueRedraw(); }
	}

	private float _maxCircleRadius = 8f;

	[Export(PropertyHint.Range, "1,60,1")]
	public float MaxCircleRadius
	{
		get => _maxCircleRadius;
		set { _maxCircleRadius = Mathf.Max(1f, value); QueueRedraw(); }
	}

	private int _seed = 1;

	[Export]
	public int Seed
	{
		get => _seed;
		set { _seed = value; QueueRedraw(); }
	}

	private Color _mossColor = new(0.16f, 0.32f, 0.14f, 0.85f);

	[Export]
	public Color MossColor
	{
		get => _mossColor;
		set { _mossColor = value; QueueRedraw(); }
	}

	public override void _Ready() => QueueRedraw();

	public override void _Draw()
	{
		var rng = new RandomNumberGenerator { Seed = (ulong)_seed };
		for (int i = 0; i < _clumpCount; i++)
		{
			float distance = rng.RandfRange(0f, _radius);
			float angle = rng.RandfRange(0f, Mathf.Tau);
			Vector2 center = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
			float circleRadius = rng.RandfRange(_minCircleRadius, Mathf.Max(_minCircleRadius, _maxCircleRadius));
			DrawCircle(center, circleRadius, _mossColor);
		}
	}
}
