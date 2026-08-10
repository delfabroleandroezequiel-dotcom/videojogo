using Godot;
using System.Linq;

namespace Metroidvania.World;

// Procedural rock pile — a cluster of irregular polygons (jagged, not round like MossPatch)
// scattered across a floor-hugging ellipse, drawn back-to-front by vertical position so rocks
// lower on screen overlap the ones behind them. Purely cosmetic, no collision — for actual
// climbable/blocking rubble use real collision geometry instead.
[Tool]
public partial class RockPile : Node2D
{
	private float _width = 60f;

	[Export(PropertyHint.Range, "10,400,1")]
	public float Width
	{
		get => _width;
		set { _width = Mathf.Max(10f, value); QueueRedraw(); }
	}

	private float _height = 24f;

	[Export(PropertyHint.Range, "6,200,1")]
	public float Height
	{
		get => _height;
		set { _height = Mathf.Max(6f, value); QueueRedraw(); }
	}

	private int _rockCount = 8;

	[Export(PropertyHint.Range, "1,40,1")]
	public int RockCount
	{
		get => _rockCount;
		set { _rockCount = Mathf.Max(1, value); QueueRedraw(); }
	}

	private float _minRockSize = 6f;

	[Export(PropertyHint.Range, "2,80,1")]
	public float MinRockSize
	{
		get => _minRockSize;
		set { _minRockSize = Mathf.Max(2f, value); QueueRedraw(); }
	}

	private float _maxRockSize = 16f;

	[Export(PropertyHint.Range, "2,120,1")]
	public float MaxRockSize
	{
		get => _maxRockSize;
		set { _maxRockSize = Mathf.Max(2f, value); QueueRedraw(); }
	}

	private float _edgeJitter = 0.3f;

	// How lumpy/angular each rock's outline is — 0 reads as a smooth blob, higher as jagged stone.
	[Export(PropertyHint.Range, "0,0.6,0.01")]
	public float EdgeJitter
	{
		get => _edgeJitter;
		set { _edgeJitter = Mathf.Clamp(value, 0f, 0.6f); QueueRedraw(); }
	}

	private int _seed = 1;

	[Export]
	public int Seed
	{
		get => _seed;
		set { _seed = value; QueueRedraw(); }
	}

	private Color _rockColor = new(0.14f, 0.14f, 0.15f, 1f);

	[Export]
	public Color RockColor
	{
		get => _rockColor;
		set { _rockColor = value; QueueRedraw(); }
	}

	private float _colorVariance = 0.15f;

	// Per-rock random shade offset from RockColor, so the pile doesn't read as one flat cutout.
	[Export(PropertyHint.Range, "0,0.5,0.01")]
	public float ColorVariance
	{
		get => _colorVariance;
		set { _colorVariance = Mathf.Clamp(value, 0f, 0.5f); QueueRedraw(); }
	}

	public override void _Ready() => QueueRedraw();

	public override void _Draw()
	{
		var rng = new RandomNumberGenerator { Seed = (ulong)_seed };

		var rocks = new (Vector2 center, float size)[_rockCount];
		for (int i = 0; i < _rockCount; i++)
		{
			float px = rng.RandfRange(-_width / 2f, _width / 2f);
			float py = rng.RandfRange(-_height / 2f, _height / 2f);
			float size = rng.RandfRange(_minRockSize, Mathf.Max(_minRockSize, _maxRockSize));
			rocks[i] = (new Vector2(px, py), size);
		}

		// Lower rocks (bigger Y) drawn last so they overlap the ones "behind" them, same painter's-
		// algorithm trick a real 2D pile would need.
		foreach ((Vector2 center, float size) in rocks.OrderBy(rock => rock.center.Y))
			DrawRock(center, size, rng);
	}

	private void DrawRock(Vector2 center, float size, RandomNumberGenerator rng)
	{
		int pointCount = rng.RandiRange(6, 9);
		var points = new Vector2[pointCount];
		for (int i = 0; i < pointCount; i++)
		{
			float angle = Mathf.Tau * i / pointCount;
			float r = size * (1f + rng.RandfRange(-_edgeJitter, _edgeJitter));
			// Flattened vertically so each rock reads as a squat stone sitting on the ground
			// rather than a perfect jittered circle.
			points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.6f) * r;
		}

		float shade = 1f + rng.RandfRange(-_colorVariance, _colorVariance);
		var color = new Color(
			Mathf.Clamp(_rockColor.R * shade, 0f, 1f),
			Mathf.Clamp(_rockColor.G * shade, 0f, 1f),
			Mathf.Clamp(_rockColor.B * shade, 0f, 1f),
			_rockColor.A);
		DrawColoredPolygon(points, color);
	}
}
