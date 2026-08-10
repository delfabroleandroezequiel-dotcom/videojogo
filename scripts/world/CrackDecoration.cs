using Godot;

namespace Metroidvania.World;

// Procedural crack decoration — a random-walk main line (small angle jitter each step) with a
// chance to spawn shorter branch cracks off it, same jittered-line idea CobwebDecoration uses for
// spokes, just walked instead of radial. Purely cosmetic, no collision.
[Tool]
public partial class CrackDecoration : Node2D
{
	private float _length = 60f;

	[Export(PropertyHint.Range, "10,400,1")]
	public float Length
	{
		get => _length;
		set { _length = Mathf.Max(10f, value); QueueRedraw(); }
	}

	private float _startAngleDegrees;

	[Export(PropertyHint.Range, "0,360,1")]
	public float StartAngleDegrees
	{
		get => _startAngleDegrees;
		set { _startAngleDegrees = value; QueueRedraw(); }
	}

	private float _wobbleDegrees = 18f;

	// Max random turn per segment step — higher reads as a jagged lightning-bolt crack, lower as
	// a straighter hairline one.
	[Export(PropertyHint.Range, "0,60,1")]
	public float WobbleDegrees
	{
		get => _wobbleDegrees;
		set { _wobbleDegrees = Mathf.Max(0f, value); QueueRedraw(); }
	}

	private int _segmentCount = 8;

	[Export(PropertyHint.Range, "2,30,1")]
	public int SegmentCount
	{
		get => _segmentCount;
		set { _segmentCount = Mathf.Max(2, value); QueueRedraw(); }
	}

	private float _branchChance = 0.25f;

	[Export(PropertyHint.Range, "0,1,0.05")]
	public float BranchChance
	{
		get => _branchChance;
		set { _branchChance = Mathf.Clamp(value, 0f, 1f); QueueRedraw(); }
	}

	private int _seed = 1;

	[Export]
	public int Seed
	{
		get => _seed;
		set { _seed = value; QueueRedraw(); }
	}

	private Color _crackColor = new(0.05f, 0.05f, 0.05f, 0.6f);

	[Export]
	public Color CrackColor
	{
		get => _crackColor;
		set { _crackColor = value; QueueRedraw(); }
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
		DrawBranch(Vector2.Zero, Mathf.DegToRad(_startAngleDegrees), _length, _segmentCount, _lineWidth, rng);
	}

	private void DrawBranch(Vector2 origin, float angle, float length, int segments, float width, RandomNumberGenerator rng)
	{
		Vector2 current = origin;
		float segmentLength = length / segments;
		for (int i = 0; i < segments; i++)
		{
			angle += Mathf.DegToRad(rng.RandfRange(-_wobbleDegrees, _wobbleDegrees));
			Vector2 next = current + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * segmentLength;
			DrawLine(current, next, _crackColor, width);

			if (i > 0 && i < segments - 1 && rng.Randf() < _branchChance)
			{
				float branchAngle = angle + Mathf.DegToRad(rng.RandfRange(30f, 70f) * (rng.Randf() < 0.5f ? 1f : -1f));
				DrawBranch(next, branchAngle, length * 0.4f, Mathf.Max(2, segments / 2), width * 0.7f, rng);
			}

			current = next;
		}
	}
}
