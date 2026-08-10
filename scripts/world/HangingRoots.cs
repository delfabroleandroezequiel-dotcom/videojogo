using Godot;

namespace Metroidvania.World;

// Procedural hanging roots — several downward strands from a ceiling anchor point, each a
// sway-wobbled line (same per-segment jitter idea CrackDecoration uses, but biased straight down)
// with a chance of a short side tendril. Purely cosmetic, no collision.
[Tool]
public partial class HangingRoots : Node2D
{
	private int _rootCount = 4;

	[Export(PropertyHint.Range, "1,12,1")]
	public int RootCount
	{
		get => _rootCount;
		set { _rootCount = Mathf.Max(1, value); QueueRedraw(); }
	}

	private float _spread = 40f;

	// Horizontal distance the roots' anchor points spread out along, before hanging down.
	[Export(PropertyHint.Range, "0,200,1")]
	public float Spread
	{
		get => _spread;
		set { _spread = Mathf.Max(0f, value); QueueRedraw(); }
	}

	private float _length = 70f;

	[Export(PropertyHint.Range, "10,400,1")]
	public float Length
	{
		get => _length;
		set { _length = Mathf.Max(10f, value); QueueRedraw(); }
	}

	private float _lengthVariance = 0.3f;

	[Export(PropertyHint.Range, "0,0.8,0.01")]
	public float LengthVariance
	{
		get => _lengthVariance;
		set { _lengthVariance = Mathf.Clamp(value, 0f, 0.8f); QueueRedraw(); }
	}

	private float _swayDegrees = 10f;

	[Export(PropertyHint.Range, "0,45,1")]
	public float SwayDegrees
	{
		get => _swayDegrees;
		set { _swayDegrees = Mathf.Max(0f, value); QueueRedraw(); }
	}

	private int _segmentCount = 6;

	[Export(PropertyHint.Range, "2,20,1")]
	public int SegmentCount
	{
		get => _segmentCount;
		set { _segmentCount = Mathf.Max(2, value); QueueRedraw(); }
	}

	private float _tendrilChance = 0.2f;

	[Export(PropertyHint.Range, "0,1,0.05")]
	public float TendrilChance
	{
		get => _tendrilChance;
		set { _tendrilChance = Mathf.Clamp(value, 0f, 1f); QueueRedraw(); }
	}

	private int _seed = 1;

	[Export]
	public int Seed
	{
		get => _seed;
		set { _seed = value; QueueRedraw(); }
	}

	private Color _rootColor = new(0.28f, 0.2f, 0.12f, 0.85f);

	[Export]
	public Color RootColor
	{
		get => _rootColor;
		set { _rootColor = value; QueueRedraw(); }
	}

	private float _lineWidth = 1.5f;

	[Export(PropertyHint.Range, "0.1,6,0.1")]
	public float LineWidth
	{
		get => _lineWidth;
		set { _lineWidth = Mathf.Max(0.1f, value); QueueRedraw(); }
	}

	public override void _Ready() => QueueRedraw();

	public override void _Draw()
	{
		var rng = new RandomNumberGenerator { Seed = (ulong)_seed };
		for (int r = 0; r < _rootCount; r++)
		{
			float startX = _rootCount == 1 ? 0f : Mathf.Lerp(-_spread / 2f, _spread / 2f, (float)r / (_rootCount - 1));
			var origin = new Vector2(startX, 0f);
			float rootLength = _length * (1f + rng.RandfRange(-_lengthVariance, _lengthVariance));
			DrawStrand(origin, 90f, rootLength, _segmentCount, _lineWidth, rng, allowTendril: true);
		}
	}

	private void DrawStrand(Vector2 origin, float baseAngleDegrees, float length, int segments, float width, RandomNumberGenerator rng, bool allowTendril)
	{
		Vector2 current = origin;
		float angle = Mathf.DegToRad(baseAngleDegrees);
		float segmentLength = length / segments;
		for (int i = 0; i < segments; i++)
		{
			angle += Mathf.DegToRad(rng.RandfRange(-_swayDegrees, _swayDegrees));
			Vector2 next = current + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * segmentLength;
			DrawLine(current, next, _rootColor, width);

			if (allowTendril && i > 1 && i < segments - 1 && rng.Randf() < _tendrilChance)
			{
				float tendrilAngle = Mathf.RadToDeg(angle) + rng.RandfRange(20f, 50f) * (rng.Randf() < 0.5f ? 1f : -1f);
				DrawStrand(next, tendrilAngle, length * 0.35f, Mathf.Max(2, segments / 2), width * 0.7f, rng, allowTendril: false);
			}

			current = next;
		}
	}
}
