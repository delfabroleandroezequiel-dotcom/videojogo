using Godot;

namespace Metroidvania.World;

// A flexible multi-segment rope simulated with Verlet integration + distance constraints —
// point[0] is pinned to this node's position (the anchor); the free end (PointCount-1) is always
// pinned to a fixed sine sweep (MaxSwingAngleDegrees / SwingFrequency), whether or not a player is
// riding it — grabbing on has zero effect on the rope's own motion, by design: it just swings
// side to side forever, on its own schedule, and Player reads EndPosition/EndVelocity each frame
// to go along for the ride. Every point in between still free-falls/constrains between those two
// fixed ends, so the body still visibly sags/curves even though both endpoints are pinned.
public partial class Rope : Node2D
{
	[Export] public float Length = 200f;
	[Export] public int SegmentCount = 12;
	[Export] public float Gravity = 900f;
	[Export] public int ConstraintIterations = 12;

	// How far the idle sweep swings from vertical, each direction, and how fast — constant and
	// repeating forever by construction (a plain sine, not a physical simulation), so it's exactly
	// as reliable to time a jump against as tuning it to almost reach a neighboring rope: at the
	// peak, the tip sits Length*sin(MaxSwingAngleDegrees) away from straight-down.
	[Export] public float MaxSwingAngleDegrees = 35f;
	[Export] public float SwingFrequency = 1f;
	[Export] public float PhaseOffset;

	[Export] public float VisualWidth = 5f;
	[Export] public Texture2D RopeTexture;

	// The source art is a bright golden tan that reads too warm/loud against a dark cave — this
	// multiplies it down instead of editing the source PNGs, so it stays easy to re-tune per rope.
	[Export] public Color TintColor = new(0.55f, 0.55f, 0.55f, 1f);

	// The frayed knotted end (ropeabajo.png) — rendered at the free end, oriented with the last
	// segment. Optional: leave null for a plain cut-off tiled end.
	[Export] public Texture2D BottomCapTexture;
	[Export] public float BottomCapVisualWidth = 9f;

	// Both source sheets have a lot of transparent padding around the actual rope art, same idea
	// as Ladder's TileSourceRect — cropped from the actual rope.png / ropeabajo.png dimensions.
	private static readonly Rect2 TileSourceRect = new(386, 26, 252, 1488);
	private static readonly Rect2 BottomCapSourceRect = new(312, 126, 456, 1232);

	private int PointCount => SegmentCount + 1;
	private float SegmentLength => Length / SegmentCount;

	private Vector2[] _points;
	private Vector2[] _oldPoints;
	private float _time;

	public Vector2 EndPosition => _points[PointCount - 1];
	public Vector2 EndVelocity => (_points[PointCount - 1] - _oldPoints[PointCount - 1]) / FixedDelta;

	private static float FixedDelta => 1f / Engine.PhysicsTicksPerSecond;

	private Area2D _grabZone;
	private Sprite2D[] _segmentSprites;
	private Sprite2D _capSprite;
	private Line2D _fallbackLine;
	private float _segmentSourceHeight;

	public override void _Ready()
	{
		_grabZone = GetNode<Area2D>("GrabZone");
		_grabZone.BodyEntered += OnBodyEntered;
		_grabZone.BodyExited += OnBodyExited;

		_points = new Vector2[PointCount];
		_oldPoints = new Vector2[PointCount];
		Vector2 startDir = new Vector2(0f, 1f).Rotated(SweepAngleAt(0f));
		for (int i = 0; i < PointCount; i++)
		{
			_points[i] = GlobalPosition + startDir * (i * SegmentLength);
			_oldPoints[i] = _points[i];
		}

		BuildVisual();
		UpdateVisual();
	}

	private void OnBodyEntered(Node2D body)
	{
		if (body is Metroidvania.Player.Player player)
			player.EnterRope(this);
	}

	private void OnBodyExited(Node2D body)
	{
		if (body is Metroidvania.Player.Player player)
			player.ExitRope(this);
	}

	// The idle sweep's current angle from straight-down — a plain sine, so it's the same
	// unchanging, repeatable motion every time, easy to aim at a neighboring rope via
	// MaxSwingAngleDegrees rather than something that has to "happen to" reach far enough.
	private float SweepAngleAt(float time) => Mathf.DegToRad(MaxSwingAngleDegrees) * Mathf.Sin(time * SwingFrequency + PhaseOffset);

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		_time += dt;

		int end = PointCount - 1;

		// Both ends are pinned (anchor + sweep) every step regardless of whether a player is
		// riding the tip — only the interior points actually simulate.
		for (int i = 1; i < end; i++)
		{
			Vector2 velocity = _points[i] - _oldPoints[i];
			Vector2 acceleration = new(0f, Gravity);
			Vector2 newPos = _points[i] + velocity + acceleration * dt * dt;
			_oldPoints[i] = _points[i];
			_points[i] = newPos;
		}

		_points[0] = GlobalPosition;
		_oldPoints[end] = _points[end];
		_points[end] = GlobalPosition + new Vector2(0f, Length).Rotated(SweepAngleAt(_time));

		for (int iter = 0; iter < ConstraintIterations; iter++)
		{
			for (int i = 0; i < PointCount - 1; i++)
				SatisfySegment(i, i + 1);
			_points[0] = GlobalPosition;
			_points[end] = GlobalPosition + new Vector2(0f, Length).Rotated(SweepAngleAt(_time));
		}

		UpdateVisual();
	}

	private void SatisfySegment(int a, int b)
	{
		Vector2 diff = _points[b] - _points[a];
		float dist = diff.Length();
		if (dist < 0.0001f)
			return;

		Vector2 correction = diff * ((dist - SegmentLength) / dist);
		if (a == 0)
		{
			_points[b] -= correction;
		}
		else
		{
			_points[a] += correction * 0.5f;
			_points[b] -= correction * 0.5f;
		}
	}

	private void UpdateVisual()
	{
		// The grab zone has to chase the simulated end point every step — it used to just sit at
		// a fixed local offset back when the whole rope was one rigid rotating body, but now the
		// tip genuinely moves around in the parent's space as the chain swings/sags.
		_grabZone.Position = ToLocal(EndPosition);

		if (_segmentSprites is null)
			return;

		for (int i = 0; i < _segmentSprites.Length; i++)
		{
			Vector2 aLocal = ToLocal(_points[i]);
			Vector2 bLocal = ToLocal(_points[i + 1]);
			Vector2 segDir = bLocal - aLocal;
			float segLen = Mathf.Max(0.01f, segDir.Length());

			Sprite2D sprite = _segmentSprites[i];
			sprite.Position = aLocal;
			sprite.Rotation = segDir.Angle() - Mathf.Pi / 2f;
			sprite.Scale = new Vector2(sprite.Scale.X, segLen / _segmentSourceHeight);
		}

		if (_fallbackLine is not null)
		{
			var linePoints = new Vector2[PointCount];
			for (int i = 0; i < PointCount; i++)
				linePoints[i] = ToLocal(_points[i]);
			_fallbackLine.Points = linePoints;
		}

		if (_capSprite is not null)
		{
			Vector2 aLocal = ToLocal(_points[^2]);
			Vector2 bLocal = ToLocal(_points[^1]);
			_capSprite.Position = bLocal;
			_capSprite.Rotation = (bLocal - aLocal).Angle() - Mathf.Pi / 2f;
		}
	}

	private void BuildVisual()
	{
		if (GetNodeOrNull<Node2D>("Visual") is Node2D existing)
			existing.QueueFree();

		var visual = new Node2D { Name = "Visual" };
		AddChild(visual);

		if (RopeTexture is null)
		{
			// Greybox fallback so the rope is visible and testable before the real art exists.
			_fallbackLine = new Line2D
			{
				Width = VisualWidth * 0.4f,
				DefaultColor = new Color(0.4f, 0.3f, 0.2f, 1f),
			};
			visual.AddChild(_fallbackLine);
			return;
		}

		// Uniform scale (same factor on both axes) so the crop's own proportions are preserved —
		// then the source region height is picked so that ONE segment, at that same scale, comes
		// out to SegmentLength tall. Without this, stretching the full multi-twist crop down to a
		// single short segment would squash 8 twists into one, unrecognizable.
		float scale = VisualWidth / TileSourceRect.Size.X;
		_segmentSourceHeight = Mathf.Min(SegmentLength / scale, TileSourceRect.Size.Y);
		var segmentRegion = new Rect2(TileSourceRect.Position, new Vector2(TileSourceRect.Size.X, _segmentSourceHeight));

		_segmentSprites = new Sprite2D[SegmentCount];
		for (int i = 0; i < SegmentCount; i++)
		{
			var sprite = new Sprite2D
			{
				Texture = RopeTexture,
				RegionEnabled = true,
				RegionRect = segmentRegion,
				Centered = false,
				Offset = new Vector2(-TileSourceRect.Size.X * 0.5f, 0f),
				Scale = new Vector2(scale, scale),
				Modulate = TintColor,
			};
			visual.AddChild(sprite);
			_segmentSprites[i] = sprite;
		}

		if (BottomCapTexture is not null)
		{
			float capScale = BottomCapVisualWidth / BottomCapSourceRect.Size.X;
			_capSprite = new Sprite2D
			{
				Texture = BottomCapTexture,
				RegionEnabled = true,
				RegionRect = BottomCapSourceRect,
				Centered = false,
				Offset = new Vector2(-BottomCapSourceRect.Size.X * 0.5f, -BottomCapSourceRect.Size.Y * 0.15f),
				Scale = new Vector2(capScale, capScale),
				Modulate = TintColor,
			};
			visual.AddChild(_capSprite);
		}
	}
}
