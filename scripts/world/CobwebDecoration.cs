using Godot;

namespace Metroidvania.World;

// Purely cosmetic cobweb — no collision, no hazard, just DrawLine spokes + concentric rings drawn
// straight from code instead of a sprite, since a few thin lines don't need source art. Radius
// jitters per spoke (JitterAmount, seeded by Seed) so it doesn't read as a mechanical perfect
// circle. Defaults to a full 360° web; drop ArcSpanDegrees to ~90 and rotate the node to hug a
// cave corner instead of hanging free.
// [Tool] so it redraws live in the editor while tuning it.
[Tool]
public partial class CobwebDecoration : Node2D
{
	private float _radius = 50f;

	[Export(PropertyHint.Range, "10,400,1")]
	public float Radius
	{
		get => _radius;
		set { _radius = Mathf.Max(10f, value); QueueRedraw(); }
	}

	private int _spokeCount = 7;

	[Export(PropertyHint.Range, "4,16,1")]
	public int SpokeCount
	{
		get => _spokeCount;
		set { _spokeCount = Mathf.Max(4, value); QueueRedraw(); }
	}

	private int _ringCount = 3;

	[Export(PropertyHint.Range, "1,8,1")]
	public int RingCount
	{
		get => _ringCount;
		set { _ringCount = Mathf.Max(1, value); QueueRedraw(); }
	}

	private float _arcStartDegrees;

	[Export(PropertyHint.Range, "0,360,1")]
	public float ArcStartDegrees
	{
		get => _arcStartDegrees;
		set { _arcStartDegrees = value; QueueRedraw(); }
	}

	private float _arcSpanDegrees = 360f;

	// 360 = free-hanging web, ~90 = wedged into a wall/ceiling corner.
	[Export(PropertyHint.Range, "30,360,1")]
	public float ArcSpanDegrees
	{
		get => _arcSpanDegrees;
		set { _arcSpanDegrees = Mathf.Clamp(value, 30f, 360f); QueueRedraw(); }
	}

	private int _seed = 1;

	[Export]
	public int Seed
	{
		get => _seed;
		set { _seed = value; QueueRedraw(); }
	}

	private float _jitterAmount = 0.12f;

	// Fraction of Radius each spoke randomly varies by.
	[Export(PropertyHint.Range, "0,0.4,0.01")]
	public float JitterAmount
	{
		get => _jitterAmount;
		set { _jitterAmount = Mathf.Clamp(value, 0f, 0.4f); QueueRedraw(); }
	}

	private Color _threadColor = new(0.82f, 0.82f, 0.8f, 0.75f);

	[Export]
	public Color ThreadColor
	{
		get => _threadColor;
		set { _threadColor = value; QueueRedraw(); }
	}

	private float _lineWidth = 1f;

	[Export(PropertyHint.Range, "0.1,4,0.1")]
	public float LineWidth
	{
		get => _lineWidth;
		set { _lineWidth = Mathf.Max(0.1f, value); QueueRedraw(); }
	}

	public override void _Ready() => QueueRedraw();

	public override void _Draw()
	{
		var rng = new RandomNumberGenerator { Seed = (ulong)_seed };
		bool isFullCircle = _arcSpanDegrees >= 359.9f;
		float startRad = Mathf.DegToRad(_arcStartDegrees);
		float spanRad = Mathf.DegToRad(_arcSpanDegrees);
		int steps = isFullCircle ? _spokeCount : _spokeCount - 1;

		var tips = new Vector2[_spokeCount];
		for (int i = 0; i < _spokeCount; i++)
		{
			float angle = startRad + spanRad * i / steps;
			float r = _radius * (1f + rng.RandfRange(-_jitterAmount, _jitterAmount));
			tips[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
			DrawLine(Vector2.Zero, tips[i], _threadColor, _lineWidth);
		}

		int ringSegments = isFullCircle ? _spokeCount : _spokeCount - 1;
		for (int ring = 1; ring <= _ringCount; ring++)
		{
			float f = (float)ring / (_ringCount + 1);
			for (int i = 0; i < ringSegments; i++)
			{
				Vector2 a = tips[i] * f;
				Vector2 b = tips[(i + 1) % _spokeCount] * f;
				DrawLine(a, b, _threadColor, _lineWidth * 0.85f);
			}
		}
	}
}
